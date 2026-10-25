using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Intuime.Core;
using Intuime.Rendering;

namespace Intuime.Views;

public partial class ComposerWindow : Window
{
    private readonly EditorSession _session = new();
    private readonly SceneSurface _surface = new();
    private readonly InspectorPanel _inspector;
    private readonly LayoutTransformControl _zoomHost = new();
    private readonly ScaleTransform _scale = new(0.55, 0.55);
    private readonly Border _frame = new() { BoxShadow = default };
    private List<SceneElement> _clipboard = new();
    private readonly List<PlayerWindow> _players = new();
    private bool _loadingLists;
    private bool _refreshingStage;
    private bool _allowClose;
    private bool _userZoom;
    private Size _fitSize;
    private string _tab = "properties";

    public ComposerWindow()
    {
        InitializeComponent();
        _inspector = new InspectorPanel(BrowseAsset);
        InspectorHost.Content = _inspector;
        _inspector.PinnedEffectChanged += () =>
        {
            EffectHost.Content = _inspector.PinnedEffect;
            EffectHost.IsVisible = _inspector.PinnedEffect != null;
        };
        _inspector.PreviewEffect += (id, name) => _surface.PreviewTap(id, name);
        _inspector.PreviewEntrance += (id, name, delay, duration) => _surface.PreviewEntrance(id, name, delay, duration);
        _zoomHost.LayoutTransform = _scale;
        _frame.Child = _surface;
        _zoomHost.Child = _frame;
        StageHost.Children.Add(_zoomHost);
        try
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Intuime/Assets/intuime.png")));
        }
        catch (Exception)
        {
        }

