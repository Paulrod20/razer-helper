using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

public sealed class BatteryChargeLimitService : IDisposable
{
    private const ushort SetBatteryChargeLimitCommand = 0x0712;
    private const byte ChargeLimitEnabledFlag = 0x80;
    private const byte ChargeLimitDisabled = 0x50;

    private readonly RazerHidTransport _transport = new();

    public Task SetChargeLimitAsync(int percentage) =>
        Task.Run(() => SetChargeLimit(percentage));

    public void Dispose() => _transport.Dispose();

    private void SetChargeLimit(int percentage)
    {
        if (percentage is not (60 or 80 or 100))
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentage),
                percentage,
                "The charge limit must be 60, 80, or 100 percent.");
        }

        var wireValue = percentage == 100
            ? ChargeLimitDisabled
            : (byte)(ChargeLimitEnabledFlag | percentage);

        var response = _transport.Send(
            SetBatteryChargeLimitCommand,
            [wireValue]);

        if (RazerHidPacket.GetArgument(response, 0) != wireValue)
        {
            throw new InvalidOperationException(
                "The Razer Blade did not confirm the battery charge-limit change.");
        }
    }
}
