namespace Intuime.Core;

public sealed record BuiltInDesign(string Id, string Category, string Name);

public static class BuiltInBackgrounds
{
    public const string DefaultId = "futuristic/neon-grid";

    public static readonly BuiltInDesign[] All =
    {
        new("futuristic/neon-grid", "Futuristic", "Neon grid"),
        new("futuristic/circuit", "Futuristic", "Circuit pulse"),
        new("futuristic/hologram", "Futuristic", "Hologram rings"),
        new("futuristic/data-rain", "Futuristic", "Data rain"),
        new("space/starfield", "Space", "Starfield"),
        new("space/nebula", "Space", "Nebula"),
        new("space/planet", "Space", "Planet"),
        new("space/aurora", "Space", "Aurora"),
        new("buildings/skyline", "Buildings", "Night skyline"),
        new("buildings/towers", "Buildings", "Glass towers"),
        new("buildings/rain", "Buildings", "Rain on the skyline"),
        new("zodiac/wheel", "Zodiac", "Constellation wheel"),
        new("zodiac/signs", "Zodiac", "Twelve signs"),
        new("zodiac/chart", "Zodiac", "Star chart"),
        new("2d/blobs", "2D", "Soft blobs"),
        new("2d/waves", "2D", "Paper waves"),
        new("2d/geometry", "2D", "Flat shapes"),
        new("2d/pixel", "2D", "Pixel dusk"),
        new("nature/ocean", "Nature", "Ocean"),
        new("nature/dunes", "Nature", "Night dunes"),
        new("nature/fireflies", "Nature", "Fireflies"),
        new("light/streaks", "Light", "Light streaks"),
        new("light/silk", "Light", "Silk"),
        new("light/bokeh", "Light", "Bokeh")
    };

    public static string[] Categories()
    {
        var list = new List<string>();
        foreach (var design in All)
        {
            if (!list.Contains(design.Category))
                list.Add(design.Category);
        }
        return list.ToArray();
    }

    public static BuiltInDesign[] In(string category) =>
        All.Where(item => string.Equals(item.Category, category, StringComparison.Ordinal)).ToArray();

    public static BuiltInDesign? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var trimmed = id.Trim();
        foreach (var design in All)
        {
            if (string.Equals(design.Id, trimmed, StringComparison.OrdinalIgnoreCase))
                return design;
        }
        return null;
    }

    public static bool Known(string? id) => Find(id) != null;

    public static string Canonical(string? id) => Find(id)?.Id ?? "";

    public static double Phase(double seconds, double period)
    {
        if (period <= 0)
            return 0;
        var t = seconds % period;
        if (t < 0)
            t += period;
        return t / period;
    }
}
