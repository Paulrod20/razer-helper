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

    // Real helper chains are shallow (browser main, then its GPU process).
    // The limit only guarantees the walk ends, whatever the process table says.
    private const int MaximumOwnerDepth = 8;

    /// <summary>
    /// Classifies what is using the dedicated GPU, biggest video memory first.
    /// A helper process with no window of its own (Edge's GPU process, say) is
    /// counted under the app that owns it, so one app is one entry with the
    /// video memory of all its processes added up.
    /// </summary>
    /// <param name="lookup">
    /// Describes a process by id, or returns null when it is gone or cannot be
    /// inspected. Asked lazily, so only the processes that matter are looked at.
    /// </param>
    public static IReadOnlyList<DgpuApp> Classify(
        IEnumerable<GpuProcessUsage> usages,
        Func<int, RunningProcess?> lookup,
        int thisProcessId,
        int thisSessionId)
    {
        var apps = new Dictionary<int, DgpuApp>();

        foreach (var usage in usages)
        {
            if (lookup(usage.ProcessId) is not { } process)
                continue;

            var (owner, verdict) = ResolveOwner(process, lookup, thisProcessId, thisSessionId);

            apps[owner.ProcessId] = apps.TryGetValue(owner.ProcessId, out var existing)
                ? existing with { DedicatedBytes = existing.DedicatedBytes + usage.DedicatedBytes }
                : new DgpuApp(owner.ProcessId, owner.Name, usage.DedicatedBytes, verdict);
        }

        return apps.Values.OrderByDescending(app => app.DedicatedBytes).ToList();
    }

    // A process that is not closable itself may be a helper of an app that is:
    // walk up through parents while they are the same program (a browser's
    // GPU process and its main process share a name). A different program in
    // between ends the search, so a script's terminal or launcher is never
    // taken for its owner.
    private static (RunningProcess Owner, DgpuAppVerdict Verdict) ResolveOwner(
        RunningProcess process,
        Func<int, RunningProcess?> lookup,
        int thisProcessId,
        int thisSessionId)
    {
        var verdict = Decide(process, thisProcessId, thisSessionId);

        if (verdict != DgpuAppVerdict.NoWindow)
            return (process, verdict);

        var child = process;

        for (var depth = 0; depth < MaximumOwnerDepth; depth++)
        {
            if (lookup(child.ParentProcessId) is not { } parent ||
                !string.Equals(parent.Name, process.Name, StringComparison.OrdinalIgnoreCase) ||
                !StartedBefore(parent, child))
            {
                break;
            }

            var parentVerdict = Decide(parent, thisProcessId, thisSessionId);

            if (parentVerdict == DgpuAppVerdict.Close)
                return (parent, DgpuAppVerdict.Close);

            if (parentVerdict != DgpuAppVerdict.NoWindow)
                break;

            child = parent;
        }

        return (process, DgpuAppVerdict.NoWindow);
    }

    // Process ids get reused, so a "parent" id may now be an unrelated program.
    // A real parent started first; without both start times there is no way to
    // know, so it is not trusted.
    private static bool StartedBefore(RunningProcess parent, RunningProcess child) =>
        parent.StartTime is { } parentStart && child.StartTime is { } childStart && parentStart <= childStart;

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
