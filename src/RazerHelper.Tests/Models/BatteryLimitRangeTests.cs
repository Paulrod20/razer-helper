using RazerHelper.Core.Models;

namespace RazerHelper.Tests.Models;

public class BatteryLimitRangeTests
{
    [Fact]
    public void TheOfferedRangeIsSixtyToOneHundredInStepsOfTwenty()
    {
        Assert.Equal(60, BatteryLimitRange.Minimum);
        Assert.Equal(100, BatteryLimitRange.Maximum);
        Assert.Equal(20, BatteryLimitRange.Step);
    }

    [Fact]
    public void OneHundredPercentMeansNoLimit()
    {
        Assert.Equal(100, BatteryLimitRange.NoLimit);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(80)]
    [InlineData(100)]
    public void IsValid_AcceptsTheOfferedSteps(int percent)
    {
        Assert.True(BatteryLimitRange.IsValid(percent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    [InlineData(50)]   // the EC accepts it, but this range does not offer it
    [InlineData(59)]
    [InlineData(61)]
    [InlineData(70)]
    [InlineData(90)]
    [InlineData(101)]
    [InlineData(120)]
    public void IsValid_RejectsAnythingElse(int percent)
    {
        Assert.False(BatteryLimitRange.IsValid(percent));
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(69, 60)]
    [InlineData(70, 80)]    // halfway rounds up
    [InlineData(79, 80)]
    [InlineData(80, 80)]
    [InlineData(89, 80)]
    [InlineData(90, 100)]   // halfway rounds up
    [InlineData(100, 100)]
    public void Normalize_SnapsToTheNearestStep(int input, int expected)
    {
        Assert.Equal(expected, BatteryLimitRange.Normalize(input));
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-5, 60)]
    [InlineData(500, 100)]
    public void Normalize_ClampsOutOfRangeValues(int input, int expected)
    {
        Assert.Equal(expected, BatteryLimitRange.Normalize(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    [InlineData(74)]
    [InlineData(1000)]
    public void Normalize_AlwaysProducesAValidLimit(int input)
    {
        Assert.True(BatteryLimitRange.IsValid(BatteryLimitRange.Normalize(input)));
    }
}
