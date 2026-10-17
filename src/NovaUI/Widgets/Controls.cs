using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

internal static class ControlStyle
{
    public static readonly Color Border = new(117, 117, 117);
    public static readonly Color Track = new(224, 224, 224);
    public static readonly Color Hover = new(227, 242, 253);
    public const float FontSize = 14;
    public const float BoxSize = 18;
    public const float Gap = 8;
}

/// <summary>Base for widgets that activate on a full click (Down and Up inside).</summary>
public abstract class ClickableWidget : Widget
{
    private bool _down;

    protected bool IsPressed => _down;
    protected abstract void Activate();

    public override bool HandlePointer(PointerEvent e)
    {
        var inside = Bounds.Contains(e.Position);
        switch (e.Kind)
        {
            case PointerKind.Down when inside:
                _down = true;
                return true;
            case PointerKind.Up when _down:
                _down = false;
                if (inside) Activate();
                return true;
        }
        return false;
    }
}

/// <summary>Labelled check box. The caller owns the value and updates it in <paramref name="onChanged"/>.</summary>
public sealed class Checkbox(string label, bool isChecked, Action<bool> onChanged) : ClickableWidget
{
    protected override void Activate() => onChanged(!isChecked);

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var t = ctx.Text.Measure(label, ControlStyle.FontSize);
        return c.Constrain(new Size(ControlStyle.BoxSize + ControlStyle.Gap + t.Width, Math.Max(ControlStyle.BoxSize, t.Height)));
    }

    public override void Paint(ICanvas canvas)
    {
        const float s = ControlStyle.BoxSize;
        var box = new Rect(Bounds.X, Bounds.Y + (Bounds.Height - s) / 2, s, s);
        if (isChecked)
        {
            canvas.FillRect(box, Color.Blue, 3);
            // tick: two line segments drawn as small squares
            Span<(float X, float Y)> pts = [(4, 9), (7.5f, 12.5f), (14, 5.5f)];
            for (var seg = 0; seg < 2; seg++)
                for (var i = 0; i <= 8; i++)
                {
                    var f = i / 8f;
                    var x = pts[seg].X + (pts[seg + 1].X - pts[seg].X) * f;
                    var y = pts[seg].Y + (pts[seg + 1].Y - pts[seg].Y) * f;
                    canvas.FillRect(new Rect(box.X + x - 1, box.Y + y - 1, 2.2f, 2.2f), Color.White, 1);
                }
        }
        else
        {
            canvas.FillRect(box, ControlStyle.Border, 3);
            canvas.FillRect(new Rect(box.X + 2, box.Y + 2, s - 4, s - 4), IsPressed ? ControlStyle.Hover : Color.White, 2);
        }
        canvas.DrawText(label, new Point(box.Right + ControlStyle.Gap, Bounds.Y + (Bounds.Height - ControlStyle.FontSize * 1.2f) / 2 + ControlStyle.FontSize), ControlStyle.FontSize, Color.Black);
    }
}

/// <summary>
/// Labelled radio button. Radios sharing a group are wired by passing the same selected value;
/// <paramref name="onSelected"/> fires with this option's value.
/// </summary>
public sealed class RadioBox<T>(string label, T value, T selected, Action<T> onSelected) : ClickableWidget
{
    protected override void Activate() => onSelected(value);

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var t = ctx.Text.Measure(label, ControlStyle.FontSize);
        return c.Constrain(new Size(ControlStyle.BoxSize + ControlStyle.Gap + t.Width, Math.Max(ControlStyle.BoxSize, t.Height)));
    }

    public override void Paint(ICanvas canvas)
    {
        const float s = ControlStyle.BoxSize;
        var box = new Rect(Bounds.X, Bounds.Y + (Bounds.Height - s) / 2, s, s);
        var on = EqualityComparer<T>.Default.Equals(value, selected);
        canvas.FillRect(box, on ? Color.Blue : ControlStyle.Border, s / 2);
        canvas.FillRect(new Rect(box.X + 2, box.Y + 2, s - 4, s - 4), IsPressed ? ControlStyle.Hover : Color.White, (s - 4) / 2);
        if (on)
            canvas.FillRect(new Rect(box.X + 5, box.Y + 5, s - 10, s - 10), Color.Blue, (s - 10) / 2);
        canvas.DrawText(label, new Point(box.Right + ControlStyle.Gap, Bounds.Y + (Bounds.Height - ControlStyle.FontSize * 1.2f) / 2 + ControlStyle.FontSize), ControlStyle.FontSize, Color.Black);
    }
}

