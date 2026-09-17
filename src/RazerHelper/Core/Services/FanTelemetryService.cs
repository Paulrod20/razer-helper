using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

public sealed class FanTelemetryService : IDisposable
{
    private const ushort GetActualFanRpmCommand = 0x0D88;
    private const byte CpuFanZone = 0x01;
    private const byte GpuFanZone = 0x02;

    private readonly RazerHidTransport _transport = new();
    private FanRpmReading? _publishedReading;
    private bool _stoppedReadingPending;

    public Task<FanRpmReading?> ReadAsync() => Task.Run(Read);

    public void Dispose() => _transport.Dispose();

    private FanRpmReading? Read()
    {
        var reading = new FanRpmReading(
            ReadFanRpm(CpuFanZone),
            ReadFanRpm(GpuFanZone));

        return Filter(reading);
    }

    private int ReadFanRpm(byte fanZone)
    {
        var response = _transport.Send(
            GetActualFanRpmCommand,
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
