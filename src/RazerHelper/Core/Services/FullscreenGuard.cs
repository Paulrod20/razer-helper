using System.Runtime.InteropServices;

namespace RazerHelper.Core.Services;

/// <summary>Whether a fullscreen game (or presentation) has the screen. An interface so the logic can be tested without one.</summary>
internal interface IFullscreenDetector
{
    bool IsFullscreenAppRunning();
}

/// <summary>
/// Asks Windows whether a fullscreen app is running: the same question other
/// programs ask before they interrupt a game with a notification. It covers
/// exclusive fullscreen and most borderless-fullscreen games.
/// </summary>
internal sealed class WindowsFullscreenDetector : IFullscreenDetector
{
    // QUERY_USER_NOTIFICATION_STATE in Windows' shellapi.h.
    private const int Busy = 2;                 // A fullscreen app is running.
    private const int RunningD3dFullScreen = 3; // A Direct3D fullscreen game is running.
    private const int PresentationMode = 4;     // Presenting (PowerPoint and similar).

    public bool IsFullscreenAppRunning() =>
        SHQueryUserNotificationState(out var state) == 0 && IsFullscreenState(state);

    internal static bool IsFullscreenState(int state) =>
        state is Busy or RunningD3dFullScreen or PresentationMode;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}

/// <summary>For tests and previews: never a fullscreen app.</summary>
internal sealed class NoFullscreenDetector : IFullscreenDetector
{
    public bool IsFullscreenAppRunning() => false;
}

/// <summary>
/// Keeps automatic display changes from happening while a fullscreen game has
/// the screen, where changing the refresh rate under it can make it flicker or
/// drop out. An automatic change asks first; if a game is running it is put on
/// hold, and the owner retries until the game is gone.
/// </summary>
internal sealed class FullscreenGuard(IFullscreenDetector detector)
{
    /// <summary>True while an automatic change is on hold, waiting for the game to end.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>Whether the change may be made now. False puts it on hold.</summary>
    public bool CanApplyNow()
    {
        IsWaiting = detector.IsFullscreenAppRunning();
        return !IsWaiting;
    }

    /// <summary>True when a change is on hold and the game has since ended.</summary>
    public bool ReadyToRetry => IsWaiting && !detector.IsFullscreenAppRunning();

    /// <summary>Drops a change that was on hold, for example because the user picked a fixed rate instead.</summary>
    public void Cancel() => IsWaiting = false;
}
