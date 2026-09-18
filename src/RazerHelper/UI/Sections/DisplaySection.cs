using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Refresh-rate controls: fixed 60/120 Hz or Auto, which follows the power
/// source. Owns the display and power-source services and reports the chosen
/// mode through an event so the host can persist it.
/// </summary>
internal sealed class DisplaySection : Panel
{
    private const string AutoMode = "Auto";
    private const int PluggedInRefreshRateHz = 120;
    private const int OnBatteryRefreshRateHz = 60;

    private readonly DisplayService _displayService = new();
    private readonly PowerSourceService _powerSourceService = new();
    private readonly Label _statusLabel;
    private readonly Button[] _modeButtons;
    private readonly string? _savedMode;

    // Power events arrive on a system thread. Post through the UI thread's
    // context instead of depending on this control's window handle existing.
    private readonly SynchronizationContext _uiContext;

    private bool _isAutoEnabled;

    public DisplaySection(string? savedMode)
    {
        _savedMode = savedMode;

        // Read here rather than in a field initializer: those run before the
        // Control base constructor, which is what may install the context.
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

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

        var modeGrid = CreateButtonGrid(["60 Hz", "120 Hz", AutoMode], "RefreshRateButton");
        _modeButtons = modeGrid.Controls.OfType<Button>().ToArray();

        foreach (var button in _modeButtons)
            button.Click += ModeButton_Click;

        Controls.Add(modeGrid);
        Controls.Add(header);

        _powerSourceService.PowerSourceChanged += PowerSourceService_PowerSourceChanged;
    }

    /// <summary>Raised with the button label ("60 Hz", "120 Hz", "Auto") after a mode is chosen.</summary>
    public event EventHandler<string>? DisplayModeChanged;

    /// <summary>Re-selects the saved mode, applies it if it is Auto, and shows the current mode.</summary>
    public void Restore()
    {
        RestoreSavedMode();
        UpdateDisplayStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerSourceService.PowerSourceChanged -= PowerSourceService_PowerSourceChanged;
            _powerSourceService.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ModeButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button selected)
            return;

        if (selected.Text == AutoMode)
        {
            _isAutoEnabled = true;
            SelectModeButton(selected);
            DisplayModeChanged?.Invoke(this, selected.Text);
            ApplyAutoRefreshRate();
            return;
        }

        _isAutoEnabled = false;

        var refreshRateText = selected.Text.Replace(" Hz", string.Empty);

        if (!int.TryParse(refreshRateText, out var requestedRefreshRate))
        {
            _statusLabel.Text = "Invalid refresh-rate selection.";
            return;
        }

        if (!_displayService.TrySetPrimaryRefreshRate(
            requestedRefreshRate,
            out var message))
        {
            _statusLabel.Text = message;
            return;
        }

        SelectModeButton(selected);
        DisplayModeChanged?.Invoke(this, selected.Text);
        UpdateDisplayStatus();
    }

    private void RestoreSavedMode()
    {
        if (string.IsNullOrWhiteSpace(_savedMode))
            return;

        var selected = _modeButtons.FirstOrDefault(button =>
            string.Equals(button.Text, _savedMode, StringComparison.Ordinal));

        if (selected is null)
            return;

        SelectModeButton(selected);
        _isAutoEnabled = selected.Text == AutoMode;

        if (_isAutoEnabled)
            ApplyAutoRefreshRate();
    }

    private void PowerSourceService_PowerSourceChanged(object? sender, EventArgs e)
    {
        if (!_isAutoEnabled)
            return;

        _uiContext.Post(_ =>
        {
            if (!IsDisposed)
                ApplyAutoRefreshRate();
        }, null);
    }

    private void SelectModeButton(Button selected)
    {
        foreach (var button in _modeButtons)
        {
            button.BackColor = ButtonColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = BorderColor;
        }

        selected.BackColor = RazerGreen;
        selected.ForeColor = BackgroundColor;
        selected.FlatAppearance.BorderColor = RazerGreen;
    }

    private void ApplyAutoRefreshRate()
    {
        var isPluggedIn = _powerSourceService.IsPluggedIn;

        if (isPluggedIn is null)
        {
            _statusLabel.Text = "Auto: power source unavailable.";
            return;
        }

        var requestedRefreshRate = isPluggedIn.Value
            ? PluggedInRefreshRateHz
            : OnBatteryRefreshRateHz;
        var displayInfo = _displayService.GetPrimaryDisplayInfo();

        if (displayInfo?.RefreshRateHz != requestedRefreshRate &&
            !_displayService.TrySetPrimaryRefreshRate(
                requestedRefreshRate,
                out var message))
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
