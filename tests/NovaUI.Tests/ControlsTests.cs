using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;
using Xunit;

namespace NovaUI.Tests;

public class ControlsTests
{
    private sealed class Measurer : ITextMeasurer
    {
        public Size Measure(string text, float fontSize) => new(text.Length * 10, fontSize);
    }

    private sealed class Canvas : ICanvas
    {
        public int Images;
        public void Clear(Color color) { }
        public void FillRect(Rect rect, Color color, float cornerRadius = 0) { }
        public void DrawText(string text, Point origin, float fontSize, Color color) { }
        public void DrawImage(Image image, Rect dest) => Images++;
        public void Save() { }
        public void Restore() { }
        public void ClipRect(Rect rect) { }
    }

    private static T Place<T>(T w, float x = 0, float y = 0) where T : Widget
    {
        var s = w.Layout(Constraints.Loose(new Size(500, 500)), new BuildContext(new Measurer()));
        w.Arrange(new Rect(x, y, s.Width, s.Height));
        return w;
    }

    private static void Click(Widget w, Point p)
    {
        w.HandlePointer(new PointerEvent(PointerKind.Down, p));
        w.HandlePointer(new PointerEvent(PointerKind.Up, p));
    }

    [Fact]
    public void Checkbox_toggles_on_click()
    {
        bool? result = null;
        var w = Place(new Checkbox("x", false, v => result = v));
        Click(w, new Point(5, 5));
        Assert.True(result);
    }

    [Fact]
    public void Radio_reports_its_value()
    {
        var picked = "";
        var w = Place(new RadioBox<string>("Large", "L", "M", v => picked = v));
        Click(w, new Point(5, 5));
        Assert.Equal("L", picked);
    }

    [Fact]
    public void Slider_drag_maps_position_to_value_with_step()
    {
        float value = 0;
        var w = Place(new Slider(0, 0, 100, v => value = v, width: 118, step: 10));
        w.HandlePointer(new PointerEvent(PointerKind.Down, new Point(9, 9)));
        Assert.Equal(0, value);
        w.HandlePointer(new PointerEvent(PointerKind.Move, new Point(59, 9)));
        Assert.Equal(50, value);
        w.HandlePointer(new PointerEvent(PointerKind.Move, new Point(500, 9)));
        Assert.Equal(100, value);
        Assert.True(w.HandlePointer(new PointerEvent(PointerKind.Up, new Point(500, 9))));
    }

    [Fact]
    public void ComboBox_opens_and_selects()
    {
        var state = new ComboBoxState(["a", "b", "c"]);
        var w = Place(new ComboBox(state));
        Click(w, new Point(10, 10));
        Assert.True(state.IsOpen);

        w = Place(new ComboBox(state));
        Assert.True(w.Bounds.Height > 28);
        Click(w, new Point(10, 28 + 2 + 1 + 28 + 5)); // second row
        Assert.False(state.IsOpen);
        Assert.Equal("b", state.SelectedItem);
    }

    [Fact]
    public void ComboBox_closes_when_clicking_outside()
    {
        var state = new ComboBoxState(["a"]);
        state.IsOpen = true;
        var w = Place(new ComboBox(state));
        Assert.True(w.HandlePointer(new PointerEvent(PointerKind.Down, new Point(400, 400))));
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void ImageButton_draws_image_and_fires()
    {
        var pressed = false;
        var img = new Image(1, 1, [255, 0, 0, 255]);
        var w = Place(new ImageButton(img, () => pressed = true, "Go"));
        var canvas = new Canvas();
        w.Paint(canvas);
        Assert.Equal(1, canvas.Images);
        Click(w, new Point(5, 5));
        Assert.True(pressed);
    }
}
