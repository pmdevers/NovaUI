using System.Runtime.InteropServices;
using NovaUI.Desktop.Hosting;
using NovaUI.Input;

namespace NovaUI.Desktop.Platforms.Windows;

/// <summary>Win32 windows presented through Direct3D 11 / DXGI. Untested best-effort backend.</summary>
internal sealed unsafe class WindowsPlatform : IPlatform
{
    private const string ClassName = "NovaUIWindow";
    private static readonly Dictionary<nint, WindowsWindow> Windows = [];

    private readonly nint _instance;

    public WindowsPlatform()
    {
        Native.SetProcessDPIAware();
        _instance = Native.GetModuleHandleW(0);

        fixed (char* name = ClassName)
        {
            var cls = new Native.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Native.WNDCLASSEXW),
                style = 0x3, // CS_HREDRAW | CS_VREDRAW
                lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc,
                hInstance = _instance,
                hCursor = Native.LoadCursorW(0, 32512),
                lpszClassName = (nint)name,
            };
            if (Native.RegisterClassExW(&cls) == 0)
                throw new InvalidOperationException("RegisterClassEx failed.");
        }
    }

    public IPlatformWindow CreateWindow(string title, int width, int height)
    {
        var rect = new Native.RECT { right = width, bottom = height };
        const uint style = 0x00CF0000; // WS_OVERLAPPEDWINDOW
        Native.AdjustWindowRect(&rect, style, 0);

        nint hwnd;
        fixed (char* cls = ClassName)
        fixed (char* caption = title)
            hwnd = Native.CreateWindowExW(0, (nint)cls, (nint)caption, style, int.MinValue, int.MinValue,
                rect.right - rect.left, rect.bottom - rect.top, 0, 0, _instance, 0);
        if (hwnd == 0)
            throw new InvalidOperationException("CreateWindowEx failed.");

        var window = new WindowsWindow(hwnd);
        Windows[hwnd] = window;
        Native.ShowWindow(hwnd, 5);
        return window;
    }

    public void PumpEvents()
    {
        Native.MSG msg;
        while (Native.PeekMessageW(&msg, 0, 0, 0, 1) != 0)
        {
            Native.TranslateMessage(&msg);
            Native.DispatchMessageW(&msg);
        }
    }

    public void Dispose()
    {
        foreach (var window in Windows.Values.ToArray())
            window.Dispose();
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (Windows.TryGetValue(hwnd, out var window))
        {
            switch (message)
            {
                case 0x0010: // WM_CLOSE
                    window.IsClosing = true;
                    return 0;
                case 0x0100: // WM_KEYDOWN
                    window.RaiseVirtualKey((int)wParam);
                    return 0;
                case 0x0102: // WM_CHAR
                    window.RaiseChar((char)wParam);
                    return 0;
                case 0x0200: // WM_MOUSEMOVE
                    window.Raise(PointerKind.Move, lParam);
                    return 0;
                case 0x0201: // WM_LBUTTONDOWN
                    window.Raise(PointerKind.Down, lParam);
                    return 0;
                case 0x0202: // WM_LBUTTONUP
                    window.Raise(PointerKind.Up, lParam);
                    return 0;
            }
        }

        return Native.DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private sealed class WindowsWindow : IPlatformWindow
    {
        private readonly nint _hwnd;
        private D3D11Presenter? _presenter;

        public WindowsWindow(nint hwnd) => _hwnd = hwnd;

        public bool IsClosing { get; set; }

        public (int Width, int Height) PixelSize
        {
            get
            {
                Native.RECT rect;
                Native.GetClientRect(_hwnd, &rect);
                return (rect.right - rect.left, rect.bottom - rect.top);
            }
        }

        public event Action<PointerEvent>? Pointer;
        public event Action<KeyEvent>? Key;

        private char _highSurrogate;

        public void RaiseVirtualKey(int virtualKey)
        {
            var key = virtualKey switch
            {
                0x08 => NovaUI.Input.Key.Backspace,
                0x2E => NovaUI.Input.Key.Delete,
                0x25 => NovaUI.Input.Key.Left,
                0x27 => NovaUI.Input.Key.Right,
                0x26 => NovaUI.Input.Key.Up,
                0x28 => NovaUI.Input.Key.Down,
                0x24 => NovaUI.Input.Key.Home,
                0x23 => NovaUI.Input.Key.End,
                0x0D => NovaUI.Input.Key.Enter,
                0x09 => NovaUI.Input.Key.Tab,
                0x1B => NovaUI.Input.Key.Escape,
                _ => NovaUI.Input.Key.None,
            };

            if (key != NovaUI.Input.Key.None)
                Key?.Invoke(new KeyEvent(key));
        }

        public void RaiseChar(char ch)
        {
            if (char.IsControl(ch) || (Native.GetKeyState(0x11) & 0x8000) != 0 && (Native.GetKeyState(0x12) & 0x8000) == 0)
                return;

            if (char.IsHighSurrogate(ch))
            {
                _highSurrogate = ch;
                return;
            }

            var text = char.IsLowSurrogate(ch) && _highSurrogate != 0 ? new string([_highSurrogate, ch]) : ch.ToString();
            _highSurrogate = '\0';
            Key?.Invoke(new KeyEvent(NovaUI.Input.Key.None, text));
        }

        public void Raise(PointerKind kind, nint lParam) =>
            Pointer?.Invoke(new PointerEvent(kind, new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF))));

        public bool Present(nint pixels, int width, int height, int rowBytes)
        {
            _presenter ??= new D3D11Presenter(_hwnd, width, height);
            return _presenter.Present(pixels, width, height, rowBytes);
        }

        public void Dispose()
        {
            if (_hwnd == 0)
                return;

            _presenter?.Dispose();
            _presenter = null;
            if (Windows.Remove(_hwnd))
                Native.DestroyWindow(_hwnd);
        }
    }

    /// <summary>Raw COM vtable calls; slot numbers come from d3d11.h / dxgi.h.</summary>
    private sealed class D3D11Presenter : IDisposable
    {
        private static readonly Guid Texture2DId = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        private nint _swapChain, _device, _context;
        private int _width, _height;

        public D3D11Presenter(nint hwnd, int width, int height)
        {
            var desc = new Native.DXGI_SWAP_CHAIN_DESC
            {
                Width = (uint)width, Height = (uint)height,
                Format = 87, // B8G8R8A8_UNORM
                SampleCount = 1,
                BufferUsage = 0x20, // RENDER_TARGET_OUTPUT
                BufferCount = 1,
                OutputWindow = hwnd,
                Windowed = 1,
            };
            nint swapChain, device, context;
            uint featureLevel;
            var hr = Native.D3D11CreateDeviceAndSwapChain(0, 1, 0, 0, null, 0, 7, &desc, &swapChain, &device, &featureLevel, &context);
            if (hr < 0)
                throw new InvalidOperationException($"D3D11CreateDeviceAndSwapChain failed: 0x{hr:X8}");

            _swapChain = swapChain;
            _device = device;
            _context = context;
            _width = width;
            _height = height;
        }

        public bool Present(nint pixels, int width, int height, int rowBytes)
        {
            if (width != _width || height != _height)
            {
                var resize = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, uint, uint, int>)VTable(_swapChain, 13);
                if (resize(_swapChain, 0, (uint)width, (uint)height, 0, 0) < 0)
                    return false;
                _width = width;
                _height = height;
            }

            nint texture;
            var id = Texture2DId;
            var getBuffer = (delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)VTable(_swapChain, 9);
            if (getBuffer(_swapChain, 0, &id, &texture) < 0)
                return false;

            var update = (delegate* unmanaged[Stdcall]<nint, nint, uint, nint, nint, uint, uint, void>)VTable(_context, 48);
            update(_context, texture, 0, 0, pixels, (uint)rowBytes, 0);
            Release(texture);

            var present = (delegate* unmanaged[Stdcall]<nint, uint, uint, int>)VTable(_swapChain, 8);
            return present(_swapChain, 1, 0) >= 0;
        }

        public void Dispose()
        {
            Release(_swapChain);
            Release(_context);
            Release(_device);
            _swapChain = _context = _device = 0;
        }

        private static nint VTable(nint obj, int slot) => ((nint*)*(nint*)obj)[slot];

        private static void Release(nint obj)
        {
            if (obj != 0)
                ((delegate* unmanaged[Stdcall]<nint, uint>)VTable(obj, 2))(obj);
        }
    }

    private static unsafe class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct WNDCLASSEXW
        {
            public uint cbSize, style;
            public nint lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public nint hwnd; public uint message; public nint wParam, lParam; public uint time; public int x, y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DXGI_SWAP_CHAIN_DESC
        {
            public uint Width, Height, RefreshNumerator, RefreshDenominator, Format, ScanlineOrdering, Scaling;
            public uint SampleCount, SampleQuality, BufferUsage, BufferCount;
            public nint OutputWindow;
            public int Windowed;
            public uint SwapEffect, Flags;
        }

        [DllImport("user32.dll")] public static extern int SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern short GetKeyState(int key);
        [DllImport("user32.dll")] public static extern ushort RegisterClassExW(WNDCLASSEXW* cls);
        [DllImport("user32.dll")] public static extern nint LoadCursorW(nint instance, nint name);
        [DllImport("user32.dll")] public static extern int AdjustWindowRect(RECT* rect, uint style, int menu);
        [DllImport("user32.dll")] public static extern nint CreateWindowExW(uint exStyle, nint cls, nint title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
        [DllImport("user32.dll")] public static extern int ShowWindow(nint hwnd, int cmd);
        [DllImport("user32.dll")] public static extern int DestroyWindow(nint hwnd);
        [DllImport("user32.dll")] public static extern int GetClientRect(nint hwnd, RECT* rect);
        [DllImport("user32.dll")] public static extern int PeekMessageW(MSG* msg, nint hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern int TranslateMessage(MSG* msg);
        [DllImport("user32.dll")] public static extern nint DispatchMessageW(MSG* msg);
        [DllImport("user32.dll")] public static extern nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);
        [DllImport("kernel32.dll")] public static extern nint GetModuleHandleW(nint name);
        [DllImport("d3d11.dll")]
        public static extern int D3D11CreateDeviceAndSwapChain(nint adapter, uint driverType, nint software, uint flags,
            uint* featureLevels, uint featureLevelCount, uint sdkVersion, DXGI_SWAP_CHAIN_DESC* desc,
            nint* swapChain, nint* device, uint* featureLevel, nint* context);
    }
}
