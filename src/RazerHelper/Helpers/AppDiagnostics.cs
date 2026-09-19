using System.Reflection;
using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Helpers;

/// <summary>
/// What the app records about itself: start and exit lines in the log, and the
/// process-wide hooks that make sure no failure is silent.
/// </summary>
internal static class AppDiagnostics
{
    /// <summary>
    /// Written when the app starts. A start line with no matching exit line
    /// means the process died.
    /// </summary>
    public static void LogStart() =>
        AppLog.Info(
            $"RazerHelper {GetVersion()} starting " +
            $"({RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}).");

    public static void LogExit() => AppLog.Info("RazerHelper exited normally.");

    /// <summary>
    /// Routes every exception nobody caught to the log, and to
    /// <paramref name="notifyUser"/> at the right moments (see
    /// <see cref="UnhandledExceptionHandler"/>). Must run before any control
    /// is created, or WinForms ignores the exception mode set here.
    /// </summary>
    public static void InstallExceptionHandling(Action<string> notifyUser)
    {
        var handler = new UnhandledExceptionHandler(AppLog.Error, notifyUser, AppLog.LogFilePath);

        // Route UI-thread exceptions to us instead of WinForms' own dialog.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => handler.OnRecoverableException(e.Exception);

        // Any other thread: the process is about to end.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            handler.OnFatalException(e.ExceptionObject as Exception
                ?? new InvalidOperationException(e.ExceptionObject?.ToString()));

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            handler.OnUnobservedTaskException(e.Exception);
            e.SetObserved();
        };
    }

    private static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown version";
}
