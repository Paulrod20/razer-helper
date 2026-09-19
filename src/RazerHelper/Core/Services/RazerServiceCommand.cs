using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Services;

/// <summary>
/// The privileged half of "stop Razer services". Changing services needs
/// administrator rights, and the app normally runs without them, so a click
/// on Stop relaunches this same exe elevated (one UAC prompt) with
/// <c>--razer-services stop</c> or <c>--razer-services restore &lt;modes&gt;</c>.
/// That process does the work, sets its exit code and quits; it never opens a window.
/// </summary>
internal static class RazerServiceCommand
{
    public const string Switch = "--razer-services";
    public const string StopVerb = "stop";
    public const string RestoreVerb = "restore";

    public const int Success = 0;
    public const int SomeFailed = 1;
    public const int BadArguments = 2;

    private static readonly JsonSerializerOptions ModeSerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The arguments that ask the elevated process to stop and disable everything.</summary>
    public static string[] StopArguments() => [Switch, StopVerb];

    /// <summary>The arguments that ask it to restore the recorded startup types.</summary>
    public static string[] RestoreArguments(IReadOnlyDictionary<string, ServiceStartMode> recordedModes) =>
        [Switch, RestoreVerb, EncodeModes(recordedModes)];

    /// <summary>
    /// Runs the command if <paramref name="args"/> is one, and returns its exit
    /// code. Returns null when the arguments are not a service command at
    /// all, so normal startup carries on. A malformed service command returns
    /// <see cref="BadArguments"/> rather than falling through to a full app start.
    /// </summary>
    public static int? TryRun(string[] args, IServiceControl control)
    {
        if (args.Length == 0 || args[0] != Switch)
            return null;

        var manager = new RazerServiceManager(control);

        try
        {
            if (args is [_, StopVerb])
                return ExitCodeFor(manager.StopAndDisableAll());

            if (args is [_, RestoreVerb, var encodedModes] && TryDecodeModes(encodedModes, out var modes))
                return ExitCodeFor(manager.RestoreAll(modes));
        }
        catch (Exception exception)
        {
            AppLog.Error("The elevated Razer service command failed.", exception);
            return SomeFailed;
        }

        AppLog.Error($"Ignoring a malformed {Switch} command: {string.Join(' ', args)}");
        return BadArguments;
    }

    /// <summary>The startup types, packed into one argument (base64 JSON) so service names with spaces need no quoting.</summary>
    internal static string EncodeModes(IReadOnlyDictionary<string, ServiceStartMode> modes) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(modes, ModeSerializerOptions));

    internal static bool TryDecodeModes(string encoded, out Dictionary<string, ServiceStartMode> modes)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            modes = JsonSerializer.Deserialize<Dictionary<string, ServiceStartMode>>(json, ModeSerializerOptions) ?? [];
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            modes = [];
            return false;
        }
    }

    private static int ExitCodeFor(ServiceOperationResult result) =>
        result.IsSuccess ? Success : SomeFailed;
}
