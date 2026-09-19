using System.Reflection;

namespace RazerHelper.Helpers;

/// <summary>The app's version as shown to the user, read from the project file so it is only set in one place.</summary>
internal static class AppVersion
{
    /// <summary>For example "v1.0". Falls back to an empty string if the version cannot be read.</summary>
    public static string Current { get; } = Format(Assembly.GetExecutingAssembly().GetName().Version);

    /// <summary>Shows major.minor, and the patch number only when it is not zero: 1.0.0 is "v1.0", 1.0.3 is "v1.0.3".</summary>
    internal static string Format(Version? version)
    {
        if (version is null)
            return string.Empty;

        var patch = Math.Max(version.Build, 0);

        return patch == 0
            ? $"v{version.Major}.{version.Minor}"
            : $"v{version.Major}.{version.Minor}.{patch}";
    }
}
