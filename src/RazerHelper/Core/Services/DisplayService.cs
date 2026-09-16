using System.Runtime.InteropServices;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

public sealed class DisplayService
{
    private const int EnumCurrentSettings = -1;

    public DisplayInfo? GetPrimaryDisplayInfo()
    {
        var deviceMode = new DeviceMode
        {
            Size = (short)Marshal.SizeOf<DeviceMode>()
        };

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNumber,
        ref DeviceMode deviceMode);

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