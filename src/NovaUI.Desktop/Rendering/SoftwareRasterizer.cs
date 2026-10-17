using System.Runtime.InteropServices;
using NovaUI.Rendering;

namespace NovaUI.Desktop.Rendering;

/// <summary>
/// Dependency-free CPU rasterizer used when no other <see cref="IRasterizer"/> is supplied.
/// Fills rectangles (anti-aliased rounded corners) and clips. Text uses the native engine
/// (FreeType on Linux) and falls back to placeholder glyph blocks where none is available.
/// </summary>
public sealed class SoftwareRasterizer : IRasterizer
{
    private readonly ITextEngine? _text;

    public SoftwareRasterizer()
    {
        _text = TextEngineFactory.TryCreate();
        TextMeasurer = _text is null ? new BlockTextMeasurer() : new EngineTextMeasurer(_text);
    }

    public ITextMeasurer TextMeasurer { get; }

    public IRasterSurface CreateSurface(int width, int height) => new Surface(width, height, _text);

    private static float GlyphAdvance(float fontSize) => fontSize * 0.6f;

    private sealed class BlockTextMeasurer : ITextMeasurer
    {
        public Size Measure(string text, float fontSize) => new(text.Length * GlyphAdvance(fontSize), fontSize * 1.2f);
    }

    private sealed class EngineTextMeasurer(ITextEngine engine) : ITextMeasurer
    {
        public Size Measure(string text, float fontSize) => new(engine.Measure(text, fontSize), fontSize * 1.2f);
    }

    private sealed unsafe class Surface : IRasterSurface, ICanvas
    {
        private readonly int _width, _height;
        private readonly byte* _data;
        private readonly ITextEngine? _text;
        private readonly Stack<(int L, int T, int R, int B)> _saved = new();
        private (int L, int T, int R, int B) _clip;

        public Surface(int width, int height, ITextEngine? text)
        {
            _text = text;
            _width = width;
            _height = height;
            RowBytes = width * 4;
            _data = (byte*)NativeMemory.AllocZeroed((nuint)(RowBytes * height));
            _clip = (0, 0, width, height);
        }

        public ICanvas Canvas => this;
        public nint Pixels => (nint)_data;
        public int RowBytes { get; }

        public void Dispose() => NativeMemory.Free(_data);

        public void Clear(Color color)
        {
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                    Blend(x, y, color, 1f, replace: true);
        }

        public void FillRect(Rect r, Color color, float cornerRadius = 0)
        {
            var x0 = Math.Max(_clip.L, (int)MathF.Floor(r.X));
            var y0 = Math.Max(_clip.T, (int)MathF.Floor(r.Y));
            var x1 = Math.Min(_clip.R, (int)MathF.Ceiling(r.Right));
            var y1 = Math.Min(_clip.B, (int)MathF.Ceiling(r.Bottom));
            var radius = Math.Min(cornerRadius, Math.Min(r.Width, r.Height) / 2);

            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var coverage = Math.Min(Overlap(x, r.X, r.Right), 1f) * Math.Min(Overlap(y, r.Y, r.Bottom), 1f);
                    if (radius > 0)
                        coverage *= CornerCoverage(x + 0.5f, y + 0.5f, r, radius);
                    if (coverage > 0)
                        Blend(x, y, color, coverage, replace: false);
                }
        }

        public void DrawText(string text, Point origin, float fontSize, Color color)
        {
            if (_text is not null)
            {
                var ox = (int)MathF.Round(origin.X);
                var oy = (int)MathF.Round(origin.Y);
                _text.Rasterize(text, fontSize, (dx, dy, coverage) =>
                {
                    int x = ox + dx, y = oy + dy;
                    if (x >= _clip.L && x < _clip.R && y >= _clip.T && y < _clip.B)
                        Blend(x, y, color, coverage / 255f, replace: false);
                });
                return;
            }

            var advance = GlyphAdvance(fontSize);
            var glyphHeight = fontSize * 0.7f;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                    continue;
                FillRect(new Rect(origin.X + i * advance + advance * 0.1f, origin.Y - glyphHeight, advance * 0.7f, glyphHeight), color, 1);
            }
        }

        public void DrawImage(Image image, Rect r)
        {
            var x0 = Math.Max(_clip.L, (int)MathF.Floor(r.X));
            var y0 = Math.Max(_clip.T, (int)MathF.Floor(r.Y));
            var x1 = Math.Min(_clip.R, (int)MathF.Ceiling(r.Right));
            var y1 = Math.Min(_clip.B, (int)MathF.Ceiling(r.Bottom));
            if (r.Width <= 0 || r.Height <= 0) return;

            for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var sx = Math.Clamp((int)((x + 0.5f - r.X) / r.Width * image.Width), 0, image.Width - 1);
                    var sy = Math.Clamp((int)((y + 0.5f - r.Y) / r.Height * image.Height), 0, image.Height - 1);
                    var c = image.GetPixel(sx, sy);
                    if (c.A > 0) Blend(x, y, c, 1f, replace: false);
                }
        }

        public void Save() => _saved.Push(_clip);

        public void Restore()
        {
            if (_saved.Count > 0)
                _clip = _saved.Pop();
        }

        public void ClipRect(Rect r) => _clip = (
            Math.Max(_clip.L, (int)MathF.Floor(r.X)),
            Math.Max(_clip.T, (int)MathF.Floor(r.Y)),
            Math.Min(_clip.R, (int)MathF.Ceiling(r.Right)),
            Math.Min(_clip.B, (int)MathF.Ceiling(r.Bottom)));

        private static float Overlap(int pixel, float start, float end) =>
            Math.Max(0, Math.Min(pixel + 1, end) - Math.Max(pixel, start));

        private static float CornerCoverage(float px, float py, Rect r, float radius)
        {
            var cx = Math.Clamp(px, r.X + radius, r.Right - radius);
            var cy = Math.Clamp(py, r.Y + radius, r.Bottom - radius);
            var distance = MathF.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Math.Clamp(radius - distance + 0.5f, 0f, 1f);
        }

        private void Blend(int x, int y, Color c, float coverage, bool replace)
        {
            var p = _data + y * RowBytes + x * 4;
            var a = c.A / 255f * coverage;
            if (replace)
                a = c.A / 255f;

            var inverse = replace ? 0f : 1f - a;
            p[0] = (byte)(c.B * a + p[0] * inverse);
            p[1] = (byte)(c.G * a + p[1] * inverse);
            p[2] = (byte)(c.R * a + p[2] * inverse);
            p[3] = (byte)(255 * a + p[3] * inverse);
        }
    }
}
