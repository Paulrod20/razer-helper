using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class FullscreenGuardTests
{
    private sealed class FakeDetector : IFullscreenDetector
    {
        public bool GameRunning { get; set; }

        public bool IsFullscreenAppRunning() => GameRunning;
    }

    [Fact]
    public void WithNoGameRunning_AChangeMayGoAheadAtOnce()
    {
        var guard = new FullscreenGuard(new FakeDetector());

        Assert.True(guard.CanApplyNow());
        Assert.False(guard.IsWaiting);
    }

    [Fact]
    public void WithAGameRunning_AChangeIsPutOnHold()
    {
        var guard = new FullscreenGuard(new FakeDetector { GameRunning = true });

        Assert.False(guard.CanApplyNow());
        Assert.True(guard.IsWaiting);
    }

    [Fact]
    public void ARetryIsOnlyReadyOnceTheGameHasEnded()
    {
        var detector = new FakeDetector { GameRunning = true };
        var guard = new FullscreenGuard(detector);
        guard.CanApplyNow();

        Assert.False(guard.ReadyToRetry);

        detector.GameRunning = false;

        Assert.True(guard.ReadyToRetry);
    }

    [Fact]
    public void NothingIsReadyToRetryWhenNothingWasOnHold()
    {
        var guard = new FullscreenGuard(new FakeDetector());

        Assert.False(guard.ReadyToRetry);
    }

    [Fact]
    public void ApplyingAfterTheGameEnds_ClearsTheHold()
    {
        var detector = new FakeDetector { GameRunning = true };
        var guard = new FullscreenGuard(detector);
        guard.CanApplyNow();
        detector.GameRunning = false;

        Assert.True(guard.CanApplyNow());
        Assert.False(guard.IsWaiting);
        Assert.False(guard.ReadyToRetry);
    }

    [Fact]
    public void ChoosingAFixedRate_DropsWhatWasOnHold()
    {
        var detector = new FakeDetector { GameRunning = true };
        var guard = new FullscreenGuard(detector);
        guard.CanApplyNow();

        guard.Cancel();
        detector.GameRunning = false;

        Assert.False(guard.IsWaiting);
        Assert.False(guard.ReadyToRetry);
    }

    [Theory]
    [InlineData(2, true)]   // A fullscreen app is running.
    [InlineData(3, true)]   // A Direct3D fullscreen game.
    [InlineData(4, true)]   // Presentation mode.
    [InlineData(1, false)]  // Screen saver or locked.
    [InlineData(5, false)]  // Normal desktop.
    [InlineData(6, false)]  // Quiet time.
    [InlineData(7, false)]  // A Store app, not fullscreen.
    public void WindowsStates_MapToFullscreenOrNot(int state, bool expected) =>
        Assert.Equal(expected, WindowsFullscreenDetector.IsFullscreenState(state));

    [Fact]
    public void TheRealDetector_AnswersOnThisMachineWithoutFailing()
    {
        // Only that it returns an answer; whether a game is running is not something a test can control.
        _ = new WindowsFullscreenDetector().IsFullscreenAppRunning();
    }
}
