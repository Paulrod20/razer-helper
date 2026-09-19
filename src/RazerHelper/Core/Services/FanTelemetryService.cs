using System.ComponentModel;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class FanTelemetryService(IRazerTransport transport)
{
    private const byte CpuFanZone = 0x01;
    private const byte GpuFanZone = 0x02;

    private FanRpmReading? _publishedReading;
    private bool _stoppedReadingPending;
    private bool _readFailing;

    public Task<FanRpmReading?> ReadAsync() => Task.Run(() =>
    {
        try
        {
            var reading = Read();

            if (_readFailing)
            {
                _readFailing = false;
                AppLog.Info("Fan telemetry recovered.");
            }

            return reading;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception)
        {
            // Polled every couple of seconds: log only the first failure of a
            // streak so a disconnected device does not flood the log.
            if (!_readFailing)
            {
                _readFailing = true;
                AppLog.Error("Fan telemetry is temporarily unavailable.", exception);
            }

            return _publishedReading;
        }
    });

    private FanRpmReading? Read()
    {
        var reading = new FanRpmReading(
            ReadFanRpm(CpuFanZone),
            ReadFanRpm(GpuFanZone));

        return Filter(reading);
    }

    private int ReadFanRpm(byte fanZone)
    {
        var response = transport.Send(
            RazerCommands.GetActualFanRpm,
            [0x00, fanZone, 0x00]);

        if (RazerHidPacket.GetArgument(response, 1) != fanZone)
            throw new InvalidOperationException("The fan response used an unexpected zone.");

        return RazerHidPacket.GetArgument(response, 2) * 100;
    }

    private FanRpmReading? Filter(FanRpmReading reading)
    {
        var hasOnlyOneStoppedFan =
            (reading.CpuFanRpm == 0) != (reading.GpuFanRpm == 0);

        if (hasOnlyOneStoppedFan)
            return _publishedReading;

        var bothFansStopped =
            reading.CpuFanRpm == 0 && reading.GpuFanRpm == 0;

        if (bothFansStopped && !_stoppedReadingPending)
        {
            _stoppedReadingPending = true;
            return _publishedReading;
        }

        _stoppedReadingPending = false;
        _publishedReading = reading;
        return _publishedReading;
    }
}
