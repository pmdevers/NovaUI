using NovaUI.Desktop.Hosting;
using NovaUI.Desktop.Rendering;

namespace NovaUI.Desktop;

/// <summary>Entry point: <c>await App.RunAsync&lt;MainWindow&gt;();</c></summary>
public static class App
{
    /// <summary>
    /// Opens <typeparamref name="T"/> and runs the UI loop until every window is closed.
    /// The loop runs on the calling thread (required by Cocoa), so call this from Main.
    /// </summary>
    public static Task RunAsync<T>(IRasterizer? rasterizer = null) where T : Window, new() => RunAsync(new T(), rasterizer);

    /// <param name="rasterizer">2D drawing backend; defaults to the dependency-free <see cref="SoftwareRasterizer"/>.</param>
    public static Task RunAsync(Window mainWindow, IRasterizer? rasterizer = null)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        try
        {
            using var platform = PlatformFactory.Create();
            var manager = new WindowManager(platform, rasterizer ?? new SoftwareRasterizer());
            using var _ = AppContext.Enter(new AppContext(manager));
            manager.Open(mainWindow);
            manager.Run();
            return Task.CompletedTask;
        }
        catch (Exception e)
        {
            return Task.FromException(e);
        }
    }
}
