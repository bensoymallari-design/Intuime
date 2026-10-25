namespace Intuime.Core;

public sealed class PlaybackEngine
{
    private readonly Func<DateTime> _clock;
    private readonly List<ActionSequence> _sequences = new();
    private readonly Dictionary<string, DateTime> _timerArmed = new();
    private readonly HashSet<string> _timerOnce = new();
    private readonly List<Tween> _tweens = new();
    private readonly List<RunningEffect> _effects = new();
    private readonly List<RunningEffect> _entrances = new();
    private string? _lastTapId;
    private DateTime _lastTapAt;
    private int _depth;
    private int _epoch;
    private int _deferDepth;
    private DeferredNav _deferredNav;
    private string? _deferredSceneId;
    private bool _deferredLeave;
    private readonly Dictionary<int, string> _showing = new();
    private int _focusScreen;
    private bool _suppressOscEcho;

    public PlaybackEngine(ExperienceDocument original, Func<DateTime>? clock = null)
    {
        Original = ProjectStore.Clone(original);
        Live = ProjectStore.Clone(original);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public ExperienceDocument Original { get; }
    public ExperienceDocument Live { get; }
    public Scene? Current => SceneFor(_focusScreen);

    public Scene? SceneFor(int screen) =>
        _showing.TryGetValue(screen, out var id) ? Live.Scenes.FirstOrDefault(sceneItem => sceneItem.Id == id) : null;

    public IEnumerable<int> OpenScreens => _showing.Keys.OrderBy(screen => screen);

    public event Action<int>? ScreenChanged;
    public bool ExitRequested { get; private set; }

    public event Action? Changed;
    public event Action? SceneChanged;
    public event Action<string>? OpenUrlRequested;
    public event Action? ExitRequestedEvent;
    public event Action<MediaRequest>? MediaCommand;
    public event Action<OscMessage>? OscSend;

    public void Start()
    {
        var screens = Live.Scenes.Select(scene => Math.Max(0, scene.Screen)).Distinct().OrderBy(screen => screen == 0 ? 1 : 0).ThenBy(screen => screen).ToList();
        foreach (var screen in screens)
        {
            var start = DisplayScreens.StartId(Live, screen);
            if (!string.IsNullOrEmpty(start))
                Enter(start, runLeave: false);
        }
    }

    public void Tick()
    {
        var now = _clock();
        var animated = AdvanceTweens(now) || AdvanceEffects(now) || AdvanceEntrances(now);
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
        var now = _clock();
        foreach (var scene in Showing())
        {
            foreach (var interaction in Enumerate(scene).Where(i => i.Trigger == TriggerKind.Timer && i.ResetOnActivity))
            {
                _timerArmed[interaction.Id] = now;
                _timerOnce.Remove(interaction.Id);
            }
        }
    }

    public void BackgroundTap() => BackgroundTap(null);

    public void BackgroundTap(string? sceneId)
    {
        var scene = string.IsNullOrEmpty(sceneId) ? Current : Showing().FirstOrDefault(item => item.Id == sceneId) ?? Current;
        if (scene == null)
            return;
        _focusScreen = Math.Max(0, scene.Screen);
        NotifyActivity();
        Run(scene.Interactions, TriggerKind.Tap, null, null);
        Changed?.Invoke();
    }

    public void PointerDown(string elementId)
    {
        if (!FocusElement(elementId, out var element) || !element!.IsVisible)
            return;
        NotifyActivity();
        Run(element.Interactions, TriggerKind.Press, element, null);
        Changed?.Invoke();
    }

    public void PointerUp(string elementId) => PointerUp(elementId, fromOsc: false);

    public void ReceiveOsc(OscMessage message)
    {
        var address = OscAddresses.Canonical(message.Address);
        if (address.Length <= 1)
            return;
        var released = message.Text == null && message.Number is <= 0;
        var elementIds = Showing()
            .SelectMany(scene => scene.Elements)
            .Where(element => element.IsVisible && OscAddresses.Same(element.OscAddress, address))
            .Select(element => element.Id)
            .ToList();
        var sceneIds = Live.Scenes
            .Where(scene => OscAddresses.Same(scene.OscAddress, address))
            .Select(scene => scene.Id)
            .ToList();
        if (elementIds.Count == 0 && sceneIds.Count == 0)
            return;

        _suppressOscEcho = true;
        try
        {
            foreach (var id in elementIds)
            {
                var element = Find(id);
                if (element == null || !element.IsVisible)
                    continue;
                if (message.Text != null && element.Kind is ElementKind.Text or ElementKind.Button)
                    element.Text = message.Text;
                if (message.Number != null && element.Kind == ElementKind.Counter)
                    SetCounter(element, message.Number.Value);
                if (!released)
                    PointerUp(id, fromOsc: true);
            }
            if (!released)
            {
                foreach (var id in sceneIds)
                    Enter(id, runLeave: true);
            }
        }
        finally
        {
            _suppressOscEcho = false;
        }
    }

    private void PointerUp(string elementId, bool fromOsc)
    {
        if (!FocusElement(elementId, out var element) || !element!.IsVisible)
            return;
        NotifyActivity();
        var holdNavigation = TapEffects.Known(element.TapEffect);
        if (holdNavigation)
        {
            StartEffect(element, element.TapEffect);
            _deferDepth++;
        }
        try
        {
            Run(element.Interactions, TriggerKind.Release, element, null);
            Run(element.Interactions, TriggerKind.Tap, element, null);
            var now = _clock();
            if (_lastTapId == elementId && (now - _lastTapAt).TotalMilliseconds <= 400)
                Run(element.Interactions, TriggerKind.DoubleTap, element, null);
            _lastTapId = elementId;
            _lastTapAt = now;
        }
        finally
        {
            if (holdNavigation)
                _deferDepth--;
        }
        if (!fromOsc)
            EmitOsc(element.OscAddress, number: 1, automatic: true);
        Changed?.Invoke();
    }

    public void KeyPressed(string key) => KeyPressed(key, -1);

    public void KeyPressed(string key, int screen)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;
        if (screen >= 0 && _showing.ContainsKey(screen))
            _focusScreen = screen;
        NotifyActivity();
        var scene = Current;
        if (scene != null)
        {
            Run(scene.Interactions, TriggerKind.Key, null, key);
            scene = Current;
            if (scene != null)
            {
                foreach (var element in scene.Elements.Where(e => e.IsVisible).ToList())
                    Run(element.Interactions, TriggerKind.Key, element, key);
            }
        }
        Run(Live.Interactions, TriggerKind.Key, null, key);
        Changed?.Invoke();
    }

