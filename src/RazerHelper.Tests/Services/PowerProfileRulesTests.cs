using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class PowerProfileRulesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public void PowerAndUnknownBothCountAsPluggedIn(bool? isPluggedIn)
    {
        Assert.True(PowerProfileRules.TreatAsPluggedIn(isPluggedIn));
    }

    [Fact]
    public void OnlyAnExplicitOfflineCountsAsBattery()
    {
        Assert.False(PowerProfileRules.TreatAsPluggedIn(false));
    }

    [Theory]
    [InlineData((byte)PerformanceMode.Balanced, true, true)]
    [InlineData((byte)PerformanceMode.Silent, true, true)]
    [InlineData((byte)PerformanceMode.Custom, true, true)]
    [InlineData((byte)PerformanceMode.Balanced, false, true)]
    [InlineData((byte)PerformanceMode.Silent, false, false)]
    [InlineData((byte)PerformanceMode.Custom, false, false)]
    public void IsModeAllowed_OffersEverythingOnPowerAndOnlyBalancedOnBattery(
        byte modeByte, bool pluggedIn, bool expected)
    {
        Assert.Equal(expected, PowerProfileRules.IsModeAllowed((PerformanceMode)modeByte, pluggedIn));
    }

    [Theory]
    [InlineData((byte)PerformanceMode.Custom, true, true)]
    [InlineData((byte)PerformanceMode.Custom, false, false)]   // Custom is not offered on battery
    [InlineData((byte)PerformanceMode.Balanced, true, false)]  // boost levels belong to Custom
    [InlineData((byte)PerformanceMode.Silent, true, false)]
    public void CanChangeBoost_NeedsCustomModeAndACPower(byte modeByte, bool pluggedIn, bool expected)
    {
        var state = new PerformanceState((PerformanceMode)modeByte, CpuBoost.Medium, GpuBoost.Low);

        Assert.Equal(expected, PowerProfileRules.CanChangeBoost(state, pluggedIn));
    }

    [Theory]
    [InlineData((byte)PerformanceMode.Custom, true, true)]
    [InlineData((byte)PerformanceMode.Custom, false, false)]   // Custom is not offered on battery
    [InlineData((byte)PerformanceMode.Balanced, true, false)]  // the EC rejects max fan outside Custom
    [InlineData((byte)PerformanceMode.Silent, true, false)]
    [InlineData((byte)PerformanceMode.Balanced, false, false)]
    public void CanUseMaxFan_NeedsCustomModeAndACPower(byte modeByte, bool pluggedIn, bool expected)
    {
        var state = new PerformanceState((PerformanceMode)modeByte, CpuBoost.Boost, GpuBoost.High);

        Assert.Equal(expected, PowerProfileRules.CanUseMaxFan(state, pluggedIn));
    }

    [Fact]
    public void CanUseMaxFan_IsFalseWhenTheModeIsUnknown()
    {
        Assert.False(PowerProfileRules.CanUseMaxFan(PerformanceState.Unknown, pluggedIn: true));
    }

    [Fact]
    public void CanUseMaxFan_DoesNotYetRequireCpuBoostAndGpuHigh()
    {
        // Synapse also wants CPU Boost and GPU High before it offers Max. That
        // rule is deliberately not applied here yet; this test records the
        // current decision so changing it is a visible, intentional edit.
        var state = new PerformanceState(PerformanceMode.Custom, CpuBoost.Low, GpuBoost.Low);

        Assert.True(PowerProfileRules.CanUseMaxFan(state, pluggedIn: true));
    }

    [Fact]
    public void CanChangeBoost_IsFalseWhenTheModeIsUnknown()
    {
        Assert.False(PowerProfileRules.CanChangeBoost(PerformanceState.Unknown, pluggedIn: true));
    }

    [Theory]
    [InlineData((byte)PerformanceMode.Silent)]
    [InlineData((byte)PerformanceMode.Custom)]
    public void Sanitize_TurnsAStaleBatteryModeIntoBalanced(byte staleModeByte)
    {
        var stored = new PowerProfile((PerformanceMode)staleModeByte, CpuBoost.High, GpuBoost.Medium);

        var safe = PowerProfileRules.Sanitize(stored, pluggedIn: false);

        Assert.Equal(PerformanceMode.Balanced, safe.Mode);
        // Only the mode is corrected; the remembered boost levels stay put.
        Assert.Equal(CpuBoost.High, safe.Cpu);
        Assert.Equal(GpuBoost.Medium, safe.Gpu);
    }

    [Fact]
    public void Sanitize_LeavesAValidBatteryProfileAlone()
    {
        var stored = new PowerProfile(PerformanceMode.Balanced);

        Assert.Same(stored, PowerProfileRules.Sanitize(stored, pluggedIn: false));
    }

    [Fact]
    public void Sanitize_LeavesAProfileWithNoModeAlone()
    {
        var stored = new PowerProfile();

        Assert.Same(stored, PowerProfileRules.Sanitize(stored, pluggedIn: false));
    }

    [Theory]
    [InlineData((byte)PerformanceMode.Balanced)]
    [InlineData((byte)PerformanceMode.Silent)]
    [InlineData((byte)PerformanceMode.Custom)]
    public void Sanitize_NeverRestrictsAProfileOnPower(byte modeByte)
    {
        var stored = new PowerProfile((PerformanceMode)modeByte, CpuBoost.Boost, GpuBoost.High);

        Assert.Same(stored, PowerProfileRules.Sanitize(stored, pluggedIn: true));
    }

    [Fact]
    public void Remember_RecordsTheBoostLevelsTheEcReportsInCustom()
    {
        var requested = new PowerProfile(PerformanceMode.Custom);
        var settled = new PerformanceState(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Low);

        Assert.Equal(
            new PowerProfile(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Low),
            PowerProfileRules.Remember(requested, settled));
    }

    [Fact]
    public void Remember_KeepsEarlierBoostLevelsOutsideCustom()
    {
        // The EC reports no boost levels outside Custom; the stored ones must
        // survive so switching back to Custom returns the user's settings.
        var requested = new PowerProfile(PerformanceMode.Silent, CpuBoost.High, GpuBoost.Medium);
        var settled = new PerformanceState(PerformanceMode.Silent, null, null);

        Assert.Equal(requested, PowerProfileRules.Remember(requested, settled));
    }

    [Fact]
    public void Remember_RecordsTheModeTheEcActuallyEndedUpIn()
    {
        var requested = new PowerProfile(PerformanceMode.Custom);
        var settled = new PerformanceState(PerformanceMode.Balanced, null, null);

        Assert.Equal(PerformanceMode.Balanced, PowerProfileRules.Remember(requested, settled).Mode);
    }

    [Fact]
    public void Remember_KeepsTheRequestedModeWhenTheEcModeIsUnreadable()
    {
        var requested = new PowerProfile(PerformanceMode.Silent);

        Assert.Equal(PerformanceMode.Silent, PowerProfileRules.Remember(requested, PerformanceState.Unknown).Mode);
    }

    [Fact]
    public void TheDefaultBatteryProfileIsBalancedWithNoBoostLevels()
    {
        Assert.Equal(new PowerProfile(PerformanceMode.Balanced), PowerProfile.DefaultOnBattery);
    }
}
