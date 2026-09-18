using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    private bool _allowClose;
    private readonly SettingsService _settingsService = new();
    private readonly BatterySection _batterySection;
    private readonly DisplaySection _displaySection;
    private readonly FanSection _fanSection;
    private AppSettings _settings;

    public TrayPopupForm()
    {
        _settings = _settingsService.Load();

        _fanSection = new FanSection();

        _displaySection = new DisplaySection(_settings.DisplayMode);
        _displaySection.DisplayModeChanged += DisplaySection_DisplayModeChanged;

        _batterySection = new BatterySection(_settings.BatteryChargeLimit);
        _batterySection.ChargeLimitApplied += BatterySection_ChargeLimitApplied;
        _batterySection.StatusChanged += Section_StatusChanged;

        // Keep the tray popup's design surface stable across display scales.
        AutoScaleMode = AutoScaleMode.None;

        ApplyTheme();
        BuildView();

        // The popup lives in the tray and is not shown at startup. Create its
        // window handle now so BeginInvoke works for power events before the
        // popup has been opened for the first time.
        CreateHandle();

        _displaySection.Restore();
        _ = _batterySection.RestoreAsync();

        Deactivate += (_, _) => BeginInvoke(HideWhenInactive);
    }

    public void CloseForApplicationExit()
    {
        _allowClose = true;
        Close();
    }

    private void ApplyTheme()
    {
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = CreateDesignFont("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 600);
        Text = "RazerHelper";
        StartPosition = FormStartPosition.Manual;
    }

    private void BuildView()
    {
        var content = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 12, 16, 12),
            RowCount = 7
        };

        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F)); // Header
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F)); // Performance
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 146F)); // Fan
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F)); // Display
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F)); // Battery
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Status
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F)); // Footer

        content.Controls.Add(CreateAppHeader(), 0, 0);
        content.Controls.Add(CreatePerformanceSection(), 0, 1);
        content.Controls.Add(_fanSection, 0, 2);
        content.Controls.Add(_displaySection, 0, 3);
        content.Controls.Add(_batterySection, 0, 4);
        content.Controls.Add(CreateStatusSection(), 0, 5);
        content.Controls.Add(CreateFooter(), 0, 6);

        Controls.Add(content);
    }

    private Control CreateAppHeader()
    {
        var header = CreateTwoColumnLayout(60F, 40F);

        var title = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Left,
            Font = CreateDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Text = "RazerHelper",
            TextAlign = ContentAlignment.MiddleLeft
        };

        var temperatures = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Name = "temperatureLabel",
            Text = "CPU: -- C   GPU: -- C",
            TextAlign = ContentAlignment.MiddleRight
        };

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(temperatures, 1, 0);
        return header;
    }

    private Control CreatePerformanceSection()
    {
        var section = CreateSectionPanel();
        var modes = CreateButtonGrid(["Balanced", "Silent", "Custom"], "ModeButton");

        section.Controls.Add(modes);
        section.Controls.Add(CreateSectionHeader("Performance Mode", "Plugged in"));
        return section;
    }

    private Control CreateStatusSection()
    {
        var section = CreateSectionPanel();
        var title = CreateSectionLabel("RazerHelper Status");
        title.Dock = DockStyle.Top;

        var note = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Name = "appStatusLabel",
            Padding = new Padding(0, 4, 0, 0),
            Text = "Tray shell is ready. Display and battery controls are active."
        };

        section.Controls.Add(note);
        section.Controls.Add(title);
        return section;
    }

    private Control CreateFooter() => new Label
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = CreateDesignFont("Segoe UI", 8.5F),
        ForeColor = Color.FromArgb(145, 145, 145),
        Text = "RazerHelper  |  Blade 16 (2023)",
        TextAlign = ContentAlignment.MiddleLeft
    };

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        _settingsService.Save(settings);
    }

    private void DisplaySection_DisplayModeChanged(object? sender, string mode) =>
        SaveSettings(_settings with { DisplayMode = mode });

    private void BatterySection_ChargeLimitApplied(object? sender, int limit) =>
        SaveSettings(_settings with { BatteryChargeLimit = limit });

    private void Section_StatusChanged(object? sender, SectionStatus status) =>
        SetStatusMessage(status.Message, status.IsError);

    private void SetStatusMessage(string message, bool isError = false)
    {
        var status = Controls.Find("appStatusLabel", true)
            .OfType<Label>()
            .FirstOrDefault();

        if (status is null)
            return;

        status.ForeColor = isError ? Color.IndianRed : Color.Silver;
        status.Text = message;
    }

    private void HideWhenInactive()
    {
        if (!_allowClose && Visible && !ContainsFocus)
            Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
            _fanSection.StartPolling();
        else
            _fanSection.StopPolling();
    }
}
