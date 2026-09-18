using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Placeholder for performance modes. The buttons are not wired to the EC yet;
/// this is where that work will live when it resumes.
/// </summary>
internal sealed class PerformanceSection : Panel
{
    public PerformanceSection()
    {
        BackColor = BackgroundColor;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 0, 8);
        Padding = Padding.Empty;

        Controls.Add(CreateButtonGrid(["Balanced", "Silent", "Custom"], "ModeButton"));
        Controls.Add(CreateSectionHeader("Performance Mode", "Plugged in"));
    }
}
