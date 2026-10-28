using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Avalonia.Threading;
using Intuime.Core;

namespace Intuime.Rendering;

public sealed class SceneSurface : UserControl, ICustomHitTest
{
    private readonly Border _page = new();
    private readonly Canvas _background = new() { IsHitTestVisible = false };
    private string _backgroundSignature = "";
    private readonly DesignGrid _grid = new() { IsHitTestVisible = false };
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly Canvas _marqueeLayer = new() { IsHitTestVisible = false };
    private readonly Border _scrollRing = new()
    {
        IsHitTestVisible = false,
        IsVisible = false,
        Background = Brushes.Transparent,
        BorderBrush = Painting.Brush("#E7A23A"),
        BorderThickness = new Thickness(6),
        CornerRadius = new CornerRadius(12)
    };
    private readonly GuideOverlay _guides = new() { IsHitTestVisible = false };
    private readonly Border _marquee = new()
    {
        BorderBrush = Painting.Brush("#E7A23A"),
        BorderThickness = new Thickness(1),
        Background = Painting.Brush("#22E7A23A"),
        IsVisible = false
    };
    private readonly Dictionary<string, Node> _nodes = new();
    private readonly Dictionary<string, Bitmap> _bitmaps = new();
    private readonly DispatcherTimer _clocks;
    private readonly DispatcherTimer _preview;
    private string? _previewId;
    private string _previewName = "";
    private bool _previewEntrance;
    private int _previewDelayMs;
    private int _previewDurationMs;
    private DateTime _previewStart;
    private EditorSession? _session;
    private PlaybackEngine? _engine;
    private string _assetRoot = "";
    private bool _design = true;
    private Gesture? _gesture;
    private bool _inline;
    private bool _clearPage;
    private double _originX;
    private double _originY;
    private double? _clipWidth;
    private double? _clipHeight;
    private Func<SceneElement, bool>? _include;
    private Func<BackgroundLayer, bool>? _backgroundInclude;
    private Func<SceneElement, bool>? _omit;
    private string? _backgroundElementId;
    private int? _screen;
    private bool _passThrough;

    public SceneSurface()
    {
        var root = new Grid();
        root.Children.Add(_page);
        root.Children.Add(_background);
        root.Children.Add(_grid);
        root.Children.Add(_canvas);
        root.Children.Add(_marqueeLayer);
        root.Children.Add(_guides);
        _marqueeLayer.Children.Add(_marquee);
        _marqueeLayer.Children.Add(_scrollRing);
        Content = root;
        ClipToBounds = true;
        _canvas.PointerPressed += OnCanvasPressed;
        _canvas.PointerMoved += OnCanvasMoved;
        _canvas.PointerReleased += OnCanvasReleased;
        _clocks = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clocks.Tick += (_, _) => RefreshTexts();
        _clocks.Start();
        _preview = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _preview.Tick += (_, _) => AdvancePreview();
    }

    public void PreviewTap(string elementId, string effectName)
    {
        StopPreview();
        if (!_design || !TapEffects.Known(effectName) || Lookup(elementId) == null)
            return;
        _previewId = elementId;
        _previewName = effectName;
        _previewEntrance = false;
        _previewStart = DateTime.UtcNow;
        _preview.Start();
    }

    public void StopMotionPreview() => StopPreview();

    public void PreviewEntrance(string elementId, string effectName, int delayMs, int durationMs)
    {
        StopPreview();
        var element = Lookup(elementId);
        if (!_design || element == null)
            return;
        if (!EntranceEffects.Known(effectName))
        {
            EntranceEffects.Reset(element);
            PaintEffect(element);
            return;
        }
        _previewId = elementId;
        _previewName = effectName;
        _previewEntrance = true;
        _previewDelayMs = Math.Max(0, delayMs);
        _previewDurationMs = Math.Max(150, durationMs);
        _previewStart = DateTime.UtcNow;
        EntranceEffects.Apply(element, effectName, 0);
        PaintEffect(element);
        _preview.Start();
    }

    private void AdvancePreview()
    {
        if (_previewId == null)
        {
            _preview.Stop();
            return;
        }
        var element = Lookup(_previewId);
        if (element == null)
        {
            StopPreview();
            return;
        }
        if (_previewEntrance)
        {
            var elapsed = (DateTime.UtcNow - _previewStart).TotalMilliseconds - _previewDelayMs;
            if (elapsed >= _previewDurationMs)
            {
                EntranceEffects.Reset(element);
                PaintEffect(element);
                _previewId = null;
                _preview.Stop();
                return;
            }
            EntranceEffects.Apply(element, _previewName, elapsed < 0 ? 0 : elapsed / _previewDurationMs);
            PaintEffect(element);
            return;
        }
        var t = (DateTime.UtcNow - _previewStart).TotalMilliseconds / Math.Max(1, TapEffects.DurationMs(_previewName));
        if (t >= 1)
        {
            TapEffects.Reset(element);
            PaintEffect(element);
            _previewId = null;
            _preview.Stop();
            return;
        }
        TapEffects.Apply(element, _previewName, t);
        PaintEffect(element);
    }

    private void StopPreview()
    {
        _preview.Stop();
        _previewEntrance = false;
        if (_previewId == null)
            return;
        var element = Lookup(_previewId);
        _previewId = null;
        if (element == null)
            return;
        TapEffects.Reset(element);
        PaintEffect(element);
    }

