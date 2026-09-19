using RazerHelper.Core.Hardware;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class BatteryChargeLimitServiceTests
{
    [Theory]
    [InlineData(60, 0xBC)]    // 0x80 | 60
    [InlineData(80, 0xD0)]    // matches the byte in Synapse's own capture for an 80% limit
    [InlineData(100, 0x50)]   // no limit: bit 7 clear
    public void ToWireValue_EncodesTheLimitTheWayTheEcExpects(int percent, byte expected)
    {
        Assert.Equal(expected, BatteryChargeLimitService.ToWireValue(percent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50)]
    [InlineData(59)]
    [InlineData(61)]
    [InlineData(70)]
    [InlineData(90)]
    [InlineData(101)]
    public void ToWireValue_RejectsALimitTheAppDoesNotOffer(int percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BatteryChargeLimitService.ToWireValue(percent));
    }

    [Fact]
    public async Task SetChargeLimitAsync_SendsOneWriteWithTheEncodedValue()
    {
        var ec = new FakeEc();
        var service = new BatteryChargeLimitService(ec);

        await service.SetChargeLimitAsync(80);

        var write = Assert.Single(ec.Log);
        Assert.Equal(RazerCommands.SetBatteryChargeLimit, write.Command);
        Assert.Equal(new byte[] { 0xD0 }, write.Arguments);
        Assert.Equal(0xD0, ec.BatteryLimitByte);
    }

    [Fact]
    public async Task SetChargeLimitAsync_TurnsTheLimitOffWithAHundredPercent()
    {
        var ec = new FakeEc { BatteryLimitByte = 0xD0 };
        var service = new BatteryChargeLimitService(ec);

        await service.SetChargeLimitAsync(100);

        Assert.Equal(0x50, ec.BatteryLimitByte);
    }

    [Fact]
    public async Task SetChargeLimitAsync_SendsNothingForAnInvalidLimit()
    {
        var ec = new FakeEc();
        var service = new BatteryChargeLimitService(ec);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetChargeLimitAsync(70));

        Assert.Empty(ec.Log);
    }

    [Fact]
    public async Task SetChargeLimitAsync_ThrowsWhenTheEcDoesNotConfirmTheLimit()
    {
        var ec = new FakeEc { EchoOverride = _ => [0x50] };
        var service = new BatteryChargeLimitService(ec);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetChargeLimitAsync(80));

        Assert.Contains("did not confirm", exception.Message);
    }
}
