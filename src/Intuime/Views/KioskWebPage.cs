using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private bool _broken;
    private string _brokenMessage = "";
    private int _closing;

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
        InstallGuard();
    }

    public static void InstallGuard()
    {
        if (_guardHooked)
            return;
        _guardHooked = true;
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            if (!WebViewFaults.IsWebFault(args.Exception, PageIsOpen))
                return;
            args.Handled = true;
            if (WebViewFaults.IsFocusGlitch(args.Exception))
                return;
            ScheduleClose(Explain(args.Exception));
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            if (!WebViewFaults.IsWebFault(args.Exception, PageIsOpen))
                return;
            args.SetObserved();
            if (WebViewFaults.IsFocusGlitch(args.Exception))
                return;
            ScheduleClose(Explain(args.Exception));
        };
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
            _broken = false;
            _brokenMessage = "";
            _opening = true;
            Ensure();
            if (_broken)
            {
                error = string.IsNullOrWhiteSpace(_brokenMessage)
                    ? "That page could not open inside the kiosk. Intuime stayed open."
                    : _brokenMessage;
                Abandon();
                return false;
            }
            error = "";
            var token = ++_showToken;
            IsVisible = true;
            IsHitTestVisible = true;
            _view!.NavigationCompleted += OnCompleted;
            void OnCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
            {
                var view = _view;
                if (view != null)
                    view.NavigationCompleted -= OnCompleted;
                if (view == null || !IsVisible || token != _showToken)
                    return;
                if (!e.IsSuccess)
                {
                    Failed?.Invoke("That page did not open inside the kiosk.");
                    return;
                }
                // The native page rejects focus until it has finished opening. Asking earlier kills the app on Windows.
                view.Focusable = true;
                try
                {
                    view.Focus();
                }
                catch (Exception ex)
                {
                    Survive(ex, closePage: false);
                }
            }
            _view.Source = new Uri(url);
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
        _broken = false;
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

        var view = new SafeWebView(this)
        {
            Focusable = false,
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

    private void Survive(Exception ex, bool closePage)
    {
        if (!closePage || !WebViewFaults.IsWebFault(ex, pageOpen: true))
            return;
        _broken = true;
        _brokenMessage = Explain(ex);
        ScheduleClose(_brokenMessage);
    }

    private static bool PageIsOpen => Pages.Exists(page => page._opening || page._view != null);

    private static void ScheduleClose(string message)
    {
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var page in Pages.ToArray())
                    page.Fail(message);
            });
        }
        catch (Exception)
        {
        }
    }

    private void Fail(string message)
    {
        if (_closing > 0)
            return;
        if (!_opening && _view == null)
            return;
        _closing++;
        try
        {
            Abandon();
            Failed?.Invoke(message);
        }
        catch (Exception)
        {
        }
        finally
        {
            _closing--;
        }
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

    /// <summary>
    /// WebView2 throws from the window focus handler, which Avalonia does not catch.
    /// Swallowing it here is what keeps Play open when a scene shows a link.
    /// </summary>
    private sealed class SafeWebView : NativeWebView
    {
        private readonly KioskWebPage _page;

        public SafeWebView(KioskWebPage page) => _page = page;

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            try
            {
                base.OnGotFocus(e);
            }
            catch (Exception ex)
            {
                _page.Survive(ex, closePage: false);
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            try
            {
                base.OnLostFocus(e);
            }
            catch (Exception ex)
            {
                _page.Survive(ex, closePage: false);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try
            {
                base.OnAttachedToVisualTree(e);
            }
            catch (Exception ex)
            {
                _page.Survive(ex, closePage: true);
            }
        }
    }
}
