namespace Intuime.Core;

public sealed record BuiltInIcon(string Id, string Category, string Name, string Spec = "");

public static class BuiltInIcons
{
    public const string DefaultId = "media/play";

    public static readonly BuiltInIcon[] All =
    {
        new("media/play", "Media", "Play"),
        new("media/pause", "Media", "Pause"),
        new("media/stop", "Media", "Stop"),
        new("media/previous", "Media", "Previous"),
        new("media/next", "Media", "Next"),
        new("media/rewind", "Media", "Rewind"),
        new("media/forward", "Media", "Fast forward"),
        new("media/record", "Media", "Record"),
        new("media/volume", "Media", "Volume"),
        new("media/mute", "Media", "Mute"),
        new("media/music", "Media", "Music"),
        new("media/microphone", "Media", "Microphone"),
        new("media/camera", "Media", "Camera"),
        new("media/headphones", "Media", "Headphones"),
        new("media/repeat", "Media", "Repeat"),
        new("media/shuffle", "Media", "Shuffle"),

        new("smiles/smile", "Smiles", "Smile"),
        new("smiles/grin", "Smiles", "Grin"),
        new("smiles/sad", "Smiles", "Sad"),
        new("smiles/cry", "Smiles", "Cry"),
        new("smiles/wink", "Smiles", "Wink"),
        new("smiles/love", "Smiles", "Love"),
        new("smiles/cool", "Smiles", "Cool"),
        new("smiles/wow", "Smiles", "Wow"),
        new("smiles/angry", "Smiles", "Angry"),
        new("smiles/think", "Smiles", "Thinking"),
        new("smiles/neutral", "Smiles", "Neutral"),
        new("smiles/sleepy", "Smiles", "Sleepy"),
        new("smiles/kiss", "Smiles", "Kiss"),
        new("smiles/party", "Smiles", "Party"),
        new("smiles/starry", "Smiles", "Star eyes"),
        new("smiles/worried", "Smiles", "Worried"),

        new("flags/united-states", "Flags", "United States", "us"),
        new("flags/united-kingdom", "Flags", "United Kingdom", "uk"),
        new("flags/france", "Flags", "France", "v:#0055A4,#FFFFFF,#EF4135"),
        new("flags/germany", "Flags", "Germany", "h:#000000,#DD0000,#FFCE00"),
        new("flags/italy", "Flags", "Italy", "v:#009246,#FFFFFF,#CE2B37"),
        new("flags/spain", "Flags", "Spain", "h:1:#AA151B,2:#F1BF00,1:#AA151B"),
        new("flags/japan", "Flags", "Japan", "disc:#FFFFFF,#BC002D"),
        new("flags/china", "Flags", "China", "cn"),
        new("flags/canada", "Flags", "Canada", "ca"),
        new("flags/brazil", "Flags", "Brazil", "br"),
        new("flags/india", "Flags", "India", "in"),
        new("flags/russia", "Flags", "Russia", "h:#FFFFFF,#0039A6,#D52B1E"),
        new("flags/netherlands", "Flags", "Netherlands", "h:#AE1C28,#FFFFFF,#21468B"),
        new("flags/ireland", "Flags", "Ireland", "v:#169B62,#FFFFFF,#FF883E"),
        new("flags/sweden", "Flags", "Sweden", "nordic:#006AA7,#FECC00"),
        new("flags/norway", "Flags", "Norway", "nordic2:#BA0C2F,#FFFFFF,#00205B"),
        new("flags/denmark", "Flags", "Denmark", "nordic:#C60C30,#FFFFFF"),
        new("flags/finland", "Flags", "Finland", "nordic:#FFFFFF,#003580"),
        new("flags/iceland", "Flags", "Iceland", "nordic2:#02529C,#FFFFFF,#DC1E35"),
        new("flags/switzerland", "Flags", "Switzerland", "plus:#DA291C,#FFFFFF"),
        new("flags/england", "Flags", "England", "cross:#FFFFFF,#CF142B"),
        new("flags/scotland", "Flags", "Scotland", "saltire:#005EB8,#FFFFFF"),
        new("flags/greece", "Flags", "Greece", "gr"),
        new("flags/turkey", "Flags", "Turkey", "crescent:#E30A17,#FFFFFF"),
        new("flags/vietnam", "Flags", "Vietnam", "star:#DA251D,#FFCD00"),
        new("flags/indonesia", "Flags", "Indonesia", "h:#FF0000,#FFFFFF"),
        new("flags/poland", "Flags", "Poland", "h:#FFFFFF,#DC143C"),
        new("flags/ukraine", "Flags", "Ukraine", "h:#0057B7,#FFD700"),
        new("flags/belgium", "Flags", "Belgium", "v:#000000,#FAE042,#ED2939"),
        new("flags/romania", "Flags", "Romania", "v:#002B7F,#FCD116,#CE1126"),
        new("flags/mexico", "Flags", "Mexico", "v:#006847,#FFFFFF,#CE1126"),
        new("flags/argentina", "Flags", "Argentina", "h:#74ACDF,#FFFFFF,#74ACDF"),
        new("flags/nigeria", "Flags", "Nigeria", "v:#008751,#FFFFFF,#008751"),
        new("flags/bangladesh", "Flags", "Bangladesh", "discl:#006A4E,#F42A41"),
        new("flags/pakistan", "Flags", "Pakistan", "pk"),
        new("flags/israel", "Flags", "Israel", "il"),
        new("flags/uae", "Flags", "United Arab Emirates", "uae"),
        new("flags/thailand", "Flags", "Thailand", "h:1:#A51931,1:#F4F5F8,2:#2D2A4A,1:#F4F5F8,1:#A51931"),
        new("flags/european-union", "Flags", "European Union", "eu"),
        new("flags/rainbow", "Flags", "Rainbow", "rain"),
        new("flags/egypt", "Flags", "Egypt", "h:#CE1126,#FFFFFF,#000000"),
        new("flags/morocco", "Flags", "Morocco", "star:#C1272D,#006233"),
        new("flags/palestine", "Flags", "Palestine", "tri:#000000,#FFFFFF,#007A3D,#CE1126"),
        new("flags/chile", "Flags", "Chile", "cl"),
        new("flags/cuba", "Flags", "Cuba", "cu"),
        new("flags/greenland", "Flags", "Greenland", "gl"),
        new("flags/colombia", "Flags", "Colombia", "h:2:#FCD116,1:#003893,1:#CE1126"),
        new("flags/peru", "Flags", "Peru", "v:#D91023,#FFFFFF,#D91023"),
        new("flags/hungary", "Flags", "Hungary", "h:#CE2939,#FFFFFF,#477050"),
        new("flags/austria", "Flags", "Austria", "h:#ED2939,#FFFFFF,#ED2939"),
        new("flags/bulgaria", "Flags", "Bulgaria", "h:#FFFFFF,#00966E,#D62612"),
        new("flags/ethiopia", "Flags", "Ethiopia", "h:#078930,#FCDD09,#DA121A"),
        new("flags/ghana", "Flags", "Ghana", "gh"),
        new("flags/portugal", "Flags", "Portugal", "pt"),
        new("flags/south-korea", "Flags", "South Korea", "kr"),

        new("arrows/left", "Arrows", "Left"),
        new("arrows/right", "Arrows", "Right"),
        new("arrows/up", "Arrows", "Up"),
        new("arrows/down", "Arrows", "Down"),
        new("arrows/refresh", "Arrows", "Refresh"),
        new("arrows/undo", "Arrows", "Undo"),
        new("arrows/redo", "Arrows", "Redo"),
        new("arrows/expand", "Arrows", "Expand"),
        new("arrows/back", "Arrows", "Back"),
        new("arrows/out", "Arrows", "Leave"),

        new("symbols/check", "Symbols", "Check"),
        new("symbols/close", "Symbols", "Close"),
        new("symbols/plus", "Symbols", "Plus"),
        new("symbols/minus", "Symbols", "Minus"),
        new("symbols/search", "Symbols", "Search"),
        new("symbols/info", "Symbols", "Info"),
        new("symbols/warning", "Symbols", "Warning"),
        new("symbols/question", "Symbols", "Question"),
        new("symbols/star", "Symbols", "Star"),
        new("symbols/heart", "Symbols", "Heart"),
        new("symbols/gear", "Symbols", "Settings"),
        new("symbols/lock", "Symbols", "Lock"),
        new("symbols/home", "Symbols", "Home"),
        new("symbols/pin", "Symbols", "Pin"),

        new("objects/mail", "Objects", "Mail"),
        new("objects/phone", "Objects", "Phone"),
        new("objects/cart", "Objects", "Cart"),
        new("objects/clock", "Objects", "Clock"),
        new("objects/calendar", "Objects", "Calendar"),
        new("objects/trash", "Objects", "Trash"),
        new("objects/share", "Objects", "Share"),
        new("objects/download", "Objects", "Download"),
        new("objects/upload", "Objects", "Upload"),
        new("objects/wifi", "Objects", "Wi-Fi"),
        new("objects/battery", "Objects", "Battery"),
        new("objects/user", "Objects", "Person")
    };

