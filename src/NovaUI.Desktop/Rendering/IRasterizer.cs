using NovaUI.Rendering;

namespace NovaUI.Desktop.Rendering;

/// <summary>Plug-in point for 2D drawing: turns ICanvas calls into BGRA pixels the platform backend can present.</summary>
public interface IRasterizer
{
    ITextMeasurer TextMeasurer { get; }

    IRasterSurface CreateSurface(int width, int height);
}

/// <summary>A BGRA8 premultiplied pixel buffer plus the canvas that draws into it.</summary>
public interface IRasterSurface : IDisposable
{
    ICanvas Canvas { get; }
    nint Pixels { get; }
    int RowBytes { get; }
}
