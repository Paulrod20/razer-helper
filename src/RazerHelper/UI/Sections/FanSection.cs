using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// CPU and GPU fan speed readout. Every poll it also reads the GPU temperature
/// and announces it (<see cref="GpuTemperatureRead"/>), for the host to show
/// wherever it likes. Polls only while the host says the popup is visible, so
/// nothing is read (and the GPU is never asked anything) while the app sits in the tray.
/// </summary>
internal sealed class FanSection : SectionPanel
{
    private const int PollIntervalMilliseconds = 2_000;

    // Each button plus its margins.
    private const int ModeButtonCellWidth = 140;

    private const string MaxUnavailableHint = "Needs Custom mode, plugged in";

    private readonly FanTelemetryService _telemetryService;
    private readonly IGpuTemperatureSource _gpuTemperature;
    private readonly IPowerSource _powerSource;
    private readonly ThemedToolTip _toolTip = new();
    private readonly System.Windows.Forms.Timer _pollTimer = new()
    {
        Interval = PollIntervalMilliseconds
    };
    private readonly Label _cpuFanLabel;
    private readonly Label _gpuFanLabel;
    private readonly Button[] _modeButtons;
    private readonly Button _autoButton;
    private readonly Button _maxButton;

    private PerformanceState _performanceState = PerformanceState.Unknown;
    private bool _isMaxAvailable;
    private bool _isPolling;
    private bool _refreshInProgress;

    public FanSection(
        FanTelemetryService telemetryService,
        IPowerSource powerSource,
        IGpuTemperatureSource gpuTemperature)
    {
        _telemetryService = telemetryService;
        _powerSource = powerSource;
        _gpuTemperature = gpuTemperature;

        _cpuFanLabel = CreateReadingLabel("CPU Fan: -- RPM");
        _gpuFanLabel = CreateReadingLabel("GPU Fan: -- RPM");

        // Each reading sits above its own button: CPU over Auto, GPU over Max,
        // side by side. The columns are the same width as the button cells
        // below, so the text lines up with the left edge of each button.
        var readings = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Height = 24,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 2),
            RowCount = 1
        };

        readings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ModeButtonCellWidth));
        readings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ModeButtonCellWidth));
        readings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        readings.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        readings.Controls.Add(_cpuFanLabel, 0, 0);
        readings.Controls.Add(_gpuFanLabel, 1, 0);

        // Auto | Max, as in Synapse's "Max Fan Speed Mode". Max is only offered in
        // Custom mode, plugged in; Auto turns it off again.
        var modeGrid = CreateButtonGrid(["Auto", "Max"], "FanModeButton");
        _modeButtons = modeGrid.Controls.OfType<Button>().ToArray();
        _autoButton = _modeButtons[0];
        _maxButton = _modeButtons[1];

        // Compact rather than full width, and left-aligned like the readout above.
        modeGrid.Dock = DockStyle.Left;
        modeGrid.Width = ModeButtonCellWidth * _modeButtons.Length;

        _autoButton.Click += (_, _) => RequestMaxFan(false);
        _maxButton.Click += (_, _) => RequestMaxFan(true);
        UpdateModeButtons();

        // Dock order: the header docks first, then the readings, and the mode
        // buttons fill what is left.
        Controls.Add(modeGrid);
        Controls.Add(readings);
        Controls.Add(CreateSectionHeader("Fans", string.Empty));

        _pollTimer.Tick += PollTimer_Tick;
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    /// <summary>Raised when the user asks for max fan speed on (true) or off (false). The host performs it.</summary>
    public event EventHandler<bool>? MaxFanRequested;

    /// <summary>Raised after every poll with the GPU temperature in Celsius, or null when there is no reading.</summary>
    public event EventHandler<double?>? GpuTemperatureRead;

    /// <summary>Tells the fan buttons which performance mode the laptop is in, since Max depends on it.</summary>
    public void ShowPerformanceState(PerformanceState state)
    {
        _performanceState = state;
        UpdateModeButtons();
    }

    /// <summary>Starts polling and takes an immediate reading.</summary>
    public void StartPolling()
    {
        _isPolling = true;
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    public void StopPolling()
    {
        _isPolling = false;
        _pollTimer.Stop();

        // A reading only means something for as long as it is being refreshed.
        // Clearing it now means reopening the popup never shows a temperature
        // from minutes ago while the first fresh one is on its way.
        GpuTemperatureRead?.Invoke(this, null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTimer_Tick;
            _pollTimer.Dispose();
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e) =>
        PostToUi(UpdateModeButtons);

    // Shows what the laptop is doing (Max when its flag is on, otherwise Auto)
    // and whether Max can be chosen. When it cannot (it needs Custom mode,
    // plugged in) the button is drawn like a disabled one and hovering it says
    // why. It is deliberately still enabled underneath, because WinForms shows
    // no tooltip on a disabled control, so RequestMaxFan must refuse the click.
    private void UpdateModeButtons()
    {
        var pluggedIn = PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn);
        _isMaxAvailable = PowerProfileRules.CanUseMaxFan(_performanceState, pluggedIn);

        // Highlighting resets the text colors, so the unavailable look goes on after it.
        HighlightSelected(_modeButtons, _performanceState.MaxFan == true ? _maxButton : _autoButton);
        SetAvailability(_maxButton, _isMaxAvailable, _toolTip, MaxUnavailableHint);
    }

    private void RequestMaxFan(bool enabled)
    {
        // Turning it on needs Max to be available; turning it off never does.
        if (enabled && !_isMaxAvailable)
            return;

        // Already in that state: nothing to change.
        if (enabled == (_performanceState.MaxFan == true))
            return;

        MaxFanRequested?.Invoke(this, enabled);
    }

    private async void PollTimer_Tick(object? sender, EventArgs e) =>
        await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_refreshInProgress)
            return;

        _refreshInProgress = true;

        try
        {
            // Both are read at once, so the temperature never trails the fan speeds.
            var temperature = Task.Run(_gpuTemperature.ReadCelsius);
            var reading = await _telemetryService.ReadAsync();
            var gpuCelsius = await temperature;

            if (!_isPolling)
                return;

            GpuTemperatureRead?.Invoke(this, gpuCelsius);

            if (reading is null)
                return;

            ShowReading(_cpuFanLabel, "CPU Fan", reading.CpuFanRpm);
            ShowReading(_gpuFanLabel, "GPU Fan", reading.GpuFanRpm);
        }
        catch (Exception exception)
        {
            AppLog.Error("Fan telemetry read failed unexpectedly.", exception);

            if (_isPolling)
            {
                ShowReading(_cpuFanLabel, "CPU Fan", null);
                ShowReading(_gpuFanLabel, "GPU Fan", null);
                GpuTemperatureRead?.Invoke(this, null);
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private static void ShowReading(Label label, string name, int? rpm) =>
        label.Text = rpm is null ? $"{name}: -- RPM" : $"{name}: {rpm} RPM";

    private static Label CreateReadingLabel(string text) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Font = CreateDesignFont("Segoe UI", 9.5F),
        ForeColor = Color.Silver,
        Margin = new Padding(4, 0, 0, 0), // Same 4px inset as the buttons below.
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };
}
