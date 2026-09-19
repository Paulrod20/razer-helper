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

    private DisplayRefreshMode? _selectedMode;

    public DisplaySection(
        DisplayService displayService,
        IPowerSource powerSource,
        DisplayRefreshMode? savedMode)
    {
        _displayService = displayService;
        _powerSource = powerSource;
        _savedMode = savedMode;

        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = 28;

        _statusLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
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

        UpdateDisplayStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;

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

        var targetHz = DisplayRefreshMode.Auto.TargetHz(isPluggedIn.Value);

        if (_displayService.GetPrimaryDisplayInfo()?.RefreshRateHz != targetHz &&
            !_displayService.TrySetPrimaryRefreshRate(targetHz, out var message))
        {
            _statusLabel.Text = message;
            return;
        }

        UpdateDisplayStatus();
    }

    private void UpdateDisplayStatus()
    {
        var displayInfo = _displayService.GetPrimaryDisplayInfo();

        _statusLabel.Text = displayInfo is null
            ? "Display information not available"
            : $"Display: {displayInfo.Width}x{displayInfo.Height} @ {displayInfo.RefreshRateHz} Hz";
    }
}
