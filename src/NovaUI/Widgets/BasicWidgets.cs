using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

public sealed class Text(string value, float fontSize = 14, Color? color = null) : Widget
{
    public override Size Layout(Constraints c, BuildContext ctx) =>
        c.Constrain(ctx.Text.Measure(value, fontSize));

    public override void Paint(ICanvas canvas) =>
        canvas.DrawText(value, new Point(Bounds.X, Bounds.Y + fontSize), fontSize, color ?? Color.Black);
}

public sealed class Padding(EdgeInsets insets, Widget child) : Widget
{
    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var s = child.Layout(c.Deflate(insets), ctx);
        return c.Constrain(new Size(s.Width + insets.Horizontal, s.Height + insets.Vertical));
    }

    public override void Arrange(Rect b)
    {
        base.Arrange(b);
        child.Arrange(new Rect(b.X + insets.Left, b.Y + insets.Top, b.Width - insets.Horizontal, b.Height - insets.Vertical));
    }

    public override void Paint(ICanvas canvas) => child.Paint(canvas);
    public override bool HandlePointer(PointerEvent e) => child.HandlePointer(e);
}

public sealed class Column(params Widget[] children) : Widget
{
    private Size[] _sizes = [];

    public override Size Layout(Constraints c, BuildContext ctx)
    {
        var loose = new Constraints(0, c.MaxWidth, 0, float.PositiveInfinity);
        _sizes = children.Select(w => w.Layout(loose, ctx)).ToArray();
        return c.Constrain(new Size(_sizes.Max(s => s.Width), _sizes.Sum(s => s.Height)));
    }

    public override void Arrange(Rect b)
    {
        base.Arrange(b);
        var y = b.Y;
        for (var i = 0; i < children.Length; i++)
        {
            children[i].Arrange(new Rect(b.X, y, _sizes[i].Width, _sizes[i].Height));
            y += _sizes[i].Height;
        }
    }

    public override void Paint(ICanvas canvas) { foreach (var c in children) c.Paint(canvas); }

    public override bool HandlePointer(PointerEvent e) =>
        children.Reverse().Any(c => c.HandlePointer(e));
}

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
        if (!Bounds.Contains(e.Position)) { _pressed = false; return false; }
        switch (e.Kind)
        {
            case PointerKind.Down: _pressed = true; return true;
            case PointerKind.Up when _pressed: _pressed = false; onPressed(); return true;
        }
        return false;
    }
}
