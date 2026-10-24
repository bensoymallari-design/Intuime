namespace Intuime.Core;

public static class WebAddresses
{
    public static bool TryNormalize(string? value, out string url)
    {
        url = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        if (text.StartsWith("//", StringComparison.Ordinal))
            text = "https:" + text;
        else if (!text.Contains("://", StringComparison.Ordinal))
            text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (string.IsNullOrEmpty(uri.Host))
            return false;

        url = uri.AbsoluteUri;
        return true;
    }
}
