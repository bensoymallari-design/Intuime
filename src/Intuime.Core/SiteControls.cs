namespace Intuime.Core;

public static class SiteControls
{
    public static bool HasOwn(Scene? scene) =>
        scene != null && scene.Elements.Any(IsOwn);

    public static bool IsOwn(SceneElement element) =>
        element.IsVisible && element.Kind is ElementKind.Button or ElementKind.Icon;
}
