using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class DgpuUnplugCoordinatorTests
{
    private static readonly DateTime Started = new(2026, 3, 1, 8, 30, 0);

    private static DgpuApp Closable(int pid, string name = "blender", DateTime? started = null) =>
        new(pid, name, 500L * 1024 * 1024, DgpuAppVerdict.Close, started ?? Started);

    private static DgpuScanResult Scan(bool? externalDisplay, params DgpuApp[] apps) =>
        new(GpuFound: true, externalDisplay, apps);

    // Everything the coordinator talks to, under the test's control.
    private sealed class Rig : IDisposable
    {
        public Rig(bool pluggedIn = true, bool enabled = true)
        {
            Power = new FakePowerSource(pluggedIn);
            Coordinator = new DgpuUnplugCoordinator(
                Power,
                () =>
                {
                    ScanCount++;
                    return Task.FromResult(NextScan());
                },
                async (apps, _) =>
                {
                    Shown.Add(apps);
                    return await Answer();
                },
                apps =>
                {
                    Closed.Add(apps);
                    return new DgpuCloseResult(apps.Count, 0);
                })
            {
                Enabled = enabled
            };
        }

        public FakePowerSource Power { get; }
        public DgpuUnplugCoordinator Coordinator { get; }
        public int ScanCount { get; private set; }
        public List<IReadOnlyList<DgpuApp>> Shown { get; } = [];
        public List<IReadOnlyList<DgpuApp>> Closed { get; } = [];

        public Func<DgpuScanResult> NextScan { get; set; } = () => Scan(false, Closable(10));
        public Func<Task<bool>> Answer { get; set; } = () => Task.FromResult(true);

        public Task Unplug()
        {
            Power.Set(false);
            return Coordinator.LastRun;
        }

        public void Dispose() => Coordinator.Dispose();
    }

    [Fact]
    public async Task Unplugging_ScansAsksAndClosesWhatWasShown()
    {
        using var rig = new Rig();

        await rig.Unplug();

        Assert.Equal([10], rig.Shown.Single().Select(app => app.ProcessId));
        Assert.Equal([10], rig.Closed.Single().Select(app => app.ProcessId));
    }

    [Fact]
    public async Task WhenTurnedOff_UnplugDoesNothingAtAll()
    {
        using var rig = new Rig(enabled: false);

        await rig.Unplug();

        Assert.Equal(0, rig.ScanCount);
        Assert.Empty(rig.Shown);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task TurningItOnWhileOnBattery_DoesNothingUntilTheNextUnplug()
    {
        using var rig = new Rig(pluggedIn: false, enabled: false);

        rig.Coordinator.Enabled = true;
        rig.Power.RaiseWithoutChange();
        await rig.Coordinator.LastRun;

        Assert.Equal(0, rig.ScanCount);
    }

    [Fact]
    public async Task BatteryPercentageUpdatesOnBattery_NeverTriggerIt()
    {
        using var rig = new Rig();
        await rig.Unplug();
        var scansAfterUnplug = rig.ScanCount;

        rig.Power.RaiseWithoutChange();
        rig.Power.RaiseWithoutChange();
        await rig.Coordinator.LastRun;

        Assert.Equal(scansAfterUnplug, rig.ScanCount);
        Assert.Single(rig.Closed);
    }

    [Fact]
    public async Task PluggingIn_DoesNothing()
    {
        using var rig = new Rig(pluggedIn: false);

        rig.Power.Set(true);
        await rig.Coordinator.LastRun;

        Assert.Equal(0, rig.ScanCount);
    }

    [Fact]
    public async Task ChargerStatusThatIsUnknown_CountsAsPlugged_SoNeverTriggers()
    {
        using var rig = new Rig();

        rig.Power.Set(null);
        await rig.Coordinator.LastRun;

        Assert.Equal(0, rig.ScanCount);
    }

    [Theory]
    [InlineData(true)]  // External display connected.
    [InlineData(null)]  // Windows would not say.
    public async Task WithAnExternalDisplay_NothingIsAskedOrClosed(bool? externalDisplay)
    {
        using var rig = new Rig();
        rig.NextScan = () => Scan(externalDisplay, Closable(10));

        await rig.Unplug();

        Assert.Empty(rig.Shown);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task WithNothingToClose_TheUserIsNotAsked()
    {
        using var rig = new Rig();
        rig.NextScan = () => Scan(false, new DgpuApp(1, "dwm", 1, DgpuAppVerdict.Protected, Started));

        await rig.Unplug();

        Assert.Empty(rig.Shown);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task WhenTheUserSaysNo_NothingIsClosed()
    {
        using var rig = new Rig();
        rig.Answer = () => Task.FromResult(false);

        await rig.Unplug();

        Assert.Single(rig.Shown);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task IfTheChargerComesBackWhileAsking_NothingIsClosed()
    {
        using var rig = new Rig();
        rig.Answer = () =>
        {
            rig.Power.Set(true);
            return Task.FromResult(true);
        };

        await rig.Unplug();

        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task AnExternalDisplayConnectedWhileAsking_StopsTheClosing()
    {
        using var rig = new Rig();
        var scans = 0;
        rig.NextScan = () => ++scans == 1 ? Scan(false, Closable(10)) : Scan(true, Closable(10));

        await rig.Unplug();

        Assert.Single(rig.Shown);
        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task OnlyAppsStillEligibleAfterTheQuestion_AreClosed()
    {
        using var rig = new Rig();
        var scans = 0;
        rig.NextScan = () => ++scans == 1
            ? Scan(false, Closable(10, "blender"), Closable(11, "gimp"))
            : Scan(false, Closable(11, "gimp")); // blender was closed by the user meanwhile.

        await rig.Unplug();

        Assert.Equal([11], rig.Closed.Single().Select(app => app.ProcessId));
    }

    [Fact]
    public async Task AnAppReplacedUnderTheSameIdWhileAsking_IsNotClosed()
    {
        using var rig = new Rig();
        var scans = 0;
        rig.NextScan = () => ++scans == 1
            ? Scan(false, Closable(10, "blender", Started))
            : Scan(false, Closable(10, "gimp", Started.AddMinutes(5))); // Same id, new program.

        await rig.Unplug();

        Assert.Empty(rig.Closed);
    }

    [Fact]
    public async Task AnAppThatAppearedAfterTheQuestion_IsNeverClosed()
    {
        using var rig = new Rig();
        var scans = 0;
        rig.NextScan = () => ++scans == 1
            ? Scan(false, Closable(10, "blender"))
            : Scan(false, Closable(10, "blender"), Closable(12, "steam")); // Never shown to the user.

        await rig.Unplug();

        Assert.Equal([10], rig.Closed.Single().Select(app => app.ProcessId));
    }

    [Fact]
    public async Task AFailure_IsContained_AndTheNextUnplugStillWorks()
    {
        using var rig = new Rig();
        var fail = true;
        rig.NextScan = () => fail ? throw new InvalidOperationException("counters unavailable") : Scan(false, Closable(10));

        await rig.Unplug();
        Assert.Empty(rig.Closed);

        fail = false;
        rig.Power.Set(true);
        await rig.Unplug();

        Assert.Single(rig.Closed);
    }

    [Fact]
    public async Task AnUnplugWhileTheQuestionIsStillOpen_IsIgnored()
    {
        using var rig = new Rig();
        var answer = new TaskCompletionSource<bool>();
        rig.Answer = () => answer.Task;

        var firstRun = rig.Unplug();
        rig.Power.Set(true);
        rig.Power.Set(false); // Unplugged again with the first question unanswered.

        Assert.Single(rig.Shown);

        answer.SetResult(false);
        await firstRun;

        Assert.Equal(1, rig.Shown.Count);
    }

    [Fact]
    public async Task AfterDispose_PowerChangesAreIgnored()
    {
        var rig = new Rig();
        rig.Dispose();

        rig.Power.Set(false);
        await rig.Coordinator.LastRun;

        Assert.Equal(0, rig.ScanCount);
    }
}
