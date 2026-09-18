using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Balanced / Silent / Custom performance modes, plus the CPU and GPU boost
/// selectors that appear while Custom is active. Owns the mode service and
/// reports results through events. The EC is the source of truth: the
/// selection is refreshed from it whenever the popup opens or the power
/// source changes, so a change made with Fn+P shows up.
/// </summary>
/// <remarks>
/// Like Synapse, only Balanced is offered on battery; the other modes and the
/// boost selectors are greyed out until the charger is connected.
/// </remarks>
internal sealed class PerformanceSection : Panel
{
    private readonly PerformanceModeService _modeService = new();
    private readonly PowerSourceService _powerSourceService;
    private readonly Dictionary<PerformanceMode, Button> _buttons = [];
    private readonly CustomBoostRow _customRow = new();
    private readonly PerformanceMode? _savedMode;
    private readonly CpuBoost? _savedCpu;
    private readonly GpuBoost? _savedGpu;

    // Hardware and power events arrive on other threads. Post UI updates
    // through the UI thread's context rather than relying on this control's
    // handle, which does not exist until the popup is first shown.
    private readonly SynchronizationContext _uiContext;

    private PerformanceMode? _currentMode;
    private CpuBoost? _currentCpu;
    private GpuBoost? _currentGpu;
    private bool _busy;

    // Tracked here because Control.Visible reads false whenever any parent is
    // hidden, which is most of the time for a tray popup.
    private bool _isCustomRowShown;

    public PerformanceSection(
        string? savedMode,
        string? savedCpu,
        string? savedGpu,
        PowerSourceService powerSourceService)
    {
        _powerSourceService = powerSourceService;
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        _savedMode = ParseSaved<PerformanceMode>(savedMode);
        _savedCpu = ParseSaved<CpuBoost>(savedCpu);
        _savedGpu = ParseSaved<GpuBoost>(savedGpu);

        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        // Synapse's order. Enum.GetValues would sort by wire byte instead.
        PerformanceMode[] modes = [PerformanceMode.Balanced, PerformanceMode.Silent, PerformanceMode.Custom];
        var grid = CreateButtonGrid(modes.Select(mode => mode.ToString()).ToArray(), "PerformanceButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = Enum.Parse<PerformanceMode>((string)button.Tag!);
            _buttons[mode] = button;
            button.Click += async (_, _) => await SelectModeAsync(mode);
        }

        _customRow.CpuSelected += async (_, level) => await SelectCpuAsync(level);
        _customRow.GpuSelected += async (_, level) => await SelectGpuAsync(level);

        // Dock order: the header docks first, then the custom row, and the
        // mode buttons fill whatever is left.
        Controls.Add(grid);
        Controls.Add(_customRow);
        Controls.Add(CreateSectionHeader("Performance Mode", string.Empty));

        // Start with the row where it was last time, so the popup does not
        // jump when the real mode is read a moment later.
        SetCustomRowShown(_savedMode == PerformanceMode.Custom);

        UpdateButtonStates();
        _powerSourceService.PowerSourceChanged += PowerSourceService_PowerSourceChanged;
    }

    /// <summary>Raised after the EC confirms a new mode that should be remembered.</summary>
    public event EventHandler<PerformanceMode>? ModeApplied;

    /// <summary>Raised after the EC confirms a new CPU boost level.</summary>
    public event EventHandler<CpuBoost>? CpuBoostApplied;

    /// <summary>Raised after the EC confirms a new GPU boost level.</summary>
    public event EventHandler<GpuBoost>? GpuBoostApplied;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Raised when the Custom row appears or disappears, so the host can resize.</summary>
    public event EventHandler<bool>? CustomRowVisibilityChanged;

    public bool IsCustomRowShown => _isCustomRowShown;

    /// <summary>Re-applies the saved mode and boost levels (the EC may have reset them), or reads them if none are saved.</summary>
    public async Task RestoreAsync()
    {
        if (_savedMode is PerformanceMode saved && IsModeAllowed(saved))
            await ApplyAsync(saved, announce: false);
        else
            await RefreshAsync();

        await RestoreBoostsAsync();
    }

    /// <summary>Shows the mode and boost levels the EC is actually in.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        PerformanceMode? mode = null;
        CpuBoost? cpu = null;
        GpuBoost? gpu = null;

        try
        {
            mode = await _modeService.GetModeAsync().ConfigureAwait(false);

            if (mode == PerformanceMode.Custom)
                (cpu, gpu) = await _modeService.GetBoostsAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the performance mode.", exception);
        }

        await PostToUiAsync(() =>
        {
            _busy = false;
            ShowState(mode, cpu, gpu);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSourceService.PowerSourceChanged -= PowerSourceService_PowerSourceChanged;
            _modeService.Dispose();
        }

        base.Dispose(disposing);
    }

    // Unknown power state (no battery reported) is treated as plugged in, so
    // a machine that cannot tell us is never locked out of its modes.
    private bool IsPluggedIn => _powerSourceService.IsPluggedIn != false;

    private bool IsModeAllowed(PerformanceMode mode) =>
        IsPluggedIn || mode == PerformanceMode.Balanced;

    private void PowerSourceService_PowerSourceChanged(object? sender, EventArgs e) =>
        _ = PostToUiAsync(() =>
        {
            UpdateButtonStates();

            // Show what the firmware did on its own rather than assuming.
            _ = RefreshAsync();
        });

    private async Task SelectModeAsync(PerformanceMode mode)
    {
        // Clicking the active mode would only rewrite what the EC already has.
        if (_busy || mode == _currentMode || !IsModeAllowed(mode))
            return;

        await ApplyAsync(mode, announce: true);
    }

