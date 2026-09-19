using System.ServiceProcess;

namespace RazerHelper.Core.Models;

internal sealed record AppSettings(
    string? DisplayMode = null,
    int? BatteryChargeLimit = null,
    PowerProfile? PluggedInProfile = null,
    PowerProfile? OnBatteryProfile = null,
    // What each Razer service was set to before "Stop" disabled it, so "Start"
    // can put back exactly that instead of guessing Automatic.
    Dictionary<string, ServiceStartMode>? RazerServiceStartModes = null,
    // Written by earlier versions, before profiles existed. Read once and
    // folded into PluggedInProfile by SettingsService, then dropped.
    string? PerformanceMode = null,
    string? CustomCpuBoost = null,
    string? CustomGpuBoost = null);
