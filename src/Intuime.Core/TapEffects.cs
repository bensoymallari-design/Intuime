namespace Intuime.Core;

public static class TapEffects
{
    public static readonly string[] All =
    {
        "Pop", "Pulse", "Bounce", "Shake", "Fade", "Flash", "Spin",
        "Squash", "Flip", "Wobble", "Heartbeat", "Shrink", "Glow"
    };

    public static bool Known(string? name) =>
        All.Any(effect => string.Equals(effect, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int DurationMs(string name) => Normalize(name) switch
    {
        "bounce" => 980,
        "spin" or "flip" => 720,
        "heartbeat" => 760,
        "shake" or "wobble" => 560,
        _ => 520
    };

    public static double Strength(SceneElement element)
    {
        var value = element.TapStrength <= 0 ? 1 : element.TapStrength;
        return Math.Clamp(value, 0.25, 3);
    }

    public static void Apply(SceneElement element, string name, double t)
    {
        Reset(element);
        t = Math.Clamp(t, 0, 1);
        var strength = Strength(element);
        var hump = Hump(t) * strength;
        switch (Normalize(name))
        {
            case "pop":
                element.EffectScale = 1 + 0.28 * hump;
                break;
            case "pulse":
                element.EffectScale = 1 + 0.12 * Math.Sin(t * Math.PI * 2) * strength;
                element.EffectOpacity = 1 - 0.55 * Math.Sin(t * Math.PI);
                break;
            case "bounce":
                element.EffectShiftY = -120 * hump;
                element.EffectScale = 1 + 0.2 * hump;
                break;
            case "shake":
                element.EffectShiftX = 26 * Math.Sin(t * Math.PI * 8) * (1 - t) * strength;
                break;
            case "fade":
                element.EffectOpacity = 1 - 0.85 * Hump(t);
                break;
            case "flash":
                element.EffectFill = t < 0.55 ? "#FFFFFF" : null;
                element.EffectScale = 1 + 0.08 * hump;
                break;
            case "spin":
                element.EffectRotation = 360 * t * Math.Min(strength, 2);
                element.EffectScale = 1 + 0.08 * hump;
                break;
            case "squash":
                element.EffectScaleX = 1 + 0.34 * hump;
                element.EffectScaleY = Math.Max(0.35, 1 - 0.28 * hump);
                break;
            case "flip":
                element.EffectScaleX = Math.Max(0.02, Math.Abs(Math.Cos(t * Math.PI)));
                element.EffectScale = 1 + 0.06 * hump;
                break;
            case "wobble":
                element.EffectRotation = Math.Sin(t * Math.PI * 4) * 22 * strength;
                element.EffectScale = 1 + 0.06 * hump;
                break;
            case "heartbeat":
                element.EffectScale = 1 + 0.2 * strength * Math.Abs(Math.Sin(t * Math.PI * 2));
                break;
            case "shrink":
                element.EffectScale = Math.Max(0.35, 1 - 0.34 * hump);
                break;
            case "glow":
                element.EffectFill = t < 0.62 ? "#FFF6D48A" : null;
                element.EffectScale = 1 + 0.14 * hump;
                break;
        }
    }

    public static void Reset(SceneElement element)
    {
        element.EffectScale = 1;
        element.EffectScaleX = 1;
        element.EffectScaleY = 1;
        element.EffectOpacity = 1;
        element.EffectRotation = 0;
        element.EffectShiftX = 0;
        element.EffectShiftY = 0;
        element.EffectFill = null;
    }

    private static double Hump(double t) => Math.Pow(Math.Sin(Math.PI * Math.Clamp(t, 0, 1)), 0.4);

    private static string Normalize(string? name) => name?.Trim().ToLowerInvariant() ?? "";
}