    public static string[] Categories()
    {
        var list = new List<string>();
        foreach (var icon in All)
        {
            if (!list.Contains(icon.Category))
                list.Add(icon.Category);
        }
        return list.ToArray();
    }

    public static BuiltInIcon[] In(string category) =>
        All.Where(item => string.Equals(item.Category, category, StringComparison.Ordinal)).ToArray();

    public static BuiltInIcon? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var trimmed = id.Trim();
        foreach (var icon in All)
        {
            if (string.Equals(icon.Id, trimmed, StringComparison.OrdinalIgnoreCase))
                return icon;
        }
        return null;
    }

    public static bool Known(string? id) => Find(id) != null;

    public static string Canonical(string? id) => Find(id)?.Id ?? "";

    public static string Family(BuiltInIcon icon) => icon.Category switch
    {
        "Flags" => "flag",
        "Smiles" => "smile",
        _ => "plate"
    };

    public static void Style(SceneElement element, BuiltInIcon icon)
    {
        element.Kind = ElementKind.Icon;
        element.Icon = icon.Id;
        element.Text = "";
        if (icon.Category == "Flags")
        {
            element.Width = 168;
            element.Height = 112;
            element.Fill = "#00000000";
            element.Foreground = "#F4F7FB";
            element.CornerRadius = 4;
            element.Stroke = "#2A3544";
            element.StrokeThickness = 1;
            element.HasShadow = false;
            return;
        }

        element.Width = 112;
        element.Height = 112;
        element.CornerRadius = icon.Category == "Smiles" ? 12 : 28;
        element.Fill = icon.Category == "Smiles" ? "#00000000" : "#163246";
        element.Foreground = "#F4F7FB";
        element.Stroke = "#00000000";
        element.StrokeThickness = 0;
        element.HasShadow = false;
    }
}
