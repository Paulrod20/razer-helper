using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>What a reset did. <see cref="Problems"/> lists, in plain words, each part that did not work.</summary>
internal sealed record FactoryResetResult(IReadOnlyList<string> Problems)
{
    public bool Succeeded => Problems.Count == 0;
}

/// <summary>
/// Puts RazerHelper back the way it was the first time it ran: saved settings
/// cleared, Start at login off, and the laptop set to Balanced with no charge
/// limit. Each part is attempted on its own, so one failing never stops the
/// others, and the result says what did not work.
/// </summary>
/// <remarks>
/// Razer's background services are deliberately not touched. The record of what
/// each service was set to before "Stop" is kept, because "Start" needs it to
/// put the services back exactly as they were; wiping it would strand them.
/// </remarks>
internal sealed class FactoryReset(
    SettingsService settingsService,
    IStartupRegistration startupRegistration,
    PerformanceService performanceService,
    BatteryChargeLimitService batteryChargeLimitService)
{
    /// <summary>First-run settings, except for the record of Razer services that "Stop" disabled.</summary>
    internal static AppSettings DefaultsKeepingServiceRecord(AppSettings current) =>
        new(RazerServiceStartModes: current.RazerServiceStartModes);

    public async Task<FactoryResetResult> RunAsync(AppSettings current)
    {
        var problems = new List<string>();

        if (!settingsService.Save(DefaultsKeepingServiceRecord(current)))
            problems.Add("Your saved settings could not be cleared.");

        try
        {
            startupRegistration.SetEnabled(false);
        }
        catch (Exception exception)
        {
            AppLog.Error("Reset: could not turn off Start at login.", exception);
            problems.Add("Start at login could not be turned off.");
        }

        // Both are attempted even if the first fails: they are independent.
        try
        {
            await performanceService.ApplyProfileAsync(new PowerProfile(PerformanceMode.Balanced));
        }
        catch (Exception exception)
        {
            AppLog.Error("Reset: could not set Balanced mode.", exception);
            problems.Add("The laptop could not be set to Balanced mode.");
        }

        try
        {
            await batteryChargeLimitService.SetChargeLimitAsync(BatteryLimitRange.NoLimit);
        }
        catch (Exception exception)
        {
            AppLog.Error("Reset: could not remove the battery charge limit.", exception);
            problems.Add("The battery charge limit could not be removed.");
        }

        AppLog.Info(problems.Count == 0
            ? "Reset to defaults completed."
            : $"Reset to defaults finished with problems: {string.Join(" ", problems)}");

        return new FactoryResetResult(problems);
    }
}
