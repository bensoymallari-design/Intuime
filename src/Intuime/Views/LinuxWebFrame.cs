using System.Runtime.InteropServices;

namespace Intuime.Views;

static class LinuxWebFrame
{
    private static readonly HashSet<IntPtr> Owned = new();
    private static readonly HashSet<IntPtr> Redirected = new();
    private static readonly XErrorHandler IgnoreError = (_, _) => 0;
    private static readonly byte[] Keymap = new byte[32];
    private static IntPtr _display;
    private static bool _handlerReady;
    private static int _escapeCode;
    private static int _leftCode;
    private static int _rightCode;
    private static bool _escapeWasDown;
    private static bool _leftWasDown;
    private static bool _rightWasDown;

    public static void Show(IntPtr playerWindow, int x, int y, int width, int height, IntPtr skip = default)
    {
        if (!OperatingSystem.IsLinux() || playerWindow == IntPtr.Zero || width < 1 || height < 1)
            return;
        var display = Display();
        if (display == IntPtr.Zero)
            return;

        foreach (var child in Children(display, playerWindow))
        {
            if (child == skip || child == IntPtr.Zero)
                continue;
            if (!Owned.Contains(child) && !IsTiny(display, child))
                continue;
            Owned.Add(child);
            Place(display, child, x, y, width, height);
        }
        XFlush(display);
    }

    public readonly record struct KeyEdges(bool Escape, bool Left, bool Right);

    public static KeyEdges PollKeys()
    {
        if (!OperatingSystem.IsLinux())
            return default;
        var display = Display();
        if (display == IntPtr.Zero)
            return default;
        EnsureKeyCodes(display);
        if (XQueryKeymap(display, Keymap) == 0)
            return default;
        var escape = Down(_escapeCode);
        var left = Down(_leftCode);
        var right = Down(_rightCode);
        var edges = new KeyEdges(escape && !_escapeWasDown, left && !_leftWasDown, right && !_rightWasDown);
        _escapeWasDown = escape;
        _leftWasDown = left;
        _rightWasDown = right;
        return edges;
    }

    public static void ShapeTo(IntPtr window, IReadOnlyList<(int X, int Y, int Width, int Height)> rectangles)
    {
        if (!OperatingSystem.IsLinux() || window == IntPtr.Zero || rectangles.Count == 0)
            return;
        var display = Display();
        if (display == IntPtr.Zero)
            return;
        var native = new XRectangle[rectangles.Count];
        for (var i = 0; i < rectangles.Count; i++)
        {
            var rect = rectangles[i];
            native[i] = new XRectangle
            {
                X = (short)Math.Clamp(rect.X, short.MinValue, short.MaxValue),
                Y = (short)Math.Clamp(rect.Y, short.MinValue, short.MaxValue),
                Width = (ushort)Math.Clamp(rect.Width, 1, ushort.MaxValue),
                Height = (ushort)Math.Clamp(rect.Height, 1, ushort.MaxValue)
            };
        }
        XShapeCombineRectangles(display, window, ShapeBounding, 0, 0, native, native.Length, ShapeSet, 0);
        XShapeCombineRectangles(display, window, ShapeInput, 0, 0, native, native.Length, ShapeSet, 0);
        XRaiseWindow(display, window);
        XFlush(display);
    }

    public static void KeepAbove(IntPtr window)
    {
        if (!OperatingSystem.IsLinux() || window == IntPtr.Zero)
            return;
        var display = Display();
        if (display == IntPtr.Zero)
            return;
        // A kiosk page is fullscreen, which sits above a normal topmost window.
        // Override-redirect leaves the window manager out so the arrow stays on top.
        // The first change unmaps the window, so every later tick maps it again.
        if (Redirected.Add(window))
        {
            var attributes = new byte[112];
            attributes[88] = 1;
            XChangeWindowAttributes(display, window, 512, attributes);
            XUnmapWindow(display, window);
        }
        XMapRaised(display, window);
        XFlush(display);
    }

    public static void Hide()
    {
        if (!OperatingSystem.IsLinux() || Owned.Count == 0)
            return;
        var display = Display();
        if (display == IntPtr.Zero)
            return;
        foreach (var window in Owned)
            XUnmapWindow(display, window);
        XFlush(display);
    }

    private static void Place(IntPtr display, IntPtr window, int x, int y, int width, int height)
    {
        XMoveResizeWindow(display, window, x, y, (uint)width, (uint)height);
        XMapWindow(display, window);
        foreach (var child in Children(display, window))
            Place(display, child, 0, 0, width, height);
    }

    private static bool IsTiny(IntPtr display, IntPtr window)
    {
        if (XGetGeometry(display, window, out _, out _, out _, out var width, out var height, out _, out _) == 0)
            return false;
        return width <= 8 || height <= 8;
    }

    private static List<IntPtr> Children(IntPtr display, IntPtr window)
    {
        var list = new List<IntPtr>();
        if (XQueryTree(display, window, out _, out _, out var children, out var count) == 0 || children == IntPtr.Zero)
            return list;
        try
        {
            for (var i = 0; i < count; i++)
                list.Add(Marshal.ReadIntPtr(children, i * IntPtr.Size));
        }
        finally
        {
            XFree(children);
        }
        return list;
    }

    private static void EnsureKeyCodes(IntPtr display)
    {
        if (_escapeCode != 0)
            return;
        _escapeCode = XKeysymToKeycode(display, 0xff1b);
        _leftCode = XKeysymToKeycode(display, 0xff51);
        _rightCode = XKeysymToKeycode(display, 0xff53);
    }

    private static bool Down(int keycode) =>
        keycode > 0 && (Keymap[keycode / 8] & (1 << (keycode % 8))) != 0;

    private static IntPtr Display()
    {
        if (_display != IntPtr.Zero)
            return _display;
        _display = XOpenDisplay(IntPtr.Zero);
        if (_display != IntPtr.Zero && !_handlerReady)
        {
            XSetErrorHandler(IgnoreError);
            _handlerReady = true;
        }
        return _display;
    }

    private const int ShapeBounding = 0;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct XRectangle
    {
        public short X;
        public short Y;
        public ushort Width;
        public ushort Height;
    }

    private delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XSetErrorHandler(XErrorHandler handler);

    [DllImport("libX11.so.6")]
    private static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent, out IntPtr children, out uint count);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    [DllImport("libX11.so.6")]
    private static extern int XGetGeometry(IntPtr display, IntPtr window, out IntPtr root, out int x, out int y, out uint width, out uint height, out uint border, out uint depth);

    [DllImport("libX11.so.6")]
    private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

    [DllImport("libX11.so.6")]
    private static extern int XMapWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XUnmapWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryKeymap(IntPtr display, byte[] keys);

    [DllImport("libX11.so.6")]
    private static extern int XKeysymToKeycode(IntPtr display, uint keysym);

    [DllImport("libX11.so.6")]
    private static extern int XRaiseWindow(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XMapRaised(IntPtr display, IntPtr window);

    [DllImport("libX11.so.6")]
    private static extern int XChangeWindowAttributes(IntPtr display, IntPtr window, ulong valuemask, byte[] attributes);

    [DllImport("libXext.so.6")]
    private static extern void XShapeCombineRectangles(IntPtr display, IntPtr window, int destination, int x, int y, XRectangle[] rectangles, int count, int operation, int ordering);
}
