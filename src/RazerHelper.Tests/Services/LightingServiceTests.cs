using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public class LightingServiceTests
{
    private static (LightingService Service, FakeEc Ec) Create()
    {
        var ec = new FakeEc();
        return (new LightingService(ec), ec);
    }

    // The exact bytes sent to a Razer Blade 16 (2023) during the hardware
    // tests, so a refactor can never quietly change what goes on the wire.

    [Theory]
    [InlineData((int)KeyboardEffect.Off, "010500")]
    [InlineData((int)KeyboardEffect.Spectrum, "010503")]
    [InlineData((int)KeyboardEffect.Breathing, "010502")]
    [InlineData((int)KeyboardEffect.Wave, "01050401")]
    public void KeyboardEffects_SendTheBytesVerifiedOnTheLaptop(int effect, string expectedArguments)
    {
        var (service, ec) = Create();

        service.SetKeyboardEffect((KeyboardEffect)effect);

        var write = Assert.Single(ec.Writes);
        Assert.Equal(RazerCommands.SetKeyboardEffect, write.Command);
        Assert.Equal(expectedArguments, Convert.ToHexString(write.Arguments));
    }

    [Fact]
    public void AKeyboardEffect_IsConfirmedByReadingItBack()
    {
        var (service, ec) = Create();

        service.SetKeyboardEffect(KeyboardEffect.Wave);

        Assert.Contains(ec.Reads, read => read.Command == RazerCommands.GetKeyboardEffect);
        Assert.Equal(4, ec.KeyboardEffectId);
    }

    [Fact]
    public void IfTheLaptopKeepsShowingTheOldEffect_ItIsAnError_NotASilentSuccess()
    {
        var (service, ec) = Create();
        ec.IgnoreKeyboardEffectWrites = true;

        var failure = Assert.Throws<InvalidOperationException>(() => service.SetKeyboardEffect(KeyboardEffect.Off));

        Assert.Contains("did not confirm", failure.Message);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 128)]
    [InlineData(100, 255)]
    public void KeyboardBrightness_IsSentAsAByte(int percent, int expectedByte)
    {
        var (service, ec) = Create();

        service.SetKeyboardBrightness(percent);

        var write = Assert.Single(ec.Writes);
        Assert.Equal(RazerCommands.SetBrightness, write.Command);
        Assert.Equal([0x01, 0x05, (byte)expectedByte], write.Arguments);
    }

    [Fact]
    public void LogoBrightness_UsesTheLogoLight()
    {
        var (service, ec) = Create();

        service.SetLogoBrightness(40);

        Assert.Equal(0x04, Assert.Single(ec.Writes).Arguments[1]);
        Assert.Equal(102, ec.LogoBrightness);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void BrightnessOutsideZeroToOneHundred_IsRefusedAndNothingIsSent(int percent)
    {
        var (service, ec) = Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetKeyboardBrightness(percent));
        Assert.Empty(ec.Log);
    }

    [Fact]
    public void LogoOn_SetsTheSteadyModeThenPowersItOn()
    {
        var (service, ec) = Create();

        service.SetLogo(LogoMode.On);

        Assert.Equal(
            [(RazerCommands.SetLogoMode, "010400"), (RazerCommands.SetLogoPower, "010401")],
            ec.Writes.Select(write => (write.Command, Convert.ToHexString(write.Arguments))));
        Assert.True(ec.LogoOn);
    }

    [Fact]
    public void LogoBreathing_SetsTheBreathingModeThenPowersItOn()
    {
        var (service, ec) = Create();

        service.SetLogo(LogoMode.Breathing);

        Assert.Equal(
            [(RazerCommands.SetLogoMode, "010402"), (RazerCommands.SetLogoPower, "010401")],
            ec.Writes.Select(write => (write.Command, Convert.ToHexString(write.Arguments))));
    }

    [Fact]
    public void LogoOff_OnlyTurnsThePowerOff_AndLeavesTheModeAlone()
    {
        var (service, ec) = Create();
        ec.LogoOn = true;
        ec.LogoModeByte = 2;

        service.SetLogo(LogoMode.Off);

        var write = Assert.Single(ec.Writes);
        Assert.Equal(RazerCommands.SetLogoPower, write.Command);
        Assert.Equal("010400", Convert.ToHexString(write.Arguments));
        Assert.Equal(2, ec.LogoModeByte);
    }

    [Fact]
    public void ALogoChangeThatIsNotEchoedBack_IsAnError()
    {
        var (service, ec) = Create();
        ec.EchoOverride = _ => [0x00, 0x00, 0x00];

        Assert.Throws<InvalidOperationException>(() => service.SetLogo(LogoMode.On));
    }

    [Fact]
    public void ReadState_ShowsWhatTheLaptopIsDoing()
    {
        var (service, ec) = Create();
        ec.KeyboardEffectId = 4;
        ec.KeyboardBrightness = 128;
        ec.LogoOn = true;
        ec.LogoModeByte = 2;
        ec.LogoBrightness = 255;

        var state = service.ReadState();

        Assert.Equal(new LightingState(KeyboardEffect.Wave, 50, LogoMode.Breathing, 100), state);
    }

    [Fact]
    public void ReadState_WithTheLogoOff_SaysOff_WhateverModeIsStored()
    {
        var (service, ec) = Create();
        ec.LogoOn = false;
        ec.LogoModeByte = 2;

        Assert.Equal(LogoMode.Off, service.ReadState().Logo);
    }

    [Theory]
    [InlineData(1)] // Static colour.
    [InlineData(5)] // Reactive.
    [InlineData(7)] // Starlight.
    public void AnEffectSetByOtherSoftware_IsReportedAsUnknown_NotAsSomethingElse(byte effectId)
    {
        var (service, ec) = Create();
        ec.KeyboardEffectId = effectId;

        Assert.Null(service.ReadState().Keyboard);
    }

    [Fact]
    public void ALogoModeWeDoNotOffer_IsReportedAsUnknown()
    {
        var (service, ec) = Create();
        ec.LogoOn = true;
        ec.LogoModeByte = 9;

        Assert.Null(service.ReadState().Logo);
    }

    [Fact]
    public void ReadingState_NeverWrites()
    {
        var (service, ec) = Create();

        service.ReadState();

        Assert.Empty(ec.Writes);
    }

    [Fact]
    public void ADifferentLightInTheResponse_IsRefused()
    {
        // A response for another light must never be taken as the keyboard's.
        Assert.Throws<InvalidOperationException>(() => new LightingService(new WrongLightTransport()).ReadState());
    }

    [Fact]
    public void NothingHereCanChangeTheDeviceMode()
    {
        // Driver mode (device mode 3, command 0x0004) is what switches the Fn
        // media keys off. Whatever lighting is asked to do, it must never be sent.
        var (service, ec) = Create();
        service.SetKeyboardEffect(KeyboardEffect.Wave);
        service.SetKeyboardBrightness(70);
        service.SetLogo(LogoMode.Breathing);
        service.SetLogo(LogoMode.Off);

        Assert.DoesNotContain(ec.Log, sent => sent.Command == 0x0004);
    }

    // Answers every read for the wrong light.
    private sealed class WrongLightTransport : IRazerTransport
    {
        public byte[] Send(ushort command, ReadOnlySpan<byte> arguments) => FakeEc.Respond(0x01, 0x09, 0x03);
    }
}
