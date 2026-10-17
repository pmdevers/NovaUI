using System.Runtime.CompilerServices;
using NovaUI.Widgets;

[assembly: InternalsVisibleTo("NovaUI.Desktop")]

namespace NovaUI;

/// <summary>Base class for application windows. Override <see cref="Build"/> to describe the UI.</summary>
public abstract class Window
{
    public virtual string Title => "NovaUI";
    public virtual int Width => 800;
    public virtual int Height => 600;

    public AppContext Context => AppContext.Current;

    /// <summary>Creates the widget tree; called again after every handled click.</summary>
    public abstract Widget Build();

    public void Close() => Context.Windows.Close(this);
}

/// <summary>Opens and closes windows; implemented by the platform host.</summary>
public interface IWindowManager
{
    IReadOnlyList<Window> Windows { get; }
    T Open<T>() where T : Window, new();
    T Open<T>(T window) where T : Window;
    void Close(Window window);
    void CloseAll();
}

/// <summary>Ambient application services, reachable from any widget or window while the app runs.</summary>
public sealed class AppContext
{
    private static AppContext? _current;

    internal AppContext(IWindowManager windows) => Windows = windows;

    public static AppContext Current =>
        _current ?? throw new InvalidOperationException("No NovaUI app is running. Start it with App.RunAsync<T>().");

    public IWindowManager Windows { get; }

    internal static IDisposable Enter(AppContext context)
    {
        _current = context;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => _current = null;
    }
}
