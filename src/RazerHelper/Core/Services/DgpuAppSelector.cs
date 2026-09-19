using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>
/// Decides which processes on the dedicated GPU may be closed. It is
/// deliberately cautious: only an ordinary app with a window, in the user's
/// own session, that is not part of Windows or a driver, is ever a candidate.
/// Anything unknown is left alone.
/// </summary>
internal static class DgpuAppSelector
{
    // Windows shell and display stack, and the GPU drivers' helpers. Closing
    // any of these takes the desktop, the taskbar or the display with it.
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm", "csrss", "winlogon", "services", "lsass", "smss", "wininit", "system", "registry",
        "svchost", "fontdrvhost", "explorer", "taskhostw", "sihost", "runtimebroker",
        "shellexperiencehost", "searchhost", "searchapp", "startmenuexperiencehost", "textinputhost",
        "applicationframehost", "systemsettings", "dllhost", "conhost", "audiodg", "ctfmon",
        "lockapp", "widgets", "widgetservice", "securityhealthsystray", "wudfhost",
        "igfxem", "igfxhk", "igfxext", "atieclxx", "atiesrxx",
        "nvcontainer", "nvdisplay.container", "nvsettings", "nvspcaps64", "nvsphelper64", "nvwmi64", "nvcplui"
    };

    // Whole families, so a renamed or versioned helper is still covered.
    private static readonly string[] ProtectedPrefixes = ["razer", "nvidia", "amd", "radeon"];

    /// <summary>
    /// Classifies every process using the dedicated GPU, biggest video memory
    /// first. A process Windows reports but that is no longer running is left out.
    /// </summary>
    public static IReadOnlyList<DgpuApp> Classify(
        IEnumerable<GpuProcessUsage> usages,
        IReadOnlyDictionary<int, RunningProcess> running,
        int thisProcessId,
        int thisSessionId)
    {
        var apps = new List<DgpuApp>();

        foreach (var usage in usages)
        {
            if (!running.TryGetValue(usage.ProcessId, out var process))
                continue;

            apps.Add(new DgpuApp(
                process.ProcessId,
                process.Name,
                usage.DedicatedBytes,
                Decide(process, thisProcessId, thisSessionId)));
        }

        return apps.OrderByDescending(app => app.DedicatedBytes).ToList();
    }

    internal static DgpuAppVerdict Decide(RunningProcess process, int thisProcessId, int thisSessionId)
    {
        // The order matters: the strongest reasons to leave something alone come first.
        if (process.ProcessId == thisProcessId)
            return DgpuAppVerdict.ThisApp;

        if (process.SessionId == 0 || process.SessionId != thisSessionId)
            return DgpuAppVerdict.OtherSession;

        if (IsProtectedName(process.Name))
            return DgpuAppVerdict.Protected;

        return process.HasWindow ? DgpuAppVerdict.Close : DgpuAppVerdict.NoWindow;
    }

    internal static bool IsProtectedName(string name)
    {
        // Names arrive without ".exe", but be forgiving if one does.
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        return ProtectedNames.Contains(name) ||
            ProtectedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
