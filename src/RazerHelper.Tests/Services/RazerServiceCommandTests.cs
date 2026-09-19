using System.ServiceProcess;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class RazerServiceCommandTests
{
    private static FakeServiceControl Machine() => new FakeServiceControl()
        .Add("RazerExperienceService", running: true, ServiceStartMode.Automatic)
        .Add("Razer Elevation Service", running: true, ServiceStartMode.Manual)
        .Add("wuauserv", running: true, ServiceStartMode.Automatic, "Windows Update");

    // ---- Recognising a service command -----------------------------------

    [Theory]
    [InlineData]                              // no arguments: a normal launch
    [InlineData("--something-else")]
    [InlineData("stop")]
    [InlineData("--RAZER-SERVICES", "stop")]  // the switch is exact
    public void TryRun_ReturnsNullForArgumentsThatAreNotAServiceCommand(params string[] args)
    {
        var control = Machine();

        Assert.Null(RazerServiceCommand.TryRun(args, control));
        Assert.Empty(control.Calls);
    }

    // ---- Stop -------------------------------------------------------------

    [Fact]
    public void TryRun_Stop_StopsAndDisablesTheRazerServicesAndReportsSuccess()
    {
        var control = Machine();

        var exitCode = RazerServiceCommand.TryRun(RazerServiceCommand.StopArguments(), control);

        Assert.Equal(RazerServiceCommand.Success, exitCode);
        Assert.False(control.Get("RazerExperienceService").IsRunning);
        Assert.Equal(ServiceStartMode.Disabled, control.Get("RazerExperienceService").StartMode);
        Assert.True(control.Get("wuauserv").IsRunning);   // not Razer: untouched
    }

    [Fact]
    public void TryRun_Stop_ReportsPartialFailureWithADistinctExitCode()
    {
        var control = Machine();
        control.FailToStop.Add("RazerExperienceService");

        var exitCode = RazerServiceCommand.TryRun(RazerServiceCommand.StopArguments(), control);

        Assert.Equal(RazerServiceCommand.SomeFailed, exitCode);
        Assert.NotEqual(RazerServiceCommand.Success, exitCode);
    }

    // ---- Restore ----------------------------------------------------------

    [Fact]
    public void TryRun_Restore_PutsTheRecordedStartupTypesBack()
    {
        var control = Machine();
        var recorded = RazerServiceManager.RecordStartModes(new RazerServiceManager(control).GetStatus(), null);
        RazerServiceCommand.TryRun(RazerServiceCommand.StopArguments(), control);

        var exitCode = RazerServiceCommand.TryRun(RazerServiceCommand.RestoreArguments(recorded), control);

        Assert.Equal(RazerServiceCommand.Success, exitCode);
        Assert.Equal(ServiceStartMode.Automatic, control.Get("RazerExperienceService").StartMode);
        Assert.True(control.Get("RazerExperienceService").IsRunning);
        Assert.Equal(ServiceStartMode.Manual, control.Get("Razer Elevation Service").StartMode);
    }

    // ---- Malformed commands -----------------------------------------------

    [Theory]
    [InlineData("--razer-services")]                         // no verb
    [InlineData("--razer-services", "explode")]              // unknown verb
    [InlineData("--razer-services", "restore")]              // missing modes
    [InlineData("--razer-services", "restore", "%%%")]       // not base64
    [InlineData("--razer-services", "restore", "aGVsbG8=")]  // base64, but not JSON
    [InlineData("--razer-services", "stop", "extra")]        // stray argument
    public void TryRun_ReportsBadArguments_AndChangesNothing(params string[] args)
    {
        var control = Machine();

        var exitCode = RazerServiceCommand.TryRun(args, control);

        // A malformed service command must not fall through to starting the
        // whole app, and must not touch any service.
        Assert.Equal(RazerServiceCommand.BadArguments, exitCode);
        Assert.Empty(control.Calls);
    }

    // ---- Packing the startup types into one argument ----------------------

    [Fact]
    public void EncodeModes_ThenDecode_RoundTripsNamesWithSpacesAndEveryMode()
    {
        var modes = new Dictionary<string, ServiceStartMode>
        {
            ["Razer Chroma SDK Server"] = ServiceStartMode.Automatic,
            ["Razer Elevation Service"] = ServiceStartMode.Manual,
            ["RGS"] = ServiceStartMode.Boot,
            ["Odd \"quoted\" name"] = ServiceStartMode.System
        };

        var decoded = RazerServiceCommand.TryDecodeModes(RazerServiceCommand.EncodeModes(modes), out var result);

        Assert.True(decoded);
        Assert.Equal(modes, result);
    }

    [Fact]
    public void EncodeModes_ProducesASingleArgumentWithNoSpaces()
    {
        // The command line is split on spaces, so it must survive unquoted.
        var encoded = RazerServiceCommand.EncodeModes(new Dictionary<string, ServiceStartMode>
        {
            ["Razer Chroma SDK Server"] = ServiceStartMode.Automatic
        });

        Assert.DoesNotContain(' ', encoded);
    }

    [Fact]
    public void EncodeModes_OfNothing_RoundTripsToNothing()
    {
        Assert.True(RazerServiceCommand.TryDecodeModes(RazerServiceCommand.EncodeModes(new Dictionary<string, ServiceStartMode>()), out var result));
        Assert.Empty(result);
    }

    [Fact]
    public void TheArgumentHelpers_ProduceTheShapeTryRunExpects()
    {
        Assert.Equal(["--razer-services", "stop"], RazerServiceCommand.StopArguments());

        var restore = RazerServiceCommand.RestoreArguments(new Dictionary<string, ServiceStartMode>());
        Assert.Equal(3, restore.Length);
        Assert.Equal("--razer-services", restore[0]);
        Assert.Equal("restore", restore[1]);
    }
}
