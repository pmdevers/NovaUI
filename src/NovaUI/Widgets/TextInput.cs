using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

/// <summary>
/// Editable text owned by the app. Keep it outside <c>Build()</c> (like <see cref="State{T}"/>), because the widget tree is rebuilt.
/// </summary>
public sealed class TextInputState(string text = "") : IKeyHandler
{
    private string _text = text;
    private int _caret = text.Length;

    public event Action? Changed;
    public event Action<string>? Submitted;

    public string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (_text == value) return;
            _text = value;
            _caret = Math.Min(_caret, _text.Length);
            Changed?.Invoke();
        }
    }

    /// <summary>Caret position as a UTF-16 index into <see cref="Text"/>.</summary>
    public int Caret
    {
        get => _caret;
        set => _caret = Math.Clamp(value, 0, _text.Length);
    }

    /// <summary>Horizontal scroll of the visible text; maintained by the widget.</summary>
    public float ScrollX { get; set; }

    public bool HandleKey(KeyEvent e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            var typed = new string(e.Text.Where(c => !char.IsControl(c)).ToArray());
            if (typed.Length == 0) return false;
            Replace(_caret, _caret, typed);
            return true;
        }

        switch (e.Key)
        {
            case Key.Backspace:
                if (_caret > 0) Replace(Step(-1), _caret, "");
                return true;
            case Key.Delete:
                if (_caret < _text.Length) Replace(_caret, Step(1), "");
                return true;
            case Key.Left: _caret = Step(-1); return true;
            case Key.Right: _caret = Step(1); return true;
            case Key.Home: _caret = 0; return true;
            case Key.End: _caret = _text.Length; return true;
            case Key.Enter: Submitted?.Invoke(_text); return true;
            default: return false;
        }
    }

    // Moves one character, treating a surrogate pair as one.
    private int Step(int direction)
    {
        var index = Math.Clamp(_caret + direction, 0, _text.Length);
        if (direction < 0 && index > 0 && char.IsLowSurrogate(_text[index]) && char.IsHighSurrogate(_text[index - 1])) index--;
        if (direction > 0 && index < _text.Length && char.IsLowSurrogate(_text[index]) && char.IsHighSurrogate(_text[index - 1])) index++;
        return index;
    }

    private void Replace(int start, int end, string value)
    {
        _text = _text[..start] + value + _text[end..];
        _caret = start + value.Length;
        Changed?.Invoke();
    }
}

/// <summary>Single-line text field. Click to focus; type, backspace/delete, arrows, home/end and enter are supported.</summary>
public sealed class TextInput(TextInputState state, string placeholder = "", float width = 240) : Widget
{
    private const float PadX = 10, PadY = 8, FontSize = 14;
    private ITextMeasurer? _measurer;
    private FocusManager? _focus;

    private Rect Inner => new(Bounds.X + PadX, Bounds.Y, Math.Max(0, Bounds.Width - PadX * 2), Bounds.Height);

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        _measurer = ctx.Text;
        _focus = ctx.Focus;
        return c.Constrain(new Size(width, ctx.Text.Measure("Ag", FontSize).Height + PadY * 2));
    }

    public override void Paint(ICanvas canvas)
    {
        var focused = _focus?.Current == state;
        canvas.FillRect(Bounds, focused ? Color.Blue : new Color(189, 189, 189), 4);
        canvas.FillRect(new Rect(Bounds.X + 1.5f, Bounds.Y + 1.5f, Bounds.Width - 3, Bounds.Height - 3), Color.White, 3);

        var inner = Inner;
        var caretX = _measurer is null ? 0 : _measurer.Measure(state.Text[..state.Caret], FontSize).Width;
        if (caretX - state.ScrollX > inner.Width - 2) state.ScrollX = caretX - inner.Width + 2;
        if (caretX - state.ScrollX < 0) state.ScrollX = caretX;
        if (state.ScrollX < 0) state.ScrollX = 0;

        canvas.Save();
        canvas.ClipRect(inner);
        var baseline = new Point(inner.X - state.ScrollX, Bounds.Y + PadY + FontSize);
        if (state.Text.Length > 0)
            canvas.DrawText(state.Text, baseline, FontSize, Color.Black);
        else if (placeholder.Length > 0)
            canvas.DrawText(placeholder, baseline, FontSize, new Color(150, 150, 150));

        if (focused)
            canvas.FillRect(new Rect(inner.X + caretX - state.ScrollX, Bounds.Y + PadY * 0.75f, 1.5f, FontSize * 1.2f + PadY * 0.5f), Color.Black);
        canvas.Restore();
    }

    public override bool HandlePointer(PointerEvent e)
    {
        if (e.Kind != PointerKind.Down || !Bounds.Contains(e.Position)) return false;

        _focus?.Request(state);
        state.Caret = IndexAt(e.Position.X - Inner.X + state.ScrollX);
        return true;
    }

    private int IndexAt(float x)
    {
        if (_measurer is null) return state.Text.Length;

        var previous = 0f;
        for (var i = 1; i <= state.Text.Length; i++)
        {
            var w = _measurer.Measure(state.Text[..i], FontSize).Width;
            if (x < (previous + w) / 2) return i - 1;
            previous = w;
        }

        return state.Text.Length;
    }
}
