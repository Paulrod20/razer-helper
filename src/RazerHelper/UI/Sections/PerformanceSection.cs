using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Performance modes and, in Custom, the CPU and GPU boost levels. There is
/// one profile per power source; the buttons edit the profile for the source
/// the laptop is on now, and that profile is applied automatically at startup
/// and whenever the charger is plugged or unplugged.
/// </summary>
/// <remarks>
/// The EC is the source of truth for what is shown: the display is refreshed
/// from it when the popup opens, so a change made outside the app appears.
/// Like Synapse, only Balanced is offered on battery; Silent, Custom and the
/// boost selectors are greyed out until the charger is connected. That is
/// Synapse policy rather than a hardware limit: the EC does accept them on
/// battery (checked on a Blade 16).
/// </remarks>
internal sealed class PerformanceSection : Panel
{
    private readonly PerformanceModeService _modeService = new();
    private readonly PowerSourceService _powerSourceService;
    private readonly Dictionary<PerformanceMode, Button> _buttons = [];
    private readonly CustomBoostRow _customRow = new();
    private readonly Label _sourceLabel;

    // Keyed by "plugged in".
    private readonly Dictionary<bool, PowerProfile> _profiles;

    // Hardware and power events arrive on other threads. Post UI updates
    // through the UI thread's context rather than relying on this control's
    // handle, which does not exist until the popup is first shown.
    private readonly SynchronizationContext _uiContext;

    private PerformanceState _state = PerformanceState.Unknown;
    private bool? _appliedSource;
    private bool _busy;
    private bool _reapplyRequested;

    // Tracked here because Control.Visible reads false whenever any parent is
    // hidden, which is most of the time for a tray popup.
    private bool _isCustomRowShown;

    public PerformanceSection(
        PowerProfile? pluggedInProfile,
        PowerProfile? onBatteryProfile,
        PowerSourceService powerSourceService)
    {
        _powerSourceService = powerSourceService;
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        _profiles = new Dictionary<bool, PowerProfile>
        {
            [true] = pluggedInProfile ?? new PowerProfile(),
            [false] = OnlyBalanced(onBatteryProfile ?? PowerProfile.DefaultOnBattery)
        };

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

        _sourceLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            TextAlign = ContentAlignment.MiddleRight
        };

        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = 28;
        header.Controls.Add(CreateSectionLabel("Performance Mode"), 0, 0);
        header.Controls.Add(_sourceLabel, 1, 0);

        // Dock order: the header docks first, then the custom row, and the
        // mode buttons fill whatever is left.
        Controls.Add(grid);
        Controls.Add(_customRow);
        Controls.Add(header);

        // Start with the row where the active profile last had it, so the
        // popup does not jump when the real mode is read a moment later.
        SetCustomRowShown(ActiveProfile.Mode == PerformanceMode.Custom);
        UpdateSourceLabel();
        UpdateButtonStates();

