using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    private bool _allowClose;
    private static readonly DisplayService _displayService = new();
    private readonly PowerSourceService _powerSourceService = new();
    private readonly FanTelemetryService _fanTelemetryService = new();
    private readonly SettingsService _settingsService = new();
    private readonly System.Windows.Forms.Timer _fanTelemetryTimer = new()
    {
        Interval = 2_000
    };
    private readonly BatterySection _batterySection;
    private AppSettings _settings;
    private bool _isAutoRefreshEnabled;
    private bool _fanRefreshInProgress;

    public TrayPopupForm()
    {
        _settings = _settingsService.Load();

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

        RestoreDisplaySetting();
        UpdateDisplayStatus();
        _ = _batterySection.RestoreAsync();

        _powerSourceService.PowerSourceChanged += PowerSourceService_PowerSourceChanged;
        _fanTelemetryTimer.Tick += FanTelemetryTimer_Tick;

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
        content.Controls.Add(CreateFanSection(), 0, 2);
        content.Controls.Add(CreateDisplaySection(), 0, 3);
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

    private Control CreateFanSection()
    {
        var section = CreateSectionPanel();
        var controls = CreateButtonGrid(["Auto", "Max", "Manual"], "FanButton");
        var readings = CreateFanReadings();

        section.Controls.Add(controls);
        section.Controls.Add(readings);
        section.Controls.Add(CreateSectionHeader("Fan Control", string.Empty));
        return section;
    }

    private Control CreateDisplaySection()
    {
        var section = CreateSectionPanel();

        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = 28;

        header.Controls.Add(CreateSectionLabel("Display"), 0, 0);

        header.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Name = "displayStatusLabel",
            Text = "Current: -- Hz",
            TextAlign = ContentAlignment.MiddleRight
        }, 1, 0);

        var refreshRates = CreateButtonGrid(
            ["60 Hz", "120 Hz", "Auto"],
            "RefreshRateButton");

        refreshRates.Name = "displayRefreshGrid";

        foreach (var button in refreshRates.Controls.OfType<Button>())
        {
            button.Click += RefreshRateButton_Click;
        }
            

        section.Controls.Add(refreshRates);
        section.Controls.Add(header);
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

    private static Control CreateFanReadings()
    {
        var readings = CreateTwoColumnLayout(50F, 50F);
        readings.Dock = DockStyle.Top;
        readings.Height = 24;
        readings.Padding = new Padding(0, 0, 0, 2);

        readings.Controls.Add(CreateReadingLabel("CPU Fan: -- RPM", "cpuFanLabel", DockStyle.Left), 0, 0);
        readings.Controls.Add(CreateReadingLabel("GPU Fan: -- RPM", "gpuFanLabel", DockStyle.Right), 1, 0);
        return readings;
    }

    private static Label CreateReadingLabel(string text, string name, DockStyle dock) => new()
    {
        AutoSize = true,
        Dock = dock,
        Font = CreateDesignFont("Segoe UI", 9.5F),
        ForeColor = Color.Silver,
        Name = name,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private void RefreshRateButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button selected)
            return;

        var grid = Controls.Find("displayRefreshGrid", true)
            .OfType<TableLayoutPanel>()
            .FirstOrDefault();

        var status = Controls.Find("displayStatusLabel", true)
            .OfType<Label>()
            .FirstOrDefault();

        if (grid is null || status is null)
            return;

        if (selected.Text == "Auto")
        {
            _isAutoRefreshEnabled = true;
            SelectRefreshRateButton(grid, selected);
            SaveSettings(_settings with { DisplayMode = selected.Text });
            ApplyAutoRefreshRate();
            return;
        }

        _isAutoRefreshEnabled = false;

        var refreshRateText = selected.Text.Replace(" Hz", string.Empty);

        if (!int.TryParse(refreshRateText, out var requestedRefreshRate))
        {
            status.Text = "Invalid refresh-rate selection.";
            return;
        }

        if (!_displayService.TrySetPrimaryRefreshRate(
            requestedRefreshRate,
            out var message))
        {
            status.Text = message;
            return;
        }

        SelectRefreshRateButton(grid, selected);
        SaveSettings(_settings with { DisplayMode = selected.Text });
        UpdateDisplayStatus();
    }

    private void RestoreDisplaySetting()
    {
        if (string.IsNullOrWhiteSpace(_settings.DisplayMode))
            return;

        var grid = Controls.Find("displayRefreshGrid", true)
            .OfType<TableLayoutPanel>()
            .FirstOrDefault();

        var selected = grid?.Controls
            .OfType<Button>()
            .FirstOrDefault(button =>
                string.Equals(
                    button.Text,
                    _settings.DisplayMode,
                    StringComparison.Ordinal));

        if (grid is null || selected is null)
            return;

        SelectRefreshRateButton(grid, selected);
        _isAutoRefreshEnabled = selected.Text == "Auto";

        if (_isAutoRefreshEnabled)
            ApplyAutoRefreshRate();
    }

    private void SaveSettings(AppSettings settings)
    {
        _settings = settings;
        _settingsService.Save(settings);
    }

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

    private void PowerSourceService_PowerSourceChanged(
    object? sender,
    EventArgs e)
    {
        if (!_isAutoRefreshEnabled || IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(ApplyAutoRefreshRate);
    }

    private static void SelectRefreshRateButton(
    TableLayoutPanel grid,
    Button selected)
    {
        foreach (var button in grid.Controls.OfType<Button>())
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
        var status = Controls.Find("displayStatusLabel", true)
            .OfType<Label>()
            .FirstOrDefault();

        if (status is null)
            return;

        var isPluggedIn = _powerSourceService.IsPluggedIn;

        if (isPluggedIn is null)
        {
            status.Text = "Auto: power source unavailable.";
            return;
        }

        var requestedRefreshRate = isPluggedIn.Value ? 120 : 60;
        var displayInfo = _displayService.GetPrimaryDisplayInfo();

        if (displayInfo?.RefreshRateHz != requestedRefreshRate &&
            !_displayService.TrySetPrimaryRefreshRate(
                requestedRefreshRate,
                out var message))
        {
            status.Text = message;
            return;
        }

        UpdateDisplayStatus();
    }

    private void HideWhenInactive()
    {
        if (!_allowClose && Visible && !ContainsFocus)
            Hide();
    }

    private async void FanTelemetryTimer_Tick(object? sender, EventArgs e) =>
        await RefreshFanReadingsAsync();

    private async Task RefreshFanReadingsAsync()
    {
        if (_fanRefreshInProgress)
            return;

        _fanRefreshInProgress = true;

        try
        {
            var reading = await _fanTelemetryService.ReadAsync();

            if (!Visible || reading is null)
                return;

            SetFanReading("cpuFanLabel", "CPU Fan", reading.CpuFanRpm);
            SetFanReading("gpuFanLabel", "GPU Fan", reading.GpuFanRpm);
        }
        catch (Exception exception)
        {
            AppLog.Error("Fan telemetry read failed unexpectedly.", exception);

            if (Visible)
            {
                SetFanReading("cpuFanLabel", "CPU Fan", null);
                SetFanReading("gpuFanLabel", "GPU Fan", null);
            }
        }
        finally
        {
            _fanRefreshInProgress = false;
        }
    }

    private void SetFanReading(string controlName, string label, int? rpm)
    {
        var control = Controls.Find(controlName, true)
            .OfType<Label>()
            .FirstOrDefault();

        if (control is not null)
            control.Text = rpm is null ? $"{label}: -- RPM" : $"{label}: {rpm} RPM";
    }

    private void UpdateDisplayStatus()
    {
        var displayInfo = _displayService.GetPrimaryDisplayInfo();
        var status = Controls.Find("displayStatusLabel", true)
            .OfType<Label>()
            .FirstOrDefault();

        if (status is not null)
        {
            status.Text = displayInfo is null
                ? "Display information not available"
                : $"Display: {displayInfo.Width}x{displayInfo.Height} @ {displayInfo.RefreshRateHz} Hz";
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _powerSourceService.PowerSourceChanged -= PowerSourceService_PowerSourceChanged;
        _powerSourceService.Dispose();
        _fanTelemetryTimer.Stop();
        _fanTelemetryTimer.Tick -= FanTelemetryTimer_Tick;
        _fanTelemetryTimer.Dispose();
        _fanTelemetryService.Dispose();

        base.OnFormClosing(e);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            _fanTelemetryTimer.Start();
            _ = RefreshFanReadingsAsync();
        }
        else
        {
            _fanTelemetryTimer.Stop();
        }
    }
}
