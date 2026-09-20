using System.ComponentModel;
using System.Diagnostics;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>The real thing: identifies a process and asks its main window to close. Never kills.</summary>
internal sealed class WindowsProcessControl : IProcessControl
{
    public ProcessIdentity? Identify(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return new ProcessIdentity(process.ProcessName, ReadStartTime(process));
        }
        catch (Exception exception) when (IsProcessAccessFailure(exception))
        {
            return null;
        }
    }

    public bool RequestClose(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            // WM_CLOSE to the main window. The app decides what happens next,
            // which includes asking the user to save.
            return process.CloseMainWindow();
        }
        catch (Exception exception) when (IsProcessAccessFailure(exception))
        {
            AppLog.Error($"Could not ask process {processId} to close.", exception);
            return false;
        }
    }

    private static DateTime? ReadStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Exception exception) when (IsProcessAccessFailure(exception))
        {
            return null;
        }
    }

    private static bool IsProcessAccessFailure(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or NotSupportedException or Win32Exception;
}