    private void PaintEffect(SceneElement element)
    {
        if (!_nodes.TryGetValue(element.Id, out var node))
            return;
        ApplyGeometry(node, element);
        ApplyChrome(node, element);
    }

    public bool IsGesturing => _gesture != null;
    public event Action<string>? OpenUrlRequested;

    public void UseDesign(EditorSession session)
    {
        _session = session;
        _engine = null;
        _design = true;
        _grid.IsVisible = session.ShowGrid;
        _assetRoot = session.AssetRoot;
    }

    public void UsePlayback(PlaybackEngine engine, string assetRoot)
    {
        _engine = engine;
        _session = null;
        _design = false;
        _grid.IsVisible = false;
        _assetRoot = assetRoot;
    }

    public void UseScreen(int screen) => _screen = screen;

    public void UseOverlay(double originX, double originY, double width, double height, Func<SceneElement, bool> include, string? backgroundElementId = null, Func<BackgroundLayer, bool>? backgrounds = null)
    {
        _originX = originX;
        _originY = originY;
        _clipWidth = Math.Max(1, width);
        _clipHeight = Math.Max(1, height);
        _include = include;
        _backgroundInclude = backgrounds;
        _backgroundElementId = backgroundElementId;
        _clearPage = true;
        Background = Brushes.Transparent;
    }

    public void MoveOverlay(double originX, double originY, double width, double height)
    {
        _originX = originX;
        _originY = originY;
        _clipWidth = Math.Max(1, width);
        _clipHeight = Math.Max(1, height);
        Width = _clipWidth.Value;
        Height = _clipHeight.Value;
        _canvas.Width = Width;
        _canvas.Height = Height;
    }

    public void SetOmit(Func<SceneElement, bool>? omit) => _omit = omit;

    public void PassClicksThrough()
    {
        _passThrough = true;
        Background = null;
        _page.IsHitTestVisible = false;
        _canvas.Background = null;
    }

    public bool HitTest(Point point)
    {
        if (!_passThrough)
            return true;
        var scene = Scene();
        if (scene == null)
            return false;
        foreach (var element in scene.Elements)
        {
            if (!Shown(element) || !element.IsVisible || !ElementFactory.CapturesPointer(element))
                continue;
            if (element.Opacity * element.EffectOpacity <= 0.04)
                continue;
            var rect = new Rect(
                element.X + element.EffectShiftX - _originX,
                element.Y + element.EffectShiftY - _originY,
                Math.Max(8, element.Width),
                Math.Max(8, element.Height));
            if (rect.Contains(point))
                return true;
        }
        return false;
    }

    public void SetAssetRoot(string assetRoot) => _assetRoot = assetRoot;

    public void SetGridVisible(bool visible) => _grid.IsVisible = visible && _design;

    public void Rebuild()
    {
        if (_inline || _gesture != null)
            return;
        var scene = Scene();
        _canvas.Children.Clear();
        _nodes.Clear();
        if (scene == null)
        {
            _page.Background = Painting.Brush("#101820");
            _background.Children.Clear();
            _backgroundSignature = "";
            PlaceScrollRing();
            return;
        }

        scene.PullLegacyBackground();
        var size = ClipSize();
        Width = size.Width;
        Height = size.Height;
        _canvas.Width = Width;
        _canvas.Height = Height;
        _page.Background = _clearPage ? Brushes.Transparent : Painting.Brush(scene.Background, "#101820");
        PlaceBackgrounds(scene);

        foreach (var element in scene.Elements.Select((value, index) => (value, index)).OrderBy(item => item.value.Z).ThenBy(item => item.index))
        {
            if (!Shown(element.value))
                continue;
            _canvas.Children.Add(CreateNode(element.value).Root);
        }

        PlaceScrollRing();
    }