/// <summary>Horizontal slider. Dragging reports values continuously through <paramref name="onChanged"/>.</summary>
public sealed class Slider(float value, float min, float max, Action<float> onChanged, float width = 200, float? step = null) : Widget
{
    private const float Thumb = 18, TrackHeight = 4;
    private float _value = Math.Clamp(value, min, max);
    private bool _dragging;

    public override Size Layout(Constraints c, BuildContext ctx) => c.Constrain(new Size(width, Thumb));

    public override void Paint(ICanvas canvas)
    {
        var travel = Math.Max(1, Bounds.Width - Thumb);
        var t = max > min ? (_value - min) / (max - min) : 0;
        var cx = Bounds.X + Thumb / 2 + travel * t;
        var ty = Bounds.Y + (Bounds.Height - TrackHeight) / 2;
        canvas.FillRect(new Rect(Bounds.X + Thumb / 2, ty, travel, TrackHeight), ControlStyle.Track, TrackHeight / 2);
        canvas.FillRect(new Rect(Bounds.X + Thumb / 2, ty, cx - Bounds.X - Thumb / 2, TrackHeight), Color.Blue, TrackHeight / 2);
        var thumb = new Rect(cx - Thumb / 2, Bounds.Y + (Bounds.Height - Thumb) / 2, Thumb, Thumb);
        canvas.FillRect(thumb, new Color(25, 118, 210), Thumb / 2);
        canvas.FillRect(new Rect(thumb.X + 2, thumb.Y + 2, Thumb - 4, Thumb - 4), _dragging ? Color.Blue : Color.White, (Thumb - 4) / 2);
    }

    public override bool HandlePointer(PointerEvent e)
    {
        switch (e.Kind)
        {
            case PointerKind.Down when Bounds.Contains(e.Position):
                _dragging = true;
                Update(e.Position.X);
                return true;
            case PointerKind.Move when _dragging:
                Update(e.Position.X);
                return true;
            case PointerKind.Up when _dragging:
                _dragging = false;
                return true;
        }
        return false;
    }

    private void Update(float x)
    {
        var travel = Math.Max(1, Bounds.Width - Thumb);
        var t = Math.Clamp((x - Bounds.X - Thumb / 2) / travel, 0, 1);
        var v = min + t * (max - min);
        if (step is > 0)
            v = Math.Clamp(min + MathF.Round((v - min) / step.Value) * step.Value, min, max);
        if (v == _value) return;
        _value = v;
        onChanged(v);
    }
}

/// <summary>Selection and open/closed state of a <see cref="ComboBox"/>. Keep it outside Build().</summary>
public sealed class ComboBoxState(IReadOnlyList<string> items, int selectedIndex = -1)
{
    private int _selected = selectedIndex;

    public IReadOnlyList<string> Items { get; } = items;
    public bool IsOpen { get; set; }
    public event Action<int>? SelectionChanged;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == _selected) return;
            _selected = value;
            SelectionChanged?.Invoke(value);
        }
    }

    public string? SelectedItem => _selected >= 0 ? Items[_selected] : null;
}

/// <summary>Drop-down list. When open, the list expands inline and pushes following widgets down.</summary>
public sealed class ComboBox(ComboBoxState state, string placeholder = "Select…", float width = 200) : Widget
{
    private const float RowHeight = 28, PadX = 10;
    private bool _down;
    private int _hover = -1;

    public override Size Layout(Constraints c, BuildContext ctx) =>
        c.Constrain(new Size(width, RowHeight + (state.IsOpen ? state.Items.Count * RowHeight + 2 : 0)));

