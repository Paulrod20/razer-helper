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

        var apps = Classify(usages, running);

        Assert.Equal(["big", "small"], apps.Select(app => app.Name));
    }

    [Fact]
    public void Classify_WithNothingOnTheGpu_IsEmpty() =>
        Assert.Empty(Classify([], new Dictionary<int, RunningProcess>()));

    // Helper processes: a browser's GPU process has no window, its main process does.

    private static readonly DateTime Start = new(2026, 1, 1, 9, 0, 0);

    private static IReadOnlyList<DgpuApp> Classify(
        IEnumerable<GpuProcessUsage> usages,
        params RunningProcess[] processes) =>
        Classify(usages, processes.ToDictionary(process => process.ProcessId));

    private static IReadOnlyList<DgpuApp> Classify(
        IEnumerable<GpuProcessUsage> usages,
        Dictionary<int, RunningProcess> running) =>
        DgpuAppSelector.Classify(usages, pid => running.GetValueOrDefault(pid), ThisPid, ThisSession);

    private static RunningProcess Helper(int pid, string name, int parent, DateTime? started, bool hasWindow = false) =>
        new(pid, name, ThisSession, hasWindow, parent, started);

    [Fact]
    public void AHelperWithoutAWindow_IsCountedUnderItsSameNamedParentApp()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 300 * 1024 * 1024)],
            Helper(300, "msedge", parent: 1, Start, hasWindow: true),
            Helper(301, "msedge", parent: 300, Start.AddSeconds(5)));

        var app = Assert.Single(apps);
        Assert.Equal(300, app.ProcessId);
        Assert.Equal(DgpuAppVerdict.Close, app.Verdict);
    }

    [Fact]
    public void AHelperTwoLevelsDown_IsStillTracedToTheApp()
    {
        var apps = Classify(
            [new GpuProcessUsage(302, 1)],
            Helper(300, "msedge", parent: 1, Start, hasWindow: true),
            Helper(301, "msedge", parent: 300, Start.AddSeconds(1)),
            Helper(302, "msedge", parent: 301, Start.AddSeconds(2)));

        Assert.Equal(300, Assert.Single(apps).ProcessId);
    }

    [Fact]
    public void SeveralHelpersOfOneApp_AreOneEntryWithTheirMemoryAddedUp()
    {
        var apps = Classify(
            [new GpuProcessUsage(300, 10), new GpuProcessUsage(301, 200), new GpuProcessUsage(302, 5)],
            Helper(300, "msedge", parent: 1, Start, hasWindow: true),
            Helper(301, "msedge", parent: 300, Start.AddSeconds(1)),
            Helper(302, "msedge", parent: 300, Start.AddSeconds(2)));

        var app = Assert.Single(apps);
        Assert.Equal(215, app.DedicatedBytes);
    }

    [Fact]
    public void AParentThatIsADifferentProgram_IsNeverTakenForTheOwner()
    {
        // A script on the GPU, started from a terminal that has a window: the
        // terminal must not be closed to stop it.
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "WindowsTerminal", parent: 1, Start, hasWindow: true),
            Helper(301, "python", parent: 300, Start.AddSeconds(5)));

        var app = Assert.Single(apps);
        Assert.Equal(301, app.ProcessId);
        Assert.Equal(DgpuAppVerdict.NoWindow, app.Verdict);
    }

    [Fact]
    public void AParentStartedAfterTheChild_IsAReusedId_NotAParent()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "msedge", parent: 1, Start.AddMinutes(10), hasWindow: true),
            Helper(301, "msedge", parent: 300, Start));

        Assert.Equal(DgpuAppVerdict.NoWindow, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void WithoutStartTimes_AParentIsNotTrusted()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "msedge", parent: 1, started: null, hasWindow: true),
            Helper(301, "msedge", parent: 300, started: null));

        Assert.Equal(DgpuAppVerdict.NoWindow, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void AParentThatCannotBeInspected_LeavesTheHelperAlone()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(301, "msedge", parent: 300, Start));

        Assert.Equal(DgpuAppVerdict.NoWindow, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void AProtectedHelper_IsNotMappedToAnything()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "explorer", parent: 1, Start, hasWindow: true),
            Helper(301, "explorer", parent: 300, Start.AddSeconds(1)));

        Assert.Equal(DgpuAppVerdict.Protected, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void AHelperWhoseParentChainIsALoop_StillTerminates()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "msedge", parent: 301, Start),
            Helper(301, "msedge", parent: 300, Start));

        Assert.Equal(DgpuAppVerdict.NoWindow, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void TheOwnersDetailsAreUsed_NotTheHelpers()
    {
        var apps = Classify(
            [new GpuProcessUsage(301, 1)],
            Helper(300, "msedge", parent: 1, Start, hasWindow: true),
            Helper(301, "msedge", parent: 300, Start.AddSeconds(1)));

        Assert.Equal("msedge", Assert.Single(apps).Name);
    }
}

