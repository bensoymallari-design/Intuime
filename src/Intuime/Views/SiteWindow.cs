using System.Diagnostics;
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

    private SiteWindow(Process? process, string? profile)
    {
        _process = process;
        _profile = profile;
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
            site = new SiteWindow(process, profile);
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
        SiteLaunch.Arguments(Path.GetFileName(browser), url, profile, bounds.X, bounds.Y, bounds.Width, bounds.Height);

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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
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
