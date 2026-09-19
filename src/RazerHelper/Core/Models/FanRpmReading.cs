namespace RazerHelper.Core.Models;

internal sealed record FanRpmReading(
    int CpuFanRpm,
    int GpuFanRpm
);
