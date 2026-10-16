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
    public static void Run(Func<Widget> root, string title = "NovaUI", int width = 800, int height = 600)
    {
        using var manager = new WindowManager();
        manager.Create(root, title, width, height);
        manager.Run();
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
