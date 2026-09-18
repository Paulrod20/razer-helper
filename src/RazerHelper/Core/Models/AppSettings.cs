namespace RazerHelper.Core.Models;

public sealed record AppSettings(
    string? DisplayMode = null,
    int? BatteryChargeLimit = null,
    string? PerformanceMode = null);
