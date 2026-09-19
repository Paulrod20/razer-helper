namespace RazerHelper.Core.Models;

/// <summary>
/// What the EC reports right now. Cpu, Gpu and MaxFan only exist in Custom
/// mode, so they are null in every other mode (the EC's max fan read-back is
/// meaningless there). A null Mode means unreadable or a mode this app does
/// not offer.
/// </summary>
internal sealed record PerformanceState(
    PerformanceMode? Mode,
    CpuBoost? Cpu,
    GpuBoost? Gpu,
    bool? MaxFan = null)
{
    public static PerformanceState Unknown { get; } = new(null, null, null);
}
