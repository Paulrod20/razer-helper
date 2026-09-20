using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class UiScaleTests
{
    [Fact]
    public void At100PercentWindowsScaling_TheBaseSizeIsUnchanged() =>
        Assert.Equal(1F, UiScale.Automatic(1F));

    [Theory]
    [InlineData(1.25F, 1.05F)]
    [InlineData(1.5F, 1.15F)]
    [InlineData(2F, 1.25F)]
    [InlineData(2.25F, 1.3F)]   // The 4K Blade 16 panel at Windows' recommended scaling.
    [InlineData(3F, 1.5F)]
    public void HigherWindowsScaling_GrowsTheWindowAQuarterAsFast(float windowsScale, float expected) =>
        Assert.Equal(expected, UiScale.Automatic(windowsScale), precision: 3);

    [Fact]
    public void WindowsScalingBelow100Percent_NeverShrinksTheWindow() =>
        Assert.Equal(1F, UiScale.Automatic(0.5F));

    [Fact]
    public void ASavedSize_WinsOverTheAutomaticOne() =>
        Assert.Equal(1.5F, UiScale.Resolve(1.5, windowsScale: 2.25F));

    [Fact]
    public void WithNothingSaved_TheAutomaticSizeIsUsed() =>
        Assert.Equal(1.3F, UiScale.Resolve(null, windowsScale: 2.25F), precision: 3);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(-2.0)]
    [InlineData(10.0)]
    public void ASavedSizeThatIsNotSensible_IsIgnored(double saved) =>
        Assert.Equal(1.3F, UiScale.Resolve(saved, windowsScale: 2.25F), precision: 3);

    [Theory]
    [InlineData(0.75)]
    [InlineData(1.0)]
    [InlineData(3.0)]
    public void TheEdgesOfTheAllowedRange_AreAccepted(double saved) =>
        Assert.Equal((float)saved, UiScale.Resolve(saved, windowsScale: 2.25F));
}
