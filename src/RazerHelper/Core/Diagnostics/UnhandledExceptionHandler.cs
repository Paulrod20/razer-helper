namespace RazerHelper.Core.Diagnostics;

/// <summary>
/// Decides what to do with an exception nobody caught: always write it to the
/// log, and tell the user at the right moments. It has no dependency on
/// Windows or the log file, so the rules can be tested; <c>Program</c> wires
/// it to the real hooks.
/// </summary>
/// <remarks>
/// This runs when something has already gone wrong, so it must never throw
/// itself; a failure to show the message is logged and swallowed.
/// </remarks>
internal sealed class UnhandledExceptionHandler(
    Action<string, Exception?> log,
    Action<string> notifyUser,
    string logLocation)
{
    private int _recoverableErrorShown;

    /// <summary>
    /// An exception reached the UI thread (a click handler, a timer). The app
    /// is normally still usable, so it keeps running. The user is told once,
    /// because a timer can fail every couple of seconds and a dialog per
    /// failure would be worse than the bug.
    /// </summary>
    public void OnRecoverableException(Exception exception)
    {
        log("An unhandled exception reached the UI thread. The app is still running.", exception);

        if (Interlocked.Exchange(ref _recoverableErrorShown, 1) == 0)
        {
            Notify(
                "RazerHelper hit an unexpected error but is still running.\n\n" +
                "If something stops working, restart it. Details were saved to:\n" +
                logLocation);
        }
    }

    /// <summary>An exception on another thread is ending the process. Record it and say so.</summary>
    public void OnFatalException(Exception exception)
    {
        log("A fatal unhandled exception is ending the app.", exception);

        Notify(
            "RazerHelper hit an unexpected error and has to close.\n\n" +
            "Details were saved to:\n" +
            logLocation);
    }

    /// <summary>A background task failed and nothing was waiting on it. Worth a log line, never a dialog.</summary>
    public void OnUnobservedTaskException(Exception exception) =>
        log("A background task failed and nothing was waiting for its result.", exception);

    private void Notify(string message)
    {
        try
        {
            notifyUser(message);
        }
        catch (Exception exception)
        {
            log("Could not show the error message to the user.", exception);
        }
    }
}
