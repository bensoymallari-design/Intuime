namespace Intuime.Core;

/// <summary>
/// Flags for the site browser. The window has no close, minimize, or maximize, so a visitor
/// cannot leave the page that way. The scene's own arrow is the way back.
/// </summary>
public static class SiteLaunch
{
    public static IReadOnlyList<string> Arguments(string browserFileName, string url, string profile, int x, int y, int width, int height)
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

        var args = new List<string>
        {
            "--user-data-dir=" + profile,
            "--no-first-run",
            "--no-default-browser-check",
            "--noerrdialogs",
            "--disable-session-crashed-bubble",
            "--disable-infobars",
            "--disable-features=Translate",
            "--new-window",
            "--window-position=" + x + "," + y,
            "--window-size=" + width + "," + height,
            "--kiosk"
        };
        if (name.Contains("edge", StringComparison.OrdinalIgnoreCase) || name.Contains("msedge", StringComparison.OrdinalIgnoreCase))
            args.Add("--edge-kiosk-type=fullscreen");
        args.Add(url);
        return args;
    }
}
