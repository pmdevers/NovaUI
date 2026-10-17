namespace NovaUI.Rendering;

/// <summary>Backend-agnostic drawing surface. Implemented by NovaUI.Skia.</summary>
public interface ICanvas
{
    void Clear(Color color);
    void FillRect(Rect rect, Color color, float cornerRadius = 0);
    void DrawText(string text, Point origin, float fontSize, Color color);
    /// <summary>Draws the image scaled to <paramref name="dest"/>. Default: per-pixel fallback.</summary>
    void DrawImage(Image image, Rect dest)
    {
        var w = dest.Width / image.Width;
        var h = dest.Height / image.Height;
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
            {
                var c = image.GetPixel(x, y);
                if (c.A > 0) FillRect(new Rect(dest.X + x * w, dest.Y + y * h, w, h), c);
            }
    }
    void Save();
    void Restore();
    void ClipRect(Rect rect);
}

/// <summary>Measures text without needing a live canvas.</summary>
public interface ITextMeasurer
{
    Size Measure(string text, float fontSize);
}
