using System.Runtime.InteropServices;

namespace RazerHelper.Helpers;

public static class TaskbarPlacement
{
    public static Point GetPopupLocation(Size popupSize)
    {
        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (taskbarHandle == IntPtr.Zero || !GetWindowRect(taskbarHandle, out var taskbarRect))
        {
            var fallback = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Point.Empty);
            return new Point(fallback.Right - popupSize.Width - 12, fallback.Bottom - popupSize.Height - 12);
        }

        var taskbar = Rectangle.FromLTRB(taskbarRect.Left, taskbarRect.Top, taskbarRect.Right, taskbarRect.Bottom);
        var screen = Screen.FromRectangle(taskbar).WorkingArea;
        var isHorizontal = taskbar.Width >= taskbar.Height;

        if (isHorizontal)
        {
            var y = taskbar.Top > screen.Top ? taskbar.Top - popupSize.Height - 8 : taskbar.Bottom + 8;
            var x = Math.Clamp(taskbar.Right - popupSize.Width - 8, screen.Left + 8, screen.Right - popupSize.Width - 8);
            return new Point(x, y);
        }

        var verticalX = taskbar.Left > screen.Left ? taskbar.Left - popupSize.Width - 8 : taskbar.Right + 8;
        var verticalY = Math.Clamp(taskbar.Bottom - popupSize.Height - 8, screen.Top + 8, screen.Bottom - popupSize.Height - 8);
        return new Point(verticalX, verticalY);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out Rect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
