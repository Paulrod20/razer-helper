namespace RazerHelper.Core.Models;

/// <summary>The temperatures read together in one poll. A null side means there was no reading for it.</summary>
internal sealed record TemperatureReading(double? CpuCelsius, double? GpuCelsius)
{
    public static TemperatureReading None { get; } = new(null, null);
}
