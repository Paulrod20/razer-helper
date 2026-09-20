using System.ServiceProcess;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class RazerSoftwareStatusTests
{
    private static ServiceState Service(string name, bool running, ServiceStartMode mode) =>
        new(name, name, running, mode);

    private static RazerSoftwareStatus Status(
        ServiceState[]? services = null,
        string[]? apps = null,
        RazerLoginEntry[]? entries = null) =>
        new(new RazerServicesStatus(services ?? []), apps ?? [], entries ?? []);

    private static RazerLoginEntry Entry(bool enabled) => new("RazerAppEngine", "cmd", enabled);

    [Fact]
    public void TheCount_IsRunningServicesPlusRunningPrograms()
    {
        var status = Status(
            services: [Service("a", true, ServiceStartMode.Automatic), Service("b", false, ServiceStartMode.Disabled)],
            apps: ["RazerAppEngine", "razerwdl"]);

        Assert.Equal(3, status.Running);
    }

    [Fact]
    public void NothingOfRazersOnThePc_MeansNotInstalled() =>
        Assert.False(Status().IsInstalled);

    [Fact]
    public void AnyOneKindOfRazerSoftware_MeansInstalled()
    {
        Assert.True(Status(services: [Service("a", false, ServiceStartMode.Disabled)]).IsInstalled);
        Assert.True(Status(apps: ["RazerAppEngine"]).IsInstalled);
        Assert.True(Status(entries: [Entry(false)]).IsInstalled);
    }

    [Fact]
    public void YourSituation_ServicesStoppedButSynapseOpenAndStartingAtLogin_StillOffersStop()
    {
        var status = Status(
            services: [Service("a", false, ServiceStartMode.Disabled)],
            apps: ["RazerAppEngine"],
            entries: [Entry(enabled: true)]);

        Assert.True(status.NeedsStop);
    }

    [Fact]
    public void ServicesAlreadyOff_ButRazerStillStartsAtLogin_OffersStop()
    {
        var status = Status(
            services: [Service("a", false, ServiceStartMode.Disabled)],
            entries: [Entry(enabled: true)]);

        Assert.Equal(0, status.Running);
        Assert.True(status.NeedsStop);
    }

    [Fact]
    public void EverythingOff_OffersStart()
    {
        var status = Status(
            services: [Service("a", false, ServiceStartMode.Disabled)],
            entries: [Entry(enabled: false)]);

        Assert.False(status.NeedsStop);
    }

    [Fact]
    public void AServiceThatIsStoppedButCouldStillStart_OffersStop() =>
        Assert.True(Status(services: [Service("a", false, ServiceStartMode.Automatic)]).NeedsStop);

    [Fact]
    public void OnlyServicesNotAlreadyStoppedAndDisabled_AreListedForStopping()
    {
        var status = Status(services:
        [
            Service("running", true, ServiceStartMode.Automatic),
            Service("manual", false, ServiceStartMode.Manual),
            Service("done", false, ServiceStartMode.Disabled)
        ]);

        Assert.Equal(["running", "manual"], status.ServicesToStop.Select(service => service.Name));
    }

    [Fact]
    public void NothingLeftToStop_MeansNoServicesToStop_SoNoAdministratorPromptIsNeeded()
    {
        var status = Status(
            services: [Service("done", false, ServiceStartMode.Disabled)],
            entries: [Entry(enabled: true)]);

        Assert.Empty(status.ServicesToStop);
    }
}
