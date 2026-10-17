using NovaUI.Desktop.Rendering;
using NovaUI.Rendering;
using SkiaSharp;

namespace NovaUI.Skia;

/// <summary>SkiaSharp-backed <see cref="IRasterizer"/>: pass it to <c>App.RunAsync</c> for full-quality text and anti-aliasing.</summary>
public sealed class SkiaRasterizer : IRasterizer
{
    public ITextMeasurer TextMeasurer { get; } = new SkiaTextMeasurer();

    public IRasterSurface CreateSurface(int width, int height) => new Surface(width, height);

    private sealed class Surface : IRasterSurface
    {
        private readonly SKBitmap _bitmap;
        private readonly SKCanvas _skCanvas;

        public Surface(int width, int height)
        {
            _bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            _skCanvas = new SKCanvas(_bitmap);
            Canvas = new SkiaCanvas(_skCanvas);
        }

        public ICanvas Canvas { get; }
        public nint Pixels => _bitmap.GetPixels();
        public int RowBytes => _bitmap.RowBytes;

        public void Dispose()
        {
            _skCanvas.Dispose();
            _bitmap.Dispose();
        }
    }
}
