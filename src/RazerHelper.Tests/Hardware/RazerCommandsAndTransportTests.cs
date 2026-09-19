using RazerHelper.Core.Hardware;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Hardware;

public class RazerCommandsTests
{
    [Theory]
    [InlineData(RazerCommands.SetPerformanceMode, 0x0D02)]
    [InlineData(RazerCommands.GetPerformanceMode, 0x0D82)]
    [InlineData(RazerCommands.SetBoost, 0x0D07)]
    [InlineData(RazerCommands.GetBoost, 0x0D87)]
    [InlineData(RazerCommands.GetActualFanRpm, 0x0D88)]
    [InlineData(RazerCommands.SetBatteryChargeLimit, 0x0712)]
    public void CommandIds_MatchTheProtocolVerifiedOnTheBlade16(ushort actual, ushort expected)
    {
        // A typo here would send the wrong command to the hardware, so the
        // values are pinned to the ids documented by the reverse-engineering
        // work and confirmed on a Blade 16 (2023).
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheGetCommandsMirrorTheirSetCommandsWithTheHighBitSetOnTheLowByte()
    {
        Assert.Equal(RazerCommands.SetPerformanceMode | 0x0080, RazerCommands.GetPerformanceMode);
        Assert.Equal(RazerCommands.SetBoost | 0x0080, RazerCommands.GetBoost);
    }
}

public class RazerTransportExtensionsTests
{
    [Fact]
    public void SendAndConfirm_ReturnsTheResponseWhenTheEcEchoesTheArguments()
    {
        var ec = new FakeEc();

        var response = ec.SendAndConfirm(RazerCommands.SetBatteryChargeLimit, [0xD0], "the limit");

        Assert.Equal(0xD0, RazerHidPacket.GetArgument(response, 0));
        Assert.Equal(0xD0, ec.BatteryLimitByte);
    }

    [Fact]
    public void SendAndConfirm_ThrowsWhenTheEcEchoesDifferentArguments()
    {
        var ec = new FakeEc { EchoOverride = _ => [0x50] };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ec.SendAndConfirm(RazerCommands.SetBatteryChargeLimit, [0xD0], "the battery limit"));

        Assert.Contains("did not confirm the battery limit", exception.Message);
    }

    [Fact]
    public void SendAndConfirm_ChecksEveryArgumentNotJustTheFirst()
    {
        var ec = new FakeEc
        {
            // Right first bytes, wrong last one.
            EchoOverride = args => [args[0], args[1], args[2], 0xFF]
        };

        Assert.Throws<InvalidOperationException>(() =>
            ec.SendAndConfirm(RazerCommands.SetPerformanceMode, [1, 1, 5, 0], "the mode"));
    }
}
