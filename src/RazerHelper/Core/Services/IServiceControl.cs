using System.ServiceProcess;

namespace RazerHelper.Core.Services;

/// <summary>A Windows service as the app sees it.</summary>
internal sealed record ServiceState(
    string Name,
    string DisplayName,
    bool IsRunning,
    ServiceStartMode StartMode);

/// <summary>
/// The few Windows service operations the app needs. An interface so the
/// stop/restore logic can be tested against a fake, without touching (or
/// needing administrator rights for) the real services.
/// </summary>
internal interface IServiceControl
{
    /// <summary>The installed services for which <paramref name="matches"/> (given name and display name) is true.</summary>
    IReadOnlyList<ServiceState> Find(Func<string, string, bool> matches);

    /// <summary>Stops a service, stopping anything that depends on it first, and waits for it to finish.</summary>
    void Stop(string name);

    /// <summary>Starts a service and waits for it to be running.</summary>
    void Start(string name);

    /// <summary>Changes when Windows starts the service (Automatic, Manual or Disabled).</summary>
    void SetStartMode(string name, ServiceStartMode mode);
}
