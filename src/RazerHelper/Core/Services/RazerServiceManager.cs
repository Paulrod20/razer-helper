using System.ServiceProcess;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>What is installed and running right now.</summary>
internal sealed record RazerServicesStatus(IReadOnlyList<ServiceState> Services)
{
    public int Total => Services.Count;

    public int Running => Services.Count(service => service.IsRunning);
}

/// <summary>The outcome of stopping or restoring the services: what worked, and what did not and why.</summary>
internal sealed record ServiceOperationResult(
    IReadOnlyList<string> Succeeded,
    IReadOnlyList<string> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

/// <summary>
/// Stops Razer's background services and keeps them off, and puts everything
/// back the way it was. Stopping also disables each service so it stays off
/// after a restart, which is how G-Helper handles ASUS's services. Unlike
/// G-Helper, the original startup type of every service is recorded first, so
/// "start" restores exactly what was there instead of forcing Automatic.
/// </summary>
internal sealed class RazerServiceManager(IServiceControl control)
{
    /// <summary>Every Razer service has "Razer" in its display name or its service name.</summary>
    public static bool IsRazerService(string name, string displayName) =>
        displayName.Contains("Razer", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Razer", StringComparison.OrdinalIgnoreCase);

    public RazerServicesStatus GetStatus() => new(control.Find(IsRazerService));

    /// <summary>
    /// The startup types to restore later: what is recorded now, plus any
    /// service currently set to something other than Disabled. A service that
    /// is Disabled with no record is left out, because nothing says it was
    /// ever on; restoring it would enable something the user turned off.
    /// </summary>
    public static IReadOnlyDictionary<string, ServiceStartMode> RecordStartModes(
        RazerServicesStatus status,
        IReadOnlyDictionary<string, ServiceStartMode>? alreadyRecorded)
    {
        var recorded = new Dictionary<string, ServiceStartMode>(alreadyRecorded ?? new Dictionary<string, ServiceStartMode>());

        foreach (var service in status.Services)
        {
            // Never overwrite an earlier record with Disabled: that would be
            // our own change from a previous stop, not the original setting.
            if (service.StartMode != ServiceStartMode.Disabled)
                recorded[service.Name] = service.StartMode;
        }

        return recorded;
    }

    /// <summary>Stops every Razer service and disables it. One failing service never prevents the rest.</summary>
    public ServiceOperationResult StopAndDisableAll()
    {
        var succeeded = new List<string>();
        var failures = new List<string>();

        foreach (var service in GetStatus().Services)
        {
            try
            {
                if (service.IsRunning)
                    control.Stop(service.Name);

                control.SetStartMode(service.Name, ServiceStartMode.Disabled);

                AppLog.Info($"Stopped and disabled '{service.DisplayName}'.");
                succeeded.Add(service.DisplayName);
            }
            catch (Exception exception)
            {
                AppLog.Error($"Could not stop and disable '{service.DisplayName}'.", exception);
                failures.Add($"{service.DisplayName}: {exception.Message}");
            }
        }

        return new ServiceOperationResult(succeeded, failures);
    }

    /// <summary>
    /// Restores the recorded startup types and starts the services that
    /// start automatically. Services with no record are not touched, except
    /// that one which is stopped but not disabled is started.
    /// </summary>
    public ServiceOperationResult RestoreAll(IReadOnlyDictionary<string, ServiceStartMode> recordedModes)
    {
        var succeeded = new List<string>();
        var failures = new List<string>();

        foreach (var service in GetStatus().Services)
        {
            try
            {
                var hasRecord = recordedModes.TryGetValue(service.Name, out var recordedMode);
                var mode = hasRecord ? recordedMode : service.StartMode;

                if (mode == ServiceStartMode.Disabled)
                    continue; // The user's own choice; leave it.

                if (hasRecord)
                    control.SetStartMode(service.Name, mode);

                // Only services that run on their own are started. Manual
                // ones are started by whatever needs them, as before.
                if (mode == ServiceStartMode.Automatic && !service.IsRunning)
                    control.Start(service.Name);

                AppLog.Info($"Restored '{service.DisplayName}' to {mode}.");
                succeeded.Add(service.DisplayName);
            }
            catch (Exception exception)
            {
                AppLog.Error($"Could not restore '{service.DisplayName}'.", exception);
                failures.Add($"{service.DisplayName}: {exception.Message}");
            }
        }

        return new ServiceOperationResult(succeeded, failures);
    }
}