    public override void Paint(ICanvas canvas)
    {
        var header = new Rect(Bounds.X, Bounds.Y, Bounds.Width, RowHeight);
        canvas.FillRect(header, state.IsOpen ? Color.Blue : ControlStyle.Border, 4);
        canvas.FillRect(new Rect(header.X + 1, header.Y + 1, header.Width - 2, header.Height - 2), Color.White, 3);
        var text = state.SelectedItem ?? placeholder;
        canvas.Save();
        canvas.ClipRect(new Rect(header.X, header.Y, header.Width - 28, header.Height));
        canvas.DrawText(text, new Point(header.X + PadX, header.Y + 19), ControlStyle.FontSize,
            state.SelectedItem is null ? ControlStyle.Border : Color.Black);
        canvas.Restore();

        // arrow: triangle built from shrinking rows
        var ax = header.Right - 20;
        for (var i = 0; i < 5; i++)
        {
            var w = 10 - i * 2;
            var y = state.IsOpen ? header.Y + 15 - i : header.Y + 11 + i;
            canvas.FillRect(new Rect(ax + i, y, w, 1), Color.Black);
        }

        if (!state.IsOpen) return;
        var list = new Rect(Bounds.X, Bounds.Y + RowHeight + 2, Bounds.Width, state.Items.Count * RowHeight);
        canvas.FillRect(list, ControlStyle.Border, 4);
        canvas.FillRect(new Rect(list.X + 1, list.Y + 1, list.Width - 2, list.Height - 2), Color.White, 3);
        for (var i = 0; i < state.Items.Count; i++)
        {
            var row = new Rect(list.X + 1, list.Y + 1 + i * RowHeight, list.Width - 2, RowHeight - (i == state.Items.Count - 1 ? 2 : 0));
            if (i == _hover) canvas.FillRect(row, ControlStyle.Hover, 3);
            else if (i == state.SelectedIndex) canvas.FillRect(row, new Color(240, 240, 240), 3);
            canvas.DrawText(state.Items[i], new Point(row.X + PadX - 1, row.Y + 19), ControlStyle.FontSize, Color.Black);
        }
    }

    private int ItemAt(Point p)
    {
        var y = p.Y - (Bounds.Y + RowHeight + 2) - 1;
        if (y < 0 || !Bounds.Contains(p)) return -1;
        var i = (int)(y / RowHeight);
        return i < state.Items.Count ? i : -1;
    }

    public override bool HandlePointer(PointerEvent e)
    {
        var inside = Bounds.Contains(e.Position);
        switch (e.Kind)
        {
            case PointerKind.Move when state.IsOpen:
                var hover = ItemAt(e.Position);
                if (hover == _hover) return false;
                _hover = hover;
                return true;
            case PointerKind.Down when inside:
                _down = true;
                return true;
            case PointerKind.Down when state.IsOpen:
                state.IsOpen = false;
                _hover = -1;
                return true;
            case PointerKind.Up when _down:
                _down = false;
                if (!inside) return true;
                if (e.Position.Y < Bounds.Y + RowHeight)
                    state.IsOpen = !state.IsOpen;
                else if (state.IsOpen && ItemAt(e.Position) is var i and >= 0)
                {
                    state.IsOpen = false;
                    _hover = -1;
                    state.SelectedIndex = i;
                }
                return true;
        }
        return false;
    }
}

/// <summary>Button showing an image and an optional label.</summary>
public sealed class ImageButton(Image image, Action onPressed, string? label = null, float imageSize = 24) : ClickableWidget
{
    private const float Pad = 8;

    protected override void Activate() => onPressed();

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var w = imageSize + Pad * 2;
        var h = imageSize + Pad * 2;
        if (label is not null)
        {
            var t = ctx.Text.Measure(label, ControlStyle.FontSize);
            w += ControlStyle.Gap + t.Width;
            h = Math.Max(h, t.Height + Pad * 2);
        }
        return c.Constrain(new Size(w, h));
    }

    public override void Paint(ICanvas canvas)
    {
        canvas.FillRect(Bounds, IsPressed ? new Color(189, 189, 189) : new Color(238, 238, 238), 6);
        canvas.DrawImage(image, new Rect(Bounds.X + Pad, Bounds.Y + (Bounds.Height - imageSize) / 2, imageSize, imageSize));
        if (label is not null)
            canvas.DrawText(label, new Point(Bounds.X + Pad + imageSize + ControlStyle.Gap, Bounds.Y + (Bounds.Height - ControlStyle.FontSize * 1.2f) / 2 + ControlStyle.FontSize),
                ControlStyle.FontSize, Color.Black);
    }
}
