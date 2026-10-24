using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Intuime.Core;

namespace Intuime.Views;

public sealed class InspectorPanel : StackPanel
{
    private readonly Func<string, Task<string?>> _browse;
    private EditorSession? _session;
    private string _tab = "properties";
    private bool _building;

    public InspectorPanel(Func<string, Task<string?>> browse)
    {
        _browse = browse;
        Spacing = 10;
    }

    public Control? PinnedEffect { get; private set; }
    public event Action? PinnedEffectChanged;
    public event Action<string, string>? PreviewEffect;
    public event Action<string, string, int, int>? PreviewEntrance;

    public void Show(EditorSession session, string tab)
    {
        _session = session;
        _tab = tab;
        Refresh();
    }

    public void Refresh()
    {
        if (_session == null)
            return;
        _building = true;
        Children.Clear();
        PinnedEffect = null;
        if (_tab == "variables")
            BuildVariables();
        else if (_tab == "interactions")
            BuildInteractions();
        else
            BuildProperties();
        PinnedEffectChanged?.Invoke();
        Dispatcher.UIThread.Post(() => _building = false, DispatcherPriority.Background);
    }

    private void BuildProperties()
    {
        var session = _session!;
        var scene = session.ActiveScene;
        if (scene == null)
            return;
        if (session.Selection.Count > 1)
        {
            Heading($"{session.Selection.Count} elements selected");
            Note("Use the toolbar to align them, or select one element to edit it.");
            return;
        }

        var element = session.Selection.FirstOrDefault();
        if (element == null)
        {
            Heading(scene.Name);
            Text("Scene name", scene.Name, value => session.RenameActiveScene(value), structure: true);
            ColorField("Background", scene.Background, value =>
            {
                scene.Background = value;
                session.TouchLive();
            });
            BuildBackgroundLayers(scene, session);
            var screenNames = DisplayScreens.Names;
            Choice("Play on", screenNames, DisplayScreens.Name(Math.Clamp(scene.Screen, 0, screenNames.Length - 1)), value =>
            {
                var next = DisplayScreens.IndexOf(value);
                if (next == Math.Max(0, scene.Screen))
                    return;
                var wasStart = DisplayScreens.IsStart(session.Document, scene);
                scene.Screen = next;
                if (wasStart)
                    session.SetScreenStart(scene);
                else
                    session.Commit();
                Refresh();
            });
            var start = new CheckBox { Content = "Starting scene for this screen", IsChecked = DisplayScreens.IsStart(session.Document, scene) };
            start.IsCheckedChanged += (_, _) =>
            {
                if (_building || start.IsChecked != true)
                    return;
                session.SetScreenStart(scene);
                Refresh();
            };
            Children.Add(start);
            Note("Main screen is this computer. Extended screen is the second monitor. A button that goes to a scene plays it on the screen chosen for that scene, and the other screen stays where it is. Mark a starting scene for the extended screen so it is not empty when you press Play.");
            Heading("Experience");
            Text("Name", session.Document.Name, value =>
            {
                session.Document.Name = string.IsNullOrWhiteSpace(value) ? session.Document.Name : value.Trim();
                session.TouchLive();
            }, structure: true);
            Number("Width", session.Document.Width, value => session.Document.Width = (int)Math.Clamp(value, 320, 7680));
            Number("Height", session.Document.Height, value => session.Document.Height = (int)Math.Clamp(value, 240, 4320));
            return;
        }

        Heading(element.Name);
        Note(Labels.Element(element.Kind));
        PinnedEffect = TapEffectPin(element);
        Text("Name", element.Name, value => element.Name = string.IsNullOrWhiteSpace(value) ? element.Name : value.Trim(), structure: true);
        Number("X", element.X, value => element.X = value);
        Number("Y", element.Y, value => element.Y = value);
        Number("Width", element.Width, value => element.Width = Math.Max(8, value));
        Number("Height", element.Height, value => element.Height = Math.Max(8, value));
        Number("Rotation", element.Rotation, value => element.Rotation = value);
        SliderField("Opacity", element.Opacity, value => element.Opacity = value);
        Check("Visible", element.IsVisible, value => element.IsVisible = value);
        Check("Locked", element.IsLocked, value => element.IsLocked = value);
        ColorField("Fill", element.Fill, value => element.Fill = value);
        if (element.Kind is not ElementKind.Ellipse)
        {
            ColorField("Stroke", element.Stroke, value => element.Stroke = value);
            Number("Stroke", element.StrokeThickness, value => element.StrokeThickness = Math.Max(0, value));
            Number("Corner", element.CornerRadius, value => element.CornerRadius = Math.Max(0, value));
        }
        if (ShowsText(element.Kind))
        {
            Multiline(element.Kind == ElementKind.DataList ? "Rows" : "Text", element.Text, value => element.Text = value);
            ColorField("Text color", element.Foreground, value => element.Foreground = value);
            Number("Font size", element.FontSize, value => element.FontSize = Math.Max(8, value));
            Check("Bold", element.FontWeight == "Bold", value => element.FontWeight = value ? "Bold" : "Normal");
            Check("Italic", element.Italic, value => element.Italic = value);
            Choice("Align", new[] { "Left", "Center", "Right" }, element.TextAlign, value => element.TextAlign = value);
        }
        if (element.Kind == ElementKind.Image)
            Choice("Fit", new[] { "Uniform", "UniformToFill", "Fill" }, element.Stretch, value => element.Stretch = value);
        if (element.Kind is ElementKind.Image or ElementKind.Video or ElementKind.Audio)
        {
            Text("File", element.Source, value => element.Source = value);
            Browse("Browse", async () =>
            {
                var kind = element.Kind == ElementKind.Image ? "image" : element.Kind == ElementKind.Video ? "video" : "audio";
                var path = await _browse(kind);
                if (path == null)
                    return;
                element.Source = path;
                session.Commit();
            });
        }
        if (element.Kind is ElementKind.Video or ElementKind.Audio)
        {
            Check("Autoplay", element.Autoplay, value => element.Autoplay = value);
            Check("Loop", element.Loop, value => element.Loop = value);
        }
        if (element.Kind == ElementKind.Video)
        {
            Check("Play bar", element.ShowControls, value => element.ShowControls = value);
            Note($"Turn this off to start with the bar hidden. In Show, Hide, or Toggle, choose \"{element.Name} play bar\" to reveal it on the video, including the time.");
        }
        if (element.Kind == ElementKind.WebLink)
        {
            Text("URL", element.Url, value => element.Url = value);
            Note("In Play, this page stays inside Intuime and fills the scene behind your other buttons. A Back button, or any other button, still opens the scene you chose. There is no close, minimize, or maximize button.");
        }
        if (element.Kind == ElementKind.Clock)
            Text("Format", element.ClockFormat, value => element.ClockFormat = value);
        if (element.Kind is ElementKind.Text or ElementKind.Button or ElementKind.DataList or ElementKind.Counter)
            Text("Variable", element.VariableBinding, value => element.VariableBinding = value.Trim());
        if (element.Kind == ElementKind.Counter)
        {
            Number("Step", element.Step, value => element.Step = value);
            Check("Limit range", element.HasRange, value => element.HasRange = value);
            Number("Min", element.Min, value => element.Min = value);
            Number("Max", element.Max, value => element.Max = value);
        }
        Check("Shadow", element.HasShadow, value => element.HasShadow = value);
    }

