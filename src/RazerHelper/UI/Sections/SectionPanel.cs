using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Base for the popup's sections: the shared look (dark, fills its grid cell,
/// 8px gap below) and a safe way to update the UI from hardware and power
/// callbacks, which arrive on other threads.
/// </summary>
internal abstract class SectionPanel : Panel
{
    // Post through the UI thread's context instead of Control.BeginInvoke,
    // which needs a window handle. A tray popup has none until it is first
    // shown, and power events can arrive long before that.
    private readonly SynchronizationContext _uiContext;

    protected SectionPanel()
    {
        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        // Read here, not in a field initializer: those run before the Control
        // base constructor, which is what may install the context.
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread, unless the section is already disposed.</summary>
    protected void PostToUi(Action action) =>
        _uiContext.Post(_ =>
        {
            if (!IsDisposed)
                action();
        }, null);

    /// <summary>Like <see cref="PostToUi"/>, but the returned task completes once the action has run, so callers can sequence on it.</summary>
    protected Task PostToUiAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _uiContext.Post(_ =>
        {
            try
            {
                if (!IsDisposed)
                    action();
            }
            finally
            {
                completion.SetResult();
            }
        }, null);

        return completion.Task;
    }
}
