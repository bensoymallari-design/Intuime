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
    public void Web_addresses_stay_inside_http()
    {
        Assert.True(WebAddresses.TryNormalize("https://example.com", out var https));
        Assert.Equal("https://example.com/", https);
        Assert.True(WebAddresses.TryNormalize("example.com/visit", out var bare));
        Assert.Equal("https://example.com/visit", bare);
        Assert.False(WebAddresses.TryNormalize("javascript:alert(1)", out _));
        Assert.False(WebAddresses.TryNormalize("file:///tmp/page.html", out _));
        Assert.False(WebAddresses.TryNormalize("   ", out _));
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

    [Fact]
    public void Background_layers_keep_order_and_swap()
    {
        var scene = new Scene
        {
            Id = "room",
            BackgroundImage = "assets/aurora.png",
            BackgroundVideo = "assets/studio.mp4",
            BackgroundVideoLoop = false,
            BackgroundVideoSound = true,
            BackgroundFill = false,
            BackgroundX = 10,
            BackgroundY = 20,
            BackgroundWidth = 400,
            BackgroundHeight = 300
        };
        var clone = ProjectStore.Clone(new ExperienceDocument { StartSceneId = scene.Id, Scenes = { scene } }).Scenes.Single();
        Assert.Equal(2, clone.Backgrounds.Count);
        Assert.False(clone.Backgrounds[0].Video);
        Assert.Equal("assets/aurora.png", clone.Backgrounds[0].Source);
        Assert.True(clone.Backgrounds[1].Video);
        Assert.False(clone.Backgrounds[1].Loop);
        Assert.True(clone.Backgrounds[1].Sound);
        Assert.Equal((10d, 20d, 400d, 300d), clone.Backgrounds[1].Box(1920, 1080));

        var logo = BackgroundLayers.Add(clone, video: false, "assets/logo.png");
        Assert.Equal(logo.Id, clone.Backgrounds[^1].Id);
        Assert.True(logo.Fill);
        BackgroundLayers.Move(clone, logo.Id, -1);
        Assert.Equal(logo.Id, clone.Backgrounds[1].Id);
        BackgroundLayers.Move(clone, logo.Id, -1);
        Assert.Equal(logo.Id, clone.Backgrounds[0].Id);
        BackgroundLayers.Move(clone, logo.Id, -1);
        Assert.Equal(logo.Id, clone.Backgrounds[0].Id);
        BackgroundLayers.Move(clone, logo.Id, 1);
        Assert.Equal(logo.Id, clone.Backgrounds[1].Id);
        BackgroundLayers.Remove(clone, logo.Id);
        Assert.Equal(2, clone.Backgrounds.Count);
        Assert.DoesNotContain(clone.Backgrounds, layer => layer.Id == logo.Id);

        var again = ProjectStore.Clone(new ExperienceDocument { Scenes = { clone } }).Scenes.Single();
        Assert.Equal(clone.Backgrounds.Select(layer => layer.Source), again.Backgrounds.Select(layer => layer.Source));
        Assert.Equal(clone.Backgrounds.Select(layer => layer.Video), again.Backgrounds.Select(layer => layer.Video));
    }

    [Fact]
    public void Background_video_and_effect_strength_round_trip()
    {
        var scene = new Scene
        {
            Id = "city",
            Name = "City",
            BackgroundVideo = "assets/city.mp4",
            BackgroundVideoLoop = false,
            BackgroundVideoSound = true
        };
        var button = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        button.TapEffect = "Squash";
        button.TapStrength = 1.6;
        scene.Elements.Add(button);
        scene.BackgroundFill = false;
        scene.BackgroundX = 40;
        scene.BackgroundY = 80;
        scene.BackgroundWidth = 640;
        scene.BackgroundHeight = 360;
        var document = new ExperienceDocument { StartSceneId = scene.Id, Scenes = { scene } };
        var clone = ProjectStore.Clone(document).Scenes.Single();
        Assert.Equal("", clone.BackgroundVideo);
        var video = Assert.Single(clone.Backgrounds);
        Assert.True(video.Video);
        Assert.Equal("assets/city.mp4", video.Source);
        Assert.False(video.Loop);
        Assert.True(video.Sound);
        Assert.False(video.Fill);
        Assert.Equal((40d, 80d, 640d, 360d), video.Box(1920, 1080));
        var fresh = new Scene();
        Assert.True(fresh.BackgroundFill);
        Assert.False(fresh.HasCustomBackgroundSize);
        Assert.Equal((0d, 0d, 1920d, 1080d), fresh.BackgroundBox(1920, 1080));
        Assert.Empty(fresh.Backgrounds);
        Assert.Equal("Squash", clone.Elements.Single().TapEffect);
        Assert.Equal(1.6, clone.Elements.Single().TapStrength);
        Assert.True(new Scene().BackgroundVideoLoop);
        Assert.False(new Scene().BackgroundVideoSound);

        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(document, () => now);
        engine.Start();
        var played = Assert.Single(engine.Current!.Backgrounds);
        Assert.Equal(640, played.Width);
        Assert.Equal(360, played.Height);
        Assert.False(played.Fill);
        Assert.True(played.Video);
        var live = engine.Current.Elements.Single();
        engine.PointerUp(live.Id);
        now = now.AddMilliseconds(260);
        engine.Tick();
        Assert.True(live.EffectScaleX > live.EffectScaleY);
        Assert.True(live.EffectScaleX > 1.2);
        Assert.True(live.EffectScaleY < 0.9);

        var pop = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 2);
        pop.TapEffect = "Pop";
        pop.TapStrength = 2;
        var popScene = new Scene { Id = "only", Elements = { pop } };
        var popEngine = new PlaybackEngine(new ExperienceDocument { StartSceneId = "only", Scenes = { popScene } }, () => now);
        popEngine.Start();
        var popped = popEngine.Current!.Elements.Single();
        popEngine.PointerUp(popped.Id);
        now = now.AddMilliseconds(260);
        popEngine.Tick();
        Assert.True(popped.EffectScale > 1.45);
    }

    [Fact]
    public void Tap_effect_plays_then_rests_and_round_trips()
    {
        var button = ElementFactory.Create(ElementKind.Button, 10, 20, 0, 1);
        button.TapEffect = "Pop";
        button.EffectScale = 4;
        var clone = ProjectStore.Clone(button);
        Assert.Equal("Pop", clone.TapEffect);
        Assert.Equal(1, clone.EffectScale);
        Assert.True(ElementFactory.CapturesPointer(button));

        var scene = new Scene { Id = "only", Name = "Only", Elements = { button } };
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(new ExperienceDocument { StartSceneId = "only", Scenes = { scene } }, () => now);
        engine.Start();
        var live = engine.Current!.Elements.Single();
        engine.PointerUp(live.Id);
        Assert.Equal(1, live.EffectScale);

        now = now.AddMilliseconds(260);
        engine.Tick();
        Assert.True(live.EffectScale > 1.15);

        now = now.AddMilliseconds(260);
        engine.Tick();
        Assert.Equal(1, live.EffectScale);
        Assert.Equal(0, live.EffectShiftX);
        Assert.Equal(0, live.EffectRotation);
        Assert.Null(live.EffectFill);
    }

    [Fact]
    public void Play_effect_shakes_the_touched_element_and_ignores_unknown_names()
    {
        var button = ElementFactory.Create(ElementKind.Rectangle, 0, 0, 0, 1);
        button.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.PlayEffect, Text = "Shake" } }
        });
        var scene = new Scene { Id = "only", Name = "Only", Elements = { button } };
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(new ExperienceDocument { StartSceneId = "only", Scenes = { scene } }, () => now);
        engine.Start();
        var live = engine.Current!.Elements.Single();
        engine.PointerUp(live.Id);
        now = now.AddMilliseconds(35);
        engine.Tick();
        Assert.True(Math.Abs(live.EffectShiftX) > 8);

        var shift = live.EffectShiftX;
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.PlayEffect, TargetId = live.Id, Text = "Wiggle" });
        Assert.Equal(shift, live.EffectShiftX);
    }

    [Fact]
    public void Tap_effect_finishes_before_the_scene_changes()
    {
        var button = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        button.TapEffect = "Bounce";
        button.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "next" } }
        });
        var next = new Scene { Id = "next", Name = "Next" };
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(new ExperienceDocument
        {
            StartSceneId = "only",
            Scenes =
            {
                new Scene { Id = "only", Name = "Only", Elements = { button } },
                next
            }
        }, () => now);
        engine.Start();
        engine.PointerUp(button.Id);
        Assert.Equal("only", engine.Current!.Id);
        var live = engine.Current.Elements.Single();
        now = now.AddMilliseconds(490);
        engine.Tick();
        Assert.Equal("only", engine.Current.Id);
        Assert.True(live.EffectShiftY < -80);

        now = now.AddMilliseconds(490);
        engine.Tick();
        Assert.Equal("next", engine.Current.Id);
    }

    [Fact]
    public void Play_effect_action_waits_before_the_next_scene()
    {
        var button = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        button.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions =
            {
                new ExperienceAction { Kind = ActionKind.PlayEffect, Text = "Fade" },
                new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "next" }
            }
        });
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(new ExperienceDocument
        {
            StartSceneId = "only",
            Scenes =
            {
                new Scene { Id = "only", Name = "Only", Elements = { button } },
                new Scene { Id = "next", Name = "Next" }
            }
        }, () => now);
        engine.Start();
        engine.PointerUp(button.Id);
        Assert.Equal("only", engine.Current!.Id);
        now = now.AddMilliseconds(260);
        engine.Tick();
        Assert.Equal("only", engine.Current.Id);
        Assert.True(engine.Current.Elements.Single().EffectOpacity < 0.4);
        now = now.AddMilliseconds(260);
        engine.Tick();
        Assert.Equal("next", engine.Current.Id);
    }

    [Fact]
    public void Entrance_waits_then_arrives_and_plays_again_next_visit()
    {
        var posed = new SceneElement();
        EntranceEffects.Apply(posed, "Bounce", 0);
        Assert.True(posed.EffectShiftY < -200);
        EntranceEffects.Apply(posed, "Rotate", 0);
        Assert.True(posed.EffectRotation < -100);
        EntranceEffects.Apply(posed, "Fade in", 1);
        Assert.Equal(1, posed.EffectOpacity);
        Assert.Equal(0, posed.EffectShiftY);

        var title = ElementFactory.Create(ElementKind.Text, 40, 50, 0, 1);
        title.Id = "title";
        title.Entrance = "Fade in";
        title.EntranceDelayMs = 400;
        title.EntranceMs = 500;
        var button = ElementFactory.Create(ElementKind.Button, 40, 200, 0, 2);
        button.Id = "go";
        button.Entrance = "Slide from left";
        button.EntranceDelayMs = 0;
        button.EntranceMs = 400;
        button.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "other" } }
        });
        var back = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        back.Id = "back";
        back.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "only" } }
        });
        var now = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(new ExperienceDocument
        {
            StartSceneId = "only",
            Scenes =
            {
                new Scene { Id = "only", Elements = { title, button } },
                new Scene { Id = "other", Elements = { back } }
            }
        }, () => now);
        var saved = ProjectStore.Clone(engine.Original).Scenes[0].Elements.Single(e => e.Id == "title");
        Assert.Equal("Fade in", saved.Entrance);
        Assert.Equal(400, saved.EntranceDelayMs);
        Assert.Equal(500, saved.EntranceMs);

        engine.Start();
        var liveTitle = engine.Current!.Elements.Single(e => e.Id == "title");
        var liveButton = engine.Current.Elements.Single(e => e.Id == "go");
        Assert.Equal(0, liveTitle.EffectOpacity);
        Assert.True(liveButton.EffectShiftX < -400);

        now = now.AddMilliseconds(200);
        engine.Tick();
        liveButton = engine.Current.Elements.Single(e => e.Id == "go");
        Assert.Equal(0, liveTitle.EffectOpacity);
        Assert.True(liveButton.EffectShiftX < 0);
        Assert.True(liveButton.EffectShiftX > -300);
        Assert.True(liveButton.EffectOpacity > 0.5);

        now = now.AddMilliseconds(200);
        engine.Tick();
        liveButton = engine.Current.Elements.Single(e => e.Id == "go");
        Assert.Equal(0, liveButton.EffectShiftX);
        Assert.Equal(1, liveButton.EffectOpacity);
        Assert.True(liveTitle.EffectOpacity < 0.05);

        now = now.AddMilliseconds(500);
        engine.Tick();
        Assert.Equal(1, engine.Current.Elements.Single(e => e.Id == "title").EffectOpacity);

        var plain = ElementFactory.Create(ElementKind.Text, 0, 0, 0, 1);
        plain.Entrance = "Wiggle";
        var plainEngine = new PlaybackEngine(new ExperienceDocument
        {
            StartSceneId = "still",
            Scenes = { new Scene { Id = "still", Elements = { plain } } }
        }, () => now);
        plainEngine.Start();
        Assert.Equal(1, plainEngine.Current!.Elements.Single().EffectOpacity);
        Assert.Equal(0, plainEngine.Current.Elements.Single().EffectShiftX);

        now = now.AddSeconds(2);
        engine.PointerUp("go");
        Assert.Equal("other", engine.Current.Id);
        engine.PointerUp("back");
        Assert.Equal("only", engine.Current.Id);
        Assert.Equal(0, engine.Current.Elements.Single(e => e.Id == "title").EffectOpacity);
        now = now.AddMilliseconds(900);
        engine.Tick();
        Assert.Equal(1, engine.Current.Elements.Single(e => e.Id == "title").EffectOpacity);
        Assert.Equal(0, engine.Current.Elements.Single(e => e.Id == "go").EffectShiftX);
    }

    [Fact]
    public void Each_screen_keeps_its_own_scene_and_its_own_start()
    {
        var go = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        go.Id = "go";
        go.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "film" } }
        });
        var later = ElementFactory.Create(ElementKind.Button, 0, 100, 0, 1);
        later.Id = "later";
        later.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions =
            {
                new ExperienceAction { Kind = ActionKind.Wait, DurationMs = 500 },
                new ExperienceAction { Kind = ActionKind.SetVariable, VariableName = "stayed", VariableValue = "yes" }
            }
        });
        var home = new Scene { Id = "home", Name = "Home", Screen = 0, Elements = { go, later } };
        var credits = new Scene { Id = "credits", Name = "Credits", Screen = 0 };
        var loop = new Scene { Id = "loop", Name = "Loop", Screen = 1 };
        var film = new Scene { Id = "film", Name = "Film", Screen = 1 };
        var document = new ExperienceDocument
        {
            StartSceneId = "home",
            ScreenStarts = { new ScreenStart { Screen = 1, SceneId = "loop" } },
            Variables = { new ExperienceVariable { Name = "stayed", Value = "no" } },
            Scenes = { home, credits, loop, film },
            Interactions =
            {
                new Interaction
                {
                    Id = "next",
                    Trigger = TriggerKind.Key,
                    Key = "Right",
                    Actions = { new ExperienceAction { Kind = ActionKind.NextScene } }
                }
            }
        };

        var loaded = ProjectStore.Deserialize(ProjectStore.Serialize(document));
        Assert.Equal(1, loaded.Scenes.Single(scene => scene.Id == "film").Screen);
        Assert.Equal("loop", Assert.Single(loaded.ScreenStarts).SceneId);
        Assert.Equal("home", loaded.StartSceneId);

        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var engine = new PlaybackEngine(loaded, () => now);
        engine.Start();
        Assert.Equal("home", engine.SceneFor(0)!.Id);
        Assert.Equal("loop", engine.SceneFor(1)!.Id);
        Assert.Equal("home", engine.Current!.Id);

        engine.PointerUp("later");
        engine.PointerUp("go");
        Assert.Equal("home", engine.SceneFor(0)!.Id);
        Assert.Equal("film", engine.SceneFor(1)!.Id);

        now = now.AddMilliseconds(500);
        engine.Tick();
        Assert.Equal("yes", TextTemplates.Get(engine.Live, "stayed"));
        Assert.Equal("home", engine.SceneFor(0)!.Id);
        Assert.Equal("film", engine.SceneFor(1)!.Id);

        engine.KeyPressed("Right", 0);
        Assert.Equal("credits", engine.SceneFor(0)!.Id);
        Assert.Equal("film", engine.SceneFor(1)!.Id);

        engine.KeyPressed("Right", 1);
        Assert.Equal("credits", engine.SceneFor(0)!.Id);
        Assert.Equal("loop", engine.SceneFor(1)!.Id);

        var session = new EditorSession();
        session.Load(ProjectStore.Clone(document), null, "", markClean: true);
        var moved = session.Document.Scenes.Single(scene => scene.Id == "loop");
        session.SetScreenStart(moved);
        Assert.True(DisplayScreens.IsStart(session.Document, moved));
        moved.Screen = 0;
        session.SetScreenStart(moved);
        Assert.Equal("loop", session.Document.StartSceneId);
        Assert.DoesNotContain(session.Document.ScreenStarts, item => item.SceneId == "loop");
    }

    [Fact]
    public void Osc_address_triggers_a_button_and_a_tap_sends_it_on()
    {
        var button = ElementFactory.Create(ElementKind.Button, 0, 0, 0, 1);
        button.Id = "go";
        button.Text = "Go";
        button.OscAddress = "begin";
        button.Interactions.Add(new Interaction
        {
            Trigger = TriggerKind.Tap,
            Actions = { new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "next" } }
        });
        var title = ElementFactory.Create(ElementKind.Text, 0, 0, 0, 1);
        title.Id = "title";
        title.Text = "Hello";
        title.OscAddress = "/title";
        var counter = ElementFactory.Create(ElementKind.Counter, 0, 40, 0, 1);
        counter.Id = "count";
        counter.VariableBinding = "partySize";
        counter.OscAddress = "/count";
        var home = new Scene
        {
            Id = "home",
            Name = "Home",
            OscAddress = "/scene/home",
            Elements = { button, title, counter }
        };
        var next = new Scene { Id = "next", Name = "Next", OscAddress = "/scene/next" };
        var document = new ExperienceDocument
        {
            StartSceneId = "home",
            OscListenPort = 8000,
            OscSendHost = "127.0.0.1",
            OscSendPort = 9000,
            Variables = { new ExperienceVariable { Name = "partySize", Value = "1" } },
            Scenes = { home, next }
        };
        var loaded = ProjectStore.Deserialize(ProjectStore.Serialize(document));
        Assert.Equal("/begin", OscAddresses.Canonical(loaded.Scenes[0].Elements[0].OscAddress));
        Assert.Equal(8000, loaded.OscListenPort);
        Assert.Equal(9000, loaded.OscSendPort);

        var sent = new List<OscMessage>();
        var engine = new PlaybackEngine(loaded);
        engine.OscSend += sent.Add;
        engine.Start();
        Assert.Equal("/scene/home", sent.Single().Address);
        Assert.Equal(1, sent.Single().Number);
        sent.Clear();

        var packet = OscCodec.Encode(new OscMessage { Address = "/begin", Number = 1 });
        var decoded = Assert.Single(OscCodec.Decode(packet));
        Assert.Equal("/begin", decoded.Address);
        Assert.Equal(1, decoded.Number);
        engine.ReceiveOsc(decoded);
        Assert.Equal("next", engine.Current!.Id);
        Assert.Empty(sent);

        engine.ReceiveOsc(new OscMessage { Address = "/scene/home", Number = 0 });
        Assert.Equal("next", engine.Current.Id);
        engine.ReceiveOsc(new OscMessage { Address = "scene/home" });
        Assert.Equal("home", engine.Current.Id);
        sent.Clear();

        engine.PointerUp("go");
        Assert.Equal("next", engine.Current.Id);
        Assert.Contains(sent, item => item.Address == "/begin" && item.Number == 1);
        Assert.Contains(sent, item => item.Address == "/scene/next" && item.Number == 1);

        engine.ReceiveOsc(new OscMessage { Address = "/scene/home" });
        engine.ReceiveOsc(new OscMessage { Address = "/title", Text = "From the controller" });
        Assert.Equal("From the controller", engine.Current!.Elements.Single(element => element.Id == "title").Text);
        engine.ReceiveOsc(new OscMessage { Address = "/count", Number = 4, Integer = true });
        Assert.Equal("4", TextTemplates.Get(engine.Live, "partySize"));

        var bundled = new List<byte>();
        bundled.AddRange(System.Text.Encoding.ASCII.GetBytes("#bundle\0"));
        bundled.AddRange(new byte[8]);
        var inner = OscCodec.Encode(new OscMessage { Address = "/scene/next", Number = 1 });
        bundled.Add((byte)(inner.Length >> 24));
        bundled.Add((byte)(inner.Length >> 16));
        bundled.Add((byte)(inner.Length >> 8));
        bundled.Add((byte)inner.Length);
        bundled.AddRange(inner);
        var fromBundle = Assert.Single(OscCodec.Decode(bundled.ToArray()));
        Assert.Equal("/scene/next", fromBundle.Address);

        sent.Clear();
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.SendOsc, Text = "/lights/{{partySize}}", VariableValue = "{{partySize}}" });
        Assert.Equal("/lights/4", sent.Single().Address);
        Assert.Equal(4, sent.Single().Number);
        Assert.True(sent.Single().Integer);
    }

    [Fact]
    public void Built_in_background_is_a_known_looping_design()
    {
        Assert.Equal(0, BuiltInBackgrounds.Phase(0, 8));
        Assert.Equal(0, BuiltInBackgrounds.Phase(8, 8));
        Assert.Equal(0, Math.Sin(Math.Tau * BuiltInBackgrounds.Phase(0, 8)), 6);
        Assert.Equal(Math.Sin(Math.Tau * BuiltInBackgrounds.Phase(0, 8)), Math.Sin(Math.Tau * BuiltInBackgrounds.Phase(8, 8)), 6);

        var ids = BuiltInBackgrounds.All.Select(item => item.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var category in new[] { "Futuristic", "Space", "Buildings", "Zodiac", "2D", "Nature", "Light" })
            Assert.True(BuiltInBackgrounds.In(category).Length >= 3);
        Assert.Equal(BuiltInBackgrounds.Categories(), new[] { "Futuristic", "Space", "Buildings", "Zodiac", "2D", "Nature", "Light" });
        Assert.Equal("futuristic/neon-grid", BuiltInBackgrounds.Canonical("Futuristic/Neon-Grid"));
        Assert.Equal("", BuiltInBackgrounds.Canonical("missing/nope"));

        var scene = new Scene { Id = "room", Name = "Room" };
        var layer = BackgroundLayers.AddBuiltIn(scene, "space/starfield");
        Assert.Equal("space/starfield", layer.BuiltIn);
        Assert.Equal("Starfield", layer.Name);
        Assert.False(layer.Video);
        Assert.True(layer.IsBuiltIn);
        Assert.True(layer.Fill);

        var clone = ProjectStore.Clone(new ExperienceDocument { StartSceneId = scene.Id, Scenes = { scene } }).Scenes.Single();
        var saved = Assert.Single(clone.Backgrounds);
        Assert.Equal("space/starfield", saved.BuiltIn);
        Assert.Equal("Starfield", saved.Name);
        Assert.False(saved.Video);

        saved.BuiltIn = "missing/nope";
        var repaired = ProjectStore.Clone(new ExperienceDocument { Scenes = { clone } }).Scenes.Single();
        Assert.Equal("", repaired.Backgrounds.Single().BuiltIn);
        Assert.False(repaired.Backgrounds.Single().IsBuiltIn);

        var old = """
        {
          "name": "Old",
          "width": 800,
          "height": 600,
          "startSceneId": "s",
          "scenes": [{
            "id": "s",
            "name": "Room",
            "backgrounds": [{ "id": "b", "name": "Picture", "source": "assets/a.png", "video": false }]
          }]
        }
        """;
        var loaded = ProjectStore.Deserialize(old).Scenes.Single();
        Assert.Equal("", loaded.Backgrounds.Single().BuiltIn);
        Assert.Equal("assets/a.png", loaded.Backgrounds.Single().Source);
    }

    [Fact]
    public void Built_in_icons_cover_media_smiles_and_flags()
    {
        Assert.Equal(new[] { "Media", "Smiles", "Flags", "Arrows", "Symbols", "Objects" }, BuiltInIcons.Categories());
        Assert.Contains(BuiltInIcons.In("Media"), icon => icon.Id == "media/play");
        Assert.Contains(BuiltInIcons.In("Media"), icon => icon.Id == "media/stop");
        Assert.Contains(BuiltInIcons.In("Media"), icon => icon.Id == "media/next");
        Assert.True(BuiltInIcons.In("Smiles").Length >= 8);
        Assert.True(BuiltInIcons.In("Flags").Length >= 20);
        var ids = BuiltInIcons.All.Select(icon => icon.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(BuiltInIcons.In("Flags"), icon => Assert.False(string.IsNullOrWhiteSpace(icon.Spec)));
        Assert.All(BuiltInIcons.All.Where(icon => icon.Category != "Flags"), icon => Assert.Equal("", icon.Spec));
        Assert.Equal("media/play", BuiltInIcons.Canonical("Media/Play"));

        var scene = new Scene { Id = "room", Name = "Room" };
        var element = ElementFactory.Create(ElementKind.Icon, 10, 20, 1, 1);
        var play = BuiltInIcons.Find("media/play")!;
        BuiltInIcons.Style(element, play);
        element.Name = play.Name;
        scene.Elements.Add(element);
        var saved = Assert.Single(ProjectStore.Clone(new ExperienceDocument { StartSceneId = scene.Id, Scenes = { scene } }).Scenes.Single().Elements);
        Assert.Equal(ElementKind.Icon, saved.Kind);
        Assert.Equal("media/play", saved.Icon);
        Assert.Equal("Play", saved.Name);
        Assert.True(ElementFactory.CapturesPointer(saved));

        saved.Icon = "missing/icon";
        var repaired = ProjectStore.Clone(new ExperienceDocument { Scenes = { new Scene { Id = "room", Elements = { saved } } } }).Scenes.Single();
        Assert.Equal(BuiltInIcons.DefaultId, repaired.Elements.Single().Icon);
    }
}
