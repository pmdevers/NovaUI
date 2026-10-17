using NovaUI.Desktop.Hosting;
using NovaUI.Desktop.Rendering;

namespace NovaUI.Desktop;

/// <summary>Owns all open windows and drives the event/render loop.</summary>
public sealed class WindowManager : IWindowManager
{
    private readonly IPlatform _platform;
    private readonly IRasterizer _rasterizer;
    private readonly List<Window> _windows = [];
    private readonly Dictionary<Window, WindowHost> _hosts = [];

    internal WindowManager(IPlatform platform, IRasterizer rasterizer)
    {
        _platform = platform;
        _rasterizer = rasterizer;
    }

    public IReadOnlyList<Window> Windows => _windows.ToArray();

    public T Open<T>() where T : Window, new() => Open(new T());

    public T Open<T>(T window) where T : Window
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_hosts.ContainsKey(window))
            throw new InvalidOperationException("Window is already open.");

        _hosts[window] = new WindowHost(_platform, _rasterizer, window);
        _windows.Add(window);
        return window;
    }

    public void Close(Window window)
    {
        if (_hosts.TryGetValue(window, out var host))
            host.RequestClose();
    }

    public void CloseAll()
    {
        foreach (var window in Windows)
            Close(window);
    }

    internal void Run()
    {
        while (_windows.Count > 0)
        {
            _platform.PumpEvents();

            foreach (var window in _windows.ToArray())
            {
                var host = _hosts[window];
                if (host.IsClosing)
                {
                    _windows.Remove(window);
                    _hosts.Remove(window);
                    host.Dispose();
                    continue;
                }

                host.Frame();
            }

            Thread.Sleep(4);
        }
    }
}
