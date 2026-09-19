using System.Globalization;

namespace RazerHelper.Core.Models;

/// <summary>
/// A refresh-rate choice: a fixed rate, or Auto, which follows the power
/// source. Replaces passing button labels like "120 Hz" around as strings.
/// </summary>
internal sealed record DisplayRefreshMode(int? FixedHz)
{
    private const string AutoLabel = "Auto";
    private const string HertzSuffix = " Hz";

    // What Auto picks: the fast rate on AC, the efficient one on battery.
    private const int PluggedInHz = 120;
    private const int OnBatteryHz = 60;

    public static DisplayRefreshMode Auto { get; } = new((int?)null);

    public static DisplayRefreshMode Fixed(int hertz) => new(hertz);

    /// <summary>The modes the popup offers, in display order.</summary>
    public static IReadOnlyList<DisplayRefreshMode> Offered { get; } = [Fixed(60), Fixed(120), Auto];

    public bool IsAuto => FixedHz is null;

    /// <summary>The text shown on the button and stored in settings, e.g. "120 Hz" or "Auto".</summary>
    public string Label => FixedHz is int hertz ? $"{hertz}{HertzSuffix}" : AutoLabel;

    /// <summary>The rate to apply now. A fixed mode always returns its own rate.</summary>
    public int TargetHz(bool pluggedIn) => FixedHz ?? (pluggedIn ? PluggedInHz : OnBatteryHz);

    /// <summary>Reads a stored label back. Returns null for anything unrecognized.</summary>
    public static DisplayRefreshMode? Parse(string? label)
    {
        if (string.Equals(label, AutoLabel, StringComparison.Ordinal))
            return Auto;

        if (label is not null &&
            label.EndsWith(HertzSuffix, StringComparison.Ordinal) &&
            int.TryParse(label.AsSpan(0, label.Length - HertzSuffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var hertz) &&
            hertz > 0)
        {
            return Fixed(hertz);
        }

        return null;
    }
}
