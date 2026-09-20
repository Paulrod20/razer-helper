namespace RazerHelper.Core.Models;

/// <summary>
/// Keyboard backlight effects the laptop runs by itself in Normal device mode.
/// A choice of solid colors is deliberately not here: on the Blade 16 the laptop
/// ignores a chosen color unless it is put in "driver mode", which switches off
/// the Fn media keys (volume, screen and keyboard brightness). In Normal mode a
/// static effect always shows Razer green, so that is what <see cref="StaticGreen"/> is.
/// </summary>
internal enum KeyboardEffect
{
    Off,

    /// <summary>One steady color: Razer green.</summary>
    StaticGreen,

    Spectrum,
    Wave,
    Breathing
}

/// <summary>What the Razer logo on the lid is doing.</summary>
internal enum LogoMode
{
    Off,

    /// <summary>Lit, steady.</summary>
    On,

    Breathing
}

/// <summary>
/// What the laptop's lighting is set to. A null field means "not something this
/// app offers", for example an effect set by other software, and is shown as no
/// selection rather than a wrong one. Brightness is a percentage, 0 to 100.
/// </summary>
internal sealed record LightingState(
    KeyboardEffect? Keyboard,
    int? KeyboardBrightness,
    LogoMode? Logo,
    int? LogoBrightness)
{
    public static LightingState Unknown { get; } = new(null, null, null, null);
}

/// <summary>Converts between the percentage shown to the user and the 0 to 255 byte the laptop uses.</summary>
internal static class LightingBrightness
{
    public const int MinimumPercent = 0;
    public const int MaximumPercent = 100;

    public static byte ToByte(int percent)
    {
        if (percent is < MinimumPercent or > MaximumPercent)
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Brightness must be 0 to 100 percent.");

        return (byte)Math.Round(percent * 255 / 100.0, MidpointRounding.AwayFromZero);
    }

    public static int ToPercent(byte value) =>
        (int)Math.Round(value * 100 / 255.0, MidpointRounding.AwayFromZero);
}
