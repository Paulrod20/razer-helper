using System.ServiceProcess;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

public sealed class TrayPopupForm : Form
{
    // Row positions in the popup's grid. The Razer services row is always
    // last: anything new goes above it.
    private const int PerformanceRow = 1;
    private const int ServicesRow = 5;

    private const int PerformanceBaseRowHeight = 124;
    private const int FanRowHeight = 108; // Header, the two readouts and the taller Auto / Max buttons.

    private static readonly string ModelText = DeviceSupportService.SupportedModelName;

    private bool _allowClose;
    private int _modalDepth;
    private bool _customRowShown;
    private bool _servicesRowShown;
    private readonly SettingsService _settingsService;
    // One EC connection shared by every service that talks to the hardware.
    private readonly IRazerTransport _transport;
    private readonly IPowerSource _powerSource;
    private readonly bool _ownsDependencies;
    private readonly BatterySection _batterySection;
    private readonly DisplaySection _displaySection;
    private readonly FanSection _fanSection;
    private readonly PerformanceSection _performanceSection;
    private readonly ServicesSection _servicesSection;
    private readonly Label _headerStatusLabel = CreateHeaderStatusLabel();
    private readonly ToolTip _toolTip = new();
    private TableLayoutPanel _content = null!;
    private AppSettings _settings;

    /// <summary>The real app: talks to the actual laptop, Windows services and user profile.</summary>
    public TrayPopupForm()
        : this(
            new RazerHidTransport(),
            new PowerSourceService(),
            new WindowsServiceControl(),
            new SettingsService(),
            ownsDependencies: true)
    {
    }

