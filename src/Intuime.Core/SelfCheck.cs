namespace Intuime.Core;

public static class SelfCheck
{
    public static int Run()
    {
        var document = SampleExperiences.CreateLumenGallery();
        var now = DateTime.UtcNow;
        var engine = new PlaybackEngine(document, () => now);
        engine.Start();
        if (engine.Current?.Id != "attract")
            return Fail("start scene");
        engine.BackgroundTap();
        if (engine.Current?.Id != "home")
            return Fail("background tap");
        engine.PointerUp("card-hours");
        if (engine.Current?.Id != "hours")
            return Fail("card navigation");
        engine.SelectDataItem("talks", 1);
        if (TextTemplates.Get(engine.Live, "talk") != "13:00  Gallery talk")
            return Fail("data list");
        engine.PointerUp("hours-back");
        engine.PointerUp("card-visit");
        engine.NudgeCounter("party-counter", 1);
        if (TextTemplates.Get(engine.Live, "partySize") != "2")
            return Fail("counter");
        var roundTrip = ProjectStore.Deserialize(ProjectStore.Serialize(document));
        if (roundTrip.Scenes.Count != document.Scenes.Count)
            return Fail("round trip");
        Console.WriteLine("Intuime self-check passed.");
        return 0;
    }

    private static int Fail(string step)
    {
        Console.Error.WriteLine("Intuime self-check failed: " + step);
        return 1;
    }
}
