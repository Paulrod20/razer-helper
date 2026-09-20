using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>How a "free up the dedicated GPU" attempt ended.</summary>
internal enum DgpuFreeUpOutcome
{
    /// <summary>Apps were asked to close.</summary>
    Closed,

    /// <summary>There is no dedicated GPU.</summary>
    NoDedicatedGpu,

    /// <summary>An external display is (or might be) connected, so the GPU stays on whatever is closed.</summary>
    ExternalDisplay,

    /// <summary>Nothing eligible is using the dedicated GPU.</summary>
    NothingToClose,

    /// <summary>The user chose not to close anything.</summary>
    Declined,

    /// <summary>Things changed while the question was open (charger back, display connected, apps gone), so nothing was closed.</summary>
    ConditionsChanged,

    /// <summary>Another attempt is already in progress.</summary>
    Busy,

    /// <summary>Something went wrong; it is in the log.</summary>
    Failed
}

/// <summary>
/// Offers to close the apps that keep the dedicated GPU awake, either when the
/// charger is unplugged (if <see cref="Enabled"/>) or on request from the user
/// (<see cref="FreeUpAsync"/>). Either way it scans, asks the user, then scans
/// again before closing anything, because a lot can change while a question is
/// open. The automatic path reacts only to a real plugged-in to battery change:
/// Windows also reports battery percentage updates, which must never trigger it.
/// </summary>
internal sealed class DgpuUnplugCoordinator : IDisposable
{
    private readonly IPowerSource _powerSource;
    private readonly Func<Task<DgpuScanResult>> _scanAsync;
    private readonly Func<IReadOnlyList<DgpuApp>, bool, Task<bool>> _confirmAsync;
    private readonly Func<IReadOnlyList<DgpuApp>, DgpuCloseResult> _close;
    private readonly Lock _sync = new();

    private bool _wasPluggedIn;
    private bool _isRunning;
    private volatile bool _enabled;

    /// <param name="scanAsync">Looks at what is on the dedicated GPU (read-only).</param>
    /// <param name="confirmAsync">
    /// Shows the user the list and returns whether they said yes. The flag says
    /// the question is only meaningful while on battery (it followed an unplug),
    /// so it should be withdrawn if the charger comes back.
    /// </param>
    /// <param name="close">Asks the given apps to close.</param>
    public DgpuUnplugCoordinator(
        IPowerSource powerSource,
        Func<Task<DgpuScanResult>> scanAsync,
        Func<IReadOnlyList<DgpuApp>, bool, Task<bool>> confirmAsync,
        Func<IReadOnlyList<DgpuApp>, DgpuCloseResult> close)
    {
        _powerSource = powerSource;
        _scanAsync = scanAsync;
        _confirmAsync = confirmAsync;
        _close = close;

        _wasPluggedIn = PowerProfileRules.TreatAsPluggedIn(powerSource.IsPluggedIn);
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Whether unplugging starts an attempt by itself. Off by default; turning it on does nothing until the next unplug.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>The most recent automatic run, so tests can wait for it. Completed when nothing has run.</summary>
    internal Task LastRun { get; private set; } = Task.CompletedTask;

    public void Dispose() =>
        _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;

    /// <summary>
    /// The user asked to free up the dedicated GPU now, on battery or plugged
    /// in. Same list, same question, same checks as after an unplug.
    /// </summary>
    public Task<DgpuFreeUpOutcome> FreeUpAsync() => RunAsync(requireBattery: false);

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e)
    {
        var pluggedIn = PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);
        bool unplugged;

        lock (_sync)
        {
            unplugged = _wasPluggedIn && !pluggedIn;
            _wasPluggedIn = pluggedIn;
        }

        if (unplugged && _enabled)
            LastRun = RunAsync(requireBattery: true);
    }

    private async Task<DgpuFreeUpOutcome> RunAsync(bool requireBattery)
    {
        lock (_sync)
        {
            if (_isRunning)
                return DgpuFreeUpOutcome.Busy;

            _isRunning = true;
        }

        try
        {
            var first = await _scanAsync().ConfigureAwait(false);

            if (!first.GpuFound)
                return DgpuFreeUpOutcome.NoDedicatedGpu;

            if (!first.MayClose)
            {
                AppLog.Info("Not closing anything: an external display is connected, or Windows could not say.");
                return DgpuFreeUpOutcome.ExternalDisplay;
            }

            var candidates = first.Closable;

            if (candidates.Count == 0)
            {
                AppLog.Info("No apps to close on the dedicated GPU.");
                return DgpuFreeUpOutcome.NothingToClose;
            }

            if (!await _confirmAsync(candidates, requireBattery).ConfigureAwait(false))
            {
                AppLog.Info("The user chose not to close apps on the dedicated GPU.");
                return DgpuFreeUpOutcome.Declined;
            }

            // While the question was open the charger may have been plugged
            // back in (an unplug-triggered run only), a display connected, or
            // an app closed or replaced. Only what is still eligible now, and
            // was on the list, is closed.
            if (requireBattery && PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn))
            {
                AppLog.Info("Charger is back; not closing anything.");
                return DgpuFreeUpOutcome.ConditionsChanged;
            }

            var confirmed = (await _scanAsync().ConfigureAwait(false)).Closable
                .Where(now => candidates.Any(shown => shown.ProcessId == now.ProcessId && shown.StartTime == now.StartTime))
                .ToList();

            if (confirmed.Count == 0)
            {
                AppLog.Info("Nothing left to close after re-checking.");
                return DgpuFreeUpOutcome.ConditionsChanged;
            }

            var result = _close(confirmed);
            AppLog.Info($"Asked {result.Asked} app(s) to close; {result.Skipped} skipped.");
            return result.Asked > 0 ? DgpuFreeUpOutcome.Closed : DgpuFreeUpOutcome.ConditionsChanged;
        }
        catch (Exception exception)
        {
            AppLog.Error("Closing apps on the dedicated GPU failed.", exception);
            return DgpuFreeUpOutcome.Failed;
        }
        finally
        {
            lock (_sync)
                _isRunning = false;
        }
    }
}
