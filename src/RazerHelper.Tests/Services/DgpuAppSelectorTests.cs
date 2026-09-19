using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class DgpuAppSelectorTests
{
    private const int ThisPid = 100;
    private const int ThisSession = 2;

    private static RunningProcess Process(int pid, string name, int session = ThisSession, bool hasWindow = true) =>
        new(pid, name, session, hasWindow);

    private static DgpuAppVerdict Decide(RunningProcess process) =>
        DgpuAppSelector.Decide(process, ThisPid, ThisSession);

    [Fact]
    public void OrdinaryAppWithAWindow_CanBeClosed() =>
        Assert.Equal(DgpuAppVerdict.Close, Decide(Process(200, "blender")));

    [Theory]
    [InlineData("dwm")]
    [InlineData("explorer")]
    [InlineData("csrss")]
    [InlineData("winlogon")]
    [InlineData("svchost")]
    [InlineData("ShellExperienceHost")]
    [InlineData("StartMenuExperienceHost")]
    [InlineData("TextInputHost")]
    [InlineData("ApplicationFrameHost")]
    [InlineData("SystemSettings")]
    [InlineData("fontdrvhost")]
    [InlineData("nvcontainer")]
    [InlineData("NVIDIA Overlay")]
    [InlineData("nvidia-smi")]
    [InlineData("RazerAppEngine")]
    [InlineData("RazerCentralService")]
    [InlineData("EXPLORER")]
    [InlineData("explorer.exe")]
    public void WindowsDriverAndRazerProcesses_AreNeverTouched(string name) =>
        Assert.Equal(DgpuAppVerdict.Protected, Decide(Process(200, name)));

    [Fact]
    public void ThisAppItself_IsNeverTouched() =>
        Assert.Equal(DgpuAppVerdict.ThisApp, Decide(Process(ThisPid, "RazerHelper")));

    [Fact]
    public void ThisApp_WinsEvenOverTheOtherRules() =>
        Assert.Equal(DgpuAppVerdict.ThisApp, Decide(Process(ThisPid, "explorer", session: 0)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(-1)]
    public void ProcessesInAnotherSessionOrSession0_AreNeverTouched(int session) =>
        Assert.Equal(DgpuAppVerdict.OtherSession, Decide(Process(200, "blender", session)));

    [Fact]
    public void ProcessWithoutAWindow_IsLeftRunning() =>
        Assert.Equal(DgpuAppVerdict.NoWindow, Decide(Process(200, "TranslucentTB", hasWindow: false)));

    [Fact]
    public void ProtectedNameWithoutAWindow_IsStillProtected() =>
        Assert.Equal(DgpuAppVerdict.Protected, Decide(Process(200, "dwm", hasWindow: false)));

    [Fact]
    public void AnAppThatMerelyContainsAProtectedWord_IsNotProtected() =>
        Assert.Equal(DgpuAppVerdict.Close, Decide(Process(200, "nvim")));

    [Fact]
    public void Classify_SortsByVideoMemory_AndDropsProcessesThatAreGone()
    {
        var usages = new[]
        {
            new GpuProcessUsage(200, 10_000_000),
            new GpuProcessUsage(201, 900_000_000),
            new GpuProcessUsage(999, 5_000_000_000) // Not running any more.
        };

        var running = new Dictionary<int, RunningProcess>
        {
            [200] = Process(200, "small"),
            [201] = Process(201, "big")
        };

        var apps = DgpuAppSelector.Classify(usages, running, ThisPid, ThisSession);

        Assert.Equal(["big", "small"], apps.Select(app => app.Name));
    }

    [Fact]
    public void Classify_WithNothingOnTheGpu_IsEmpty() =>
        Assert.Empty(DgpuAppSelector.Classify([], new Dictionary<int, RunningProcess>(), ThisPid, ThisSession));
}
