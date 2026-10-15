using NovaUI.Widgets;

namespace NovaUI.Desktop;

public static class Desktop
{
    /// <summary>
    /// Opens a window and runs the app until closed.
    /// TODO: create a Silk.NET window with a GL context, wrap it in a Skia GRContext/SKSurface,
    /// forward pointer events to NovaApp.OnPointer and call NovaApp.Render each frame.
    /// </summary>
    public static void Run(Func<Widget> root, string title = "NovaUI", int width = 800, int height = 600)
        => throw new NotImplementedException("Desktop host is not implemented yet.");
}
