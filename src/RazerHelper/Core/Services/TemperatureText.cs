namespace RazerHelper.Core.Services;

/// <summary>How temperatures are written on screen.</summary>
internal static class TemperatureText
{
    /// <summary>
    /// The readings that exist, side by side, for example "GPU: 62\u00B0C" or
    /// "CPU: 71\u00B0C  \u00B7  GPU: 62\u00B0C". Empty when there is nothing to show, so a
    /// missing sensor leaves no placeholder and no wrong number.
    /// </summary>
    public static string Format(double? cpuCelsius, double? gpuCelsius)
    {
        var parts = new List<string>();

        if (cpuCelsius is { } cpu)
            parts.Add($"CPU: {Round(cpu)}\u00B0C");

        if (gpuCelsius is { } gpu)
            parts.Add($"GPU: {Round(gpu)}\u00B0C");

        return string.Join("  \u00B7  ", parts);
    }

    /// <summary>
    /// Where each shown temperature comes from, for a hover note. The CPU one
    /// says plainly that it is a sensor near the CPU and reads lower than die
    /// sensors, so nobody compares it with another tool and feels misled.
    /// Empty when there is nothing shown.
    /// </summary>
    public static string Explain(double? cpuCelsius, double? gpuCelsius)
    {
        var lines = new List<string>();

        if (cpuCelsius is not null)
        {
            lines.Add("CPU: read from the laptop's controller.");
            lines.Add("It is a sensor near the CPU, so it reads");
            lines.Add("lower than die sensors like Afterburner's.");
        }

        if (gpuCelsius is not null)
            lines.Add("GPU: read from the graphics driver.");

        return string.Join("\n", lines);
    }

    // Whole degrees: a tenth of a degree is noise on a fluctuating reading.
    private static int Round(double celsius) => (int)Math.Round(celsius, MidpointRounding.AwayFromZero);
}