    /// <summary>
    /// Everything the popup talks to, supplied from outside. The app passes
    /// the real ones; a test or a screenshot tool can pass fakes and look at the
    /// real popup without touching the laptop, its services or the user's settings.
    /// </summary>
    internal TrayPopupForm(
        IRazerTransport transport,
        IPowerSource powerSource,
        IServiceControl serviceControl,
        SettingsService settingsService,
        bool ownsDependencies = false)
    {
        _transport = transport;
        _powerSource = powerSource;
        _settingsService = settingsService;
        _ownsDependencies = ownsDependencies;

        _settings = _settingsService.Load();

        _fanSection = new FanSection(new FanTelemetryService(_transport));

        _performanceSection = new PerformanceSection(
            new PerformanceService(_transport),
            _powerSource,
            _settings.PluggedInProfile,
            _settings.OnBatteryProfile);
        _performanceSection.ProfileChanged += PerformanceSection_ProfileChanged;
        _performanceSection.CustomRowVisibilityChanged += PerformanceSection_CustomRowVisibilityChanged;
        _performanceSection.StatusChanged += Section_StatusChanged;

        _displaySection = new DisplaySection(
            new DisplayService(),
            _powerSource,
            DisplayRefreshMode.Parse(_settings.DisplayMode));
        _displaySection.DisplayModeChanged += DisplaySection_DisplayModeChanged;

        _batterySection = new BatterySection(
            new BatteryChargeLimitService(_transport),
            _settings.BatteryChargeLimit);
        _batterySection.ChargeLimitApplied += BatterySection_ChargeLimitApplied;
        _batterySection.StatusChanged += Section_StatusChanged;

        _servicesSection = new ServicesSection(
            new RazerServiceManager(serviceControl),
            _settings.RazerServiceStartModes);
        _servicesSection.HasServicesChanged += ServicesSection_HasServicesChanged;
        _servicesSection.StartModesRecorded += ServicesSection_StartModesRecorded;
        _servicesSection.ModalStateChanged += ServicesSection_ModalStateChanged;
        _servicesSection.StatusChanged += Section_StatusChanged;

        // Keep the tray popup's design surface stable across display scales.
        AutoScaleMode = AutoScaleMode.None;

        ApplyTheme();
        BuildView();
        CheckForSupportedDevice();

        _displaySection.Restore();
        _ = _batterySection.RestoreAsync();
        _ = _performanceSection.RestoreAsync();
        _ = _servicesSection.RefreshAsync();

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
        // Wide enough that the longest button label ("Medium" in the Custom row)
        // fits with a clear gap between the CPU and GPU groups.
        ClientSize = new Size(584, 600);
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
        // The Performance and Razer services rows change height; see
        // ResizeToFitRows.
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F)); // Header
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, PerformanceBaseRowHeight)); // Performance
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, FanRowHeight)); // Fan
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F)); // Display
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F)); // Battery
        _content.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F)); // Razer services (bottom)

        _content.Controls.Add(CreateAppHeader(), 0, 0);
        _content.Controls.Add(_performanceSection, 0, PerformanceRow);
        _content.Controls.Add(_fanSection, 0, 2);
        _content.Controls.Add(_displaySection, 0, 3);
        _content.Controls.Add(_batterySection, 0, 4);
        _content.Controls.Add(_servicesSection, 0, ServicesRow);

        Controls.Add(_content);

        _customRowShown = _performanceSection.IsCustomRowShown;
        ResizeToFitRows();
    }

    // Two rows change height: Performance gains the Custom boost row, and the
    // Razer services row only exists when Razer's software is installed. Grow
    // or shrink the popup to match, then re-anchor it to the taskbar so it
    // does not end up floating or overlapping it.
    private void ResizeToFitRows()
    {
        _content.RowStyles[PerformanceRow].Height = PerformanceBaseRowHeight +
            (_customRowShown ? CustomBoostRow.RowHeight : 0);
        _content.RowStyles[ServicesRow].Height = _servicesRowShown ? ServicesSection.RowHeight : 0;

        var contentHeight = _content.Padding.Vertical +
            _content.RowStyles.Cast<RowStyle>().Sum(row => row.Height);

        ClientSize = new Size(ClientSize.Width, (int)contentHeight);

        if (Visible)
            Location = TaskbarPlacement.GetPopupLocation(Size);
    }

    // Left: the app name. Right: the laptop model, or a red message when
    // something fails. The bottom of the popup belongs to the Razer services
    // row, so this is where status text lives.
    private Control CreateAppHeader()
    {
        var header = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        header.Controls.Add(new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = CreateDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Text = "RazerHelper",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        header.Controls.Add(_headerStatusLabel, 1, 0);

        return header;
    }

    private static Label CreateHeaderStatusLabel() => new()
    {
        AutoEllipsis = true,
        AutoSize = false,
        Dock = DockStyle.Fill,
        Font = CreateDesignFont("Segoe UI", 8.5F),
        ForeColor = SubtleTextColor,
        Margin = new Padding(12, 0, 0, 0),
        Text = ModelText,
        TextAlign = ContentAlignment.MiddleRight
    };

    // Successes are visible in the controls themselves, so only failures are
    // worth words. They replace the model name in red until the next result.
    private void ShowStatus(SectionStatus status)
    {
        _headerStatusLabel.ForeColor = status.IsError ? Color.IndianRed : SubtleTextColor;
        _headerStatusLabel.Text = status.IsError ? status.Message : ModelText;

        // The label cuts long text short with an ellipsis; the tooltip has the rest.
        _toolTip.SetToolTip(_headerStatusLabel, status.IsError ? status.Message : string.Empty);
    }

    private void CheckForSupportedDevice()
    {
        if (new DeviceSupportService().IsSupportedDevicePresent())
        {
            AppLog.Info($"{DeviceSupportService.SupportedModelName} control interface found.");
            return;
        }

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

    private void DisplaySection_DisplayModeChanged(object? sender, DisplayRefreshMode mode) =>
        SaveSettings(_settings with { DisplayMode = mode.Label });

    private void PerformanceSection_ProfileChanged(object? sender, PowerProfileChange change) =>
        SaveSettings(change.PluggedIn
            ? _settings with { PluggedInProfile = change.Profile }
            : _settings with { OnBatteryProfile = change.Profile });

    private void BatterySection_ChargeLimitApplied(object? sender, int limit) =>
        SaveSettings(_settings with { BatteryChargeLimit = limit });

    private void ServicesSection_StartModesRecorded(object? sender, Dictionary<string, ServiceStartMode> modes) =>
        SaveSettings(_settings with { RazerServiceStartModes = modes });

    private void PerformanceSection_CustomRowVisibilityChanged(object? sender, bool shown)
    {
        _customRowShown = shown;
        ResizeToFitRows();
    }

    private void ServicesSection_HasServicesChanged(object? sender, bool hasServices)
    {
        _servicesRowShown = hasServices;
        ResizeToFitRows();
    }

    // A dialog or the elevation prompt takes focus from the popup, which
    // would hide it (and the dialog with it) unless auto-hide is held off.
    private void ServicesSection_ModalStateChanged(object? sender, bool isModal) =>
        _modalDepth += isModal ? 1 : -1;

    private void Section_StatusChanged(object? sender, SectionStatus status) =>
        ShowStatus(status);

    private void HideWhenInactive()
    {
        if (!_allowClose && _modalDepth == 0 && Visible && !ContainsFocus)
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
        // Sections unsubscribe from the shared services as they are disposed,
        // so those must go first; then the form releases what it created.
        base.Dispose(disposing);

        if (disposing)
        {
            _toolTip.Dispose();

            // Only what the form created itself; supplied dependencies belong to the caller.
            if (_ownsDependencies)
            {
                (_powerSource as IDisposable)?.Dispose();
                (_transport as IDisposable)?.Dispose();
            }
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

            // Razer's services can be started or stopped from outside the app.
            _ = _servicesSection.RefreshAsync();
        }
        else
            _fanSection.StopPolling();
    }
}
