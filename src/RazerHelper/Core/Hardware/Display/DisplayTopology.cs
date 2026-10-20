using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Core.Hardware;

/// <summary>
/// Tells whether an external display is connected, and which GDI device name
/// is the laptop's own panel, by asking Windows how each active display is
/// wired. On this class of laptop the ports are wired to the dedicated GPU,
/// so an external display keeps it awake no matter which apps are closed;
/// separately, refresh-rate changes must land on the panel, not a monitor.
/// </summary>
internal static class DisplayTopology
{
    private const uint OnlyActivePaths = 0x00000002;
    private const int Success = 0;
    private const int InsufficientBuffer = 122;
    private const int GetSourceName = 1;

    // Sizes of DISPLAYCONFIG_PATH_INFO and DISPLAYCONFIG_MODE_INFO. A path's
    // first 20 bytes are its source info (adapter id low/high, then id), and
    // outputTechnology sits after that, then the target's adapter id (8) and
    // id (4) and mode index (4).
    private const int PathInfoSize = 72;
    private const int ModeInfoSize = 64;
    private const int SourceAdapterIdLowOffset = 0;
    private const int SourceAdapterIdHighOffset = 4;
    private const int SourceIdOffset = 8;
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
            return ReadPaths()?.Any(path => !IsInternal(path.OutputTechnology));
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            AppLog.Error("Could not query the display configuration.", exception);
            return null;
        }
    }

    /// <summary>
    /// The GDI device name (such as "\\.\DISPLAY1") of the laptop's own
    /// panel, for EnumDisplaySettings/ChangeDisplaySettingsEx, which default
    /// to the primary display when given none. Null if Windows would not say
    /// or no active path is wired as internal (an external-only setup, or an
    /// unrecognized wiring counted as external by <see cref="IsInternal"/>).
    /// </summary>
    public static string? GetInternalDisplayDeviceName()
    {
        try
        {
            var paths = ReadPaths();
            var internalPath = paths?.FirstOrDefault(path => IsInternal(path.OutputTechnology));

            return internalPath is { } path ? ReadSourceDeviceName(path) : null;
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

    private readonly record struct PathSource(int AdapterIdLow, int AdapterIdHigh, uint Id, uint OutputTechnology);

    private static List<PathSource>? ReadPaths()
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

            var sources = new List<PathSource>();

            for (var index = 0; index < pathCount; index++)
            {
                var offset = index * PathInfoSize;

                sources.Add(new PathSource(
                    AdapterIdLow: BitConverter.ToInt32(paths, offset + SourceAdapterIdLowOffset),
                    AdapterIdHigh: BitConverter.ToInt32(paths, offset + SourceAdapterIdHighOffset),
                    Id: BitConverter.ToUInt32(paths, offset + SourceIdOffset),
                    OutputTechnology: BitConverter.ToUInt32(paths, offset + OutputTechnologyOffset)));
            }

            return sources;
        }

        return null;
    }

    private static string? ReadSourceDeviceName(PathSource source)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = GetSourceName,
                Size = Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterIdLow = source.AdapterIdLow,
                AdapterIdHigh = source.AdapterIdHigh,
                Id = source.Id
            }
        };

        return DisplayConfigGetDeviceInfo(ref request) == Success ? request.ViewGdiDeviceName : null;
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

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public int Type;
        public int Size;
        public int AdapterIdLow;
        public int AdapterIdHigh;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }
}
