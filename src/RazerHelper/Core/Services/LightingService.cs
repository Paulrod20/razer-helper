using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>
/// Reads and changes the keyboard backlight and the lid logo. Every command
/// here was sent to a Razer Blade 16 (2023) and read back before it was written
/// down. The device stays in Normal mode throughout, so the Fn media keys keep
/// working; nothing here can change the device mode.
/// </summary>
internal sealed class LightingService(IRazerTransport transport)
{
    // "Variable storage" 1 writes the laptop's own saved slot, so the effect
    // survives idle and wake. (Without it the laptop shows its default again.)
    private const byte StoreInLaptop = 0x01;

    private const byte LogoLed = 0x04;
    private const byte KeyboardLed = 0x05;

    // Effect ids of the extended matrix effect command.
    private const byte EffectOff = 0x00;
    private const byte EffectStatic = 0x01;
    private const byte EffectBreathing = 0x02;
    private const byte EffectSpectrum = 0x03;
    private const byte EffectWave = 0x04;
    private const byte WaveDirection = 0x01;

    // A static effect carries a color, laid out as OpenRazer sends it:
    // [store, led, 1, 0, 0, 1, red, green, blue]. In Normal mode the laptop
    // shows Razer green whatever is sent (checked on a Blade 16 with five other
    // colors), so Razer green is sent: what is asked for is what is shown.
    private static readonly byte[] StaticGreen = [StoreInLaptop, KeyboardLed, EffectStatic, 0x00, 0x00, 0x01, 0x44, 0xD6, 0x2C];

    // Logo mode values.
    private const byte LogoSteady = 0x00;
    private const byte LogoBreathing = 0x02;

    public Task<LightingState> ReadStateAsync() => Task.Run(ReadState);

    public Task SetKeyboardEffectAsync(KeyboardEffect effect) => Task.Run(() => SetKeyboardEffect(effect));

    public Task SetKeyboardBrightnessAsync(int percent) => Task.Run(() => SetKeyboardBrightness(percent));

    public Task SetLogoAsync(LogoMode mode) => Task.Run(() => SetLogo(mode));

    public Task SetLogoBrightnessAsync(int percent) => Task.Run(() => SetLogoBrightness(percent));

    internal LightingState ReadState() => new(
        ReadKeyboardEffect(),
        LightingBrightness.ToPercent(ReadBrightness(KeyboardLed)),
        ReadLogo(),
        LightingBrightness.ToPercent(ReadBrightness(LogoLed)));

    internal void SetKeyboardEffect(KeyboardEffect effect)
    {
        byte[] arguments = effect switch
        {
            KeyboardEffect.Off => [StoreInLaptop, KeyboardLed, EffectOff],
            KeyboardEffect.StaticGreen => StaticGreen,
            KeyboardEffect.Spectrum => [StoreInLaptop, KeyboardLed, EffectSpectrum],
            KeyboardEffect.Breathing => [StoreInLaptop, KeyboardLed, EffectBreathing],
            KeyboardEffect.Wave => [StoreInLaptop, KeyboardLed, EffectWave, WaveDirection],
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null)
        };

        transport.Send(RazerCommands.SetKeyboardEffect, arguments);

        // Believe what the laptop says it is doing, not what was asked for.
        var now = ReadKeyboardEffect();

        if (now != effect)
        {
            AppLog.Error($"Asked for keyboard effect {effect} but the laptop reports {now?.ToString() ?? "something else"}.");
            throw new InvalidOperationException($"The Razer Blade did not confirm the keyboard effect {effect}.");
        }
    }

    internal void SetLogo(LogoMode mode)
    {
        // Off is only the power switch; the mode is left as it was.
        if (mode != LogoMode.Off)
        {
            transport.SendAndConfirm(
                RazerCommands.SetLogoMode,
                [0x01, LogoLed, mode == LogoMode.Breathing ? LogoBreathing : LogoSteady],
                "the logo mode");
        }

        transport.SendAndConfirm(
            RazerCommands.SetLogoPower,
            [0x01, LogoLed, (byte)(mode == LogoMode.Off ? 0x00 : 0x01)],
            "the logo power");
    }

    internal void SetKeyboardBrightness(int percent) => SetBrightness(KeyboardLed, percent, "keyboard brightness");

    internal void SetLogoBrightness(int percent) => SetBrightness(LogoLed, percent, "logo brightness");

    private void SetBrightness(byte led, int percent, string description) =>
        transport.SendAndConfirm(
            RazerCommands.SetBrightness,
            [0x01, led, LightingBrightness.ToByte(percent)],
            description);

    private KeyboardEffect? ReadKeyboardEffect()
    {
        var response = transport.Send(RazerCommands.GetKeyboardEffect, [StoreInLaptop, KeyboardLed, 0x00]);

        if (RazerHidPacket.GetArgument(response, 1) != KeyboardLed)
            throw new InvalidOperationException("The keyboard effect response was for a different light.");

        // Effects set by other software (reactive, starlight) are not ones this
        // app offers: report "unknown", never a wrong one.
        return RazerHidPacket.GetArgument(response, 2) switch
        {
            EffectOff => KeyboardEffect.Off,
            EffectStatic => KeyboardEffect.StaticGreen,
            EffectBreathing => KeyboardEffect.Breathing,
            EffectSpectrum => KeyboardEffect.Spectrum,
            EffectWave => KeyboardEffect.Wave,
            _ => null
        };
    }

    private LogoMode? ReadLogo()
    {
        var power = ReadRegister(RazerCommands.GetLogoPower, LogoLed);

        if (power == 0x00)
            return LogoMode.Off;

        return ReadRegister(RazerCommands.GetLogoMode, LogoLed) switch
        {
            LogoSteady => LogoMode.On,
            LogoBreathing => LogoMode.Breathing,
            _ => null
        };
    }

    private byte ReadBrightness(byte led) => ReadRegister(RazerCommands.GetBrightness, led);

    // The lighting registers answer [1, led, value].
    private byte ReadRegister(ushort command, byte led)
    {
        var response = transport.Send(command, [0x01, led, 0x00]);

        if (RazerHidPacket.GetArgument(response, 1) != led)
            throw new InvalidOperationException($"The lighting response for 0x{command:X4} was for a different light.");

        return RazerHidPacket.GetArgument(response, 2);
    }
}
