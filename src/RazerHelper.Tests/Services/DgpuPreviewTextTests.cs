using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class DgpuPreviewTextTests
{
    private const long Mb = 1024 * 1024;

    private static DgpuApp App(string name, long megabytes, DgpuAppVerdict verdict, int pid = 1) =>
        new(pid, name, megabytes * Mb, verdict);

    [Fact]
    public void NoDedicatedGpu_SaysThereIsNothingToClose() =>
        Assert.Contains("No dedicated GPU", DgpuPreviewText.Build(new DgpuScanResult(false, false, [])));

    [Fact]
    public void ExternalDisplay_SaysNothingWouldBeClosed()
    {
        var text = DgpuPreviewText.Build(new DgpuScanResult(true, true, [App("blender", 500, DgpuAppVerdict.Close)]));

        Assert.Contains("external display is connected", text);
        Assert.Contains("nothing would be closed", text);
    }

    [Fact]
    public void UnknownDisplayState_IsTreatedLikeExternal() =>
        Assert.Contains("nothing would be closed", DgpuPreviewText.Build(new DgpuScanResult(true, null, [])));

    [Fact]
    public void ListsClosableAppsGroupedByName_WithTheirMemory()
    {
        var text = DgpuPreviewText.Build(new DgpuScanResult(true, false,
        [
            App("msedge", 150, DgpuAppVerdict.Close, 1),
            App("msedge", 10, DgpuAppVerdict.Close, 2),
            App("blender", 800, DgpuAppVerdict.Close, 3)
        ]));

        Assert.Contains("blender (800 MB)", text);
        Assert.Contains("msedge (2 processes, 160 MB)", text);
        Assert.DoesNotContain("external display", text);
    }

    [Fact]
    public void ProtectedAndBackgroundProcesses_AreListedAsLeftAlone_NotAsClosable()
    {
        var text = DgpuPreviewText.Build(new DgpuScanResult(true, false,
        [
            App("dwm", 1700, DgpuAppVerdict.Protected, 1),
            App("TranslucentTB", 9, DgpuAppVerdict.NoWindow, 2),
            App("blender", 800, DgpuAppVerdict.Close, 3)
        ]));

        var closable = text[..text.IndexOf("Left alone", StringComparison.Ordinal)];

        Assert.Contains("blender", closable);
        Assert.DoesNotContain("dwm", closable);
        Assert.DoesNotContain("TranslucentTB", closable);
        Assert.Contains("dwm, TranslucentTB", text);
    }

    [Fact]
    public void NothingClosable_SaysNone() =>
        Assert.Contains("none", DgpuPreviewText.Build(new DgpuScanResult(true, false, [App("dwm", 1700, DgpuAppVerdict.Protected)])));
}
