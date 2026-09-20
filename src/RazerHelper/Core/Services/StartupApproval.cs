namespace RazerHelper.Core.Services;

/// <summary>
/// The "Startup" switch in Windows' Task Manager. An entry in a Run key starts
/// at sign-in unless a matching value in the StartupApproved key marks it
/// disabled: 12 bytes, the first being 03 (or 07) for disabled and 02 (or 06)
/// for enabled, then the time it was changed. Flipping this is reversible and
/// leaves the program's own Run entry untouched, unlike deleting it.
/// </summary>
internal static class StartupApproval
{
    private const byte Disabled = 0x03;
    private const byte DisabledAlternate = 0x07;

    /// <summary>
    /// True only for the values Windows documents as disabled. Anything else,
    /// including a missing value, means the entry still starts: wrongly
    /// calling something enabled costs one extra write, while wrongly calling
    /// it disabled would leave it running.
    /// </summary>
    public static bool IsDisabled(byte[]? flag) =>
        flag is { Length: > 0 } && flag[0] is Disabled or DisabledAlternate;

    /// <summary>The value Task Manager writes when an entry is switched off.</summary>
    public static byte[] CreateDisabled(DateTime nowUtc)
    {
        var flag = new byte[12];
        flag[0] = Disabled;
        BitConverter.GetBytes(nowUtc.ToFileTimeUtc()).CopyTo(flag, 4);
        return flag;
    }

    /// <summary>Text form for the settings file. No value (never set) is an empty string.</summary>
    public static string ToText(byte[]? flag) => flag is null ? string.Empty : Convert.ToHexString(flag);

    /// <summary>The bytes for a stored text, or null for "there was no value" (or text that is not valid).</summary>
    public static byte[]? FromText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        try
        {
            return Convert.FromHexString(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
