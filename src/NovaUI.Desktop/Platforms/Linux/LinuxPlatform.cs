using NovaUI.Desktop.Hosting;
using System.Text;
using NovaUI.Input;

namespace NovaUI.Desktop.Platforms.Linux;

/// <summary>X11 windows (also used through XWayland) presented through Vulkan.</summary>
internal sealed unsafe class LinuxPlatform : IPlatform
{
    private readonly nint _display;
    private readonly nint _deleteAtom;
    private readonly Dictionary<nint, LinuxWindow> _windows = [];
    private readonly nint _inputMethod;
    private VulkanDevice? _vulkan;

    public LinuxPlatform()
    {
        _display = X11.XOpenDisplay(0);
        if (_display == 0)
            throw new InvalidOperationException("Unable to open the X11 display (is DISPLAY set?).");

        _deleteAtom = X11.XInternAtom(_display, "WM_DELETE_WINDOW", 0);

        // Needed for Xutf8LookupString to return UTF-8 for the user's layout.
        X11.SetLocale(6, "");
        X11.XSetLocaleModifiers("");
        _inputMethod = X11.XOpenIM(_display, 0, 0, 0);
    }

    public IPlatformWindow CreateWindow(string title, int width, int height)
    {
        var screen = X11.XDefaultScreen(_display);
        var handle = X11.XCreateSimpleWindow(_display, X11.XRootWindow(_display, screen), 0, 0, (uint)width, (uint)height, 0, 0, 0);
        X11.XSelectInput(_display, handle,
            (nint)(X11.KeyPressMask | X11.ButtonPressMask | X11.ButtonReleaseMask | X11.PointerMotionMask | X11.ExposureMask | X11.StructureNotifyMask));
        X11.XStoreName(_display, handle, title);
        var atom = _deleteAtom;
        X11.XSetWMProtocols(_display, handle, &atom, 1);

        _vulkan ??= new VulkanDevice();
        var surface = _vulkan.CreateSurface(_display, handle);
        try
        {
            _vulkan.Initialize(surface);
        }
        catch
        {
            _vulkan.DestroySurface(surface);
            X11.XDestroyWindow(_display, handle);
            throw;
        }

        X11.XMapWindow(_display, handle);
        X11.XFlush(_display);

        nint context = 0;
        if (_inputMethod != 0)
            context = X11.XCreateIC(_inputMethod, "inputStyle", 0x0008 | 0x0400, "clientWindow", handle, 0); // PreeditNothing | StatusNothing

        var window = new LinuxWindow(this, handle, width, height, new VulkanPresenter(_vulkan, surface), context);
        _windows[handle] = window;
        return window;
    }

    public void PumpEvents()
    {
        byte* ev = stackalloc byte[X11.EventSize];
        while (X11.XPending(_display) > 0)
        {
            X11.XNextEvent(_display, ev);
            if (X11.XFilterEvent(ev, 0) != 0)
                continue;

            var type = *(int*)ev;
            var handle = *(nint*)(ev + 32);

            switch (type)
            {
                case X11.ConfigureNotify:
                    // XConfigureEvent: event@32, window@40
                    if (_windows.TryGetValue(*(nint*)(ev + 40), out var resized))
                        resized.Size = (*(int*)(ev + 56), *(int*)(ev + 60));
                    break;
                case X11.ClientMessage:
                    if (*(nint*)(ev + 56) == _deleteAtom && _windows.TryGetValue(handle, out var closing))
                        closing.IsClosing = true;
                    break;
                case X11.KeyPress:
                    if (_windows.TryGetValue(handle, out var typing))
                        typing.RaiseKey(TranslateKey(ev, typing.InputContext));
                    break;
                case X11.ButtonPress or X11.ButtonRelease or X11.MotionNotify:
                    if (!_windows.TryGetValue(handle, out var target))
                        break;
                    var position = new Point(*(int*)(ev + 64), *(int*)(ev + 68));
                    if (type == X11.MotionNotify)
                        target.RaisePointer(new PointerEvent(PointerKind.Move, position));
                    else if (*(uint*)(ev + 84) == 1)
                        target.RaisePointer(new PointerEvent(type == X11.ButtonPress ? PointerKind.Down : PointerKind.Up, position));
                    break;
            }
        }
    }

    private static KeyEvent? TranslateKey(byte* ev, nint context)
    {
        var state = *(uint*)(ev + 80);
        var modifiers = KeyModifiers.None;
        if ((state & 1) != 0) modifiers |= KeyModifiers.Shift;
        if ((state & 4) != 0) modifiers |= KeyModifiers.Control;
        if ((state & 8) != 0) modifiers |= KeyModifiers.Alt;
        if ((state & 64) != 0) modifiers |= KeyModifiers.Meta;

        byte* buffer = stackalloc byte[64];
        nint keysym = 0;
        int length;
        if (context != 0)
        {
            int status;
            length = X11.Xutf8LookupString(context, ev, buffer, 63, &keysym, &status);
            if (status < 2)
                length = 0;
        }
        else
        {
            length = X11.XLookupString(ev, buffer, 63, &keysym, 0);
        }

        var key = keysym switch
        {
            0xFF08 => Key.Backspace,
            0xFFFF => Key.Delete,
            0xFF51 => Key.Left,
            0xFF53 => Key.Right,
            0xFF52 => Key.Up,
            0xFF54 => Key.Down,
            0xFF50 => Key.Home,
            0xFF57 => Key.End,
            0xFF0D or 0xFF8D => Key.Enter,
            0xFF09 => Key.Tab,
            0xFF1B => Key.Escape,
            _ => Key.None,
        };

        string? text = null;
        if (key == Key.None && length > 0 && (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
            text = Encoding.UTF8.GetString(buffer, length);

        return key == Key.None && text is null ? null : new KeyEvent(key, text, modifiers);
    }

    private void Remove(LinuxWindow window)
    {
        _windows.Remove(window.Handle);
        X11.XDestroyWindow(_display, window.Handle);
        X11.XFlush(_display);
    }

    public void Dispose()
    {
        foreach (var window in _windows.Values.ToArray())
            window.Dispose();

        _vulkan?.Dispose();
        if (_inputMethod != 0)
            X11.XCloseIM(_inputMethod);
        X11.XCloseDisplay(_display);
    }

    private sealed class LinuxWindow(LinuxPlatform platform, nint handle, int width, int height, VulkanPresenter presenter, nint inputContext) : IPlatformWindow
    {
        private bool _disposed;

        public nint Handle { get; } = handle;
        public bool IsClosing { get; set; }
        public (int Width, int Height) Size { get; set; } = (width, height);
        public (int Width, int Height) PixelSize => Size;

        public event Action<PointerEvent>? Pointer;
        public event Action<KeyEvent>? Key;

        public nint InputContext { get; } = inputContext;

        public void RaiseKey(KeyEvent? e)
        {
            if (e is { } key)
                Key?.Invoke(key);
        }

        public void RaisePointer(PointerEvent e) => Pointer?.Invoke(e);

        public bool Present(nint pixels, int width, int height, int rowBytes) =>
            presenter.Present(pixels, width, height, rowBytes, Size.Width, Size.Height);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (InputContext != 0)
                X11.XDestroyIC(InputContext);
            presenter.Dispose();
            platform.Remove(this);
        }
    }
}
