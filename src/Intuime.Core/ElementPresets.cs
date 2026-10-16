namespace Intuime.Core;

public sealed class ElementPreset
{
    public ElementPreset(string name, string sample, Action<SceneElement> apply)
    {
        Name = name;
        Sample = sample;
        Apply = apply;
    }

    public string Name { get; }
    public string Sample { get; }
    public Action<SceneElement> Apply { get; }
}

public static class ElementPresets
{
    public static IReadOnlyList<ElementPreset> For(ElementKind kind) => kind switch
    {
        ElementKind.Text => TextPresets,
        ElementKind.Image => ImagePresets,
        ElementKind.Rectangle => RectanglePresets,
        ElementKind.Ellipse => EllipsePresets,
        ElementKind.Button => ButtonPresets,
        ElementKind.Hotspot => HotspotPresets,
        ElementKind.Clock => ClockPresets,
        ElementKind.WebLink => WebLinkPresets,
        ElementKind.Video => VideoPresets,
        ElementKind.Audio => AudioPresets,
        ElementKind.DataList => DataListPresets,
        ElementKind.Counter => CounterPresets,
        _ => Array.Empty<ElementPreset>()
    };

    public static SceneElement Create(ElementKind kind, ElementPreset preset, double x, double y, int z, int index)
    {
        var element = ElementFactory.Create(kind, x, y, z, index);
        preset.Apply(element);
        element.Name = preset.Name;
        return element;
    }

    private static readonly ElementPreset[] ButtonPresets =
    {
        Button("Amber", "Continue", "#E7A23A", "#1A1204", 16, 300, 76, shadow: true),
        Button("White pill", "Begin", "#F4F7FB", "#121820", 38, 240, 68, shadow: true),
        Button("Dark", "Learn more", "#163246", "#F4F7FB", 12, 280, 72, shadow: true),
        Button("Teal", "Next", "#1F6F8B", "#F4F7FB", 18, 260, 72, shadow: true),
        Button("Outline", "Details", "#00000000", "#E7A23A", 14, 240, 68, stroke: "#E7A23A"),
        Button("Close", "Close", "#C4524A", "#FFFFFF", 14, 160, 60, shadow: true),
        Button("Ghost", "Skip", "#00000000", "#D5E0EA", 10, 160, 56),
        Button("Plain", "Button", "#E7A23A", "#1A1204", 14, 320, 84, shadow: true)
    };

    private static readonly ElementPreset[] TextPresets =
    {
        Text("Display", "Welcome", 72, "Bold", "#F4F7FB", 760, 110),
        Text("Title", "Title", 48, "Bold", "#F4F7FB", 640, 80),
        Text("Subtitle", "A short line under the title", 28, "Normal", "#8EA0B5", 680, 56),
        Text("Body", "Write a sentence or two here. It stays left aligned and easy to read.", 24, "Normal", "#D5E0EA", 640, 140),
        Text("Caption", "Caption", 16, "Normal", "#8EA0B5", 420, 36),
        Text("Accent", "Highlighted", 36, "Bold", "#E7A23A", 520, 64),
        Text("Quote", "A quiet line.", 32, "Normal", "#F4F7FB", 560, 72, italic: true),
        Text("Plain", "Text", 40, "Bold", "#F4F7FB", 520, 80)
    };

    private static readonly ElementPreset[] RectanglePresets =
    {
        Shape("Card", "#121C28", 420, 240, 20, shadow: true),
        Shape("Soft panel", "#1B2C3A", 480, 200, 24, shadow: true),
        Shape("Amber block", "#E7A23A", 360, 140, 16),
        Shape("Teal block", "#1F6F8B", 360, 160, 18),
        Shape("Bar", "#E7A23A", 720, 14, 7),
        Shape("Frame", "#00000000", 420, 260, 16, stroke: "#E7A23A"),
        Shape("Chip", "#163246", 220, 56, 28),
        Shape("Plain", "#1F6F8B", 360, 200, 16)
    };

