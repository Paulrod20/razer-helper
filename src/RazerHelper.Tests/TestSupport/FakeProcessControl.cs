using RazerHelper.Core.Services;

namespace RazerHelper.Tests.TestSupport;

/// <summary>Stands in for the real processes: nothing here can close anything on the machine running the tests.</summary>
internal sealed class FakeProcessControl : IProcessControl
{
    private readonly Dictionary<int, ProcessIdentity> _running = [];

    /// <summary>Process ids that were asked to close.</summary>
    public List<int> CloseRequests { get; } = [];

    /// <summary>Ids whose close request reports failure, like a process with no window.</summary>
    public HashSet<int> WithoutWindow { get; } = [];

    public FakeProcessControl Add(int processId, string name, DateTime? startTime)
    {
        _running[processId] = new ProcessIdentity(name, startTime);
        return this;
    }

    public ProcessIdentity? Identify(int processId) => _running.GetValueOrDefault(processId);

    public bool RequestClose(int processId)
    {
        CloseRequests.Add(processId);
        return !WithoutWindow.Contains(processId);
    }
}
