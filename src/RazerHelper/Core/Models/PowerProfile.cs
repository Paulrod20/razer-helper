namespace RazerHelper.Core.Models;

/// <summary>
/// Performance settings remembered for one power source. A null field means
/// "leave whatever the EC has"; Cpu and Gpu only matter in Custom mode.
/// </summary>
internal sealed record PowerProfile(
    PerformanceMode? Mode = null,
    CpuBoost? Cpu = null,
    GpuBoost? Gpu = null)
{
    /// <summary>The battery profile: Balanced is the only mode offered on battery, as in Synapse.</summary>
    public static PowerProfile DefaultOnBattery { get; } = new(PerformanceMode.Balanced);
}
