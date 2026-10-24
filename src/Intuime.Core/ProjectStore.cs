using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intuime.Core;

public static class ProjectStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ExperienceDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static ExperienceDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<ExperienceDocument>(json, Options)
            ?? throw new InvalidDataException("This file is not an Intuime experience.");
        EnsureIds(document);
        return document;
    }

    public static ExperienceDocument Clone(ExperienceDocument document) =>
        Deserialize(Serialize(document));

    public static Scene Clone(Scene scene)
    {
        var json = JsonSerializer.Serialize(scene, Options);
        return JsonSerializer.Deserialize<Scene>(json, Options)
            ?? throw new InvalidDataException("Could not copy the scene.");
    }

    public static SceneElement Clone(SceneElement element)
    {
        var json = JsonSerializer.Serialize(element, Options);
        return JsonSerializer.Deserialize<SceneElement>(json, Options)
            ?? throw new InvalidDataException("Could not copy the element.");
    }

    public static void Save(ExperienceDocument document, string path)
    {
        EnsureIds(document);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(document));
    }

    public static ExperienceDocument Load(string path) =>
        Deserialize(File.ReadAllText(path));

    public static void EnsureIds(ExperienceDocument document)
    {
        document.Width = Math.Clamp(document.Width, 320, 7680);
        document.Height = Math.Clamp(document.Height, 240, 4320);
        if (document.Scenes.Count == 0)
        {
            document.Scenes.Add(new Scene
            {
                Id = Ids.New(),
                Name = "Scene 1",
                Background = "#101820"
            });
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in document.Scenes)
        {
            scene.Id = Unique(scene.Id, seen);
            scene.Name = string.IsNullOrWhiteSpace(scene.Name) ? "Scene" : scene.Name;
            scene.Background = string.IsNullOrWhiteSpace(scene.Background) ? "#101820" : scene.Background;
            foreach (var interaction in scene.Interactions)
                Repair(interaction, seen);
            foreach (var element in scene.Elements)
            {
                element.Id = Unique(element.Id, seen);
                element.Name = string.IsNullOrWhiteSpace(element.Name) ? element.Kind.ToString() : element.Name;
                element.Opacity = Math.Clamp(element.Opacity, 0, 1);
                if (element.Width < 8) element.Width = 8;
                if (element.Height < 8) element.Height = 8;
                foreach (var interaction in element.Interactions)
                    Repair(interaction, seen);
            }
        }

        foreach (var interaction in document.Interactions)
            Repair(interaction, seen);

        if (document.Scenes.All(s => s.Id != document.StartSceneId))
            document.StartSceneId = document.Scenes[0].Id;

        document.Variables.RemoveAll(v => string.IsNullOrWhiteSpace(v.Name));
    }

    public static IEnumerable<string> EnumerateAssets(ExperienceDocument document)
    {
        foreach (var scene in document.Scenes)
        {
            if (!string.IsNullOrWhiteSpace(scene.BackgroundImage))
                yield return scene.BackgroundImage;
            if (!string.IsNullOrWhiteSpace(scene.BackgroundVideo))
                yield return scene.BackgroundVideo;
            foreach (var element in scene.Elements)
            {
                if (!string.IsNullOrWhiteSpace(element.Source))
                    yield return element.Source;
            }
        }
    }

    public static string Resolve(string assetRoot, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "";
        if (Path.IsPathRooted(source))
            return source;
        var relative = source.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(assetRoot, relative));
    }

    private static void Repair(Interaction interaction, HashSet<string> seen)
    {
        interaction.Id = Unique(interaction.Id, seen);
        if (interaction.TimerMs < 100)
            interaction.TimerMs = 100;
        interaction.Actions ??= new();
    }

    private static string Unique(string? id, HashSet<string> seen)
    {
        var value = string.IsNullOrWhiteSpace(id) ? Ids.New() : id.Trim();
        while (!seen.Add(value))
            value = Ids.New();
        return value;
    }
}
