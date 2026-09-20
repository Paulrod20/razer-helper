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
    string? CustomGpuBoost = null,
    // Settings window. Both are on unless the user turns them off, which is
    // also what a settings file from an earlier version means.
    bool AutoSwitchProfiles = true,
    bool HideWhenClickedAway = true,
    // Off unless the user turns it on: it closes other programs.
    bool CloseGpuAppsOnUnplug = false,
    // Program names (e.g. "blender") to never close, on top of the built-in
    // list. Edited by hand in settings.json.
    string[]? NeverCloseApps = null,
    // What Task Manager's startup switch was for each Razer login entry before
    // "Stop" turned it off (as stored text; empty means it had none), so "Start"
    // can put back exactly that.
    Dictionary<string, string>? RazerLoginApprovals = null);
