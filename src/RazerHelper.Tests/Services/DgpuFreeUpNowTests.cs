using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;
using static RazerHelper.Tests.TestSupport.CoordinatorRig;

namespace RazerHelper.Tests.Services;

// "Free up GPU": the same flow as after an unplug, on request, whatever the power source.
public class DgpuFreeUpNowTests
{
    [Fact]
    public async Task PluggedIn_ItWorks_AndTheQuestionIsNotMarkedForWithdrawal()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);

        var outcome = await rig.Coordinator.FreeUpAsync();

        Assert.Equal(DgpuFreeUpOutcome.Closed, outcome);
        Assert.Equal([false], rig.DismissFlags);
        Assert.Equal([10], rig.Closed.Single().Select(app => app.ProcessId));
    }

    [Fact]
    public async Task OnBattery_ItWorksToo()
    {
        using var rig = new CoordinatorRig(pluggedIn: false);

        Assert.Equal(DgpuFreeUpOutcome.Closed, await rig.Coordinator.FreeUpAsync());
    }

    [Fact]
    public async Task ItWorksWithTheUnplugOptionTurnedOff()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.Coordinator.Enabled = false;

        Assert.Equal(DgpuFreeUpOutcome.Closed, await rig.Coordinator.FreeUpAsync());
    }

    [Fact]
    public async Task APowerEventWhilePluggedInAndAsking_DoesNotCancelIt()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.Answer = () =>
        {
            rig.Power.RaiseWithoutChange(); // A battery percentage update while the question is open.
            return Task.FromResult(true);
        };

        Assert.Equal(DgpuFreeUpOutcome.Closed, await rig.Coordinator.FreeUpAsync());
    }

    [Fact]
    public async Task AfterAnUnplug_TheQuestionIsMarkedToBeWithdrawnIfTheChargerComesBack()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.Coordinator.Enabled = true;

        rig.Power.Set(false);
        await rig.Coordinator.LastRun;

        Assert.Equal([true], rig.DismissFlags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task WithAnExternalDisplay_ItSaysSoAndClosesNothing(bool? externalDisplay)
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.NextScan = () => new DgpuScanResult(true, externalDisplay, [Closable(10)]);

        Assert.Equal(DgpuFreeUpOutcome.ExternalDisplay, await rig.Coordinator.FreeUpAsync());
        Assert.Empty(rig.DismissFlags);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task WithoutADedicatedGpu_ItSaysSo()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.NextScan = () => new DgpuScanResult(false, false, []);

        Assert.Equal(DgpuFreeUpOutcome.NoDedicatedGpu, await rig.Coordinator.FreeUpAsync());
    }

    [Fact]
    public async Task WithNothingToClose_ItSaysSo_AndDoesNotAsk()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.NextScan = () => new DgpuScanResult(true, false, [new DgpuApp(1, "dwm", 1, DgpuAppVerdict.Protected, Started)]);

        Assert.Equal(DgpuFreeUpOutcome.NothingToClose, await rig.Coordinator.FreeUpAsync());
        Assert.Empty(rig.DismissFlags);
    }

    [Fact]
    public async Task WhenTheUserDeclines_NothingIsClosed()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        rig.Answer = () => Task.FromResult(false);

        Assert.Equal(DgpuFreeUpOutcome.Declined, await rig.Coordinator.FreeUpAsync());
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task AnExternalDisplayConnectedWhileAsking_StopsTheClosing()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        var scans = 0;
        rig.NextScan = () => ++scans == 1
            ? new DgpuScanResult(true, false, [Closable(10)])
            : new DgpuScanResult(true, true, [Closable(10)]);

        Assert.Equal(DgpuFreeUpOutcome.ConditionsChanged, await rig.Coordinator.FreeUpAsync());
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task ClickingTwice_TheSecondIsToldItIsBusy()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        var answer = new TaskCompletionSource<bool>();
        rig.Answer = () => answer.Task;

        var first = rig.Coordinator.FreeUpAsync();
        var second = await rig.Coordinator.FreeUpAsync();
        answer.SetResult(false);
        await first;

        Assert.Equal(DgpuFreeUpOutcome.Busy, second);
        Assert.Single(rig.DismissFlags);
    }

    [Fact]
    public async Task AFailure_ReportsFailed_AndTheNextClickStillWorks()
    {
        using var rig = new CoordinatorRig(pluggedIn: true);
        var fail = true;
        rig.NextScan = () => fail ? throw new InvalidOperationException("boom") : new DgpuScanResult(true, false, [Closable(10)]);

        Assert.Equal(DgpuFreeUpOutcome.Failed, await rig.Coordinator.FreeUpAsync());

        fail = false;
        Assert.Equal(DgpuFreeUpOutcome.Closed, await rig.Coordinator.FreeUpAsync());
    }

    [Fact]
    public async Task IfNoAppCouldBeClosed_ItIsNotReportedAsClosed()
    {
        using var coordinator = new DgpuFreeUpCoordinator(
            new FakePowerSource(true),
            () => Task.FromResult(new DgpuScanResult(true, false, [Closable(10)])),
            (_, _) => Task.FromResult(true),
            _ => new DgpuCloseResult(Asked: 0, Skipped: 1));

        Assert.Equal(DgpuFreeUpOutcome.ConditionsChanged, await coordinator.FreeUpAsync());
    }
}
