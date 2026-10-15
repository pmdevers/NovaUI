using NovaUI;
using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;
using Xunit;

namespace NovaUI.Tests;

public class LayoutTests
{
    private sealed class FakeMeasurer : ITextMeasurer
    {
        public Size Measure(string text, float fontSize) => new(text.Length * 10, fontSize);
    }

    private sealed class FakeCanvas : ICanvas
    {
        public void Clear(Color color) { }
        public void FillRect(Rect rect, Color color, float cornerRadius = 0) { }
        public void DrawText(string text, Point origin, float fontSize, Color color) { }
        public void Save() { }
        public void Restore() { }
        public void ClipRect(Rect rect) { }
    }

    [Fact]
    public void Padding_adds_insets_to_child_size()
    {
        var w = new Padding(EdgeInsets.All(5), new Text("abcd", 10));
        var size = w.Layout(Constraints.Loose(new Size(500, 500)), new BuildContext(new FakeMeasurer()));
        Assert.Equal(new Size(50, 20), size);
    }

    [Fact]
    public void State_raises_changed_only_on_change()
    {
        var s = new State<int>(1);
        var n = 0;
        s.Changed += () => n++;
        s.Value = 1;
        s.Value = 2;
        Assert.Equal(1, n);
    }

    [Fact]
    public void Button_click_survives_redraw_between_press_and_release()
    {
        var clicks = 0;
        var app = new NovaApp(() => new Button("Click", () => clicks++), new FakeMeasurer());
        var canvas = new FakeCanvas();
        var position = new Point(5, 5);

        app.Render(canvas, new Size(100, 100));
        app.OnPointer(new PointerEvent(PointerKind.Down, position));
        app.Render(canvas, new Size(100, 100));
        app.OnPointer(new PointerEvent(PointerKind.Up, position));

        Assert.Equal(1, clicks);
    }
}