    private async Task ApplyAsync(PerformanceMode mode, bool announce)
    {
        _busy = true;
        UpdateButtonStates();

        Exception? failure = null;
        CpuBoost? cpu = null;
        GpuBoost? gpu = null;

        try
        {
            await _modeService.SetModeAsync(mode).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        // Entering Custom reveals the boost selectors, so read what they hold.
        // The mode change itself already succeeded if this fails.
        if (failure is null && mode == PerformanceMode.Custom)
        {
            try
            {
                (cpu, gpu) = await _modeService.GetBoostsAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AppLog.Error("Could not read the boost levels.", exception);
            }
        }

        await PostToUiAsync(() =>
        {
            _busy = false;
            UpdateButtonStates();

            if (failure is null)
            {
                ShowState(mode, cpu, gpu);
                AppLog.Info($"Performance mode set to {mode}.");

                // Battery only allows Balanced, so there is nothing to
                // remember, and it must not overwrite the plugged-in choice.
                if (IsPluggedIn)
                    ModeApplied?.Invoke(this, mode);

                if (announce)
                    StatusChanged?.Invoke(this, new SectionStatus($"Performance mode set to {mode}."));

                return;
            }

            AppLog.Error($"Performance mode change to {mode} failed.", failure);

            // Put the highlight back on what the EC last confirmed.
            ShowState(_currentMode, _currentCpu, _currentGpu);
            StatusChanged?.Invoke(this, new SectionStatus(
                "Could not change the performance mode.",
                IsError: true));
        });
    }

    private Task SelectCpuAsync(CpuBoost level)
    {
        if (!CanChangeBoost() || level == _currentCpu)
            return Task.CompletedTask;

        return ApplyBoostAsync(
            () => _modeService.SetCpuBoostAsync(level),
            $"CPU boost {level}",
            () =>
            {
                _currentCpu = level;
                CpuBoostApplied?.Invoke(this, level);
            });
    }

    private Task SelectGpuAsync(GpuBoost level)
    {
        if (!CanChangeBoost() || level == _currentGpu)
            return Task.CompletedTask;

        return ApplyBoostAsync(
            () => _modeService.SetGpuBoostAsync(level),
            $"GPU boost {level}",
            () =>
            {
                _currentGpu = level;
                GpuBoostApplied?.Invoke(this, level);
            });
    }

    private bool CanChangeBoost() =>
        !_busy && _currentMode == PerformanceMode.Custom && IsModeAllowed(PerformanceMode.Custom);

    private async Task ApplyBoostAsync(Func<Task> write, string description, Action onSuccess)
    {
        _busy = true;
        UpdateButtonStates();

        Exception? failure = null;

        try
        {
            await write().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await PostToUiAsync(() =>
        {
            _busy = false;
            UpdateButtonStates();

            if (failure is null)
            {
                onSuccess();
                AppLog.Info($"{description} applied.");
            }
            else
            {
                AppLog.Error($"{description} failed.", failure);
                StatusChanged?.Invoke(this, new SectionStatus(
                    "Could not change the boost level.",
                    IsError: true));
            }

            // Either way, show what the EC last confirmed.
            _customRow.ShowBoosts(_currentCpu, _currentGpu);
        });
    }

    // Runs after the mode restore has settled, so the mode we hold is the
    // EC's. Only writes levels that differ, and only in Custom on AC power.
    private async Task RestoreBoostsAsync()
    {
        if (_savedCpu is null && _savedGpu is null)
            return;

        if (_currentMode != PerformanceMode.Custom || !IsModeAllowed(PerformanceMode.Custom))
            return;

        var changed = false;

        try
        {
            if (_savedCpu is CpuBoost cpu && cpu != _currentCpu)
            {
                await _modeService.SetCpuBoostAsync(cpu).ConfigureAwait(false);
                changed = true;
            }

            if (_savedGpu is GpuBoost gpu && gpu != _currentGpu)
            {
                await _modeService.SetGpuBoostAsync(gpu).ConfigureAwait(false);
                changed = true;
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not restore the saved boost levels.", exception);
        }

        if (changed)
            await RefreshAsync();
    }

    private void ShowState(PerformanceMode? mode, CpuBoost? cpu, GpuBoost? gpu)
    {
        _currentMode = mode;
        _currentCpu = cpu;
        _currentGpu = gpu;

        HighlightSelected(
            _buttons.Values,
            mode is PerformanceMode known ? _buttons[known] : null);

        _customRow.ShowBoosts(cpu, gpu);
        SetCustomRowShown(mode == PerformanceMode.Custom);
    }

    private void SetCustomRowShown(bool shown)
    {
        if (_isCustomRowShown == shown && _customRow.Visible == shown)
            return;

        var changed = _isCustomRowShown != shown;

        _isCustomRowShown = shown;
        _customRow.Visible = shown;

        if (changed)
            CustomRowVisibilityChanged?.Invoke(this, shown);
    }

    private void UpdateButtonStates()
    {
        foreach (var (mode, button) in _buttons)
            button.Enabled = !_busy && IsModeAllowed(mode);

        _customRow.Enabled = !_busy && IsModeAllowed(PerformanceMode.Custom);
    }

    private static T? ParseSaved<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

    // Completes once the update has run, so callers can sequence on it.
    private Task PostToUiAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiContext.Post(_ =>
        {
            try
            {
                if (!IsDisposed)
                    action();
            }
            finally
            {
                completion.SetResult();
            }
        }, null);

        return completion.Task;
    }
}
