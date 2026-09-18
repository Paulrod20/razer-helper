namespace RazerHelper.Core.Models;

/// <summary>GPU boost level in Custom performance mode. Values are the EC's wire bytes.</summary>
public enum GpuBoost : byte
{
    Low = 0,
    Medium = 1,
    High = 2
}
