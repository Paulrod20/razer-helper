using System.ComponentModel;
using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark, flat slider that snaps to fixed steps and labels each one. The
/// stock TrackBar ignores the theme, so this draws the track, thumb and tick
/// labels itself.
/// </summary>
/// <remarks>
/// <see cref="ValueChanged"/> fires on every step while dragging;
/// <see cref="Committed"/> fires once when the user lets go (mouse up, or a
/// key that moved the value), which is the moment to act on the value.
/// </remarks>
internal sealed class ThemedSlider : Control
{
    private const int ThumbRadius = 9;
    private const int TrackHeight = 4;
    private const int TrackY = 16;
    private const int LabelTop = 32;

    private static readonly Color TrackColor = Color.FromArgb(70, 70, 70);
    private static readonly Color DisabledColor = Color.FromArgb(100, 100, 100);
    private static readonly Font LabelFont = CreateDesignFont("Segoe UI", 8.5F);

    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int _step;

    private int _value;
    private bool _dragging;
    private bool _keyMovedValue;

    public ThemedSlider(int minimum, int maximum, int step)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimum, maximum);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, 0);

        _minimum = minimum;
        _maximum = maximum;
        _step = step;
        _value = minimum;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.StandardClick,
            true);
        TabStop = true;
        BackColor = BackgroundColor;
        Height = 50;
    }

    public event EventHandler? ValueChanged;

    public event EventHandler? Committed;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            var snapped = Snap(value);

            if (snapped == _value)
                return;

            _value = snapped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End ||
        base.IsInputKey(keyData);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || !Enabled)
            return;

        Focus();
        _dragging = true;
        Value = ValueAt(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragging)
            Value = ValueAt(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        _dragging = false;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var target = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - _step,
            Keys.Right or Keys.Up => _value + _step,
            Keys.Home => _minimum,
            Keys.End => _maximum,
            _ => (int?)null
        };

        if (target is null)
            return;

        e.Handled = true;

        var before = _value;
        Value = target.Value;
        _keyMovedValue |= _value != before;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        // Only a key that actually moved the slider is worth acting on;
        // Tab and friends are not.
        if (!_keyMovedValue)
            return;

        _keyMovedValue = false;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var accent = Enabled ? RazerGreen : DisabledColor;
        var textColor = Enabled ? Color.Silver : DisabledColor;
        var thumbX = XAt(_value);

        // Track, then the filled part up to the thumb.
        using (var trackBrush = new SolidBrush(TrackColor))
        using (var fillBrush = new SolidBrush(accent))
        {
            var track = new Rectangle(XAt(_minimum), TrackY - TrackHeight / 2, TrackWidth, TrackHeight);
            graphics.FillRectangle(trackBrush, track);
            graphics.FillRectangle(fillBrush, track.Left, track.Top, thumbX - track.Left, track.Height);
        }

        // One label per step, the selected one brighter.
        using (var labelBrush = new SolidBrush(textColor))
        using (var selectedBrush = new SolidBrush(Enabled ? Color.White : DisabledColor))
        {
            var format = new StringFormat { Alignment = StringAlignment.Center };

            for (var step = _minimum; step <= _maximum; step += _step)
            {
                graphics.DrawString(
                    step.ToString(),
                    LabelFont,
                    step == _value ? selectedBrush : labelBrush,
                    XAt(step),
                    LabelTop,
                    format);
            }
        }

        // Thumb, with a ring when the keyboard has focus.
        var thumb = new Rectangle(thumbX - ThumbRadius, TrackY - ThumbRadius, ThumbRadius * 2, ThumbRadius * 2);

        using (var thumbBrush = new SolidBrush(accent))
        using (var ringPen = new Pen(Color.White, 2))
        {
            graphics.FillEllipse(thumbBrush, thumb);

            if (Focused && Enabled)
                graphics.DrawEllipse(ringPen, thumb);
        }
    }

    private int TrackWidth => Math.Max(1, Width - ThumbRadius * 2 - 1);

    private int XAt(int value) =>
        ThumbRadius + (int)Math.Round((value - _minimum) / (double)(_maximum - _minimum) * TrackWidth);

    private int ValueAt(int x)
    {
        var fraction = Math.Clamp((x - ThumbRadius) / (double)TrackWidth, 0, 1);
        return Snap(_minimum + (int)Math.Round(fraction * (_maximum - _minimum)));
    }

    private int Snap(int value)
    {
        var clamped = Math.Clamp(value, _minimum, _maximum);
        return _minimum + (int)Math.Round((clamped - _minimum) / (double)_step, MidpointRounding.AwayFromZero) * _step;
    }
}
