namespace RazerHelper.Core.Hardware;

/// <summary>Where the CPU-side temperature comes from. An interface so the display can be tested without the laptop.</summary>
internal interface ICpuTemperatureSource
{
    /// <summary>The temperature in degrees Celsius, or null when there is no reading.</summary>
    double? ReadCelsius();
}

/// <summary>For tests and previews: no CPU temperature to report.</summary>
internal sealed class NoCpuTemperature : ICpuTemperatureSource
{
    public double? ReadCelsius() => null;
}
