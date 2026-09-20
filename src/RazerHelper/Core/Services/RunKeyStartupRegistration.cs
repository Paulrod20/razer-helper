using Microsoft.Win32;

namespace RazerHelper.Core.Services;

/// <summary>
/// Starts the app at sign-in through the current user's Run key. That needs no
/// administrator rights and shows up in Task Manager's Startup tab, where the
/// user can also switch it off; that switch lives in the StartupApproved key,
/// so it is honored here too.
/// </summary>
internal sealed class RunKeyStartupRegistration(
    string executablePath,
    string valueName = RunKeyStartupRegistration.DefaultValueName,
    string runKeyPath = RunKeyStartupRegistration.DefaultRunKeyPath,
    string approvedKeyPath = RunKeyStartupRegistration.DefaultApprovedKeyPath) : IStartupRegistration
{
    internal const string DefaultValueName = "RazerHelper";
    internal const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string DefaultApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public bool IsEnabled
    {
        get
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath);

            if (runKey?.GetValue(valueName) is not string)
                return false;

            using var approvedKey = Registry.CurrentUser.OpenSubKey(approvedKeyPath);

            return !StartupApproval.IsDisabled(approvedKey?.GetValue(valueName) as byte[]);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(runKeyPath);

            // Quoted, so a path with spaces is not split at the first space.
            runKey.SetValue(valueName, $"\"{executablePath}\"");

            // A leftover "Disabled" from Task Manager would otherwise keep it off.
            using var approvedKey = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
            approvedKey?.DeleteValue(valueName, throwOnMissingValue: false);
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
