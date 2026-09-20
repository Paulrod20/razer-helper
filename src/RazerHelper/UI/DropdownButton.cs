using System.ComponentModel;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark drop-down list. The stock ComboBox ignores the theme (its arrow and
/// list stay light), so this is a flat button that opens a dark menu under
/// itself, in the same style as the other buttons.
/// </summary>
/// <remarks>
/// <see cref="SelectionChanged"/> fires only when the user picks an item;
/// <see cref="Select"/> changes what is shown without raising it, so showing
/// what the laptop is doing never looks like a click.
/// </remarks>
internal sealed class DropdownButton : Button
{
    private static readonly Color HoverColor = Color.FromArgb(70, 70, 70);
    private static readonly SolidBrush ArrowBrush = new(Color.Silver);
    private static readonly SolidBrush DisabledArrowBrush = new(SystemColors.GrayText);

    private readonly string[] _items;
    private readonly string _placeholder;
    private readonly ContextMenuStrip _menu = new();
    private int _selectedIndex = -1;

    public DropdownButton(IReadOnlyList<string> items, string placeholder = "-")
    {
        _items = [.. items];
        _placeholder = placeholder;

        BackColor = ButtonColor;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        Font = GetDesignFont("Segoe UI", 9.5F);
        ForeColor = Color.White;
        Padding = new Padding(8, 0, 22, 0);
        TextAlign = ContentAlignment.MiddleLeft;
        UseVisualStyleBackColor = false;
        FlatAppearance.BorderColor = BorderColor;
        FlatAppearance.BorderSize = 1;

        _menu.BackColor = ButtonColor;
        _menu.ForeColor = Color.White;
        _menu.Font = Font;
        _menu.ShowImageMargin = false;
        _menu.Renderer = new DarkMenuRenderer();

        for (var index = 0; index < _items.Length; index++)
        {
            var itemIndex = index;
            var item = new ToolStripMenuItem(_items[index])
            {
                BackColor = ButtonColor,
                ForeColor = Color.White,
                Padding = new Padding(4, 4, 4, 4)
            };

            item.Click += (_, _) => Pick(itemIndex);
            _menu.Items.Add(item);
        }

        UpdateText();
    }

    /// <summary>Raised when the user picks an item.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The picked item, or -1 when none is shown.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex => _selectedIndex;

    /// <summary>Shows an item (or none, with -1) without raising <see cref="SelectionChanged"/>.</summary>
    public void Select(int index)
    {
        _selectedIndex = index >= 0 && index < _items.Length ? index : -1;
        UpdateText();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        _menu.MinimumSize = new Size(Width, 0);
        _menu.Show(this, new Point(0, Height));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);

        // The arrow, drawn as a small triangle.
        var centerX = Width - 12;
        var centerY = Height / 2;

        pevent.Graphics.FillPolygon(Enabled ? ArrowBrush : DisabledArrowBrush,
        [
            new Point(centerX - 4, centerY - 2),
            new Point(centerX + 4, centerY - 2),
            new Point(centerX, centerY + 3)
        ]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _menu.Dispose();

        base.Dispose(disposing);
    }

    private void Pick(int index)
    {
        if (index == _selectedIndex)
            return;

        Select(index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateText()
    {
        Text = _selectedIndex >= 0 ? _items[_selectedIndex] : _placeholder;

        // The current choice is picked out in the list, in the app's green.
        for (var index = 0; index < _menu.Items.Count; index++)
            _menu.Items[index].ForeColor = index == _selectedIndex ? RazerGreen : Color.White;
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Keep the color chosen per item (the selected one is green).
            e.TextColor = e.Item.ForeColor;
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ButtonColor;
        public override Color MenuBorder => BorderColor;
        public override Color MenuItemBorder => HoverColor;
        public override Color MenuItemSelected => HoverColor;
        public override Color MenuItemSelectedGradientBegin => HoverColor;
        public override Color MenuItemSelectedGradientEnd => HoverColor;
        public override Color ImageMarginGradientBegin => ButtonColor;
        public override Color ImageMarginGradientMiddle => ButtonColor;
        public override Color ImageMarginGradientEnd => ButtonColor;
    }
}
