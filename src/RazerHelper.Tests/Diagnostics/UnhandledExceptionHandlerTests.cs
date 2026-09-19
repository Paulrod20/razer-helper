using RazerHelper.Core.Diagnostics;

namespace RazerHelper.Tests.Diagnostics;

public class UnhandledExceptionHandlerTests
{
    private const string LogLocation = @"C:\Users\Someone\AppData\Local\RazerHelper\razerhelper.log";

    private sealed class Recorder
    {
        public List<(string Message, Exception? Exception)> Logged { get; } = [];
        public List<string> Notifications { get; } = [];
        public Action<string>? NotifyBehavior { get; set; }

        public UnhandledExceptionHandler CreateHandler() => new(
            (message, exception) => Logged.Add((message, exception)),
            message =>
            {
                Notifications.Add(message);
                NotifyBehavior?.Invoke(message);
            },
            LogLocation);
    }

    [Fact]
    public void ARecoverableException_IsLoggedWithItsDetails()
    {
        var recorder = new Recorder();
        var problem = new InvalidOperationException("the popup broke");

        recorder.CreateHandler().OnRecoverableException(problem);

        var entry = Assert.Single(recorder.Logged);
        Assert.Same(problem, entry.Exception);
        Assert.Contains("UI thread", entry.Message);
    }

    [Fact]
    public void ARecoverableException_TellsTheUserWhereTheLogIs()
    {
        var recorder = new Recorder();

        recorder.CreateHandler().OnRecoverableException(new Exception("boom"));

        var notification = Assert.Single(recorder.Notifications);
        Assert.Contains(LogLocation, notification);
        Assert.Contains("still running", notification);
    }

    [Fact]
    public void RepeatedRecoverableExceptions_AreAllLoggedButTheUserIsToldOnlyOnce()
    {
        // A timer that fails every two seconds must not bury the user in dialogs.
        var recorder = new Recorder();
        var handler = recorder.CreateHandler();

        for (var i = 0; i < 5; i++)
            handler.OnRecoverableException(new Exception($"failure {i}"));

        Assert.Equal(5, recorder.Logged.Count);
        Assert.Single(recorder.Notifications);
    }

    [Fact]
    public void AFatalException_IsLoggedAndTheUserIsToldTheAppIsClosing()
    {
        var recorder = new Recorder();
        var problem = new NullReferenceException("a bug on a worker thread");

        recorder.CreateHandler().OnFatalException(problem);

        Assert.Same(problem, Assert.Single(recorder.Logged).Exception);
        var notification = Assert.Single(recorder.Notifications);
        Assert.Contains("has to close", notification);
        Assert.Contains(LogLocation, notification);
    }

    [Fact]
    public void AFatalException_AlwaysNotifies_EvenAfterARecoverableOne()
    {
        var recorder = new Recorder();
        var handler = recorder.CreateHandler();

        handler.OnRecoverableException(new Exception("earlier, survivable"));
        handler.OnFatalException(new Exception("now the end"));

        Assert.Equal(2, recorder.Notifications.Count);
        Assert.Contains("has to close", recorder.Notifications[1]);
    }

    [Fact]
    public void AnUnobservedTaskException_IsLoggedButNeverShownToTheUser()
    {
        var recorder = new Recorder();
        var problem = new TimeoutException("nobody awaited this");

        recorder.CreateHandler().OnUnobservedTaskException(problem);

        Assert.Same(problem, Assert.Single(recorder.Logged).Exception);
        Assert.Empty(recorder.Notifications);
    }

    [Fact]
    public void AFailureToShowTheMessage_DoesNotEscapeFromTheHandler()
    {
        // The handler runs when things are already broken; it must not add a
        // second, worse failure on top.
        var recorder = new Recorder { NotifyBehavior = _ => throw new InvalidOperationException("no window station") };

        var exception = Record.Exception(() =>
            recorder.CreateHandler().OnFatalException(new Exception("original")));

        Assert.Null(exception);
        Assert.Contains(recorder.Logged, entry => entry.Message.Contains("Could not show the error message"));
    }

    [Fact]
    public void AFailureToShowTheMessage_StillLeavesTheOriginalErrorInTheLog()
    {
        var original = new Exception("the real problem");
        var recorder = new Recorder { NotifyBehavior = _ => throw new InvalidOperationException("dialog failed") };

        recorder.CreateHandler().OnRecoverableException(original);

        Assert.Contains(recorder.Logged, entry => ReferenceEquals(entry.Exception, original));
    }

    [Fact]
    public async Task RecoverableExceptionsFromSeveralThreads_StillProduceExactlyOneNotification()
    {
        var recorder = new Recorder();
        var handler = recorder.CreateHandler();
        var gate = new Barrier(8);

        var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            gate.SignalAndWait();
            handler.OnRecoverableException(new Exception($"thread {i}"));
        }));
        await Task.WhenAll(tasks);

        Assert.Single(recorder.Notifications);
    }
}
