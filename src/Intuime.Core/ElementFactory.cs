namespace Intuime.Core;

public static class ElementFactory
{
    public static readonly (ElementKind Kind, string Label, string Blurb)[] Catalog =
    {
        (ElementKind.Text, "Text", "Titles, captions, and bound values"),
        (ElementKind.Image, "Image", "PNG, JPG, GIF, or WEBP"),
        (ElementKind.Rectangle, "Rectangle", "Panels, cards, and bars"),
        (ElementKind.Ellipse, "Ellipse", "Circles and dots"),
        (ElementKind.Button, "Button", "A labeled tap target"),
        (ElementKind.Hotspot, "Hotspot", "An invisible tap region"),
        (ElementKind.Clock, "Clock", "Live time on the player"),
        (ElementKind.WebLink, "Web link", "Shows the page inside the kiosk"),
        (ElementKind.Video, "Video", "Plays a video file in the player"),
        (ElementKind.Audio, "Audio", "Plays a sound on the player"),
        (ElementKind.DataList, "Data list", "Tap a row to set a variable"),
        (ElementKind.Counter, "Counter", "Plus and minus for a variable")
    };

    public static SceneElement Create(ElementKind kind, double x, double y, int z, int index)
    {
        var element = new SceneElement
        {
            Id = Ids.New(),
            Name = $"{Labels.Element(kind)} {index}",
            Kind = kind,
            X = x,
            Y = y,
            Z = z,
            Fill = "#163246",
            Foreground = "#F4F7FB",
            FontSize = 28,
            TextAlign = "Center",
            VerticalAlign = "Center"
        };

        switch (kind)
        {
            case ElementKind.Text:
                element.Width = 520;
                element.Height = 80;
                element.Text = "Text";
                element.FontSize = 40;
                element.FontWeight = "Bold";
                element.TextAlign = "Left";
                element.Fill = "#00000000";
                break;
            case ElementKind.Image:
                element.Width = 480;
                element.Height = 320;
                element.CornerRadius = 12;
                element.Fill = "#10202C";
                break;
            case ElementKind.Rectangle:
                element.Width = 360;
                element.Height = 200;
                element.CornerRadius = 16;
                element.Fill = "#1F6F8B";
                break;
            case ElementKind.Ellipse:
                element.Width = 220;
                element.Height = 220;
                element.Fill = "#E7A23A";
                break;
            case ElementKind.Button:
                element.Width = 320;
                element.Height = 84;
                element.Text = "Button";
                element.FontSize = 28;
                element.FontWeight = "Bold";
                element.Fill = "#E7A23A";
                element.Foreground = "#1A1204";
                element.CornerRadius = 14;
                element.HasShadow = true;
                break;
            case ElementKind.Hotspot:
                element.Width = 280;
                element.Height = 180;
                element.Fill = "#00000000";
                element.Name = $"Hotspot {index}";
                break;
            case ElementKind.Clock:
                element.Width = 280;
                element.Height = 64;
                element.FontSize = 36;
                element.FontWeight = "Bold";
                element.ClockFormat = "HH:mm:ss";
                element.Fill = "#00000000";
                element.TextAlign = "Left";
                break;
            case ElementKind.WebLink:
                element.Width = 460;
                element.Height = 72;
                element.Text = "Open the website";
                element.Url = "https://example.com";
                element.Fill = "#142433";
                element.Foreground = "#8FD0FF";
                element.CornerRadius = 12;
                element.FontSize = 24;
                break;
            case ElementKind.Video:
                element.Width = 640;
                element.Height = 360;
                element.Fill = "#071018";
                element.CornerRadius = 12;
                element.Stroke = "#2A3544";
                element.StrokeThickness = 2;
                break;
            case ElementKind.Audio:
                element.Width = 480;
                element.Height = 84;
                element.Text = "Audio";
                element.Fill = "#142433";
                element.CornerRadius = 12;
                element.FontSize = 22;
                break;
            case ElementKind.DataList:
                element.Width = 460;
                element.Height = 280;
                element.Text = "First choice\nSecond choice\nThird choice";
                element.Fill = "#121C28";
                element.Foreground = "#F4F7FB";
                element.CornerRadius = 12;
                element.FontSize = 24;
                element.TextAlign = "Left";
                element.VariableBinding = "item";
                break;
            case ElementKind.Counter:
                element.Width = 360;
                element.Height = 96;
                element.Fill = "#142433";
                element.Foreground = "#F4F7FB";
                element.CornerRadius = 16;
                element.FontSize = 40;
                element.FontWeight = "Bold";
                element.VariableBinding = "count";
                element.Step = 1;
                element.Min = 0;
                element.Max = 10;
                element.HasRange = true;
                break;
            case ElementKind.Icon:
                var icon = BuiltInIcons.Find(BuiltInIcons.DefaultId)!;
                element.Name = $"Icon {index}";
                BuiltInIcons.Style(element, icon);
                break;
        }

        return element;
    }

    public static bool CapturesPointer(SceneElement element)
    {
        if (element.Kind is ElementKind.Button or ElementKind.Hotspot or ElementKind.WebLink
            or ElementKind.DataList or ElementKind.Counter or ElementKind.Video or ElementKind.Audio
            or ElementKind.Icon)
            return true;
        if (!string.IsNullOrWhiteSpace(element.Url))
            return true;
        if (TapEffects.Known(element.TapEffect))
            return true;
        return element.Interactions.Any(i =>
            i.Trigger is TriggerKind.Tap or TriggerKind.DoubleTap or TriggerKind.Press or TriggerKind.Release);
    }
}
