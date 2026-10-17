using NovaUI.Rendering;
using SkiaSharp;

namespace NovaUI.Skia;

public sealed class SkiaCanvas(SKCanvas canvas) : ICanvas
{
    private static SKColor Map(Color c) => new(c.R, c.G, c.B, c.A);

    public void Clear(Color color) => canvas.Clear(Map(color));

    public void FillRect(Rect r, Color color, float cornerRadius = 0)
    {
        using var paint = new SKPaint { Color = Map(color), IsAntialias = true };
        var rect = new SKRect(r.X, r.Y, r.Right, r.Bottom);
        if (cornerRadius > 0) canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, paint);
        else canvas.DrawRect(rect, paint);
    }

    public void DrawText(string text, Point origin, float fontSize, Color color)
    {
        using var font = new SKFont(SkiaTextMeasurer.SharedTypeface) { Size = fontSize };
        using var paint = new SKPaint { Color = Map(color), IsAntialias = true };
        canvas.DrawText(text, origin.X, origin.Y, SKTextAlign.Left, font, paint);
    }

    public void DrawImage(Image image, Rect r)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var skImage = SKImage.FromPixelCopy(info, image.Rgba);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(skImage, new SKRect(r.X, r.Y, r.Right, r.Bottom), new SKSamplingOptions(SKFilterMode.Linear), paint);
    }

    public void Save() => canvas.Save();
    public void Restore() => canvas.Restore();
    public void ClipRect(Rect r) => canvas.ClipRect(new SKRect(r.X, r.Y, r.Right, r.Bottom));
}

public sealed class SkiaTextMeasurer : ITextMeasurer
{
    internal static SKTypeface SharedTypeface { get; } =
        SKFontManager.Default.MatchFamily("Segoe UI Symbol")
        ?? SKFontManager.Default.MatchFamily("Segoe UI")
        ?? SKTypeface.Default;

    public Size Measure(string text, float fontSize)
    {
        using var font = new SKFont(SharedTypeface) { Size = fontSize };
        var width = font.MeasureText(text);
        return new Size(width, fontSize * 1.2f);
    }
}
