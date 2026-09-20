using RazerHelper.Helpers;

namespace RazerHelper.Tests.Helpers;

public class AppRestartTests
{
    [Fact]
    public void ReadsThePreviousProcessIdFromTheRestartArguments()
    {
        Assert.True(AppRestart.TryGetPreviousProcessId(["--restarted", "4242"], out var processId));
        Assert.Equal(4242, processId);
    }

    [Theory]
    [InlineData]
    [InlineData("--restarted")]
    [InlineData("--restarted", "abc")]
    [InlineData("--restarted", "-5")]
    [InlineData("--restarted", "0")]
    [InlineData("--restarted", " 12")]
    [InlineData("--restarted", "+12")]
    [InlineData("--restarted", "12", "extra")]
    [InlineData("--razer-services", "stop")]
    [InlineData("4242")]
    public void AnythingElse_IsNotARestart(params string[] args) =>
        Assert.False(AppRestart.TryGetPreviousProcessId(args, out _));

    [Fact]
    public void WaitingWithoutTheRestartSwitch_ReturnsImmediately() =>
        AppRestart.WaitForPreviousCopy([]);

    [Fact]
    public void WaitingForAProcessThatIsAlreadyGone_ReturnsImmediately()
    {
        // The highest possible id is not a running process.
        AppRestart.WaitForPreviousCopy(["--restarted", int.MaxValue.ToString()]);
    }
}

public class AppRestartWaitTests
{
    [Fact]
    public void WaitingForALiveProcess_BlocksUntilItHasExited()
    {
        using var previous = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "powershell.exe", "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 2")
        {
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        AppRestart.WaitForPreviousCopy(["--restarted", previous.Id.ToString()]);
        clock.Stop();

        Assert.True(previous.HasExited, "It returned while the previous copy was still running.");
        Assert.True(clock.Elapsed > TimeSpan.FromMilliseconds(500), "It did not wait.");
    }
}
