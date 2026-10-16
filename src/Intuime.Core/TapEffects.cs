namespace Intuime.Core;

public static class TapEffects
{
    public static readonly string[] All = { "Pop", "Pulse", "Bounce", "Shake", "Fade", "Flash", "Spin" };

    public static bool Known(string? name) =>
        All.Any(effect => string.Equals(effect, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int DurationMs(string name) => Normalize(name) switch
    {
        "bounce" => 640,
        "spin" => 560,
        "shake" => 480,
        _ => 420
    };

    public static void Apply(SceneElement element, string name, double t)
    {
        Reset(element);
        t = Math.Clamp(t, 0, 1);
        switch (Normalize(name))
        {
            case "pop":
                element.EffectScale = 1 + 0.16 * Math.Sin(t * Math.PI);
                break;
            case "pulse":
                element.EffectScale = 1 + 0.07 * Math.Sin(t * Math.PI * 2);
                element.EffectOpacity = 1 - 0.4 * Math.Sin(t * Math.PI);
                break;
            case "bounce":
                element.EffectShiftY = -36 * Math.Sin(t * Math.PI);
                element.EffectScale = 1 + 0.08 * Math.Sin(t * Math.PI);
                break;
            case "shake":
                element.EffectShiftX = 12 * Math.Sin(t * Math.PI * 6) * (1 - t);
                break;
            case "fade":
                element.EffectOpacity = 1 - 0.8 * Math.Sin(t * Math.PI);
                break;
            case "flash":
                element.EffectFill = t < 0.45 ? "#FFFFFF" : null;
                element.EffectScale = 1 + 0.05 * Math.Sin(t * Math.PI);
                break;
            case "spin":
                element.EffectRotation = 360 * Math.Sin(t * Math.PI);
                element.EffectScale = 1 + 0.06 * Math.Sin(t * Math.PI);
                break;
        }
    }

    public static void Reset(SceneElement element)
    {
        element.EffectScale = 1;
        element.EffectOpacity = 1;
        element.EffectRotation = 0;
        element.EffectShiftX = 0;
        element.EffectShiftY = 0;
        element.EffectFill = null;
    }

    private static string Normalize(string? name) => name?.Trim().ToLowerInvariant() ?? "";
}
