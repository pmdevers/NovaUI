namespace NovaUI.Rendering;

/// <summary>Backend-agnostic drawing surface. Implemented by NovaUI.Skia.</summary>
public interface ICanvas
{
    void Clear(Color color);
    void FillRect(Rect rect, Color color, float cornerRadius = 0);
    void DrawText(string text, Point origin, float fontSize, Color color);
    void Save();
    void Restore();
    void ClipRect(Rect rect);
}

/// <summary>Measures text without needing a live canvas.</summary>
public interface ITextMeasurer
{
    Size Measure(string text, float fontSize);
}
