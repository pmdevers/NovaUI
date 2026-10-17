using System.Runtime.InteropServices;

namespace NovaUI.Desktop.Platforms.Linux;

/// <summary>Minimal Xlib bindings (64-bit Linux: C long == nint).</summary>
internal static unsafe class X11
{
    private const string Lib = "libX11.so.6";

    public const int KeyPress = 2;
    public const int ButtonPress = 4;
    public const int ButtonRelease = 5;
    public const int MotionNotify = 6;
    public const int ConfigureNotify = 22;
    public const int ClientMessage = 33;

    public const long KeyPressMask = 1 << 0;
    public const long ButtonPressMask = 1 << 2;
    public const long ButtonReleaseMask = 1 << 3;
    public const long PointerMotionMask = 1 << 6;
    public const long ExposureMask = 1 << 15;
    public const long StructureNotifyMask = 1 << 17;

    public const int EventSize = 192;

    [DllImport(Lib)] public static extern nint XOpenDisplay(nint name);
    [DllImport(Lib)] public static extern int XCloseDisplay(nint display);
    [DllImport(Lib)] public static extern int XDefaultScreen(nint display);
    [DllImport(Lib)] public static extern nint XRootWindow(nint display, int screen);
    [DllImport(Lib)] public static extern nint XCreateSimpleWindow(nint display, nint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
    [DllImport(Lib)] public static extern int XSelectInput(nint display, nint window, nint mask);
    [DllImport(Lib)] public static extern int XStoreName(nint display, nint window, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Lib)] public static extern nint XInternAtom(nint display, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int onlyIfExists);
    [DllImport(Lib)] public static extern int XSetWMProtocols(nint display, nint window, nint* protocols, int count);
    [DllImport(Lib)] public static extern int XMapWindow(nint display, nint window);
    [DllImport(Lib)] public static extern int XDestroyWindow(nint display, nint window);
    [DllImport(Lib)] public static extern int XPending(nint display);
    [DllImport(Lib)] public static extern int XNextEvent(nint display, byte* ev);
    [DllImport(Lib)] public static extern int XFlush(nint display);
    [DllImport(Lib)] public static extern int XFilterEvent(byte* ev, nint window);
    [DllImport(Lib)] public static extern nint XSetLocaleModifiers([MarshalAs(UnmanagedType.LPUTF8Str)] string modifiers);
    [DllImport(Lib)] public static extern nint XOpenIM(nint display, nint db, nint resName, nint resClass);
    [DllImport(Lib)] public static extern int XCloseIM(nint im);
    [DllImport(Lib, EntryPoint = "XCreateIC")]
    public static extern nint XCreateIC(nint im, [MarshalAs(UnmanagedType.LPUTF8Str)] string inputStyleName, nint inputStyle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string clientWindowName, nint clientWindow, nint terminator);
    [DllImport(Lib)] public static extern void XDestroyIC(nint ic);
    [DllImport(Lib)] public static extern int Xutf8LookupString(nint ic, byte* ev, byte* buffer, int bytes, nint* keysym, int* status);
    [DllImport(Lib)] public static extern int XLookupString(byte* ev, byte* buffer, int bytes, nint* keysym, nint status);

    [DllImport("libc", EntryPoint = "setlocale")]
    public static extern nint SetLocale(int category, [MarshalAs(UnmanagedType.LPUTF8Str)] string locale);
}
