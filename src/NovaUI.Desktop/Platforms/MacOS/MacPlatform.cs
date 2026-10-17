using System.Runtime.InteropServices;
using NovaUI.Desktop.Hosting;
using NovaUI.Input;

namespace NovaUI.Desktop.Platforms.MacOS;

/// <summary>Cocoa windows presented through a CAMetalLayer. Untested best-effort backend.</summary>
internal sealed class MacPlatform : IPlatform
{
    private readonly nint _app;
    private readonly nint _device;
    private readonly nint _queue;
    private readonly nint _defaultMode;
    private readonly List<MacWindow> _windows = [];

    public MacPlatform()
    {
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
        NativeLibrary.Load("/System/Library/Frameworks/QuartzCore.framework/QuartzCore");

        _device = ObjC.MTLCreateSystemDefaultDevice();
        if (_device == 0)
            throw new InvalidOperationException("No Metal capable GPU found.");

        _queue = ObjC.Send(_device, "newCommandQueue");
        _app = ObjC.Send(ObjC.Class("NSApplication"), "sharedApplication");
        ObjC.Send(_app, "setActivationPolicy:", 0L); // regular app
        ObjC.Send(_app, "finishLaunching");
        ObjC.Send(_app, "activateIgnoringOtherApps:", (byte)1);
        _defaultMode = ObjC.NSString("kCFRunLoopDefaultMode");
    }

    public IPlatformWindow CreateWindow(string title, int width, int height)
    {
        var window = new MacWindow(_device, _queue, title, width, height);
        _windows.Add(window);
        return window;
    }

    public void PumpEvents()
    {
        var pool = ObjC.NewPool();
        while (true)
        {
            var ev = ObjC.NextEvent(_app, ulong.MaxValue, 0, _defaultMode, 1);
            if (ev == 0)
                break;

            var target = ObjC.Send(ev, "window");
            var type = ObjC.SendULong(ev, "type");
            if (type is 1 or 2 or 5 or 6)
            {
                var window = _windows.Find(w => w.Handle == target);
                window?.RaisePointer(ev, type);
            }

            if (type == 10) // keyDown: consumed here so AppKit does not beep
            {
                _windows.Find(w => w.Handle == target)?.RaiseKey(ev);
                continue;
            }

            ObjC.Send(_app, "sendEvent:", ev);
        }

        ObjC.DrainPool(pool);
    }

    public void Dispose()
    {
        foreach (var window in _windows.ToArray())
            window.Dispose();
    }

    private sealed class MacWindow : IPlatformWindow
    {
        private readonly nint _queue;
        private readonly nint _view;
        private readonly nint _layer;
        private (double W, double H) _drawableSize;
        private bool _disposed;

        public MacWindow(nint device, nint queue, string title, int width, int height)
        {
            _queue = queue;

            var frame = new ObjC.CGRect(200, 200, width, height);
            const nuint style = 1 | 2 | 4 | 8; // titled, closable, miniaturizable, resizable
            Handle = ObjC.InitWindow(ObjC.Send(ObjC.Class("NSWindow"), "alloc"), frame, style, 2, 0);
            ObjC.Send(Handle, "setReleasedWhenClosed:", (byte)0);
            ObjC.Send(Handle, "setTitle:", ObjC.NSString(title));
            ObjC.Send(Handle, "setAcceptsMouseMovedEvents:", (byte)1);

            _view = ObjC.Send(Handle, "contentView");
            _layer = ObjC.Send(ObjC.Class("CAMetalLayer"), "layer");
            ObjC.Send(_layer, "setDevice:", device);
            ObjC.Send(_layer, "setPixelFormat:", 80L); // BGRA8Unorm
            ObjC.Send(_layer, "setFramebufferOnly:", (byte)0); // allow CPU uploads
            ObjC.Send(_view, "setLayer:", _layer);
            ObjC.Send(_view, "setWantsLayer:", (byte)1);

            ObjC.Send(Handle, "makeKeyAndOrderFront:", (nint)0);
        }

        public nint Handle { get; }

        public bool IsClosing =>
            _disposed || (ObjC.SendByte(Handle, "isVisible") == 0 && ObjC.SendByte(Handle, "isMiniaturized") == 0);