    private static readonly ElementPreset[] EllipsePresets =
    {
        Shape("Gold circle", "#E7A23A", 180, 180, 0),
        Shape("Teal circle", "#1F6F8B", 160, 160, 0),
        Shape("Soft dot", "#F4F7FB", 48, 48, 0),
        Shape("Ring", "#00000000", 180, 180, 0, stroke: "#F4F7FB", thickness: 6),
        Shape("Avatar", "#163246", 140, 140, 0, stroke: "#E7A23A", thickness: 4),
        Shape("Plain", "#E7A23A", 220, 220, 0)
    };

    private static readonly ElementPreset[] ImagePresets =
    {
        Shape("Rounded photo", "#10202C", 520, 340, 18),
        Shape("Circle crop", "#10202C", 280, 280, 140),
        Shape("Banner", "#10202C", 860, 260, 8),
        Shape("Portrait", "#10202C", 360, 480, 16),
        Shape("Polaroid", "#E7EEF6", 360, 420, 6, stroke: "#D5E0EA"),
        Shape("Plain", "#10202C", 480, 320, 12)
    };

    private static readonly ElementPreset[] HotspotPresets =
    {
        Shape("Wide tap", "#00000000", 640, 180, 8),
        Shape("Square tap", "#00000000", 240, 240, 8),
        Shape("Small target", "#00000000", 120, 120, 8),
        Shape("Plain", "#00000000", 280, 180, 0)
    };

    private static readonly ElementPreset[] ClockPresets =
    {
        Clock("Big time", "HH:mm", 72, 320, 100, "#00000000", "#F4F7FB"),
        Clock("With seconds", "HH:mm:ss", 36, 280, 64, "#00000000", "#F4F7FB"),
        Clock("Date", "ddd d MMM", 28, 320, 56, "#00000000", "#8EA0B5"),
        Clock("Badge", "HH:mm", 32, 180, 64, "#163246", "#E7A23A", 16),
        Clock("Plain", "HH:mm:ss", 36, 280, 64, "#00000000", "#F4F7FB")
    };

    private static readonly ElementPreset[] WebLinkPresets =
    {
        Link("Text link", "Visit the site", "#00000000", "#8FD0FF", 0, 280, 48),
        Link("Amber link", "Open", "#E7A23A", "#1A1204", 14, 200, 64),
        Link("Quiet link", "More information", "#142433", "#D5E0EA", 12, 320, 64),
        Link("Plain", "Open the website", "#142433", "#8FD0FF", 12, 460, 72)
    };

    private static readonly ElementPreset[] VideoPresets =
    {
        Shape("Cinema", "#071018", 800, 450, 16, stroke: "#2A3544"),
        Shape("Portrait film", "#071018", 360, 640, 16, stroke: "#2A3544"),
        Shape("Rounded", "#071018", 480, 480, 28, stroke: "#2A3544"),
        Shape("Wide", "#071018", 960, 400, 8, stroke: "#2A3544"),
        Shape("Plain", "#071018", 640, 360, 12, stroke: "#2A3544")
    };

    private static readonly ElementPreset[] AudioPresets =
    {
        Audio("Dark bar", "Room tone", "#142433", 480, 84, 12),
        Audio("Pill", "Voice note", "#163246", 360, 64, 32),
        Audio("Amber chip", "Play sound", "#E7A23A", 280, 64, 16, "#1A1204"),
        Audio("Plain", "Audio", "#142433", 480, 84, 12)
    };

    private static readonly ElementPreset[] DataListPresets =
    {
        List("Dark menu", "Morning tour\nAfternoon talk\nEvening hours", "#121C28", "#F4F7FB", 26, 420, 240, 16),
        List("Compact", "One\nTwo\nThree\nFour", "#0E1822", "#D5E0EA", 20, 320, 200, 10),
        List("Large rows", "First choice\nSecond choice\nThird choice", "#163246", "#F4F7FB", 32, 520, 280, 18),
        List("Plain", "First choice\nSecond choice\nThird choice", "#121C28", "#F4F7FB", 24, 460, 280, 12)
    };

