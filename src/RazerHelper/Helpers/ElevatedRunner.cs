using System.ComponentModel;
using System.Diagnostics;

namespace RazerHelper.Helpers;

/// <summary>How a run of the app's own elevated helper ended.</summary>
internal sealed record ElevatedResult(int ExitCode, bool UserDeclined)
{
    public static ElevatedResult Declined { get; } = new(-1, UserDeclined: true);
}

/// <summary>
/// Runs this same exe again with administrator rights, for the few things
/// that need them (stopping services). The user sees one Windows UAC prompt;
/// the rest of the app keeps running without elevation.
/// </summary>
internal static class ElevatedRunner
{
    // Windows' error for "the user cancelled the elevation prompt".
    private const int ErrorCancelled = 1223;

    public static Task<ElevatedResult> RunAsync(IReadOnlyList<string> arguments) =>
        Task.Run(() => Run(arguments));

    private static ElevatedResult Run(IReadOnlyList<string> arguments)
    {
        var (fileName, prefix) = GetEntryPoint();

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = prefix + string.Join(' ', arguments),
                UseShellExecute = true,      // required for the elevation prompt
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            }) ?? throw new InvalidOperationException("The elevated process could not be started.");

            process.WaitForExit();
            return new ElevatedResult(process.ExitCode, UserDeclined: false);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            return ElevatedResult.Declined;
        }
    }

    // Normally the app's own .exe. When started through "dotnet RazerHelper.dll"
    // the process is dotnet itself, so the dll must be passed along.
    private static (string FileName, string ArgumentPrefix) GetEntryPoint()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The app's own path is unknown.");

        if (!string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            return (processPath, string.Empty);

        // The first command-line argument is the dll that dotnet was asked to run.
        // (Assembly.Location would do here too, but it is empty in a single-file build.)
        return (processPath, $"\"{Path.GetFullPath(Environment.GetCommandLineArgs()[0])}\" ");
    }
}
