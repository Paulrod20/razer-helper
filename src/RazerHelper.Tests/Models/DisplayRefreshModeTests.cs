using RazerHelper.Core.Models;

namespace RazerHelper.Tests.Models;

public class DisplayRefreshModeTests
{
    [Fact]
    public void OfferedModes_AreSixtyOneTwentyAndAutoInThatOrder()
    {
        Assert.Equal(
            [DisplayRefreshMode.Fixed(60), DisplayRefreshMode.Fixed(120), DisplayRefreshMode.Auto],
            DisplayRefreshMode.Offered);
    }

    [Theory]
    [InlineData(true, 120)]
    [InlineData(false, 60)]
    public void Auto_PicksTheFastRateOnPowerAndTheEfficientRateOnBattery(bool pluggedIn, int expectedHz)
    {
        Assert.Equal(expectedHz, DisplayRefreshMode.Auto.TargetHz(pluggedIn));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFixedMode_IgnoresThePowerSource(bool pluggedIn)
    {
        Assert.Equal(60, DisplayRefreshMode.Fixed(60).TargetHz(pluggedIn));
        Assert.Equal(120, DisplayRefreshMode.Fixed(120).TargetHz(pluggedIn));
    }

    [Fact]
    public void IsAuto_IsTrueOnlyForAuto()
    {
        Assert.True(DisplayRefreshMode.Auto.IsAuto);
        Assert.False(DisplayRefreshMode.Fixed(60).IsAuto);
    }

    [Theory]
    [InlineData(60, "60 Hz")]
    [InlineData(120, "120 Hz")]
    public void Label_IsTheRateWithAUnit(int hertz, string expected)
    {
        Assert.Equal(expected, DisplayRefreshMode.Fixed(hertz).Label);
    }

    [Fact]
    public void Label_OfAutoIsAuto()
    {
        Assert.Equal("Auto", DisplayRefreshMode.Auto.Label);
    }

    [Fact]
    public void EveryOfferedMode_SurvivesAStoreAndReloadOfItsLabel()
    {
        // The label is what lands in settings.json, so it must parse back.
        Assert.All(DisplayRefreshMode.Offered, mode =>
            Assert.Equal(mode, DisplayRefreshMode.Parse(mode.Label)));
    }

    [Fact]
    public void Parse_ReadsAnyPositiveRate_NotOnlyTheOfferedOnes()
    {
        // Settings written by a build that offered other rates should still load.
        Assert.Equal(DisplayRefreshMode.Fixed(144), DisplayRefreshMode.Parse("144 Hz"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fast")]
    [InlineData("auto")]      // case-sensitive: settings files are written by us
    [InlineData("60Hz")]      // missing space
    [InlineData("0 Hz")]
    [InlineData("-60 Hz")]
    [InlineData("abc Hz")]
    [InlineData(" 60 Hz")]      // leading space
    [InlineData("+60 Hz")]      // sign
    [InlineData("60 Hz ")]      // trailing space
    public void Parse_ReturnsNullForAnythingUnrecognised(string? label)
    {
        Assert.Null(DisplayRefreshMode.Parse(label));
    }

    [Fact]
    public void ModesWithTheSameRateAreEqual()
    {
        Assert.Equal(DisplayRefreshMode.Fixed(60), DisplayRefreshMode.Fixed(60));
        Assert.NotEqual(DisplayRefreshMode.Fixed(60), DisplayRefreshMode.Fixed(120));
        Assert.NotEqual(DisplayRefreshMode.Fixed(120), DisplayRefreshMode.Auto);
    }
}