    public void SelectDataItem(string elementId, int index)
    {
        if (!FocusElement(elementId, out var element) || !element!.IsVisible)
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
        if (!FocusElement(elementId, out var element) || !element!.IsVisible)
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
        if (HoldNavigation())
        {
            _deferredNav = DeferredNav.Enter;
            _deferredSceneId = sceneId;
            _deferredLeave = runLeave;
            return;
        }
        if (_depth > 8)
            return;
        _epoch++;
        var destination = Live.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (destination == null)
            return;

        var screen = Math.Max(0, destination.Screen);
        var leaving = SceneFor(screen);
        if (runLeave && leaving != null && leaving.Id != sceneId)
            Run(leaving.Interactions, TriggerKind.SceneLeave, null, null);

        var retired = new HashSet<string>(StringComparer.Ordinal);
        if (leaving != null)
        {
            foreach (var element in leaving.Elements)
                retired.Add(element.Id);
        }
        foreach (var element in destination.Elements)
            retired.Add(element.Id);

        Restore(sceneId);
        destination = Live.Scenes.First(s => s.Id == sceneId);
        _showing[screen] = sceneId;
        _focusScreen = screen;
        ArmTimers(destination);
        _sequences.RemoveAll(item => item.Screen == screen);
        DropMotion(retired);
        BeginEntrances(destination);
        SceneJustRaised = true;
        ScreenChanged?.Invoke(screen);
        SceneChanged?.Invoke();

        if (SceneFor(screen)?.Id != sceneId)
            return;

        foreach (var element in destination.Elements.Where(e => e.Autoplay && e.Kind is ElementKind.Video or ElementKind.Audio))
        {
            element.IsPlaying = true;
            MediaCommand?.Invoke(new MediaRequest(element.Id, MediaTransport.Play));
        }

        Run(destination.Interactions, TriggerKind.SceneEnter, null, null);
        if (SceneFor(screen)?.Id == sceneId)
        {
            Changed?.Invoke();
            EmitOsc(destination.OscAddress, number: 1, automatic: true);
        }
    }

    private void DropMotion(HashSet<string> elementIds)
    {
        if (elementIds.Count == 0)
            return;
        _tweens.RemoveAll(tween => elementIds.Contains(tween.Element.Id));
        _effects.RemoveAll(effect => elementIds.Contains(effect.Element.Id));
        _entrances.RemoveAll(effect => elementIds.Contains(effect.Element.Id));
    }

