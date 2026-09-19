namespace RazerHelper.Core.Models;

internal sealed record AppSettings(
    string? DisplayMode = null,
    int? BatteryChargeLimit = null,
    PowerProfile? PluggedInProfile = null,
    PowerProfile? OnBatteryProfile = null,
    // Written by earlier versions, before profiles existed. Read once and
    // folded into PluggedInProfile by SettingsService, then dropped.
    string? PerformanceMode = null,
    string? CustomCpuBoost = null,
    string? CustomGpuBoost = null);
