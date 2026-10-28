using Intuime.Core;
using Xunit;

namespace Intuime.Tests;

public class SceneWebReturnTests
{
    [Fact]
    public void Entering_a_web_scene_remembers_the_scene_you_left()
    {
        var site = new Scene
        {
            Id = "site",
            Name = "Site",
            Interactions =
            {
                new Interaction
                {
                    Trigger = TriggerKind.SceneEnter,
                    Actions = { new ExperienceAction { Kind = ActionKind.OpenUrl, Text = "https://example.com" } }
                }
            }
        };
        var home = new Scene
        {
            Id = "home",
            Name = "Home",
            Elements =
            {
                new SceneElement
                {
                    Id = "open",
                    Kind = ElementKind.Button,
                    Name = "Open",
                    IsVisible = true,
                    Width = 200,
                    Height = 80,
                    Interactions =
                    {
                        new Interaction
                        {
                            Trigger = TriggerKind.Tap,
                            Actions = { new ExperienceAction { Kind = ActionKind.OpenUrl, Text = "https://example.com/tap" } }
                        }
                    }
                }
            }
        };
        var engine = new PlaybackEngine(new ExperienceDocument
        {
            StartSceneId = "home",
            Scenes = { home, site }
        }, () => new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));

        string? openedFrom = null;
        var openedDuringEnter = false;
        engine.OpenUrlRequested += _ =>
        {
            openedDuringEnter = engine.IsOpeningFromSceneEnter;
            openedFrom = engine.CameFromSceneId;
        };

        engine.Start();
        Assert.Equal("home", engine.Current!.Id);
        Assert.Null(engine.CameFromSceneId);

        engine.RunExternal(new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "site" });
        Assert.Equal("site", engine.Current.Id);
        Assert.Equal("home", engine.CameFromSceneId);
        Assert.True(openedDuringEnter);
        Assert.Equal("home", openedFrom);

        openedDuringEnter = true;
        openedFrom = "stale";
        engine.RunExternal(new ExperienceAction { Kind = ActionKind.GoToScene, TargetId = "home" });
        engine.PointerUp("open");
        Assert.False(openedDuringEnter);
        Assert.Equal("site", openedFrom);
    }
}
