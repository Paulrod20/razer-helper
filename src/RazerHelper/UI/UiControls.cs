using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

internal static class UiControls
{
    public static TableLayoutPanel CreateTwoColumnLayout(float leftWidth, float rightWidth)
    {
        var layout = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, leftWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rightWidth));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return layout;
    }

    public static Control CreateSectionHeader(string title, string detail)
    {
        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = 28;

        header.Controls.Add(CreateSectionLabel(title), 0, 0);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Right,
                Font = GetDesignFont("Segoe UI", 9.5F),
                ForeColor = Color.Silver,
                Text = detail,
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);
        }

        return header;
    }

    public static Label CreateSectionLabel(string text) => new()
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = GetDesignFont("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.White,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    public static Control CreateButtonGrid(IReadOnlyList<string> buttonNames, string nameSuffix)
    {
        var grid = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = buttonNames.Count,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < buttonNames.Count; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / buttonNames.Count));

            var name = buttonNames[index];
            var button = CreateActionButton(name);
            button.Name = $"{name}{nameSuffix}";
            button.Tag = name;
            grid.Controls.Add(button, index, 0);
        }

        return grid;
    }

    /// <summary>Highlights <paramref name="selected"/> and resets the rest; null clears the selection.</summary>
    public static void HighlightSelected(IEnumerable<Button> buttons, Button? selected)
    {
        foreach (var button in buttons)
        {
            var isSelected = ReferenceEquals(button, selected);

            button.BackColor = isSelected ? RazerGreen : ButtonColor;
            button.ForeColor = isSelected ? BackgroundColor : Color.White;
            button.FlatAppearance.BorderColor = isSelected ? RazerGreen : BorderColor;
        }
    }

    /// <summary>
    /// Draws a button as unavailable and says why on hover, or restores it.
    /// The button stays enabled underneath, because WinForms shows no tooltip
    /// on a disabled control; so its click handler must check availability
    /// itself. An unavailable button also stays out of the keyboard focus order.
    /// </summary>
    public static void SetAvailability(Button button, bool available, ToolTip toolTip, string reasonWhenUnavailable)
    {
        // A selected button is green with dark text; the rest are dark with light text.
        var normalText = button.BackColor == RazerGreen ? BackgroundColor : Color.White;

        button.ForeColor = available ? normalText : SystemColors.GrayText;
        button.Cursor = available ? Cursors.Hand : Cursors.Default;
        button.TabStop = available;
        toolTip.SetToolTip(button, available ? string.Empty : reasonWhenUnavailable);
    }

    public static Button CreateActionButton(string text)
    {
        var button = new Button
        {
            BackColor = ButtonColor,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = new Padding(4),
            Text = text,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
