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

    // The popup uses AutoScaleMode.None, so fonts are sized against the system
    // DPI here to keep the design surface stable across display scales.
    public static Font CreateDesignFont(
        string familyName,
        float pointSize,
        FontStyle style = FontStyle.Regular) =>
        new(familyName, pointSize / DpiScale, style, GraphicsUnit.Point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
