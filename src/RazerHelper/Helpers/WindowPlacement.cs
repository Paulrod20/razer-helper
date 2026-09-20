namespace RazerHelper.Helpers;

public static class WindowPlacement
{
    private const int Gap = 8;

    /// <summary>
    /// Where to put a window so it sits next to another one without covering it:
    /// on the left if there is room (the popup lives at the right edge, by the
    /// tray), otherwise on the right, lined up with the bottom of the other window.
    /// It is always kept fully inside the screen's usable area.
    /// </summary>
    public static Point Beside(Rectangle anchor, Size size, Rectangle workingArea)
    {
        var left = anchor.Left - size.Width - Gap;
        var right = anchor.Right + Gap;

        int x;

        if (left >= workingArea.Left)
            x = left;
        else if (right + size.Width <= workingArea.Right)
            x = right;
        else
            x = left; // Neither side has room; the clamp below keeps it on screen.

        var y = anchor.Bottom - size.Height;

        return new Point(
            Math.Clamp(x, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - size.Width)),
            Math.Clamp(y, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - size.Height)));
    }
}
