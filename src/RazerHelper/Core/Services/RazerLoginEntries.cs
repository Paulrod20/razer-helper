using Microsoft.Win32;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>One of Razer's programs set to start when the user signs in.</summary>
internal sealed record RazerLoginEntry(string Name, string Command, bool IsEnabled);

/// <summary>
/// Razer's startup entries and the Task Manager on/off switch for each. An
/// interface so the stop and restore logic can be tested without touching the
/// real registry.
/// </summary>
internal interface ILoginEntries
{
    IReadOnlyList<RazerLoginEntry> FindRazerEntries();

    /// <summary>The entry's current on/off flag as stored text: empty when it has none (the default, on).</summary>
    string ReadApproval(string name);

    /// <summary>Switches the entry off, like the Startup tab in Task Manager. The entry itself is left as it is.</summary>
    void Disable(string name);

    /// <summary>Puts the on/off flag back to what <see cref="ReadApproval"/> returned earlier.</summary>
    void Restore(string name, string previousApproval);
}

/// <summary>
/// The current user's Run key. Razer starts through it (RazerAppEngine launches
/// Synapse and the Chroma app hidden at every sign-in). This app's own entry is
/// never touched, even though its name starts with "Razer".
/// </summary>
internal sealed class RunKeyLoginEntries(
    string ownValueName = RunKeyStartupRegistration.DefaultValueName,
    string runKeyPath = RunKeyStartupRegistration.DefaultRunKeyPath,
    string approvedKeyPath = RunKeyStartupRegistration.DefaultApprovedKeyPath) : ILoginEntries
{
    public IReadOnlyList<RazerLoginEntry> FindRazerEntries()
    {
        var entries = new List<RazerLoginEntry>();

        using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath);
        using var approvedKey = Registry.CurrentUser.OpenSubKey(approvedKeyPath);

        foreach (var name in runKey?.GetValueNames() ?? [])
        {
            if (runKey!.GetValue(name) is not string command || !IsRazerEntry(name, command))
                continue;

            var disabled = StartupApproval.IsDisabled(approvedKey?.GetValue(name) as byte[]);
            entries.Add(new RazerLoginEntry(name, command, IsEnabled: !disabled));
        }

        return entries;
    }

    public string ReadApproval(string name)
    {
        using var approvedKey = Registry.CurrentUser.OpenSubKey(approvedKeyPath);
        return StartupApproval.ToText(approvedKey?.GetValue(name) as byte[]);
    }

    public void Disable(string name)
    {
        using var approvedKey = Registry.CurrentUser.CreateSubKey(approvedKeyPath);
        approvedKey.SetValue(name, StartupApproval.CreateDisabled(DateTime.UtcNow), RegistryValueKind.Binary);
    }

    public void Restore(string name, string previousApproval)
    {
        var previous = StartupApproval.FromText(previousApproval);

        if (previous is null)
        {
            // There was no flag before, so remove ours: back to the default.
            using var existing = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }

        using var approvedKey = Registry.CurrentUser.CreateSubKey(approvedKeyPath);
        approvedKey.SetValue(name, previous, RegistryValueKind.Binary);
    }

    // Razer's own program, and not this app.
    private bool IsRazerEntry(string name, string command) =>
        !string.Equals(name, ownValueName, StringComparison.OrdinalIgnoreCase) &&
        RazerSoftwarePaths.IsInRazerFolder(command);
}

/// <summary>What stopping the login entries did: the record to keep, and anything that failed.</summary>
internal sealed record LoginEntryChange(IReadOnlyDictionary<string, string> Record, IReadOnlyList<string> Failures)
{
    public bool IsSuccess => Failures.Count == 0;
}

/// <summary>
/// Turns Razer's login entries off and back on. Like the services, what was
/// there is written down first, so "Start" puts back exactly that and a crash
/// half way can never leave an entry off with no record of what it was.
/// </summary>
internal sealed class RazerLoginEntryManager(ILoginEntries entries)
{
    public IReadOnlyList<RazerLoginEntry> Find() => entries.FindRazerEntries();

    /// <summary>
    /// Switches off every entry that is on. <paramref name="saveRecord"/> is
    /// called with the record before anything changes. An entry that already has a
    /// record keeps it (a later flag would be our own change, not the original),
    /// and an entry that is already off is left alone: it was the user's choice.
    /// </summary>
    public LoginEntryChange DisableAll(
        IReadOnlyDictionary<string, string>? existingRecord,
        Action<IReadOnlyDictionary<string, string>> saveRecord)
    {
        var record = new Dictionary<string, string>(existingRecord ?? new Dictionary<string, string>());
        var failures = new List<string>();
        var toDisable = entries.FindRazerEntries().Where(entry => entry.IsEnabled).ToList();

        foreach (var entry in toDisable)
        {
            try
            {
                record.TryAdd(entry.Name, entries.ReadApproval(entry.Name));
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                AppLog.Error($"Could not read the startup setting of '{entry.Name}'.", exception);
                failures.Add($"{entry.Name}: {exception.Message}");
            }
        }

        // Written down before anything changes.
        saveRecord(record);

        foreach (var entry in toDisable.Where(entry => record.ContainsKey(entry.Name)))
        {
            try
            {
                entries.Disable(entry.Name);
                AppLog.Info($"Turned off '{entry.Name}' at login.");
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                AppLog.Error($"Could not turn off '{entry.Name}' at login.", exception);
                failures.Add($"{entry.Name}: {exception.Message}");
            }
        }

        return new LoginEntryChange(record, failures);
    }

    /// <summary>
    /// Puts each recorded entry back the way it was. Returns the names that
    /// failed. An entry that no longer exists (Razer was uninstalled) is skipped.
    /// </summary>
    public IReadOnlyList<string> RestoreAll(IReadOnlyDictionary<string, string> record)
    {
        var failures = new List<string>();
        var present = entries.FindRazerEntries().Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, previous) in record.Where(pair => present.Contains(pair.Key)))
        {
            try
            {
                entries.Restore(name, previous);
                AppLog.Info($"Restored '{name}' at login.");
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                AppLog.Error($"Could not restore '{name}' at login.", exception);
                failures.Add($"{name}: {exception.Message}");
            }
        }

        return failures;
    }

    private static bool IsRegistryFailure(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or System.Security.SecurityException or ArgumentException;
}

/// <summary>For tests and previews: no login entries at all, and nothing can be changed.</summary>
internal sealed class NoLoginEntries : ILoginEntries
{
    public IReadOnlyList<RazerLoginEntry> FindRazerEntries() => [];

    public string ReadApproval(string name) => string.Empty;

    public void Disable(string name)
    {
    }

    public void Restore(string name, string previousApproval)
    {
    }
}
