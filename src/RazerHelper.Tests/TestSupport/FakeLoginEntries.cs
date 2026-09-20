using RazerHelper.Core.Services;

namespace RazerHelper.Tests.TestSupport;

/// <summary>Razer login entries held in memory, with a log of what was changed and in which order.</summary>
internal sealed class FakeLoginEntries : ILoginEntries
{
    private readonly Dictionary<string, (string Command, string Approval)> _entries = [];

    /// <summary>Everything done to the entries, in order, e.g. "disable:RazerAppEngine".</summary>
    public List<string> Actions { get; } = [];

    /// <summary>Names whose change throws, like a locked registry key.</summary>
    public HashSet<string> Failing { get; } = [];

    public FakeLoginEntries Add(string name, string approval = "", string command = @"C:\Program Files\Razer\App.exe")
    {
        _entries[name] = (command, approval);
        return this;
    }

    public string ApprovalOf(string name) => _entries[name].Approval;

    public IReadOnlyList<RazerLoginEntry> FindRazerEntries() =>
        _entries.Select(pair => new RazerLoginEntry(
            pair.Key,
            pair.Value.Command,
            IsEnabled: !StartupApproval.IsDisabled(StartupApproval.FromText(pair.Value.Approval)))).ToList();

    public string ReadApproval(string name)
    {
        if (Failing.Contains(name))
            throw new UnauthorizedAccessException("registry is locked");

        return _entries[name].Approval;
    }

    public void Disable(string name)
    {
        if (Failing.Contains(name))
            throw new UnauthorizedAccessException("registry is locked");

        Actions.Add($"disable:{name}");
        _entries[name] = (_entries[name].Command, StartupApproval.ToText(StartupApproval.CreateDisabled(DateTime.UtcNow)));
    }

    public void Restore(string name, string previousApproval)
    {
        if (Failing.Contains(name))
            throw new UnauthorizedAccessException("registry is locked");

        Actions.Add($"restore:{name}");
        _entries[name] = (_entries[name].Command, previousApproval);
    }
}
