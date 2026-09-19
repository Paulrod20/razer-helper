using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

/// <summary>
/// The rules that decide what the performance UI offers and stores. Pure
/// functions with no hardware or UI, so every rule can be tested directly.
/// </summary>
internal static class PowerProfileRules
{
    /// <summary>Windows reporting no power state (no battery, or unknown) counts as plugged in.</summary>
    public static bool TreatAsPluggedIn(bool? isPluggedIn) => isPluggedIn != false;

    /// <summary>
    /// Like Synapse, only Balanced is offered on battery. That is Synapse
    /// policy, not a hardware limit: the EC accepts every mode on battery.
    /// </summary>
    public static bool IsModeAllowed(PerformanceMode mode, bool pluggedIn) =>
        pluggedIn || mode == PerformanceMode.Balanced;

    /// <summary>
    /// Max fan speed is a Custom-only setting (the EC rejects it elsewhere), and
    /// like Custom itself it is offered only when plugged in. Synapse also
    /// requires CPU Boost and GPU High; this deliberately does not, yet.
    /// </summary>
    public static bool CanUseMaxFan(PerformanceState state, bool pluggedIn) =>
        state.Mode == PerformanceMode.Custom && IsModeAllowed(PerformanceMode.Custom, pluggedIn);

    /// <summary>Boost levels belong to Custom, so they follow Custom's availability.</summary>
    public static bool CanChangeBoost(PerformanceState state, bool pluggedIn) =>
        state.Mode == PerformanceMode.Custom && IsModeAllowed(PerformanceMode.Custom, pluggedIn);

    /// <summary>
    /// Makes a stored profile safe to apply on <paramref name="pluggedIn"/>.
    /// A battery profile saved by an earlier version may hold a mode that is no
    /// longer offered there; it falls back to Balanced. Plugged-in profiles
    /// are never restricted.
    /// </summary>
    public static PowerProfile Sanitize(PowerProfile profile, bool pluggedIn)
    {
        if (profile.Mode is not PerformanceMode mode || IsModeAllowed(mode, pluggedIn))
            return profile;

        return profile with { Mode = PerformanceMode.Balanced };
    }

    /// <summary>
    /// The profile to store after the EC settled in <paramref name="state"/>.
    /// It records what the EC really ended up in, so a stored profile is fully
    /// specified. Boost levels the EC does not report (it only reports them in
    /// Custom) keep their requested value for the next time Custom is used.
    /// </summary>
    public static PowerProfile Remember(PowerProfile requested, PerformanceState state) =>
        requested with
        {
            Mode = state.Mode ?? requested.Mode,
            Cpu = state.Cpu ?? requested.Cpu,
            Gpu = state.Gpu ?? requested.Gpu
        };
}
