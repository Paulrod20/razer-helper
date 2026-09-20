using System.ServiceProcess;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class RazerSoftwareSummaryTests
{
    private static readonly DateTime At = new(2026, 9, 19, 23, 14, 5);

    private static ServiceState Service(bool running, ServiceStartMode mode = ServiceStartMode.Automatic) =>
        new("s", "s", running, mode);

    private static string Describe(
        ServiceState[]? services = null,
        string[]? apps = null,
        RazerLoginEntry[]? entries = null) =>
        RazerSoftwareSummary.Describe(
            new RazerSoftwareStatus(new RazerServicesStatus(services ?? []), apps ?? [], entries ?? []),
            At);

    [Fact]
    public void TheFirstLine_ShowsHowTheNumberOnTheLabelAddsUp()
    {
        var text = Describe(services: [Service(true), Service(true)], apps: ["RazerAppEngine"]);

        Assert.StartsWith("3 running = 2 services + 1 Razer programs", text);
    }

    [Fact]
    public void ServicesAreShownAsRunningOutOfTotal()
    {
        var text = Describe(services: [Service(true), Service(false, ServiceStartMode.Disabled), Service(false, ServiceStartMode.Disabled)]);

        Assert.Contains("Services: 1 of 3 running", text);
    }

    [Fact]
    public void ServicesStoppedButNotDisabled_AreCalledOut_SoZeroRunningDoesNotLookLikeNothingToDo()
    {
        var text = Describe(services: [Service(false, ServiceStartMode.Automatic), Service(false, ServiceStartMode.Disabled)]);

        Assert.Contains("Services: 0 of 2 running", text);
        Assert.Contains("(1 not yet disabled)", text);
    }

    [Fact]
    public void WhenServicesAreAllOffAndDisabled_NothingIsCalledOut()
    {
        var text = Describe(services: [Service(false, ServiceStartMode.Disabled)]);

        Assert.DoesNotContain("not yet disabled", text);
    }

    [Fact]
    public void RunningProgramsAreListedByName()
    {
        var text = Describe(apps: ["RazerAppEngine", "razerwdl"]);

        Assert.Contains("Programs running:", text);
        Assert.Contains("  RazerAppEngine", text);
        Assert.Contains("  razerwdl", text);
    }

    [Fact]
    public void NoPrograms_SaysNone() =>
        Assert.Contains("Programs: none running", Describe());

    [Fact]
    public void AnEnabledLoginEntry_IsShownAsOn_WithItsName()
    {
        var text = Describe(entries: [new RazerLoginEntry("RazerAppEngine", "cmd", IsEnabled: true)]);

        Assert.Contains("Start at login: ON (RazerAppEngine)", text);
    }

    [Fact]
    public void ADisabledLoginEntry_IsShownAsOff() =>
        Assert.Contains("Start at login: off", Describe(entries: [new RazerLoginEntry("RazerAppEngine", "cmd", IsEnabled: false)]));

    [Fact]
    public void NoLoginEntryAtAll_SaysSo() =>
        Assert.Contains("no Razer entry found", Describe());

    [Fact]
    public void ItSaysWhenItWasChecked_SoAStaleNumberIsEasyToSpot() =>
        Assert.EndsWith("Checked at 23:14:05", Describe());
}
