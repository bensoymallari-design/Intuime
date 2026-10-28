using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Intuime.Core;

namespace Intuime.Views;

public sealed class KioskWebPage : Grid
{
    private static readonly List<KioskWebPage> Pages = new();
    private static bool _guardHooked;

    private NativeWebView? _view;
    private int _showToken;
    private bool _opening;

    public event Action<string>? Failed;

    public bool DrawsInTheWindow { get; } = OperatingSystem.IsWindows();

    public KioskWebPage()
    {
        IsVisible = false;
        IsHitTestVisible = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Background = Avalonia.Media.Brushes.Black;
        Pages.Add(this);
        HookGuard();
    }

    public void Release() => Pages.Remove(this);

    public void Fit(double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        if (_view == null)
            return;
        _view.Width = Width;
        _view.Height = Height;
    }

    public bool TryShow(string? raw, out string error)
    {
        if (!WebAddresses.TryNormalize(raw, out var url))
        {
            error = "Use a web address, like https://example.com.";
            return false;
        }

        try
        {
            _opening = true;
            Ensure();
            error = "";
            var token = ++_showToken;
            IsVisible = true;
            IsHitTestVisible = true;
            _view!.NavigationCompleted += OnCompleted;
            void OnCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
            {
                if (_view != null)
                    _view.NavigationCompleted -= OnCompleted;
                if (!IsVisible || token != _showToken || e.IsSuccess)
                    return;
                Failed?.Invoke("That page did not open inside the kiosk.");
            }
            _view.Source = new Uri(url);
            _view.Focus();
            return true;
        }
        catch (Exception ex)
        {
            error = Explain(ex);
            Abandon();
            return false;
        }
    }

    public void HidePage()
    {
        _showToken++;
        _opening = false;
        IsVisible = false;
        IsHitTestVisible = false;
        if (_view == null)
            return;
        try
        {
            _view.Source = new Uri("about:blank");
        }
        catch (Exception)
        {
        }
    }

    private void Ensure()
    {
        if (_view != null)
            return;

        var view = new NativeWebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };
        view.EnvironmentRequested += (_, args) =>
        {
            args.EnableDevTools = false;
            if (args is LinuxWpeWebViewEnvironmentRequestedEventArgs wpe)
                wpe.PreferWebKitGtkInstead = true;
            if (args is GtkWebViewEnvironmentRequestedEventArgs gtk)
                gtk.ExperimentalOffscreen = false;
            if (args is WindowsWebView2EnvironmentRequestedEventArgs windows)
                windows.ExperimentalOffscreen = true;
        };
        view.NavigationStarted += (_, args) =>
        {
            var uri = args.Request;
            if (uri == null || uri.Scheme == "about")
                return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                args.Cancel = true;
        };
        view.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            var uri = args.Request;
            if (uri != null && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                view.Navigate(uri);
        };
        _view = view;
        Children.Add(view);
    }

    private static void HookGuard()
    {
        if (_guardHooked)
            return;
        _guardHooked = true;
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            if (!IsWebFault(args.Exception))
                return;
            args.Handled = true;
            var message = Explain(args.Exception);
            foreach (var page in Pages.ToArray())
                page.Fail(message);
        };
    }

    private void Fail(string message)
    {
        if (!_opening && _view == null)
            return;
        Abandon();
        Failed?.Invoke(message);
    }

    private void Abandon()
    {
        _opening = false;
        _showToken++;
        IsVisible = false;
        IsHitTestVisible = false;
        var view = _view;
        _view = null;
        if (view == null)
            return;
        try
        {
            Children.Remove(view);
        }
        catch (Exception)
        {
        }
    }

    private static bool IsWebFault(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            var text = current.GetType().FullName + " " + current.Message + " " + current.StackTrace;
            if (text.Contains("NativeWebView", StringComparison.Ordinal) ||
                text.Contains("WebView", StringComparison.Ordinal) ||
                text.Contains("webkit", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("WebKit", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string Explain(Exception exception)
    {
        var missing = false;
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is DllNotFoundException or FileNotFoundException or TypeLoadException)
                missing = true;
            var message = current.Message ?? "";
            if (message.Contains("WebView2", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("couldn't", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("could not", StringComparison.OrdinalIgnoreCase)))
                missing = true;
        }
        return missing
            ? "The web viewer is missing on this computer, so the page stayed closed."
            : "That page could not open inside the kiosk. Intuime stayed open.";
    }
}
