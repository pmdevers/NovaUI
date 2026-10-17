using NovaUI.Desktop.Rendering;

namespace NovaUI.Desktop.Hosting;

/// <summary>Connects a <see cref="Window"/> to a native window: rasterizes with Skia and presents the pixels.</summary>
internal sealed class WindowHost : IDisposable
{
    private readonly IPlatformWindow _native;
    private readonly NovaApp _app;
    private readonly IRasterizer _rasterizer;
    private IRasterSurface? _surface;
    private int _surfaceWidth, _surfaceHeight;
    private bool _closeRequested;
    private bool _forceRender = true;

    public WindowHost(IPlatform platform, IRasterizer rasterizer, Window window)
    {
        _rasterizer = rasterizer;
        _native = platform.CreateWindow(window.Title, window.Width, window.Height);
        _app = new NovaApp(window.Build, rasterizer.TextMeasurer);
        _native.Pointer += _app.OnPointer;
        _native.Key += _app.OnKey;
    }

    public bool IsClosing => _closeRequested || _native.IsClosing;

    public void RequestClose() => _closeRequested = true;

    public void Frame()
    {
        var (width, height) = _native.PixelSize;
        if (width <= 0 || height <= 0)
            return;

        if (_surface is null || _surfaceWidth != width || _surfaceHeight != height)
        {
            _surface?.Dispose();
            _surface = _rasterizer.CreateSurface(width, height);
            _surfaceWidth = width;
            _surfaceHeight = height;
            _forceRender = true;
        }

        if (!_forceRender && !_app.NeedsRedraw)
            return;

        _app.Render(_surface.Canvas, new Size(width, height));
        _forceRender = !_native.Present(_surface.Pixels, width, height, _surface.RowBytes);
    }

    public void Dispose()
    {
        _native.Pointer -= _app.OnPointer;
        _native.Key -= _app.OnKey;
        _native.Dispose();
        _surface?.Dispose();
    }
}
