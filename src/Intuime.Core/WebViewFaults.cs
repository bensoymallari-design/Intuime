namespace Intuime.Core;

/// <summary>
/// Decides which failures belong to the in-app web page.
/// Windows can throw these after the page has already started opening,
/// and the stack sometimes no longer mentions the web view.
/// </summary>
public static class WebViewFaults
{
    public static bool IsWebFault(Exception? exception, bool pageOpen = false)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (Hits(current))
                return true;
            if (pageOpen && IsRangeFault(current))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A focus glitch means the page itself can stay up. Closing it would hide a site that already opened.
    /// </summary>
    public static bool IsFocusGlitch(Exception? exception)
    {
        var text = Flatten(exception);
        if (text.Contains("movefocus", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Contains("rpc_e_changed_mode", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("0x80010106", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("thread mode", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("webkit", StringComparison.OrdinalIgnoreCase))
            return false;
        return text.Contains("expected range", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Hits(Exception current)
    {
        var text = (current.GetType().FullName ?? "") + " " + current.Message + " " + current.StackTrace;
        if (Contains(text, "webview") || Contains(text, "webkit") || Contains(text, "corewebview") || Contains(text, "movefocus"))
            return true;
        if (Contains(text, "rpc_e_changed_mode") || Contains(text, "0x80010106") || Contains(text, "thread mode"))
            return true;
        return false;
    }

    private static bool IsRangeFault(Exception current) =>
        current is ArgumentException && Contains(current.Message, "expected range");

    private static string Flatten(Exception? exception)
    {
        var text = "";
        for (var current = exception; current != null; current = current.InnerException)
            text += (current.GetType().FullName ?? "") + " " + current.Message + " " + current.StackTrace + " ";
        return text;
    }

    private static bool Contains(string? text, string value) =>
        text?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
}
