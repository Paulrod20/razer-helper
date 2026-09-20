using System.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>
/// What is on the dedicated GPU right now.
/// <paramref name="ExternalDisplay"/> is null when Windows would not say; treat that as "connected".
/// </summary>
internal sealed record DgpuScanResult(bool GpuFound, bool? ExternalDisplay, IReadOnlyList<DgpuApp> Apps)
{
    /// <summary>
    /// Whether closing apps can help at all, and so may be done. Only when a
    /// dedicated GPU exists and the laptop's own panel is the only display: an
    /// external display is driven by that GPU, which then stays on whatever is
    /// closed. Anything uncertain counts as "no". The preview and the closer
    /// both use this one rule.
    /// </summary>
    public bool MayClose => GpuFound && ExternalDisplay == false;

    /// <summary>The apps that would be asked to close: empty unless <see cref="MayClose"/>.</summary>
    public IReadOnlyList<DgpuApp> Closable =>
        MayClose ? Apps.Where(app => app.Verdict == DgpuAppVerdict.Close).ToList() : [];
}

/// <summary>Looks at what is using the dedicated GPU. Read-only: it never closes or changes anything.</summary>
internal static class DgpuScanner
{
    public static Task<DgpuScanResult> ScanAsync(IReadOnlyCollection<string>? neverClose = null) =>
        Task.Run(() => Scan(neverClose));

    internal static DgpuScanResult Scan(IReadOnlyCollection<string>? neverClose = null)
    {
        var adapters = DiscreteGpuReader.ReadDiscreteAdapters();
        var externalDisplay = DisplayTopology.HasExternalDisplay();

        if (adapters.Count == 0)
            return new DgpuScanResult(false, externalDisplay, []);

        var usages = DiscreteGpuReader.ReadProcessUsage(adapters.Select(adapter => adapter.Luid).ToHashSet());

        using var current = Process.GetCurrentProcess();

        // Only the processes on the GPU, and the parents the selector asks
        // about, are ever inspected; each is looked at once.
        var parents = ProcessTree.ReadParentIds();
        var described = new Dictionary<int, RunningProcess?>();

        RunningProcess? Lookup(int processId)
        {
            if (!described.TryGetValue(processId, out var process))
                described[processId] = process = Describe(processId, parents);

            return process;
        }

        var apps = DgpuAppSelector.Classify(usages, Lookup, current.Id, current.SessionId, neverClose);

        return new DgpuScanResult(true, externalDisplay, apps);
    }

    // Null when the process is gone or cannot be inspected; the selector then
    // never treats it as a candidate or as an owner.
    private static RunningProcess? Describe(int processId, IReadOnlyDictionary<int, int> parents)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            return new RunningProcess(
                processId,
                process.ProcessName,
                process.SessionId,
                process.MainWindowHandle != IntPtr.Zero,
                parents.GetValueOrDefault(processId),
                ReadStartTime(process));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    // Protected processes refuse this; an unknown start time just means the
    // process cannot be used as somebody's parent.
    private static DateTime? ReadStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
