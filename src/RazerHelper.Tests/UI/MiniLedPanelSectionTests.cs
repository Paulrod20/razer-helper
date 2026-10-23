using RazerHelper.Core.Services;
using RazerHelper.Tests.TestSupport;
using RazerHelper.UI.Sections;

namespace RazerHelper.Tests.UI;

public class MiniLedPanelSectionTests
{
    private static MiniLedPanelService Service(Func<bool> fail) => new(
        new ScriptedTransport((command, _) =>
        {
            if (fail())
                throw new IOException("Temporary HID failure");
            var reply = new byte[91];
            reply[6] = 1;
            reply[9] = command == 0x0D8F ? (byte)3 : (byte)0;
            return reply;
        }), () => 0x029F);

    [Fact]
    public Task StartupFailureCanRecoverOnNextOpening() => OnUiThread(async () =>
    {
        var failing = true;
        using var section = new MiniLedPanelSection(Service(() => failing));
        var availabilityEvents = 0;
        section.AvailabilityChanged += (_, _) => availabilityEvents++;

        await section.InitializeAsync();
        Assert.False(section.Visible);
        failing = false;
        await section.InitializeAsync();
        Assert.True(section.Visible);
        Assert.Equal(1, availabilityEvents);
    });

    [Fact]
    public Task LaterReadFailureDoesNotRemoveDetectedControls() => OnUiThread(async () =>
    {
        var failing = false;
        using var section = new MiniLedPanelSection(Service(() => failing));
        var availabilityEvents = 0;
        section.AvailabilityChanged += (_, _) => availabilityEvents++;

        await section.InitializeAsync();
        failing = true;
        await section.InitializeAsync();
        Assert.True(section.Visible);
        failing = false;
        await section.InitializeAsync();
        Assert.True(section.Visible);
        Assert.Equal(1, availabilityEvents);
    });

    [Fact]
    public Task RecreatedSectionDetectsPanelWithoutSavedUiState() => OnUiThread(async () =>
    {
        var service = Service(() => false);
        for (var opening = 0; opening < 3; opening++)
        {
            using var section = new MiniLedPanelSection(service);
            await section.InitializeAsync();
            Assert.True(section.Visible);
        }
    });

    private static Task OnUiThread(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new System.Windows.Forms.WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            context.Post(async _ =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    System.Windows.Forms.Application.ExitThread();
                }
            }, null);
            System.Windows.Forms.Application.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
