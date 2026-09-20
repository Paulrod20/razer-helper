using System.ServiceProcess;

namespace RazerHelper.Core.Services;

/// <summary>
/// Everything of Razer's that can be running or set to start: its services, its
/// own programs (Synapse and helpers), and its entry that starts them at login.
/// </summary>
internal sealed record RazerSoftwareStatus(
    RazerServicesStatus Services,
    IReadOnlyList<string> RunningApps,
    IReadOnlyList<RazerLoginEntry> LoginEntries)
{
    /// <summary>Whether any of Razer's software is on this PC, so the row is worth showing.</summary>
    public bool IsInstalled => Services.Total > 0 || RunningApps.Count > 0 || LoginEntries.Count > 0;

    /// <summary>Running services plus running Razer programs. The number shown to the user.</summary>
    public int Running => Services.Running + RunningApps.Count;

    public bool LoginEnabled => LoginEntries.Any(entry => entry.IsEnabled);

    /// <summary>True while there is anything left for "Stop" to do. Otherwise the button offers "Start".</summary>
    public bool NeedsStop => Running > 0 || LoginEnabled || ServicesToStop.Count > 0;

    /// <summary>The services that are still running or could still start: not already stopped and disabled.</summary>
    public IReadOnlyList<ServiceState> ServicesToStop =>
        Services.Services.Where(service => service.IsRunning || service.StartMode != ServiceStartMode.Disabled).ToList();
}

/// <summary>
/// Brings Razer's services, programs and login entry together. The services need
/// administrator rights and are handled by the elevated helper; the rest is
/// done here, as the user.
/// </summary>
internal sealed class RazerSoftwareManager(
    RazerServiceManager services,
    RazerLoginEntryManager loginEntries,
    IRazerApps apps)
{
    public RazerSoftwareStatus GetStatus() => new(services.GetStatus(), apps.FindRunning(), loginEntries.Find());

    public LoginEntryChange DisableLoginEntries(
        IReadOnlyDictionary<string, string>? existingRecord,
        Action<IReadOnlyDictionary<string, string>> saveRecord) =>
        loginEntries.DisableAll(existingRecord, saveRecord);

    public IReadOnlyList<string> RestoreLoginEntries(IReadOnlyDictionary<string, string> record) =>
        loginEntries.RestoreAll(record);

    /// <summary>Asks Razer's running programs to close (never force), and returns the ones still open.</summary>
    public IReadOnlyList<string> AskAppsToClose() => apps.AskToClose();
}
