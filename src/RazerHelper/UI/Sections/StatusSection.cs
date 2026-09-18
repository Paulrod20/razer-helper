using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>Shows the latest status message reported by any section.</summary>
internal sealed class StatusSection : Panel
{
    private readonly Label _messageLabel;

    public StatusSection()
    {
        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        var title = CreateSectionLabel("RazerHelper Status");
        title.Dock = DockStyle.Top;

        _messageLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = CreateDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.Silver,
            Padding = new Padding(0, 4, 0, 0),
            Text = "Tray shell is ready. Display and battery controls are active."
        };

        Controls.Add(_messageLabel);
        Controls.Add(title);
    }

    public void ShowStatus(SectionStatus status)
    {
        _messageLabel.ForeColor = status.IsError ? Color.IndianRed : Color.Silver;
        _messageLabel.Text = status.Message;
    }
}
