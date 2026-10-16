namespace Intuime.Core;

public sealed class EditorSession
{
    private readonly List<string> _history = new();
    private int _index = -1;
    private string _saved = "";

    public ExperienceDocument Document { get; private set; } = new();
    public string? FilePath { get; private set; }
    public string AssetRoot { get; private set; } = "";
    public Scene? ActiveScene { get; private set; }
    public List<SceneElement> Selection { get; } = new();
    public bool SnapToGrid { get; set; } = true;
    public int GridSize { get; set; } = 8;
    public double Zoom { get; set; } = 0.55;
    public bool ShowGrid { get; set; } = true;

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index >= 0 && _index < _history.Count - 1;
    public bool IsDirty => ProjectStore.Serialize(Document) != _saved;

    public event Action? DocumentChanged;
    public event Action? StructureChanged;
    public event Action? SelectionChanged;

    public void Load(ExperienceDocument document, string? filePath, string assetRoot, bool markClean)
    {
        Document = document;
        ProjectStore.EnsureIds(Document);
        FilePath = filePath;
        AssetRoot = assetRoot;
        _history.Clear();
        _history.Add(ProjectStore.Serialize(Document));
        _index = 0;
        _saved = markClean ? _history[0] : "";
        ActiveScene = Document.Scenes.FirstOrDefault(s => s.Id == Document.StartSceneId) ?? Document.Scenes[0];
        Selection.Clear();
        RaiseAll();
    }

