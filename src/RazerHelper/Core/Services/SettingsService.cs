using System.Text.Json;
using System.Text.Json.Serialization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Models;

namespace RazerHelper.Core.Services;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public SettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RazerHelper"))
    {
    }

    /// <summary>Stores settings in the given folder instead of the user's profile, e.g. for tests.</summary>
    internal SettingsService(string settingsDirectory)
    {
        _settingsDirectory = settingsDirectory;
        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                ?? new AppSettings();

            return MigrateLegacyProfile(settings);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            AppLog.Error("Could not load RazerHelper settings.", exception);
            return new AppSettings();
        }
    }

    // Before profiles, the app stored one mode and its boost levels. Those were
    // chosen while plugged in as far as anyone can tell, so they become the
    // plugged-in profile; the battery profile starts from its default.
    private static AppSettings MigrateLegacyProfile(AppSettings settings)
    {
        if (settings.PerformanceMode is null &&
            settings.CustomCpuBoost is null &&
            settings.CustomGpuBoost is null)
        {
            return settings;
        }

        return settings with
        {
            PluggedInProfile = settings.PluggedInProfile ?? new PowerProfile(
                ParseEnum<PerformanceMode>(settings.PerformanceMode),
                ParseEnum<CpuBoost>(settings.CustomCpuBoost),
                ParseEnum<GpuBoost>(settings.CustomGpuBoost)),
            PerformanceMode = null,
            CustomCpuBoost = null,
            CustomGpuBoost = null
        };
    }

    private static T? ParseEnum<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);

            var temporaryPath = $"{_settingsPath}.tmp";
            var json = JsonSerializer.Serialize(settings, SerializerOptions);

            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            AppLog.Error("Could not save RazerHelper settings.", exception);
        }
    }
}
