using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RazerHelper.Core.Hardware;

/// <summary>
/// Finds HID interfaces by vendor and product id straight from Windows' device
/// list. It reads the interface paths (which contain the ids) and opens only the
/// devices it is asked about, instead of opening and describing every HID device
/// on the machine. That is what the general-purpose HID library did, at a cost
/// of about 100 ms on the UI thread, a burst of 20 threads and several megabytes
/// that never came back. Everything here only reads.
/// </summary>
internal static class HidDeviceLocator
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int HidpStatusSuccess = 0x00110000;
    private const int ProductNameCharacters = 128;

    private static readonly IntPtr InvalidHandle = new(-1);

    /// <summary>The paths of the HID interfaces with this vendor id (and product id, if given).</summary>
    public static IReadOnlyList<string> FindPaths(int vendorId, int? productId = null)
    {
        var matches = new List<string>();

        HidD_GetHidGuid(out var hidGuid);
        var deviceSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);

        if (deviceSet == InvalidHandle)
            return matches;

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInterfaceData { Size = Marshal.SizeOf<DeviceInterfaceData>() };

                // False once the list runs out.
                if (!SetupDiEnumDeviceInterfaces(deviceSet, IntPtr.Zero, ref hidGuid, index, ref data))
                    break;

                var path = ReadPath(deviceSet, ref data);

                if (path is not null && Matches(path, vendorId, productId))
                    matches.Add(path);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceSet);
        }

        return matches;
    }

    /// <summary>
    /// Opens the interface with no access rights of its own. That is still
    /// enough to exchange feature reports, and Windows allows it on system HID
    /// interfaces where an ordinary read or write handle is refused (the same
    /// fallback hidapi uses). Returns null when it cannot be opened.
    /// </summary>
    public static SafeFileHandle? OpenForFeatureReports(string path)
    {
        var handle = CreateFile(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (!handle.IsInvalid)
            return handle;

        handle.Dispose();
        return null;
    }

    /// <summary>The size of a feature report on this interface, or null when Windows will not say.</summary>
    public static int? GetFeatureReportLength(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsed))
            return null;

        try
        {
            return HidP_GetCaps(preparsed, out var caps) == HidpStatusSuccess
                ? caps.FeatureReportByteLength
                : null;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    /// <summary>The product name the device reports, or null when it cannot be read.</summary>
    public static string? GetProductName(string path)
    {
        using var handle = OpenForFeatureReports(path);

        if (handle is null)
            return null;

        var buffer = new char[ProductNameCharacters];

        if (!HidD_GetProductString(handle, buffer, (uint)(buffer.Length * sizeof(char))))
            return null;

        var length = Array.IndexOf(buffer, '\0');
        return new string(buffer, 0, length < 0 ? buffer.Length : length);
    }

    /// <summary>
    /// Whether an interface path belongs to this vendor (and product). The ids
    /// are in the path itself, for example
    /// <c>\\?\hid#vid_1532&amp;pid_029f&amp;mi_02#7&amp;...</c>.
    /// </summary>
    internal static bool Matches(string path, int vendorId, int? productId) =>
        ParseHexField(path, "vid_") == vendorId &&
        (productId is null || ParseHexField(path, "pid_") == productId);

    /// <summary>The four-digit hex number after a marker such as "vid_", ignoring case, or null when absent.</summary>
    internal static int? ParseHexField(string path, string marker)
    {
        var start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (start < 0 || start + marker.Length + 4 > path.Length)
            return null;

        return int.TryParse(
            path.AsSpan(start + marker.Length, 4),
            System.Globalization.NumberStyles.AllowHexSpecifier,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    // The interface's path is variable length, so it is asked for in two steps:
    // first how much room it needs, then the path itself.
    private static string? ReadPath(IntPtr deviceSet, ref DeviceInterfaceData data)
    {
        SetupDiGetDeviceInterfaceDetail(deviceSet, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);

        if (required == 0)
            return null;

        var buffer = Marshal.AllocHGlobal((int)required);

        try
        {
            // The header is a 4-byte size, which Windows wants as 8 on 64-bit.
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);

            return SetupDiGetDeviceInterfaceDetail(deviceSet, ref data, buffer, required, out _, IntPtr.Zero)
                ? Marshal.PtrToStringUni(buffer + sizeof(int))
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // These mirror Windows' structures byte for byte; the tests pin their sizes.
    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;

        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    // The name comes back as UTF-16, so this must be Unicode: the default would
    // marshal the buffer one byte per character and yield only the first letter.
    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, [Out] char[] buffer, uint bufferLength);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parentWindow, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref DeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref DeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
