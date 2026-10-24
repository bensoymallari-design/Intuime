using Intuime.Core;

namespace Intuime;

public static class AssetFiles
{
    public static string PrepareSampleRoot()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Intuime", "sample-cache");
        CopyBundledAssets(root);
        return root;
    }

    public static string NewDraftRoot()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Intuime",
            "drafts",
            Ids.New());
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        return root;
    }

    public static void CopyBundledAssets(string root)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "samples", "lumen-gallery", "assets");
        var destination = Path.Combine(root, "assets");
        Directory.CreateDirectory(destination);
        if (!Directory.Exists(source))
            return;
        foreach (var file in Directory.GetFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, true);
        }
    }

    public static string Import(string assetRoot, string sourcePath)
    {
        var assets = Path.Combine(assetRoot, "assets");
        Directory.CreateDirectory(assets);
        var name = Path.GetFileName(sourcePath);
        var destination = Path.Combine(assets, name);
        var index = 1;
        while (File.Exists(destination) && !SameFile(destination, sourcePath))
        {
            name = $"{Path.GetFileNameWithoutExtension(sourcePath)}-{index}{Path.GetExtension(sourcePath)}";
            destination = Path.Combine(assets, name);
            index++;
        }
        if (!SameFile(destination, sourcePath))
            File.Copy(sourcePath, destination, true);
        return "assets/" + name;
    }

    public static void Materialize(ExperienceDocument document, string fromRoot, string toRoot)
    {
        Directory.CreateDirectory(Path.Combine(toRoot, "assets"));
        foreach (var scene in document.Scenes)
        {
            scene.BackgroundImage = Relocate(scene.BackgroundImage, fromRoot, toRoot);
            scene.BackgroundVideo = Relocate(scene.BackgroundVideo, fromRoot, toRoot);
            foreach (var element in scene.Elements)
                element.Source = Relocate(element.Source, fromRoot, toRoot);
        }
    }

    private static string Relocate(string source, string fromRoot, string toRoot)
    {
        if (string.IsNullOrWhiteSpace(source))
            return source;
        var absolute = ProjectStore.Resolve(fromRoot, source);
        if (!File.Exists(absolute))
            return source;
        var name = Path.GetFileName(absolute);
        var destination = Path.Combine(toRoot, "assets", name);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!SameFile(absolute, destination))
            File.Copy(absolute, destination, true);
        return "assets/" + name;
    }

    private static bool SameFile(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
