using NovaUI.Rendering;

namespace NovaUI.Widgets;

public sealed class Text(string value, float fontSize = 14, Color? color = null) : Widget
{
    public override Size Layout(Constraints c, BuildContext ctx) =>
        c.Constrain(ctx.Text.Measure(value, fontSize));

    public override void Paint(ICanvas canvas) =>
        canvas.DrawText(value, new Point(Bounds.X, Bounds.Y + fontSize), fontSize, color ?? Color.Black);
}
