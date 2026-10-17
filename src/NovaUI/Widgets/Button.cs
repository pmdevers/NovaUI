using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

public sealed class Button(string label, Action onPressed) : Widget
{
    private const float PadX = 16, PadY = 8, FontSize = 14;
    private bool _pressed;

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var t = ctx.Text.Measure(label, FontSize);
        return c.Constrain(new Size(t.Width + PadX * 2, t.Height + PadY * 2));
    }

    public override void Paint(ICanvas canvas)
    {
        canvas.FillRect(Bounds, _pressed ? new Color(25, 118, 210) : Color.Blue, 6);
        canvas.DrawText(label, new Point(Bounds.X + PadX, Bounds.Y + PadY + FontSize), FontSize, Color.White);
    }

    public override bool HandlePointer(PointerEvent e)
    {
        if (!Bounds.Contains(e.Position))
        {
            var wasPressed = _pressed;
            _pressed = false;
            return wasPressed;
        }

        switch (e.Kind)
        {
            case PointerKind.Down: _pressed = true; return true;
            case PointerKind.Up when _pressed: _pressed = false; onPressed(); return true;
        }
        return false;
    }
}
