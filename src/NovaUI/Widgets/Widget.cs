using NovaUI.Input;
using NovaUI.Rendering;

namespace NovaUI.Widgets;

/// <summary>Context handed to widgets during layout and paint.</summary>
public sealed record BuildContext(ITextMeasurer Text, FocusManager? Focus = null);

/// <summary>Base class for every UI element. Widgets are declared in code.</summary>
public abstract class Widget
{
    public Rect Bounds { get; protected set; }

    /// <summary>Application services (window manager, ...).</summary>
    public AppContext Context => AppContext.Current;

    /// <summary>Compute and return the size this widget wants within the constraints.</summary>
    public abstract Size Layout(Constraints constraints, BuildContext context);

    /// <summary>Position the widget; called after Layout.</summary>
    public virtual void Arrange(Rect bounds) => Bounds = bounds;

    public abstract void Paint(ICanvas canvas);

    /// <summary>Returns true if the event was handled.</summary>
    public virtual bool HandlePointer(PointerEvent e) => false;
}

/// <summary>Widget that composes other widgets by returning them from Build().</summary>
public abstract class CompositeWidget : Widget
{
    private Widget? _child;

    protected abstract Widget Build();

    /// <summary>Discard the cached child so the next layout rebuilds it.</summary>
    public void Invalidate() => _child = null;

    private Widget Child => _child ??= Build();

    public override Size Layout(Constraints c, BuildContext ctx) => Child.Layout(c, ctx);
    public override void Arrange(Rect bounds) { base.Arrange(bounds); Child.Arrange(bounds); }
    public override void Paint(ICanvas canvas) => Child.Paint(canvas);
    public override bool HandlePointer(PointerEvent e) => Child.HandlePointer(e);
}
