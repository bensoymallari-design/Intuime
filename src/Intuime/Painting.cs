using Avalonia.Media;

namespace Intuime;

public static class Painting
{
    public const string Ink = "#0E1218";
    public const string Panel = "#121820";
    public const string Line = "#2A3544";
    public const string Text = "#E7EEF6";
    public const string Muted = "#8EA0B5";
    public const string Accent = "#E7A23A";

    public static readonly string[] Swatches =
    {
        "#E7A23A", "#F4F7FB", "#081018", "#163246", "#1F6F8B", "#3DDC97",
        "#69C3FF", "#E15B64", "#D5E0EA", "#9BB0C3", "#FFFFFF", "#00000000"
    };

    public static IBrush Brush(string? hex, string fallback = "#FFFFFFFF")
    {
        return new SolidColorBrush(Parse(hex, fallback));
    }

    public static Color Parse(string? hex, string fallback = "#FFFFFFFF")
    {
        foreach (var candidate in new[] { hex, fallback, "#FFFFFFFF" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            try
            {
                return Color.Parse(candidate);
            }
            catch (FormatException)
            {
            }
        }
        return Colors.White;
    }

    public static FontWeight Weight(string? value) => value?.ToLowerInvariant() switch
    {
        "bold" => FontWeight.Bold,
        "semibold" => FontWeight.SemiBold,
        _ => FontWeight.Normal
    };

    public static TextAlignment Align(string? value) => value switch
    {
        "Center" => TextAlignment.Center,
        "Right" => TextAlignment.Right,
        _ => TextAlignment.Left
    };

    public static Avalonia.Layout.VerticalAlignment VAlign(string? value) => value switch
    {
        "Top" => Avalonia.Layout.VerticalAlignment.Top,
        "Bottom" => Avalonia.Layout.VerticalAlignment.Bottom,
        _ => Avalonia.Layout.VerticalAlignment.Center
    };

    public static Stretch StretchOf(string? value) => value switch
    {
        "Fill" => Stretch.Fill,
        "UniformToFill" => Stretch.UniformToFill,
        "None" => Stretch.None,
        _ => Stretch.Uniform
    };
}
