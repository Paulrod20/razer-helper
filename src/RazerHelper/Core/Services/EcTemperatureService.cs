using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

/// <summary>The two temperatures the laptop's controller reports, in whole degrees Celsius. Null when a byte is not believable.</summary>
internal sealed record EcTemperatures(int? CpuCelsius, int? GpuCelsius);

/// <summary>
/// Reads the CPU-side temperature from the laptop's embedded controller: one
/// read-only command, sent on the same channel as the fan speeds. Read only;
/// it can never change anything.
/// </summary>
/// <remarks>
/// <para>
/// The register was found by scanning the controller, not from documentation.
/// Its CPU byte follows CPU heat but is a slow sensor near the CPU, not the
/// die, so it reads well below tools that read the die. It is shown that way.
/// </para>
/// <para>
/// Because the sensor is slow (it took over a minute of full load to climb 10
/// degrees), the last reading is reused for a few seconds instead of asking
/// the controller again. Each controller read costs about 30 ms of waiting on
/// the shared channel, so this halves the temperature's share of the traffic
/// without the number ever being noticeably out of date.
/// </para>
/// </remarks>
internal sealed class EcTemperatureService(
    IRazerTransport transport,
    TimeSpan? cacheFor = null,
    Func<DateTime>? clock = null) : ICpuTemperatureSource
{
    /// <summary>How long a reading is reused. The sensor changes by about a degree every few seconds at most.</summary>
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(4);

    private readonly TimeSpan _cacheFor = cacheFor ?? DefaultCacheDuration;
    private readonly Func<DateTime> _now = clock ?? (() => DateTime.UtcNow);

    private bool _isSupported = true;
    private EcTemperatures? _last;
    private DateTime _lastReadAt;

    public double? ReadCelsius() => Read().CpuCelsius;

    internal EcTemperatures Read()
    {
        // A model without this register answers "not supported" once; asking
        // again every couple of seconds would be pointless traffic.
        if (!_isSupported)
            return new EcTemperatures(null, null);

        var now = _now();

        if (_last is not null && now - _lastReadAt < _cacheFor)
            return _last;

        try
        {
            var reply = transport.Send(RazerCommands.GetTemperatures, [0x00]);

            _last = new EcTemperatures(
                Believable(RazerHidPacket.GetArgument(reply, 1)),
                Believable(RazerHidPacket.GetArgument(reply, 2)));
            _lastReadAt = now;

            return _last;
        }
        catch (RazerCommandNotSupportedException)
        {
            _isSupported = false;
            AppLog.Info("This laptop does not report temperatures through its controller.");
            return new EcTemperatures(null, null);
        }

        // Any other failure is not cached, so the next call tries again.
    }

    /// <summary>
    /// 0 and 255 are what a missing or failed sensor reads as, and anything
    /// outside a plausible range is treated as no reading rather than shown.
    /// </summary>
    internal static int? Believable(byte celsius) =>
        celsius is >= 10 and <= 125 ? celsius : null;
}
