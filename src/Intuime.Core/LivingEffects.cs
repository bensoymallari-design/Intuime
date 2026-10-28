namespace Intuime.Core;

public static class LivingEffects
{
    public static readonly string[] All =
    {
        "Bounce",
        "Float",
        "Breathe",
        "Sway",
        "Tilt",
        "Wave",
        "Wobble"
    };

    public readonly record struct Pose(
        double Scale,
        double ScaleX,
        double ScaleY,
        double Rotation,
        double ShiftX,
        double ShiftY,
        double Wave)
    {
        public static Pose Still { get; } = new(1, 1, 1, 0, 0, 0, 0);
    }

    public static bool Known(string? name) =>
        All.Any(effect => string.Equals(effect, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsWave(string? name) =>
        string.Equals(Normalize(name), "wave", StringComparison.Ordinal);

    public static double PeriodSeconds(string? name) => Normalize(name) switch
    {
        "bounce" => 0.9,
        "float" => 2.6,
        "breathe" => 1.8,
        "sway" => 2.2,
        "tilt" => 2.8,
        "wave" => 1.6,
        "wobble" => 1.4,
        _ => 1
    };

    public static Pose At(string? name, double seconds, double strength)
    {
        if (!Known(name))
            return Pose.Still;
        var amount = Math.Clamp(strength <= 0 ? 1 : strength, 0.25, 3);
        var period = PeriodSeconds(name);
        var turns = seconds / period;
        var t = turns - Math.Floor(turns);
        var sine = Math.Sin(t * Math.Tau);
        var hop = Math.Abs(sine);
        return Normalize(name) switch
        {
            "bounce" => new Pose(1, 1, 1 - 0.08 * hop * amount, 0, 0, -22 * hop * amount, 0),
            "float" => new Pose(1, 1, 1, 0, 0, -18 * sine * amount, 0),
            "breathe" => new Pose(1 + 0.07 * sine * amount, 1, 1, 0, 0, 0, 0),
            "sway" => new Pose(1, 1, 1, 9 * sine * amount, 0, 0, 0),
            "tilt" => new Pose(1, 1 + 0.14 * sine * amount, 1, 7 * sine * amount, 14 * sine * amount, 0, 0),
            "wave" => new Pose(1 + 0.025 * sine, 1, 1, 0, 0, 0, t),
            "wobble" => new Pose(
                1,
                1 + 0.1 * sine * amount,
                1 - 0.08 * sine * amount,
                8 * Math.Sin(t * Math.Tau * 2) * amount,
                0,
                -8 * Math.Cos(t * Math.Tau) * amount,
                0),
            _ => Pose.Still
        };
    }

    public static double HighlightScale(double seconds)
    {
        var wave = Math.Sin(seconds * Math.Tau / 1.4);
        return 1.08 + 0.03 * wave;
    }

    private static string Normalize(string? name) => name?.Trim().ToLowerInvariant() ?? "";
}
