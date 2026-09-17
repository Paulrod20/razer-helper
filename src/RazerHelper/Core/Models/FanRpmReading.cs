namespace RazerHelper.Core.Models;

public sealed record FanRpmReading(
    int CpuFanRpm,
    int GpuFanRpm
);
