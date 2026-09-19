using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RazerHelper.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, json);
    }

    [Fact]
    public void Load_WithNoFile_ReturnsEmptySettings()
    {
        Assert.Equal(new AppSettings(), new SettingsService(_directory).Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsEverything()
    {
        var service = new SettingsService(_directory);
        var settings = new AppSettings(
            DisplayMode: "Auto",
            BatteryChargeLimit: 80,
            PluggedInProfile: new PowerProfile(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Medium),
            OnBatteryProfile: new PowerProfile(PerformanceMode.Balanced));

        service.Save(settings);

        Assert.Equal(settings, service.Load());
    }

    [Fact]
    public void Save_CreatesTheSettingsFolderWhenItDoesNotExist()
    {
        new SettingsService(_directory).Save(new AppSettings(DisplayMode: "60 Hz"));

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        // Saving writes a temp file and swaps it in, so a crash mid-save
        // cannot leave a half-written settings.json.
        new SettingsService(_directory).Save(new AppSettings(DisplayMode: "Auto"));

        Assert.Equal([SettingsPath], Directory.GetFiles(_directory));
    }

    [Fact]
    public void Save_WritesEnumsAsReadableNamesAndOmitsEmptyValues()
    {
        new SettingsService(_directory).Save(new AppSettings(
            PluggedInProfile: new PowerProfile(PerformanceMode.Custom, CpuBoost.Medium, null)));

        var json = File.ReadAllText(SettingsPath);

        Assert.Contains("\"Mode\": \"Custom\"", json);
        Assert.Contains("\"Cpu\": \"Medium\"", json);
        Assert.DoesNotContain("null", json);
        Assert.DoesNotContain("Gpu", json);
    }

    [Fact]
    public void Load_MigratesTheSingleModeFromBeforeProfilesIntoThePluggedInProfile()
    {
        // The exact shape an earlier build wrote to disk.
        WriteSettings("""
            {
              "DisplayMode": "Auto",
              "BatteryChargeLimit": 100,
              "PerformanceMode": "Custom",
              "CustomCpuBoost": null,
              "CustomGpuBoost": null
            }
            """);

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(new PowerProfile(PerformanceMode.Custom), settings.PluggedInProfile);
        Assert.Null(settings.OnBatteryProfile);   // falls back to the default at run time
        Assert.Equal("Auto", settings.DisplayMode);
        Assert.Equal(100, settings.BatteryChargeLimit);
    }

    [Fact]
    public void Load_MigratesEarlierBoostLevelsToo()
    {
        WriteSettings("""
            { "PerformanceMode": "Custom", "CustomCpuBoost": "High", "CustomGpuBoost": "Medium" }
            """);

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(new PowerProfile(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Medium), settings.PluggedInProfile);
    }

    [Fact]
    public void Load_ClearsTheOldFieldsAfterMigrating_SoTheyAreNotWrittenAgain()
    {
        WriteSettings("""{ "PerformanceMode": "Silent" }""");
        var service = new SettingsService(_directory);

        service.Save(service.Load());

        var json = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain("PerformanceMode", json);
        Assert.Contains("\"Mode\": \"Silent\"", json);
    }

    [Fact]
    public void Load_DoesNotLetOldFieldsOverwriteAProfileThatAlreadyExists()
    {
        WriteSettings("""
            {
              "PerformanceMode": "Silent",
              "PluggedInProfile": { "Mode": "Custom", "Cpu": "High" }
            }
            """);

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(new PowerProfile(PerformanceMode.Custom, CpuBoost.High), settings.PluggedInProfile);
    }

    [Fact]
    public void Load_IgnoresOldValuesItDoesNotRecognise()
    {
        WriteSettings("""{ "PerformanceMode": "Turbo", "CustomCpuBoost": "Ludicrous" }""");

        var settings = new SettingsService(_directory).Load();

        Assert.Equal(new PowerProfile(), settings.PluggedInProfile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"DisplayMode\": ")]
    [InlineData("[]")]
    public void Load_FallsBackToEmptySettingsForAFileItCannotRead(string contents)
    {
        WriteSettings(contents);

        Assert.Equal(new AppSettings(), new SettingsService(_directory).Load());
    }

    [Fact]
    public void Load_FallsBackToEmptySettingsWhenAProfileNamesAModeThisBuildDoesNotKnow()
    {
        // Documents current behavior: one unrecognised value in the new format
        // (say, from a newer build) discards the whole file instead of just
        // that field. It does not crash, and the user's choices are re-made.
        WriteSettings("""
            {
              "DisplayMode": "Auto",
              "PluggedInProfile": { "Mode": "Hyperboost" }
            }
            """);

        Assert.Equal(new AppSettings(), new SettingsService(_directory).Load());
    }
}
