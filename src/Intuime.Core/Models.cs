using System.Text.Json.Serialization;

namespace Intuime.Core;

public enum ElementKind
{
    Text,
    Image,
    Rectangle,
    Ellipse,
    Button,
    Hotspot,
    Clock,
    WebLink,
    Video,
    Audio,
    DataList,
    Counter
}

public enum TriggerKind
{
    Tap,
    DoubleTap,
    Press,
    Release,
    SceneEnter,
    SceneLeave,
    Timer,
    Key
}

public enum ActionKind
{
    GoToScene,
    NextScene,
    PreviousScene,
    Show,
    Hide,
    ToggleVisibility,
    SetText,
    SetOpacity,
    MoveTo,
    Resize,
    BringToFront,
    SendToBack,
    SetFill,
    PlayMedia,
    PauseMedia,
    StopMedia,
    OpenUrl,
    SetBackground,
    SetVariable,
    Wait,
    RestartExperience,
    ExitPlayer,
    PlayEffect
}

public enum MediaTransport
{
    Play,
    Pause,
    Stop
}

public sealed class ExperienceDocument
{
    public int FileVersion { get; set; } = 1;
    public string Name { get; set; } = "Untitled experience";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public string? StartSceneId { get; set; }
    public List<ExperienceVariable> Variables { get; set; } = new();
    public List<Interaction> Interactions { get; set; } = new();
    public List<Scene> Scenes { get; set; } = new();
}

public sealed class ExperienceVariable
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class Scene
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Scene";
    public string Background { get; set; } = "#101820";
    public string BackgroundImage { get; set; } = "";
    public string BackgroundVideo { get; set; } = "";
    public bool BackgroundVideoLoop { get; set; } = true;
    public bool BackgroundVideoSound { get; set; }
    public List<SceneElement> Elements { get; set; } = new();
    public List<Interaction> Interactions { get; set; } = new();
}

public sealed class SceneElement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Element";
    public ElementKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 80;
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public int Z { get; set; }
    public string Fill { get; set; } = "#1F6F8B";
    public string Stroke { get; set; } = "#00000000";
    public double StrokeThickness { get; set; }
    public double CornerRadius { get; set; }
    public string Text { get; set; } = "";
    public double FontSize { get; set; } = 28;
    public string FontWeight { get; set; } = "Normal";
    public bool Italic { get; set; }
    public string Foreground { get; set; } = "#FFFFFF";
    public string TextAlign { get; set; } = "Left";
    public string VerticalAlign { get; set; } = "Center";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public string Stretch { get; set; } = "Uniform";
    public string ClockFormat { get; set; } = "HH:mm";
    public string VariableBinding { get; set; } = "";
    public double Step { get; set; } = 1;
    public double Min { get; set; }
    public double Max { get; set; } = 10;
    public bool HasRange { get; set; }
    public bool Autoplay { get; set; }
    public bool Loop { get; set; }
    public bool ShowControls { get; set; } = true;
    public bool HasShadow { get; set; }
    public string TapEffect { get; set; } = "";
    public double TapStrength { get; set; } = 1;
    public string Entrance { get; set; } = "";
    public int EntranceDelayMs { get; set; }
    public int EntranceMs { get; set; } = EntranceEffects.DefaultDurationMs;
    public List<Interaction> Interactions { get; set; } = new();

    [JsonIgnore]
    public bool IsPlaying { get; set; }

    [JsonIgnore]
    public double EffectScale { get; set; } = 1;

    [JsonIgnore]
    public double EffectScaleX { get; set; } = 1;

    [JsonIgnore]
    public double EffectScaleY { get; set; } = 1;

    [JsonIgnore]
    public double EffectOpacity { get; set; } = 1;

    [JsonIgnore]
    public double EffectRotation { get; set; }

    [JsonIgnore]
    public double EffectShiftX { get; set; }

    [JsonIgnore]
    public double EffectShiftY { get; set; }

    [JsonIgnore]
    public string? EffectFill { get; set; }
}

public sealed class Interaction
{
    public string Id { get; set; } = "";
    public TriggerKind Trigger { get; set; } = TriggerKind.Tap;
    public string Key { get; set; } = "";
    public int TimerMs { get; set; } = 5000;
    public bool TimerRepeat { get; set; }
    public bool ResetOnActivity { get; set; } = true;
    public List<ExperienceAction> Actions { get; set; } = new();
}

public sealed class ExperienceAction
{
    public ActionKind Kind { get; set; }
    public string TargetId { get; set; } = "";
    public string Text { get; set; } = "";
    public string VariableName { get; set; } = "";
    public string VariableValue { get; set; } = "";
    public string IfVariable { get; set; } = "";
    public string IfEquals { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Number { get; set; }
    public int DurationMs { get; set; }
}

public readonly record struct MediaRequest(string ElementId, MediaTransport Transport);

public static class PlayBarTargets
{
    public const string Prefix = "playbar:";

    public static string For(string videoId) => Prefix + videoId;

    public static bool TryGetVideoId(string? targetId, out string videoId)
    {
        if (!string.IsNullOrEmpty(targetId) &&
            targetId.StartsWith(Prefix, StringComparison.Ordinal) &&
            targetId.Length > Prefix.Length)
        {
            videoId = targetId[Prefix.Length..];
            return true;
        }

        videoId = "";
        return false;
    }
}

public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N")[..12];
}

public static class Labels
{
    public static string Trigger(TriggerKind trigger) => trigger switch
    {
        TriggerKind.Tap => "Is tapped",
        TriggerKind.DoubleTap => "Is double-tapped",
        TriggerKind.Press => "Is pressed",
        TriggerKind.Release => "Is released",
        TriggerKind.SceneEnter => "Scene is entered",
        TriggerKind.SceneLeave => "Scene is left",
        TriggerKind.Timer => "Timer elapses",
        TriggerKind.Key => "Key is pressed",
        _ => trigger.ToString()
    };

    public static string Action(ActionKind kind) => kind switch
    {
        ActionKind.GoToScene => "Go to scene",
        ActionKind.NextScene => "Next scene",
        ActionKind.PreviousScene => "Previous scene",
        ActionKind.Show => "Show",
        ActionKind.Hide => "Hide",
        ActionKind.ToggleVisibility => "Toggle visibility",
        ActionKind.SetText => "Set text",
        ActionKind.SetOpacity => "Set opacity",
        ActionKind.MoveTo => "Move to",
        ActionKind.Resize => "Resize",
        ActionKind.BringToFront => "Bring to front",
        ActionKind.SendToBack => "Send to back",
        ActionKind.SetFill => "Set fill",
        ActionKind.PlayMedia => "Play media",
        ActionKind.PauseMedia => "Pause media",
        ActionKind.StopMedia => "Stop media",
        ActionKind.OpenUrl => "Open web page",
        ActionKind.SetBackground => "Set background",
        ActionKind.SetVariable => "Set variable",
        ActionKind.Wait => "Wait",
        ActionKind.RestartExperience => "Restart experience",
        ActionKind.ExitPlayer => "Exit player",
        ActionKind.PlayEffect => "Play effect",
        _ => kind.ToString()
    };

    public static string Element(ElementKind kind) => kind switch
    {
        ElementKind.WebLink => "Web link",
        ElementKind.DataList => "Data list",
        _ => kind.ToString()
    };
}
