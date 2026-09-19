namespace RazerHelper.Core.Models;

/// <summary>
/// Performance settings remembered for one power source. A null field means
/// "leave whatever the EC has"; Cpu and Gpu only matter in Custom mode.
/// </summary>
public sealed record PowerProfile(
    PerformanceMode? Mode = null,
    CpuBoost? Cpu = null,
    GpuBoost? Gpu = null)
{
    /// <summary>What battery power gets until the user chooses otherwise, as in Synapse.</summary>
    public static PowerProfile DefaultOnBattery { get; } = new(PerformanceMode.Balanced);
}