        public (int Width, int Height) PixelSize
        {
            get
            {
                var bounds = ObjC.GetRect(_view, "bounds");
                return ((int)bounds.Width, (int)bounds.Height);
            }
        }

        public event Action<PointerEvent>? Pointer;
        public event Action<KeyEvent>? Key;

        public void RaiseKey(nint ev)
        {
            var flags = ObjC.SendULong(ev, "modifierFlags");
            var modifiers = KeyModifiers.None;
            if ((flags & (1UL << 17)) != 0) modifiers |= KeyModifiers.Shift;
            if ((flags & (1UL << 18)) != 0) modifiers |= KeyModifiers.Control;
            if ((flags & (1UL << 19)) != 0) modifiers |= KeyModifiers.Alt;
            if ((flags & (1UL << 20)) != 0) modifiers |= KeyModifiers.Meta;

            var key = ObjC.SendUShort(ev, "keyCode") switch
            {
                51 => NovaUI.Input.Key.Backspace,
                117 => NovaUI.Input.Key.Delete,
                123 => NovaUI.Input.Key.Left,
                124 => NovaUI.Input.Key.Right,
                126 => NovaUI.Input.Key.Up,
                125 => NovaUI.Input.Key.Down,
                115 => NovaUI.Input.Key.Home,
                119 => NovaUI.Input.Key.End,
                36 or 76 => NovaUI.Input.Key.Enter,
                48 => NovaUI.Input.Key.Tab,
                53 => NovaUI.Input.Key.Escape,
                _ => NovaUI.Input.Key.None,
            };

            string? text = null;
            if (key == NovaUI.Input.Key.None && (modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0)
            {
                var characters = ObjC.Send(ev, "characters");
                if (characters != 0)
                    text = Marshal.PtrToStringUTF8(ObjC.Send(characters, "UTF8String"));
            }

            if (key != NovaUI.Input.Key.None || !string.IsNullOrEmpty(text))
                Key?.Invoke(new KeyEvent(key, text, modifiers));
        }

        public void RaisePointer(nint ev, ulong type)
        {
            var local = ObjC.ConvertPoint(_view, ObjC.GetPoint(ev, "locationInWindow"));
            var height = ObjC.GetRect(_view, "bounds").Height;
            var position = new Point((float)local.X, (float)(height - local.Y));
            var kind = type switch { 1 => PointerKind.Down, 2 => PointerKind.Up, _ => PointerKind.Move };
            Pointer?.Invoke(new PointerEvent(kind, position));
        }

        public bool Present(nint pixels, int width, int height, int rowBytes)
        {
            var pool = ObjC.NewPool();
            try
            {
                if (_drawableSize != (width, height))
                {
                    ObjC.SetSize(_layer, "setDrawableSize:", new ObjC.CGSize(width, height));
                    _drawableSize = (width, height);
                }

                var drawable = ObjC.Send(_layer, "nextDrawable");
                if (drawable == 0)
                    return false;

                var texture = ObjC.Send(drawable, "texture");
                var copyWidth = Math.Min((ulong)width, ObjC.SendULong(texture, "width"));
                var copyHeight = Math.Min((ulong)height, ObjC.SendULong(texture, "height"));
                ObjC.ReplaceRegion(texture, new ObjC.MTLRegion(copyWidth, copyHeight), 0, pixels, (nuint)rowBytes);

                var commands = ObjC.Send(_queue, "commandBuffer");
                ObjC.Send(commands, "presentDrawable:", drawable);
                ObjC.Send(commands, "commit");
                return true;
            }
            finally
            {
                ObjC.DrainPool(pool);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ObjC.Send(Handle, "close");
            ObjC.Send(Handle, "release");
        }
    }

    /// <summary>Objective-C runtime helpers. Struct returns use objc_msgSend_stret on x64 only.</summary>
    private static unsafe class ObjC
    {
        private const string Lib = "/usr/lib/libobjc.A.dylib";

        [StructLayout(LayoutKind.Sequential)]
        public readonly record struct CGPoint(double X, double Y);

        [StructLayout(LayoutKind.Sequential)]
        public readonly record struct CGSize(double Width, double Height);

        [StructLayout(LayoutKind.Sequential)]
        public readonly record struct CGRect(double X, double Y, double Width, double Height);

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct MTLRegion(ulong width, ulong height)
        {
            public readonly ulong X = 0, Y = 0, Z = 0, Width = width, Height = height, Depth = 1;
        }

        [DllImport("/System/Library/Frameworks/Metal.framework/Metal")]
        public static extern nint MTLCreateSystemDefaultDevice();

        [DllImport(Lib)] private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
        [DllImport(Lib)] private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint Msg(nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint Msg(nint r, nint s, nint a);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint Msg(nint r, nint s, long a);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint Msg(nint r, nint s, byte a);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern ulong MsgULong(nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern byte MsgByte(nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint MsgUtf8(nint r, nint s, [MarshalAs(UnmanagedType.LPUTF8Str)] string a);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint MsgInitWindow(nint r, nint s, CGRect rect, nuint style, nuint backing, byte defer);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern nint MsgNextEvent(nint r, nint s, ulong mask, nint date, nint mode, byte dequeue);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern void MsgSize(nint r, nint s, CGSize size);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern CGPoint MsgPoint(nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern CGPoint MsgConvert(nint r, nint s, CGPoint p, nint view);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern CGRect MsgRect(nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend_stret")] private static extern void MsgRectStret(CGRect* result, nint r, nint s);
        [DllImport(Lib, EntryPoint = "objc_msgSend")] private static extern void MsgReplace(nint r, nint s, MTLRegion region, nuint level, nint bytes, nuint rowBytes);

        public static nint Class(string name) => objc_getClass(name);
        public static nint Selector(string name) => sel_registerName(name);

        public static nint Send(nint receiver, string selector) => Msg(receiver, Selector(selector));
        public static nint Send(nint receiver, string selector, nint arg) => Msg(receiver, Selector(selector), arg);
        public static nint Send(nint receiver, string selector, long arg) => Msg(receiver, Selector(selector), arg);
        public static nint Send(nint receiver, string selector, byte arg) => Msg(receiver, Selector(selector), arg);
        public static ulong SendULong(nint receiver, string selector) => MsgULong(receiver, Selector(selector));
        public static ushort SendUShort(nint receiver, string selector) => (ushort)MsgULong(receiver, Selector(selector));
        public static byte SendByte(nint receiver, string selector) => MsgByte(receiver, Selector(selector));

        public static nint NSString(string value) => MsgUtf8(Class("NSString"), Selector("stringWithUTF8String:"), value);

        public static nint InitWindow(nint window, CGRect rect, nuint style, nuint backing, byte defer) =>
            MsgInitWindow(window, Selector("initWithContentRect:styleMask:backing:defer:"), rect, style, backing, defer);

        public static nint NextEvent(nint app, ulong mask, nint date, nint mode, byte dequeue) =>
            MsgNextEvent(app, Selector("nextEventMatchingMask:untilDate:inMode:dequeue:"), mask, date, mode, dequeue);

        public static void SetSize(nint receiver, string selector, CGSize size) => MsgSize(receiver, Selector(selector), size);
        public static CGPoint GetPoint(nint receiver, string selector) => MsgPoint(receiver, Selector(selector));
        public static CGPoint ConvertPoint(nint view, CGPoint windowPoint) => MsgConvert(view, Selector("convertPoint:fromView:"), windowPoint, 0);

        public static CGRect GetRect(nint receiver, string selector)
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
                return MsgRect(receiver, Selector(selector));

            CGRect result;
            MsgRectStret(&result, receiver, Selector(selector));
            return result;
        }

        public static void ReplaceRegion(nint texture, MTLRegion region, nuint level, nint bytes, nuint rowBytes) =>
            MsgReplace(texture, Selector("replaceRegion:mipmapLevel:withBytes:bytesPerRow:"), region, level, bytes, rowBytes);

        public static nint NewPool() => Send(Send(Class("NSAutoreleasePool"), "alloc"), "init");
        public static void DrainPool(nint pool) => Send(pool, "drain");
    }
}