    private Control TapEffectPin(SceneElement element)
    {
        var effectNames = new[] { "None" }.Concat(TapEffects.All).ToArray();
        var currentEffect = TapEffects.All.FirstOrDefault(name =>
            string.Equals(name, element.TapEffect, StringComparison.OrdinalIgnoreCase)) ?? "None";
        var panel = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(12, 0, 12, 8) };
        panel.Children.Add(ChoiceControl("When tapped", effectNames, currentEffect, value => value, value =>
        {
            element.TapEffect = value == "None" ? "" : value;
            _session?.Commit();
            if (TapEffects.Known(element.TapEffect))
                PreviewEffect?.Invoke(element.Id, element.TapEffect);
        }));
        panel.Children.Add(new TextBlock
        {
            Text = "How strong",
            Foreground = Painting.Brush(Painting.Muted),
            FontSize = 12
        });
        var strength = new Slider
        {
            Minimum = 0.4,
            Maximum = 2.2,
            Value = element.TapStrength <= 0 ? 1 : Math.Clamp(element.TapStrength, 0.4, 2.2)
        };
        strength.ValueChanged += (_, _) =>
        {
            if (_building)
                return;
            element.TapStrength = strength.Value;
        };
        strength.PointerReleased += (_, _) =>
        {
            if (_building)
                return;
            element.TapStrength = strength.Value;
            _session?.Commit();
            if (TapEffects.Known(element.TapEffect))
                PreviewEffect?.Invoke(element.Id, element.TapEffect);
        };
        panel.Children.Add(strength);
        panel.Children.Add(new TextBlock
        {
            Text = "Pick a motion. Drag How strong to make it bigger or smaller. The button plays it here, and again when someone taps it in Play.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Painting.Brush(Painting.Muted),
            FontSize = 12
        });
        var entranceNames = new[] { "None" }.Concat(EntranceEffects.All).ToArray();
        var currentEntrance = EntranceEffects.All.FirstOrDefault(name =>
            string.Equals(name, element.Entrance, StringComparison.OrdinalIgnoreCase)) ?? "None";
        panel.Children.Add(ChoiceControl("When the scene opens", entranceNames, currentEntrance, value => value, value =>
        {
            element.Entrance = value == "None" ? "" : value;
            if (element.EntranceMs <= 0)
                element.EntranceMs = EntranceEffects.DefaultDurationMs;
            _session?.Commit();
            PlayEntrance();
        }));
        if (EntranceEffects.Known(element.Entrance))
        {
            panel.Children.Add(NumberControl("Starts after (seconds)", element.EntranceDelayMs / 1000.0, value =>
            {
                element.EntranceDelayMs = (int)Math.Round(Math.Clamp(value, 0, 30) * 1000);
                PlayEntrance();
            }));
            panel.Children.Add(NumberControl("How long (seconds)", EntranceEffects.DurationMs(element) / 1000.0, value =>
            {
                element.EntranceMs = (int)Math.Round(Math.Clamp(value <= 0 ? 0.7 : value, 0.15, 8) * 1000);
                PlayEntrance();
            }));
        }
        panel.Children.Add(new TextBlock
        {
            Text = "Plays when this scene opens. Starts after is the wait. Fade in begins hidden, then appears. Slide, rotate, bounce, pop, grow, and flip each arrive in their own way.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Painting.Brush(Painting.Muted),
            FontSize = 12
        });
        return panel;

