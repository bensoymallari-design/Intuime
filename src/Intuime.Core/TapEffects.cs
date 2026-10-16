namespace Intuime.Core;

public static class TapEffects
{
    public static readonly string[] All = { "Pop", "Pulse", "Bounce", "Shake", "Fade", "Flash", "Spin" };

    public static bool Known(string? name) =>
        All.Any(effect => string.Equals(effect, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int DurationMs(string name) => Normalize(name) switch
    {
        "bounce" => 980,
        "spin" => 720,
        "shake" => 560,
        _ => 520
    };

    public static void Apply(SceneElement element, string name, double t)
    {
        Reset(element);
        t = Math.Clamp(t, 0, 1);
        switch (Normalize(name))
        {
            case "pop":
                element.EffectScale = 1 + 0.28 * Hump(t);
                break;
            case "pulse":
                element.EffectScale = 1 + 0.12 * Math.Sin(t * Math.PI * 2);
                element.EffectOpacity = 1 - 0.55 * Math.Sin(t * Math.PI);
                break;
            case "bounce":
                element.EffectShiftY = -120 * Hump(t);
                element.EffectScale = 1 + 0.2 * Hump(t);
                break;
            case "shake":
                element.EffectShiftX = 26 * Math.Sin(t * Math.PI * 8) * (1 - t);
                break;
            case "fade":
                element.EffectOpacity = 1 - 0.85 * Hump(t);
                break;
            case "flash":
                element.EffectFill = t < 0.55 ? "#FFFFFF" : null;
                element.EffectScale = 1 + 0.08 * Hump(t);
                break;
            case "spin":
                element.EffectRotation = 360 * t;
                element.EffectScale = 1 + 0.08 * Hump(t);
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

    private static double Hump(double t) => Math.Pow(Math.Sin(Math.PI * Math.Clamp(t, 0, 1)), 0.4);

    private static string Normalize(string? name) => name?.Trim().ToLowerInvariant() ?? "";
}
