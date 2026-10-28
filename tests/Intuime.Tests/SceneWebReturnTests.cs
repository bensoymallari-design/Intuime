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

    [Fact]
    public void A_button_or_icon_replaces_the_corner_arrow()
    {
        var plain = new Scene
        {
            Id = "plain",
            Elements = { new SceneElement { Id = "title", Kind = ElementKind.Text, IsVisible = true } }
        };
        Assert.False(SiteControls.HasOwn(plain));

        plain.Elements.Add(new SceneElement { Id = "back", Kind = ElementKind.Icon, IsVisible = true });
        Assert.True(SiteControls.HasOwn(plain));

        var hidden = new SceneElement { Id = "hidden", Kind = ElementKind.Button, IsVisible = false };
        Assert.False(SiteControls.IsOwn(hidden));
        Assert.True(SiteControls.IsOwn(new SceneElement { Id = "go", Kind = ElementKind.Button, IsVisible = true }));
    }

    [Fact]
    public void The_site_browser_has_no_window_buttons()
    {
        var chrome = SiteLaunch.Arguments("chrome.exe", "https://example.com", @"C:\temp\profile", 10, 20, 800, 600);
        Assert.Contains("--kiosk", chrome);
        Assert.Contains("https://example.com", chrome);
        Assert.DoesNotContain(chrome, arg => arg.StartsWith("--app=", StringComparison.Ordinal));
        Assert.Contains("--window-position=10,20", chrome);
        Assert.Contains("--window-size=800,600", chrome);

        var edge = SiteLaunch.Arguments(@"C:\Program Files\Microsoft\Edge\Application\msedge.exe", "https://example.com", "profile", 0, 0, 1920, 1080);
        Assert.Contains("--kiosk", edge);
        Assert.Contains("--edge-kiosk-type=fullscreen", edge);
        Assert.DoesNotContain(edge, arg => arg.StartsWith("--app=", StringComparison.Ordinal));
        Assert.Equal("https://example.com", edge[^1]);

        var firefox = SiteLaunch.Arguments("firefox", "https://example.com", "profile", 0, 0, 100, 100);
        Assert.Contains("-kiosk", firefox);
        Assert.DoesNotContain("-new-window", firefox);
        Assert.Equal("https://example.com", firefox[^1]);
    }
}