    private void PlaceBackgrounds(Scene scene)
    {
        var size = ClipSize();
        var signature = _originX.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "," + _originY.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "|" + _clearPage + "|" + (_backgroundInclude != null) + "|" + _design;
        for (var index = 0; index < scene.Backgrounds.Count; index++)
        {
            var layer = scene.Backgrounds[index];
            var box = layer.Box(size.Width, size.Height);
            signature += $"|{index}:{layer.Id}:{layer.BuiltIn}:{layer.Source}:{layer.Video}:{layer.Link}:{layer.HasCustomSize}:{box.X}:{box.Y}:{box.Width}:{box.Height}";
        }
        if (signature == _backgroundSignature)
            return;
        _backgroundSignature = signature;
        _background.Children.Clear();
        for (var index = 0; index < scene.Backgrounds.Count; index++)
        {
            var layer = scene.Backgrounds[index];
            var included = _backgroundInclude != null ? _backgroundInclude(layer) : !_clearPage;
            var drawImage = !layer.Video && !layer.IsBuiltIn && !layer.Link && included;
            var drawMotion = layer.IsBuiltIn && included;
            var drawFrame = _design && _backgroundInclude == null && !_clearPage
                && (layer.Video || layer.Link || layer.HasCustomSize || scene.Backgrounds.Count > 1);
            if (!drawImage && !drawMotion && !drawFrame)
                continue;
            var box = layer.Box(size.Width, size.Height);
            if (drawMotion)
            {
                var motion = new BuiltInBackdrop(layer.BuiltIn)
                {
                    IsHitTestVisible = false,
                    Width = Math.Max(1, box.Width),
                    Height = Math.Max(1, box.Height)
                };
                Canvas.SetLeft(motion, box.X - _originX);
                Canvas.SetTop(motion, box.Y - _originY);
                _background.Children.Add(motion);
            }
            if (drawImage)
            {
                var image = new Image
                {
                    IsHitTestVisible = false,
                    Stretch = layer.HasCustomSize ? Stretch.Fill : Stretch.UniformToFill,
                    Source = LoadBitmap(layer.Source),
                    Width = Math.Max(1, box.Width),
                    Height = Math.Max(1, box.Height)
                };
                Canvas.SetLeft(image, box.X - _originX);
                Canvas.SetTop(image, box.Y - _originY);
                _background.Children.Add(image);
            }
            if (!drawFrame)
                continue;
            var file = layer.Link
                ? string.IsNullOrWhiteSpace(layer.Source) ? "Web page" : layer.Source
                : layer.IsBuiltIn
                    ? BuiltInBackgrounds.Find(layer.BuiltIn)?.Name ?? layer.Name
                    : string.IsNullOrWhiteSpace(layer.Source) ? layer.Name : System.IO.Path.GetFileName(layer.Source);
            var label = "Layer " + (index + 1) + (layer.Video || layer.Link ? "  ·  " + file + "  ·  press Play" : "  ·  " + file);
            var frame = new Border
            {
                IsHitTestVisible = false,
                Background = Brushes.Transparent,
                BorderBrush = Painting.Brush("#E7A23A"),
                BorderThickness = layer.HasCustomSize ? new Thickness(3) : new Thickness(0),
                Width = Math.Max(1, box.Width),
                Height = Math.Max(1, box.Height),
                Child = new Border
                {
                    Background = Painting.Brush("#E6101820"),
                    Padding = new Thickness(14, 8),
                    CornerRadius = new CornerRadius(10),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = layer.Link ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                    Margin = new Thickness(16),
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = label,
                        Foreground = Brushes.White,
                        FontSize = 18,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = Math.Max(160, box.Width - 60)
                    }
                }
            };
            Canvas.SetLeft(frame, box.X - _originX);
            Canvas.SetTop(frame, box.Y - _originY);
            _background.Children.Add(frame);
        }
    }

    private bool Shown(SceneElement element) =>
        (_include == null || _include(element)) && (_omit == null || !_omit(element));

    public void Sync()
    {
        if (_inline)
            return;
        var scene = Scene();
        if (scene == null)
        {
            PlaceScrollRing();
            return;
        }
        var shown = scene.Elements.Where(Shown).ToList();
        if (_nodes.Count != shown.Count || shown.Any(element => !_nodes.ContainsKey(element.Id)))
        {
            Rebuild();
            return;
        }

        if (!_clearPage)
            _page.Background = Painting.Brush(scene.Background, "#101820");
        PlaceBackgrounds(scene);
        foreach (var element in scene.Elements)
        {
            if (!_nodes.TryGetValue(element.Id, out var node))
                continue;
            ApplyGeometry(node, element);
            ApplyChrome(node, element);
            node.RefreshText?.Invoke();
            if (node.Picture != null && node.ImagePath != element.Source)
            {
                node.ImagePath = element.Source;
                node.Picture.Source = LoadBitmap(element.Source);
            }
            if (element.Kind == ElementKind.DataList && node.Signature != element.Text)
            {
                Rebuild();
                return;
            }
        }

        PlaceScrollRing();
    }

    private void PlaceScrollRing()
    {
        var id = _design ? null : _engine?.ScrollTargetId;
        var element = id == null ? null : Lookup(id);
        if (element == null || !element.IsVisible || !Shown(element))
        {
            _scrollRing.IsVisible = false;
            return;
        }

        const double pad = 8;
        var width = Math.Max(8, element.Width);
        var height = Math.Max(8, element.Height);
        _scrollRing.Width = width + pad * 2;
        _scrollRing.Height = height + pad * 2;
        _scrollRing.CornerRadius = new CornerRadius(Math.Max(12, element.CornerRadius + 4));
        Canvas.SetLeft(_scrollRing, element.X - _originX - pad);
        Canvas.SetTop(_scrollRing, element.Y - _originY - pad);
        var scaleX = Math.Max(0.01, element.EffectScale * element.EffectScaleX);
        var scaleY = Math.Max(0.01, element.EffectScale * element.EffectScaleY);
        var motion = new TransformGroup();
        motion.Children.Add(new ScaleTransform(scaleX, scaleY));
        motion.Children.Add(new RotateTransform(element.Rotation + element.EffectRotation));
        motion.Children.Add(new TranslateTransform(element.EffectShiftX, element.EffectShiftY));
        _scrollRing.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _scrollRing.RenderTransform = motion;
        _scrollRing.IsVisible = true;
    }

    private void RefreshTexts()
    {
        if (_gesture != null || _inline)
            return;
        foreach (var node in _nodes.Values)
            node.RefreshText?.Invoke();
    }

    private Node CreateNode(SceneElement element)
    {
        var root = new Grid
        {
            Width = Math.Max(8, element.Width),
            Height = Math.Max(8, element.Height),
            Background = Brushes.Transparent,
            Tag = element.Id
        };
        var node = new Node { Root = root };
        _nodes[element.Id] = node;
        ApplyGeometry(node, element);
        root.Children.Add(CreateBody(node, element));
        if (_design && _session?.Selection.Any(item => item.Id == element.Id) == true)
            AddAdorner(root, element);
        root.ContextMenu = _design ? MenuFor(element) : null;
        root.DoubleTapped += (_, e) =>
        {
            if (_design && element.Kind is ElementKind.Text or ElementKind.Button)
            {
                BeginInlineEdit(root, element);
                e.Handled = true;
            }
        };
        root.PointerPressed += (_, e) => OnElementPressed(element, e);
        root.PointerReleased += (_, e) => OnElementReleased(element, e);
        return node;
    }

    private Control CreateBody(Node node, SceneElement element)
    {
        if (element.Kind == ElementKind.Ellipse)
        {
            var shape = new Ellipse();
            node.ChromeShape = shape;
            node.RefreshText = () => ApplyShape(shape, Lookup(element.Id) ?? element);
            ApplyShape(shape, element);
            return shape;
        }

        var frame = new Border { ClipToBounds = true };
        node.Chrome = frame;
        var inner = CreateInner(node, frame, element);
        frame.Child = inner;
        ApplyChrome(node, element);
        return frame;
    }

    private Control CreateInner(Node node, Border frame, SceneElement element)
    {
        switch (element.Kind)
        {
            case ElementKind.Image:
                var image = new Image { Stretch = Painting.StretchOf(element.Stretch) };
                node.Picture = image;
                node.ImagePath = element.Source;
                image.Source = LoadBitmap(element.Source);
                var stack = new Grid();
                stack.Children.Add(image);
                if (image.Source == null)
                    stack.Children.Add(MakeText(node, element, "Drop an image or use Browse"));
                return stack;
            case ElementKind.DataList:
                node.Signature = element.Text;
                return DataListBody(element);
            case ElementKind.Counter:
                return CounterBody(node, element);
            case ElementKind.Icon:
                var face = new IconFace
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                };
                node.RefreshText = () =>
                {
                    var live = Lookup(element.Id) ?? element;
                    face.Set(live.Icon, live.Foreground);
                };
                node.RefreshText();
                return face;
            case ElementKind.Video:
            case ElementKind.Audio:
                var media = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 8) };
                var primary = new TextBlock { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
                var secondary = new TextBlock { Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
                node.PrimaryText = primary;
                node.SecondaryText = secondary;
                node.RefreshText = () =>
                {
                    var live = Lookup(element.Id) ?? element;
                    primary.Text = live.Kind == ElementKind.Video ? "Video" : (string.IsNullOrWhiteSpace(live.Text) ? "Audio" : live.Text);
                    var file = string.IsNullOrWhiteSpace(live.Source) ? "No file yet" : System.IO.Path.GetFileName(live.Source);
                    if (live.Loop)
                        file += "  ·  loops";
                    secondary.Text = live.IsPlaying ? file + "  ·  playing" : file;
                    StyleText(primary, live);
                    secondary.Foreground = Painting.Brush(live.Foreground);
                    secondary.FontSize = Math.Max(12, live.FontSize * 0.55);
                };
                node.RefreshText();
                media.Children.Add(primary);
                media.Children.Add(secondary);
                return media;
            default:
                return MakeText(node, element, null);
        }
    }

    private Control MakeText(Node node, SceneElement element, string? placeholder)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        node.PrimaryText = block;
        node.RefreshText = () =>
        {
            var live = Lookup(element.Id) ?? element;
            var document = Document();
            block.Text = _design && live.Kind == ElementKind.Hotspot
                ? live.Name
                : document == null
                    ? live.Text
                    : TextTemplates.Display(live, document);
            if (string.IsNullOrWhiteSpace(block.Text) && placeholder != null)
                block.Text = placeholder;
            StyleText(block, live);
        };
        node.RefreshText();
        return new Grid { Children = { block } };
    }

    private Control DataListBody(SceneElement element)
    {
        var stack = new StackPanel { Margin = new Thickness(8) };
        var items = element.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < items.Length; index++)
        {
            var captured = index;
            var row = new Button
            {
                Content = items[index],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4),
                Padding = new Thickness(12, 8),
                Background = Painting.Brush("#1C2836"),
                Foreground = Painting.Brush(element.Foreground),
                FontSize = element.FontSize
            };
            row.Click += (_, _) =>
            {
                if (_design)
                    _session?.SelectOnly(Lookup(element.Id) ?? element);
                else
                    _engine?.SelectDataItem(element.Id, captured);
            };
            stack.Children.Add(row);
        }
        return stack;
    }

    private Control CounterBody(Node node, SceneElement element)
    {
        var grid = new Grid { Margin = new Thickness(8, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var minus = CounterButton("−");
        var plus = CounterButton("+");
        var value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        node.PrimaryText = value;
        node.RefreshText = () =>
        {
            var live = Lookup(element.Id) ?? element;
            var document = Document();
            value.Text = document == null ? "0" : TextTemplates.Display(live, document);
            StyleText(value, live);
            value.TextAlignment = TextAlignment.Center;
        };
        node.RefreshText();
        minus.Click += (_, _) => Nudge(element.Id, -1);
        plus.Click += (_, _) => Nudge(element.Id, 1);
        Grid.SetColumn(value, 1);
        Grid.SetColumn(plus, 2);
        grid.Children.Add(minus);
        grid.Children.Add(value);
        grid.Children.Add(plus);
        return grid;
    }

    private static Button CounterButton(string label) => new()
    {
        Content = label,
        Width = 64,
        FontSize = 28,
        Background = Painting.Brush("#0E1822"),
        Foreground = Painting.Brush("#F4F7FB"),
        VerticalAlignment = VerticalAlignment.Stretch,
        CornerRadius = new CornerRadius(10)
    };

    private void Nudge(string id, int direction)
    {
        if (_design)
        {
            var element = Lookup(id);
            if (element == null || _session == null)
                return;
            var name = TextTemplates.BindingName(element);
            _ = double.TryParse(TextTemplates.Get(_session.Document, name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var current);
            var step = element.Step == 0 ? 1 : element.Step;
            var next = current + direction * step;
            if (element.HasRange)
                next = Math.Clamp(next, Math.Min(element.Min, element.Max), Math.Max(element.Min, element.Max));
            TextTemplates.Set(_session.Document, name, TextTemplates.FormatNumber(next));
            _session.Commit();
            return;
        }
        _engine?.NudgeCounter(id, direction);
    }

    private void AddAdorner(Grid root, SceneElement element)
    {
        root.Children.Add(new Border
        {
            BorderBrush = Painting.Brush("#E7A23A"),
            BorderThickness = new Thickness(1.5),
            IsHitTestVisible = false
        });
        if (element.IsLocked)
            return;
        AddHandle(root, element, "nw", HorizontalAlignment.Left, VerticalAlignment.Top);
        AddHandle(root, element, "n", HorizontalAlignment.Center, VerticalAlignment.Top);
        AddHandle(root, element, "ne", HorizontalAlignment.Right, VerticalAlignment.Top);
        AddHandle(root, element, "e", HorizontalAlignment.Right, VerticalAlignment.Center);
        AddHandle(root, element, "se", HorizontalAlignment.Right, VerticalAlignment.Bottom);
        AddHandle(root, element, "s", HorizontalAlignment.Center, VerticalAlignment.Bottom);
        AddHandle(root, element, "sw", HorizontalAlignment.Left, VerticalAlignment.Bottom);
        AddHandle(root, element, "w", HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    private void AddHandle(Grid root, SceneElement element, string code, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var handle = new Border
        {
            Width = 12,
            Height = 12,
            Background = Brushes.White,
            BorderBrush = Painting.Brush("#1A1204"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = new Thickness(
                horizontal == HorizontalAlignment.Left ? -6 : 0,
                vertical == VerticalAlignment.Top ? -6 : 0,
                horizontal == HorizontalAlignment.Right ? -6 : 0,
                vertical == VerticalAlignment.Bottom ? -6 : 0),
            Cursor = CursorFor(code)
        };
        handle.PointerPressed += (_, e) =>
        {
            if (_session == null || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
                return;
            _session.SelectOnly(Lookup(element.Id) ?? element);
            _gesture = Gesture.Resize(e.GetPosition(_canvas), Lookup(element.Id) ?? element, code);
            e.Pointer.Capture(_canvas);
            e.Handled = true;
        };
        root.Children.Add(handle);
    }

    private ContextMenu MenuFor(SceneElement element)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Duplicate", () => _session?.DuplicateSelection()));
        menu.Items.Add(Item("Delete", () => _session?.DeleteSelection()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Bring to front", () => _session?.BringToFront()));
        menu.Items.Add(Item("Send to back", () => _session?.SendToBack()));
        menu.Items.Add(Item(element.IsLocked ? "Unlock" : "Lock", () =>
        {
            var live = Lookup(element.Id);
            if (live == null || _session == null)
                return;
            live.IsLocked = !live.IsLocked;
            _session.Commit(structure: true);
        }));
        return menu;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnElementPressed(SceneElement element, PointerPressedEventArgs e)
    {
        var live = Lookup(element.Id) ?? element;
        if (!_design)
        {
            if (live.IsVisible)
                _engine?.PointerDown(live.Id);
            e.Handled = true;
            return;
        }
        if (_session == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _session?.SelectOnly(live);
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            _session.ToggleSelect(live);
        else if (_session.Selection.All(item => item.Id != live.Id))
            _session.SelectOnly(live);

        if (!live.IsLocked)
        {
            _gesture = Gesture.Move(e.GetPosition(_canvas), _session.Selection.Where(item => !item.IsLocked));
            e.Pointer.Capture(_canvas);
        }
        e.Handled = true;
    }

    private void OnElementReleased(SceneElement element, PointerReleasedEventArgs e)
    {
        if (_design || _engine == null)
            return;
        var live = Lookup(element.Id) ?? element;
        if (!live.IsVisible)
            return;
        _engine.PointerUp(live.Id);
        if (live.Kind == ElementKind.WebLink &&
            !string.IsNullOrWhiteSpace(live.Url) &&
            live.Interactions.All(interaction => interaction.Trigger != TriggerKind.Tap))
            OpenUrlRequested?.Invoke(live.Url);
        e.Handled = true;
    }

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_passThrough || e.Source != _canvas)
            return;
        if (!_design)
        {
            if (!string.IsNullOrEmpty(_backgroundElementId))
                _engine?.PointerDown(_backgroundElementId);
            _gesture = Gesture.Background(e.GetPosition(_canvas));
            e.Pointer.Capture(_canvas);
            return;
        }
        if (_session == null || !e.GetCurrentPoint(_canvas).Properties.IsLeftButtonPressed)
            return;
        _session.SelectOnly(null);
        _gesture = Gesture.Marquee(e.GetPosition(_canvas));
        _marquee.IsVisible = true;
        e.Pointer.Capture(_canvas);
        e.Handled = true;
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (_gesture == null || _session == null && _design)
            return;
        var point = e.GetPosition(_canvas);
        if (_gesture.Kind == GestureKind.Move)
        {
            var dx = point.X - _gesture.Start.X;
            var dy = point.Y - _gesture.Start.Y;
            MoveSelection(dx, dy);
        }
        else if (_gesture.Kind == GestureKind.Resize && _gesture.PrimaryId != null)
        {
            var element = Lookup(_gesture.PrimaryId);
            if (element != null && _gesture.Origins.TryGetValue(element.Id, out var origin))
            {
                Resize(element, origin, point, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                if (_nodes.TryGetValue(element.Id, out var node))
                    ApplyGeometry(node, element);
            }
        }
        else if (_gesture.Kind == GestureKind.Marquee)
        {
            var rect = RectOf(_gesture.Start, point);
            Canvas.SetLeft(_marquee, rect.X);
            Canvas.SetTop(_marquee, rect.Y);
            _marquee.Width = rect.Width;
            _marquee.Height = rect.Height;
        }
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_gesture == null)
            return;
        var gesture = _gesture;
        _gesture = null;
        e.Pointer.Capture(null);
        if (!_design)
        {
            if (gesture.Kind == GestureKind.Background)
            {
                if (!string.IsNullOrEmpty(_backgroundElementId))
                    _engine?.PointerUp(_backgroundElementId);
                else
                    _engine?.BackgroundTap(Scene()?.Id);
            }
            return;
        }

        _marquee.IsVisible = false;
        ClearGuides();
        if (gesture.Kind == GestureKind.Move)
        {
            var end = e.GetPosition(_canvas);
            var moved = Math.Abs(end.X - gesture.Start.X) + Math.Abs(end.Y - gesture.Start.Y);
            if (moved < 6 && gesture.Origins.Count == 1)
            {
                var tapped = Lookup(gesture.Origins.Keys.First());
                if (tapped != null && TapEffects.Known(tapped.TapEffect))
                {
                    _session?.Commit();
                    PreviewTap(tapped.Id, tapped.TapEffect);
                    return;
                }
            }
        }
        if (gesture.Kind == GestureKind.Marquee && _session != null)
        {
            var rect = RectOf(gesture.Start, e.GetPosition(_canvas));
            if (rect.Width < 4 && rect.Height < 4)
                return;
            var hits = Scene()?.Elements.Where(element =>
                LayoutMath.Intersects(element, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height)).ToList()
                ?? new List<SceneElement>();
            _session.SetSelection(hits);
            return;
        }
        _session?.Commit();
    }

    private void MoveSelection(double dx, double dy)
    {
        if (_gesture == null)
            return;
        var proposed = new List<(string Id, double X, double Y, double Width, double Height)>();
        foreach (var origin in _gesture.Origins)
            proposed.Add((origin.Key, origin.Value.X + dx, origin.Value.Y + dy, origin.Value.Width, origin.Value.Height));
        if (proposed.Count == 0)
        {
            ClearGuides();
            return;
        }

        var minX = proposed.Min(item => item.X);
        var minY = proposed.Min(item => item.Y);
        var moving = new GuideBox(
            minX,
            minY,
            proposed.Max(item => item.X + item.Width) - minX,
            proposed.Max(item => item.Y + item.Height) - minY);
        var movingIds = _gesture.Origins.Keys.ToHashSet();
        var others = Scene()?.Elements
            .Where(element => element.IsVisible && !movingIds.Contains(element.Id))
            .Select(element => new GuideBox(element.X, element.Y, element.Width, element.Height))
            .ToList() ?? new List<GuideBox>();
        var size = DocumentSize();
        var zoom = Math.Max(0.2, _session?.Zoom ?? 1);
        var placed = AlignGuides.Snap(
            moving,
            size.Width,
            size.Height,
            others,
            Math.Max(6, 8 / zoom),
            _session?.SnapToGrid == true,
            _session?.GridSize ?? 8);
        var shiftX = placed.X - minX;
        var shiftY = placed.Y - minY;
        foreach (var item in proposed)
        {
            var element = Lookup(item.Id);
            if (element == null)
                continue;
            element.X = item.X + shiftX;
            element.Y = item.Y + shiftY;
            if (_nodes.TryGetValue(element.Id, out var node))
                ApplyGeometry(node, element);
        }
        ShowGuides(placed.Lines);
    }

    private void ShowGuides(IReadOnlyList<AlignGuide> lines)
    {
        if (!_design || lines.Count == 0)
        {
            ClearGuides();
            return;
        }
        var zoom = Math.Max(0.2, _session?.Zoom ?? 1);
        _guides.OriginX = _originX;
        _guides.OriginY = _originY;
        _guides.Thickness = Math.Clamp(2 / zoom, 1.25, 4);
        _guides.Lines = lines;
        _guides.InvalidateVisual();
    }

    private void ClearGuides()
    {
        if (_guides.Lines.Count == 0)
            return;
        _guides.Lines = Array.Empty<AlignGuide>();
        _guides.InvalidateVisual();
    }

    private void Resize(SceneElement element, LayoutBox origin, Point point, bool keepAspect)
    {
        var dx = point.X - _gesture!.Start.X;
        var dy = point.Y - _gesture.Start.Y;
        var x = origin.X;
        var y = origin.Y;
        var width = origin.Width;
        var height = origin.Height;
        var handle = _gesture.Handle;
        if (handle.Contains('e')) width = Math.Max(20, Snap(origin.Width + dx));
        if (handle.Contains('s')) height = Math.Max(20, Snap(origin.Height + dy));
        if (handle.Contains('w'))
        {
            width = Math.Max(20, Snap(origin.Width - dx));
            x = origin.X + origin.Width - width;
        }
        if (handle.Contains('n'))
        {
            height = Math.Max(20, Snap(origin.Height - dy));
            y = origin.Y + origin.Height - height;
        }
        if (keepAspect && origin.Height > 0 && element.Kind is ElementKind.Image or ElementKind.Video)
        {
            var ratio = origin.Width / origin.Height;
            if (handle is "e" or "w")
                height = Math.Max(20, width / ratio);
            else
                height = Math.Max(20, width / ratio);
            if (handle.Contains('n'))
                y = origin.Y + origin.Height - height;
        }
        element.X = x;
        element.Y = y;
        element.Width = width;
        element.Height = height;
    }

    private void BeginInlineEdit(Grid root, SceneElement element)
    {
        var live = Lookup(element.Id) ?? element;
        _inline = true;
        var box = new TextBox
        {
            Text = live.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = live.FontSize,
            Background = Painting.Brush("#0E1822"),
            Foreground = Painting.Brush("#F4F7FB")
        };
        root.Children.Add(box);
        var committed = false;
        void CommitEdit(bool save)
        {
            if (committed)
                return;
            committed = true;
            if (save)
                live.Text = box.Text ?? "";
            _inline = false;
            if (save)
                _session?.Commit();
            else
                Rebuild();
        }
        box.LostFocus += (_, _) => CommitEdit(true);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CommitEdit(false);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                CommitEdit(true);
                e.Handled = true;
            }
        };
        Dispatcher.UIThread.Post(() => box.Focus());
    }

    private void ApplyGeometry(Node node, SceneElement element)
    {
        Canvas.SetLeft(node.Root, element.X - _originX);
        Canvas.SetTop(node.Root, element.Y - _originY);
        node.Root.Width = Math.Max(8, element.Width);
        node.Root.Height = Math.Max(8, element.Height);
        var scaleX = Math.Max(0.01, element.EffectScale * element.EffectScaleX);
        var scaleY = Math.Max(0.01, element.EffectScale * element.EffectScaleY);
        var motion = new TransformGroup();
        motion.Children.Add(new ScaleTransform(scaleX, scaleY));
        motion.Children.Add(new RotateTransform(element.Rotation + element.EffectRotation));
        motion.Children.Add(new TranslateTransform(element.EffectShiftX, element.EffectShiftY));
        node.Root.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        node.Root.RenderTransform = motion;
        var hidden = !element.IsVisible;
        var opacity = Math.Clamp(element.Opacity * element.EffectOpacity, 0, 1);
        node.Root.Opacity = hidden ? (_design ? Math.Min(element.Opacity, 0.35) : 0) : opacity;
        node.Root.IsHitTestVisible = _design || (element.IsVisible && opacity > 0.04 && ElementFactory.CapturesPointer(element));
        if (!_design && element.IsVisible && ElementFactory.CapturesPointer(element))
            node.Root.Cursor = new Cursor(StandardCursorType.Hand);
    }

    private void ApplyChrome(Node node, SceneElement element)
    {
        if (node.ChromeShape != null)
        {
            ApplyShape(node.ChromeShape, element);
            return;
        }
        if (node.Chrome == null)
            return;
        var fill = element.EffectFill ?? element.Fill;
        if (_design && element.Kind == ElementKind.Hotspot)
            fill = "#33E7A23A";
        node.Chrome.Background = Painting.Brush(fill, "#00000000");
        node.Chrome.CornerRadius = new CornerRadius(element.Kind == ElementKind.Hotspot && _design ? 4 : element.CornerRadius);
        node.Chrome.BorderBrush = Painting.Brush(_design && element.Kind == ElementKind.Hotspot ? "#E7A23A" : element.Stroke, "#00000000");
        node.Chrome.BorderThickness = new Thickness(_design && element.Kind == ElementKind.Hotspot ? 1.5 : element.StrokeThickness);
        try
        {
            node.Chrome.BoxShadow = element.HasShadow && element.IsVisible
                ? BoxShadows.Parse("0 10 24 0 #55000000")
                : default;
        }
        catch (FormatException)
        {
            node.Chrome.BoxShadow = default;
        }
        if (node.Picture != null)
            node.Picture.Stretch = Painting.StretchOf(element.Stretch);
    }

    private static void ApplyShape(Ellipse shape, SceneElement element)
    {
        shape.Fill = Painting.Brush(element.EffectFill ?? element.Fill, "#E7A23A");
        shape.Stroke = Painting.Brush(element.Stroke, "#00000000");
        shape.StrokeThickness = element.StrokeThickness;
        shape.Opacity = 1;
    }

    private static void StyleText(TextBlock block, SceneElement element)
    {
        block.Foreground = Painting.Brush(element.Foreground);
        block.FontSize = Math.Max(8, element.FontSize);
        block.FontWeight = Painting.Weight(element.FontWeight);
        block.FontStyle = element.Italic ? FontStyle.Italic : FontStyle.Normal;
        block.TextAlignment = element.Kind is ElementKind.Button or ElementKind.WebLink or ElementKind.Counter
            ? TextAlignment.Center
            : Painting.Align(element.TextAlign);
        block.VerticalAlignment = element.Kind is ElementKind.Button or ElementKind.WebLink or ElementKind.Counter
            ? VerticalAlignment.Center
            : Painting.VAlign(element.VerticalAlign);
        block.HorizontalAlignment = HorizontalAlignment.Stretch;
        block.Margin = element.Kind is ElementKind.Button or ElementKind.Text or ElementKind.Hotspot
            ? new Thickness(12, 4)
            : new Thickness(0);
    }

    private Bitmap? LoadBitmap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;
        var path = ProjectStore.Resolve(_assetRoot, source);
        if (_bitmaps.TryGetValue(path, out var cached))
            return cached;
        if (!File.Exists(path))
            return null;
        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);
            _bitmaps[path] = bitmap;
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Scene? Scene() => _design ? _session?.ActiveScene : _screen is int screen ? _engine?.SceneFor(screen) : _engine?.Current;
    private ExperienceDocument? Document() => _design ? _session?.Document : _engine?.Live;

    private (double Width, double Height) ClipSize()
    {
        if (_clipWidth is double width && _clipHeight is double height)
            return (width, height);
        return DocumentSize();
    }

    private (double Width, double Height) DocumentSize()
    {
        var document = Document();
        return document == null ? (1920, 1080) : (document.Width, document.Height);
    }

    private SceneElement? Lookup(string id) => Scene()?.Elements.FirstOrDefault(element => element.Id == id);

    private double Snap(double value) =>
        _session == null ? value : LayoutMath.Snap(value, _session.SnapToGrid, _session.GridSize);

    private static Rect RectOf(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        return new Rect(x, y, Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }

    private static Cursor CursorFor(string handle) => handle switch
    {
        "n" or "s" => new Cursor(StandardCursorType.SizeNorthSouth),
        "e" or "w" => new Cursor(StandardCursorType.SizeWestEast),
        "nw" or "se" => new Cursor(StandardCursorType.TopLeftCorner),
        _ => new Cursor(StandardCursorType.TopRightCorner)
    };

    private sealed class Node
    {
        public Grid Root { get; set; } = null!;
        public Border? Chrome { get; set; }
        public Ellipse? ChromeShape { get; set; }
        public TextBlock? PrimaryText { get; set; }
        public TextBlock? SecondaryText { get; set; }
        public Image? Picture { get; set; }
        public string ImagePath { get; set; } = "";
        public string Signature { get; set; } = "";
        public Action? RefreshText { get; set; }
    }

    private enum GestureKind { Move, Resize, Marquee, Background }

    private sealed class Gesture
    {
        public GestureKind Kind { get; private init; }
        public Point Start { get; private init; }
        public string Handle { get; private init; } = "";
        public string? PrimaryId { get; private init; }
        public Dictionary<string, LayoutBox> Origins { get; } = new();

        public static Gesture Move(Point start, IEnumerable<SceneElement> elements)
        {
            var gesture = new Gesture { Kind = GestureKind.Move, Start = start };
            foreach (var element in elements)
                gesture.Origins[element.Id] = new LayoutBox(element.X, element.Y, element.Width, element.Height);
            return gesture;
        }

        public static Gesture Resize(Point start, SceneElement element, string handle)
        {
            var gesture = new Gesture { Kind = GestureKind.Resize, Start = start, Handle = handle, PrimaryId = element.Id };
            gesture.Origins[element.Id] = new LayoutBox(element.X, element.Y, element.Width, element.Height);
            return gesture;
        }

        public static Gesture Marquee(Point start) => new() { Kind = GestureKind.Marquee, Start = start };
        public static Gesture Background(Point start) => new() { Kind = GestureKind.Background, Start = start };
    }

    private readonly record struct LayoutBox(double X, double Y, double Width, double Height);
}

public sealed class GuideOverlay : Control
{
    public IReadOnlyList<AlignGuide> Lines { get; set; } = Array.Empty<AlignGuide>();
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double Thickness { get; set; } = 2;

    public override void Render(DrawingContext context)
    {
        if (Lines.Count == 0)
            return;
        var pen = new Pen(Painting.Brush("#FF4FA3"), Thickness);
        foreach (var guide in Lines)
            context.DrawLine(
                pen,
                new Point(guide.X1 - OriginX, guide.Y1 - OriginY),
                new Point(guide.X2 - OriginX, guide.Y2 - OriginY));
    }
}

public sealed class DesignGrid : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var pen = new Pen(Painting.Brush("#182230"), 1);
        for (double x = 0; x <= Bounds.Width; x += 40)
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        for (double y = 0; y <= Bounds.Height; y += 40)
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
    }
}
