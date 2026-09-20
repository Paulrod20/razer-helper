using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class DgpuAppCloserTests
{
    private static readonly DateTime Started = new(2026, 3, 1, 8, 30, 0);

    private static DgpuApp App(int pid, string name, DgpuAppVerdict verdict = DgpuAppVerdict.Close, DateTime? started = null) =>
        new(pid, name, 100, verdict, started ?? Started);

    [Fact]
    public void AskingAnAppThatStillMatches_RequestsItToClose()
    {
        var control = new FakeProcessControl().Add(10, "blender", Started);

        var result = new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Equal([10], control.CloseRequests);
        Assert.Equal(new DgpuCloseResult(Asked: 1, Skipped: 0), result);
    }

    [Fact]
    public void ARecycledProcessId_IsNeverClosed()
    {
        // The scanned program exited and Windows gave its id to a newer one.
        var control = new FakeProcessControl().Add(10, "blender", Started.AddHours(3));

        var result = new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Empty(control.CloseRequests);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void ADifferentProgramUnderTheSameId_IsNeverClosed()
    {
        var control = new FakeProcessControl().Add(10, "notepad", Started);

        new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Empty(control.CloseRequests);
    }

    [Fact]
    public void AProcessThatIsGone_IsSkipped()
    {
        var control = new FakeProcessControl();

        var result = new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Empty(control.CloseRequests);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void WithoutAScannedStartTime_NothingIsClosed()
    {
        var control = new FakeProcessControl().Add(10, "blender", null);

        new DgpuAppCloser(control).Close([new DgpuApp(10, "blender", 100, DgpuAppVerdict.Close, StartTime: null)]);

        Assert.Empty(control.CloseRequests);
    }

    [Fact]
    public void WhenTheRunningStartTimeCannotBeRead_NothingIsClosed()
    {
        var control = new FakeProcessControl().Add(10, "blender", null);

        new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Empty(control.CloseRequests);
    }

    [Theory]
    [InlineData((int)DgpuAppVerdict.Protected)]
    [InlineData((int)DgpuAppVerdict.ThisApp)]
    [InlineData((int)DgpuAppVerdict.OtherSession)]
    [InlineData((int)DgpuAppVerdict.NoWindow)]
    public void OnlyTheCloseVerdictIsEverActedOn(int verdictValue)
    {
        var control = new FakeProcessControl().Add(10, "blender", Started);

        new DgpuAppCloser(control).Close([App(10, "blender", (DgpuAppVerdict)verdictValue)]);

        Assert.Empty(control.CloseRequests);
    }

    [Theory]
    [InlineData("explorer")]
    [InlineData("dwm")]
    [InlineData("claude")]
    [InlineData("WindowsTerminal")]
    public void AProtectedName_IsRefusedEvenIfSomethingWrongMarkedItClosable(string name)
    {
        var control = new FakeProcessControl().Add(10, name, Started);

        new DgpuAppCloser(control).Close([App(10, name)]);

        Assert.Empty(control.CloseRequests);
    }

    [Fact]
    public void TheUsersNeverCloseList_IsRespectedAtCloseTime()
    {
        var control = new FakeProcessControl().Add(10, "blender", Started);

        new DgpuAppCloser(control).Close([App(10, "blender")], neverClose: ["Blender"]);

        Assert.Empty(control.CloseRequests);
    }

    [Fact]
    public void AnAppWithNoWindowToClose_IsCountedAsSkipped()
    {
        var control = new FakeProcessControl().Add(10, "blender", Started);
        control.WithoutWindow.Add(10);

        var result = new DgpuAppCloser(control).Close([App(10, "blender")]);

        Assert.Equal(new DgpuCloseResult(Asked: 0, Skipped: 1), result);
    }

    [Fact]
    public void OneBadAppDoesNotStopTheOthers()
    {
        var control = new FakeProcessControl()
            .Add(10, "blender", Started.AddHours(1)) // Recycled id.
            .Add(11, "gimp", Started);

        var result = new DgpuAppCloser(control).Close([App(10, "blender"), App(11, "gimp")]);

        Assert.Equal([11], control.CloseRequests);
        Assert.Equal(new DgpuCloseResult(Asked: 1, Skipped: 1), result);
    }

    [Fact]
    public void TheProcessControlInterface_HasNoWayToKillAProcess()
    {
        // The guarantee that apps always get to ask about unsaved work.
        var members = typeof(IProcessControl).GetMethods().Select(method => method.Name).Order();

        Assert.Equal(["Identify", "RequestClose"], members);
    }
}
