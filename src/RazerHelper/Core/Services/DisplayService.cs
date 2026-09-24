using System.Runtime.InteropServices;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class DisplayService
{
    private const int EnumCurrentSettings = -1;
    private const int ChangeSuccessful = 0;
    private const int CdsTest = 0x00000002;
    private const int DmDisplayFrequency = 0x00400000;

    private static DeviceMode CreateDeviceMode() => new() { Size = (short)Marshal.SizeOf<DeviceMode>() };

    // Null (Windows' "the primary display") when the laptop's own panel
    // cannot be identified, which is the same as every call here behaved
    // before this was detected. Re-read each call: which display is internal
    // cannot change while running, but resolving it fresh costs one more
    // Win32 call and keeps this from being the one place a stale value lingers.
    private static string? InternalDeviceName() => DisplayTopology.GetInternalDisplayDeviceName();

    public DisplayInfo? GetInternalDisplayInfo()
    {
        var deviceMode = CreateDeviceMode();

        var foundDisplay = EnumDisplaySettings(
            deviceName: InternalDeviceName(),
            modeNumber: EnumCurrentSettings,
            deviceMode: ref deviceMode);

        if (!foundDisplay)
            return null;

        if (deviceMode.Width <= 0 || deviceMode.Height <= 0)
            return null;

        return new DisplayInfo(
            Width: deviceMode.Width,
            Height: deviceMode.Height,
            RefreshRateHz: deviceMode.RefreshRateHz
        );
    }

    /// <summary>The distinct refresh rates Windows reports for the internal display's current resolution, ascending.</summary>
    public IReadOnlyList<int> GetAvailableRefreshRates()
    {
        var rates = new SortedSet<int>();

        foreach (var mode in EnumerateModesAtCurrentResolution(InternalDeviceName()))
        {
            // 0 and 1 are Windows' "use the hardware default" sentinel, not a real rate.
            if (mode.RefreshRateHz > 1)
                rates.Add(mode.RefreshRateHz);
        }

        return rates.ToList();
    }

    public bool TrySetInternalRefreshRate(int refreshRateHz, out string message)
    {
        var deviceName = InternalDeviceName();

        foreach (var mode in EnumerateModesAtCurrentResolution(deviceName))
        {
            if (mode.RefreshRateHz != refreshRateHz)
                continue;

            var candidateMode = mode;
            candidateMode.Fields = DmDisplayFrequency;

            var testResult = ChangeDisplaySettingsEx(
                deviceName: deviceName,
                deviceMode: ref candidateMode,
                hwnd: IntPtr.Zero,
                flags: CdsTest,
                lParam: IntPtr.Zero);

            if (testResult != ChangeSuccessful)
            {
                message = $"{refreshRateHz} Hz is not available for the current display mode.";
                return false;
            }

            var applyResult = ChangeDisplaySettingsEx(
                deviceName: deviceName,
                deviceMode: ref candidateMode,
                hwnd: IntPtr.Zero,
                flags: 0,
                lParam: IntPtr.Zero);

            if (applyResult != ChangeSuccessful)
            {
                message = $"Windows could not apply {refreshRateHz} Hz. Error: {applyResult}.";
                return false;
            }

            message = $"Switched to {refreshRateHz} Hz.";
            return true;
        }

        var current = GetInternalDisplayInfo();
        message = current is null
            ? $"{refreshRateHz} Hz is not available."
            : $"{refreshRateHz} Hz is not available at {current.Width}x{current.Height}.";
        return false;
    }

    /// <summary>Every mode Windows reports for <paramref name="deviceName"/> whose resolution and color depth match its current settings.</summary>
    private static IEnumerable<DeviceMode> EnumerateModesAtCurrentResolution(string? deviceName)
    {
        var currentMode = CreateDeviceMode();

        if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref currentMode))
            yield break;

        for (var modeNumber = 0; ; modeNumber++)
        {
            var candidateMode = CreateDeviceMode();

            if (!EnumDisplaySettings(deviceName, modeNumber, ref candidateMode))
                yield break;

            if (candidateMode.Width == currentMode.Width &&
                candidateMode.Height == currentMode.Height &&
                candidateMode.BitsPerPixel == currentMode.BitsPerPixel)
            {
                yield return candidateMode;
            }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNumber,
        ref DeviceMode deviceMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChangeDisplaySettingsExW")]
    private static extern int ChangeDisplaySettingsEx(
        string? deviceName,
        ref DeviceMode deviceMode,
        IntPtr hwnd,
        int flags,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DeviceMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public short SpecVersion;
        public short DriverVersion;
        public short Size;
        public short DriverExtra;
        public int Fields;

        public int PositionX;
        public int PositionY;
        public int DisplayOrientation;
        public int DisplayFixedOutput;

        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;

        public short LogPixels;
        public int BitsPerPixel;
        public int Width;
        public int Height;
        public int DisplayFlags;
        public int RefreshRateHz;
        public int IcmMethod;
        public int IcmIntent;
        public int MediaType;
        public int DitherType;
        public int Reserved1;
        public int Reserved2;
        public int PanningWidth;
        public int PanningHeight;
    }
}