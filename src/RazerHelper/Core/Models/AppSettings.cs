namespace RazerHelper.Core.Models;

public sealed record AppSettings(
    string? DisplayMode = null,
    int BatteryChargeLimit = 80);
