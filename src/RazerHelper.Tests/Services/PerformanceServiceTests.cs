using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class PerformanceServiceTests
{
    // The EC's wire values (see PerformanceMode, CpuBoost, GpuBoost).
    private const byte Balanced = 0;
    private const byte Custom = 4;
    private const byte Silent = 5;

    private static (FakeEc Ec, PerformanceService Service) Create(byte mode = Balanced, byte cpu = 1, byte gpu = 0)
    {
        var ec = new FakeEc { CpuBoost = cpu, GpuBoost = gpu };
        ec.SetBothZones(mode);
        return (ec, new PerformanceService(ec));
    }

    private static string Describe(FakeEc.Sent sent) =>
        $"{sent.Command:X4}:{Convert.ToHexString(sent.Arguments)}";

    private static IEnumerable<FakeEc.Sent> BoostWrites(FakeEc ec) =>
        ec.Writes.Where(write => write.Command == RazerCommands.SetBoost);

    // ---- ReadState -------------------------------------------------------

    [Fact]
    public void ReadState_InBalanced_ReportsTheModeAndDoesNotReadBoostLevels()
    {
        var (ec, service) = Create(Balanced);

        var state = service.ReadState();

        Assert.Equal(new PerformanceState(PerformanceMode.Balanced, null, null), state);
        Assert.DoesNotContain(ec.Log, sent => sent.Command == RazerCommands.GetBoost);
    }

    [Fact]
    public void ReadState_InSilent_ReportsSilent()
    {
        var (_, service) = Create(Silent);

        Assert.Equal(PerformanceMode.Silent, service.ReadState().Mode);
    }

    [Fact]
    public void ReadState_InCustom_AlsoReadsTheBoostLevels()
    {
        var (_, service) = Create(Custom, cpu: 2, gpu: 1);

        Assert.Equal(
            new PerformanceState(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Medium),
            service.ReadState());
    }

    [Theory]
    [InlineData(2)]   // Performance: real to the EC, not offered on this model
    [InlineData(6)]   // Battery
    [InlineData(7)]   // Hyperboost
    [InlineData(200)]
    public void ReadState_ReportsNoModeForAValueThisAppDoesNotOffer(byte wireMode)
    {
        var (_, service) = Create(wireMode);

        Assert.Null(service.ReadState().Mode);
    }

    [Fact]
    public void ReadState_ReportsNoLevelForABoostValueThisAppDoesNotOffer()
    {
        // 4 is the CPU overclock value, 3 is beyond the GPU's range.
        var (_, service) = Create(Custom, cpu: 4, gpu: 3);

        var state = service.ReadState();

        Assert.Equal(PerformanceMode.Custom, state.Mode);
        Assert.Null(state.Cpu);
        Assert.Null(state.Gpu);
    }

    [Fact]
    public void ReadState_RereadsWhenTheZonesBrieflyDisagree_AndSettlesOnTheNewMode()
    {
        // A mode change (Fn+P, another tool) can land between the two zone
        // reads. The disagreement is transient, so the service asks again.
        var (ec, service) = Create();
        ec.ZoneMode[0] = Balanced;
        ec.ZoneMode[1] = Custom;

        var modeReads = 0;
        ec.BeforeSend = (fake, command) =>
        {
            if (command == RazerCommands.GetPerformanceMode && ++modeReads == 3)
                fake.SetBothZones(Custom);
        };

        Assert.Equal(PerformanceMode.Custom, service.ReadState().Mode);
    }

    [Fact]
    public void ReadState_ReportsNoModeWhenTheZonesKeepDisagreeing()
    {
        var (ec, service) = Create();
        ec.ZoneMode[0] = Balanced;
        ec.ZoneMode[1] = Custom;

        var state = service.ReadState();

        Assert.Null(state.Mode);
        Assert.DoesNotContain(ec.Log, sent => sent.Command == RazerCommands.GetBoost);
    }

    // ---- ApplyProfile: nothing to do ------------------------------------

    [Fact]
    public void ApplyProfile_WithAProfileTheEcAlreadyMatches_WritesNothing()
    {
        var (ec, service) = Create(Custom, cpu: 1, gpu: 0);

        var state = service.ApplyProfile(new PowerProfile(PerformanceMode.Custom, CpuBoost.Medium, GpuBoost.Low));

        Assert.Empty(ec.Writes);
        Assert.Equal(new PerformanceState(PerformanceMode.Custom, CpuBoost.Medium, GpuBoost.Low), state);
    }

    [Fact]
    public void ApplyProfile_WithAnEmptyProfile_LeavesTheEcAlone()
    {
        var (ec, service) = Create(Silent);

        var state = service.ApplyProfile(new PowerProfile());

        Assert.Empty(ec.Writes);
        Assert.Equal(PerformanceMode.Silent, state.Mode);
    }

    // ---- ApplyProfile: mode changes -------------------------------------

    [Fact]
    public void ApplyProfile_ChangingTheMode_WritesBothZonesWithTheFanOnAuto()
    {
        var (ec, service) = Create(Balanced);

        service.ApplyProfile(new PowerProfile(PerformanceMode.Silent));

        Assert.Equal(
            ["0D02:01010500", "0D02:01020500"],   // [enable, zone, mode, fan=auto]
            ec.Writes.Select(Describe));
    }

    [Fact]
    public void ApplyProfile_ReturnsWhatTheEcReportsAfterTheChange()
    {
        var (ec, service) = Create(Balanced);

        var state = service.ApplyProfile(new PowerProfile(PerformanceMode.Silent));

        Assert.Equal(PerformanceMode.Silent, state.Mode);
        Assert.Equal(new byte[] { Silent, Silent }, ec.ZoneMode);
    }

    [Fact]
    public void ApplyProfile_LeavingCustom_DoesNotTouchTheBoostLevelsTheEcKeeps()
    {
        var (ec, service) = Create(Custom, cpu: 2, gpu: 1);

        service.ApplyProfile(new PowerProfile(PerformanceMode.Balanced));

        Assert.Empty(BoostWrites(ec));
        Assert.Equal(2, ec.CpuBoost);
        Assert.Equal(1, ec.GpuBoost);
    }

    // ---- ApplyProfile: boost levels -------------------------------------

    [Fact]
    public void ApplyProfile_IntoCustom_WritesTheModeFirstAndThenTheBoostLevels()
    {
        var (ec, service) = Create(Balanced, cpu: 0, gpu: 0);

        service.ApplyProfile(new PowerProfile(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Medium));

        Assert.Equal(
            ["0D02:01010400", "0D02:01020400", "0D07:010102", "0D07:010201"],
            ec.Writes.Select(Describe));
    }

    [Fact]
    public void ApplyProfile_InCustom_WritesOnlyTheBoostLevelThatDiffers()
    {
        var (ec, service) = Create(Custom, cpu: 1, gpu: 0);

        service.ApplyProfile(new PowerProfile(PerformanceMode.Custom, CpuBoost.Medium, GpuBoost.High));

        Assert.Equal(["0D07:010202"], ec.Writes.Select(Describe));
    }

    [Fact]
    public void ApplyProfile_WithOnlyBoostLevels_ChangesThemInCustomWithoutWritingTheMode()
    {
        var (ec, service) = Create(Custom, cpu: 1, gpu: 0);

        service.ApplyProfile(new PowerProfile(Cpu: CpuBoost.Boost));

        Assert.Equal(["0D07:010103"], ec.Writes.Select(Describe));
    }

    [Fact]
    public void ApplyProfile_DoesNotWriteBoostLevelsOutsideCustom()
    {
        // The EC drops or misapplies boost writes outside Custom, so a
        // profile that mentions boosts must not send them from another mode.
        var (ec, service) = Create(Balanced);

        service.ApplyProfile(new PowerProfile(PerformanceMode.Silent, CpuBoost.Boost, GpuBoost.High));

        Assert.Empty(BoostWrites(ec));
    }

    [Fact]
    public void ApplyProfile_DoesNotWriteBoostLevelsWhenTheEcIgnoresTheSwitchToCustom()
    {
        var (ec, service) = Create(Balanced);
        ec.IgnoreModeWrites = true;

        var state = service.ApplyProfile(new PowerProfile(PerformanceMode.Custom, CpuBoost.Boost, GpuBoost.High));

        Assert.Empty(BoostWrites(ec));
        Assert.Equal(PerformanceMode.Balanced, state.Mode);
    }

    // ---- Failures --------------------------------------------------------

    [Fact]
    public void ApplyProfile_ThrowsWhenTheEcDoesNotEchoTheModeWrite()
    {
        var (ec, service) = Create(Balanced);
        ec.EchoOverride = args => [args[0], args[1], Balanced, args[3]];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.ApplyProfile(new PowerProfile(PerformanceMode.Silent)));

        Assert.Contains("did not confirm", exception.Message);
    }

    [Fact]
    public void ApplyProfile_ThrowsWhenTheEcDoesNotEchoTheBoostWrite()
    {
        var (ec, service) = Create(Custom, cpu: 1);
        ec.EchoOverride = args => [args[0], args[1], 0];

        Assert.Throws<InvalidOperationException>(() =>
            service.ApplyProfile(new PowerProfile(Cpu: CpuBoost.Boost)));
    }

    [Fact]
    public void ReadState_ThrowsWhenTheEcAnswersForTheWrongZone()
    {
        var transport = new ScriptedTransport((_, args) => FakeEc.Respond(0x00, 9, 0x00, 0x00));
        var service = new PerformanceService(transport);

        Assert.Throws<InvalidOperationException>(() => service.ReadState());
    }

    [Fact]
    public async Task TheAsyncEntryPoints_ReturnTheSameResultsAsTheSynchronousOnes()
    {
        var (_, service) = Create(Custom, cpu: 1, gpu: 0);

        var state = await service.ReadStateAsync();
        var applied = await service.ApplyProfileAsync(new PowerProfile(PerformanceMode.Custom, CpuBoost.High, null));

        Assert.Equal(PerformanceMode.Custom, state.Mode);
        Assert.Equal(CpuBoost.High, applied.Cpu);
    }
}
