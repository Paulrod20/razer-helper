using System.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>
/// What is on the dedicated GPU right now.
/// <paramref name="ExternalDisplay"/> is null when Windows would not say; treat that as "connected".
/// </summary>
internal sealed record DgpuScanResult(bool GpuFound, bool? ExternalDisplay, IReadOnlyList<DgpuApp> Apps);

/// <summary>Looks at what is using the dedicated GPU. Read-only: it never closes or changes anything.</summary>
internal static class DgpuScanner
{
    public static Task<DgpuScanResult> ScanAsync() => Task.Run(Scan);

    internal static DgpuScanResult Scan()
    {
        var adapters = DiscreteGpuReader.ReadDiscreteAdapters();
        var externalDisplay = DisplayTopology.HasExternalDisplay();

        if (adapters.Count == 0)
            return new DgpuScanResult(false, externalDisplay, []);

        var usages = DiscreteGpuReader.ReadProcessUsage(adapters.Select(adapter => adapter.Luid).ToHashSet());

        using var current = Process.GetCurrentProcess();
        var apps = DgpuAppSelector.Classify(usages, DescribeRunningProcesses(), current.Id, current.SessionId);

        return new DgpuScanResult(true, externalDisplay, apps);
    }

    private static Dictionary<int, RunningProcess> DescribeRunningProcesses()
    {
        var running = new Dictionary<int, RunningProcess>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    running[process.Id] = new RunningProcess(
                        process.Id,
                        process.ProcessName,
                        process.SessionId,
                        process.MainWindowHandle != IntPtr.Zero);
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    // Exited already, or not ours to inspect. Not describing it
                    // means it is not in the map, so it is never a candidate.
                }
            }
        }

        return running;
    }
}
