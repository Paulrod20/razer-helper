using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The question shown after unplugging: which apps are keeping the dedicated
/// GPU awake, and do you want them asked to close. It appears on top of
/// whatever you are doing, defaults to "Not now", and goes away by itself
/// (as "no") if the charger is plugged back in, since the question is moot then.
/// </summary>
internal sealed class GpuAppsConfirmForm : Form
{
    private const int ContentWidth = 380;

    private readonly IPowerSource _powerSource;
    private readonly bool _dismissWhenPluggedIn;

    public GpuAppsConfirmForm(IReadOnlyList<DgpuApp> apps, IPowerSource powerSource, bool dismissWhenPluggedIn)
    {
        _powerSource = powerSource;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "RazerHelper";
        TopMost = true;

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16, 14, 16, 14),
            WrapContents = false
        };

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = RazerGreen,
            Margin = new Padding(0, 0, 0, 8),
            Text = "Save battery?"
        });

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            MaximumSize = new Size(ContentWidth, 0),
            Text = DgpuText.BuildConfirmation(apps)
        });

        var yes = CreateActionButton("Ask them to close");
        yes.Dock = DockStyle.None;
        yes.DialogResult = DialogResult.Yes;
        yes.Size = new Size(150, 32);

        var no = CreateActionButton("Not now");
        no.Dock = DockStyle.None;
        no.DialogResult = DialogResult.No;
        no.Size = new Size(100, 32);

        // Enter and Esc both mean "no": closing programs is never the default.
        AcceptButton = no;
        CancelButton = no;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 12, 0, 0),
            Width = ContentWidth
        };
        buttons.Controls.Add(no);
        buttons.Controls.Add(yes);
        layout.Controls.Add(buttons);

        Controls.Add(layout);

        _dismissWhenPluggedIn = dismissWhenPluggedIn;
        _powerSource.PowerSourceChanged += PowerSource_PowerSourceChanged;
    }

    // Power events arrive on another thread.
    private void PowerSource_PowerSourceChanged(object? sender, EventArgs e)
    {
        if (!_dismissWhenPluggedIn || !PowerProfileRules.TreatAsPluggedIn(_powerSource.IsPluggedIn) || IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(() =>
            {
                DialogResult = DialogResult.No;
                Close();
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            // Already closing.
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _powerSource.PowerSourceChanged -= PowerSource_PowerSourceChanged;

        base.Dispose(disposing);
    }
}
