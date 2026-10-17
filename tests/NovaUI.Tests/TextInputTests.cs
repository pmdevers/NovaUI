using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Widgets;
using Xunit;

namespace NovaUI.Tests;

public class TextInputTests
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

    private static KeyEvent Type(string text) => new(Key.None, text);

    [Fact]
    public void Typing_inserts_at_caret()
    {
        var state = new TextInputState("ac");
        state.Caret = 1;
        state.HandleKey(Type("b"));
        Assert.Equal("abc", state.Text);
        Assert.Equal(2, state.Caret);
    }

    [Fact]
    public void Backspace_and_delete_remove_characters()
    {
        var state = new TextInputState("abc");
        state.HandleKey(new KeyEvent(Key.Backspace));
        Assert.Equal("ab", state.Text);
        state.HandleKey(new KeyEvent(Key.Home));
        state.HandleKey(new KeyEvent(Key.Delete));
        Assert.Equal("b", state.Text);
    }

    [Fact]
    public void Surrogate_pairs_are_edited_as_one_character()
    {
        var state = new TextInputState("a😀");
        state.HandleKey(new KeyEvent(Key.Backspace));
        Assert.Equal("a", state.Text);
    }

    [Fact]
    public void Control_characters_are_ignored_and_enter_submits()
    {
        var state = new TextInputState();
        string? submitted = null;
        state.Submitted += s => submitted = s;
        Assert.False(state.HandleKey(Type("\u0001")));
        state.HandleKey(Type("hi"));
        state.HandleKey(new KeyEvent(Key.Enter));
        Assert.Equal("hi", submitted);
    }

    [Fact]
    public void Clicking_focuses_and_keys_reach_the_input_and_clicking_elsewhere_blurs()
    {
        var state = new TextInputState();
        var app = new NovaApp(() => new Padding(EdgeInsets.All(10), new TextInput(state, width: 200)), new FakeMeasurer());
        app.Render(new FakeCanvas(), new Size(400, 300));

        app.OnPointer(new PointerEvent(PointerKind.Down, new Point(50, 20)));
        Assert.Same(state, app.Focus.Current);

        app.OnKey(Type("x"));
        Assert.Equal("x", state.Text);

        app.OnPointer(new PointerEvent(PointerKind.Down, new Point(350, 250)));
        Assert.Null(app.Focus.Current);
        app.OnKey(Type("y"));
        Assert.Equal("x", state.Text);
    }

    [Fact]
    public void Clicking_positions_the_caret()
    {
        var state = new TextInputState("hello");
        var app = new NovaApp(() => new TextInput(state, width: 200), new FakeMeasurer());
        app.Render(new FakeCanvas(), new Size(400, 300));

        // Text starts 10px in; each char is 10px wide, so x=10+22 is between 2 and 3 characters.
        app.OnPointer(new PointerEvent(PointerKind.Down, new Point(32, 10)));
        Assert.Equal(2, state.Caret);
    }
}
