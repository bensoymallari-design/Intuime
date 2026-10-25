using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Intuime.Core;
using Intuime.Rendering;

namespace Intuime.Views;

public static class InsertDesigns
{
    private static Action? _dismissDesigns;
    private static Action? _dismissIcons;

    public static MenuItem KindMenu(ElementKind kind, string label, Action<ElementPreset> insert)
    {
        var parent = new MenuItem { Header = label };
        foreach (var preset in ElementPresets.For(kind))
        {
            var captured = preset;
            var child = new MenuItem
            {
                Header = Row(kind, preset),
                Padding = new Thickness(6, 3)
            };
            child.Click += (_, _) => insert(captured);
            parent.Items.Add(child);
        }
        parent.PointerEntered += (_, _) =>
        {
            if (!parent.IsSubMenuOpen)
                parent.Open();
        };
        return parent;
    }

    public static Control Row(ElementKind kind, ElementPreset preset)
    {
        var element = ElementFactory.Create(kind, 0, 0, 0, 1);
        preset.Apply(element);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { Swatch(kind, element, preset.Sample), new TextBlock
            {
                Text = preset.Name,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Painting.Brush(Painting.Text),
                FontSize = 13
            } }
        };
        return row;
    }

    public static void ShowOnHover(Control anchor, ElementKind kind, Action<ElementPreset> insert)
    {
        var hover = new Hover(kind, insert);
        anchor.PointerEntered += (_, _) => hover.Queue(anchor);
        anchor.PointerExited += (_, _) => hover.Leave();
    }

    public static MenuItem IconMenu(Action<string> insert)
    {
        var parent = new MenuItem { Header = "Icons" };
        foreach (var category in BuiltInIcons.Categories())
        {
            var group = new MenuItem { Header = category };
            foreach (var icon in BuiltInIcons.In(category))
            {
                var captured = icon;
                var child = new MenuItem { Header = captured.Name };
                child.Click += (_, _) => insert(captured.Id);
                group.Items.Add(child);
            }
            parent.Items.Add(group);
        }
        return parent;
    }

    public static void ShowIcons(Control anchor, Action<string> insert)
    {
        var hover = new IconHover(insert);
        anchor.PointerEntered += (_, _) => hover.Queue(anchor);
        anchor.PointerExited += (_, _) => hover.Leave();
    }

    private static Control Swatch(ElementKind kind, SceneElement element, string sample)
    {
        var clearFill = string.IsNullOrWhiteSpace(element.Fill) || element.Fill == "#00000000";
        var clearStroke = element.StrokeThickness <= 0 || element.Stroke == "#00000000";
        var circle = kind == ElementKind.Ellipse || (kind == ElementKind.Image && Math.Abs(element.Width - element.Height) < 2 && element.CornerRadius >= element.Width / 2);
        var outline = !clearStroke || kind == ElementKind.Hotspot;
        var plate = new Border
        {
            Width = circle ? 46 : 108,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = Painting.Brush("#0E1822"),
            Child = new Border
            {
                Width = circle ? 28 : 96,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(circle ? 14 : Math.Clamp(element.CornerRadius, 0, 14)),
                Background = Painting.Brush(clearFill ? "#00000000" : element.Fill),
                BorderBrush = Painting.Brush(outline ? (clearStroke ? "#E7A23A" : element.Stroke) : "#00000000"),
                BorderThickness = new Thickness(outline ? 1.5 : 0),
                BoxShadow = element.HasShadow ? BoxShadows.Parse("0 4 10 0 #55000000") : default,
                Child = ShowsWords(kind) ? new TextBlock
                {
                    Text = Short(sample),
                    FontSize = kind == ElementKind.Text ? 12 : 11,
                    FontWeight = element.FontWeight == "Bold" ? FontWeight.Bold : FontWeight.Normal,
                    FontStyle = element.Italic ? FontStyle.Italic : FontStyle.Normal,
                    Foreground = Painting.Brush(element.Foreground),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(6, 0)
                } : null
            }
        };
        return plate;
    }

    private static bool ShowsWords(ElementKind kind) => kind is
        ElementKind.Text or ElementKind.Button or ElementKind.WebLink or ElementKind.Audio
        or ElementKind.Clock or ElementKind.DataList or ElementKind.Counter;

    private static string Short(string sample)
    {
        var line = sample.Split('\n')[0];
        return line.Length <= 14 ? line : line[..13] + "…";
    }

    private sealed class Hover
    {
        private static Hover? _open;
        private static readonly DispatcherTimer SwitchTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
        private static Action? _switchTo;
        private readonly Popup _popup;
        private readonly ScrollViewer _scroller;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(320) };
        private Control? _anchor;
        private object? _tip;

        public Hover(ElementKind kind, Action<ElementPreset> insert)
        {
            _dismissDesigns ??= () => _open?.Hide();
            var list = new StackPanel { Spacing = 2 };
            foreach (var preset in ElementPresets.For(kind))
            {
                var captured = preset;
                var button = new Button
                {
                    Content = Row(kind, preset),
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(6, 4),
                    BorderThickness = new Thickness(0)
                };
                button.PointerEntered += (_, _) => button.Background = Painting.Brush("#1B2330");
                button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
                button.Click += (_, _) =>
                {
                    _timer.Stop();
                    Close();
                    insert(captured);
                };
                list.Children.Add(button);
            }

            _scroller = new ScrollViewer
            {
                Content = list,
                MaxHeight = 480,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var host = new Border
            {
                Background = Painting.Brush("#121820"),
                BorderBrush = Painting.Brush("#2A3544"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                MinWidth = 220,
                Child = _scroller
            };
            host.PointerEntered += (_, _) =>
            {
                _switchTo = null;
                SwitchTimer.Stop();
                _timer.Stop();
            };
            host.PointerExited += (_, _) => _timer.Start();
            _popup = new Popup
            {
                Child = host,
                Placement = PlacementMode.AnchorAndGravity,
                PlacementAnchor = PopupAnchor.TopLeft,
                PlacementGravity = PopupGravity.BottomRight,
                PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideY,
                ShouldUseOverlayLayer = false,
                IsLightDismissEnabled = false
            };
            _timer.Tick += (_, _) => Close();
            if (SwitchTimer.Tag == null)
            {
                SwitchTimer.Tag = "ready";
                SwitchTimer.Tick += (_, _) =>
                {
                    SwitchTimer.Stop();
                    var show = _switchTo;
                    _switchTo = null;
                    show?.Invoke();
                };
            }
        }

        public void Queue(Control anchor)
        {
            _open?.Hold();
            if (_open == null || _open == this)
            {
                Show(anchor);
                return;
            }
            _switchTo = () => Show(anchor);
            SwitchTimer.Stop();
            SwitchTimer.Start();
        }

        public void Hold() => _timer.Stop();

        public void Show(Control anchor)
        {
            _dismissIcons?.Invoke();
            if (_open != null && _open != this)
                _open.Hide();
            _open = this;
            _timer.Stop();
            if (_anchor != anchor)
            {
                RestoreTip();
                _anchor = anchor;
                _tip = ToolTip.GetTip(anchor);
                ToolTip.SetTip(anchor, null);
            }
            var window = TopLevel.GetTopLevel(anchor);
            if (window == null)
                return;
            var corner = anchor.TranslatePoint(new Point(0, 0), window);
            if (corner == null)
                return;
            var edge = anchor.Parent is Visual panel
                ? panel.TranslatePoint(new Point(panel.Bounds.Width, 0), window)
                : null;
            var x = (edge?.X ?? corner.Value.X + anchor.Bounds.Width) + 8;
            _scroller.MaxHeight = Math.Min(440, Math.Max(220, window.Bounds.Height - 120));
            _popup.PlacementTarget = window;
            _popup.PlacementRect = new Rect(x, corner.Value.Y, 1, 1);
            if (!_popup.IsOpen)
                _popup.Open();
        }

        public void Leave()
        {
            _switchTo = null;
            SwitchTimer.Stop();
            _timer.Start();
        }

        private void Close()
        {
            _timer.Stop();
            _popup.IsOpen = false;
            RestoreTip();
            _anchor = null;
            if (_open == this)
                _open = null;
        }

        private void Hide()
        {
            _timer.Stop();
            _popup.IsOpen = false;
            RestoreTip();
            _anchor = null;
        }

        private void RestoreTip()
        {
            if (_anchor != null)
                ToolTip.SetTip(_anchor, _tip);
        }
    }

    private sealed class IconHover
    {
        private static IconHover? _open;
        private readonly Popup _popup;
        private readonly StackPanel _list;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(320) };
        private readonly Action<string> _insert;
        private readonly Button[] _groups;
        private Control? _anchor;
        private object? _tip;
        private string _category;

        public IconHover(Action<string> insert)
        {
            _insert = insert;
            _category = BuiltInIcons.Categories()[0];
            _dismissIcons ??= () => _open?.Hide();
            var categories = new WrapPanel { Orientation = Orientation.Horizontal };
            _groups = BuiltInIcons.Categories().Select(category =>
            {
                var captured = category;
                var button = new Button
                {
                    Content = captured,
                    Classes = { "tool" },
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 4)
                };
                button.Click += (_, _) =>
                {
                    _category = captured;
                    Fill();
                };
                categories.Children.Add(button);
                return button;
            }).ToArray();
            _list = new StackPanel { Spacing = 2 };
            var scroller = new ScrollViewer
            {
                Content = _list,
                MaxHeight = 420,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var host = new StackPanel { Spacing = 8 };
            host.Children.Add(categories);
            host.Children.Add(scroller);
            var frame = new Border
            {
                Background = Painting.Brush("#121820"),
                BorderBrush = Painting.Brush("#2A3544"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                MinWidth = 260,
                Child = host
            };
            frame.PointerEntered += (_, _) => _timer.Stop();
            frame.PointerExited += (_, _) => _timer.Start();
            _popup = new Popup
            {
                Child = frame,
                Placement = PlacementMode.AnchorAndGravity,
                PlacementAnchor = PopupAnchor.TopLeft,
                PlacementGravity = PopupGravity.BottomRight,
                PlacementConstraintAdjustment = PopupPositionerConstraintAdjustment.SlideY,
                ShouldUseOverlayLayer = false,
                IsLightDismissEnabled = false
            };
            _timer.Tick += (_, _) => Close();
            Fill();
        }

        public void Queue(Control anchor)
        {
            _open?.Hold();
            Show(anchor);
        }

        public void Leave() => _timer.Start();

        public void Hold() => _timer.Stop();

        private void Show(Control anchor)
        {
            _dismissDesigns?.Invoke();
            if (_open != null && _open != this)
                _open.Hide();
            _open = this;
            _timer.Stop();
            if (_anchor != anchor)
            {
                RestoreTip();
                _anchor = anchor;
                _tip = ToolTip.GetTip(anchor);
                ToolTip.SetTip(anchor, null);
            }
            var window = TopLevel.GetTopLevel(anchor);
            if (window == null)
                return;
            var corner = anchor.TranslatePoint(new Point(0, 0), window);
            if (corner == null)
                return;
            var edge = anchor.Parent is Visual panel
                ? panel.TranslatePoint(new Point(panel.Bounds.Width, 0), window)
                : null;
            var x = (edge?.X ?? corner.Value.X + anchor.Bounds.Width) + 8;
            _popup.PlacementTarget = window;
            _popup.PlacementRect = new Rect(x, corner.Value.Y, 1, 1);
            if (!_popup.IsOpen)
                _popup.Open();
        }

        private void Fill()
        {
            foreach (var button in _groups)
                button.Background = Painting.Brush((button.Content as string) == _category ? "#283448" : "#1B2330");
            _list.Children.Clear();
            var flag = _category == "Flags";
            foreach (var icon in BuiltInIcons.In(_category))
            {
                var captured = icon;
                var face = new IconFace
                {
                    Width = flag ? 46 : 28,
                    Height = flag ? 30 : 28,
                    IsHitTestVisible = false
                };
                face.Set(captured.Id, "#F4F7FB");
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        face,
                        new TextBlock
                        {
                            Text = captured.Name,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = Painting.Brush(Painting.Text),
                            FontSize = 13,
                            MaxWidth = 180,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    }
                };
                var button = new Button
                {
                    Content = row,
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(6, 4),
                    BorderThickness = new Thickness(0)
                };
                button.PointerEntered += (_, _) => button.Background = Painting.Brush("#1B2330");
                button.PointerExited += (_, _) => button.Background = Brushes.Transparent;
                button.Click += (_, _) =>
                {
                    _timer.Stop();
                    Close();
                    _insert(captured.Id);
                };
                _list.Children.Add(button);
            }
        }

        private void Close()
        {
            _timer.Stop();
            _popup.IsOpen = false;
            RestoreTip();
            _anchor = null;
            if (_open == this)
                _open = null;
        }

        private void Hide()
        {
            _timer.Stop();
            _popup.IsOpen = false;
            RestoreTip();
            _anchor = null;
        }

        private void RestoreTip()
        {
            if (_anchor != null)
                ToolTip.SetTip(_anchor, _tip);
        }
    }
}
