using System.Reflection;
using NovaUI.Desktop.Rendering;
using Xunit;

namespace NovaUI.Tests;

public class DependencyTests
{
    private static bool ReferencesSkia(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("SkiaSharp", StringComparison.Ordinal)
                                                 || a.Name.StartsWith("NovaUI.Skia", StringComparison.Ordinal));

    [Fact]
    public void Core_does_not_reference_Skia() => Assert.False(ReferencesSkia(typeof(Window).Assembly));

    [Fact]
    public void Desktop_does_not_reference_Skia() => Assert.False(ReferencesSkia(typeof(SoftwareRasterizer).Assembly));

    [Fact]
    public void Software_rasterizer_draws_without_Skia()
    {
        using var surface = new SoftwareRasterizer().CreateSurface(10, 10);
        surface.Canvas.Clear(Color.White);
        surface.Canvas.FillRect(new Rect(2, 2, 4, 4), new Color(255, 0, 0));

        unsafe
        {
            var px = (byte*)surface.Pixels;
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, new[] { px[3 * 40 + 12], px[3 * 40 + 13], px[3 * 40 + 14], px[3 * 40 + 15] });
            Assert.Equal(255, px[0]);
        }
    }

    [Fact]
    public void Software_rasterizer_measures_text_proportionally()
    {
        var measurer = new SoftwareRasterizer().TextMeasurer;
        Assert.True(measurer.Measure("Hello", 20).Width > measurer.Measure("Hi", 20).Width);
        Assert.True(measurer.Measure("Hello", 40).Width > measurer.Measure("Hello", 20).Width);
    }
}
