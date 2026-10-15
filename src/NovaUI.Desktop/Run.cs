using System.Numerics;
using NovaUI.Input;
using NovaUI.Rendering;
using NovaUI.Skia;
using NovaUI.Widgets;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;

namespace NovaUI.Desktop;

public static class Desktop
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

    /// <summary>Opens a window and runs the app until closed.</summary>
    public static unsafe void Run(Func<Widget> root, string title = "NovaUI", int width = 800, int height = 600)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var options = WindowOptions.Default;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));

        // The GLFW build bundled with Silk.NET fails to initialise on Wayland sessions; SDL handles both.
        Window.PrioritizeSdl();
        using var window = Window.Create(options);
        var app = new NovaApp(root, new SkiaTextMeasurer());
        GL? gl = null;
        IInputContext? input = null;
        SKBitmap? bitmap = null;
        SKCanvas? canvas = null;
        uint texture = 0;
        uint vertexArray = 0;
        uint program = 0;
        var surfaceWidth = 0;
        var surfaceHeight = 0;

        window.Load += () =>
        {
            gl = GL.GetApi(window);
            program = CreateProgram(gl);
            vertexArray = gl.GenVertexArray();
            gl.BindVertexArray(vertexArray);
            texture = gl.GenTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.UseProgram(program);
            gl.Uniform1(gl.GetUniformLocation(program, "source"), 0);

            input = window.CreateInput();
            foreach (var mouse in input.Mice)
            {
                mouse.MouseDown += (_, button) =>
                {
                    if (button == MouseButton.Left)
                        app.OnPointer(new PointerEvent(PointerKind.Down, ToPoint(mouse.Position, window)));
                };
                mouse.MouseUp += (_, button) =>
                {
                    if (button == MouseButton.Left)
                        app.OnPointer(new PointerEvent(PointerKind.Up, ToPoint(mouse.Position, window)));
                };
                mouse.MouseMove += (_, position) =>
                    app.OnPointer(new PointerEvent(PointerKind.Move, ToPoint(position, window)));
            }
        };

        window.FramebufferResize += size => gl?.Viewport(size);
        window.Render += _ =>
        {
            if (gl is null) return;

            var framebufferSize = window.FramebufferSize;
            var currentWidth = framebufferSize.X;
            var currentHeight = framebufferSize.Y;
            if (currentWidth <= 0 || currentHeight <= 0) return;

            if (currentWidth != surfaceWidth || currentHeight != surfaceHeight)
            {
                canvas?.Dispose();
                bitmap?.Dispose();
                surfaceWidth = currentWidth;
                surfaceHeight = currentHeight;
                bitmap = new SKBitmap(new SKImageInfo(surfaceWidth, surfaceHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
                canvas = new SKCanvas(bitmap);
                gl.BindTexture(TextureTarget.Texture2D, texture);
                gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba,
                    (uint)surfaceWidth, (uint)surfaceHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (void*)0);
                app.Render(new SkiaCanvas(canvas), new Size(surfaceWidth, surfaceHeight));
                UploadPixels(gl, bitmap, texture, surfaceWidth, surfaceHeight);
            }
            else if (app.NeedsRedraw && bitmap is not null && canvas is not null)
            {
                app.Render(new SkiaCanvas(canvas), new Size(surfaceWidth, surfaceHeight));
                UploadPixels(gl, bitmap, texture, surfaceWidth, surfaceHeight);
            }

            gl.Viewport(0, 0, (uint)currentWidth, (uint)currentHeight);
            gl.ClearColor(1, 1, 1, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            gl.UseProgram(program);
            gl.BindVertexArray(vertexArray);
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        };

        try
        {
            window.Run();
        }
        finally
        {
            canvas?.Dispose();
            bitmap?.Dispose();
            input?.Dispose();
            if (gl is not null)
            {
                if (program != 0) gl.DeleteProgram(program);
                if (texture != 0) gl.DeleteTexture(texture);
                if (vertexArray != 0) gl.DeleteVertexArray(vertexArray);
                gl.Dispose();
            }
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
