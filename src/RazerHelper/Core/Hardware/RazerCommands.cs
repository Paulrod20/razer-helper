namespace RazerHelper.Core.Hardware;

/// <summary>
/// Every EC command the app sends, in one place. A command id is
/// <c>(command class &lt;&lt; 8) | command</c>; the "get" counterpart of a
/// "set" is the same id with 0x80 set on the low byte.
/// </summary>
/// <remarks>
/// Verified on a Razer Blade 16 (2023), PID 0x029F. The ids come from the
/// community reverse-engineering work in razer-ctl (MIT) and OpenRazer.
/// </remarks>
internal static class RazerCommands
{
    // Class 0x0D: performance and fans.
    public const ushort SetPerformanceMode = 0x0D02;
    public const ushort GetPerformanceMode = 0x0D82;
    public const ushort SetBoost = 0x0D07;
    public const ushort GetBoost = 0x0D87;
    public const ushort GetActualFanRpm = 0x0D88;

    // Class 0x07: max fan speed. Custom performance mode only.
    public const ushort SetMaxFan = 0x070F;
    public const ushort GetMaxFan = 0x078F;

    // Class 0x07: battery.
    public const ushort SetBatteryChargeLimit = 0x0712;
}
