using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Hardware;

/// <summary>
/// Tells whether an external display is connected, by asking Windows how each
/// active display is wired (the laptop's own panel is "internal"). On this
/// class of laptop the ports are wired to the dedicated GPU, so an external
/// display keeps it awake no matter which apps are closed.
/// </summary>
internal static class DisplayTopology
{
    private const uint OnlyActivePaths = 0x00000002;
    private const int Success = 0;
    private const int InsufficientBuffer = 122;

    // Sizes of DISPLAYCONFIG_PATH_INFO and DISPLAYCONFIG_MODE_INFO, and where
    // outputTechnology sits inside a path: after the 20-byte source info, then
    // the target's adapter id (8), id (4) and mode index (4).
    private const int PathInfoSize = 72;
    private const int ModeInfoSize = 64;
    private const int OutputTechnologyOffset = 36;

    private const uint OutputTechnologyInternal = 0x80000000;
    private const uint OutputTechnologyDisplayPortEmbedded = 11;
    private const uint OutputTechnologyUdiEmbedded = 13;

    /// <summary>
    /// True when at least one active display is not the laptop's own panel,
    /// false when only the panel is active, and null when Windows would not
    /// say. Callers should treat null like true: do nothing risky.
    /// </summary>
    public static bool? HasExternalDisplay()
    {
        try
        {
            var technologies = ReadOutputTechnologies();
            return technologies is null ? null : technologies.Any(technology => !IsInternal(technology));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            AppLog.Error("Could not query the display configuration.", exception);
            return null;
        }
    }

    /// <summary>The wiring types that mean the built-in panel. Anything else, including "unknown", counts as external.</summary>
    internal static bool IsInternal(uint outputTechnology) =>
        outputTechnology is OutputTechnologyInternal
            or OutputTechnologyDisplayPortEmbedded
            or OutputTechnologyUdiEmbedded;

    private static List<uint>? ReadOutputTechnologies()
    {
        // The display setup can change between asking for the size and reading
        // it, so retry a few times.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(OnlyActivePaths, out var pathCount, out var modeCount) != Success)
                return null;

            var paths = new byte[pathCount * PathInfoSize];
            var modes = new byte[modeCount * ModeInfoSize];
            var result = QueryDisplayConfig(OnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);

            if (result == InsufficientBuffer)
                continue;

            if (result != Success)
                return null;

            var technologies = new List<uint>();

            for (var index = 0; index < pathCount; index++)
                technologies.Add(BitConverter.ToUInt32(paths, (index * PathInfoSize) + OutputTechnologyOffset));

            return technologies;
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] byte[] paths,
        ref uint modeCount,
        [Out] byte[] modes,
        IntPtr currentTopologyId);
}
