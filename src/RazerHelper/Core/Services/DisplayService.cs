using System.Runtime.InteropServices;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class DisplayService
{
    private const int EnumCurrentSettings = -1;
    private const int ChangeSuccessful = 0;
    private const int CdsTest = 0x00000002;
    private const int DmDisplayFrequency = 0x00400000;

    private static DeviceMode CreateDeviceMode() => new() { Size = (short)Marshal.SizeOf<DeviceMode>() };

    public DisplayInfo? GetPrimaryDisplayInfo()
    {
        var deviceMode = CreateDeviceMode();

        var foundDisplay = EnumDisplaySettings(
            deviceName: null,
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

    /// <summary>The distinct refresh rates Windows reports for the display's current resolution, ascending.</summary>
    public IReadOnlyList<int> GetAvailableRefreshRates()
    {
        var rates = new SortedSet<int>();

        foreach (var mode in EnumerateModesAtCurrentResolution())
        {
            // 0 and 1 are Windows' "use the hardware default" sentinel, not a real rate.
            if (mode.RefreshRateHz > 1)
                rates.Add(mode.RefreshRateHz);
        }

        return rates.ToList();
    }

    public bool TrySetPrimaryRefreshRate(int refreshRateHz, out string message)
    {
        foreach (var mode in EnumerateModesAtCurrentResolution())
        {
            if (mode.RefreshRateHz != refreshRateHz)
                continue;

            var candidateMode = mode;
            candidateMode.Fields = DmDisplayFrequency;

            var testResult = ChangeDisplaySettingsEx(
                deviceName: null,
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
                deviceName: null,
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

        var current = GetPrimaryDisplayInfo();
        message = current is null
            ? $"{refreshRateHz} Hz is not available."
            : $"{refreshRateHz} Hz is not available at {current.Width}x{current.Height}.";
        return false;
    }

    /// <summary>Every mode Windows reports whose resolution and color depth match the display's current settings.</summary>
    private static IEnumerable<DeviceMode> EnumerateModesAtCurrentResolution()
    {
        var currentMode = CreateDeviceMode();

        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref currentMode))
            yield break;

        for (var modeNumber = 0; ; modeNumber++)
        {
            var candidateMode = CreateDeviceMode();

            if (!EnumDisplaySettings(null, modeNumber, ref candidateMode))
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