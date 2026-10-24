using Avalonia.Controls;
using Avalonia.Platform;
using Intuime.Core;

namespace Intuime.Views;

public sealed class KioskWebPage : Grid
{
    private NativeWebView? _view;
    private int _showToken;

    public event Action<string>? Failed;

    public bool DrawsInTheWindow { get; } = OperatingSystem.IsWindows();

    public KioskWebPage()
    {
        IsVisible = false;
        IsHitTestVisible = false;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Background = Avalonia.Media.Brushes.Black;
    }

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
            IsVisible = false;
            IsHitTestVisible = false;
            error = string.IsNullOrWhiteSpace(ex.Message)
                ? "That page could not open inside the kiosk."
                : ex.Message;
            return false;
        }
    }

    public void HidePage()
    {
        _showToken++;
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
}
