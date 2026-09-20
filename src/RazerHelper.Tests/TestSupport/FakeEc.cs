using RazerHelper.Core.Hardware;

namespace RazerHelper.Tests.TestSupport;

/// <summary>
/// A stand-in for the laptop's embedded controller. It keeps the same state
/// the real one does (a performance mode per zone, boost levels, a battery
/// limit byte) and answers the same commands, echoing writes back the way the
/// hardware does, so service logic can be tested with no laptop attached.
/// </summary>
internal sealed class FakeEc : IRazerTransport
{
    // The response argument area starts at byte 9 (see RazerHidPacket).
    private const int ResponseLength = 91;
    private const int ArgumentOffset = 9;
    private const byte CustomMode = 4;

    public readonly byte[] ZoneMode = [0, 0];
    public byte CpuBoost;
    public byte GpuBoost;
    public readonly byte[] FanRpmHundreds = [0, 0];
    public byte BatteryLimitByte = 0x50;

    // Lighting, in the units the real laptop uses.
    /// <summary>Keyboard effect id: 0 off, 2 breathing, 3 spectrum, 4 wave (1 static, 5 reactive and 7 starlight exist too).</summary>
    public byte KeyboardEffectId = 3;
    public byte KeyboardWaveDirection;
    public byte KeyboardBrightness = 255;
    public bool LogoOn;
    public byte LogoModeByte; // 0 steady, 2 breathing.
    public byte LogoBrightness = 255;

    /// <summary>When true a keyboard effect write is acknowledged but the laptop keeps showing what it had.</summary>
    public bool IgnoreKeyboardEffectWrites;

    /// <summary>The EC's max fan speed flag. Like the real one it only exists in Custom mode.</summary>
    public bool MaxFan;

    /// <summary>When true the EC accepts a mode write but silently keeps its old mode.</summary>
    public bool IgnoreModeWrites;

    /// <summary>When set, replaces the arguments echoed for every write, simulating an EC that did not take the change.</summary>
    public Func<byte[], byte[]>? EchoOverride;

    /// <summary>Called before each command is handled; may change EC state or throw to simulate a fault.</summary>
    public Action<FakeEc, ushort>? BeforeSend;

    public List<Sent> Log { get; } = [];

    public IEnumerable<Sent> Writes => Log.Where(sent => IsWrite(sent.Command));

    public IEnumerable<Sent> Reads => Log.Where(sent => !IsWrite(sent.Command));

    public byte[] Send(ushort command, ReadOnlySpan<byte> arguments)
    {
        var args = arguments.ToArray();
        Log.Add(new Sent(command, args));
        BeforeSend?.Invoke(this, command);

        return command switch
        {
            RazerCommands.GetPerformanceMode => Respond(0x00, args[1], ZoneMode[args[1] - 1], 0x00),
            RazerCommands.SetPerformanceMode => SetMode(args),
            RazerCommands.GetBoost => Respond(0x00, args[1], args[1] == 1 ? CpuBoost : GpuBoost),
            RazerCommands.SetBoost => SetBoost(args),
            RazerCommands.GetMaxFan => Respond((byte)(MaxFan ? 2 : 0), 0x00),
            RazerCommands.SetMaxFan => SetMaxFan(args),
            RazerCommands.GetActualFanRpm => Respond(0x00, args[1], FanRpmHundreds[args[1] - 1]),
            RazerCommands.SetBatteryChargeLimit => SetBattery(args),
            RazerCommands.SetKeyboardEffect => SetKeyboardEffect(args),
            RazerCommands.GetKeyboardEffect => Respond(args[0], args[1], KeyboardEffectId, KeyboardWaveDirection),
            RazerCommands.SetBrightness => SetBrightness(args),
            RazerCommands.GetBrightness => Respond(args[0], args[1], args[1] == 5 ? KeyboardBrightness : LogoBrightness),
            RazerCommands.SetLogoPower => SetLogoPower(args),
            RazerCommands.GetLogoPower => Respond(args[0], args[1], (byte)(LogoOn ? 1 : 0)),
            RazerCommands.SetLogoMode => SetLogoMode(args),
            RazerCommands.GetLogoMode => Respond(args[0], args[1], LogoModeByte),
            _ => throw new NotSupportedException($"FakeEc does not know command 0x{command:X4}.")
        };
    }

    /// <summary>Puts both zones in one mode, as a real mode change does.</summary>
    public void SetBothZones(byte mode)
    {
        ZoneMode[0] = mode;
        ZoneMode[1] = mode;
        ClearMaxFanOutsideCustom();
    }

    private byte[] SetMode(byte[] args)
    {
        // args: [enable, zone, mode, fanMode]
        if (!IgnoreModeWrites)
        {
            ZoneMode[args[1] - 1] = args[2];
            ClearMaxFanOutsideCustom();
        }

        return Echo(args);
    }

    private byte[] SetMaxFan(byte[] args)
    {
        // args: [2 = on, 0 = off]. The real EC rejects this outside Custom mode.
        if (ZoneMode[0] != CustomMode || ZoneMode[1] != CustomMode)
            throw new InvalidOperationException("The EC rejected the max fan command outside Custom mode.");

        MaxFan = args[0] == 2;
        return Echo(args);
    }

    // Leaving Custom clears the flag in the real EC.
    private void ClearMaxFanOutsideCustom()
    {
        if (ZoneMode[0] != CustomMode || ZoneMode[1] != CustomMode)
            MaxFan = false;
    }

    private byte[] SetBoost(byte[] args)
    {
        // args: [enable, cluster, level]
        if (args[1] == 1)
            CpuBoost = args[2];
        else
            GpuBoost = args[2];

        return Echo(args);
    }

    private byte[] SetKeyboardEffect(byte[] args)
    {
        // args: [store, led, effect, (direction for wave)]. Like the real one it replies with the same bytes.
        if (!IgnoreKeyboardEffectWrites)
        {
            KeyboardEffectId = args[2];

            if (args.Length > 3)
                KeyboardWaveDirection = args[3];
        }

        return Echo(args);
    }

    private byte[] SetBrightness(byte[] args)
    {
        // args: [1, led, brightness]; LED 5 is the keyboard, 4 the logo.
        if (args[1] == 5)
            KeyboardBrightness = args[2];
        else
            LogoBrightness = args[2];

        return Echo(args);
    }

    private byte[] SetLogoPower(byte[] args)
    {
        LogoOn = args[2] != 0;
        return Echo(args);
    }

    private byte[] SetLogoMode(byte[] args)
    {
        LogoModeByte = args[2];
        return Echo(args);
    }

    private byte[] SetBattery(byte[] args)
    {
        BatteryLimitByte = args[0];
        return Echo(args);
    }

    private byte[] Echo(byte[] args) => Respond(EchoOverride?.Invoke(args) ?? args);

    public static byte[] Respond(params byte[] arguments)
    {
        var response = new byte[ResponseLength];
        arguments.CopyTo(response, ArgumentOffset);
        return response;
    }

    private static bool IsWrite(ushort command) =>
        (command & 0x0080) == 0;

    /// <summary>One command the code under test sent.</summary>
    public sealed record Sent(ushort Command, byte[] Arguments);
}
