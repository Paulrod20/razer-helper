using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class DgpuScanResultTests
{
    private static DgpuApp App(string name, DgpuAppVerdict verdict) => new(1, name, 100, verdict);

    [Fact]
    public void OnlyTheLaptopPanel_MayClose() =>
        Assert.True(new DgpuScanResult(true, false, []).MayClose);

    [Fact]
    public void AnExternalDisplay_MayNotClose() =>
        Assert.False(new DgpuScanResult(true, true, []).MayClose);

    [Fact]
    public void AnUnknownDisplayState_MayNotClose() =>
        Assert.False(new DgpuScanResult(true, null, []).MayClose);

    [Fact]
    public void WithoutADedicatedGpu_MayNotClose() =>
        Assert.False(new DgpuScanResult(false, false, []).MayClose);

    [Fact]
    public void Closable_HoldsOnlyAppsWithTheCloseVerdict()
    {
        var scan = new DgpuScanResult(true, false,
        [
            App("blender", DgpuAppVerdict.Close),
            App("dwm", DgpuAppVerdict.Protected),
            App("tray", DgpuAppVerdict.NoWindow),
            App("system", DgpuAppVerdict.OtherSession),
            App("me", DgpuAppVerdict.ThisApp)
        ]);

        Assert.Equal(["blender"], scan.Closable.Select(app => app.Name));
    }

    [Fact]
    public void Closable_IsEmptyWheneverClosingIsNotAllowed()
    {
        var apps = new[] { App("blender", DgpuAppVerdict.Close) };

        Assert.Empty(new DgpuScanResult(true, true, apps).Closable);
        Assert.Empty(new DgpuScanResult(true, null, apps).Closable);
        Assert.Empty(new DgpuScanResult(false, false, apps).Closable);
    }
}
