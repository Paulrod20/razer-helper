using System.Runtime.InteropServices;
using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.Hardware;

public class HidDeviceLocatorTests
{
    private const string LaptopPath = @"\\?\hid#vid_1532&pid_029f&mi_02#7&2a5f1c3e&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string MousePath = @"\\?\HID#VID_1532&PID_0084&MI_00#8&1B2C3D4E&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string OtherVendorPath = @"\\?\hid#vid_046d&pid_c52b&mi_00#7&1&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    [Fact]
    public void MatchesTheVendorAndProductInThePath()
    {
        Assert.True(HidDeviceLocator.Matches(LaptopPath, 0x1532, 0x029F));
    }

    [Fact]
    public void MatchingIgnoresLetterCase()
    {
        Assert.True(HidDeviceLocator.Matches(MousePath, 0x1532, 0x0084));
    }

    [Fact]
    public void AnyProductMatchesWhenNoneIsGiven()
    {
        Assert.True(HidDeviceLocator.Matches(LaptopPath, 0x1532, null));
        Assert.True(HidDeviceLocator.Matches(MousePath, 0x1532, null));
    }

    [Fact]
    public void DoesNotMatchAnotherProductOrVendor()
    {
        Assert.False(HidDeviceLocator.Matches(MousePath, 0x1532, 0x029F));
        Assert.False(HidDeviceLocator.Matches(OtherVendorPath, 0x1532, null));
    }

    [Fact]
    public void ReadsAHexFieldAndReportsWhenItIsMissing()
    {
        Assert.Equal(0x029F, HidDeviceLocator.ParseHexField(LaptopPath, "pid_"));
        Assert.Null(HidDeviceLocator.ParseHexField(@"\\?\hid#nothing-here", "vid_"));
    }

    [Fact]
    public void ATruncatedOrNonHexFieldIsNotAMatch()
    {
        Assert.Null(HidDeviceLocator.ParseHexField(@"\\?\hid#vid_15", "vid_"));
        Assert.Null(HidDeviceLocator.ParseHexField(@"\\?\hid#vid_zzzz", "vid_"));
    }

    // Windows reads these structures by byte layout, so a wrong size would
    // silently corrupt every call that uses them.
    [Fact]
    public void NativeStructuresMatchWindowsLayout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 32 : 28, Marshal.SizeOf<HidDeviceLocator.DeviceInterfaceData>());
        Assert.Equal(64, Marshal.SizeOf<HidDeviceLocator.HidpCaps>());
        Assert.Equal(8, (int)Marshal.OffsetOf<HidDeviceLocator.HidpCaps>(nameof(HidDeviceLocator.HidpCaps.FeatureReportByteLength)));
    }
}
