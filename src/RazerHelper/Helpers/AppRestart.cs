using System.Diagnostics;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Helpers;

/// <summary>
/// Restarts the app. The new copy is started with the old one's process id and
/// waits for it to finish exiting before it starts, because only one copy may run
/// at a time and the old one still holds that lock until it is gone.
/// </summary>
internal static class AppRestart
{
    internal const string Switch = "--restarted";

    private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(10);

    /// <summary>Starts a new copy of this app that waits for this one to exit. Returns false if it could not be started.</summary>
    public static bool Relaunch()
    {
        try
        {
            var executable = Environment.ProcessPath;

            if (executable is null)
                return false;

            using var started = Process.Start(new ProcessStartInfo(executable)
            {
                ArgumentList = { Switch, Environment.ProcessId.ToString() },
                UseShellExecute = false
            });

            return started is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppLog.Error("Could not restart RazerHelper.", exception);
            return false;
        }
    }

    /// <summary>At startup: if this copy was started by a restart, waits (up to 10 seconds) for the previous copy to exit.</summary>
    public static void WaitForPreviousCopy(string[] args)
    {
        if (!TryGetPreviousProcessId(args, out var processId))
            return;

        try
        {
            using var previous = Process.GetProcessById(processId);
            previous.WaitForExit(MaximumWait);
        }
        catch (ArgumentException)
        {
            // Already gone, which is what we wanted.
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            AppLog.Error("Could not wait for the previous RazerHelper to exit.", exception);
        }
    }

    internal static bool TryGetPreviousProcessId(string[] args, out int processId)
    {
        processId = 0;

        return args is [Switch, var value] &&
            int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out processId) &&
            processId > 0;
    }
}