    private static readonly ElementPreset[] CounterPresets =
    {
        Counter("Dark stepper", "#142433", "#F4F7FB", 16, 320, 96),
        Counter("Amber count", "#E7A23A", "#1A1204", 24, 280, 88),
        Counter("Minimal", "#00000000", "#F4F7FB", 8, 240, 80),
        Counter("Plain", "#142433", "#F4F7FB", 16, 360, 96)
    };

    private static ElementPreset Button(string name, string sample, string fill, string foreground, double radius, double width, double height, bool shadow = false, string? stroke = null) =>
        new(name, sample, element =>
        {
            element.Text = sample;
            element.Fill = fill;
            element.Foreground = foreground;
            element.CornerRadius = radius;
            element.Width = width;
            element.Height = height;
            element.FontWeight = "Bold";
            element.FontSize = height >= 72 ? 26 : 22;
            element.TextAlign = "Center";
            element.VerticalAlign = "Center";
            element.HasShadow = shadow;
            element.Stroke = stroke ?? "#00000000";
            element.StrokeThickness = stroke == null ? 0 : 2;
        });

    private static ElementPreset Text(string name, string sample, double size, string weight, string foreground, double width, double height, bool italic = false) =>
        new(name, sample, element =>
        {
            element.Text = sample;
            element.FontSize = size;
            element.FontWeight = weight;
            element.Italic = italic;
            element.Foreground = foreground;
            element.Fill = "#00000000";
            element.Width = width;
            element.Height = height;
            element.TextAlign = "Left";
            element.HasShadow = false;
        });

    private static ElementPreset Shape(string name, string fill, double width, double height, double radius, bool shadow = false, string? stroke = null, double thickness = 2) =>
        new(name, name, element =>
        {
            element.Fill = fill;
            element.Width = width;
            element.Height = height;
            element.CornerRadius = radius;
            element.HasShadow = shadow;
            element.Stroke = stroke ?? "#00000000";
            element.StrokeThickness = stroke == null ? 0 : thickness;
        });

    private static ElementPreset Clock(string name, string format, double size, double width, double height, string fill, string foreground, double radius = 0) =>
        new(name, format, element =>
        {
            element.ClockFormat = format;
            element.FontSize = size;
            element.FontWeight = "Bold";
            element.Width = width;
            element.Height = height;
            element.Fill = fill;
            element.Foreground = foreground;
            element.CornerRadius = radius;
            element.TextAlign = fill == "#00000000" ? "Left" : "Center";
        });

    private static ElementPreset Link(string name, string sample, string fill, string foreground, double radius, double width, double height) =>
        new(name, sample, element =>
        {
            element.Text = sample;
            element.Fill = fill;
            element.Foreground = foreground;
            element.CornerRadius = radius;
            element.Width = width;
            element.Height = height;
            element.FontWeight = fill == "#00000000" ? "Normal" : "Bold";
            element.FontSize = 22;
            element.Url = "https://example.com";
        });

    private static ElementPreset Audio(string name, string sample, string fill, double width, double height, double radius, string foreground = "#F4F7FB") =>
        new(name, sample, element =>
        {
            element.Text = sample;
            element.Fill = fill;
            element.Foreground = foreground;
            element.Width = width;
            element.Height = height;
            element.CornerRadius = radius;
            element.FontSize = 22;
        });

    private static ElementPreset List(string name, string rows, string fill, string foreground, double size, double width, double height, double radius) =>
        new(name, rows, element =>
        {
            element.Text = rows;
            element.Fill = fill;
            element.Foreground = foreground;
            element.FontSize = size;
            element.Width = width;
            element.Height = height;
            element.CornerRadius = radius;
            element.TextAlign = "Left";
        });

    private static ElementPreset Counter(string name, string fill, string foreground, double radius, double width, double height) =>
        new(name, name, element =>
        {
            element.Fill = fill;
            element.Foreground = foreground;
            element.CornerRadius = radius;
            element.Width = width;
            element.Height = height;
            element.FontWeight = "Bold";
            element.FontSize = 36;
        });
}
