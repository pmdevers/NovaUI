using NovaUI;
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
}
