namespace Intuime.Core;

/// <summary>
/// Flags for the site browser. The window has no close, minimize, or maximize, so a visitor
/// cannot leave the page that way. The scene's own arrow is the way back.
/// </summary>
public static class SiteLaunch
{
    public static bool IsEdge(string? browserFileName)
    {
        var name = browserFileName ?? "";
        return name.Contains("edge", StringComparison.OrdinalIgnoreCase)
            || name.Contains("msedge", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> Arguments(string browserFileName, string url, string profile, int x, int y, int width, int height, bool windows = false)
    {
        var name = browserFileName ?? "";
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (name.Contains("firefox", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                "-profile", profile,
                "-no-remote",
                "-kiosk",
                url
            };
        }

        // Edge's fullscreen kiosk (--edge-kiosk-type=fullscreen) stays on a white page on a normal
        // Windows PC. App mode is the launch that actually paints the site. The frame is removed after.
        var edgeApp = windows && IsEdge(name);
        var args = new List<string>
        {
            "--user-data-dir=" + profile,
            "--no-first-run",
            "--disable-fre",
            "--no-default-browser-check",
            "--noerrdialogs",
            "--disable-session-crashed-bubble",
            "--disable-infobars",
            "--new-window",
            "--window-position=" + x + "," + y,
            "--window-size=" + width + "," + height
        };
        if (edgeApp)
            args.Add("--app=" + url);
        else
        {
            args.Add("--kiosk");
            args.Add(url);
        }
        return args;
    }
}
