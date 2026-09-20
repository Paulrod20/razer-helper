namespace RazerHelper.Core.Services;

/// <summary>
/// How much bigger than the base design the whole window is drawn. The window is
/// laid out in fixed pixels so it looks the same on every display, which makes it
/// small on a dense screen (a 4K laptop panel at 225% Windows scaling). This picks
/// a multiplier that grows with Windows' own scaling, but only part of the way:
/// enough to read comfortably without the window taking over the screen.
/// </summary>
internal static class UiScale
{
    private const float Smallest = 0.75F;
    private const float Largest = 3F;

    // How much of Windows' extra scaling (above 100%) is applied.
    private const float FollowsWindows = 0.25F;

    /// <summary>The saved choice if it is sensible, otherwise the automatic size for this display.</summary>
    public static float Resolve(double? saved, float windowsScale) =>
        saved is { } value && value >= Smallest && value <= Largest
            ? (float)value
            : Automatic(windowsScale);

    /// <summary>100% Windows scaling gives exactly the base size; higher scaling grows it a quarter as fast, in steps of 5%.</summary>
    internal static float Automatic(float windowsScale)
    {
        var scale = 1F + FollowsWindows * (Math.Max(windowsScale, 1F) - 1F);
        // Halves round up, so 150% Windows scaling (1.125) gives 1.15 and not 1.10.
        return (float)(Math.Round(scale * 20.0, MidpointRounding.AwayFromZero) / 20.0);
    }
}
