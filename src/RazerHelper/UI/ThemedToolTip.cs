using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A small tooltip in the app's dark theme. Windows' stock tooltip is a pale
/// yellow box in the system font, which clashes with the popup and cannot be
/// made smaller, so this one draws itself.
/// </summary>
internal sealed class ThemedToolTip : ToolTip
{
    private static readonly Font TipFont = CreateDesignFont("Segoe UI", 8F);
    private static readonly Padding TextPadding = new(7, 3, 7, 3);

    public ThemedToolTip()
    {
        OwnerDraw = true;

        // The popup is a tray window that is often not the active one.
        ShowAlways = true;

        Popup += ThemedToolTip_Popup;
        Draw += ThemedToolTip_Draw;
    }

    // Size the tip to its text, so a short message gives a small box.
    private void ThemedToolTip_Popup(object? sender, PopupEventArgs e)
    {
        var text = e.AssociatedControl is null ? string.Empty : GetToolTip(e.AssociatedControl);
        var textSize = TextRenderer.MeasureText(text, TipFont, Size.Empty, TextFormatFlags.NoPadding);

        e.ToolTipSize = new Size(
            textSize.Width + TextPadding.Horizontal,
            textSize.Height + TextPadding.Vertical);
    }

    private void ThemedToolTip_Draw(object? sender, DrawToolTipEventArgs e)
    {
        using var background = new SolidBrush(ButtonColor);
        using var border = new Pen(BorderColor);

        e.Graphics.FillRectangle(background, e.Bounds);
        e.Graphics.DrawRectangle(border, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

        // A short message reads best centered; a list of lines reads best from the left.
        var alignment = e.ToolTipText?.Contains('\n') == true ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;

        TextRenderer.DrawText(
            e.Graphics,
            e.ToolTipText,
            TipFont,
            new Rectangle(
                e.Bounds.X + TextPadding.Left,
                e.Bounds.Y,
                e.Bounds.Width - TextPadding.Horizontal,
                e.Bounds.Height),
            Color.Gainsboro,
            alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
