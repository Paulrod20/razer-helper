using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class EcTemperatureServiceTests
{
    private static (EcTemperatureService Service, FakeEc Ec) Create()
    {
        var ec = new FakeEc();
        return (new EcTemperatureService(ec), ec);
    }

    [Fact]
    public void ReadsBothTemperaturesFromTheOneRegister()
    {
        var (service, ec) = Create();
        ec.CpuTemperature = 55;
        ec.GpuTemperature = 39;

        Assert.Equal(new EcTemperatures(55, 39), service.Read());
    }

    [Fact]
    public void AsACpuSource_ItReportsTheCpuSide()
    {
        var (service, ec) = Create();
        ec.CpuTemperature = 48;

        Assert.Equal(48.0, ((ICpuTemperatureSource)service).ReadCelsius());
    }

    [Fact]
    public void ItSendsExactlyOneReadCommand_AndNothingElse_EachTime()
    {
        var (service, ec) = Create();

        service.Read();

        var sent = Assert.Single(ec.Log);
        Assert.Equal(RazerCommands.GetTemperatures, sent.Command);
        Assert.Equal([0x00], sent.Arguments);
    }

    [Fact]
    public void TheCommandIsARead_NotAWrite()
    {
        // The protocol marks a read with the top bit of the low byte. This is
        // what makes it impossible for the temperature poll to change anything.
        Assert.Equal(0x80, RazerCommands.GetTemperatures & 0x0080);
        Assert.Empty(new FakeEc().Writes);

        var (service, ec) = Create();
        service.Read();
        Assert.Empty(ec.Writes);
    }

    [Theory]
    [InlineData(0)]     // A missing sensor.
    [InlineData(255)]   // A failed sensor.
    [InlineData(9)]     // Below anything real.
    [InlineData(126)]   // Above anything real.
    public void AByteThatIsNotBelievable_IsNoReading_NotADegreeValue(int raw) =>
        Assert.Null(EcTemperatureService.Believable((byte)raw));

    [Theory]
    [InlineData(10)]
    [InlineData(45)]
    [InlineData(125)]
    public void APlausibleByte_IsShownAsIs(int raw) =>
        Assert.Equal(raw, EcTemperatureService.Believable((byte)raw));

    [Fact]
    public void OneBadByte_DoesNotHideTheOther()
    {
        var (service, ec) = Create();
        ec.CpuTemperature = 0;
        ec.GpuTemperature = 40;

        Assert.Equal(new EcTemperatures(null, 40), service.Read());
    }

    [Fact]
    public void OnAModelWithoutTheRegister_ItSaysNothing_AndStopsAsking()
    {
        var (service, ec) = Create();
        ec.TemperaturesUnsupported = true;

        Assert.Equal(new EcTemperatures(null, null), service.Read());
        Assert.Equal(new EcTemperatures(null, null), service.Read());
        Assert.Equal(new EcTemperatures(null, null), service.Read());

        Assert.Single(ec.Log); // Asked once, told "not supported", never asked again.
    }

    [Fact]
    public void AnUnsupportedRegister_IsNotAnError_SoNoExceptionEscapes()
    {
        var (service, ec) = Create();
        ec.TemperaturesUnsupported = true;

        var exception = Record.Exception(() => service.ReadCelsius());

        Assert.Null(exception);
    }

    [Fact]
    public void ADeviceFault_IsNotSwallowed_SoTheCallerCanContainIt()
    {
        var (service, ec) = Create();
        ec.BeforeSend = (_, _) => throw new InvalidOperationException("EC not responding");

        Assert.Throws<InvalidOperationException>(() => service.Read());
    }

    [Fact]
    public void NoCpuSource_ReportsNothing() =>
        Assert.Null(new NoCpuTemperature().ReadCelsius());
}

// Reusing a reading for a few seconds: the sensor is slow, so asking the controller every poll is wasted traffic.
public class EcTemperatureCacheTests
{
    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    private static (EcTemperatureService Service, FakeEc Ec, Clock Clock) Create(TimeSpan? cacheFor = null)
    {
        var ec = new FakeEc();
        var clock = new Clock();
        return (new EcTemperatureService(ec, cacheFor, () => clock.Now), ec, clock);
    }

    [Fact]
    public void TwoReadsInsideTheWindow_AskTheControllerOnce()
    {
        var (service, ec, clock) = Create();

        service.Read();
        clock.Advance(2);
        service.Read();

        Assert.Single(ec.Log);
    }

    [Fact]
    public void AReadAfterTheWindow_AsksAgain_AndShowsTheNewValue()
    {
        var (service, ec, clock) = Create();
        ec.CpuTemperature = 50;
        service.Read();

        ec.CpuTemperature = 54;
        clock.Advance(4.5);

        Assert.Equal(54, service.Read().CpuCelsius);
        Assert.Equal(2, ec.Log.Count);
    }

    [Fact]
    public void WithinTheWindow_TheEarlierValueIsReturned_NotAFreshOne()
    {
        var (service, ec, clock) = Create();
        ec.CpuTemperature = 50;
        service.Read();

        ec.CpuTemperature = 60;
        clock.Advance(2);

        Assert.Equal(50, service.Read().CpuCelsius);
    }

    [Fact]
    public void SteadyPolling_EveryTwoSeconds_HalvesTheControllerReads()
    {
        // The popup polls every 2 s. With the 4 s window that is one controller read per two polls.
        var (service, ec, clock) = Create();

        for (var poll = 0; poll < 30; poll++)
        {
            service.Read();
            clock.Advance(2);
        }

        Assert.Equal(15, ec.Log.Count);
    }

    [Fact]
    public void AZeroWindow_ReadsEveryTime()
    {
        var (service, ec, clock) = Create(TimeSpan.Zero);

        service.Read();
        service.Read();
        clock.Advance(0.1);
        service.Read();

        Assert.Equal(3, ec.Log.Count);
    }

    [Fact]
    public void AFailedRead_IsNotCached_SoTheNextCallTriesAgain()
    {
        var (service, ec, clock) = Create();
        var fail = true;
        ec.BeforeSend = (_, _) =>
        {
            if (fail)
                throw new InvalidOperationException("EC busy");
        };

        Assert.Throws<InvalidOperationException>(() => service.Read());

        fail = false;
        clock.Advance(0.5);

        Assert.Equal(52, service.Read().CpuCelsius); // Tried again straight away, inside the window.
    }

    [Fact]
    public void TheDefaultWindow_IsFourSeconds() =>
        Assert.Equal(TimeSpan.FromSeconds(4), EcTemperatureService.DefaultCacheDuration);
}
