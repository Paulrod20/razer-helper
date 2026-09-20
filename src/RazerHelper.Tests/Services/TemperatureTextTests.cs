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

public class TemperatureExplanationTests
{
    [Fact]
    public void TheCpuNote_SaysItComesFromTheControllerAndUpdatesMoreSlowlyThanOtherTools()
    {
        var text = TemperatureText.Explain(55, null);

        Assert.Contains("laptop's controller", text);
        Assert.Contains("closely follows the CPU", text);
        Assert.Contains("updates more", text);
        Assert.Contains("slowly than tools like Afterburner", text);
    }

    [Fact]
    public void TheGpuNote_SaysItComesFromTheGraphicsDriver()
    {
        var text = TemperatureText.Explain(null, 41);

        Assert.Contains("graphics driver", text);
        Assert.DoesNotContain("CPU", text);
    }

    [Fact]
    public void BothNotes_AreShownWhenBothTemperaturesAre()
    {
        var text = TemperatureText.Explain(55, 41);

        Assert.Contains("CPU:", text);
        Assert.Contains("GPU:", text);
    }

    [Fact]
    public void WithNothingShown_ThereIsNothingToExplain() =>
        Assert.Equal(string.Empty, TemperatureText.Explain(null, null));

    [Fact]
    public void TheNoteIsSeveralShortLines_SoTheTooltipStaysSmall()
    {
        var lines = TemperatureText.Explain(55, 41).Split('\n');

        Assert.All(lines, line => Assert.True(line.Length <= 46, $"Too long for a small tooltip: {line}"));
    }

    [Fact]
    public void TheCombinedText_ForBothTemperatures_MatchesWhatTheHeaderShows() =>
        Assert.Equal("CPU: 55°C  ·  GPU: 41°C", TemperatureText.Format(55.2, 41.4));
}
