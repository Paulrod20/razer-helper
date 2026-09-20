using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class PowerSourceServiceTests
{
    [Fact]
    public void BatteryPercentageChangesDoNotRaiseTheEvent()
    {
        bool? pluggedIn = false;
        using var service = new PowerSourceService(() => pluggedIn);
        var raised = 0;
        service.PowerSourceChanged += (_, _) => raised++;

        service.OnStatusChange();
        service.OnStatusChange();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void PluggingInAndUnpluggingEachRaiseItOnce()
    {
        bool? pluggedIn = false;
        using var service = new PowerSourceService(() => pluggedIn);
        var raised = 0;
        service.PowerSourceChanged += (_, _) => raised++;

        pluggedIn = true;
        service.OnStatusChange();
        service.OnStatusChange(); // a repeat report of the same state
        Assert.Equal(1, raised);

        pluggedIn = false;
        service.OnStatusChange();
        Assert.Equal(2, raised);
    }

    [Fact]
    public void AStateThatBecomesUnknownAndBackIsReported()
    {
        bool? pluggedIn = true;
        using var service = new PowerSourceService(() => pluggedIn);
        var raised = 0;
        service.PowerSourceChanged += (_, _) => raised++;

        pluggedIn = null;
        service.OnStatusChange();
        pluggedIn = true;
        service.OnStatusChange();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void IsPluggedInReadsTheCurrentState()
    {
        bool? pluggedIn = true;
        using var service = new PowerSourceService(() => pluggedIn);

        Assert.True(service.IsPluggedIn);

        pluggedIn = false;
        Assert.False(service.IsPluggedIn);
    }
}
