using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;

namespace NovaUI;

/// <summary>Platform-independent app runtime: owns the root widget and drives layout/paint/input.</summary>
public sealed class NovaApp(Func<Widget> root, ITextMeasurer text)
{
    private Widget _root = root();
    private bool _dirty = true;

    public bool NeedsRedraw => _dirty;

    public FocusManager Focus { get; } = new();

    /// <summary>Discard the widget tree and rebuild it from the root factory.</summary>
    public void Rebuild() { _root = root(); _dirty = true; }

    public void Render(ICanvas canvas, Size viewport)
    {
        _root.Layout(Constraints.Loose(viewport), new BuildContext(text, Focus));
        _root.Arrange(new Rect(0, 0, viewport.Width, viewport.Height));
        canvas.Clear(Color.White);
        _root.Paint(canvas);
        _dirty = false;
    }

    public void OnKey(KeyEvent e)
    {
        if (Focus.Current?.HandleKey(e) == true) Rebuild();
    }

    public void OnPointer(PointerEvent e)
    {
        if (e.Kind == PointerKind.Down && Focus.Clear()) _dirty = true;

        if (!_root.HandlePointer(e)) return;

        if (e.Kind == PointerKind.Up) Rebuild();
        else _dirty = true;
    }
}
