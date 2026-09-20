namespace RazerHelper.Core.Services;

/// <summary>What counts as Razer's software, told apart by where it lives on disk.</summary>
internal static class RazerSoftwarePaths
{
    /// <summary>
    /// True when a program or command line is inside a folder named "Razer",
    /// which is where Razer's installer puts Synapse and its helpers (under
    /// Program Files and under each user's AppData\Local). A folder that only
    /// contains the word, such as this app's own "razer-helper", does not match.
    /// </summary>
    public static bool IsInRazerFolder(string? pathOrCommand) =>
        pathOrCommand is not null &&
        pathOrCommand.Replace('/', '\\').Contains("\\Razer\\", StringComparison.OrdinalIgnoreCase);
}
