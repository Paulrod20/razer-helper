namespace RazerHelper.Core.Models;

/// <summary>What the app would do with a process that is using the dedicated GPU.</summary>
internal enum DgpuAppVerdict
{
    /// <summary>A normal app with a window: it can be asked to close.</summary>
    Close,

    /// <summary>Part of Windows, the display stack, a GPU driver or Razer: never touched.</summary>
    Protected,

    /// <summary>This app itself.</summary>
    ThisApp,

    /// <summary>Another user's process, or a system service (session 0).</summary>
    OtherSession,

    /// <summary>No window to ask nicely, such as a tray utility or background helper: left running.</summary>
    NoWindow
}

/// <summary>One process holding the dedicated GPU, and what would happen to it.</summary>
internal sealed record DgpuApp(int ProcessId, string Name, long DedicatedBytes, DgpuAppVerdict Verdict);

/// <summary>Video memory a process holds on the dedicated GPU, as Windows reports it.</summary>
internal sealed record GpuProcessUsage(int ProcessId, long DedicatedBytes);

/// <summary>
/// What is known about a running process, gathered by the caller so the decision stays pure.
/// The parent and start time let a helper process be traced back to the app that launched it.
/// </summary>
internal sealed record RunningProcess(
    int ProcessId,
    string Name,
    int SessionId,
    bool HasWindow,
    int ParentProcessId = 0,
    DateTime? StartTime = null);
