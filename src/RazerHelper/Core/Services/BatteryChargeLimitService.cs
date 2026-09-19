using RazerHelper.Core.Hardware;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class BatteryChargeLimitService(IRazerTransport transport)
{
    private const byte ChargeLimitEnabledFlag = 0x80;
    private const byte ChargeLimitDisabled = 0x50;

    public Task SetChargeLimitAsync(int percentage) =>
        Task.Run(() => SetChargeLimit(percentage));

    /// <summary>
    /// The byte the EC expects: bit 7 set plus the percentage when a limit is
    /// on, or 0x50 (bit 7 clear) for no limit.
    /// </summary>
    internal static byte ToWireValue(int percentage)
    {
        if (!BatteryLimitRange.IsValid(percentage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentage),
                percentage,
                $"The charge limit must be {BatteryLimitRange.Minimum} to " +
                $"{BatteryLimitRange.Maximum} percent in steps of {BatteryLimitRange.Step}.");
        }

        return percentage == BatteryLimitRange.NoLimit
            ? ChargeLimitDisabled
            : (byte)(ChargeLimitEnabledFlag | percentage);
    }

    private void SetChargeLimit(int percentage)
    {
        var wireValue = ToWireValue(percentage);

        transport.SendAndConfirm(
            RazerCommands.SetBatteryChargeLimit,
            [wireValue],
            "the battery charge limit");
    }
}
