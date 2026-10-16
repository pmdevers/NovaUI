using NovaUI.Input;
using NovaUI.Skia;
using NovaUI.Widgets;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Numerics;

namespace NovaUI.Desktop;

public sealed class WindowManager : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        out vec2 texCoord;
        void main()
        {
            vec2 positions[3] = vec2[](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));
            vec2 coords[3] = vec2[](vec2(0.0, 0.0), vec2(2.0, 0.0), vec2(0.0, 2.0));
            gl_Position = vec4(positions[gl_VertexID], 0.0, 1.0);
            texCoord = coords[gl_VertexID];
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 texCoord;
        uniform sampler2D source;
        out vec4 outputColor;
        void main()
        {
            outputColor = texture(source, vec2(texCoord.x, 1.0 - texCoord.y));
        }
        """;

    private static readonly object SdlLock = new();
    private static bool _sdlPrioritized;

    private readonly object _gate = new();
    private readonly List<WindowSession> _sessions = [];
    private bool _disposed;

    public int Count
    {
        get
        {
            lock (_gate)
                return _sessions.Count;
        }
    }

    public IReadOnlyList<IWindow> Windows
    {
        get
        {
            lock (_gate)
                return _sessions.Select(session => session.Window).ToArray();
        }
    }

    public IWindow Create(Func<Widget> root, string title = "NovaUI", int width = 800, int height = 600)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ThrowIfDisposed();

        EnsureSdl();

        var session = new WindowSession(this, root, title, width, height);
        lock (_gate)
            _sessions.Add(session);

        try
        {
            session.Initialize();
        }
        catch
        {
            Remove(session);
            session.Dispose();
            throw;
        }

        return session.Window;
    }

    public void Run()
    {
        ThrowIfDisposed();

        while (true)
        {
            WindowSession[] snapshot;
            lock (_gate)
                snapshot = _sessions.ToArray();

            if (snapshot.Length == 0)
                return;

            foreach (var session in snapshot)
            {
                if (session.IsClosing)
                {
                    Remove(session);
                    continue;
                }

                session.DoEvents();
            }

            foreach (var session in snapshot)
            {
                if (session.IsClosing)
                {
                    Remove(session);
                    continue;
                }

                session.DoRender();
            }

            RemoveClosedSessions();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        WindowSession[] snapshot;
        lock (_gate)
        {
            snapshot = _sessions.ToArray();
            _sessions.Clear();
        }

        foreach (var session in snapshot)
            session.Dispose();
    }

    private void Remove(WindowSession session)
    {
        lock (_gate)
            _sessions.Remove(session);
    }

    private void RemoveClosedSessions()
    {
        lock (_gate)
            _sessions.RemoveAll(session => session.IsClosing);
    }

    private static void EnsureSdl()
    {
        lock (SdlLock)
        {
            if (_sdlPrioritized)
                return;

            Window.PrioritizeSdl();
            _sdlPrioritized = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowManager));
    }

    private sealed class WindowSession : IDisposable
    {
        private readonly WindowManager _manager;
        private readonly NovaApp _app;
        private readonly IWindow _window;
        private GL? _gl;
        private IInputContext? _input;
        private SKBitmap? _bitmap;
        private SKCanvas? _canvas;
        private uint _texture;
        private uint _vertexArray;
        private uint _program;
        private int _surfaceWidth;
        private int _surfaceHeight;
        private bool _disposed;

        public WindowSession(WindowManager manager, Func<Widget> root, string title, int width, int height)
        {
            _manager = manager;

            var options = WindowOptions.Default;
            options.Title = title;
            options.Size = new Vector2D<int>(width, height);
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));

            _window = global::Silk.NET.Windowing.Window.Create(options);
            _app = new NovaApp(root, new SkiaTextMeasurer());
        }

        public IWindow Window => _window;
        public bool IsClosing => _disposed || _window.IsClosing;

        public void Initialize()
        {
            _window.Load += OnLoad;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Closing += OnClosing;
            _window.Render += OnRender;
            _window.Initialize();
        }

        public void DoEvents() => _window.DoEvents();
        public void DoRender() => _window.DoRender();

        private void OnLoad()
        {
            _gl = GL.GetApi(_window);
            _program = CreateProgram(_gl);
            _vertexArray = _gl.GenVertexArray();
            _gl.BindVertexArray(_vertexArray);
            _texture = _gl.GenTexture();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            _gl.UseProgram(_program);
            _gl.Uniform1(_gl.GetUniformLocation(_program, "source"), 0);

            _input = _window.CreateInput();
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseDown += (_, button) =>
                {
                    if (button == MouseButton.Left)
                        _app.OnPointer(new PointerEvent(PointerKind.Down, ToPoint(mouse.Position, _window)));
                };
                mouse.MouseUp += (_, button) =>
                {
                    if (button == MouseButton.Left)
                        _app.OnPointer(new PointerEvent(PointerKind.Up, ToPoint(mouse.Position, _window)));
                };
                mouse.MouseMove += (_, position) =>
                    _app.OnPointer(new PointerEvent(PointerKind.Move, ToPoint(position, _window)));
            }
        }

        private void OnFramebufferResize(Vector2D<int> size) => _gl?.Viewport(size);

        private void OnClosing()
        {
            Cleanup();
            _manager.Remove(this);
        }

        private unsafe void OnRender(double _)
        {
            if (_gl is null)
                return;

            var framebufferSize = _window.FramebufferSize;
            var currentWidth = framebufferSize.X;
            var currentHeight = framebufferSize.Y;
            if (currentWidth <= 0 || currentHeight <= 0)
                return;

            if (currentWidth != _surfaceWidth || currentHeight != _surfaceHeight)
            {
                _canvas?.Dispose();
                _bitmap?.Dispose();
                _surfaceWidth = currentWidth;
                _surfaceHeight = currentHeight;
                _bitmap = new SKBitmap(new SKImageInfo(_surfaceWidth, _surfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
                _canvas = new SKCanvas(_bitmap);
                _gl.BindTexture(TextureTarget.Texture2D, _texture);
                unsafe
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba,
                        (uint)_surfaceWidth, (uint)_surfaceHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
                }
                _app.Render(new SkiaCanvas(_canvas), new Size(_surfaceWidth, _surfaceHeight));
                UploadPixels(_gl, _bitmap, _texture, _surfaceWidth, _surfaceHeight);
            }
            else if (_app.NeedsRedraw && _bitmap is not null && _canvas is not null)
            {
                _app.Render(new SkiaCanvas(_canvas), new Size(_surfaceWidth, _surfaceHeight));
                UploadPixels(_gl, _bitmap, _texture, _surfaceWidth, _surfaceHeight);
            }

            _gl.Viewport(0, 0, (uint)currentWidth, (uint)currentHeight);
            _gl.ClearColor(1, 1, 1, 1);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            _gl.UseProgram(_program);
            _gl.BindVertexArray(_vertexArray);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _texture);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        private void Cleanup()
        {
            if (_disposed)
                return;

            _disposed = true;

            _canvas?.Dispose();
            _bitmap?.Dispose();
            _input?.Dispose();

            if (_gl is not null)
            {
                if (_program != 0) _gl.DeleteProgram(_program);
                if (_texture != 0) _gl.DeleteTexture(_texture);
                if (_vertexArray != 0) _gl.DeleteVertexArray(_vertexArray);
                _gl.Dispose();
            }

            _canvas = null;
            _bitmap = null;
            _input = null;
            _gl = null;
            _program = 0;
            _texture = 0;
            _vertexArray = 0;
        }

        public void Dispose()
        {
            try
            {
                _window.Close();
            }
            catch
            {
                // Ignore shutdown errors during teardown.
            }

            Cleanup();
            _manager.Remove(this);
            _window.Dispose();
        }
    }

    private static Point ToPoint(Vector2 position, IWindow window)
    {
        var size = window.Size;
        var framebuffer = window.FramebufferSize;
        return new Point(
            size.X == 0 ? position.X : position.X * framebuffer.X / size.X,
            size.Y == 0 ? position.Y : position.Y * framebuffer.Y / size.Y);
    }

    private static unsafe void UploadPixels(GL gl, SKBitmap bitmap, uint texture, int width, int height)
    {
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)width, (uint)height,
            PixelFormat.Rgba, PixelType.UnsignedByte, (void*)bitmap.GetPixels());
    }

    private static uint CreateProgram(GL gl)
    {
        var vertexShader = CompileShader(gl, ShaderType.VertexShader, VertexShaderSource);
        var fragmentShader = CompileShader(gl, ShaderType.FragmentShader, FragmentShaderSource);
        var program = gl.CreateProgram();
        gl.AttachShader(program, vertexShader);
        gl.AttachShader(program, fragmentShader);
        gl.LinkProgram(program);
        gl.GetProgram(program, GLEnum.LinkStatus, out var linked);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);
        if (linked == 0)
        {
            var log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"Unable to link the desktop rendering program: {log}");
        }

        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, GLEnum.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Unable to compile the {type} desktop shader: {log}");
        }

        return shader;
    }
}
