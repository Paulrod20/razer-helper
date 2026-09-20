using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Refresh-rate controls: a fixed rate or Auto, which follows the power
/// source. Reports the chosen mode through an event so the host can persist it.
/// </summary>
internal sealed class DisplaySection : SectionPanel
{
    private readonly DisplayService _displayService;
    private readonly IPowerSource _powerSource;
    private readonly DisplayRefreshMode? _savedMode;
    private readonly Label _statusLabel;
    private readonly Dictionary<DisplayRefreshMode, Button> _buttons = [];
    private readonly FullscreenGuard _fullscreenGuard;

    // Only runs while an automatic change is on hold behind a fullscreen game.
    private readonly System.Windows.Forms.Timer _retryTimer = new() { Interval = 10_000 };

    private DisplayRefreshMode? _selectedMode;

    public DisplaySection(
        DisplayService displayService,
        IPowerSource powerSource,
        DisplayRefreshMode? savedMode,
        IFullscreenDetector? fullscreenDetector = null)
    {
        _fullscreenGuard = new FullscreenGuard(fullscreenDetector ?? new NoFullscreenDetector());
        _retryTimer.Tick += RetryTimer_Tick;
        _displayService = displayService;
        _powerSource = powerSource;
        _savedMode = savedMode;

        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = S(28);

        _statusLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Text = "Current: -- Hz",
            TextAlign = ContentAlignment.MiddleRight
        };

        header.Controls.Add(CreateSectionLabel("Display"), 0, 0);
        header.Controls.Add(_statusLabel, 1, 0);

        var modes = DisplayRefreshMode.Offered;
        var grid = CreateButtonGrid(modes.Select(mode => mode.Label).ToArray(), "RefreshRateButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var mode = modes.First(candidate => candidate.Label == (string)button.Tag!);
            _buttons[mode] = button;
            button.Click += (_, _) => SelectMode(mode);
        }

        Controls.Add(grid);
        Controls.Add(header);

        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Raised after a mode is chosen and applied.</summary>
    public event EventHandler<DisplayRefreshMode>? DisplayModeChanged;

    /// <summary>Re-selects the saved mode, applies it if it is Auto, and shows the current mode.</summary>
    public void Restore()
    {
        if (_savedMode is not null && _buttons.ContainsKey(_savedMode))
        {
            Select(_savedMode);

            if (_savedMode.IsAuto)
                ApplyAuto();
        }

        // Keep the "waiting for the game" note if a change was just put on hold.
        if (!_fullscreenGuard.IsWaiting)
            UpdateDisplayStatus();
    }

    /// <summary>
    /// Shows what the display is doing now. Called when the popup opens, because a
    /// game may have changed the resolution or refresh rate since it was last read.
    /// Also lets a change that was held back behind a game go ahead if the game is gone.
    /// </summary>
    public void RefreshStatus()
    {
        if (_fullscreenGuard.ReadyToRetry)
            ApplyAuto();
        else if (!_fullscreenGuard.IsWaiting)
            UpdateDisplayStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _retryTimer.Stop();
            _retryTimer.Tick -= RetryTimer_Tick;
            _retryTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SelectMode(DisplayRefreshMode mode)
    {
        if (mode.IsAuto)
        {
            Select(mode);
            DisplayModeChanged?.Invoke(this, mode);
            ApplyAuto();
            return;
        }

        // A rate the user picked replaces anything that was on hold.
        _fullscreenGuard.Cancel();
        _retryTimer.Stop();

        if (!_displayService.TrySetPrimaryRefreshRate(mode.FixedHz!.Value, out var message))
        {
            _statusLabel.Text = message;
            return;
        }

        Select(mode);
        DisplayModeChanged?.Invoke(this, mode);
        UpdateDisplayStatus();
    }

    private void Select(DisplayRefreshMode mode)
    {
        _selectedMode = mode;
        HighlightSelected(_buttons.Values, _buttons[mode]);
    }

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e)
    {
        if (_selectedMode?.IsAuto == true)
            PostToUi(ApplyAuto);
    }

    private void ApplyAuto()
    {
        var isPluggedIn = _powerSource.IsPluggedIn;

        if (isPluggedIn is null)
        {
            _statusLabel.Text = "Auto: power source unavailable.";
            return;
        }

        // A fullscreen game has the screen: changing the refresh rate under it can
        // make it flicker or drop out. Hold the change, and try again until it is gone.
        if (!_fullscreenGuard.CanApplyNow())
        {
            _statusLabel.Text = "Auto: waiting for the game";
            _retryTimer.Start();
            return;
        }

        _retryTimer.Stop();

        var targetHz = DisplayRefreshMode.Auto.TargetHz(isPluggedIn.Value);

        if (_displayService.GetPrimaryDisplayInfo()?.RefreshRateHz != targetHz &&
            !_displayService.TrySetPrimaryRefreshRate(targetHz, out var message))
        {
            _statusLabel.Text = message;
            return;
        }

        UpdateDisplayStatus();
    }

    private void RetryTimer_Tick(object? sender, EventArgs e)
    {
        if (_fullscreenGuard.ReadyToRetry)
            ApplyAuto();
    }

    private void UpdateDisplayStatus()
    {
        var displayInfo = _displayService.GetPrimaryDisplayInfo();

        _statusLabel.Text = displayInfo is null
            ? "Display information not available"
            : $"Display: {displayInfo.Width}x{displayInfo.Height} @ {displayInfo.RefreshRateHz} Hz";
    }
}
