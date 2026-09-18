using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

public sealed class PerformanceModeService : IDisposable
{
    private const ushort SetPerformanceModeCommand = 0x0D02;
    private const ushort GetPerformanceModeCommand = 0x0D82;
    private const byte FanModeAuto = 0x00;

    // The EC keeps the mode per zone (CPU and GPU) and expects both written.
    private static readonly byte[] Zones = [0x01, 0x02];

    private readonly RazerHidTransport _transport = new();

    public Task<PerformanceMode?> GetModeAsync() => Task.Run(GetMode);

    public Task SetModeAsync(PerformanceMode mode) => Task.Run(() => SetMode(mode));

    public void Dispose() => _transport.Dispose();

    private PerformanceMode? GetMode()
    {
        // The zones are separate HID round trips, so a mode change landing
        // between them (Fn+P, Synapse) makes them disagree for a moment.
        // Re-read once before treating a disagreement as real.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var cpuZone = ReadZoneMode(Zones[0]);
            var gpuZone = ReadZoneMode(Zones[1]);

            if (cpuZone != gpuZone)
                continue;

            return Enum.IsDefined((PerformanceMode)cpuZone)
                ? (PerformanceMode)cpuZone
                : null;
        }

        AppLog.Error("The EC reported different performance modes for its two zones.");
        return null;
    }

    private byte ReadZoneMode(byte zone)
    {
        var response = _transport.Send(GetPerformanceModeCommand, [0x00, zone, 0x00, 0x00]);

        if (RazerHidPacket.GetArgument(response, 1) != zone)
            throw new InvalidOperationException("The performance-mode response used an unexpected zone.");

        return RazerHidPacket.GetArgument(response, 2);
    }

    private void SetMode(PerformanceMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported performance mode.");

        foreach (var zone in Zones)
        {
            byte[] arguments = [0x01, zone, (byte)mode, FanModeAuto];
            var response = _transport.Send(SetPerformanceModeCommand, arguments);

            // The EC echoes the arguments it accepted; anything else means the
            // write did not take effect.
            for (var index = 0; index < arguments.Length; index++)
            {
                if (RazerHidPacket.GetArgument(response, index) != arguments[index])
                {
                    throw new InvalidOperationException(
                        $"The Razer Blade did not confirm performance mode {mode} for zone {zone}.");
                }
            }
        }
    }
}
