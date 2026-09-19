using System.ComponentModel;
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
/// What is allowed and what gets stored is decided by
/// <see cref="PowerProfileRules"/>; this control only wires that to the UI.
/// </remarks>
internal sealed class PerformanceSection : SectionPanel
{
    private readonly PerformanceService _performanceService;
    private readonly IPowerSource _powerSource;
    private readonly Dictionary<PerformanceMode, Button> _buttons = [];
    private readonly CustomBoostRow _customRow = new();
    private readonly Label _sourceLabel;
    private readonly ThemedToolTip _toolTip = new();

    // Keyed by "plugged in".
    private readonly Dictionary<bool, PowerProfile> _profiles;

    private PerformanceState _state = PerformanceState.Unknown;
    private bool? _appliedSource;
    private bool _busy;
    private bool _reapplyRequested;
    private bool _autoSwitchProfiles = true;

    // Tracked here because Control.Visible reads false whenever any parent is
    // hidden, which is most of the time for a tray popup.
    private bool _isCustomRowShown;

    public PerformanceSection(
        PerformanceService performanceService,
        IPowerSource powerSource,
        PowerProfile? pluggedInProfile,
        PowerProfile? onBatteryProfile)
    {
        _performanceService = performanceService;
        _powerSource = powerSource;

        _profiles = new Dictionary<bool, PowerProfile>
        {
            [true] = PowerProfileRules.Sanitize(pluggedInProfile ?? new PowerProfile(), pluggedIn: true),
            [false] = PowerProfileRules.Sanitize(onBatteryProfile ?? PowerProfile.DefaultOnBattery, pluggedIn: false)
        };

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

        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Raised after the user changes a profile and the EC confirms it.</summary>
    public event EventHandler<PowerProfileChange>? ProfileChanged;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Raised whenever the section shows a new state, so others (the fan buttons) can follow it.</summary>
    public event EventHandler<PerformanceState>? StateChanged;

    /// <summary>Raised when the Custom row appears or disappears, so the host can resize.</summary>
    public event EventHandler<bool>? CustomRowVisibilityChanged;

    public bool IsCustomRowShown => _isCustomRowShown;

    /// <summary>
    /// Whether plugging or unplugging the charger switches to the profile for
    /// that source. Turning it back on catches up straight away if the source
    /// changed while it was off.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoSwitchProfiles
    {
        get => _autoSwitchProfiles;
        set
        {
            if (_autoSwitchProfiles == value)
                return;

            _autoSwitchProfiles = value;

            if (value && IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        }
    }

    /// <summary>Applies the profile for the current power source, e.g. at startup.</summary>
    public Task RestoreAsync() => ApplyActiveProfileAsync();

    /// <summary>
    /// Turns max fan speed on or off. A one-off: it is not stored in a profile,
    /// and the EC clears it by itself when the mode leaves Custom. Ignored when
    /// it is already in that state, while something else is talking to the EC,
    /// or when Max is not available (Custom mode, plugged in).
    /// </summary>
    public Task SetMaxFanAsync(bool enabled)
    {
        if (_busy || enabled == (_state.MaxFan == true) || !PowerProfileRules.CanUseMaxFan(_state, IsPluggedIn))
            return Task.CompletedTask;

        return RunAsync(
            () => _performanceService.SetMaxFanAsync(enabled),
            "Could not change max fan speed.",
            _ => { }); // Nothing to remember: it is not part of a profile.
    }

    /// <summary>Shows the mode and boost levels the EC is actually in.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        var state = PerformanceState.Unknown;

        try
        {
            state = await _performanceService.ReadStateAsync().ConfigureAwait(false);
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
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool IsPluggedIn => PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);

    private PowerProfile ActiveProfile => _profiles[IsPluggedIn];

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e) =>
        _ = PostToUiAsync(() =>
        {
            UpdateSourceLabel();
            UpdateButtonStates();

            // Windows also raises this for battery percentage changes; only a
            // change of source means a different profile.
            if (_autoSwitchProfiles && IsPluggedIn != _appliedSource)
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
            () => _performanceService.ApplyProfileAsync(profile),
            "Could not apply the power profile.",
            _ => _appliedSource = source);
    }

    private Task SelectModeAsync(PerformanceMode mode) =>
        mode == _state.Mode || !PowerProfileRules.IsModeAllowed(mode, IsPluggedIn)
            ? Task.CompletedTask // Already there, or not offered on this power source.
            : ChangeProfileAsync(profile => profile with { Mode = mode }, "Could not change the performance mode.");

    private Task SelectCpuAsync(CpuBoost level) =>
        !PowerProfileRules.CanChangeBoost(_state, IsPluggedIn) || level == _state.Cpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Cpu = level }, "Could not change the boost level.");

    private Task SelectGpuAsync(GpuBoost level) =>
        !PowerProfileRules.CanChangeBoost(_state, IsPluggedIn) || level == _state.Gpu
            ? Task.CompletedTask
            : ChangeProfileAsync(profile => profile with { Gpu = level }, "Could not change the boost level.");

    private Task ChangeProfileAsync(Func<PowerProfile, PowerProfile> edit, string failureMessage)
    {
        if (_busy)
            return Task.CompletedTask;

        var source = IsPluggedIn;
        var edited = edit(_profiles[source]);

        return RunAsync(
            () => _performanceService.ApplyProfileAsync(edited),
            failureMessage,
            state =>
            {
                var saved = PowerProfileRules.Remember(edited, state);

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
                AppLog.Info($"Performance state is now {result.Mode} (CPU {result.Cpu}, GPU {result.Gpu}, max fan {result.MaxFan?.ToString() ?? "n/a"}).");
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

            if (_autoSwitchProfiles && IsPluggedIn != _appliedSource)
                _ = ApplyActiveProfileAsync();
        }
    }

    private void ShowState(PerformanceState state)
    {
        _state = state;

        HighlightSelected(
            _buttons.Values,
            state.Mode is PerformanceMode known ? _buttons[known] : null);

        // Highlighting resets the text colors, so redo the unavailable look.
        UpdateButtonStates();

        _customRow.ShowBoosts(state.Cpu, state.Gpu);
        SetCustomRowShown(state.Mode == PerformanceMode.Custom);

        StateChanged?.Invoke(this, state);
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
        var pluggedIn = IsPluggedIn;

        foreach (var (mode, button) in _buttons)
        {
            // Truly disabled only while a write is in flight. A mode that is not
            // offered on this power source stays clickable underneath (the click
            // handler refuses it) so hovering it can explain why.
            button.Enabled = !_busy;
            SetAvailability(
                button,
                PowerProfileRules.IsModeAllowed(mode, pluggedIn),
                _toolTip,
                "Needs to be plugged in");
        }

        _customRow.Enabled = !_busy && PowerProfileRules.CanChangeBoost(_state, pluggedIn);
    }
}
