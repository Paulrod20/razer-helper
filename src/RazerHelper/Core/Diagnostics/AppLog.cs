using System.Diagnostics;

namespace RazerHelper.Core.Diagnostics;

/// <summary>
/// Minimal file log so failures are visible in release builds, where
/// Debug output is discarded. Logging never throws.
/// </summary>
internal static class AppLog
{
    private const long MaximumLogBytes = 512 * 1024;

    private static readonly Lock SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RazerHelper");
    private static readonly string LogPath = Path.Combine(LogDirectory, "razerhelper.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        Debug.Write(line);

        lock (SyncRoot)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfTooLarge();
                File.AppendAllText(LogPath, line);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Could not write the RazerHelper log: {exception.Message}");
            }
        }
    }

    // Keep one previous log so disk use stays bounded.
    private static void RotateIfTooLarge()
    {
        var info = new FileInfo(LogPath);

        if (info.Exists && info.Length > MaximumLogBytes)
            File.Move(LogPath, $"{LogPath}.old", overwrite: true);
    }
}
