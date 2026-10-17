using System;
using System.Linq;
using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

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

    public override bool HandlePointer(PointerEvent e)
    {
        for (var i = children.Length - 1; i >= 0; i--)
            if (children[i].HandlePointer(e)) return true;

        return false;
    }
}