    private void RaiseAll()
    {
        StructureChanged?.Invoke();
        DocumentChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void TouchLive()
    {
        DocumentChanged?.Invoke();
    }

    public void Commit(bool structure = false)
    {
        var json = ProjectStore.Serialize(Document);
        if (_index >= 0 && _history[_index] == json)
        {
            if (structure) StructureChanged?.Invoke();
            DocumentChanged?.Invoke();
            return;
        }

        if (_index < _history.Count - 1)
            _history.RemoveRange(_index + 1, _history.Count - _index - 1);
        _history.Add(json);
        _index = _history.Count - 1;
        while (_history.Count > 100)
        {
            _history.RemoveAt(0);
            _index--;
        }

        if (structure) StructureChanged?.Invoke();
        DocumentChanged?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;
        _index--;
        Restore(_history[_index]);
    }

    public void Redo()
    {
        if (!CanRedo)
            return;
        _index++;
        Restore(_history[_index]);
    }

    public void MarkSaved(string path, string assetRoot)
    {
        FilePath = path;
        AssetRoot = assetRoot;
        _saved = ProjectStore.Serialize(Document);
        DocumentChanged?.Invoke();
    }

    public SceneElement AddElement(ElementKind kind) => AddElement(kind, null);

    public SceneElement AddElement(ElementKind kind, ElementPreset? preset)
    {
        var scene = RequireScene();
        var index = scene.Elements.Count(e => e.Kind == kind) + 1;
        var stagger = scene.Elements.Count % 6;
        var element = preset == null
            ? ElementFactory.Create(kind, 80 + stagger * 28, 80 + stagger * 28, NextZ(scene), index)
            : ElementPresets.Create(kind, preset, 80 + stagger * 28, 80 + stagger * 28, NextZ(scene), index);
        if (preset != null)
            element.Name = UniqueName(scene, preset.Name);
        scene.Elements.Add(element);
        SelectOnly(element);
        Commit(structure: true);
        return element;
    }

    public SceneElement AddElement(SceneElement element)
    {
        var scene = RequireScene();
        element.Z = NextZ(scene);
        scene.Elements.Add(element);
        SelectOnly(element);
        Commit(structure: true);
        return element;
    }

    public void DeleteSelection()
    {
        var scene = ActiveScene;
        if (scene == null || Selection.Count == 0)
            return;
        var ids = Selection.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        scene.Elements.RemoveAll(e => ids.Contains(e.Id));
        Selection.Clear();
        Commit(structure: true);
        SelectionChanged?.Invoke();
    }

    public void DuplicateSelection()
    {
        var scene = ActiveScene;
        if (scene == null || Selection.Count == 0)
            return;
        var copies = CloneElements(Selection, 24, 24);
        foreach (var copy in copies)
        {
            copy.Z = NextZ(scene);
            scene.Elements.Add(copy);
        }
        SetSelection(copies);
        Commit(structure: true);
    }

    public void CopySelection(out List<SceneElement> clipboard)
    {
        clipboard = CloneElements(Selection, 0, 0);
    }

    public void Paste(IReadOnlyList<SceneElement> clipboard)
    {
        var scene = ActiveScene;
        if (scene == null || clipboard.Count == 0)
            return;
        var copies = CloneElements(clipboard, 24, 24);
        foreach (var copy in copies)
        {
            copy.Z = NextZ(scene);
            scene.Elements.Add(copy);
        }
        SetSelection(copies);
        Commit(structure: true);
    }

    public void SelectOnly(SceneElement? element)
    {
        Selection.Clear();
        if (element != null)
            Selection.Add(element);
        SelectionChanged?.Invoke();
    }

    public void ToggleSelect(SceneElement element)
    {
        var existing = Selection.FirstOrDefault(s => s.Id == element.Id);
        if (existing == null)
            Selection.Add(element);
        else
            Selection.Remove(existing);
        SelectionChanged?.Invoke();
    }

    public void SetSelection(IEnumerable<SceneElement> elements)
    {
        Selection.Clear();
        Selection.AddRange(elements);
        SelectionChanged?.Invoke();
    }

    public void SelectAll()
    {
        if (ActiveScene == null)
            return;
        SetSelection(ActiveScene.Elements.ToList());
    }

    public void Nudge(double dx, double dy)
    {
        if (Selection.Count == 0 || Selection.All(s => s.IsLocked))
            return;
        foreach (var element in Selection.Where(s => !s.IsLocked))
        {
            element.X += dx;
            element.Y += dy;
        }
        Commit();
    }

    public void Align(string edge)
    {
        var items = Selection.Where(s => !s.IsLocked).ToList();
        LayoutMath.Align(items, edge);
        Commit();
    }

    public void Distribute(bool horizontal)
    {
        var items = Selection.Where(s => !s.IsLocked).ToList();
        LayoutMath.Distribute(items, horizontal);
        Commit();
    }

    public void BringForward() => MoveZ(1);
    public void SendBackward() => MoveZ(-1);

    public void BringToFront()
    {
        if (ActiveScene == null || Selection.Count == 0)
            return;
        var z = NextZ(ActiveScene);
        foreach (var element in Selection)
            element.Z = z++;
        Commit(structure: true);
    }

    public void SendToBack()
    {
        if (ActiveScene == null || Selection.Count == 0)
            return;
        var z = ActiveScene.Elements.Count == 0 ? 0 : ActiveScene.Elements.Min(e => e.Z) - Selection.Count;
        foreach (var element in Selection)
            element.Z = z++;
        Commit(structure: true);
    }

    public Scene AddScene()
    {
        var scene = new Scene
        {
            Id = Ids.New(),
            Name = $"Scene {Document.Scenes.Count + 1}",
            Background = "#101820"
        };
        Document.Scenes.Add(scene);
        ActiveScene = scene;
        Selection.Clear();
        Commit(structure: true);
        SelectionChanged?.Invoke();
        return scene;
    }

    public Scene DuplicateActiveScene()
    {
        var scene = RequireScene();
        var copy = ProjectStore.Clone(scene);
        var map = new Dictionary<string, string>(StringComparer.Ordinal) { [scene.Id] = Ids.New() };
        copy.Id = map[scene.Id];
        copy.Name = scene.Name + " copy";
        foreach (var element in copy.Elements)
        {
            var next = Ids.New();
            map[element.Id] = next;
            element.Id = next;
        }
        Remap(copy.Interactions, map);
        foreach (var element in copy.Elements)
            Remap(element.Interactions, map);
        foreach (var interaction in copy.Interactions.Concat(copy.Elements.SelectMany(e => e.Interactions)))
            interaction.Id = Ids.New();
        var index = Document.Scenes.FindIndex(s => s.Id == scene.Id);
        Document.Scenes.Insert(index + 1, copy);
        ActiveScene = copy;
        Selection.Clear();
        Commit(structure: true);
        SelectionChanged?.Invoke();
        return copy;
    }

    public void DeleteActiveScene()
    {
        var scene = ActiveScene;
        if (scene == null || Document.Scenes.Count <= 1)
            return;
        Document.Scenes.Remove(scene);
        if (Document.StartSceneId == scene.Id)
            Document.StartSceneId = Document.Scenes[0].Id;
        ActiveScene = Document.Scenes[0];
        Selection.Clear();
        Commit(structure: true);
        SelectionChanged?.Invoke();
    }

    public void MoveActiveScene(int delta)
    {
        var scene = ActiveScene;
        if (scene == null)
            return;
        var index = Document.Scenes.IndexOf(scene);
        var next = index + delta;
        if (next < 0 || next >= Document.Scenes.Count)
            return;
        Document.Scenes.RemoveAt(index);
        Document.Scenes.Insert(next, scene);
        Commit(structure: true);
    }

    public void Activate(string sceneId)
    {
        var scene = Document.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (scene == null || ActiveScene?.Id == scene.Id)
            return;
        ActiveScene = scene;
        Selection.Clear();
        StructureChanged?.Invoke();
        DocumentChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void SetStartScene(string sceneId)
    {
        if (Document.Scenes.All(s => s.Id != sceneId))
            return;
        Document.StartSceneId = sceneId;
        Commit(structure: true);
    }

    public void RenameActiveScene(string name)
    {
        if (ActiveScene == null)
            return;
        ActiveScene.Name = string.IsNullOrWhiteSpace(name) ? ActiveScene.Name : name.Trim();
        Commit(structure: true);
    }

    private void MoveZ(int direction)
    {
        var scene = ActiveScene;
        var element = Selection.FirstOrDefault();
        if (scene == null || element == null)
            return;
        var ordered = scene.Elements.OrderBy(e => e.Z).ToList();
        var index = ordered.FindIndex(e => e.Id == element.Id);
        var swap = index + direction;
        if (index < 0 || swap < 0 || swap >= ordered.Count)
            return;
        (ordered[index].Z, ordered[swap].Z) = (ordered[swap].Z, ordered[index].Z);
        Commit(structure: true);
    }

    private void Restore(string json)
    {
        var sceneId = ActiveScene?.Id;
        var selected = Selection.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        Document = ProjectStore.Deserialize(json);
        ActiveScene = Document.Scenes.FirstOrDefault(s => s.Id == sceneId) ?? Document.Scenes[0];
        Selection.Clear();
        Selection.AddRange(ActiveScene.Elements.Where(e => selected.Contains(e.Id)));
        StructureChanged?.Invoke();
        DocumentChanged?.Invoke();
        SelectionChanged?.Invoke();
    }

    private Scene RequireScene() =>
        ActiveScene ?? throw new InvalidOperationException("There is no active scene.");

    private static int NextZ(Scene scene) =>
        scene.Elements.Count == 0 ? 0 : scene.Elements.Max(e => e.Z) + 1;

    private static string UniqueName(Scene scene, string name)
    {
        if (scene.Elements.All(element => element.Name != name))
            return name;
        var number = 2;
        while (scene.Elements.Any(element => element.Name == $"{name} {number}"))
            number++;
        return $"{name} {number}";
    }

    private static List<SceneElement> CloneElements(IEnumerable<SceneElement> source, double dx, double dy)
    {
        var originals = source.ToList();
        var map = originals.ToDictionary(e => e.Id, _ => Ids.New(), StringComparer.Ordinal);
        var copies = new List<SceneElement>();
        foreach (var element in originals)
        {
            var copy = ProjectStore.Clone(element);
            copy.Id = map[element.Id];
            copy.Name = element.Name;
            copy.X += dx;
            copy.Y += dy;
            copy.IsPlaying = false;
            foreach (var interaction in copy.Interactions)
                interaction.Id = Ids.New();
            Remap(copy.Interactions, map);
            copies.Add(copy);
        }
        return copies;
    }

    private static void Remap(List<Interaction> interactions, Dictionary<string, string> map)
    {
        foreach (var interaction in interactions)
        {
            foreach (var action in interaction.Actions)
            {
                if (PlayBarTargets.TryGetVideoId(action.TargetId, out var videoId) && map.TryGetValue(videoId, out var remapped))
                    action.TargetId = PlayBarTargets.For(remapped);
                else if (!string.IsNullOrEmpty(action.TargetId) && map.TryGetValue(action.TargetId, out var next))
                    action.TargetId = next;
            }
        }
    }
}