        void PlayEntrance() =>
            PreviewEntrance?.Invoke(element.Id, element.Entrance, element.EntranceDelayMs, EntranceEffects.DurationMs(element));
    }

    private void BuildBackgroundLayers(Scene scene, EditorSession session)
    {
        scene.PullLegacyBackground();
        Heading("Backgrounds");
        Note("Add more than one picture or video. They stack. Layer 1 is the back. Behind and In front swap that layer with its neighbor. A PNG with an empty background shows the layer behind it. Your buttons stay in front of all of these.");
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var addPicture = new Button { Content = "Add picture", Classes = { "tool" } };
        addPicture.Click += async (_, _) =>
        {
            if (_building)
                return;
            var path = await _browse("image");
            if (path == null)
                return;
            BackgroundLayers.Add(scene, video: false, path);
            session.Commit();
            Refresh();
        };
        var addVideo = new Button { Content = "Add video", Classes = { "tool" } };
        addVideo.Click += async (_, _) =>
        {
            if (_building)
                return;
            var path = await _browse("video");
            if (path == null)
                return;
            BackgroundLayers.Add(scene, video: true, path);
            session.Commit();
            Refresh();
        };
        addRow.Children.Add(addPicture);
        addRow.Children.Add(addVideo);
        Children.Add(addRow);

        for (var index = 0; index < scene.Backgrounds.Count; index++)
        {
            var layer = scene.Backgrounds[index];
            var position = index;
            var card = new StackPanel { Spacing = 8 };
            var file = string.IsNullOrWhiteSpace(layer.Source) ? "No file yet" : System.IO.Path.GetFileName(layer.Source);
            card.Children.Add(new TextBlock
            {
                Text = "Layer " + (position + 1) + "  ·  " + (layer.Video ? "Video" : "Picture") + "  ·  " + file,
                Foreground = Painting.Brush("#E7EEF6"),
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            var row = new WrapPanel { Orientation = Orientation.Horizontal };
            LayerButton(row, "Behind", () => BackgroundLayers.Move(scene, layer.Id, -1));
            LayerButton(row, "In front", () => BackgroundLayers.Move(scene, layer.Id, 1));
            LayerButton(row, "Remove", () => BackgroundLayers.Remove(scene, layer.Id));
            card.Children.Add(row);
            card.Children.Add(LayerText("File", layer.Source, value => layer.Source = value));
            var replace = new Button { Content = layer.Video ? "Choose video" : "Choose image", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
            replace.Click += async (_, _) =>
            {
                if (_building)
                    return;
                var path = await _browse(layer.Video ? "video" : "image");
                if (path == null)
                    return;
                layer.Source = path;
                session.Commit();
                Refresh();
            };
            card.Children.Add(replace);
            if (layer.Video)
            {
                card.Children.Add(LayerCheck("Loop the video", layer.Loop, value => layer.Loop = value));
                card.Children.Add(LayerCheck("Play the video sound", layer.Sound, value => layer.Sound = value));
            }
            var box = layer.Box(session.Document.Width, session.Document.Height);
            card.Children.Add(LayerNumber("X", box.X, value =>
            {
                RememberLayerSize(layer, session);
                layer.X = value;
            }));
            card.Children.Add(LayerNumber("Y", box.Y, value =>
            {
                RememberLayerSize(layer, session);
                layer.Y = value;
            }));
            card.Children.Add(LayerNumber("Width", box.Width, value =>
            {
                RememberLayerSize(layer, session);
                layer.Width = Math.Max(8, value);
            }));
            card.Children.Add(LayerNumber("Height", box.Height, value =>
            {
                RememberLayerSize(layer, session);
                layer.Height = Math.Max(8, value);
            }));
            var fill = new Button { Content = "Fill the scene", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
            fill.Click += (_, _) =>
            {
                if (_building)
                    return;
                layer.Fill = true;
                layer.X = 0;
                layer.Y = 0;
                layer.Width = 0;
                layer.Height = 0;
                session.Commit();
                Refresh();
            };
            card.Children.Add(fill);
            Children.Add(new Border
            {
                BorderBrush = Painting.Brush("#2A3544"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(8),
                Child = card
            });
        }
    }

    private void LayerButton(Panel row, string label, Action action)
    {
        var button = new Button { Content = label, Classes = { "tool" }, Margin = new Avalonia.Thickness(0, 0, 8, 8) };
        button.Click += (_, _) =>
        {
            if (_building)
                return;
            action();
            _session?.Commit();
            Refresh();
        };
        row.Children.Add(button);
    }

    private Control LayerText(string label, string value, Action<string> apply)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(box.Text ?? "");
            _session?.TouchLive();
        };
        box.LostFocus += (_, _) => _session?.Commit();
        panel.Children.Add(box);
        return panel;
    }

    private Control LayerNumber(string label, double value, Action<double> apply)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var box = new TextBox { Text = value.ToString("0.##", CultureInfo.InvariantCulture) };
        box.TextChanged += (_, _) =>
        {
            if (_building)
                return;
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                apply(parsed);
                _session?.TouchLive();
            }
        };
        box.LostFocus += (_, _) => _session?.Commit();
        panel.Children.Add(box);
        return panel;
    }

    private Control LayerCheck(string label, bool value, Action<bool> apply)
    {
        var box = new CheckBox { Content = label, IsChecked = value };
        box.IsCheckedChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(box.IsChecked == true);
            _session?.Commit();
        };
        return box;
    }

    private static void RememberLayerSize(BackgroundLayer layer, EditorSession session)
    {
        if (!layer.Fill && layer.Width >= 8 && layer.Height >= 8)
            return;
        if (layer.Width < 8)
            layer.Width = session.Document.Width;
        if (layer.Height < 8)
            layer.Height = session.Document.Height;
        layer.Fill = false;
    }

    private void BuildInteractions()
    {
        var session = _session!;
        var scene = session.ActiveScene;
        if (scene == null)
            return;
        Note("Actions run from top to bottom. Moving to another scene stops the rest of the list.");
        var element = session.Selection.Count == 1 ? session.Selection[0] : null;
        if (element != null)
            InteractionList(element.Name, element.Interactions, element);
        InteractionList(scene.Name + " scene", scene.Interactions, element);
        InteractionList("Every scene", session.Document.Interactions, element);
    }

    private void InteractionList(string title, List<Interaction> interactions, SceneElement? context)
    {
        Heading(title);
        foreach (var interaction in interactions.ToList())
            Children.Add(InteractionCard(interactions, interaction, context));
        var add = new Button { Content = "Add trigger", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) =>
        {
            if (_building || _session == null)
                return;
            interactions.Add(new Interaction
            {
                Id = Ids.New(),
                Trigger = title.Contains("scene", StringComparison.OrdinalIgnoreCase) && context == null
                    ? TriggerKind.Timer
                    : title.StartsWith("Every", StringComparison.Ordinal)
                        ? TriggerKind.Key
                        : TriggerKind.Tap,
                Key = "Right",
                TimerMs = 5000
            });
            _session.Commit();
            Refresh();
        };
        Children.Add(add);
    }

    private Control InteractionCard(List<Interaction> owner, Interaction interaction, SceneElement? context)
    {
        var card = new Border
        {
            Background = Painting.Brush("#0E1822"),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(10),
            Child = new StackPanel { Spacing = 8 }
        };
        var body = (StackPanel)card.Child;
        body.Children.Add(ChoiceControl("When", Enum.GetValues<TriggerKind>(), interaction.Trigger, Labels.Trigger, value =>
        {
            interaction.Trigger = value;
            CommitAndRefresh();
        }));
        if (interaction.Trigger == TriggerKind.Timer)
        {
            body.Children.Add(NumberControl("Milliseconds", interaction.TimerMs, value => interaction.TimerMs = (int)Math.Max(100, value)));
            body.Children.Add(CheckControl("Repeat", interaction.TimerRepeat, value => interaction.TimerRepeat = value));
            body.Children.Add(CheckControl("Reset when someone touches", interaction.ResetOnActivity, value => interaction.ResetOnActivity = value));
        }
        if (interaction.Trigger == TriggerKind.Key)
            body.Children.Add(TextControl("Key", interaction.Key, value => interaction.Key = value.Trim()));

        foreach (var action in interaction.Actions.ToList())
            body.Children.Add(ActionCard(interaction, action, context));

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new Button { Content = "Add action", Classes = { "tool" } };
        add.Click += (_, _) =>
        {
            interaction.Actions.Add(new ExperienceAction
            {
                Kind = ActionKind.GoToScene,
                TargetId = _session?.Document.Scenes.FirstOrDefault()?.Id ?? ""
            });
            CommitAndRefresh();
        };
        var remove = new Button { Content = "Remove trigger", Classes = { "tool" } };
        remove.Click += (_, _) =>
        {
            owner.Remove(interaction);
            CommitAndRefresh();
        };
        row.Children.Add(add);
        row.Children.Add(remove);
        body.Children.Add(row);
        return card;
    }

    private Control ActionCard(Interaction interaction, ExperienceAction action, SceneElement? context)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(ChoiceControl("Do", Enum.GetValues<ActionKind>(), action.Kind, Labels.Action, value =>
        {
            action.Kind = value;
            if (value == ActionKind.PlayEffect && !TapEffects.Known(action.Text))
                action.Text = "Pop";
            CommitAndRefresh();
        }));
        switch (action.Kind)
        {
            case ActionKind.GoToScene:
                panel.Children.Add(SceneChoice(action));
                break;
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
                panel.Children.Add(ElementChoice(action, context));
                break;
            case ActionKind.PlayEffect:
                panel.Children.Add(ElementChoice(action, context));
                var effect = TapEffects.All.FirstOrDefault(name =>
                    string.Equals(name, action.Text, StringComparison.OrdinalIgnoreCase)) ?? "Pop";
                panel.Children.Add(ChoiceControl("Effect", TapEffects.All, effect, value => value, value =>
                {
                    action.Text = value;
                    _session?.TouchLive();
                }));
                panel.Children.Add(new TextBlock
                {
                    Text = "Leave the element on “the element that was touched” to use the one they tapped. If Go to scene is the next action, that scene waits until the effect finishes.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Painting.Brush(Painting.Muted),
                    FontSize = 12
                });
                break;
        }
        if (action.Kind == ActionKind.OpenUrl)
        {
            panel.Children.Add(TextControl("URL", action.Text, value => action.Text = value));
            panel.Children.Add(new TextBlock
            {
                Text = "This opens inside the kiosk and fills the scene behind your other buttons. Those buttons can still open another scene. There is no close or minimize button.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Painting.Brush(Painting.Muted),
                FontSize = 12
            });
        }
        if (action.Kind is ActionKind.SetText or ActionKind.SetBackground or ActionKind.SetFill)
            panel.Children.Add(TextControl(action.Kind == ActionKind.SetFill || action.Kind == ActionKind.SetBackground ? "Color" : "Value", action.Text, value => action.Text = value));
        if (action.Kind == ActionKind.SetOpacity)
            panel.Children.Add(NumberControl("Opacity 0-1", action.Number, value => action.Number = value));
        if (action.Kind == ActionKind.MoveTo)
        {
            panel.Children.Add(NumberControl("X", action.X, value => action.X = value));
            panel.Children.Add(NumberControl("Y", action.Y, value => action.Y = value));
        }
        if (action.Kind == ActionKind.Resize)
        {
            panel.Children.Add(NumberControl("Width", action.Width, value => action.Width = value));
            panel.Children.Add(NumberControl("Height", action.Height, value => action.Height = value));
        }
        if (action.Kind is ActionKind.MoveTo or ActionKind.Resize or ActionKind.SetOpacity or ActionKind.Wait)
            panel.Children.Add(NumberControl("Duration ms", action.DurationMs, value => action.DurationMs = (int)Math.Max(0, value)));
        if (action.Kind == ActionKind.SetVariable)
        {
            panel.Children.Add(TextControl("Variable", action.VariableName, value => action.VariableName = value.Trim()));
            panel.Children.Add(TextControl("Value", action.VariableValue, value => action.VariableValue = value));
        }
        panel.Children.Add(TextControl("Only if variable", action.IfVariable, value => action.IfVariable = value.Trim()));
        panel.Children.Add(TextControl("Equals", action.IfEquals, value => action.IfEquals = value));
        var remove = new Button { Content = "Remove action", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
        remove.Click += (_, _) =>
        {
            interaction.Actions.Remove(action);
            CommitAndRefresh();
        };
        panel.Children.Add(remove);
        return new Border
        {
            BorderBrush = Painting.Brush("#2A3544"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(8),
            Child = panel
        };
    }

    private void BuildVariables()
    {
        var session = _session!;
        Heading("Variables");
        Note("Type {{name}} in a text element. The player fills it in. time and date are built in.");
        foreach (var variable in session.Document.Variables.ToList())
        {
            var name = TextControl("Name", variable.Name, value => variable.Name = value.Trim());
            var value = TextControl("Value", variable.Value, value => variable.Value = value);
            var remove = new Button { Content = "Remove", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Avalonia.Thickness(0, 4, 0, 8) };
            remove.Click += (_, _) =>
            {
                session.Document.Variables.Remove(variable);
                session.Commit();
                Refresh();
            };
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(name);
            block.Children.Add(value);
            block.Children.Add(remove);
            Children.Add(block);
        }
        var add = new Button { Content = "Add variable", Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) =>
        {
            session.Document.Variables.Add(new ExperienceVariable { Name = "name", Value = "" });
            session.Commit();
            Refresh();
        };
        Children.Add(add);
    }

    private Control SceneChoice(ExperienceAction action)
    {
        var scenes = _session!.Document.Scenes;
        return ChoiceControl("Scene", scenes.Select(scene => scene.Id).ToArray(), action.TargetId, id =>
        {
            var scene = scenes.FirstOrDefault(item => item.Id == id);
            if (scene == null)
                return id;
            return scene.Screen == 0 ? scene.Name : scene.Name + " · " + DisplayScreens.Name(scene.Screen);
        }, value =>
        {
            action.TargetId = value;
            _session.TouchLive();
        });
    }

    private Control ElementChoice(ExperienceAction action, SceneElement? context)
    {
        var elements = _session?.ActiveScene?.Elements ?? new List<SceneElement>();
        var includeBars = action.Kind is ActionKind.Show or ActionKind.Hide or ActionKind.ToggleVisibility;
        var ids = new List<string> { "" };
        foreach (var element in elements)
        {
            ids.Add(element.Id);
            if (includeBars && element.Kind == ElementKind.Video)
                ids.Add(PlayBarTargets.For(element.Id));
        }
        return ChoiceControl("Element", ids.ToArray(), action.TargetId, id =>
        {
            if (string.IsNullOrEmpty(id))
                return context == null ? "Choose an element" : "The element that was touched";
            if (PlayBarTargets.TryGetVideoId(id, out var videoId))
            {
                var name = elements.FirstOrDefault(element => element.Id == videoId)?.Name ?? "Video";
                return name + " play bar";
            }
            return elements.FirstOrDefault(element => element.Id == id)?.Name ?? id;
        }, value =>
        {
            action.TargetId = value;
            _session?.TouchLive();
        });
    }

    private void Heading(string text) => Children.Add(new TextBlock
    {
        Text = text.ToUpperInvariant(),
        Classes = { "section" },
        Margin = new Avalonia.Thickness(0, 8, 0, 0)
    });

    private void Note(string text) => Children.Add(new TextBlock
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Painting.Brush(Painting.Muted),
        FontSize = 12
    });

    private void Text(string label, string value, Action<string> apply, bool structure = false) =>
        Children.Add(TextControl(label, value, apply, structure));

    private void Multiline(string label, string value, Action<string> apply)
    {
        var box = Editor(label, value, apply);
        box.AcceptsReturn = true;
        box.Height = 110;
        box.TextWrapping = TextWrapping.Wrap;
    }

    private void Number(string label, double value, Action<double> apply)
    {
        var box = Editor(label, value.ToString("0.##", CultureInfo.InvariantCulture), text =>
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                apply(parsed);
        });
        box.LostFocus += (_, _) => _session?.Commit();
    }

    private void SliderField(string label, double value, Action<double> apply)
    {
        Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = value };
        slider.ValueChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(slider.Value);
            _session?.TouchLive();
        };
        slider.PointerReleased += (_, _) => _session?.Commit();
        Children.Add(slider);
    }

    private void ColorField(string label, string value, Action<string> apply)
    {
        Text(label, value, apply);
        var wrap = new WrapPanel();
        foreach (var swatch in Painting.Swatches)
        {
            var button = new Button
            {
                Width = 22,
                Height = 22,
                Margin = new Avalonia.Thickness(0, 0, 6, 6),
                Padding = new Avalonia.Thickness(0),
                Background = Painting.Brush(swatch),
                BorderBrush = Painting.Brush("#2A3544"),
                BorderThickness = new Avalonia.Thickness(1)
            };
            button.Click += (_, _) =>
            {
                apply(swatch);
                _session?.Commit();
                Refresh();
            };
            wrap.Children.Add(button);
        }
        Children.Add(wrap);
    }

    private void Check(string label, bool value, Action<bool> apply) =>
        Children.Add(CheckControl(label, value, apply));

    private void Choice(string label, string[] values, string selected, Action<string> apply) =>
        Children.Add(ChoiceControl(label, values, selected, value => value, value =>
        {
            apply(value);
            _session?.TouchLive();
        }));

    private void Browse(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Classes = { "tool" }, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += async (_, _) =>
        {
            if (_building)
                return;
            await action();
            Refresh();
        };
        Children.Add(button);
    }

    private TextBox Editor(string label, string value, Action<string> apply, bool structure = false)
    {
        Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(box.Text ?? "");
            _session?.TouchLive();
        };
        box.LostFocus += (_, _) => _session?.Commit(structure);
        Children.Add(box);
        return box;
    }

    private Control TextControl(string label, string value, Action<string> apply, bool structure = false)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(box.Text ?? "");
            _session?.TouchLive();
        };
        box.LostFocus += (_, _) => _session?.Commit(structure);
        panel.Children.Add(box);
        return panel;
    }

    private Control NumberControl(string label, double value, Action<double> apply)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var box = new TextBox { Text = value.ToString("0.##", CultureInfo.InvariantCulture) };
        box.TextChanged += (_, _) =>
        {
            if (_building)
                return;
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                apply(parsed);
                _session?.TouchLive();
            }
        };
        box.LostFocus += (_, _) => _session?.Commit();
        panel.Children.Add(box);
        return panel;
    }

    private Control CheckControl(string label, bool value, Action<bool> apply)
    {
        var box = new CheckBox { Content = label, IsChecked = value };
        box.IsCheckedChanged += (_, _) =>
        {
            if (_building)
                return;
            apply(box.IsChecked == true);
            _session?.Commit();
        };
        return box;
    }

    private Control ChoiceControl<T>(string label, T[] values, T selected, Func<T, string> display, Action<T> apply)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, Foreground = Painting.Brush(Painting.Muted), FontSize = 12 });
        var combo = new ComboBox { ItemsSource = values, SelectedItem = selected };
        combo.ItemTemplate = new FuncDataTemplate<T>((value, _) => new TextBlock { Text = value == null ? "" : display(value) });
        combo.SelectionChanged += (_, _) =>
        {
            if (_building || combo.SelectedItem is not T value)
                return;
            apply(value);
        };
        panel.Children.Add(combo);
        return panel;
    }

    private void CommitAndRefresh()
    {
        if (_building || _session == null)
            return;
        _session.Commit();
        Refresh();
    }

    private static bool ShowsText(ElementKind kind) => kind is
        ElementKind.Text or ElementKind.Button or ElementKind.WebLink or ElementKind.Audio or ElementKind.DataList;
}
