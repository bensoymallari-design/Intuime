using Intuime.Core;
using Xunit;

namespace Intuime.Tests;

public class ExperienceTests
{
    [Fact]
    public void Sample_round_trips_and_every_target_exists()
    {
        var document = SampleExperiences.CreateLumenGallery();
        var loaded = ProjectStore.Deserialize(ProjectStore.Serialize(document));
        Assert.Equal(document.Scenes.Count, loaded.Scenes.Count);
        Assert.Equal("attract", loaded.StartSceneId);
        Assert.Contains(loaded.Scenes, s => s.Id == loaded.StartSceneId);

        var sceneIds = loaded.Scenes.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var scene in loaded.Scenes)
        {
            var elementIds = scene.Elements.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var action in scene.Interactions.Concat(scene.Elements.SelectMany(e => e.Interactions)).SelectMany(i => i.Actions))
            {
                if (action.Kind == ActionKind.GoToScene)
                    Assert.Contains(action.TargetId, sceneIds);
                if (action.Kind is ActionKind.SetText or ActionKind.PlayMedia or ActionKind.PauseMedia or ActionKind.StopMedia)
                    Assert.Contains(action.TargetId, elementIds);
            }
        }

        Assert.DoesNotContain(loaded.Scenes.Single(s => s.Id == "attract").Interactions, i => i.Trigger == TriggerKind.Timer);
        Assert.Contains(loaded.Scenes.Single(s => s.Id == "home").Interactions, i => i.Trigger == TriggerKind.Timer);
    }

    [Fact]
    public void Player_walks_the_gallery_sample()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(SampleExperiences.CreateLumenGallery(), () => now);
        engine.Start();
        Assert.Equal("attract", engine.Current!.Id);

        engine.BackgroundTap();
        Assert.Equal("home", engine.Current.Id);

        engine.PointerUp("card-collection");
        Assert.Equal("collection", engine.Current.Id);
        engine.PointerUp("work-aurora");
        Assert.Equal("aurora", engine.Current.Id);
        engine.PointerUp("aurora-play-film");
        Assert.True(engine.Current.Elements.Single(e => e.Id == "aurora-film").IsPlaying);
        engine.PointerUp("aurora-pause-film");
        Assert.False(engine.Current.Elements.Single(e => e.Id == "aurora-film").IsPlaying);

        engine.PointerUp("aurora-back");
        Assert.Equal("collection", engine.Current.Id);
        engine.PointerUp("collection-back");
        engine.PointerUp("card-hours");
        engine.SelectDataItem("talks", 0);
        Assert.Equal("10:30  Glassblowing demo", TextTemplates.Get(engine.Live, "talk"));
        Assert.Contains("10:30", TextTemplates.Display(engine.Current.Elements.Single(e => e.Id == "talk-selection"), engine.Live));

        engine.PointerUp("hours-back");
        engine.PointerUp("card-map");
        engine.PointerUp("room-harbor");
        Assert.Equal("Harbor Light", TextTemplates.Get(engine.Live, "selectedRoom"));
        Assert.Contains("Harbor Light", engine.Current.Elements.Single(e => e.Id == "map-detail").Text);
        engine.PointerUp("map-enter");
        Assert.Equal("harbor", engine.Current.Id);

        engine.KeyPressed("right");
        Assert.Equal("orchard", engine.Current.Id);
        engine.KeyPressed("left");
        Assert.Equal("harbor", engine.Current.Id);

        engine.PointerUp("harbor-back");
        engine.PointerUp("collection-back");
        engine.PointerUp("card-visit");
        Assert.Equal("1", TextTemplates.Get(engine.Live, "partySize"));
        engine.NudgeCounter("party-counter", 1);
        engine.NudgeCounter("party-counter", 1);
        Assert.Equal("3", TextTemplates.Get(engine.Live, "partySize"));
        engine.PointerUp("reserve");
        Assert.Contains("3", TextTemplates.Get(engine.Live, "note"));
        Assert.Contains("3", TextTemplates.Display(engine.Current.Elements.Single(e => e.Id == "visit-note"), engine.Live));

        for (var i = 0; i < 20; i++)
            engine.NudgeCounter("party-counter", 1);
        Assert.Equal("12", TextTemplates.Get(engine.Live, "partySize"));
    }

    [Fact]
    public void Timer_wait_tween_and_scene_restore_use_the_injected_clock()
    {
        var now = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var home = new Scene
        {
            Id = "home",
            Name = "Home",
            Elements =
            {
                new SceneElement { Id = "title", Name = "Title", Kind = ElementKind.Text, X = 10, Y = 10, Width = 100, Height = 40, Text = "Hello" }
            },
            Interactions =
            {
                new Interaction
                {
                    Id = "idle",
                    Trigger = TriggerKind.Timer,
                    TimerMs = 2000,
                    TimerRepeat = true,
                    ResetOnActivity = true,
                    Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "away" } }
                },
                new Interaction
                {
                    Id = "enter",
                    Trigger = TriggerKind.SceneEnter,
                    Actions =
                    {
                        new ExperienceAction { Kind = ActionKind.Wait, DurationMs = 500 },
                        new ExperienceAction { Kind = ActionKind.SetVariable, VariableName = "ready", VariableValue = "yes" },
                        new ExperienceAction { Kind = ActionKind.MoveTo, TargetId = "title", X = 110, Y = 10, DurationMs = 1000 }
                    }
                }
            }
        };
        var away = new Scene { Id = "away", Name = "Away" };
        var document = new ExperienceDocument
        {
            StartSceneId = "home",
            Scenes = { home, away },
            Variables = { new ExperienceVariable { Name = "ready", Value = "no" } }
        };

        var engine = new PlaybackEngine(document, () => now);
        engine.Start();
        Assert.Equal("no", TextTemplates.Get(engine.Live, "ready"));
        Assert.Equal(10, engine.Current!.Elements[0].X);

        now = now.AddMilliseconds(500);
        engine.Tick();
        Assert.Equal("yes", TextTemplates.Get(engine.Live, "ready"));
        Assert.Equal(10, engine.Current.Elements[0].X);

        now = now.AddMilliseconds(500);
        engine.Tick();
        Assert.Equal("home", engine.Current.Id);
        Assert.Equal(60, engine.Current.Elements[0].X, 1);

        engine.NotifyActivity();
        now = now.AddMilliseconds(900);
        engine.Tick();
        Assert.Equal("home", engine.Current.Id);

        now = now.AddMilliseconds(1100);
        engine.Tick();
        Assert.Equal("away", engine.Current.Id);

        engine.KeyPressed("unused");
        var back = new ExperienceDocument
        {
            StartSceneId = "home",
            Scenes =
            {
                ProjectStore.Clone(home),
                away
            },
            Interactions =
            {
                new Interaction
                {
                    Id = "back",
                    Trigger = TriggerKind.Key,
                    Key = "b",
                    Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "home" } }
                }
            }
        };
        now = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        engine = new PlaybackEngine(back, () => now);
        string? opened = null;
        var exited = false;
        engine.OpenUrlRequested += url => opened = url;
        engine.ExitRequestedEvent += () => exited = true;
        engine.Start();
        engine.Current!.Elements[0].X = 400;
        engine.PointerUp("missing");
        engine.KeyPressed("B");
        Assert.Equal("home", engine.Current.Id);
        Assert.Equal(10, engine.Current.Elements.Single(e => e.Id == "title").X);

        engine.RunExternal(new ExperienceAction { Kind = ActionKind.OpenUrl, Text = "https://example.com" });
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.ExitPlayer });
        Assert.Equal("https://example.com", opened);
        Assert.True(exited);
    }

    [Fact]
    public void Conditions_double_tap_show_hide_and_restart()
    {
        var scene = new Scene
        {
            Id = "only",
            Name = "Only",
            Elements =
            {
                new SceneElement
                {
                    Id = "box",
                    Name = "Box",
                    Kind = ElementKind.Rectangle,
                    IsVisible = true,
                    Interactions =
                    {
                        new Interaction
                        {
                            Id = "tap",
                            Trigger = TriggerKind.Tap,
                            Actions = { new ExperienceAction { Kind = ActionKind.SetVariable, VariableName = "mode", VariableValue = "tap" } }
                        },
                        new Interaction
                        {
                            Id = "double",
                            Trigger = TriggerKind.DoubleTap,
                            Actions =
                            {
                                new ExperienceAction { Kind = ActionKind.SetVariable, VariableName = "mode", VariableValue = "double" },
                                new ExperienceAction { Kind = ActionKind.Hide, TargetId = "box", IfVariable = "mode", IfEquals = "nope" },
                                new ExperienceAction { Kind = ActionKind.Hide, TargetId = "box", IfVariable = "mode", IfEquals = "double" }
                            }
                        }
                    }
                }
            }
        };
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var document = new ExperienceDocument
        {
            StartSceneId = "only",
            Scenes = { scene },
            Variables = { new ExperienceVariable { Name = "keep", Value = "stay" } }
        };
        var engine = new PlaybackEngine(document, () => now);
        engine.Start();
        engine.PointerUp("box");
        Assert.Equal("tap", TextTemplates.Get(engine.Live, "mode"));
        Assert.True(engine.Current!.Elements[0].IsVisible);
        now = now.AddMilliseconds(200);
        engine.PointerUp("box");
        Assert.Equal("double", TextTemplates.Get(engine.Live, "mode"));
        Assert.False(engine.Current.Elements[0].IsVisible);

        TextTemplates.Set(engine.Live, "keep", "changed");
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.RestartExperience });
        Assert.Equal("only", engine.Current.Id);
        Assert.Equal("stay", TextTemplates.Get(engine.Live, "keep"));
        Assert.True(engine.Current.Elements[0].IsVisible);
    }

    [Fact]
    public void Layout_hit_testing_align_and_snap()
    {
        var box = new SceneElement { X = 0, Y = 0, Width = 100, Height = 20, Rotation = 0 };
        Assert.True(LayoutMath.Contains(box, 10, 10));
        Assert.False(LayoutMath.Contains(box, 10, 30));

        box.Rotation = 90;
        Assert.True(LayoutMath.Contains(box, 50, 60));
        Assert.False(LayoutMath.Contains(box, 80, 10));

        var items = new List<SceneElement>
        {
            new() { X = 0, Y = 5, Width = 10, Height = 10 },
            new() { X = 30, Y = 0, Width = 10, Height = 40 },
            new() { X = 80, Y = 15, Width = 20, Height = 10 }
        };
        LayoutMath.Align(items, "left");
        Assert.All(items, i => Assert.Equal(0, i.X));
        LayoutMath.Align(items, "top");
        Assert.All(items, i => Assert.Equal(0, i.Y));
        Assert.Equal(16, LayoutMath.Snap(14, true, 8));
        Assert.Equal(14, LayoutMath.Snap(14, false, 8));
    }

    [Fact]
    public void Editor_undo_duplicate_and_scene_remap()
    {
        var session = new EditorSession();
        session.Load(SampleExperiences.CreateBlank("Draft", 1280, 720), null, "", markClean: true);
        Assert.False(session.IsDirty);
        var added = session.AddElement(ElementKind.Button);
        Assert.True(session.IsDirty);
        Assert.True(session.CanUndo);
        session.Undo();
        Assert.Single(session.ActiveScene!.Elements);
        session.Redo();
        Assert.Contains(session.ActiveScene.Elements, e => e.Id == added.Id);

        var first = session.ActiveScene.Elements[0];
        var second = session.ActiveScene.Elements[1];
        second.Interactions.Add(new Interaction
        {
            Id = "go",
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.Show, TargetId = first.Id } }
        });
        session.SetSelection(new[] { first, second });
        session.DuplicateSelection();
        var copies = session.Selection;
        Assert.Equal(2, copies.Count);
        var copiedButton = copies.Single(c => c.Kind == ElementKind.Button);
        Assert.Equal(copies.Single(c => c.Kind == ElementKind.Text).Id, copiedButton.Interactions[0].Actions[0].TargetId);
        Assert.NotEqual(first.Id, copiedButton.Interactions[0].Actions[0].TargetId);

        session.ActiveScene.Interactions.Add(new Interaction
        {
            Id = "self",
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = session.ActiveScene.Id } }
        });
        var copy = session.DuplicateActiveScene();
        Assert.Equal(copy.Id, copy.Interactions.Single().Actions[0].TargetId);
        Assert.NotEqual(session.Document.Scenes[0].Id, copy.Id);
    }

    [Fact]
    public void Pointer_capture_rules_match_the_player()
    {
        var plain = ElementFactory.Create(ElementKind.Text, 0, 0, 0, 1);
        Assert.False(ElementFactory.CapturesPointer(plain));
        plain.Interactions.Add(new Interaction { Trigger = TriggerKind.Tap });
        Assert.True(ElementFactory.CapturesPointer(plain));
        Assert.True(ElementFactory.CapturesPointer(ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1)));
        Assert.True(ElementFactory.CapturesPointer(ElementFactory.Create(ElementKind.Hotspot, 0, 0, 0, 1)));
        Assert.False(ElementFactory.CapturesPointer(ElementFactory.Create(ElementKind.Rectangle, 0, 0, 0, 1)));
    }

    [Fact]
    public void Text_in_front_of_a_video_is_drawn_on_that_video()
    {
        var video = ElementFactory.Create(ElementKind.Video, 0, 0, 1, 1);
        video.Width = 800;
        video.Height = 450;
        var caption = ElementFactory.Create(ElementKind.Text, 40, 40, 2, 2);
        caption.Text = "X";
        var behind = ElementFactory.Create(ElementKind.Text, 40, 200, 0, 3);
        var outside = ElementFactory.Create(ElementKind.Text, 2000, 40, 5, 4);
        var elements = new List<SceneElement> { video, caption, behind, outside };

        Assert.Equal(video.Id, LayoutMath.VideoOverlayOwner(elements, caption));
        Assert.Null(LayoutMath.VideoOverlayOwner(elements, behind));
        Assert.Null(LayoutMath.VideoOverlayOwner(elements, outside));
        Assert.Null(LayoutMath.VideoOverlayOwner(elements, video));
    }

    [Fact]
    public void Video_loop_and_play_bar_round_trip()
    {
        var video = ElementFactory.Create(ElementKind.Video, 0, 0, 0, 1);
        Assert.True(video.ShowControls);
        video.Loop = true;
        video.ShowControls = false;
        var clone = ProjectStore.Clone(video);
        Assert.True(clone.Loop);
        Assert.False(clone.ShowControls);
    }

    [Fact]
    public void Tapping_a_video_can_show_its_hidden_play_bar()
    {
        var video = ElementFactory.Create(ElementKind.Video, 0, 0, 0, 1);
        video.Name = "Video 1";
        video.ShowControls = false;
        video.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.Show, TargetId = PlayBarTargets.For(video.Id) } }
        });
        var scene = new Scene { Id = "film", Name = "Film", Elements = { video } };
        var document = new ExperienceDocument { StartSceneId = scene.Id, Scenes = { scene } };
        var engine = new PlaybackEngine(document);
        engine.Start();
        var live = engine.Current!.Elements.Single();
        Assert.False(live.ShowControls);

        engine.PointerUp(live.Id);
        Assert.True(live.ShowControls);
        Assert.True(live.IsVisible);

        engine.RunExternal(new ExperienceAction { Kind = ActionKind.Hide, TargetId = PlayBarTargets.For(live.Id) });
        Assert.False(live.ShowControls);
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.ToggleVisibility, TargetId = PlayBarTargets.For(live.Id) });
        Assert.True(live.ShowControls);

        live.ShowControls = false;
        live.IsVisible = false;
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.Show, TargetId = live.Id });
        Assert.True(live.IsVisible);
        Assert.False(live.ShowControls);
    }

    [Fact]
    public void Duplicating_a_video_keeps_the_play_bar_target()
    {
        var session = new EditorSession();
        session.Load(SampleExperiences.CreateBlank("Draft", 800, 600), null, "", markClean: true);
        var video = session.AddElement(ElementKind.Video);
        video.Name = "Video 1";
        video.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.Show, TargetId = PlayBarTargets.For(video.Id) } }
        });
        session.SetSelection(new[] { video });
        session.DuplicateSelection();
        var copy = session.Selection.Single();
        Assert.Equal(PlayBarTargets.For(copy.Id), copy.Interactions[0].Actions[0].TargetId);
        Assert.NotEqual(PlayBarTargets.For(video.Id), copy.Interactions[0].Actions[0].TargetId);
    }

    [Fact]
    public void Insert_designs_cover_every_kind_and_can_be_placed()
    {
        foreach (var item in ElementFactory.Catalog)
        {
            var presets = ElementPresets.For(item.Kind);
            Assert.True(presets.Count >= 4);
            var signatures = presets.Select(preset =>
            {
                var element = ElementFactory.Create(item.Kind, 0, 0, 0, 1);
                preset.Apply(element);
                return $"{element.Fill}|{element.Foreground}|{element.Text}|{element.Width}|{element.CornerRadius}|{element.FontSize}";
            }).Distinct().ToList();
            Assert.True(signatures.Count >= 3);
        }

        var session = new EditorSession();
        session.Load(SampleExperiences.CreateBlank("Draft", 800, 600), null, "", markClean: true);
        var amber = ElementPresets.For(ElementKind.Button).First(preset => preset.Name == "Amber");
        var placed = session.AddElement(ElementKind.Button, amber);
        Assert.Equal("Amber", placed.Name);
        Assert.Equal("Continue", placed.Text);
        Assert.Equal("#E7A23A", placed.Fill);
        var again = session.AddElement(ElementKind.Button, amber);
        Assert.Equal("Amber 2", again.Name);
    }
}
