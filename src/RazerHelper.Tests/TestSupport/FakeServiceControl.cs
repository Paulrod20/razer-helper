using System.ServiceProcess;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.TestSupport;

/// <summary>
/// An in-memory stand-in for the Windows service manager that behaves like
/// it: stopping marks a service stopped, changing the startup type sticks,
/// and every call is recorded in order.
/// </summary>
internal sealed class FakeServiceControl : IServiceControl
{
    private readonly List<ServiceState> _services = [];

    public List<string> Calls { get; } = [];

    /// <summary>Service names whose Stop call should throw, as a protected service would.</summary>
    public HashSet<string> FailToStop { get; } = [];

    public IReadOnlyList<ServiceState> Services => _services;

    public FakeServiceControl Add(string name, bool running, ServiceStartMode mode, string? displayName = null)
    {
        _services.Add(new ServiceState(name, displayName ?? name, running, mode));
        return this;
    }

    public ServiceState Get(string name) => _services.Single(service => service.Name == name);

    public IReadOnlyList<ServiceState> Find(Func<string, string, bool> matches) =>
        _services.Where(service => matches(service.Name, service.DisplayName)).ToList();

    public void Stop(string name)
    {
        Calls.Add($"stop {name}");

        if (FailToStop.Contains(name))
            throw new InvalidOperationException("Access is denied.");

        Update(name, service => service with { IsRunning = false });
    }

    public void Start(string name)
    {
        Calls.Add($"start {name}");
        Update(name, service => service with { IsRunning = true });
    }

    public void SetStartMode(string name, ServiceStartMode mode)
    {
        Calls.Add($"mode {name}={mode}");
        Update(name, service => service with { StartMode = mode });
    }

    private void Update(string name, Func<ServiceState, ServiceState> change)
    {
        var index = _services.FindIndex(service => service.Name == name);
        _services[index] = change(_services[index]);
    }
}
