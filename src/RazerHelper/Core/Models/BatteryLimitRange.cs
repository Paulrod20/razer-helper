namespace RazerHelper.Core.Models;

/// <summary>
/// The charge limits the app offers. This is the one place the range is
/// defined; the slider and the EC service both read it, so widening the range
/// later is a change here and nowhere else.
/// </summary>
internal static class BatteryLimitRange
{
    public const int Minimum = 60;
    public const int Maximum = 100;
    public const int Step = 20;

    /// <summary>100% means "charge without a limit".</summary>
    public const int NoLimit = Maximum;

    public static bool IsValid(int percent) =>
        percent is >= Minimum and <= Maximum && (percent - Minimum) % Step == 0;

    /// <summary>Clamps to the range and snaps to the nearest offered step.</summary>
    public static int Normalize(int percent)
    {
        var clamped = Math.Clamp(percent, Minimum, Maximum);
        var steps = (int)Math.Round((clamped - Minimum) / (double)Step, MidpointRounding.AwayFromZero);
        return Minimum + steps * Step;
    }
}
