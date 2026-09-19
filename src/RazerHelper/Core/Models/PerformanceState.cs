namespace RazerHelper.Core.Models;

/// <summary>
/// What the EC reports right now. Cpu and Gpu are only read in Custom mode,
/// so they are null in every other mode. A null Mode means unreadable or a
/// mode this app does not offer.
/// </summary>
internal sealed record PerformanceState(
    PerformanceMode? Mode,
    CpuBoost? Cpu,
    GpuBoost? Gpu)
{
    public static PerformanceState Unknown { get; } = new(null, null, null);
}
