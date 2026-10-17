using System.Runtime.InteropServices;
using NovaUI.Desktop.Rendering;

namespace NovaUI.Desktop.Platforms.Linux;

/// <summary>Text through FreeType, with the font located by fontconfig. Offsets assume 64-bit Linux.</summary>
internal sealed unsafe class FreeTypeTextEngine : ITextEngine
{
    private const string FreeType = "libfreetype.so.6";
    private const string FontConfig = "libfontconfig.so.1";

    private const int FaceGlyphSlot = 152;
    private const int SlotAdvanceX = 128;
    private const int SlotBitmapRows = 152, SlotBitmapWidth = 156, SlotBitmapPitch = 160, SlotBitmapBuffer = 168;
    private const int SlotBitmapLeft = 192, SlotBitmapTop = 196;
    private const int LoadRender = 4;

    private static readonly string[] FallbackFonts =
    [
        "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
        "/usr/share/fonts/TTF/DejaVuSans.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/noto/NotoSans-Regular.ttf",
    ];

    private readonly nint _library;
    private readonly nint _face;
    private readonly Dictionary<(int Size, char Ch), Glyph> _cache = [];
    private int _currentSize = -1;

    private sealed record Glyph(int Left, int Top, int Width, int Rows, byte[] Coverage, float Advance);

    public FreeTypeTextEngine()
    {
        nint library;
        if (FT_Init_FreeType(&library) != 0)
            throw new InvalidOperationException("FreeType initialization failed.");
        _library = library;

        var path = FindFontFile() ?? throw new InvalidOperationException("No usable font file found.");
        nint face;
        if (FT_New_Face(library, path, 0, &face) != 0)
            throw new InvalidOperationException($"FreeType could not open '{path}'.");
        _face = face;
    }

    public float Measure(string text, float fontSize)
    {
        var size = ToQuarterPixels(fontSize);
        var width = 0f;
        foreach (var ch in text)
            width += Get(size, ch).Advance;
        return width;
    }

    public void Rasterize(string text, float fontSize, GlyphBlit blit)
    {
        var size = ToQuarterPixels(fontSize);
        var pen = 0f;
        foreach (var ch in text)
        {
            var glyph = Get(size, ch);
            var x0 = (int)MathF.Round(pen) + glyph.Left;
            var y0 = -glyph.Top;
            for (var row = 0; row < glyph.Rows; row++)
                for (var col = 0; col < glyph.Width; col++)
                {
                    var coverage = glyph.Coverage[row * glyph.Width + col];
                    if (coverage != 0)
                        blit(x0 + col, y0 + row, coverage);
                }

            pen += glyph.Advance;
        }
    }

    private static int ToQuarterPixels(float fontSize) => Math.Max(4, (int)MathF.Round(fontSize * 4));

    private Glyph Get(int quarterPixels, char ch)
    {
        if (_cache.TryGetValue((quarterPixels, ch), out var cached))
            return cached;

        if (_currentSize != quarterPixels)
        {
            // 26.6 fixed point at 72 dpi, so points equal pixels.
            FT_Set_Char_Size(_face, 0, quarterPixels * 16, 72, 72);
            _currentSize = quarterPixels;
        }

        Glyph glyph;
        if (FT_Load_Char(_face, ch, LoadRender) != 0)
        {
            glyph = new Glyph(0, 0, 0, 0, [], quarterPixels / 8f);
        }
        else
        {
            var slot = *(byte**)((byte*)_face + FaceGlyphSlot);
            var rows = (int)*(uint*)(slot + SlotBitmapRows);
            var width = (int)*(uint*)(slot + SlotBitmapWidth);
            var pitch = *(int*)(slot + SlotBitmapPitch);
            var buffer = *(byte**)(slot + SlotBitmapBuffer);
            var advance = *(nint*)(slot + SlotAdvanceX) / 64f;

            var coverage = new byte[rows * width];
            for (var row = 0; row < rows; row++)
                new ReadOnlySpan<byte>(buffer + row * pitch, width).CopyTo(coverage.AsSpan(row * width));

            glyph = new Glyph(*(int*)(slot + SlotBitmapLeft), *(int*)(slot + SlotBitmapTop), width, rows, coverage, advance);
        }

        _cache[(quarterPixels, ch)] = glyph;
        return glyph;
    }

    private static string? FindFontFile()
    {
        try
        {
            var config = FcInitLoadConfigAndFonts();
            var pattern = FcNameParse("sans-serif");
            FcConfigSubstitute(config, pattern, 0);
            FcDefaultSubstitute(pattern);

            int result;
            var match = FcFontMatch(config, pattern, &result);
            if (match != 0)
            {
                byte* file;
                if (FcPatternGetString(match, "file", 0, &file) == 0 && file != null)
                {
                    var path = Marshal.PtrToStringUTF8((nint)file);
                    if (path is not null && File.Exists(path))
                        return path;
                }
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
        }

        return FallbackFonts.FirstOrDefault(File.Exists);
    }

    [DllImport(FreeType)] private static extern int FT_Init_FreeType(nint* library);
    [DllImport(FreeType)] private static extern int FT_New_Face(nint library, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, nint index, nint* face);
    [DllImport(FreeType)] private static extern int FT_Set_Char_Size(nint face, nint width, nint height, uint hres, uint vres);
    [DllImport(FreeType)] private static extern int FT_Load_Char(nint face, nuint code, int flags);

    [DllImport(FontConfig)] private static extern nint FcInitLoadConfigAndFonts();
    [DllImport(FontConfig)] private static extern nint FcNameParse([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(FontConfig)] private static extern int FcConfigSubstitute(nint config, nint pattern, int kind);
    [DllImport(FontConfig)] private static extern void FcDefaultSubstitute(nint pattern);
    [DllImport(FontConfig)] private static extern nint FcFontMatch(nint config, nint pattern, int* result);
    [DllImport(FontConfig)] private static extern int FcPatternGetString(nint pattern, [MarshalAs(UnmanagedType.LPUTF8Str)] string obj, int n, byte** value);
}
