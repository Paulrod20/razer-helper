using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class TemperatureTextTests
{
    [Fact]
    public void OnlyTheGpu_IsShownAlone() =>
        Assert.Equal("GPU: 62\u00B0C", TemperatureText.Format(null, 62.4));

    [Fact]
    public void BothReadings_AreShownSideBySide() =>
        Assert.Equal("CPU: 71\u00B0C  \u00B7  GPU: 62\u00B0C", TemperatureText.Format(71.0, 62.4));

    [Fact]
    public void OnlyTheCpu_IsShownAlone() =>
        Assert.Equal("CPU: 71\u00B0C", TemperatureText.Format(71.0, null));

    [Fact]
    public void NoReadings_ShowNothing_NotAPlaceholder() =>
        Assert.Equal(string.Empty, TemperatureText.Format(null, null));

    [Theory]
    [InlineData(62.4, "GPU: 62\u00B0C")]
    [InlineData(62.5, "GPU: 63\u00B0C")]
    [InlineData(62.6, "GPU: 63\u00B0C")]
    public void ShownInWholeDegrees(double celsius, string expected) =>
        Assert.Equal(expected, TemperatureText.Format(null, celsius));
}
