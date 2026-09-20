using System.Runtime.InteropServices;

namespace RazerHelper.UI;

internal static class UiTheme
{
    private static readonly float DpiScale = GetDpiForSystem() / 96F;

    public static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    public static readonly Color ButtonColor = Color.FromArgb(50, 50, 50);
    public static readonly Color BorderColor = Color.FromArgb(80, 80, 80);
    public static readonly Color RazerGreen = Color.FromArgb(68, 214, 44);

    /// <summary>Quiet secondary text: the model name, hints and notes.</summary>
    public static readonly Color SubtleTextColor = Color.FromArgb(145, 145, 145);

    // The app only ever uses a handful of distinct fonts, and controls never
    // dispose a font they are handed, so each look is created once and shared.
    private static readonly Dictionary<(string Family, float Size, FontStyle Style), Font> DesignFonts = [];

    /// <summary>
    /// The shared font for this look, created the first time it is asked for.
    /// Never dispose it: every control that uses it shares the same object.
    /// </summary>
    /// <remarks>
    /// The popup uses AutoScaleMode.None, so fonts are sized against the system
    /// DPI here to keep the design surface stable across display scales.
    /// </remarks>
    public static Font GetDesignFont(
        string familyName,
        float pointSize,
        FontStyle style = FontStyle.Regular)
    {
        lock (DesignFonts)
        {
            var key = (familyName, pointSize, style);

            if (!DesignFonts.TryGetValue(key, out var font))
                DesignFonts[key] = font = new Font(familyName, pointSize / DpiScale, style, GraphicsUnit.Point);

            return font;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