        _powerSourceService.PowerSourceChanged += PowerSourceService_PowerSourceChanged;
    }

    /// <summary>Raised after the user changes a profile and the EC confirms it.</summary>
    public event EventHandler<PowerProfileChange>? ProfileChanged;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Raised when the Custom row appears or disappears, so the host can resize.</summary>
    public event EventHandler<bool>? CustomRowVisibilityChanged;

    public bool IsCustomRowShown => _isCustomRowShown;

    /// <summary>Applies the profile for the current power source, e.g. at startup.</summary>
    public Task RestoreAsync() => ApplyActiveProfileAsync();

    /// <summary>Shows the mode and boost levels the EC is actually in.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        var state = PerformanceState.Unknown;

        try
        {
            state = await _modeService.ReadStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the performance mode.", exception);
        }

        await PostToUiAsync(() =>
        {
            ShowState(state);
            EndBusy();
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

    // Unknown power state (no battery reported) counts as plugged in.
    private bool IsPluggedIn => _powerSourceService.IsPluggedIn != false;

    private PowerProfile ActiveProfile => _profiles[IsPluggedIn];

    // On battery only Balanced is offered, as in Synapse.
    private bool IsModeAllowed(PerformanceMode mode) =>
        IsPluggedIn || mode == PerformanceMode.Balanced;

    // Boost levels belong to Custom, so they follow Custom's availability.
    private bool CanChangeBoost =>
        _state.Mode == PerformanceMode.Custom && IsModeAllowed(PerformanceMode.Custom);

    // A battery profile saved by an earlier version may hold a mode that is
    // no longer offered there; never apply one.
    private static PowerProfile OnlyBalanced(PowerProfile profile) =>
        profile.Mode is null or PerformanceMode.Balanced
            ? profile
            : profile with { Mode = PerformanceMode.Balanced };

    private void PowerSourceService_PowerSourceChanged(object? sender, EventArgs e) =>
        _ = PostToUiAsync(() =>
        {
            UpdateSourceLabel();
            UpdateButtonStates();

            // Windows also raises this for battery percentage changes; only a
            // change of source means a different profile.
            if (IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        });

    private Task ApplyActiveProfileAsync()
    {
        if (_busy)
        {
            // Something else is talking to the EC; pick this up when it is done.
            _reapplyRequested = true;
            return Task.CompletedTask;
        }

        var source = IsPluggedIn;
        var profile = _profiles[source];

        return RunAsync(
            () => _modeService.ApplyProfileAsync(profile),
            "Could not apply the power profile.",
            _ => _appliedSource = source);
    }

    private Task SelectModeAsync(PerformanceMode mode) =>
        mode == _state.Mode || !IsModeAllowed(mode)
            ? Task.CompletedTask // Would only rewrite what the EC already has.
            : ChangeProfileAsync(profile => profile with { Mode = mode }, "Could not change the performance mode.");

    private Task SelectCpuAsync(CpuBoost level) =>
        !CanChangeBoost || level == _state.Cpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Cpu = level }, "Could not change the boost level.");

    private Task SelectGpuAsync(GpuBoost level) =>
        !CanChangeBoost || level == _state.Gpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Gpu = level }, "Could not change the boost level.");

    private Task ChangeProfileAsync(Func<PowerProfile, PowerProfile> edit, string failureMessage)
    {
        if (_busy)
            return Task.CompletedTask;

        var source = IsPluggedIn;
        var edited = edit(_profiles[source]);

        return RunAsync(
            () => _modeService.ApplyProfileAsync(edited),
            failureMessage,
            state =>
            {
                // Keep what the EC really ended up in, so the stored profile
                // is fully specified. Boost levels the EC does not report
                // (outside Custom) stay as they were for the next Custom.
                var saved = edited with
                {
                    Mode = state.Mode ?? edited.Mode,
                    Cpu = state.Cpu ?? edited.Cpu,
                    Gpu = state.Gpu ?? edited.Gpu
                };

                _profiles[source] = saved;
                _appliedSource = source;
                ProfileChanged?.Invoke(this, new PowerProfileChange(source, saved));
            });
    }

    // Runs an EC operation off the UI thread, then shows the resulting state.
    // On failure the display goes back to what the EC last confirmed.
    private async Task RunAsync(
        Func<Task<PerformanceState>> operation,
        string failureMessage,
        Action<PerformanceState> onSuccess)
    {
        _busy = true;
        UpdateButtonStates();

        PerformanceState? result = null;
        Exception? failure = null;

        try
        {
            result = await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await PostToUiAsync(() =>
        {
            if (result is not null)
            {
                ShowState(result);
                onSuccess(result);
                AppLog.Info($"Performance state is now {result.Mode} (CPU {result.Cpu}, GPU {result.Gpu}).");
                StatusChanged?.Invoke(this, new SectionStatus("Performance profile applied."));
            }
            else
            {
                AppLog.Error(failureMessage, failure);
                ShowState(_state);
                StatusChanged?.Invoke(this, new SectionStatus(failureMessage, IsError: true));
            }

            EndBusy();
        });
    }

    private void EndBusy()
    {
        _busy = false;
        UpdateButtonStates();

        // A plug or unplug arrived while we were busy.
        if (_reapplyRequested)
        {
            _reapplyRequested = false;

            if (IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        }
    }

    private void ShowState(PerformanceState state)
    {
        _state = state;

        HighlightSelected(
            _buttons.Values,
            state.Mode is PerformanceMode known ? _buttons[known] : null);

        _customRow.ShowBoosts(state.Cpu, state.Gpu);
        SetCustomRowShown(state.Mode == PerformanceMode.Custom);
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

    private void UpdateSourceLabel() =>
        _sourceLabel.Text = IsPluggedIn ? "Plugged in" : "On battery";

    private void UpdateButtonStates()
    {
        foreach (var (mode, button) in _buttons)
            button.Enabled = !_busy && IsModeAllowed(mode);

        _customRow.Enabled = !_busy && CanChangeBoost;
    }

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