    private IEnumerable<Scene> Showing() =>
        _showing.Values.Select(id => Live.Scenes.FirstOrDefault(scene => scene.Id == id)).Where(scene => scene != null)!;

    private void Restore(string sceneId)
    {
        var pristine = Original.Scenes.First(s => s.Id == sceneId);
        var live = Live.Scenes.First(s => s.Id == sceneId);
        var copy = ProjectStore.Clone(pristine);
        live.Name = copy.Name;
        live.Background = copy.Background;
        live.BackgroundImage = copy.BackgroundImage;
        live.BackgroundVideo = copy.BackgroundVideo;
        live.BackgroundVideoLoop = copy.BackgroundVideoLoop;
        live.BackgroundVideoSound = copy.BackgroundVideoSound;
        live.BackgroundFill = copy.BackgroundFill;
        live.BackgroundX = copy.BackgroundX;
        live.BackgroundY = copy.BackgroundY;
        live.BackgroundWidth = copy.BackgroundWidth;
        live.BackgroundHeight = copy.BackgroundHeight;
        live.Backgrounds = copy.Backgrounds;
        live.OscAddress = copy.OscAddress;
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
        if (!_showing.Any())
            return false;
        var fired = false;
        foreach (var scene in Showing().ToList())
        {
            foreach (var interaction in Enumerate(scene).Where(i => i.Trigger == TriggerKind.Timer).ToList())
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
                if (Showing().All(item => item.Interactions.All(i => i.Id != interaction.Id) &&
                        item.Elements.SelectMany(e => e.Interactions).All(i => i.Id != interaction.Id)))
                    break;
            }
        }
        return fired;
    }

    private SceneElement? ContextFor(Interaction interaction)
    {
        foreach (var scene in Showing())
        {
            var element = scene.Elements.FirstOrDefault(e => e.Interactions.Any(i => i.Id == interaction.Id));
            if (element != null)
                return element;
        }
        return null;
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
        var sequence = new ActionSequence { Screen = _focusScreen };
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
        or ActionKind.PlayMedia or ActionKind.PauseMedia or ActionKind.StopMedia
        or ActionKind.PlayEffect;

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
                {
                    sequence.Actions.Clear();
                    _sequences.Remove(sequence);
                    return;
                }
                if (action.Kind == ActionKind.PlayEffect && sequence.Actions.Count > 0 && TapEffects.Known(action.Text))
                {
                    sequence.WaitUntil = _clock().AddMilliseconds(TapEffects.DurationMs(action.Text));
                    return;
                }
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
            case ActionKind.SendOsc:
                SendOsc(action);
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
            case ActionKind.PlayEffect:
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
        var owner = element == null ? null : Showing().FirstOrDefault(scene => scene.Elements.Any(item => item.Id == element.Id));
        if (element == null || owner == null)
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
                element.Z = owner.Elements.Max(e => e.Z) + 1;
                break;
            case ActionKind.SendToBack:
                element.Z = owner.Elements.Min(e => e.Z) - 1;
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
            case ActionKind.PlayEffect:
                StartEffect(element, action.Text);
                break;
        }
    }

    private void BeginEntrances(Scene scene)
    {
        var now = _clock();
        foreach (var element in scene.Elements)
        {
            if (!element.IsVisible || !EntranceEffects.Known(element.Entrance))
                continue;
            EntranceEffects.Apply(element, element.Entrance, 0);
            _entrances.Add(new RunningEffect(
                element,
                element.Entrance.Trim(),
                now.AddMilliseconds(EntranceEffects.DelayMs(element)),
                EntranceEffects.DurationMs(element)));
        }
    }

    private bool AdvanceEntrances(DateTime now)
    {
        if (_entrances.Count == 0)
            return false;
        foreach (var effect in _entrances.ToList())
        {
            var elapsed = (now - effect.Start).TotalMilliseconds;
            if (elapsed < 0)
            {
                EntranceEffects.Apply(effect.Element, effect.Name, 0);
                continue;
            }
            var t = elapsed / effect.DurationMs;
            if (t >= 1)
            {
                EntranceEffects.Reset(effect.Element);
                _entrances.Remove(effect);
                continue;
            }
            EntranceEffects.Apply(effect.Element, effect.Name, t);
        }
        return true;
    }

    private void StartEffect(SceneElement element, string? name)
    {
        if (!TapEffects.Known(name))
            return;
        _effects.RemoveAll(effect => effect.Element.Id == element.Id);
        _entrances.RemoveAll(effect => effect.Element.Id == element.Id);
        TapEffects.Reset(element);
        _effects.Add(new RunningEffect(element, name!.Trim(), _clock(), TapEffects.DurationMs(name!)));
    }

    private bool AdvanceEffects(DateTime now)
    {
        if (_effects.Count == 0)
            return false;
        foreach (var effect in _effects.ToList())
        {
            var t = (now - effect.Start).TotalMilliseconds / effect.DurationMs;
            if (t >= 1)
            {
                TapEffects.Reset(effect.Element);
                _effects.Remove(effect);
                continue;
            }
            TapEffects.Apply(effect.Element, effect.Name, t);
        }
        ReleaseDeferredNavigation();
        return true;
    }

    private bool HoldNavigation() => _deferDepth > 0 && _deferredNav == DeferredNav.None;

    private void ReleaseDeferredNavigation()
    {
        if (_effects.Count > 0 || _deferredNav == DeferredNav.None)
            return;
        var kind = _deferredNav;
        var sceneId = _deferredSceneId;
        var leave = _deferredLeave;
        _deferredNav = DeferredNav.None;
        _deferredSceneId = null;
        switch (kind)
        {
            case DeferredNav.Enter when !string.IsNullOrEmpty(sceneId):
                Enter(sceneId, leave);
                break;
            case DeferredNav.Next:
                MoveScene(1);
                break;
            case DeferredNav.Previous:
                MoveScene(-1);
                break;
            case DeferredNav.Restart:
                Restart();
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
        if (HoldNavigation())
        {
            _deferredNav = delta < 0 ? DeferredNav.Previous : DeferredNav.Next;
            return;
        }
        if (Current == null || Live.Scenes.Count == 0)
            return;
        var screen = Math.Max(0, Current.Screen);
        var scenes = Live.Scenes.Where(scene => Math.Max(0, scene.Screen) == screen).ToList();
        if (scenes.Count == 0)
            return;
        var index = scenes.FindIndex(scene => scene.Id == Current.Id);
        if (index < 0)
            index = 0;
        var next = (index + delta) % scenes.Count;
        if (next < 0)
            next += scenes.Count;
        Enter(scenes[next].Id, runLeave: true);
    }

    private void Restart()
    {
        if (HoldNavigation())
        {
            _deferredNav = DeferredNav.Restart;
            return;
        }
        Live.Variables = ProjectStore.Clone(Original).Variables;
        _showing.Clear();
        var screens = Live.Scenes.Select(scene => Math.Max(0, scene.Screen)).Distinct().OrderBy(screen => screen == 0 ? 1 : 0).ThenBy(screen => screen);
        foreach (var screen in screens)
        {
            var start = DisplayScreens.StartId(Live, screen);
            if (!string.IsNullOrEmpty(start))
                Enter(start, runLeave: false);
        }
    }

    private void SendOsc(ExperienceAction action)
    {
        var address = TextTemplates.Expand(action.Text, Live);
        var value = TextTemplates.Expand(action.VariableValue, Live);
        OscCodec.TryArgument(value, out var message, address);
        EmitOsc(message.Address, message.Number, message.Text, message.Integer, automatic: false);
    }

    private void SetCounter(SceneElement element, double next)
    {
        if (element.HasRange)
            next = Math.Clamp(next, Math.Min(element.Min, element.Max), Math.Max(element.Min, element.Max));
        TextTemplates.Set(Live, TextTemplates.BindingName(element), TextTemplates.FormatNumber(next));
    }

    private void EmitOsc(string? address, double? number = null, string? text = null, bool integer = false, bool automatic = false)
    {
        if (automatic && _suppressOscEcho)
            return;
        var canonical = OscAddresses.Canonical(address);
        if (canonical.Length <= 1)
            return;
        OscSend?.Invoke(new OscMessage
        {
            Address = canonical,
            Number = text == null ? number : null,
            Text = text,
            Integer = integer
        });
    }

    private bool FocusElement(string elementId, out SceneElement? element)
    {
        element = Find(elementId);
        if (element == null)
            return false;
        var found = element;
        var owner = Showing().FirstOrDefault(scene => scene.Elements.Any(item => item.Id == found.Id));
        if (owner != null)
            _focusScreen = Math.Max(0, owner.Screen);
        return true;
    }

    private SceneElement? Find(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var scene in Showing())
        {
            var element = scene.Elements.FirstOrDefault(e => e.Id == id);
            if (element != null)
                return element;
        }
        return null;
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
        public int Screen { get; set; }
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

    private sealed record RunningEffect(SceneElement Element, string Name, DateTime Start, int DurationMs);

    private enum DeferredNav
    {
        None,
        Enter,
        Next,
        Previous,
        Restart
    }
}
