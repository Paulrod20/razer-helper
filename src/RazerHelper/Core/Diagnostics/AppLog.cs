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
    private static string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RazerHelper");

    /// <summary>The log file's full path, for telling the user where to look.</summary>
    public static string LogFilePath => Path.Combine(_logDirectory, "razerhelper.log");

    /// <summary>Writes the log to <paramref name="directory"/> instead of the user's profile, so tests never touch the real log.</summary>
    internal static void RedirectTo(string directory)
    {
        lock (SyncRoot)
            _logDirectory = directory;
    }

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
                Directory.CreateDirectory(_logDirectory);
                RotateIfTooLarge();
                File.AppendAllText(LogFilePath, line);
            }
            catch (Exception exception)
            {
                // Deliberately broad: the log is how failures get reported,
                // including from the crash handler, so it must never be the
                // thing that throws. A full disk or a locked file just means
                // this one line is lost.
                Debug.WriteLine($"Could not write the RazerHelper log: {exception.Message}");
            }
        }
    }

    // Keep one previous log so disk use stays bounded.
    private static void RotateIfTooLarge()
    {
        var info = new FileInfo(LogFilePath);

        if (info.Exists && info.Length > MaximumLogBytes)
            File.Move(LogFilePath, $"{LogFilePath}.old", overwrite: true);
    }
}