        BuildMenus();
        BuildToolbar();
        BuildInsert();
        _session.DocumentChanged += () => RefreshStage(rebuildInspector: !IsInspectorEditing());
        _session.StructureChanged += QueueReloadLists;
        _session.SelectionChanged += () =>
        {
            _surface.StopMotionPreview();
            HighlightLists();
            _inspector.Refresh();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!_surface.IsGesturing)
                    _surface.Rebuild();
            });
        };
        SceneList.SelectionChanged += OnScenePicked;
        LayerList.SelectionChanged += OnLayerPicked;
        Viewport.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(Viewport, true);
        Viewport.AddHandler(DragDrop.DropEvent, OnDrop);
        Viewport.SizeChanged += (_, _) =>
        {
            RefitIfAutomatic();
            CenterStage();
        };

        var root = AssetFiles.PrepareSampleRoot();
        _session.Load(SampleExperiences.CreateLumenGallery(), null, root, markClean: true);
        _surface.UseDesign(_session);
        ReloadLists();
        RefreshStage(rebuildInspector: true);
        RefitIfAutomatic();
    }

    private void BuildMenus()
    {
        Menu("File",
            ("New    Ctrl+N", () => _ = NewExperience()),
            ("Open    Ctrl+O", () => _ = Open()),
            ("Save    Ctrl+S", () => _ = Save()),
            ("Save as    Ctrl+Shift+S", () => _ = SaveAs()),
            ("Open the gallery sample", OpenSample),
            ("Exit", Close));
        Menu("Edit",
            ("Undo    Ctrl+Z", () => _session.Undo()),
            ("Redo    Ctrl+Y", () => _session.Redo()),
            ("Duplicate    Ctrl+D", () => _session.DuplicateSelection()),
            ("Delete", () => _session.DeleteSelection()),
            ("Select all    Ctrl+A", () => _session.SelectAll()));
        BuildInsertMenu();
        Menu("Arrange",
            ("Align left", () => _session.Align("left")),
            ("Align center", () => _session.Align("center")),
            ("Align right", () => _session.Align("right")),
            ("Align top", () => _session.Align("top")),
            ("Align middle", () => _session.Align("middle")),
            ("Align bottom", () => _session.Align("bottom")),
            ("Distribute horizontally", () => _session.Distribute(true)),
            ("Distribute vertically", () => _session.Distribute(false)),
            ("Bring to front", () => _session.BringToFront()),
            ("Send to back", () => _session.SendToBack()));
        Menu("Play",
            ("Preview    F5", () => Play(false)),
            ("Full screen    Shift+F5", () => Play(true)));
        Menu("Help",
            ("Shortcuts", () => _ = Shortcuts()),
            ("About Intuime", () => _ = Dialogs.Alert(this, "Intuime",
                "Intuime composes touchscreen experiences. Design scenes, wire taps and timers, then play them full screen. It follows the same idea as Intuiface: a visual composer plus a player.")));
    }

    private void BuildInsertMenu()
    {
        var button = new Button { Content = "Insert", Classes = { "tool" } };
        var flyout = new MenuFlyout();
        foreach (var item in ElementFactory.Catalog)
        {
            var kind = item.Kind;
            flyout.Items.Add(InsertDesigns.KindMenu(kind, item.Label, preset => _session.AddElement(kind, preset)));
        }
        flyout.Items.Add(InsertDesigns.IconMenu(id => _session.AddIcon(id)));
        button.Flyout = flyout;
        MenuHost.Children.Add(button);
    }

    private void Menu(string header, params (string Label, Action Action)[] items)
    {
        var button = new Button { Content = header, Classes = { "tool" } };
        var flyout = new MenuFlyout();
        foreach (var item in items)
        {
            var menuItem = new MenuItem { Header = item.Label };
            menuItem.Click += (_, _) => item.Action();
            flyout.Items.Add(menuItem);
        }
        button.Flyout = flyout;
        MenuHost.Children.Add(button);
    }

    private void BuildToolbar()
    {
        Tool("Undo", () => _session.Undo());
        Tool("Redo", () => _session.Redo());
        Tool("Snap", () =>
        {
            _session.SnapToGrid = !_session.SnapToGrid;
            SetStatus();
        });
        Tool("Grid", () =>
        {
            _session.ShowGrid = !_session.ShowGrid;
            _surface.SetGridVisible(_session.ShowGrid);
        });
        Tool("−", () => SetZoom(_session.Zoom / 1.1, fromUser: true));
        Tool("+", () => SetZoom(_session.Zoom * 1.1, fromUser: true));
        Tool("Fit", Fit);
        Tool("Left", () => _session.Align("left"));
        Tool("Center", () => _session.Align("center"));
        Tool("Right", () => _session.Align("right"));
        Tool("Top", () => _session.Align("top"));
        Tool("Middle", () => _session.Align("middle"));
        Tool("Bottom", () => _session.Align("bottom"));
        Tool("Duplicate", () => _session.DuplicateSelection());
        Tool("Delete", () => _session.DeleteSelection());
    }

    private void Tool(string label, Action action)
    {
        var button = new Button { Content = label, Classes = { "tool" }, Margin = new Avalonia.Thickness(0, 0, 6, 6) };
        button.Click += (_, _) => action();
        ToolBar.Children.Add(button);
    }

    private void BuildInsert()
    {
        var icons = new Button
        {
            Content = "Icons",
            Classes = { "tool" },
            Margin = new Avalonia.Thickness(0, 0, 6, 6),
            Width = 124
        };
        ToolTip.SetTip(icons, "Play, smiles, flags, and more. Hover to choose. No picture file needed.");
        icons.Click += (_, _) => _session.AddIcon(BuiltInIcons.DefaultId);
        InsertDesigns.ShowIcons(icons, id => _session.AddIcon(id));
        InsertPanel.Children.Add(icons);
        foreach (var item in ElementFactory.Catalog)
        {
            var button = new Button
            {
                Content = item.Label,
                Classes = { "tool" },
                Margin = new Avalonia.Thickness(0, 0, 6, 6),
                Width = 124
            };
            ToolTip.SetTip(button, item.Blurb + "  ·  hover for designs");
            var kind = item.Kind;
            button.Click += (_, _) => _session.AddElement(kind);
            InsertDesigns.ShowOnHover(button, kind, preset => _session.AddElement(kind, preset));
            InsertPanel.Children.Add(button);
        }
    }

    private void RefreshStage(bool rebuildInspector)
    {
        if (_refreshingStage)
            return;
        _refreshingStage = true;
        try
        {
        var scene = _session.ActiveScene;
        _frame.Width = _session.Document.Width;
        _frame.Height = _session.Document.Height;
        _frame.Background = Painting.Brush(scene?.Background ?? "#101820");
        _surface.Width = _session.Document.Width;
        _surface.Height = _session.Document.Height;
        _surface.SetAssetRoot(_session.AssetRoot);
        _surface.UseDesign(_session);
        if (!_surface.IsGesturing)
            _surface.Rebuild();
        _scale.ScaleX = _session.Zoom;
        _scale.ScaleY = _session.Zoom;
        TitleLabel.Text = _session.Document.Name + (_session.IsDirty ? "  ·  unsaved" : "");
        Title = "Intuime — " + _session.Document.Name;
        SetStatus();
        if (rebuildInspector)
            _inspector.Show(_session, _tab);
        }
        finally
        {
            _refreshingStage = false;
        }
    }

    private void SetStatus()
    {
        var scene = _session.ActiveScene;
        StatusLabel.Text = scene == null
            ? "No scene"
            : $"{scene.Name}   ·   {scene.Elements.Count} elements   ·   {_session.Selection.Count} selected   ·   {Math.Round(_session.Zoom * 100)}%   ·   snap {(_session.SnapToGrid ? "on" : "off")}";
    }

    private int _reloadVersion;

    private void QueueReloadLists()
    {
        var version = ++_reloadVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _reloadVersion)
                ReloadLists();
        }, DispatcherPriority.Background);
    }

    private void ReloadLists()
    {
        _loadingLists = true;
        SceneList.SelectedIndex = -1;
        LayerList.SelectedIndex = -1;
        SceneList.Items.Clear();
        foreach (var scene in _session.Document.Scenes)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new Avalonia.CornerRadius(3),
                Background = Painting.Brush(scene.Background)
            });
            row.Children.Add(new TextBlock { Text = scene.Name, VerticalAlignment = VerticalAlignment.Center });
            if (scene.Screen > 0)
                row.Children.Add(new TextBlock { Text = DisplayScreens.Name(scene.Screen), Foreground = Painting.Brush(Painting.Muted), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            if (DisplayScreens.IsStart(_session.Document, scene))
                row.Children.Add(new TextBlock { Text = "Start", Foreground = Painting.Brush(Painting.Accent), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            SceneList.Items.Add(new ListBoxItem { Content = row, Tag = scene.Id });
        }
        LayerList.Items.Clear();
        if (_session.ActiveScene != null)
        {
            foreach (var element in _session.ActiveScene.Elements.OrderByDescending(item => item.Z))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var visible = new Button { Content = element.IsVisible ? "Hide" : "Show", Classes = { "tool" }, Padding = new Avalonia.Thickness(6, 2) };
                var locked = new Button { Content = element.IsLocked ? "Unlock" : "Lock", Classes = { "tool" }, Padding = new Avalonia.Thickness(6, 2) };
                var id = element.Id;
                visible.Click += (_, _) => Toggle(id, elementToChange => elementToChange.IsVisible = !elementToChange.IsVisible);
                locked.Click += (_, _) => Toggle(id, elementToChange => elementToChange.IsLocked = !elementToChange.IsLocked);
                row.Children.Add(visible);
                row.Children.Add(locked);
                row.Children.Add(new TextBlock
                {
                    Text = element.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Painting.Brush(element.IsVisible ? Painting.Text : Painting.Muted)
                });
                row.Children.Add(new TextBlock
                {
                    Text = Labels.Element(element.Kind),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Painting.Brush(Painting.Muted),
                    FontSize = 11
                });
                LayerList.Items.Add(new ListBoxItem { Content = row, Tag = element.Id });
            }
        }
        _loadingLists = false;
        HighlightLists();
    }

    private void Toggle(string id, Action<SceneElement> change)
    {
        var element = _session.ActiveScene?.Elements.FirstOrDefault(item => item.Id == id);
        if (element == null)
            return;
        change(element);
        _session.Commit(structure: true);
    }

    private void HighlightLists()
    {
        _loadingLists = true;
        foreach (var item in SceneList.Items.OfType<ListBoxItem>())
            item.IsSelected = (string?)item.Tag == _session.ActiveScene?.Id;
        var selected = _session.Selection.Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var item in LayerList.Items.OfType<ListBoxItem>())
            item.IsSelected = selected.Contains((string?)item.Tag ?? "");
        _loadingLists = false;
    }

    private void OnScenePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingLists)
            return;
        if (SceneList.SelectedItem is ListBoxItem { Tag: string id })
            _session.Activate(id);
    }

    private void OnLayerPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_loadingLists || _session.ActiveScene == null)
            return;
        var ids = (LayerList.SelectedItems ?? new System.Collections.ArrayList()).OfType<ListBoxItem>().Select(item => item.Tag as string).Where(id => id != null).ToHashSet();
        _session.SetSelection(_session.ActiveScene.Elements.Where(element => ids.Contains(element.Id)));
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox)
            return;
        var player = _players.FirstOrDefault(item => item.IsVisible);
        if (player != null)
        {
            if (e.Key == Key.Escape)
            {
                foreach (var open in _players.Where(item => item.IsVisible).ToList())
                    open.Close();
            }
            else if (e.Key == Key.F11)
                player.ToggleFullScreen();
            else
                player.PressKey(e.Key.ToString());
            e.Handled = true;
            return;
        }
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (control && e.Key == Key.S)
        {
            if (shift) await SaveAs();
            else await Save();
            e.Handled = true;
            return;
        }
        if (control && e.Key == Key.O) { await Open(); e.Handled = true; return; }
        if (control && e.Key == Key.N) { await NewExperience(); e.Handled = true; return; }
        if (control && e.Key == Key.Z) { _session.Undo(); e.Handled = true; return; }
        if (control && (e.Key == Key.Y || (shift && e.Key == Key.Z))) { _session.Redo(); e.Handled = true; return; }
        if (control && e.Key == Key.D) { _session.DuplicateSelection(); e.Handled = true; return; }
        if (control && e.Key == Key.A) { _session.SelectAll(); e.Handled = true; return; }
        if (control && e.Key == Key.C) { _session.CopySelection(out _clipboard); e.Handled = true; return; }
        if (control && e.Key == Key.V) { _session.Paste(_clipboard); e.Handled = true; return; }
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            _session.DeleteSelection();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F5)
        {
            Play(shift);
            e.Handled = true;
            return;
        }
        var step = shift ? 10 : (_session.SnapToGrid ? _session.GridSize : 1);
        var moved = e.Key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => (0, 0)
        };
        if (moved != (0, 0))
        {
            _session.Nudge(moved.Item1, moved.Item2);
            e.Handled = true;
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        SetZoom(_session.Zoom * (e.Delta.Y > 0 ? 1.1 : 0.9), fromUser: true);
        e.Handled = true;
    }

    private void SetZoom(double zoom, bool fromUser)
    {
        if (fromUser)
            _userZoom = true;
        _session.Zoom = Math.Clamp(zoom, 0.12, 2.5);
        _scale.ScaleX = _session.Zoom;
        _scale.ScaleY = _session.Zoom;
        SetStatus();
        CenterStage();
    }

    private void CenterStage()
    {
        var scaledWidth = _session.Document.Width * _scale.ScaleX;
        var scaledHeight = _session.Document.Height * _scale.ScaleY;
        var viewWidth = Viewport.Bounds.Width;
        var viewHeight = Viewport.Bounds.Height;
        if (viewWidth < 40 || viewHeight < 40 || scaledWidth < 1 || scaledHeight < 1)
            return;
        var padX = Math.Max(24, (viewWidth - scaledWidth) / 2);
        var padY = Math.Max(24, (viewHeight - scaledHeight) / 2);
        StageHost.Margin = new Thickness(padX, padY);
    }

    private void RefitIfAutomatic()
    {
        if (_userZoom)
            return;
        if (Viewport.Bounds.Width < 80 || Viewport.Bounds.Height < 80 || _session.Document.Width < 1)
            return;
        var size = Viewport.Bounds.Size;
        if (Math.Abs(size.Width - _fitSize.Width) < 1 && Math.Abs(size.Height - _fitSize.Height) < 1)
            return;
        _fitSize = size;
        var scale = Math.Min(
            (size.Width - 48) / _session.Document.Width,
            (size.Height - 48) / _session.Document.Height);
        SetZoom(scale, fromUser: false);
    }

    private void Fit()
    {
        _userZoom = false;
        _fitSize = default;
        RefitIfAutomatic();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null)
            return;
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path == null)
                continue;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var kind = extension switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" => ElementKind.Image,
                ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" or ".wmv" => ElementKind.Video,
                ".mp3" or ".wav" or ".ogg" or ".aac" or ".m4a" or ".flac" => ElementKind.Audio,
                _ => (ElementKind?)null
            };
            if (kind == null)
                continue;
            var element = ElementFactory.Create(kind.Value, 80, 80, 0, _session.ActiveScene?.Elements.Count + 1 ?? 1);
            element.Source = AssetFiles.Import(_session.AssetRoot, path);
            _session.AddElement(element);
        }
        await Task.CompletedTask;
    }

    private void OnAddScene(object? sender, RoutedEventArgs e) => _session.AddScene();
    private void OnDuplicateScene(object? sender, RoutedEventArgs e) => _session.DuplicateActiveScene();
    private async void OnDeleteScene(object? sender, RoutedEventArgs e)
    {
        if (_session.Document.Scenes.Count <= 1)
        {
            await Dialogs.Alert(this, "Scenes", "An experience needs at least one scene.");
            return;
        }
        if (await Dialogs.Confirm(this, "Delete scene", $"Delete {_session.ActiveScene?.Name}?"))
            _session.DeleteActiveScene();
    }

    private void OnLayerUp(object? sender, RoutedEventArgs e) => _session.BringForward();
    private void OnLayerDown(object? sender, RoutedEventArgs e) => _session.SendBackward();
    private void OnBringFront(object? sender, RoutedEventArgs e) => _session.BringToFront();
    private void OnSendBack(object? sender, RoutedEventArgs e) => _session.SendToBack();
    private void OnPreview(object? sender, RoutedEventArgs e) => Play(false);
    private void OnPlay(object? sender, RoutedEventArgs e) => Play(true);
    private void OnTabProperties(object? sender, RoutedEventArgs e) => SetTab("properties");
    private void OnTabInteract(object? sender, RoutedEventArgs e) => SetTab("interactions");
    private void OnTabData(object? sender, RoutedEventArgs e) => SetTab("variables");

    private void SetTab(string tab)
    {
        _tab = tab;
        _inspector.Show(_session, tab);
    }

    private void Play(bool fullscreen)
    {
        if (_session.Document.Scenes.Count == 0)
            return;
        foreach (var open in _players.Where(item => item.IsVisible).ToList())
            open.Close();
        _players.Clear();
        _players.AddRange(PlayerWindow.Open(ProjectStore.Clone(_session.Document), _session.AssetRoot, fullscreen, Screens));
    }

    private async Task NewExperience()
    {
        if (!await EnsureSaved())
            return;
        var created = await Dialogs.NewExperience(this);
        if (created == null)
            return;
        _session.Load(SampleExperiences.CreateBlank(created.Value.Name, created.Value.Width, created.Value.Height), null, AssetFiles.NewDraftRoot(), markClean: true);
        Fit();
    }

    private void OpenSample()
    {
        var root = AssetFiles.PrepareSampleRoot();
        _session.Load(SampleExperiences.CreateLumenGallery(), null, root, markClean: true);
        Fit();
    }

    private async Task Open()
    {
        if (!await EnsureSaved())
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open an Intuime experience",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Intuime experience") { Patterns = new[] { "*.intuime", "*.json" } }
            }
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null)
            return;
        try
        {
            var document = ProjectStore.Load(path);
            _session.Load(document, path, Path.GetDirectoryName(path)!, markClean: true);
            Fit();
        }
        catch (Exception ex)
        {
            await Dialogs.Alert(this, "Could not open", ex.Message);
        }
    }

    private async Task Save()
    {
        if (string.IsNullOrEmpty(_session.FilePath))
        {
            await SaveAs();
            return;
        }
        await Write(_session.FilePath);
    }

    private async Task SaveAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save experience",
            DefaultExtension = "intuime",
            SuggestedFileName = _session.Document.Name + ".intuime",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Intuime experience") { Patterns = new[] { "*.intuime" } }
            }
        });
        var path = file?.TryGetLocalPath();
        if (path == null)
            return;
        if (!path.EndsWith(".intuime", StringComparison.OrdinalIgnoreCase))
            path += ".intuime";
        await Write(path);
    }

    private async Task Write(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            AssetFiles.Materialize(_session.Document, _session.AssetRoot, directory);
            ProjectStore.Save(_session.Document, path);
            _session.MarkSaved(path, directory);
            SetStatus();
            TitleLabel.Text = _session.Document.Name;
        }
        catch (Exception ex)
        {
            await Dialogs.Alert(this, "Could not save", ex.Message);
        }
    }

    private async Task<string?> BrowseAsset(string kind)
    {
        var patterns = kind switch
        {
            "video" => new[] { "*.mp4", "*.mov", "*.avi", "*.mkv", "*.webm", "*.wmv" },
            "audio" => new[] { "*.mp3", "*.wav", "*.ogg", "*.aac", "*.m4a", "*.flac" },
            _ => new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp" }
        };
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a file",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } }
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        return path == null ? null : AssetFiles.Import(_session.AssetRoot, path);
    }

    private async Task<bool> EnsureSaved()
    {
        if (!_session.IsDirty)
            return true;
        var choice = await Dialogs.AskSave(this, _session.Document.Name);
        if (choice == SaveChoice.Cancel)
            return false;
        if (choice == SaveChoice.Discard)
            return true;
        var before = _session.FilePath;
        await Save();
        return !_session.IsDirty || _session.FilePath != before && !string.IsNullOrEmpty(_session.FilePath);
    }

    private bool IsInspectorEditing() => _inspector.IsKeyboardFocusWithin;

    private async Task Shortcuts()
    {
        await Dialogs.Alert(this, "Shortcuts",
            "Ctrl+N new\nCtrl+O open\nCtrl+S save\nCtrl+Z undo    Ctrl+Y redo\nCtrl+D duplicate    Delete remove\nCtrl+C copy    Ctrl+V paste    Ctrl+A select all\nArrow keys nudge    Shift+arrow nudges farther\nCtrl+wheel zooms\nF5 preview    Shift+F5 full screen\n\nIn the player: Esc exits, F11 full screen, Left and Right change scene.");
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowClose || !_session.IsDirty)
        {
            foreach (var open in _players.Where(item => item.IsVisible).ToList())
                open.Close();
            return;
        }
        e.Cancel = true;
        if (!await EnsureSaved())
            return;
        _allowClose = true;
        Close();
    }
}
