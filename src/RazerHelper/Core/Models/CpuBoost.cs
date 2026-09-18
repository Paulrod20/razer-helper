namespace RazerHelper.Core.Models;

/// <summary>
/// CPU boost level in Custom performance mode. Values are the EC's wire bytes.
/// The EC also accepts 4 for the CPU overclock toggle, which is handled
/// separately from this selector.
/// </summary>
public enum CpuBoost : byte
{
    Low = 0,
    Medium = 1,
    High = 2,
    Boost = 3
}
