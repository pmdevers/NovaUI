namespace NovaUI.Desktop.Rendering;

/// <summary>Receives one covered pixel, relative to the text origin (baseline-left). Coverage is 0-255.</summary>
internal delegate void GlyphBlit(int dx, int dy, byte coverage);

/// <summary>Native, per-OS glyph rendering used by <see cref="SoftwareRasterizer"/>.</summary>
internal interface ITextEngine
{
    float Measure(string text, float fontSize);
    void Rasterize(string text, float fontSize, GlyphBlit blit);
}

internal static class TextEngineFactory
{
    /// <summary>Returns the native engine for this OS, or null when unavailable (placeholder blocks are used).</summary>
    public static ITextEngine? TryCreate()
    {
        try
        {
            if (OperatingSystem.IsLinux())
                return new Platforms.Linux.FreeTypeTextEngine();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
        }

        return null;
    }
}
