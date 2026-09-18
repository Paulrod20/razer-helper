using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Fan readout and (not yet functional) fan mode buttons. Owns the telemetry
/// service and polls it only while the host says the popup is visible.
/// </summary>
internal sealed class FanSection : Panel
{
    private const int PollIntervalMilliseconds = 2_000;

    private readonly FanTelemetryService _telemetryService = new();
    private readonly System.Windows.Forms.Timer _pollTimer = new()
    {
        Interval = PollIntervalMilliseconds
    };
    private readonly Label _cpuFanLabel;
    private readonly Label _gpuFanLabel;

    private bool _isPolling;
    private bool _refreshInProgress;

    public FanSection()
    {
        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        _cpuFanLabel = CreateReadingLabel("CPU Fan: -- RPM", DockStyle.Left);
        _gpuFanLabel = CreateReadingLabel("GPU Fan: -- RPM", DockStyle.Right);

        var readings = CreateTwoColumnLayout(50F, 50F);
        readings.Dock = DockStyle.Top;
        readings.Height = 24;
        readings.Padding = new Padding(0, 0, 0, 2);
        readings.Controls.Add(_cpuFanLabel, 0, 0);
        readings.Controls.Add(_gpuFanLabel, 1, 0);

        Controls.Add(CreateButtonGrid(["Auto", "Max", "Manual"], "FanButton"));
        Controls.Add(readings);
        Controls.Add(CreateSectionHeader("Fan Control", string.Empty));

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
            _telemetryService.Dispose();
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

    private static Label CreateReadingLabel(string text, DockStyle dock) => new()
    {
        AutoSize = true,
        Dock = dock,
        Font = CreateDesignFont("Segoe UI", 9.5F),
        ForeColor = Color.Silver,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };
}
