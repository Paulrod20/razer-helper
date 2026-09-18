using RazerHelper.Core.Models;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>CPU and GPU boost selectors shown while the Custom performance mode is active.</summary>
internal sealed class CustomBoostRow : TableLayoutPanel
{
    public const int RowHeight = 72;

    private readonly Dictionary<CpuBoost, Button> _cpuButtons = [];
    private readonly Dictionary<GpuBoost, Button> _gpuButtons = [];

    public CustomBoostRow()
    {
        BackColor = BackgroundColor;
        ColumnCount = 2;
        Dock = DockStyle.Bottom;
        Height = RowHeight;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 1;
        Visible = false;

        var cpuLevels = Enum.GetValues<CpuBoost>();
        var gpuLevels = Enum.GetValues<GpuBoost>();

        // Width follows the option count so every button is the same size.
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F * cpuLevels.Length / (cpuLevels.Length + gpuLevels.Length)));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F * gpuLevels.Length / (cpuLevels.Length + gpuLevels.Length)));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Add(CreateSelector("CPU", cpuLevels, _cpuButtons, level => CpuSelected?.Invoke(this, level)), 0, 0);
        Controls.Add(CreateSelector("GPU", gpuLevels, _gpuButtons, level => GpuSelected?.Invoke(this, level)), 1, 0);
    }

    public event EventHandler<CpuBoost>? CpuSelected;

    public event EventHandler<GpuBoost>? GpuSelected;

    /// <summary>Highlights the given levels; null clears a selector.</summary>
    public void ShowBoosts(CpuBoost? cpu, GpuBoost? gpu)
    {
        HighlightSelected(_cpuButtons.Values, cpu is CpuBoost c ? _cpuButtons[c] : null);
        HighlightSelected(_gpuButtons.Values, gpu is GpuBoost g ? _gpuButtons[g] : null);
    }

    private static Panel CreateSelector<TLevel>(
        string title,
        TLevel[] levels,
        Dictionary<TLevel, Button> buttons,
        Action<TLevel> onSelected)
        where TLevel : struct, Enum
    {
        var panel = new Panel
        {
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var grid = CreateButtonGrid(levels.Select(level => level.ToString()).ToArray(), $"{title}BoostButton");

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var level = Enum.Parse<TLevel>((string)button.Tag!);
            buttons[level] = button;
            button.Click += (_, _) => onSelected(level);
        }

        // Dock order: the label docks first, the buttons fill what is left.
        panel.Controls.Add(grid);
        panel.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Font = CreateDesignFont("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Height = 22,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        });

        return panel;
    }
}
