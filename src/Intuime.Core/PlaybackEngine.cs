namespace Intuime.Core;

public sealed class PlaybackEngine
{
    private readonly Func<DateTime> _clock;
    private readonly List<ActionSequence> _sequences = new();
    private readonly Dictionary<string, DateTime> _timerArmed = new();
    private readonly HashSet<string> _timerOnce = new();
    private readonly List<Tween> _tweens = new();
    private string? _lastTapId;
    private DateTime _lastTapAt;
    private int _depth;
    private int _epoch;

    public PlaybackEngine(ExperienceDocument original, Func<DateTime>? clock = null)
    {
        Original = ProjectStore.Clone(original);
        Live = ProjectStore.Clone(original);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public ExperienceDocument Original { get; }
    public ExperienceDocument Live { get; }
    public Scene? Current { get; private set; }
    public bool ExitRequested { get; private set; }

    public event Action? Changed;
    public event Action? SceneChanged;
    public event Action<string>? OpenUrlRequested;
    public event Action? ExitRequestedEvent;
    public event Action<MediaRequest>? MediaCommand;

    public void Start()
    {
        var start = Live.Scenes.FirstOrDefault(s => s.Id == Live.StartSceneId) ?? Live.Scenes.FirstOrDefault();
        if (start == null)
            return;
        Enter(start.Id, runLeave: false);
    }

    public void Tick()
    {
        var now = _clock();
        var animated = AdvanceTweens(now);
        var waited = false;
        foreach (var sequence in _sequences.ToList())
        {
            if (sequence.WaitUntil == null || now < sequence.WaitUntil.Value)
                continue;
            var epoch = _epoch;
            sequence.WaitUntil = null;
            waited = true;
            Pump(sequence);
            if (epoch != _epoch)
                break;
        }

        var timed = FireTimers(now);
        if ((animated || waited || timed) && !SceneJustRaised)
            Changed?.Invoke();
        SceneJustRaised = false;
    }

    private bool SceneJustRaised { get; set; }

    public void NotifyActivity()
    {
        if (Current == null)
            return;
        var now = _clock();
        foreach (var interaction in Enumerate(Current).Where(i => i.Trigger == TriggerKind.Timer && i.ResetOnActivity))
        {
            _timerArmed[interaction.Id] = now;
            _timerOnce.Remove(interaction.Id);
        }
    }

    public void BackgroundTap()
    {
        if (Current == null)
            return;
        NotifyActivity();
        Run(Current.Interactions, TriggerKind.Tap, null, null);
        Changed?.Invoke();
    }

    public void PointerDown(string elementId)
    {
        var element = Find(elementId);
        if (element == null || !element.IsVisible)
            return;
        NotifyActivity();
        Run(element.Interactions, TriggerKind.Press, element, null);
        Changed?.Invoke();
    }

    public void PointerUp(string elementId)
    {
        var element = Find(elementId);
        if (element == null || !element.IsVisible)
            return;
        NotifyActivity();
        Run(element.Interactions, TriggerKind.Release, element, null);
        Run(element.Interactions, TriggerKind.Tap, element, null);
        var now = _clock();
        if (_lastTapId == elementId && (now - _lastTapAt).TotalMilliseconds <= 400)
            Run(element.Interactions, TriggerKind.DoubleTap, element, null);
        _lastTapId = elementId;
        _lastTapAt = now;
        Changed?.Invoke();
    }

    public void KeyPressed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        NotifyActivity();
        if (Current != null)
        {
            Run(Current.Interactions, TriggerKind.Key, null, key);
            if (Current != null)
            {
                foreach (var element in Current.Elements.Where(e => e.IsVisible).ToList())
                    Run(element.Interactions, TriggerKind.Key, element, key);
            }
        }
        Run(Live.Interactions, TriggerKind.Key, null, key);
        Changed?.Invoke();
    }

    public void SelectDataItem(string elementId, int index)
    {
        var element = Find(elementId);
        if (element == null || !element.IsVisible)
            return;
        var items = Lines(element.Text);
        if (index < 0 || index >= items.Count)
            return;
        NotifyActivity();
        TextTemplates.Set(Live, "item", items[index]);
        TextTemplates.Set(Live, "itemIndex", index.ToString());
        if (!string.IsNullOrWhiteSpace(element.VariableBinding))
            TextTemplates.Set(Live, element.VariableBinding, items[index]);
        Run(element.Interactions, TriggerKind.Tap, element, null);
        Changed?.Invoke();
    }

    public void RunExternal(ExperienceAction action)
    {
        RunActions(new[] { action }, null);
        Changed?.Invoke();
    }

