namespace Intuime.Core;

public static class SampleExperiences
{
    public static ExperienceDocument CreateBlank(string name, int width, int height)
    {
        var scene = new Scene
        {
            Id = Ids.New(),
            Name = "Scene 1",
            Background = "#101820"
        };
        var title = ElementFactory.Create(ElementKind.Text, 80, 72, 0, 1);
        title.Text = string.IsNullOrWhiteSpace(name) ? "Untitled" : name.Trim();
        title.Name = "Title";
        scene.Elements.Add(title);
        var document = new ExperienceDocument
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled experience" : name.Trim(),
            Width = width,
            Height = height,
            StartSceneId = scene.Id,
            Scenes = { scene }
        };
        ProjectStore.EnsureIds(document);
        return document;
    }

    public static ExperienceDocument CreateLumenGallery()
    {
        var document = new ExperienceDocument
        {
            Name = "Lumen Gallery",
            Width = 1920,
            Height = 1080,
            StartSceneId = "attract",
            Variables =
            {
                new() { Name = "partySize", Value = "1" },
                new() { Name = "talk", Value = "none yet" },
                new() { Name = "selectedRoom", Value = "" },
                new() { Name = "note", Value = "Tap reserve to hold tickets." }
            },
            Interactions =
            {
                Key("experience-next", "Right", Next()),
                Key("experience-prev", "Left", Previous())
            }
        };

        document.Scenes.Add(Attract());
        document.Scenes.Add(Home());
        document.Scenes.Add(Collection());
        document.Scenes.Add(Exhibit("aurora", "Aurora Glass",
            "Molten color, cooled into a sheet you can stand inside. The studio film beside this label is the eight-second loop shipped with Intuime.",
            "assets/aurora.png", "harbor", includeFilm: true));
        document.Scenes.Add(Exhibit("harbor", "Harbor Light",
            "A tide chart rendered as glass. Step back until the horizon line sits at eye level, then play the room tone.",
            "assets/harbor.png", "orchard", includeFilm: false));
        document.Scenes.Add(Exhibit("orchard", "Night Orchard",
            "Blossoms counted by hand, then blown up until each one is a lamp. This is the last work on the loop.",
            "assets/orchard.png", "collection", includeFilm: false));
        document.Scenes.Add(Hours());
        document.Scenes.Add(Map());
        document.Scenes.Add(Visit());
        ProjectStore.EnsureIds(document);
        return document;
    }

    private static Scene Attract()
    {
        var scene = NewScene("attract", "Attract", "#081018");
        scene.Interactions.Add(On(TriggerKind.Tap, "attract-tap", Go("home")));
        scene.Elements.Add(Rect("attract-bar", "Accent", 0, 0, 1920, 8, "#E7A23A"));
        scene.Elements.Add(new SceneElement
        {
            Id = "attract-glow",
            Name = "Glow",
            Kind = ElementKind.Ellipse,
            X = 660,
            Y = 250,
            Width = 600,
            Height = 460,
            Fill = "#14384A",
            Opacity = 0.95,
            Z = 0
        });
        scene.Elements.Add(Label("attract-kicker", "Kicker", 0, 250, 1920, 36, "LUMEN GALLERY", 20, "#E7A23A", center: true));
        scene.Elements.Add(Label("attract-title", "Title", 140, 310, 1640, 170, "Come closer.", 84, "#F4F7FB", center: true, bold: true));
        scene.Elements.Add(Label("attract-sub", "Subtitle", 300, 500, 1320, 100,
            "A touchscreen tour of the collection.\nTap anywhere to begin.", 30, "#B7C6D6", center: true));
        var begin = Button("begin", "Begin", 760, 660, 400, 88, "Begin");
        begin.Interactions.Add(On(TriggerKind.Tap, "begin-tap", Go("home")));
        scene.Elements.Add(begin);
        scene.Elements.Add(Clock("attract-clock", 72, 990, 320, 52, 28));
        scene.Elements.Add(Label("attract-keys", "Keys", 1180, 990, 668, 48,
            "Arrow keys move between scenes", 18, "#8EA0B5", center: false));
        return scene;
    }

    private static Scene Home()
    {
        var scene = NewScene("home", "Home", "#081018");
        Idle(scene);
        scene.Elements.Add(Label("home-kicker", "Kicker", 72, 48, 700, 28, "LUMEN GALLERY", 16, "#E7A23A"));
        scene.Elements.Add(Label("home-title", "Title", 72, 88, 1100, 72, "Where to?", 56, "#F4F7FB", bold: true));
        scene.Elements.Add(Label("home-sub", "Subtitle", 72, 168, 800, 40, "Touch a path to walk into it.", 22, "#9BB0C3"));
        scene.Elements.Add(Clock("home-clock", 1500, 56, 348, 52, 28));
        Card(scene, "collection", "The collection", "Three works, one room at a time.", 72, 250, "#143044", "collection");
        Card(scene, "hours", "Today's hours", "The clock, the talks, the room tone.", 988, 250, "#1C2A22", "hours");
        Card(scene, "map", "Floor map", "Tap a room, then step inside.", 72, 630, "#2A2418", "map");
        Card(scene, "visit", "Plan a visit", "Count the group and hold tickets.", 988, 630, "#2A1C28", "visit");
        return scene;
    }

    private static Scene Collection()
    {
        var scene = NewScene("collection", "Collection", "#081018");
        Idle(scene);
        Header(scene, "The collection", "home");
        Work(scene, "aurora", "Aurora Glass", "assets/aurora.png", 72);
        Work(scene, "harbor", "Harbor Light", "assets/harbor.png", 688);
        Work(scene, "orchard", "Night Orchard", "assets/orchard.png", 1304);
        return scene;
    }

    private static Scene Exhibit(string id, string title, string body, string image, string next, bool includeFilm)
    {
        var scene = NewScene(id, title, "#081018");
        Idle(scene);
        Header(scene, title, "collection");
        var art = Image($"{id}-art", title, 72, 150, 860, 820, image);
        scene.Elements.Add(art);
        scene.Elements.Add(Label($"{id}-body", "Body", 980, 170, 860, 220, body, 26, "#D5E0EA"));
        var audio = Audio($"{id}-audio", "Room tone", 980, 420, 520, 84, "assets/room-tone.wav");
        scene.Elements.Add(audio);
        var play = Button($"{id}-play-audio", "Play tone", 980, 530, 250, 68, "Play tone");
        play.Interactions.Add(On(TriggerKind.Tap, $"{id}-play-audio-tap",
            new ExperienceAction { Kind = ActionKind.PlayMedia, TargetId = audio.Id }));
        scene.Elements.Add(play);
        var nextButton = Button($"{id}-next", "Next", 1260, 530, 250, 68, id == "orchard" ? "All works" : "Next work");
        nextButton.Fill = "#163246";
        nextButton.Foreground = "#F4F7FB";
        nextButton.Interactions.Add(On(TriggerKind.Tap, $"{id}-next-tap", Go(next)));
        scene.Elements.Add(nextButton);
        if (includeFilm)
        {
            var film = Video($"{id}-film", "Studio film", 980, 640, 760, 280, "assets/studio.mp4");
            scene.Elements.Add(film);
            var playFilm = Button($"{id}-play-film", "Play film", 980, 940, 200, 64, "Play film");
            playFilm.Interactions.Add(On(TriggerKind.Tap, $"{id}-play-film-tap",
                new ExperienceAction { Kind = ActionKind.PlayMedia, TargetId = film.Id }));
            scene.Elements.Add(playFilm);
            var pause = Button($"{id}-pause-film", "Pause film", 1200, 940, 200, 64, "Pause");
            pause.Fill = "#163246";
            pause.Foreground = "#F4F7FB";
            pause.Interactions.Add(On(TriggerKind.Tap, $"{id}-pause-film-tap",
                new ExperienceAction { Kind = ActionKind.PauseMedia, TargetId = film.Id }));
            scene.Elements.Add(pause);
        }
        return scene;
    }

    private static Scene Hours()
    {
        var scene = NewScene("hours", "Hours", "#081018");
        Idle(scene);
        Header(scene, "Today's hours", "home");
        scene.Elements.Add(Clock("hours-clock", 72, 180, 760, 140, 84));
        scene.Elements.Add(Label("hours-copy", "Hours", 72, 360, 760, 280,
            "Tuesday – Thursday   11:00 – 18:00\nFriday   11:00 – 21:00\nSaturday – Sunday   10:00 – 18:00\nMonday   closed\n\nThe building chime plays on the hour.",
            28, "#D5E0EA"));
        var list = new SceneElement
        {
            Id = "talks",
            Name = "Talks",
            Kind = ElementKind.DataList,
            X = 980,
            Y = 180,
            Width = 860,
            Height = 320,
            Z = 4,
            Fill = "#121C28",
            Foreground = "#F4F7FB",
            CornerRadius = 16,
            FontSize = 26,
            TextAlign = "Left",
            VariableBinding = "talk",
            Text = "10:30  Glassblowing demo\n13:00  Gallery talk\n15:30  Family tour"
        };
        scene.Elements.Add(list);
        scene.Elements.Add(Label("talk-selection", "Selected talk", 980, 530, 860, 80,
            "Selected talk: {{talk}}", 28, "#E7A23A"));
        var tone = Audio("hours-audio", "Chime", 980, 650, 520, 84, "assets/room-tone.wav");
        scene.Elements.Add(tone);
        var play = Button("hours-play", "Play chime", 980, 760, 260, 68, "Play chime");
        play.Interactions.Add(On(TriggerKind.Tap, "hours-play-tap",
            new ExperienceAction { Kind = ActionKind.PlayMedia, TargetId = tone.Id }));
        scene.Elements.Add(play);
        return scene;
    }

    private static Scene Map()
    {
        var scene = NewScene("map", "Map", "#081018");
        Idle(scene);
        Header(scene, "Floor map", "home");
        scene.Elements.Add(Rect("map-floor", "Floor", 72, 170, 1080, 820, "#10202C", radius: 20));
        Room(scene, "aurora", "Aurora Glass", 120, 220, 440, 300, "#1F6F8B");
        Room(scene, "harbor", "Harbor Light", 600, 220, 480, 300, "#245C73");
        Room(scene, "orchard", "Night Orchard", 120, 560, 960, 360, "#3E2A55");
        scene.Elements.Add(Rect("map-panel", "Panel", 1200, 170, 648, 820, "#121C28", radius: 20));
        scene.Elements.Add(Label("map-detail", "Detail", 1240, 210, 568, 360,
            "Tap a room on the plan.\nThe enter button uses the room you picked.", 28, "#D5E0EA"));
        var enter = Button("map-enter", "Enter", 1240, 820, 400, 80, "Enter room");
        enter.Interactions.Add(On(TriggerKind.Tap, "map-enter-tap",
            GoIf("aurora", "selectedRoom", "Aurora Glass"),
            GoIf("harbor", "selectedRoom", "Harbor Light"),
            GoIf("orchard", "selectedRoom", "Night Orchard")));
        scene.Elements.Add(enter);
        return scene;
    }

    private static Scene Visit()
    {
        var scene = NewScene("visit", "Visit", "#081018");
        Idle(scene);
        Header(scene, "Plan a visit", "home");
        scene.Elements.Add(Label("visit-lead", "Lead", 72, 170, 760, 50, "How many are coming?", 28, "#D5E0EA"));
        scene.Elements.Add(new SceneElement
        {
            Id = "party-counter",
            Name = "Group size",
            Kind = ElementKind.Counter,
            X = 72,
            Y = 240,
            Width = 460,
            Height = 110,
            Z = 3,
            Fill = "#142433",
            Foreground = "#F4F7FB",
            CornerRadius = 16,
            FontSize = 42,
            FontWeight = "Bold",
            VariableBinding = "partySize",
            Step = 1,
            Min = 1,
            Max = 12,
            HasRange = true
        });
        scene.Elements.Add(Label("party-readout", "Readout", 72, 380, 760, 70, "Tickets held: {{partySize}}", 36, "#F4F7FB", bold: true));
        var reserve = Button("reserve", "Reserve", 72, 490, 300, 76, "Reserve");
        reserve.Interactions.Add(On(TriggerKind.Tap, "reserve-tap", new ExperienceAction
        {
            Kind = ActionKind.SetVariable,
            VariableName = "note",
            VariableValue = "Held for {{partySize}} guests. Pick them up at the desk."
        }));
        scene.Elements.Add(reserve);
        scene.Elements.Add(Label("visit-note", "Note", 72, 600, 820, 120, "{{note}}", 26, "#E7A23A"));
        scene.Elements.Add(new SceneElement
        {
            Id = "visit-web",
            Name = "Website",
            Kind = ElementKind.WebLink,
            X = 72,
            Y = 760,
            Width = 560,
            Height = 76,
            Z = 6,
            Text = "lumen.gallery",
            Url = "https://example.com",
            Fill = "#142433",
            Foreground = "#8FD0FF",
            CornerRadius = 12,
            FontSize = 26,
            TextAlign = "Center",
            VerticalAlign = "Center"
        });
        scene.Elements.Add(Label("visit-address", "Address", 980, 240, 820, 360,
            "12 Quay Street\nNorth Harbor\nOpen from the hours board\n\nWheelchairs enter on the quay side.\nThe player returns to the attract scene after a quiet minute.",
            28, "#D5E0EA"));
        return scene;
    }

    private static void Card(Scene scene, string id, string title, string body, double x, double y, string fill, string target)
    {
        scene.Elements.Add(Rect($"card-{id}-bg", title, x, y, 860, 340, fill, radius: 24));
        scene.Elements.Add(Label($"card-{id}-title", title, x + 36, y + 48, 780, 70, title, 40, "#F4F7FB", bold: true));
        scene.Elements.Add(Label($"card-{id}-body", body, x + 36, y + 140, 760, 80, body, 24, "#C5D4E2"));
        var hotspot = Hotspot($"card-{id}", title, x, y, 860, 340);
        hotspot.Interactions.Add(On(TriggerKind.Tap, $"card-{id}-tap", Go(target)));
        scene.Elements.Add(hotspot);
    }

    private static void Work(Scene scene, string id, string title, string image, double x)
    {
        scene.Elements.Add(Image($"work-{id}-img", title, x, 170, 544, 460, image));
        scene.Elements.Add(Label($"work-{id}-caption", title, x, 650, 544, 50, title, 28, "#F4F7FB", bold: true));
        var hotspot = Hotspot($"work-{id}", title, x, 170, 544, 540);
        hotspot.Interactions.Add(On(TriggerKind.Tap, $"work-{id}-tap", Go(id)));
        scene.Elements.Add(hotspot);
    }

    private static void Room(Scene scene, string id, string title, double x, double y, double w, double h, string fill)
    {
        scene.Elements.Add(Rect($"room-{id}-bg", title, x, y, w, h, fill, radius: 16));
        scene.Elements.Add(Label($"room-{id}-label", title, x + 24, y + h / 2 - 24, w - 48, 48, title, 28, "#F4F7FB", bold: true));
        var hotspot = Hotspot($"room-{id}", title, x, y, w, h);
        hotspot.Interactions.Add(On(TriggerKind.Tap, $"room-{id}-tap",
            new ExperienceAction { Kind = ActionKind.SetVariable, VariableName = "selectedRoom", VariableValue = title },
            new ExperienceAction { Kind = ActionKind.SetText, TargetId = "map-detail", Text = title + "\n\nTap Enter room to walk in." }));
        scene.Elements.Add(hotspot);
    }

    private static void Header(Scene scene, string title, string backTarget)
    {
        var back = Button($"{scene.Id}-back", "Back", 72, 36, 180, 64, "Back");
        back.Fill = "#163246";
        back.Foreground = "#F4F7FB";
        back.Interactions.Add(On(TriggerKind.Tap, $"{scene.Id}-back-tap", Go(backTarget)));
        scene.Elements.Add(back);
        scene.Elements.Add(Label($"{scene.Id}-title", "Title", 280, 36, 1100, 64, title, 40, "#F4F7FB", bold: true));
        scene.Elements.Add(Clock($"{scene.Id}-clock", 1500, 40, 348, 52, 26));
    }

    private static Scene NewScene(string id, string name, string background) => new()
    {
        Id = id,
        Name = name,
        Background = background
    };

    private static void Idle(Scene scene)
    {
        scene.Interactions.Add(new Interaction
        {
            Id = scene.Id + "-idle",
            Trigger = TriggerKind.Timer,
            TimerMs = 60000,
            TimerRepeat = true,
            ResetOnActivity = true,
            Actions = { Go("attract") }
        });
    }

    private static SceneElement Rect(string id, string name, double x, double y, double w, double h, string fill, double radius = 0) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Rectangle,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Fill = fill,
        CornerRadius = radius,
        Z = 0
    };

    private static SceneElement Label(string id, string name, double x, double y, double w, double h, string text, double size, string color, bool center = false, bool bold = false) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Text,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Text = text,
        FontSize = size,
        FontWeight = bold ? "Bold" : "Normal",
        Foreground = color,
        Fill = "#00000000",
        TextAlign = center ? "Center" : "Left",
        VerticalAlign = "Center",
        Z = 1
    };

    private static SceneElement Button(string id, string name, double x, double y, double w, double h, string text) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Button,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Text = text,
        FontSize = 26,
        FontWeight = "Bold",
        Fill = "#E7A23A",
        Foreground = "#1A1204",
        CornerRadius = 14,
        TextAlign = "Center",
        VerticalAlign = "Center",
        HasShadow = true,
        Z = 2
    };

    private static SceneElement Hotspot(string id, string name, double x, double y, double w, double h) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Hotspot,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Fill = "#00000000",
        Z = 5
    };

    private static SceneElement Image(string id, string name, double x, double y, double w, double h, string source) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Image,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Source = source,
        Fill = "#10202C",
        CornerRadius = 18,
        Stretch = "UniformToFill",
        Z = 1
    };

    private static SceneElement Video(string id, string name, double x, double y, double w, double h, string source) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Video,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Source = source,
        Fill = "#071018",
        CornerRadius = 12,
        Stroke = "#2A3544",
        StrokeThickness = 2,
        Z = 2
    };

    private static SceneElement Audio(string id, string name, double x, double y, double w, double h, string source) => new()
    {
        Id = id,
        Name = name,
        Kind = ElementKind.Audio,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        Text = name,
        Source = source,
        Fill = "#142433",
        Foreground = "#F4F7FB",
        CornerRadius = 12,
        FontSize = 22,
        TextAlign = "Center",
        VerticalAlign = "Center",
        Z = 2
    };

    private static SceneElement Clock(string id, double x, double y, double w, double h, double size) => new()
    {
        Id = id,
        Name = "Clock",
        Kind = ElementKind.Clock,
        X = x,
        Y = y,
        Width = w,
        Height = h,
        FontSize = size,
        FontWeight = "Bold",
        Foreground = "#F4F7FB",
        Fill = "#00000000",
        ClockFormat = "HH:mm",
        TextAlign = "Right",
        VerticalAlign = "Center",
        Z = 2
    };

    private static Interaction On(TriggerKind trigger, string id, params ExperienceAction[] actions) => new()
    {
        Id = id,
        Trigger = trigger,
        Actions = actions.ToList()
    };

    private static Interaction Key(string id, string key, ExperienceAction action) => new()
    {
        Id = id,
        Trigger = TriggerKind.Key,
        Key = key,
        Actions = { action }
    };

    private static ExperienceAction Go(string scene) => new() { Kind = ActionKind.GoToScene, TargetId = scene };

    private static ExperienceAction GoIf(string scene, string variable, string equals) => new()
    {
        Kind = ActionKind.GoToScene,
        TargetId = scene,
        IfVariable = variable,
        IfEquals = equals
    };

    private static ExperienceAction Next() => new() { Kind = ActionKind.NextScene };

    private static ExperienceAction Previous() => new() { Kind = ActionKind.PreviousScene };
}
