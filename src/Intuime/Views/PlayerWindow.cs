using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Intuime.Core;
using Intuime.Media;
using Intuime.Rendering;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace Intuime.Views;

public sealed class PlayerWindow : Window
{
    private readonly PlaybackEngine _engine;
    private readonly string _assetRoot;
    private readonly SceneSurface _surface = new();
    private readonly LayoutTransformControl _scaler = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly Canvas _overlay = new();
    private readonly TextBlock _hint;
    private readonly TextBlock _status;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, Host> _hosts = new();
    private double _fade;
    private double _hintAge;

    public PlayerWindow(ExperienceDocument document, string assetRoot, bool fullscreen)
    {
        _assetRoot = assetRoot;
        _engine = new PlaybackEngine(document);
        Title = "Intuime Player — " + document.Name;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Width = Math.Min(1400, document.Width);
        Height = Math.Min(900, document.Height);
        if (fullscreen)
            WindowState = WindowState.FullScreen;
        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Intuime/Assets/intuime.png")));
        }
        catch (Exception)
        {
        }

        _surface.UsePlayback(_engine, assetRoot);
        _surface.OpenUrlRequested += OpenUrl;
        _scaler.LayoutTransform = _scale;
        _scaler.Child = _surface;
        _hint = new TextBlock
        {
            Text = "Esc exits    ·    F11 full screen    ·    arrow keys change scene",
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 18, 0, 0),
            FontSize = 14
        };
        _status = new TextBlock
        {
            Foreground = Painting.Brush("#E7A23A"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24),
            MaxWidth = 720,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            IsVisible = false
        };
        Content = new Grid
        {
            Children = { _scaler, _overlay, _hint, _status }
        };

        _engine.SceneChanged += () =>
        {
            _fade = 0;
            _status.IsVisible = false;
            DisposeHosts();
            _surface.Rebuild();
            CreateHosts();
            LayoutVideos();
        };
        _engine.Changed += () =>
        {
            _surface.Sync();
            LayoutVideos();
        };
        _engine.MediaCommand += ApplyMedia;
        _engine.OpenUrlRequested += OpenUrl;
        _engine.ExitRequestedEvent += () => Close();
        _engine.Start();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) =>
        {
            _engine.Tick();
            _fade = Math.Min(1, _fade + 0.1);
            _scaler.Opacity = _fade;
            _overlay.Opacity = _fade;
            _hintAge += 0.05;
            if (_hintAge > 2.4)
                _hint.Opacity = Math.Max(0, _hint.Opacity - 0.04);
            LayoutVideos();
        };
        _timer.Start();
        Opened += (_, _) => Focus();
        Closed += (_, _) =>
        {
            _timer.Stop();
            DisposeHosts();
        };
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        PointerPressed += (_, _) => Focus();
    }

    public void PressKey(string key) => _engine.KeyPressed(key);

    public void ToggleFullScreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Fit();
        LayoutVideos();
    }

    private void Fit()
    {
        var width = Math.Max(1, _engine.Live.Width);
        var height = Math.Max(1, _engine.Live.Height);
        var scale = Math.Min(Bounds.Width / width, Bounds.Height / height);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            scale = 1;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        _surface.Width = width;
        _surface.Height = height;
    }

    private void CreateHosts()
    {
        var vlc = MediaRuntime.Ensure();
        if (vlc == null || _engine.Current == null)
        {
            if (!MediaRuntime.IsAvailable && _engine.Current?.Elements.Any(element => element.Kind is ElementKind.Video or ElementKind.Audio) == true)
                ShowStatus(MediaRuntime.Status);
            return;
        }

        foreach (var element in _engine.Current.Elements.Where(element => element.Kind is ElementKind.Video or ElementKind.Audio))
        {
            try
            {
                var player = new MediaPlayer(vlc)
                {
                    EnableKeyInput = false,
                    EnableMouseInput = false,
                    Volume = 80
                };
                VideoView? view = null;
                if (element.Kind == ElementKind.Video)
                {
                    view = new VideoView { MediaPlayer = player };
                    _overlay.Children.Add(view);
                }
                _hosts[element.Id] = new Host(player, view);
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message);
            }
        }
    }

    private void ApplyMedia(MediaRequest request)
    {
        if (!_hosts.TryGetValue(request.ElementId, out var host))
            return;
        var element = _engine.Current?.Elements.FirstOrDefault(item => item.Id == request.ElementId);
        if (element == null)
            return;
        var path = ProjectStore.Resolve(_assetRoot, element.Source);
        try
        {
            switch (request.Transport)
            {
                case MediaTransport.Play:
                    if (!File.Exists(path))
                    {
                        ShowStatus("Missing media file: " + element.Source);
                        return;
                    }
                    var vlc = MediaRuntime.Shared;
                    if (vlc == null)
                        return;
                    host.Player.Play(new LibVLCSharp.Shared.Media(vlc, path, FromType.FromPath));
                    break;
                case MediaTransport.Pause:
                    host.Player.Pause();
                    break;
                case MediaTransport.Stop:
                    host.Player.Stop();
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void LayoutVideos()
    {
        var scene = _engine.Current;
        if (scene == null)
            return;
        var scale = _scale.ScaleX <= 0 ? 1 : _scale.ScaleX;
        var originX = Math.Max(0, (Bounds.Width - _engine.Live.Width * scale) / 2);
        var originY = Math.Max(0, (Bounds.Height - _engine.Live.Height * scale) / 2);
        foreach (var element in scene.Elements.Where(element => element.Kind == ElementKind.Video))
        {
            if (!_hosts.TryGetValue(element.Id, out var host) || host.View == null)
                continue;
            host.View.IsVisible = element.IsVisible;
            Canvas.SetLeft(host.View, originX + element.X * scale);
            Canvas.SetTop(host.View, originY + element.Y * scale);
            host.View.Width = Math.Max(1, element.Width * scale);
            host.View.Height = Math.Max(1, element.Height * scale);
        }
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }
        _engine.KeyPressed(e.Key.ToString());
        e.Handled = true;
    }

    private void DisposeHosts()
    {
        foreach (var host in _hosts.Values)
        {
            try
            {
                if (host.View != null)
                    host.View.MediaPlayer = null;
                host.Player.Stop();
                host.Player.Dispose();
            }
            catch (Exception)
            {
            }
        }
        _hosts.Clear();
        _overlay.Children.Clear();
    }

    private sealed record Host(MediaPlayer Player, VideoView? View);
}
