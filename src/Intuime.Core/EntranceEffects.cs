namespace Intuime.Core;

public static class EntranceEffects
{
    public static readonly string[] All =
    {
        "Fade in",
        "Slide from left",
        "Slide from right",
        "Slide from above",
        "Slide from below",
        "Rotate",
        "Bounce",
        "Pop",
        "Grow",
        "Flip"
    };

    public const int DefaultDurationMs = 700;

    public static bool Known(string? name) =>
        All.Any(effect => string.Equals(effect, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int DelayMs(SceneElement element) => Math.Clamp(element.EntranceDelayMs, 0, 30000);

    public static int DurationMs(SceneElement element)
    {
        var value = element.EntranceMs <= 0 ? DefaultDurationMs : element.EntranceMs;
        return Math.Clamp(value, 150, 8000);
    }

    public static void Apply(SceneElement element, string name, double t)
    {
        TapEffects.Reset(element);
        t = Math.Clamp(t, 0, 1);
        switch (Normalize(name))
        {
            case "fade in":
                element.EffectOpacity = Ease(t);
                break;
            case "slide from left":
                element.EffectShiftX = -640 * (1 - Ease(t));
                element.EffectOpacity = Appear(t);
                break;
            case "slide from right":
                element.EffectShiftX = 640 * (1 - Ease(t));
                element.EffectOpacity = Appear(t);
                break;
            case "slide from above":
                element.EffectShiftY = -420 * (1 - Ease(t));
                element.EffectOpacity = Appear(t);
                break;
            case "slide from below":
                element.EffectShiftY = 420 * (1 - Ease(t));
                element.EffectOpacity = Appear(t);
                break;
            case "rotate":
                element.EffectRotation = -180 * (1 - Ease(t));
                element.EffectScale = 0.72 + 0.28 * Ease(t);
                element.EffectOpacity = Appear(t);
                break;
            case "bounce":
                var bounce = BounceOut(t);
                element.EffectShiftY = -280 * (1 - bounce);
                element.EffectScale = 0.86 + 0.14 * bounce;
                element.EffectOpacity = Appear(t);
                break;
            case "pop":
                var pop = BackOut(t);
                element.EffectScale = Math.Max(0.02, 0.15 + 0.85 * pop);
                element.EffectOpacity = Appear(t);
                break;
            case "grow":
                element.EffectScale = Math.Max(0.02, Ease(t));
                element.EffectOpacity = Ease(t);
                break;
            case "flip":
                element.EffectScaleX = Math.Max(0.02, Ease(t));
                element.EffectOpacity = Appear(t);
                break;
        }
    }

    public static void Reset(SceneElement element) => TapEffects.Reset(element);

    private static double Appear(double t) => Math.Clamp(t / 0.28, 0, 1);

    private static double Ease(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - t, 3);
    }

    private static double BackOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    private static double BounceOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        const double n1 = 7.5625;
        const double d1 = 2.75;
        if (t < 1 / d1)
            return n1 * t * t;
        if (t < 2 / d1)
        {
            t -= 1.5 / d1;
            return n1 * t * t + 0.75;
        }
        if (t < 2.5 / d1)
        {
            t -= 2.25 / d1;
            return n1 * t * t + 0.9375;
        }
        t -= 2.625 / d1;
        return n1 * t * t + 0.984375;
    }

    private static string Normalize(string? name) => name?.Trim().ToLowerInvariant() ?? "";
}
