using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RazerHelper.Core.Services;

/// <summary>
/// Razer's own programs that are running (Synapse and its helpers), which are
/// not Windows services. An interface so the logic can be tested without
/// any real process.
/// </summary>
internal interface IRazerApps
{
    /// <summary>The distinct program names running now, for example "RazerAppEngine".</summary>
    IReadOnlyList<string> FindRunning();

    /// <summary>
    /// Asks each one to close, like clicking its X, and waits a moment.
    /// Never a force-kill. Returns the names still running afterwards: Synapse
    /// lives in the tray and may ignore the request, and can be quit from there.
    /// </summary>
    IReadOnlyList<string> AskToClose();
}

/// <summary>Finds Razer's programs by where they are installed, and asks them to close.</summary>
internal sealed class WindowsRazerApps : IRazerApps
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int GraceMilliseconds = 3_000;

    public IReadOnlyList<string> FindRunning()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (process, name) in EnumerateRazerProcesses())
        {
            process.Dispose();
            names.Add(name);
        }

        return [.. names];
    }

    public IReadOnlyList<string> AskToClose()
    {
        var running = EnumerateRazerProcesses().ToList();
        var deadline = DateTime.UtcNow.AddMilliseconds(GraceMilliseconds);

        foreach (var (process, _) in running)
        {
            try
            {
                process.CloseMainWindow();
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                // Already gone, or not ours to close.
            }
        }

        foreach (var (process, _) in running)
        {
            try
            {
                var wait = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
                process.WaitForExit(wait);
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return FindRunning();
    }

    private static IEnumerable<(Process Process, string Name)> EnumerateRazerProcesses()
    {
        // One buffer for every path lookup, instead of one per process.
        var buffer = new char[1024];

        foreach (var processId in ListProcessIds())
        {
            if (processId == Environment.ProcessId)
                continue; // Never this app, which may itself live somewhere with "Razer" in its path.

            var path = TryGetPath(processId, buffer);

            if (!RazerSoftwarePaths.IsInRazerFolder(path))
                continue;

            // A Process object is only built for the few that matter; Process.GetProcesses
            // would build one for every process on the machine.
            Process process;

            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                continue; // Exited since the snapshot.
            }

            yield return (process, Path.GetFileNameWithoutExtension(path)!);
        }
    }

    // The ids of every running process, in one call. The list is grown until it
    // is large enough to hold them all.
    private static int[] ListProcessIds()
    {
        var ids = new int[512];

        while (true)
        {
            if (!EnumProcesses(ids, (uint)(ids.Length * sizeof(int)), out var bytesReturned))
                return [];

            if (bytesReturned < ids.Length * sizeof(int))
                return ids[..((int)bytesReturned / sizeof(int))];

            ids = new int[ids.Length * 2];
        }
    }

    // Cheaper than Process.MainModule, and works for processes we may only query in a limited way.
    private static string? TryGetPath(int processId, char[] buffer)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, processId);

        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var size = buffer.Length;

            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? new string(buffer, 0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumProcesses([Out] int[] processIds, uint size, out uint bytesReturned);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, [Out] char[] path, ref int size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>For tests and previews: no Razer programs running, and nothing to close.</summary>
internal sealed class NoRazerApps : IRazerApps
{
    public IReadOnlyList<string> FindRunning() => [];

    public IReadOnlyList<string> AskToClose() => [];
}