public class DgpuAppSelectorNeverCloseTests
{
    private static readonly RunningProcess Blender = new(200, "blender", 2, HasWindow: true);

    [Theory]
    [InlineData("claude")]
    [InlineData("Claude")]
    [InlineData("WindowsTerminal")]
    [InlineData("pwsh")]
    [InlineData("cmd")]
    [InlineData("devenv")]
    [InlineData("Code")]
    [InlineData("Cursor")]
    public void TerminalsEditorsAndTheClaudeApp_AreProtectedByDefault(string name) =>
        Assert.Equal(DgpuAppVerdict.Protected, DgpuAppSelector.Decide(new RunningProcess(200, name, 2, true), 100, 2));

    [Fact]
    public void AProgramTheUserAddedToTheirList_IsProtected() =>
        Assert.Equal(DgpuAppVerdict.Protected, DgpuAppSelector.Decide(Blender, 100, 2, neverClose: ["blender"]));

    [Theory]
    [InlineData("BLENDER")]
    [InlineData("blender.exe")]
    [InlineData("  blender  ")]
    public void TheUsersList_IgnoresCaseSpacesAndExeSuffix(string entry) =>
        Assert.Equal(DgpuAppVerdict.Protected, DgpuAppSelector.Decide(Blender, 100, 2, neverClose: [entry]));

    [Fact]
    public void OtherProgramsStayClosable_WhenTheUserListsADifferentOne() =>
        Assert.Equal(DgpuAppVerdict.Close, DgpuAppSelector.Decide(Blender, 100, 2, neverClose: ["gimp"]));

    [Fact]
    public void TheUsersList_AppliesThroughClassify_AndTheHelperTrace()
    {
        var main = new RunningProcess(300, "blender", 2, true, 1, new DateTime(2026, 1, 1, 9, 0, 0));
        var helper = new RunningProcess(301, "blender", 2, false, 300, new DateTime(2026, 1, 1, 9, 0, 5));
        var byId = new[] { main, helper }.ToDictionary(p => p.ProcessId);

        var apps = DgpuAppSelector.Classify(
            [new GpuProcessUsage(301, 1)], pid => byId.GetValueOrDefault(pid), 100, 2, neverClose: ["blender"]);

        Assert.NotEqual(DgpuAppVerdict.Close, Assert.Single(apps).Verdict);
    }

    [Fact]
    public void TheOwnersStartTime_IsCarriedForTheCloserToCheck()
    {
        var started = new DateTime(2026, 1, 1, 9, 0, 0);
        var main = new RunningProcess(300, "blender", 2, true, 1, started);
        var byId = new[] { main }.ToDictionary(p => p.ProcessId);

        var apps = DgpuAppSelector.Classify([new GpuProcessUsage(300, 1)], pid => byId.GetValueOrDefault(pid), 100, 2);

        Assert.Equal(started, Assert.Single(apps).StartTime);
    }
}
