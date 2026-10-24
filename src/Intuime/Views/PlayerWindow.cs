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
    private readonly KioskWebPage _page = new();
    private string _openPageUrl = "";
    private readonly SceneSurface _pageButtons = new();
    private readonly LayoutTransformControl _pageButtonsHost = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false
    };
    private readonly Window? _buttonsWindow;
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
        _pageButtons.UsePlayback(_engine, assetRoot);
        _pageButtons.PassClicksThrough();
        _pageButtons.OpenUrlRequested += OpenUrl;
        _pageButtonsHost.LayoutTransform = _scale;
        _pageButtonsHost.Child = _pageButtons;
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
        _page.Failed += ShowStatus;
        if (OperatingSystem.IsLinux())
        {
            _pageButtonsHost.HorizontalAlignment = HorizontalAlignment.Left;
            _pageButtonsHost.VerticalAlignment = VerticalAlignment.Top;
            _buttonsWindow = new Window
            {
                Title = "Intuime",
                SystemDecorations = SystemDecorations.None,
                ExtendClientAreaToDecorationsHint = true,
                ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                CanResize = false,
                ShowActivated = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                SizeToContent = SizeToContent.Manual,
                Content = _pageButtonsHost
            };
        }
        else
            _buttonsWindow = null;
        var layers = new Grid();
        layers.Children.Add(_scaler);
        layers.Children.Add(_overlay);
        layers.Children.Add(_page);
        if (_buttonsWindow == null)
            layers.Children.Add(_pageButtonsHost);
        layers.Children.Add(_hint);
        layers.Children.Add(_status);
        Content = layers;

        _engine.SceneChanged += () =>
        {
            _fade = 0;
            _status.IsVisible = false;
            _page.HidePage();
            _pageButtonsHost.IsVisible = false;
            if (_buttonsWindow?.IsVisible == true)
                _buttonsWindow.Hide();
            DisposeHosts();
            _surface.SetOmit(null);
            _surface.Rebuild();
            CreateHosts();
            LayoutVideos();
        };
        _engine.Changed += () =>
        {
            _surface.Sync();
            if (_pageButtonsHost.IsVisible)
                _pageButtons.Sync();
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
            UpdateTransports();
            if (_page.IsVisible)
            {
                var keys = LinuxWebFrame.PollKeys();
                if (keys.Escape)
                {
                    Close();
                    return;
                }
                if (keys.Left)
                    _engine.KeyPressed("Left");
                if (keys.Right)
                    _engine.KeyPressed("Right");
            }
        };
        _timer.Start();
        Opened += (_, _) => Focus();
        Closed += (_, _) =>
        {
            _timer.Stop();
            if (_buttonsWindow != null)
                _buttonsWindow.Close();
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
        LayoutPage();
    }

    private void CreateHosts()
    {
        var vlc = MediaRuntime.Ensure();
        var scene = _engine.Current;
        if (vlc == null || scene == null)
        {
            scene?.PullLegacyBackground();
            var wantsMedia = scene?.Elements.Any(element => element.Kind is ElementKind.Video or ElementKind.Audio) == true
                || scene?.Backgrounds.Any(layer => layer.Video && !string.IsNullOrWhiteSpace(layer.Source)) == true;
            if (!MediaRuntime.IsAvailable && wantsMedia)
                ShowStatus(MediaRuntime.Status);
            return;
        }

        scene.PullLegacyBackground();
        var videos = scene.Backgrounds
            .Select((layer, index) => (layer, index))
            .Where(item => item.layer.Video && !string.IsNullOrWhiteSpace(item.layer.Source))
            .ToList();
        foreach (var (layer, index) in videos)
            CreateBackdrop(vlc, scene, layer, index);
        if (videos.Count > 0)
        {
            _surface.SetOmit(CoveredByBackdrop);
            _surface.Rebuild();
        }

        foreach (var element in scene.Elements.Where(element => element.Kind is ElementKind.Video or ElementKind.Audio))
        {
            try
            {
                var player = new MediaPlayer(vlc)
                {
                    EnableKeyInput = false,
                    EnableMouseInput = false,
                    Volume = 80
                };
                var host = new Host(player, element.Loop) { ElementId = element.Id };
                player.EndReached += (_, _) => Dispatcher.UIThread.Post(() => OnEnded(host));
                player.Playing += (_, _) => Dispatcher.UIThread.Post(() => OnPlaying(host));
                VideoView? view = null;
                if (element.Kind == ElementKind.Video)
                {
                    var videoId = element.Id;
                    host.Foreground = new SceneSurface();
                    host.Foreground.UsePlayback(_engine, _assetRoot);
                    host.Foreground.UseOverlay(element.X, element.Y, element.Width, element.Height,
                        candidate => OverlayOwner(candidate) == videoId, videoId);
                    host.Foreground.Rebuild();
                    host.ForegroundScale = new ScaleTransform(Math.Max(0.01, _scale.ScaleX), Math.Max(0.01, _scale.ScaleY));
                    var foregroundHost = new LayoutTransformControl
                    {
                        LayoutTransform = host.ForegroundScale,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Child = host.Foreground,
                        ZIndex = 0
                    };
                    host.Bar = BuildPlayBar(host);
                    host.Bar.IsVisible = element.ShowControls;
                    host.Bar.IsHitTestVisible = element.ShowControls;
                    var layer = new Grid { Children = { foregroundHost, host.Bar } };
                    view = new VideoView { MediaPlayer = player, Content = layer, ZIndex = 1 };
                    host.View = view;
                    _overlay.Children.Add(view);
                }
                _hosts[element.Id] = host;
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message);
            }
        }
    }

    private const string BackdropPrefix = "bg:";

    private void CreateBackdrop(LibVLC vlc, Scene scene, BackgroundLayer layer, int index)
    {
        var path = ProjectStore.Resolve(_assetRoot, layer.Source);
        if (!File.Exists(path))
        {
            ShowStatus("Missing background video: " + layer.Source);
            return;
        }

        try
        {
            var box = layer.Box(_engine.Live.Width, _engine.Live.Height);
            var player = new MediaPlayer(vlc)
            {
                EnableKeyInput = false,
                EnableMouseInput = false,
                Mute = !layer.Sound,
                Volume = layer.Sound ? 80 : 0,
                AspectRatio = Math.Max(1, (int)Math.Round(box.Width)) + ":" + Math.Max(1, (int)Math.Round(box.Height)),
                Scale = 0
            };
            var host = new Host(player, layer.Loop) { Path = path, LayerId = layer.Id };
            player.EndReached += (_, _) => Dispatcher.UIThread.Post(() => OnEnded(host));
            host.Foreground = new SceneSurface();
            host.Foreground.UsePlayback(_engine, _assetRoot);
            host.Foreground.UseOverlay(box.X, box.Y, box.Width, box.Height,
                candidate => ElementOverVideo(candidate, box),
                null,
                picture => PictureOverVideo(scene, picture, index, box));
            host.Foreground.Rebuild();
            host.ForegroundScale = new ScaleTransform(Math.Max(0.01, _scale.ScaleX), Math.Max(0.01, _scale.ScaleY));
            var foregroundHost = new LayoutTransformControl
            {
                LayoutTransform = host.ForegroundScale,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = host.Foreground
            };
            var view = new VideoView { MediaPlayer = player, Content = foregroundHost, ZIndex = 0 };
            host.View = view;
            _overlay.Children.Add(view);
            _hosts[BackdropPrefix + layer.Id] = host;
            LaunchWhenReady(host, view);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private bool PictureOverVideo(Scene scene, BackgroundLayer picture, int videoIndex, (double X, double Y, double Width, double Height) box)
    {
        var index = scene.Backgrounds.FindIndex(layer => layer.Id == picture.Id);
        return index > videoIndex && !picture.Video
            && picture.Hits(box.X, box.Y, box.Width, box.Height, _engine.Live.Width, _engine.Live.Height);
    }

    private bool ElementOverVideo(SceneElement candidate, (double X, double Y, double Width, double Height) box)
    {
        if (!candidate.IsVisible || candidate.Kind == ElementKind.Video || OverlayOwner(candidate) != null)
            return false;
        return candidate.X < box.X + box.Width &&
               candidate.X + candidate.Width > box.X &&
               candidate.Y < box.Y + box.Height &&
               candidate.Y + candidate.Height > box.Y;
    }

    private bool CoveredByBackdrop(SceneElement candidate)
    {
        var scene = _engine.Current;
        if (scene == null || !candidate.IsVisible || candidate.Kind == ElementKind.Video || OverlayOwner(candidate) != null)
            return false;
        return scene.Backgrounds.Any(layer => layer.Video && layer.Source.Length > 0 && FullyInside(candidate, layer.Box(_engine.Live.Width, _engine.Live.Height)));
    }

    private static bool FullyInside(SceneElement candidate, (double X, double Y, double Width, double Height) box) =>
        candidate.X >= box.X &&
        candidate.Y >= box.Y &&
        candidate.X + candidate.Width <= box.X + box.Width &&
        candidate.Y + candidate.Height <= box.Y + box.Height;

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
                    host.Path = path;
                    host.Loop = element.Loop;
                    StartMedia(host);
                    break;
                case MediaTransport.Pause:
                    host.Ended = false;
                    host.Player.SetPause(true);
                    break;
                case MediaTransport.Stop:
                    host.Ended = false;
                    host.PendingSeekMs = null;
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
        foreach (var layer in scene.Backgrounds.Where(layer => layer.Video))
        {
            if (!_hosts.TryGetValue(BackdropPrefix + layer.Id, out var backdrop) || backdrop.View == null)
                continue;
            var box = layer.Box(_engine.Live.Width, _engine.Live.Height);
            Canvas.SetLeft(backdrop.View, originX + box.X * scale);
            Canvas.SetTop(backdrop.View, originY + box.Y * scale);
            backdrop.View.Width = Math.Max(1, box.Width * scale);
            backdrop.View.Height = Math.Max(1, box.Height * scale);
            if (backdrop.ForegroundScale != null)
            {
                backdrop.ForegroundScale.ScaleX = scale;
                backdrop.ForegroundScale.ScaleY = scale;
            }
            backdrop.Foreground?.MoveOverlay(box.X, box.Y, box.Width, box.Height);
            backdrop.Foreground?.Sync();
        }
        foreach (var element in scene.Elements.Where(element => element.Kind == ElementKind.Video))
        {
            if (!_hosts.TryGetValue(element.Id, out var host) || host.View == null)
                continue;
            host.View.IsVisible = element.IsVisible;
            Canvas.SetLeft(host.View, originX + element.X * scale);
            Canvas.SetTop(host.View, originY + element.Y * scale);
            host.View.Width = Math.Max(1, element.Width * scale);
            host.View.Height = Math.Max(1, element.Height * scale);
            if (host.ForegroundScale != null)
            {
                host.ForegroundScale.ScaleX = scale;
                host.ForegroundScale.ScaleY = scale;
            }
            host.Foreground?.MoveOverlay(element.X, element.Y, element.Width, element.Height);
            host.Foreground?.Sync();
            if (host.Bar != null)
            {
                var showBar = element.IsVisible && element.ShowControls;
                host.Bar.IsVisible = showBar;
                host.Bar.IsHitTestVisible = showBar;
            }
        }

        LayoutPage();
    }

    private void LayoutPage()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (!_page.IsVisible)
        {
            LinuxWebFrame.Hide();
            if (_buttonsWindow?.IsVisible == true)
                _buttonsWindow.Hide();
            return;
        }

        var scale = _scale.ScaleX <= 0 ? 1 : _scale.ScaleX;
        var width = Math.Max(1, _engine.Live.Width * scale);
        var height = Math.Max(1, _engine.Live.Height * scale);
        var originX = Math.Max(0, (Bounds.Width - width) / 2);
        var originY = Math.Max(0, (Bounds.Height - height) / 2);
        _page.Fit(width, height);
        if (_pageButtonsHost.IsVisible)
            _pageButtons.MoveOverlay(0, 0, _engine.Live.Width, _engine.Live.Height);
        var buttons = _buttonsWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (!_page.DrawsInTheWindow)
            LinuxWebFrame.Show(handle, (int)originX, (int)originY, (int)width, (int)height, buttons);
        PlacePageButtons(originX, originY, width, height);
    }

    private void PlacePageButtons(double originX, double originY, double width, double height)
    {
        if (_buttonsWindow == null || !_page.IsVisible)
            return;
        var shapes = PageShapes();
        if (shapes.Count == 0)
        {
            if (_buttonsWindow.IsVisible)
                _buttonsWindow.Hide();
            return;
        }

        _buttonsWindow.Position = this.PointToScreen(new Point(originX, originY));
        _buttonsWindow.Width = width;
        _buttonsWindow.Height = height;
        _buttonsWindow.Background = Brushes.Transparent;
        if (!_buttonsWindow.IsVisible)
            _buttonsWindow.Show(this);
        var xid = _buttonsWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (xid == IntPtr.Zero)
            return;
        LinuxWebFrame.ShapeTo(xid, shapes);
    }

    private List<(int X, int Y, int Width, int Height)> PageShapes()
    {
        var shapes = new List<(int X, int Y, int Width, int Height)>();
        var scene = _engine.Current;
        if (scene == null)
            return shapes;
        var scale = _scale.ScaleX <= 0 ? 1 : _scale.ScaleX;
        foreach (var element in scene.Elements)
        {
            if (!element.IsVisible || !ElementFactory.CapturesPointer(element) || IsOpenWebLink(element, _openPageUrl))
                continue;
            if (element.Opacity * element.EffectOpacity <= 0.04)
                continue;
            var x = (int)Math.Round((element.X + element.EffectShiftX) * scale);
            var y = (int)Math.Round((element.Y + element.EffectShiftY) * scale);
            var width = (int)Math.Round(element.Width * scale);
            var height = (int)Math.Round(element.Height * scale);
            shapes.Add((Math.Max(0, x), Math.Max(0, y), Math.Max(1, width), Math.Max(1, height)));
        }
        return shapes;
    }

    private Border BuildPlayBar(Host host)
    {
        var toggle = new Button
        {
            Content = "Play",
            MinWidth = 96,
            Padding = new Thickness(12, 6),
            Background = Painting.Brush("#E7A23A"),
            Foreground = Painting.Brush("#1A1204"),
            CornerRadius = new CornerRadius(8)
        };
        toggle.Click += (_, _) => TogglePlayback(host);
        var readout = new TextBlock
        {
            Text = "Ready",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 280
        };
        var slider = new Slider { Minimum = 0, Maximum = 1000, VerticalAlignment = VerticalAlignment.Center };
        slider.AddHandler(PointerPressedEvent, (_, _) => host.Scrubbing = true, RoutingStrategies.Tunnel);
        slider.AddHandler(PointerReleasedEvent, (_, _) =>
        {
            host.Scrubbing = false;
            SeekFromSlider(host);
        }, RoutingStrategies.Tunnel);
        slider.ValueChanged += (_, _) =>
        {
            if (host.Scrubbing)
                SeekFromSlider(host);
        };
        var frame = new TextBox
        {
            Width = 72,
            Watermark = "Frame",
            VerticalAlignment = VerticalAlignment.Center
        };
        frame.GotFocus += (_, _) => host.FrameEditing = true;
        frame.LostFocus += (_, _) =>
        {
            host.FrameEditing = false;
            SeekFrame(host);
        };
        frame.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            SeekFrame(host);
            e.Handled = true;
        };
        host.Toggle = toggle;
        host.Readout = readout;
        host.Slider = slider;
        host.FrameBox = frame;
        var times = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { toggle, readout }
        };
        var seek = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,72"),
            ColumnSpacing = 8,
            Margin = new Thickness(0, 6, 0, 0)
        };
        seek.Children.Add(slider);
        Grid.SetColumn(frame, 1);
        seek.Children.Add(frame);
        var stack = new StackPanel();
        stack.Children.Add(times);
        stack.Children.Add(seek);
        return new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(8, 0, 8, 8),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(10),
            Background = Painting.Brush("#F0101820"),
            ZIndex = 20,
            IsHitTestVisible = true,
            Child = stack
        };
    }

    private void OnEnded(Host host)
    {
        if (host.Loop && !string.IsNullOrEmpty(host.Path))
        {
            StartMedia(host);
            return;
        }
        host.Ended = true;
        host.PendingSeekMs = null;
        UpdateTransport(host);
    }

    private void OnPlaying(Host host)
    {
        RememberFps(host);
        if (host.PendingSeekMs is not long ms)
            return;
        host.PendingSeekMs = null;
        host.Player.Time = ms;
        host.Player.SetPause(true);
        host.Ended = false;
        UpdateTransport(host);
    }

    private void TogglePlayback(Host host)
    {
        if (!EnsureSource(host))
            return;
        if (host.Ended || host.Player.State is VLCState.Stopped or VLCState.NothingSpecial or VLCState.Ended or VLCState.Error)
        {
            StartMedia(host);
            return;
        }
        host.Player.SetPause(host.Player.State != VLCState.Paused);
        host.Ended = false;
        UpdateTransport(host);
    }

    private void LaunchWhenReady(Host host, VideoView view)
    {
        void Go()
        {
            if (host.Started)
                return;
            host.Started = true;
            Dispatcher.UIThread.Post(() => StartMedia(host), DispatcherPriority.Background);
        }

        if (view.IsLoaded)
            Go();
        else
            view.Loaded += (_, _) => Go();
    }

    private void StartMedia(Host host)
    {
        var vlc = MediaRuntime.Shared;
        if (vlc == null || string.IsNullOrEmpty(host.Path))
            return;
        host.Ended = false;
        host.Player.Play(new LibVLCSharp.Shared.Media(vlc, host.Path, FromType.FromPath));
    }

    private void SeekFromSlider(Host host)
    {
        if (host.Slider == null)
            return;
        var length = host.Player.Length;
        if (length <= 0)
            return;
        SeekAndPause(host, (long)(host.Slider.Value / host.Slider.Maximum * length));
    }

    private void SeekFrame(Host host)
    {
        if (host.FrameBox == null || !int.TryParse(host.FrameBox.Text, out var frame))
            return;
        var fps = host.Fps > 1 ? host.Fps : 24;
        SeekAndPause(host, (long)(Math.Max(0, frame) / fps * 1000.0));
    }

    private bool EnsureSource(Host host)
    {
        if (!string.IsNullOrEmpty(host.Path) && File.Exists(host.Path))
            return true;
        var element = string.IsNullOrEmpty(host.ElementId)
            ? null
            : _engine.Current?.Elements.FirstOrDefault(item => item.Id == host.ElementId);
        if (element == null || string.IsNullOrWhiteSpace(element.Source))
        {
            ShowStatus("Choose a video file first.");
            return false;
        }
        var path = ProjectStore.Resolve(_assetRoot, element.Source);
        if (!File.Exists(path))
        {
            ShowStatus("Missing media file: " + element.Source);
            return false;
        }
        host.Path = path;
        host.Loop = element.Loop;
        return true;
    }

    private void SeekAndPause(Host host, long milliseconds)
    {
        if (!EnsureSource(host))
            return;
        milliseconds = Math.Max(0, milliseconds);
        host.Ended = false;
        if (host.Player.State is VLCState.Stopped or VLCState.NothingSpecial or VLCState.Ended or VLCState.Error || host.Player.Media == null)
        {
            host.PendingSeekMs = milliseconds;
            StartMedia(host);
            return;
        }
        host.Player.Time = milliseconds;
        host.Player.SetPause(true);
        UpdateTransport(host);
    }

    private static void RememberFps(Host host)
    {
        var media = host.Player.Media;
        if (media == null)
            return;
        foreach (var track in media.Tracks)
        {
            if (track.TrackType != TrackType.Video)
                continue;
            var video = track.Data.Video;
            if (video.FrameRateDen > 0 && video.FrameRateNum > 0)
                host.Fps = video.FrameRateNum / (double)video.FrameRateDen;
        }
    }

    private void UpdateTransports()
    {
        foreach (var host in _hosts.Values)
            UpdateTransport(host);
    }

    private static void UpdateTransport(Host host)
    {
        if (host.Toggle == null || host.Readout == null || host.Slider == null)
            return;
        var state = host.Player.State;
        var ended = host.Ended || state == VLCState.Ended;
        host.Toggle.Content = ended ? "Play again" : state == VLCState.Playing ? "Pause" : "Play";
        var time = host.Player.Time;
        var length = host.Player.Length;
        var label = ended ? "Ended" : state == VLCState.Playing ? "Playing" : state == VLCState.Paused ? "Paused" : "Ready";
        var fps = host.Fps > 1 ? host.Fps : 0;
        var frame = fps > 1 && time >= 0 ? (int)Math.Round(time / 1000.0 * fps) : 0;
        var clock = length > 0
            ? $"{Clock(time)} / {Clock(length)}" + (fps > 1 ? $"   frame {frame}" : "")
            : $"{Clock(Math.Max(0, time))} / 0:00";
        host.Readout.Text = $"{label}   {clock}";
        if (!host.Scrubbing && length > 0)
            host.Slider.Value = Math.Clamp(time / (double)length * host.Slider.Maximum, 0, host.Slider.Maximum);
        if (!host.FrameEditing && fps > 1 && host.FrameBox != null)
            host.FrameBox.Text = frame.ToString();
    }

    private static string Clock(long milliseconds)
    {
        if (milliseconds < 0)
            milliseconds = 0;
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    private string? OverlayOwner(SceneElement candidate)
    {
        var scene = _engine.Current;
        return scene == null ? null : LayoutMath.VideoOverlayOwner(scene.Elements, candidate);
    }

    private void ShowStatus(string message)
    {
        _status.Text = message;
        _status.IsVisible = true;
    }

    private void OpenUrl(string url)
    {
        if (!_page.TryShow(url, out var error))
        {
            ShowStatus(error);
            return;
        }

        _status.IsVisible = false;
        _hint.IsVisible = false;
        _openPageUrl = url;
        ShowPageButtons(url);
        LayoutPage();
    }

    private void ShowPageButtons(string url)
    {
        var width = Math.Max(1, _engine.Live.Width);
        var height = Math.Max(1, _engine.Live.Height);
        _pageButtons.UseOverlay(0, 0, width, height, element => !IsOpenWebLink(element, url));
        _pageButtons.PassClicksThrough();
        _pageButtons.Rebuild();
        _pageButtonsHost.IsVisible = true;
    }

    private static bool IsOpenWebLink(SceneElement element, string url)
    {
        if (element.Kind != ElementKind.WebLink)
            return false;
        return WebAddresses.TryNormalize(element.Url, out var link) &&
               WebAddresses.TryNormalize(url, out var open) &&
               string.Equals(link, open, StringComparison.OrdinalIgnoreCase);
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
                {
                    host.View.Content = null;
                    host.View.MediaPlayer = null;
                }
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

    private sealed class Host
    {
        public Host(MediaPlayer player, bool loop)
        {
            Player = player;
            Loop = loop;
        }

        public MediaPlayer Player { get; }
        public VideoView? View { get; set; }
        public SceneSurface? Foreground { get; set; }
        public ScaleTransform? ForegroundScale { get; set; }
        public Border? Bar { get; set; }
        public string ElementId { get; set; } = "";
        public string LayerId { get; set; } = "";
        public string Path { get; set; } = "";
        public bool Loop { get; set; }
        public bool Scrubbing { get; set; }
        public bool FrameEditing { get; set; }
        public bool Ended { get; set; }
        public bool Started { get; set; }
        public long? PendingSeekMs { get; set; }
        public double Fps { get; set; }
        public Button? Toggle { get; set; }
        public TextBlock? Readout { get; set; }
        public Slider? Slider { get; set; }
        public TextBox? FrameBox { get; set; }
    }
}
