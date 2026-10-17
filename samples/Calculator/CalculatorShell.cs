using NovaUI;
using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;

sealed class CalculatorShell(Widget content) : Widget
{
    private readonly MenuBar _menuBar = new(
        new MenuBarMenu("File",
            new MenuBarItem("New"),
            new MenuBarItem("Open", () => NovaUI.AppContext.Current.Windows.Open<CalculatorWindow>()),
            new MenuBarItem("Save"),
            new MenuBarItem("Exit", () => NovaUI.AppContext.Current.Windows.CloseAll())),
        new MenuBarMenu("Edit",
            new MenuBarItem("Undo"),
            new MenuBarItem("Redo"),
            new MenuBarItem("Cut"),
            new MenuBarItem("Copy"),
            new MenuBarItem("Paste")),
        new MenuBarMenu("View",
            new MenuBarItem("Standard"),
            new MenuBarItem("Scientific"),
            new MenuBarItem("Compact overlay")));

    private readonly Widget _content = content;
    private Size _menuSize;
    private Rect _contentBounds;

    public override Size Layout(Constraints constraints, BuildContext context)
    {
        _menuSize = _menuBar.Layout(constraints, context);
        var contentHeight = Math.Max(0, constraints.MaxHeight - _menuSize.Height);
        var contentSize = _content.Layout(new Constraints(0, constraints.MaxWidth, 0, contentHeight), context);
        return constraints.Constrain(new Size(Math.Max(_menuSize.Width, contentSize.Width), _menuSize.Height + contentSize.Height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        _menuBar.Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, _menuSize.Height));
        _contentBounds = new Rect(bounds.X, bounds.Y + _menuSize.Height, bounds.Width, Math.Max(0, bounds.Height - _menuSize.Height));
        _content.Arrange(_contentBounds);
    }

    public override void Paint(ICanvas canvas)
    {
        _content.Paint(canvas);
        _menuBar.Paint(canvas);
    }

    public override bool HandlePointer(PointerEvent e)
    {
        if (_menuBar.HandlePointer(e)) return true;
        return _content.HandlePointer(e);
    }
}
