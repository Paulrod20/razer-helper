using System.Diagnostics;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Helpers;

/// <summary>Things the Settings window opens outside the app.</summary>
internal static class ExternalLinks
{
    /// <summary>Razer's support site: pick your laptop there to find its drivers and software.</summary>
    public const string RazerDriversUrl = "https://mysupport.razer.com/";

    public static void OpenRazerDrivers() => Open(RazerDriversUrl);

    public static void OpenLogFolder() =>
        Open(Path.GetDirectoryName(AppLog.LogFilePath)!);

    // Never throws: a missing browser or folder must not take the app down.
    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not open {target}.", exception);
        }
    }
}
