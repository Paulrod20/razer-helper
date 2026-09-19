using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// CPU and GPU fan speed readout. Polls the telemetry service
/// only while the host says the popup is visible.
/// </summary>
internal sealed class FanSection : SectionPanel
{
    private const int PollIntervalMilliseconds = 2_000;

    // Each button plus its margins.
    private const int ModeButtonCellWidth = 140;

    private readonly FanTelemetryService _telemetryService;
    private readonly System.Windows.Forms.Timer _pollTimer = new()
    {
        Interval = PollIntervalMilliseconds
    };
    private readonly Label _cpuFanLabel;
    private readonly Label _gpuFanLabel;

    private bool _isPolling;
    private bool _refreshInProgress;

    public FanSection(FanTelemetryService telemetryService)
    {
        _telemetryService = telemetryService;

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

        // Auto | Max, as in Synapse's "Max Fan Speed Mode". Not connected to the
        // laptop yet, so both stay disabled (Auto shown as the current mode)
        // rather than looking live and doing nothing.
        var modeGrid = CreateButtonGrid(["Auto", "Max"], "FanModeButton");
        var modeButtons = modeGrid.Controls.OfType<Button>().ToArray();

        // Compact rather than full width, and left-aligned like the readout above.
        modeGrid.Dock = DockStyle.Left;
        modeGrid.Width = ModeButtonCellWidth * modeButtons.Length;

        foreach (var button in modeButtons)
            button.Enabled = false;

        HighlightSelected(modeButtons, modeButtons[0]);

        // Dock order: the header docks first, then the readings, and the mode
        // buttons fill what is left.
        Controls.Add(modeGrid);
        Controls.Add(readings);
        Controls.Add(CreateSectionHeader("Fans", string.Empty));

        _pollTimer.Tick += PollTimer_Tick;
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
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= PollTimer_Tick;
            _pollTimer.Dispose();
        }

        base.Dispose(disposing);
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
            var reading = await _telemetryService.ReadAsync();

            if (!_isPolling || reading is null)
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
