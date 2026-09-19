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

    public readonly byte[] ZoneMode = [0, 0];
    public byte CpuBoost;
    public byte GpuBoost;
    public readonly byte[] FanRpmHundreds = [0, 0];
    public byte BatteryLimitByte = 0x50;

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
            RazerCommands.GetActualFanRpm => Respond(0x00, args[1], FanRpmHundreds[args[1] - 1]),
            RazerCommands.SetBatteryChargeLimit => SetBattery(args),
            _ => throw new NotSupportedException($"FakeEc does not know command 0x{command:X4}.")
        };
    }

    /// <summary>Puts both zones in one mode, as a real mode change does.</summary>
    public void SetBothZones(byte mode)
    {
        ZoneMode[0] = mode;
        ZoneMode[1] = mode;
    }

    private byte[] SetMode(byte[] args)
    {
        // args: [enable, zone, mode, fanMode]
        if (!IgnoreModeWrites)
            ZoneMode[args[1] - 1] = args[2];

        return Echo(args);
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
