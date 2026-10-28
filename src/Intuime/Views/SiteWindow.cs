using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Intuime.Core;

namespace Intuime.Views;

/// <summary>
/// Opens a site in a locked browser window. There is no close, minimize, or maximize.
/// The embedded viewer was closing the whole app on Windows, so the page stays in its own process.
/// </summary>
sealed class SiteWindow : IDisposable
{
    private readonly Process? _process;
    private readonly string? _profile;
    private readonly bool _lockFrame;

    private SiteWindow(Process? process, string? profile, bool lockFrame)
    {
        _process = process;
        _profile = profile;
        _lockFrame = lockFrame;
    }

    public static bool TryOpen(string url, PixelRect bounds, out SiteWindow? site, out string error)
    {
        site = null;
        var browser = FindBrowser();
        if (browser == null)
        {
            error = "Intuime could not find a browser on this computer, so the page stayed closed.";
            return false;
        }

        var profile = Path.Combine(Path.GetTempPath(), "intuime-site", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(profile);
            // A brand-new profile otherwise sits on the browser's first-run page, which is blank in a locked window.
            File.WriteAllBytes(Path.Combine(profile, "First Run"), Array.Empty<byte>());
            var edgeOnWindows = OperatingSystem.IsWindows() && SiteLaunch.IsEdge(browser);
            var start = new ProcessStartInfo
            {
                FileName = browser,
                UseShellExecute = false
            };
            foreach (var arg in Arguments(browser, url, profile, bounds))
                start.ArgumentList.Add(arg);
            var process = Process.Start(start);
            if (process == null)
            {
                error = "That site could not open, so you stayed on the scene.";
                return false;
            }
            site = new SiteWindow(process, profile, edgeOnWindows);
            error = "";
            return true;
        }
        catch (Exception)
        {
            try
            {
                if (Directory.Exists(profile))
                    Directory.Delete(profile, recursive: true);
            }
            catch (Exception)
            {
            }
            error = "That site could not open, so you stayed on the scene.";
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }
        try
        {
            _process?.Dispose();
        }
        catch (Exception)
        {
        }
        try
        {
            if (_profile != null && Directory.Exists(_profile))
                Directory.Delete(_profile, recursive: true);
        }
        catch (Exception)
        {
        }
    }

    private static IEnumerable<string> Arguments(string browser, string url, string profile, PixelRect bounds) =>
        SiteLaunch.Arguments(Path.GetFileName(browser), url, profile, bounds.X, bounds.Y, bounds.Width, bounds.Height, OperatingSystem.IsWindows());

    /// <summary>
    /// Edge paints in app mode, which still has a title bar. Drop that bar so only the arrow remains.
    /// </summary>
    public void HoldOpen(PixelRect bounds)
    {
        if (!_lockFrame || !OperatingSystem.IsWindows() || string.IsNullOrEmpty(_profile) || bounds.Width < 1 || bounds.Height < 1)
            return;
        var profile = _profile;
        EnumWindows((hwnd, _) =>
        {
            TryLock(hwnd, profile, bounds);
            return true;
        }, IntPtr.Zero);
    }

    private static void TryLock(IntPtr hwnd, string profile, PixelRect bounds)
    {
        if (!IsWindowVisible(hwnd))
            return;
        GetWindowThreadProcessId(hwnd, out var pid);
        var command = CommandLine(pid);
        if (command == null || command.IndexOf(profile, StringComparison.OrdinalIgnoreCase) < 0)
            return;
        if (!GetWindowRect(hwnd, out var rect))
            return;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 300 || height < 200)
            return;

        const int chrome = 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
        var style = GetWindowLong(hwnd, -16);
        var wanted = (style | unchecked((int)0x80000000) | 0x10000000) & ~chrome;
        var placed = rect.Left == bounds.X && rect.Top == bounds.Y && width == bounds.Width && height == bounds.Height;
        if (style == wanted && placed)
            return;
        if (style != wanted)
            SetWindowLong(hwnd, -16, wanted);
        SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0020 | 0x0004 | 0x0010);
    }

    private static string? CommandLine(uint pid)
    {
        var handle = OpenProcess(0x0400 | 0x0010, false, pid);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            var info = new ProcessBasicInformation();
            if (NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _) != 0)
                return null;
            if (!ReadPointer(handle, info.PebBaseAddress + 0x20, out var parameters))
                return null;
            var header = new byte[16];
            if (!ReadProcessMemory(handle, parameters + 0x70, header, header.Length, out _))
                return null;
            var length = BitConverter.ToUInt16(header, 0);
            var buffer = BitConverter.ToInt64(header, 8);
            if (length < 2 || buffer == 0)
                return null;
            var text = new byte[length];
            if (!ReadProcessMemory(handle, new IntPtr(buffer), text, text.Length, out _))
                return null;
            return Encoding.Unicode.GetString(text);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool ReadPointer(IntPtr process, IntPtr address, out IntPtr value)
    {
        var bytes = new byte[IntPtr.Size];
        value = IntPtr.Zero;
        if (!ReadProcessMemory(process, address, bytes, bytes.Length, out _))
            return false;
        value = IntPtr.Size == 8 ? new IntPtr(BitConverter.ToInt64(bytes, 0)) : new IntPtr(BitConverter.ToInt32(bytes, 0));
        return value != IntPtr.Zero;
    }

    internal static void KeepAbove(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return;
        if (OperatingSystem.IsWindows())
        {
            SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            return;
        }
        LinuxWebFrame.KeepAbove(window);
    }

    private static string? FindBrowser()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var path in WindowsBrowsers())
            {
                if (File.Exists(path))
                    return path;
            }
        }

        foreach (var name in new[] { "microsoft-edge", "microsoft-edge-stable", "msedge", "google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "firefox" })
        {
            var found = FindOnPath(name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static IEnumerable<string> WindowsBrowsers()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root))
                continue;
            yield return Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
            yield return Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe");
        }
    }

    internal static void ClipTo(IntPtr window, IReadOnlyList<(int X, int Y, int Width, int Height)> rectangles)
    {
        if (window == IntPtr.Zero || rectangles.Count == 0)
            return;
        if (OperatingSystem.IsLinux())
        {
            LinuxWebFrame.ShapeTo(window, rectangles);
            return;
        }
        if (!OperatingSystem.IsWindows())
            return;

        IntPtr combined = IntPtr.Zero;
        foreach (var rect in rectangles)
        {
            var piece = CreateRectRgn(rect.X, rect.Y, rect.X + Math.Max(1, rect.Width), rect.Y + Math.Max(1, rect.Height));
            if (combined == IntPtr.Zero)
            {
                combined = piece;
                continue;
            }
            var next = CreateRectRgn(0, 0, 0, 0);
            CombineRgn(next, combined, piece, 2);
            DeleteObject(combined);
            DeleteObject(piece);
            combined = next;
        }
        SetWindowRgn(window, combined, true);
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lparam);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr read);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref ProcessBasicInformation info, int size, out int returned);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr sourceA, IntPtr sourceB, int mode);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir, name);
            if (File.Exists(full))
                return full;
        }
        return null;
    }
}
