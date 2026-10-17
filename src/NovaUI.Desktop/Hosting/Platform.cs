using NovaUI.Input;

namespace NovaUI.Desktop.Hosting;

/// <summary>OS integration: native windows plus presentation through Vulkan / Direct3D / Metal.</summary>
internal interface IPlatform : IDisposable
{
    IPlatformWindow CreateWindow(string title, int width, int height);

    /// <summary>Dispatches pending OS events; never blocks.</summary>
    void PumpEvents();
}

internal interface IPlatformWindow : IDisposable
{
    bool IsClosing { get; }

    /// <summary>Client area size in pixels.</summary>
    (int Width, int Height) PixelSize { get; }

    event Action<PointerEvent>? Pointer;
    event Action<KeyEvent>? Key;

    /// <summary>Shows a BGRA8 premultiplied frame. Returns false if it must be presented again.</summary>
    bool Present(nint pixels, int width, int height, int rowBytes);
}

internal static class PlatformFactory
{
    public static IPlatform Create()
    {
        if (OperatingSystem.IsWindows())
            return new Platforms.Windows.WindowsPlatform();
        if (OperatingSystem.IsMacOS())
            return new Platforms.MacOS.MacPlatform();
        if (OperatingSystem.IsLinux())
            return new Platforms.Linux.LinuxPlatform();

        throw new PlatformNotSupportedException("NovaUI.Desktop supports Windows, macOS and Linux.");
    }
}
