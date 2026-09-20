using RazerHelper.Core.Models;

namespace RazerHelper.Tests.Models;

public class LightingBrightnessTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 255)]
    [InlineData(50, 128)]
    [InlineData(25, 64)]
    public void PercentToByte(int percent, int expected) =>
        Assert.Equal((byte)expected, LightingBrightness.ToByte(percent));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(255, 100)]
    [InlineData(128, 50)]
    public void ByteToPercent(int value, int expected) =>
        Assert.Equal(expected, LightingBrightness.ToPercent((byte)value));

    [Fact]
    public void EveryWholePercent_SurvivesAWriteAndAReadBack()
    {
        // The slider must show the value the user picked after the laptop
        // stores it as a byte, or it would jump by a percent on every refresh.
        for (var percent = 0; percent <= 100; percent++)
            Assert.Equal(percent, LightingBrightness.ToPercent(LightingBrightness.ToByte(percent)));
    }

    [Fact]
    public void MoreBrightness_NeverMeansASmallerByte()
    {
        var previous = -1;

        for (var percent = 0; percent <= 100; percent++)
        {
            var value = LightingBrightness.ToByte(percent);
            Assert.True(value >= previous);
            previous = value;
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void OutOfRange_IsRefused(int percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LightingBrightness.ToByte(percent));
}
