using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The very bottom of the popup: the app version on the left, then the
/// "Free up GPU" and Settings links on the right. It only reports clicks;
/// the popup decides what they do.
/// </summary>
internal sealed class AppFooter : TableLayoutPanel
{
    public AppFooter()
    {
        BackColor = BackgroundColor;
        ColumnCount = 3;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 1;

        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = GetDesignFont("Segoe UI", 8F),
            ForeColor = SubtleTextColor,
            Margin = Padding.Empty,
            Text = $"RazerHelper {AppVersion.Current}",
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);

        var freeUpLink = CreateLink("Free up GPU", new Padding(0, 0, 14, 0));
        freeUpLink.LinkClicked += (_, _) => FreeUpGpuRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(freeUpLink, 1, 0);

        var settingsLink = CreateLink("Settings", Padding.Empty);
        settingsLink.LinkClicked += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(settingsLink, 2, 0);
    }

    public event EventHandler? FreeUpGpuRequested;

    public event EventHandler? SettingsRequested;

    private static LinkLabel CreateLink(string text, Padding margin) => new()
    {
        ActiveLinkColor = Color.White,
        AutoSize = true,
        Dock = DockStyle.Fill,
        Font = GetDesignFont("Segoe UI", 8.5F),
        LinkBehavior = LinkBehavior.HoverUnderline,
        LinkColor = Color.Silver,
        Margin = margin,
        Text = text,
        TextAlign = ContentAlignment.BottomRight
    };
}
