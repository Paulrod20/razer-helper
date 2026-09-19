using System.ServiceProcess;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class RazerServiceManagerTests
{
    private const ServiceStartMode Auto = ServiceStartMode.Automatic;
    private const ServiceStartMode Manual = ServiceStartMode.Manual;
    private const ServiceStartMode Off = ServiceStartMode.Disabled;

    // A small copy of what is really installed on a Blade: automatic services,
    // a manual one that is running, a manual one that is not, and non-Razer services.
    private static FakeServiceControl RealisticMachine() => new FakeServiceControl()
        .Add("RazerExperienceService", running: true, Auto, "Razer Experience Service")
        .Add("Razer Chroma SDK Server", running: true, Auto)
        .Add("Razer Elevation Service", running: true, Manual)
        .Add("Razer Chroma Stream Server", running: false, Manual)
        .Add("wuauserv", running: true, Auto, "Windows Update")
        .Add("NVDisplay.ContainerLocalSystem", running: true, Auto, "NVIDIA Display Container LS");

    // ---- Recognising Razer services -------------------------------------

    [Theory]
    [InlineData("Razer Chroma SDK Server", "Razer Chroma SDK Server", true)]
    [InlineData("RazerExperienceService", "Some Other Title", true)]      // by service name
    [InlineData("RGS", "Razer Gaming Services Updater", true)]            // by display name only
    [InlineData("rzr", "RAZER SOMETHING", true)]                          // case-insensitive
    [InlineData("wuauserv", "Windows Update", false)]
    [InlineData("NVDisplay", "NVIDIA Display Container LS", false)]
    [InlineData("Spooler", "Print Spooler", false)]
    public void IsRazerService_MatchesRazersServicesAndNothingElse(string name, string displayName, bool expected)
    {
        Assert.Equal(expected, RazerServiceManager.IsRazerService(name, displayName));
    }

    // ---- Status ----------------------------------------------------------

    [Fact]
    public void GetStatus_CountsOnlyRazerServices_AndOnlyTheRunningOnesAsRunning()
    {
        var status = new RazerServiceManager(RealisticMachine()).GetStatus();

        Assert.Equal(4, status.Total);      // the two non-Razer services are not counted
        Assert.Equal(3, status.Running);
    }

    [Fact]
    public void GetStatus_OnAMachineWithoutRazerSoftware_ReportsNothing()
    {
        var control = new FakeServiceControl().Add("wuauserv", running: true, Auto, "Windows Update");

        var status = new RazerServiceManager(control).GetStatus();

        Assert.Equal(0, status.Total);
        Assert.Equal(0, status.Running);
    }

    // ---- Stopping --------------------------------------------------------

    [Fact]
    public void StopAndDisableAll_StopsThenDisablesEveryRazerService()
    {
        var control = RealisticMachine();

        var result = new RazerServiceManager(control).StopAndDisableAll();

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Succeeded.Count);
        Assert.All(control.Services.Where(s => RazerServiceManager.IsRazerService(s.Name, s.DisplayName)), service =>
        {
            Assert.False(service.IsRunning);
            Assert.Equal(Off, service.StartMode);
        });
    }

    [Fact]
    public void StopAndDisableAll_StopsAServiceBeforeDisablingIt()
    {
        var control = new FakeServiceControl().Add("Razer Chroma SDK Server", running: true, Auto);

        new RazerServiceManager(control).StopAndDisableAll();

        Assert.Equal(["stop Razer Chroma SDK Server", "mode Razer Chroma SDK Server=Disabled"], control.Calls);
    }

    [Fact]
    public void StopAndDisableAll_DoesNotTryToStopAServiceThatIsAlreadyStopped_ButStillDisablesIt()
    {
        // Stopping a stopped service can throw, and leaving it startable
        // would let something revive it later.
        var control = new FakeServiceControl().Add("Razer Chroma Stream Server", running: false, Manual);

        new RazerServiceManager(control).StopAndDisableAll();

        Assert.Equal(["mode Razer Chroma Stream Server=Disabled"], control.Calls);
    }

    [Fact]
    public void StopAndDisableAll_NeverTouchesServicesThatAreNotRazers()
    {
        var control = RealisticMachine();

        new RazerServiceManager(control).StopAndDisableAll();

        Assert.DoesNotContain(control.Calls, call => call.Contains("wuauserv") || call.Contains("NVDisplay"));
        Assert.True(control.Get("wuauserv").IsRunning);
        Assert.Equal(Auto, control.Get("wuauserv").StartMode);
    }

    [Fact]
    public void StopAndDisableAll_OneServiceThatWillNotStopDoesNotPreventTheOthers()
    {
        var control = RealisticMachine();
        control.FailToStop.Add("Razer Chroma SDK Server");

        var result = new RazerServiceManager(control).StopAndDisableAll();

        Assert.False(result.IsSuccess);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("Razer Chroma SDK Server", failure);
        Assert.Contains("Access is denied", failure);

        Assert.Equal(3, result.Succeeded.Count);
        Assert.False(control.Get("RazerExperienceService").IsRunning);
        Assert.Equal(Off, control.Get("RazerExperienceService").StartMode);
    }

    [Fact]
    public void StopAndDisableAll_LeavesAServiceItCouldNotStopAsItWas()
    {
        // Half-done is worse than not done: a service that would not stop
        // keeps its startup type, so a retry starts from a known state.
        var control = new FakeServiceControl().Add("Razer Chroma SDK Server", running: true, Auto);
        control.FailToStop.Add("Razer Chroma SDK Server");

        new RazerServiceManager(control).StopAndDisableAll();

        Assert.Equal(Auto, control.Get("Razer Chroma SDK Server").StartMode);
    }

    [Fact]
    public void StopAndDisableAll_OnAMachineWithoutRazerSoftware_DoesNothing()
    {
        var control = new FakeServiceControl().Add("wuauserv", running: true, Auto, "Windows Update");

        var result = new RazerServiceManager(control).StopAndDisableAll();

        Assert.True(result.IsSuccess);
        Assert.Empty(control.Calls);
    }

    // ---- Recording original startup types --------------------------------

    [Fact]
    public void RecordStartModes_RemembersEachServicesCurrentStartupType()
    {
        var manager = new RazerServiceManager(RealisticMachine());

        var recorded = RazerServiceManager.RecordStartModes(manager.GetStatus(), alreadyRecorded: null);

        Assert.Equal(Auto, recorded["RazerExperienceService"]);
        Assert.Equal(Manual, recorded["Razer Elevation Service"]);
        Assert.Equal(Manual, recorded["Razer Chroma Stream Server"]);
        Assert.Equal(4, recorded.Count);
    }

    [Fact]
    public void RecordStartModes_DoesNotRecordADisabledServiceItHasNeverSeenEnabled()
    {
        // Nothing says it was ever on, so restoring must not switch it on.
        var control = new FakeServiceControl().Add("Razer Elevation Service", running: false, Off);

        var recorded = RazerServiceManager.RecordStartModes(new RazerServiceManager(control).GetStatus(), null);

        Assert.Empty(recorded);
    }

    [Fact]
    public void RecordStartModes_KeepsTheOriginalWhenTheServiceIsNowDisabledByUs()
    {
        // The second time the user presses Stop the services are already
        // Disabled (by the first press). That must not erase what they were.
        var control = new FakeServiceControl().Add("RazerExperienceService", running: false, Off);
        var earlier = new Dictionary<string, ServiceStartMode> { ["RazerExperienceService"] = Auto };

        var recorded = RazerServiceManager.RecordStartModes(new RazerServiceManager(control).GetStatus(), earlier);

        Assert.Equal(Auto, recorded["RazerExperienceService"]);
    }

    [Fact]
    public void RecordStartModes_UpdatesARecordWhenTheUserChangedTheServiceSince()
    {
        var control = new FakeServiceControl().Add("RazerExperienceService", running: true, Manual);
        var earlier = new Dictionary<string, ServiceStartMode> { ["RazerExperienceService"] = Auto };

        var recorded = RazerServiceManager.RecordStartModes(new RazerServiceManager(control).GetStatus(), earlier);

        Assert.Equal(Manual, recorded["RazerExperienceService"]);
    }

    [Fact]
    public void RecordStartModes_DoesNotModifyTheDictionaryItWasGiven()
    {
        var earlier = new Dictionary<string, ServiceStartMode> { ["old"] = Auto };
        var control = new FakeServiceControl().Add("RazerExperienceService", running: true, Auto);

        RazerServiceManager.RecordStartModes(new RazerServiceManager(control).GetStatus(), earlier);

        Assert.Single(earlier);
    }

    // ---- Restoring -------------------------------------------------------

    [Fact]
    public void RestoreAll_PutsBackTheOriginalStartupTypes_AndStartsOnlyTheAutomaticOnes()
    {
        var control = RealisticMachine();
        var manager = new RazerServiceManager(control);
        var originals = RazerServiceManager.RecordStartModes(manager.GetStatus(), null);
        manager.StopAndDisableAll();
        control.Calls.Clear();

        var result = manager.RestoreAll(originals);

        Assert.True(result.IsSuccess);

        // Automatic: type restored and running again.
        Assert.Equal(Auto, control.Get("RazerExperienceService").StartMode);
        Assert.True(control.Get("RazerExperienceService").IsRunning);

        // Manual: type restored, but left for whatever needs it to start.
        Assert.Equal(Manual, control.Get("Razer Elevation Service").StartMode);
        Assert.False(control.Get("Razer Elevation Service").IsRunning);
        Assert.Equal(Manual, control.Get("Razer Chroma Stream Server").StartMode);
        Assert.False(control.Get("Razer Chroma Stream Server").IsRunning);
    }

    [Fact]
    public void RestoreAll_SetsTheStartupTypeBeforeStartingTheService()
    {
        // A disabled service cannot be started.
        var control = new FakeServiceControl().Add("RazerExperienceService", running: false, Off);

        new RazerServiceManager(control).RestoreAll(new Dictionary<string, ServiceStartMode> { ["RazerExperienceService"] = Auto });

        Assert.Equal(["mode RazerExperienceService=Automatic", "start RazerExperienceService"], control.Calls);
    }

    [Fact]
    public void RestoreAll_LeavesAServiceTheUserDisabledThemselves()
    {
        var control = new FakeServiceControl().Add("Razer Elevation Service", running: false, Off);

        new RazerServiceManager(control).RestoreAll(new Dictionary<string, ServiceStartMode>());

        Assert.Empty(control.Calls);
        Assert.Equal(Off, control.Get("Razer Elevation Service").StartMode);
    }

    [Fact]
    public void RestoreAll_StartsAnAutomaticServiceThatHasNoRecordButIsJustStopped()
    {
        var control = new FakeServiceControl().Add("RazerExperienceService", running: false, Auto);

        new RazerServiceManager(control).RestoreAll(new Dictionary<string, ServiceStartMode>());

        Assert.Equal(["start RazerExperienceService"], control.Calls);   // type not rewritten
    }

    [Fact]
    public void RestoreAll_IgnoresRecordsForServicesThatNoLongerExist()
    {
        var control = new FakeServiceControl().Add("RazerExperienceService", running: false, Off);
        var records = new Dictionary<string, ServiceStartMode>
        {
            ["RazerExperienceService"] = Auto,
            ["Razer Uninstalled Thing"] = Auto
        };

        var result = new RazerServiceManager(control).RestoreAll(records);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(control.Calls, call => call.Contains("Uninstalled"));
    }

    [Fact]
    public void RestoreAll_OneFailureDoesNotPreventTheOthers()
    {
        var control = new FakeServiceControl()
            .Add("RazerExperienceService", running: false, Off)
            .Add("Razer Chroma SDK Server", running: false, Off);
        var records = new Dictionary<string, ServiceStartMode>
        {
            ["RazerExperienceService"] = Auto,
            ["Razer Chroma SDK Server"] = Auto
        };
        var failing = new FailingStartControl(control, "RazerExperienceService");

        var result = new RazerServiceManager(failing).RestoreAll(records);

        Assert.False(result.IsSuccess);
        Assert.Contains("RazerExperienceService", Assert.Single(result.Failures));
        Assert.True(control.Get("Razer Chroma SDK Server").IsRunning);
    }

    /// <summary>Wraps a fake so that starting one named service throws.</summary>
    private sealed class FailingStartControl(FakeServiceControl inner, string failingName) : IServiceControl
    {
        public IReadOnlyList<ServiceState> Find(Func<string, string, bool> matches) => inner.Find(matches);

        public void Stop(string name) => inner.Stop(name);

        public void Start(string name)
        {
            if (name == failingName)
                throw new InvalidOperationException("The service did not respond to the start request.");

            inner.Start(name);
        }

        public void SetStartMode(string name, ServiceStartMode mode) => inner.SetStartMode(name, mode);
    }
}