    public void NudgeCounter(string elementId, int direction)
    {
        var element = Find(elementId);
        if (element == null || !element.IsVisible)
            return;
        NotifyActivity();
        var name = TextTemplates.BindingName(element);
        _ = double.TryParse(TextTemplates.Get(Live, name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var current);
        var step = element.Step == 0 ? 1 : element.Step;
        var next = current + direction * step;
        if (element.HasRange)
            next = Math.Clamp(next, Math.Min(element.Min, element.Max), Math.Max(element.Min, element.Max));
        TextTemplates.Set(Live, name, TextTemplates.FormatNumber(next));
        Changed?.Invoke();
    }

    private void Enter(string sceneId, bool runLeave)
    {
        if (_depth > 8)
            return;
        _epoch++;
        var destination = Live.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (destination == null)
            return;

        if (runLeave && Current != null && Current.Id != sceneId)
            Run(Current.Interactions, TriggerKind.SceneLeave, null, null);

        Restore(sceneId);
        Current = Live.Scenes.First(s => s.Id == sceneId);
        ArmTimers(Current);
        _sequences.Clear();
        _tweens.Clear();
        SceneJustRaised = true;
        SceneChanged?.Invoke();

        if (Current?.Id != sceneId)
            return;

        foreach (var element in Current.Elements.Where(e => e.Autoplay && e.Kind is ElementKind.Video or ElementKind.Audio))
        {
            element.IsPlaying = true;
            MediaCommand?.Invoke(new MediaRequest(element.Id, MediaTransport.Play));
        }

        Run(Current.Interactions, TriggerKind.SceneEnter, null, null);
        if (Current?.Id == sceneId)
            Changed?.Invoke();
    }

    private void Restore(string sceneId)
    {
        var pristine = Original.Scenes.First(s => s.Id == sceneId);
        var live = Live.Scenes.First(s => s.Id == sceneId);
        var copy = ProjectStore.Clone(pristine);
        live.Name = copy.Name;
        live.Background = copy.Background;
        live.BackgroundImage = copy.BackgroundImage;
        live.Elements = copy.Elements;
        live.Interactions = copy.Interactions;
    }

    private void ArmTimers(Scene scene)
    {
        var now = _clock();
        foreach (var interaction in Enumerate(scene).Where(i => i.Trigger == TriggerKind.Timer))
        {
            _timerArmed[interaction.Id] = now;
            _timerOnce.Remove(interaction.Id);
        }
    }

    private bool FireTimers(DateTime now)
    {
        if (Current == null)
            return false;
        var fired = false;
        foreach (var interaction in Enumerate(Current).Where(i => i.Trigger == TriggerKind.Timer).ToList())
        {
            if (!_timerArmed.TryGetValue(interaction.Id, out var armed))
                continue;
            if ((now - armed).TotalMilliseconds < interaction.TimerMs)
                continue;
            if (!interaction.TimerRepeat && !_timerOnce.Add(interaction.Id))
                continue;
            if (interaction.TimerRepeat)
                _timerArmed[interaction.Id] = now;
            RunActions(interaction.Actions, ContextFor(interaction));
            fired = true;
            if (Current == null || Current.Interactions.All(i => i.Id != interaction.Id) &&
                Current.Elements.SelectMany(e => e.Interactions).All(i => i.Id != interaction.Id))
                break;
        }
        return fired;
    }

    private SceneElement? ContextFor(Interaction interaction)
    {
        if (Current == null)
            return null;
        return Current.Elements.FirstOrDefault(e => e.Interactions.Any(i => i.Id == interaction.Id));
    }

    private void Run(List<Interaction> interactions, TriggerKind trigger, SceneElement? context, string? key)
    {
        foreach (var interaction in interactions.Where(i => i.Trigger == trigger).ToList())
        {
            if (trigger == TriggerKind.Key &&
                !string.Equals(interaction.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                continue;
            RunActions(interaction.Actions, context);
        }
    }

    private void RunActions(IEnumerable<ExperienceAction> actions, SceneElement? context)
    {
        var sequence = new ActionSequence();
        foreach (var action in actions)
            sequence.Actions.Add(Clone(action, context));
        _sequences.Add(sequence);
        Pump(sequence);
    }

    private static ExperienceAction Clone(ExperienceAction action, SceneElement? context)
    {
        var copy = new ExperienceAction
        {
            Kind = action.Kind,
            TargetId = action.TargetId,
            Text = action.Text,
            VariableName = action.VariableName,
            VariableValue = action.VariableValue,
            IfVariable = action.IfVariable,
            IfEquals = action.IfEquals,
            X = action.X,
            Y = action.Y,
            Width = action.Width,
            Height = action.Height,
            Number = action.Number,
            DurationMs = action.DurationMs
        };
        if (string.IsNullOrEmpty(copy.TargetId) && context != null && NeedsTarget(copy.Kind))
            copy.TargetId = context.Id;
        return copy;
    }

    private static bool NeedsTarget(ActionKind kind) => kind is
        ActionKind.Show or ActionKind.Hide or ActionKind.ToggleVisibility or ActionKind.SetText
        or ActionKind.SetOpacity or ActionKind.MoveTo or ActionKind.Resize
        or ActionKind.BringToFront or ActionKind.SendToBack or ActionKind.SetFill
        or ActionKind.PlayMedia or ActionKind.PauseMedia or ActionKind.StopMedia;

    private void Pump(ActionSequence sequence)
    {
        if (_depth > 12)
            return;
        var epoch = _epoch;
        _depth++;
        try
        {
            while (sequence.Actions.Count > 0 && sequence.WaitUntil == null && epoch == _epoch)
            {
                var action = sequence.Actions[0];
                sequence.Actions.RemoveAt(0);
                if (!Passes(action))
                    continue;
                if (action.Kind == ActionKind.Wait)
                {
                    sequence.WaitUntil = _clock().AddMilliseconds(Math.Max(0, action.DurationMs));
                    return;
                }
                if (Apply(action))
                    return;
            }
            if (epoch == _epoch && sequence.Actions.Count == 0 && sequence.WaitUntil == null)
                _sequences.Remove(sequence);
        }
        finally
        {
            _depth--;
        }
    }

    private bool Passes(ExperienceAction action)
    {
        if (string.IsNullOrWhiteSpace(action.IfVariable))
            return true;
        var value = TextTemplates.Get(Live, action.IfVariable);
        return string.Equals(value, action.IfEquals ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private bool Apply(ExperienceAction action)
    {
        switch (action.Kind)
        {
            case ActionKind.GoToScene:
                Enter(action.TargetId, runLeave: true);
                return true;
            case ActionKind.NextScene:
                MoveScene(1);
                return true;
            case ActionKind.PreviousScene:
                MoveScene(-1);
                return true;
            case ActionKind.RestartExperience:
                Restart();
                return true;
            case ActionKind.ExitPlayer:
                ExitRequested = true;
                ExitRequestedEvent?.Invoke();
                return true;
            case ActionKind.OpenUrl:
                if (!string.IsNullOrWhiteSpace(action.Text))
                    OpenUrlRequested?.Invoke(TextTemplates.Expand(action.Text, Live));
                return false;
            case ActionKind.SetBackground:
                if (Current != null)
                    Current.Background = string.IsNullOrWhiteSpace(action.Text) ? Current.Background : action.Text;
                return false;
            case ActionKind.SetVariable:
                TextTemplates.Set(Live, action.VariableName, TextTemplates.Expand(action.VariableValue, Live));
                return false;
            case ActionKind.Show:
            case ActionKind.Hide:
            case ActionKind.ToggleVisibility:
            case ActionKind.SetText:
            case ActionKind.SetOpacity:
            case ActionKind.MoveTo:
            case ActionKind.Resize:
            case ActionKind.BringToFront:
            case ActionKind.SendToBack:
            case ActionKind.SetFill:
            case ActionKind.PlayMedia:
            case ActionKind.PauseMedia:
            case ActionKind.StopMedia:
                ApplyElement(action);
                return false;
            default:
                return false;
        }
    }

    private void ApplyElement(ExperienceAction action)
    {
        if (PlayBarTargets.TryGetVideoId(action.TargetId, out var videoId) &&
            action.Kind is ActionKind.Show or ActionKind.Hide or ActionKind.ToggleVisibility)
        {
            var video = Find(videoId);
            if (video == null || video.Kind != ElementKind.Video)
                return;
            video.ShowControls = action.Kind switch
            {
                ActionKind.Show => true,
                ActionKind.Hide => false,
                _ => !video.ShowControls
            };
            return;
        }

        var element = Find(action.TargetId);
        if (element == null || Current == null)
            return;
        switch (action.Kind)
        {
            case ActionKind.Show:
                element.IsVisible = true;
                break;
            case ActionKind.Hide:
                element.IsVisible = false;
                break;
            case ActionKind.ToggleVisibility:
                element.IsVisible = !element.IsVisible;
                break;
            case ActionKind.SetText:
                element.Text = TextTemplates.Expand(action.Text, Live);
                break;
            case ActionKind.SetFill:
                if (!string.IsNullOrWhiteSpace(action.Text))
                    element.Fill = action.Text;
                break;
            case ActionKind.SetOpacity:
                TweenOrSet(element, action.DurationMs, opacity: NormalizeOpacity(action.Number));
                break;
            case ActionKind.MoveTo:
                TweenOrSet(element, action.DurationMs, x: action.X, y: action.Y);
                break;
            case ActionKind.Resize:
                TweenOrSet(element, action.DurationMs, width: action.Width, height: action.Height);
                break;
            case ActionKind.BringToFront:
                element.Z = Current.Elements.Max(e => e.Z) + 1;
                break;
            case ActionKind.SendToBack:
                element.Z = Current.Elements.Min(e => e.Z) - 1;
                break;
            case ActionKind.PlayMedia:
                element.IsPlaying = true;
                MediaCommand?.Invoke(new MediaRequest(element.Id, MediaTransport.Play));
                break;
            case ActionKind.PauseMedia:
                element.IsPlaying = false;
                MediaCommand?.Invoke(new MediaRequest(element.Id, MediaTransport.Pause));
                break;
            case ActionKind.StopMedia:
                element.IsPlaying = false;
                MediaCommand?.Invoke(new MediaRequest(element.Id, MediaTransport.Stop));
                break;
        }
    }

    private void TweenOrSet(SceneElement element, int durationMs, double? x = null, double? y = null,
        double? width = null, double? height = null, double? opacity = null)
    {
        if (durationMs <= 0)
        {
            if (x != null) element.X = x.Value;
            if (y != null) element.Y = y.Value;
            if (width != null) element.Width = Math.Max(8, width.Value);
            if (height != null) element.Height = Math.Max(8, height.Value);
            if (opacity != null) element.Opacity = opacity.Value;
            return;
        }

        _tweens.RemoveAll(t => t.Element.Id == element.Id);
        _tweens.Add(new Tween
        {
            Element = element,
            FromX = element.X,
            FromY = element.Y,
            FromWidth = element.Width,
            FromHeight = element.Height,
            FromOpacity = element.Opacity,
            ToX = x ?? element.X,
            ToY = y ?? element.Y,
            ToWidth = width ?? element.Width,
            ToHeight = height ?? element.Height,
            ToOpacity = opacity ?? element.Opacity,
            AnimateX = x != null,
            AnimateY = y != null,
            AnimateWidth = width != null,
            AnimateHeight = height != null,
            AnimateOpacity = opacity != null,
            Start = _clock(),
            DurationMs = durationMs
        });
    }

    private bool AdvanceTweens(DateTime now)
    {
        if (_tweens.Count == 0)
            return false;
        foreach (var tween in _tweens.ToList())
        {
            var t = Math.Clamp((now - tween.Start).TotalMilliseconds / tween.DurationMs, 0, 1);
            var eased = t * t * (3 - 2 * t);
            if (tween.AnimateX) tween.Element.X = Lerp(tween.FromX, tween.ToX, eased);
            if (tween.AnimateY) tween.Element.Y = Lerp(tween.FromY, tween.ToY, eased);
            if (tween.AnimateWidth) tween.Element.Width = Math.Max(8, Lerp(tween.FromWidth, tween.ToWidth, eased));
            if (tween.AnimateHeight) tween.Element.Height = Math.Max(8, Lerp(tween.FromHeight, tween.ToHeight, eased));
            if (tween.AnimateOpacity) tween.Element.Opacity = Lerp(tween.FromOpacity, tween.ToOpacity, eased);
            if (t >= 1)
                _tweens.Remove(tween);
        }
        return true;
    }

    private void MoveScene(int delta)
    {
        if (Current == null || Live.Scenes.Count == 0)
            return;
        var index = Live.Scenes.FindIndex(s => s.Id == Current.Id);
        if (index < 0)
            index = 0;
        var next = (index + delta) % Live.Scenes.Count;
        if (next < 0)
            next += Live.Scenes.Count;
        Enter(Live.Scenes[next].Id, runLeave: true);
    }

    private void Restart()
    {
        var start = Original.StartSceneId ?? Original.Scenes.FirstOrDefault()?.Id;
        Live.Variables = ProjectStore.Clone(Original).Variables;
        if (!string.IsNullOrEmpty(start))
            Enter(start, runLeave: false);
    }

    private SceneElement? Find(string? id)
    {
        if (Current == null || string.IsNullOrEmpty(id))
            return null;
        return Current.Elements.FirstOrDefault(e => e.Id == id);
    }

    private static IEnumerable<Interaction> Enumerate(Scene scene) =>
        scene.Interactions.Concat(scene.Elements.SelectMany(e => e.Interactions));

    private static List<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static double NormalizeOpacity(double value)
    {
        var scaled = value > 1 ? value / 100.0 : value;
        return Math.Clamp(scaled, 0, 1);
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;

    private sealed class ActionSequence
    {
        public List<ExperienceAction> Actions { get; } = new();
        public DateTime? WaitUntil { get; set; }
    }

    private sealed class Tween
    {
        public SceneElement Element { get; set; } = null!;
        public double FromX, FromY, FromWidth, FromHeight, FromOpacity;
        public double ToX, ToY, ToWidth, ToHeight, ToOpacity;
        public bool AnimateX, AnimateY, AnimateWidth, AnimateHeight, AnimateOpacity;
        public DateTime Start;
        public int DurationMs;
    }
}
