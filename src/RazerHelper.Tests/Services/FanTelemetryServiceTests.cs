using System.ComponentModel;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class FanTelemetryServiceTests
{
    private static (FakeEc Ec, FanTelemetryService Service) Create()
    {
        var ec = new FakeEc();
        return (ec, new FanTelemetryService(ec));
    }

    private static void SetFans(FakeEc ec, byte cpuHundreds, byte gpuHundreds)
    {
        ec.FanRpmHundreds[0] = cpuHundreds;
        ec.FanRpmHundreds[1] = gpuHundreds;
    }

    [Fact]
    public async Task ReadAsync_ConvertsTheEcsHundredsOfRpmIntoRpm()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);

        Assert.Equal(new FanRpmReading(2300, 2100), await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_WithOnlyOneFanStopped_ReturnsThePreviousReading()
    {
        // One fan reading zero while the other spins is the EC glitching, not
        // the laptop, so it must not flash "0 RPM" in the UI.
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        var good = await service.ReadAsync();

        SetFans(ec, 0, 21);

        Assert.Equal(good, await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_WithOnlyOneFanStoppedAndNothingBefore_ReturnsNull()
    {
        var (ec, service) = Create();
        SetFans(ec, 0, 21);

        Assert.Null(await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_BothFansStopped_NeedsTwoReadingsInARowToBelieveIt()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        var running = await service.ReadAsync();

        SetFans(ec, 0, 0);
        var firstStopped = await service.ReadAsync();
        var secondStopped = await service.ReadAsync();

        Assert.Equal(running, firstStopped);                      // not yet believed
        Assert.Equal(new FanRpmReading(0, 0), secondStopped);     // confirmed
    }

    [Fact]
    public async Task ReadAsync_AStoppedReadingBetweenRunningOnesIsNotConfirmed()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        await service.ReadAsync();

        SetFans(ec, 0, 0);
        await service.ReadAsync();               // pending

        SetFans(ec, 24, 22);
        var running = await service.ReadAsync(); // running again resets the pending stop

        SetFans(ec, 0, 0);
        var stoppedOnce = await service.ReadAsync();

        Assert.Equal(new FanRpmReading(2400, 2200), running);
        Assert.Equal(running, stoppedOnce);      // a single stop is still not believed
    }

    [Fact]
    public async Task ReadAsync_WhenTheEcIsUnreachable_ReturnsTheLastReadingInsteadOfThrowing()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        var good = await service.ReadAsync();

        ec.BeforeSend = (_, _) => throw new InvalidOperationException("The EC went away.");

        Assert.Equal(good, await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_WhenTheEcIsUnreachableAndNothingWasReadYet_ReturnsNull()
    {
        var (ec, service) = Create();
        ec.BeforeSend = (_, _) => throw new InvalidOperationException("The EC went away.");

        Assert.Null(await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TreatsAWindowsDeviceErrorTheSameWay()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        var good = await service.ReadAsync();

        ec.BeforeSend = (_, _) => throw new Win32Exception(31, "A device attached to the system is not functioning.");

        Assert.Equal(good, await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_RecoversOnceTheEcComesBack()
    {
        var (ec, service) = Create();
        SetFans(ec, 23, 21);
        await service.ReadAsync();
        ec.BeforeSend = (_, _) => throw new InvalidOperationException("The EC went away.");
        await service.ReadAsync();

        ec.BeforeSend = null;
        SetFans(ec, 25, 24);

        Assert.Equal(new FanRpmReading(2500, 2400), await service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_DoesNotHideAnUnexpectedKindOfFailure()
    {
        // Only "the device is unavailable" errors are expected; anything else
        // is a bug and should surface rather than be silently swallowed.
        var (ec, service) = Create();
        ec.BeforeSend = (_, _) => throw new NotSupportedException("A bug, not a flaky device.");

        await Assert.ThrowsAsync<NotSupportedException>(() => service.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_TreatsAnAnswerForTheWrongFanAsAnUnavailableDevice()
    {
        var service = new FanTelemetryService(new ScriptedTransport((_, _) => FakeEc.Respond(0x00, 9, 23)));

        Assert.Null(await service.ReadAsync());
    }
}
