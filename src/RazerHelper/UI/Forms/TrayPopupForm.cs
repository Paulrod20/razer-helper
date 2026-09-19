using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    private const int PerformanceBaseRowHeight = 124;
    private const int FanRowHeight = 62; // Header and readouts only; no fan controls yet.

    private bool _allowClose;
    private readonly SettingsService _settingsService = new();
    private readonly PowerSourceService _powerSourceService = new();
    private readonly BatterySection _batterySection;
    private readonly DisplaySection _displaySection;
    private readonly FanSection _fanSection;
    private readonly PerformanceSection _performanceSection;
    private readonly Label _footerLabel = CreateFooterLabel();
    private TableLayoutPanel _content = null!;
    private AppSettings _settings;

    public TrayPopupForm()
    {
        _settings = _settingsService.Load();

        _fanSection = new FanSection();

        _performanceSection = new PerformanceSection(
            _settings.PluggedInProfile,
            _settings.OnBatteryProfile,
            _powerSourceService);
        _performanceSection.ProfileChanged += PerformanceSection_ProfileChanged;
        _performanceSection.CustomRowVisibilityChanged += PerformanceSection_CustomRowVisibilityChanged;
        _performanceSection.StatusChanged += Section_StatusChanged;

        _displaySection = new DisplaySection(_settings.DisplayMode, _powerSourceService);
        _displaySection.DisplayModeChanged += DisplaySection_DisplayModeChanged;

        _batterySection = new BatterySection(_settings.BatteryChargeLimit);
        _batterySection.ChargeLimitApplied += BatterySection_ChargeLimitApplied;
        _batterySection.StatusChanged += Section_StatusChanged;

        // Keep the tray popup's design surface stable across display scales.
        AutoScaleMode = AutoScaleMode.None;

        ApplyTheme();
        BuildView();
        ReportUnsupportedDevice();

        _displaySection.Restore();
        _ = _batterySection.RestoreAsync();
        _ = _performanceSection.RestoreAsync();

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
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 600);
        Text = "RazerHelper";
        StartPosition = FormStartPosition.Manual;
    }

    private void BuildView()
    {
        _content = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16, 12, 16, 12),
            RowCount = 6
        };

        // Every row is a fixed height, so the popup's height is their sum.
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F)); // Header
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, PerformanceBaseRowHeight)); // Performance
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, FanRowHeight)); // Fan
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F)); // Display
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F)); // Battery
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F)); // Footer

        _content.Controls.Add(CreateAppHeader(), 0, 0);
        _content.Controls.Add(_performanceSection, 0, 1);
        _content.Controls.Add(_fanSection, 0, 2);
        _content.Controls.Add(_displaySection, 0, 3);
        _content.Controls.Add(_batterySection, 0, 4);
        _content.Controls.Add(_footerLabel, 0, 5);

        Controls.Add(_content);

        ResizeForPerformanceRow(_performanceSection.IsCustomRowShown);
    }

    // The Custom boost row makes the Performance row taller. Grow or shrink
    // the popup to match, then re-anchor it to the taskbar so it does not
    // end up floating or overlapping it.
    private void ResizeForPerformanceRow(bool customRowShown)
    {
        _content.RowStyles[1].Height = PerformanceBaseRowHeight +
            (customRowShown ? CustomBoostRow.RowHeight : 0);

        var contentHeight = _content.Padding.Vertical +
            _content.RowStyles.Cast<RowStyle>().Sum(row => row.Height);

        ClientSize = new Size(ClientSize.Width, (int)contentHeight);

        if (Visible)
            Location = TaskbarPlacement.GetPopupLocation(Size);
    }

    // Temperatures are left out until there is a trustworthy CPU temperature
    // source; a permanent "-- C" only looks broken.
    private static Control CreateAppHeader() => new Label
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = CreateDesignFont("Segoe UI", 12F, FontStyle.Bold),
        ForeColor = RazerGreen,
        Text = "RazerHelper",
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static readonly Color FooterColor = Color.FromArgb(145, 145, 145);
    private static readonly string FooterText =
        $"RazerHelper  |  {DeviceSupportService.SupportedModelName}";

    private static Label CreateFooterLabel() => new()
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = CreateDesignFont("Segoe UI", 8.5F),
        ForeColor = FooterColor,
        Text = FooterText,
        TextAlign = ContentAlignment.MiddleLeft
    };

    // There is no status panel: successes are visible in the controls
    // themselves, so only failures are worth words. They take over the footer
    // in red until the next result replaces them.
    private void ShowStatus(SectionStatus status)
    {
        _footerLabel.ForeColor = status.IsError ? Color.IndianRed : FooterColor;
        _footerLabel.Text = status.IsError ? status.Message : FooterText;
    }

    private void ReportUnsupportedDevice()
    {
        if (new DeviceSupportService().IsSupportedDevicePresent())
            return;

        AppLog.Error($"{DeviceSupportService.SupportedModelName} control interface not found.");

        ShowStatus(new SectionStatus(
            $"{DeviceSupportService.SupportedModelName} not detected; fan and battery controls unavailable.",
            IsError: true));
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        _settingsService.Save(settings);
    }

    private void DisplaySection_DisplayModeChanged(object? sender, string mode) =>
        SaveSettings(_settings with { DisplayMode = mode });

    private void PerformanceSection_ProfileChanged(object? sender, PowerProfileChange change) =>
        SaveSettings(change.PluggedIn
            ? _settings with { PluggedInProfile = change.Profile }
            : _settings with { OnBatteryProfile = change.Profile });

    private void BatterySection_ChargeLimitApplied(object? sender, int limit) =>
        SaveSettings(_settings with { BatteryChargeLimit = limit });

    private void PerformanceSection_CustomRowVisibilityChanged(object? sender, bool shown) =>
        ResizeForPerformanceRow(shown);

    private void Section_StatusChanged(object? sender, SectionStatus status) =>
        ShowStatus(status);

    private void HideWhenInactive()
    {
        if (!_allowClose && Visible && !ContainsFocus)
            Hide();
    }

    // A borderless window has no shadow of its own; ask for the standard one
    // so the popup lifts off whatever is behind it.
    protected override CreateParams CreateParams
    {
        get
        {
            const int ClassDropShadow = 0x00020000;

            var parameters = base.CreateParams;
            parameters.ClassStyle |= ClassDropShadow;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    protected override void Dispose(bool disposing)
    {
        // Sections unsubscribe from the shared service as they are disposed.
        base.Dispose(disposing);

        if (disposing)
            _powerSourceService.Dispose();
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
        {
            _fanSection.StartPolling();

            // Fn+P changes the mode without telling us; show what the EC has.
            _ = _performanceSection.RefreshAsync();
        }
        else
            _fanSection.StopPolling();
    }
}
