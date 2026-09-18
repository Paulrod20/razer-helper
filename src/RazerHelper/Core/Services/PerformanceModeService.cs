using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

public sealed class PerformanceModeService : IDisposable
{
    private const ushort SetPerformanceModeCommand = 0x0D02;
    private const ushort GetPerformanceModeCommand = 0x0D82;
    private const ushort SetBoostCommand = 0x0D07;
    private const ushort GetBoostCommand = 0x0D87;
    private const byte FanModeAuto = 0x00;
    private const byte CpuCluster = 0x01;
    private const byte GpuCluster = 0x02;

    // The EC keeps the mode per zone (CPU and GPU) and expects both written.
    private static readonly byte[] Zones = [0x01, 0x02];

    private readonly RazerHidTransport _transport = new();

    public Task<PerformanceMode?> GetModeAsync() => Task.Run(GetMode);

    public Task SetModeAsync(PerformanceMode mode) => Task.Run(() => SetMode(mode));

    /// <summary>The boost levels the EC currently holds; a level is null if it is not one we know.</summary>
    public Task<(CpuBoost? Cpu, GpuBoost? Gpu)> GetBoostsAsync() => Task.Run(() =>
    {
        var cpu = ReadBoost(CpuCluster);
        var gpu = ReadBoost(GpuCluster);

        return (
            Enum.IsDefined((CpuBoost)cpu) ? (CpuBoost?)cpu : null,
            Enum.IsDefined((GpuBoost)gpu) ? (GpuBoost?)gpu : null);
    });

    public Task SetCpuBoostAsync(CpuBoost boost) => Task.Run(() =>
    {
        if (!Enum.IsDefined(boost))
            throw new ArgumentOutOfRangeException(nameof(boost), boost, "Unsupported CPU boost.");

        SetBoost(CpuCluster, (byte)boost);
    });

    public Task SetGpuBoostAsync(GpuBoost boost) => Task.Run(() =>
    {
        if (!Enum.IsDefined(boost))
            throw new ArgumentOutOfRangeException(nameof(boost), boost, "Unsupported GPU boost.");

        SetBoost(GpuCluster, (byte)boost);
    });

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
            SendAndConfirm(
                SetPerformanceModeCommand,
                [0x01, zone, (byte)mode, FanModeAuto],
                $"performance mode {mode} for zone {zone}");
        }
    }

    private byte ReadBoost(byte cluster)
    {
        var response = _transport.Send(GetBoostCommand, [0x00, cluster, 0x00]);

        if (RazerHidPacket.GetArgument(response, 1) != cluster)
            throw new InvalidOperationException("The boost response used an unexpected cluster.");

        return RazerHidPacket.GetArgument(response, 2);
    }

    private void SetBoost(byte cluster, byte level)
    {
        // The EC only honors boost writes in Custom mode; anywhere else the
        // write would be dropped or misapplied, so refuse it up front.
        if (GetMode() != PerformanceMode.Custom)
            throw new InvalidOperationException("Boost levels can only be changed in Custom mode.");

        SendAndConfirm(
            SetBoostCommand,
            [0x01, cluster, level],
            $"boost level {level} for cluster {cluster}");
    }

    // The EC echoes the arguments it accepted; anything else means the write
    // did not take effect.
    private void SendAndConfirm(ushort command, byte[] arguments, string description)
    {
        var response = _transport.Send(command, arguments);

        for (var index = 0; index < arguments.Length; index++)
        {
            if (RazerHidPacket.GetArgument(response, index) != arguments[index])
                throw new InvalidOperationException($"The Razer Blade did not confirm {description}.");
        }
    }
}
