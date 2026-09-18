using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Balanced / Silent / Custom performance modes. Owns the mode service and
/// reports results through events. The EC is the source of truth: the
/// selection is refreshed from it whenever the popup opens or the power
/// source changes, so a change made with Fn+P shows up.
/// </summary>
/// <remarks>
/// Like Synapse, only Balanced is offered on battery; the other modes are
/// greyed out until the charger is connected.
/// </remarks>
internal sealed class PerformanceSection : Panel
{
    private readonly PerformanceModeService _modeService = new();
    private readonly PowerSourceService _powerSourceService;
    private readonly Dictionary<PerformanceMode, Button> _buttons = [];
    private readonly PerformanceMode? _savedMode;

    // Hardware and power events arrive on other threads. Post UI updates
    // through the UI thread's context rather than relying on this control's
    // handle, which does not exist until the popup is first shown.
    private readonly SynchronizationContext _uiContext;

    private PerformanceMode? _currentMode;
    private bool _busy;

    public PerformanceSection(string? savedMode, PowerSourceService powerSourceService)
    {
        _powerSourceService = powerSourceService;
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        _savedMode = Enum.TryParse<PerformanceMode>(savedMode, out var parsed) &&
                     Enum.IsDefined(parsed)
            ? parsed
            : null;

        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        var modes = Enum.GetValues<PerformanceMode>();
        var grid = CreateButtonGrid(modes.Select(mode => mode.ToString()).ToArray(), "PerformanceButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = Enum.Parse<PerformanceMode>((string)button.Tag!);
            _buttons[mode] = button;
            button.Click += async (_, _) => await SelectModeAsync(mode);
        }

        Controls.Add(grid);
        Controls.Add(CreateSectionHeader("Performance Mode", string.Empty));

        UpdateButtonStates();
        _powerSourceService.PowerSourceChanged += PowerSourceService_PowerSourceChanged;
    }

    /// <summary>Raised after the EC confirms a new mode that should be remembered.</summary>
    public event EventHandler<PerformanceMode>? ModeApplied;

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    /// <summary>Re-applies the saved mode (the EC may have reset it), or reads it if none is saved.</summary>
    public async Task RestoreAsync()
    {
        if (_savedMode is PerformanceMode saved && IsModeAllowed(saved))
        {
            await ApplyAsync(saved, announce: false);
            return;
        }

        await RefreshAsync();
    }

    /// <summary>Shows the mode the EC is actually in.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        PerformanceMode? mode = null;

        try
        {
            mode = await _modeService.GetModeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the performance mode.", exception);
        }

        PostToUi(() =>
        {
            _busy = false;
            ShowMode(mode);
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
        PostToUi(() =>
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

        try
        {
            await _modeService.SetModeAsync(mode).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        PostToUi(() =>
        {
            _busy = false;
            UpdateButtonStates();

            if (failure is null)
            {
                ShowMode(mode);
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
            ShowMode(_currentMode);
            StatusChanged?.Invoke(this, new SectionStatus(
                "Could not change the performance mode.",
                IsError: true));
        });
    }

    private void ShowMode(PerformanceMode? mode)
    {
        _currentMode = mode;

        HighlightSelected(
            _buttons.Values,
            mode is PerformanceMode known ? _buttons[known] : null);
    }

    private void UpdateButtonStates()
    {
        foreach (var (mode, button) in _buttons)
            button.Enabled = !_busy && IsModeAllowed(mode);
    }

    private void PostToUi(Action action) =>
        _uiContext.Post(_ =>
        {
            if (!IsDisposed)
                action();
        }, null);
}
