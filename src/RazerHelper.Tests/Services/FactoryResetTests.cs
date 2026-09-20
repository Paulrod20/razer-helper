using System.ServiceProcess;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;

namespace RazerHelper.Tests.Services;

public sealed class FactoryResetTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RazerHelper.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class FakeStartup(bool enabled) : IStartupRegistration
    {
        public bool IsEnabled { get; private set; } = enabled;
        public bool FailToChange { get; set; }

        public void SetEnabled(bool value)
        {
            if (FailToChange)
                throw new UnauthorizedAccessException("registry is locked");

            IsEnabled = value;
        }
    }

    // Settings as a long-time user might have them.
    private static AppSettings LotsOfSettings() => new(
        DisplayMode: "Auto",
        BatteryChargeLimit: 80,
        PluggedInProfile: new PowerProfile(PerformanceMode.Custom, CpuBoost.High, GpuBoost.Medium),
        OnBatteryProfile: new PowerProfile(PerformanceMode.Balanced),
        RazerServiceStartModes: new Dictionary<string, ServiceStartMode> { ["RazerExperienceService"] = ServiceStartMode.Automatic },
        AutoSwitchProfiles: false,
        HideWhenClickedAway: false,
        CloseGpuAppsOnUnplug: true,
        NeverCloseApps: ["blender"]);

    private (FactoryReset Reset, SettingsService Settings, FakeStartup Startup, FakeEc Ec) Create(bool startAtLogin = true)
    {
        var settings = new SettingsService(_directory);
        var startup = new FakeStartup(startAtLogin);
        var ec = new FakeEc();
        ec.SetBothZones(4); // Custom.
        ec.BatteryLimitByte = 0xD0; // 80%.

        var reset = new FactoryReset(settings, startup, new PerformanceService(ec), new BatteryChargeLimitService(ec));
        return (reset, settings, startup, ec);
    }

    [Fact]
    public async Task ClearsEverySavedSetting()
    {
        var (reset, settings, _, _) = Create();
        settings.Save(LotsOfSettings());

        var result = await reset.RunAsync(LotsOfSettings());
        var loaded = settings.Load();

        Assert.True(result.Succeeded);
        Assert.Null(loaded.DisplayMode);
        Assert.Null(loaded.BatteryChargeLimit);
        Assert.Null(loaded.PluggedInProfile);
        Assert.Null(loaded.OnBatteryProfile);
        Assert.True(loaded.AutoSwitchProfiles);
        Assert.True(loaded.HideWhenClickedAway);
        Assert.False(loaded.CloseGpuAppsOnUnplug);
        Assert.Null(loaded.NeverCloseApps);
    }

    [Fact]
    public async Task KeepsTheRecordOfServicesThatStopDisabled_SoStartCanStillRestoreThem()
    {
        var (reset, settings, _, _) = Create();
        var before = LotsOfSettings();
        settings.Save(before);

        await reset.RunAsync(before);

        Assert.Equal(ServiceStartMode.Automatic, settings.Load().RazerServiceStartModes!["RazerExperienceService"]);
    }

    [Fact]
    public async Task TurnsStartAtLoginOff()
    {
        var (reset, _, startup, _) = Create(startAtLogin: true);

        await reset.RunAsync(LotsOfSettings());

        Assert.False(startup.IsEnabled);
    }

    [Fact]
    public async Task SetsTheLaptopToBalancedWithNoChargeLimit()
    {
        var (reset, _, _, ec) = Create();

        await reset.RunAsync(LotsOfSettings());

        Assert.Equal([0, 0], ec.ZoneMode);
        Assert.Equal(0x50, ec.BatteryLimitByte); // No limit.
    }

    [Fact]
    public async Task LeavingCustom_AlsoClearsMaxFan()
    {
        var (reset, _, _, ec) = Create();
        ec.MaxFan = true;

        await reset.RunAsync(LotsOfSettings());

        Assert.False(ec.MaxFan);
    }

    [Fact]
    public async Task OnAFirstRunLaptopAlreadyAtDefaults_NothingBreaksAndNothingExtraIsWritten()
    {
        var (reset, _, _, ec) = Create();
        ec.SetBothZones(0);
        ec.BatteryLimitByte = 0x50;

        var result = await reset.RunAsync(new AppSettings());

        Assert.True(result.Succeeded);
        Assert.Equal(0x50, ec.BatteryLimitByte);
    }

    [Fact]
    public async Task IfTheLaptopWillNotAnswer_TheRestOfTheResetStillHappens_AndItIsReported()
    {
        var (reset, settings, startup, ec) = Create();
        settings.Save(LotsOfSettings());
        ec.BeforeSend = (_, _) => throw new InvalidOperationException("EC not responding");

        var result = await reset.RunAsync(LotsOfSettings());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Problems, problem => problem.Contains("Balanced"));
        Assert.Contains(result.Problems, problem => problem.Contains("charge limit"));
        Assert.False(startup.IsEnabled);
        Assert.Null(settings.Load().BatteryChargeLimit);
    }

    [Fact]
    public async Task IfStartAtLoginCannotBeChanged_ItIsReported_AndTheLaptopPartStillRuns()
    {
        var (reset, _, startup, ec) = Create();
        startup.FailToChange = true;

        var result = await reset.RunAsync(LotsOfSettings());

        Assert.Equal(["Start at login could not be turned off."], result.Problems);
        Assert.Equal(0x50, ec.BatteryLimitByte);
    }

    [Fact]
    public async Task IfTheSettingsFileCannotBeWritten_ItIsReported()
    {
        // A directory where the settings file should be makes the write fail.
        Directory.CreateDirectory(Path.Combine(_directory, "settings.json"));
        var (reset, _, _, _) = Create();

        var result = await reset.RunAsync(LotsOfSettings());

        Assert.Contains(result.Problems, problem => problem.Contains("saved settings"));
    }

    [Fact]
    public void TheDefaults_AreExactlyAFreshSettingsObject_ApartFromTheServiceRecord()
    {
        var defaults = FactoryReset.DefaultsKeepingServiceRecord(new AppSettings());

        Assert.Equal(new AppSettings(), defaults);
    }

    [Fact]
    public void ARealUserSettingsFile_IsCoveredFieldByField()
    {
        // If a setting is added to AppSettings, this fails until the reset
        // has been considered for it: every property must either be reset to
        // its first-run value or be the deliberately kept service record.
        var defaults = FactoryReset.DefaultsKeepingServiceRecord(LotsOfSettings());
        var fresh = new AppSettings();

        foreach (var property in typeof(AppSettings).GetProperties().Where(p => p.Name is not ("EqualityContract")))
        {
            if (property.Name == nameof(AppSettings.RazerServiceStartModes))
                continue;

            Assert.True(
                Equals(property.GetValue(fresh), property.GetValue(defaults)),
                $"The reset does not restore {property.Name} to its first-run value.");
        }
    }
}
