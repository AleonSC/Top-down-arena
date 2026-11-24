using System;
using System.IO;
using System.Linq;
using Arena;

// Training (Core/Training.cs, Docs/Training.md): a lobby mode whose drills start from its page, never from START;
// each drill's best time is kept on the profile and earns a medal.
static partial class Program
{
    static void TrainingTest(float dt)
    {
        var drills = Training.Drills;
        Check(drills.Length > 0 && drills.All(d => !string.IsNullOrEmpty(d.Key) && d.Keys.Length > 0 && d.Gold > 0f && d.Gold < d.Silver && Training.Groups.Contains(d.Group))
            && drills.Select(d => d.Key).Distinct().Count() == drills.Length,
            "every drill has a key of its own, its keys, a group on the page and a gold time under its silver");
        var dd = drills[0];
        Check(Training.Medal(dd, 0f) == 0 && Training.Medal(dd, dd.Gold) == 3 && Training.Medal(dd, dd.Silver) == 2 && Training.Medal(dd, dd.Silver + 1f) == 1,
            "a best time earns gold under the gold time, silver under the silver, bronze for finishing, nothing when never finished");

        var p = new Profile();
        p.DrillDone("move", 30f); p.DrillDone("move", 12f); p.DrillDone("move", 20f);
        var back = Profile.Parse(p.ToText());
        Check(p.DrillBestTime("move") == 12f && back.DrillBestTime("move") == 12f && back.DrillBestTime("dodge") == 0f,
            "a drill keeps its best time, and it's saved with the profile");

        var row = ModeDef.For(GameMode.Training);
        Check(row.Mode == GameMode.Training && ModeDef.FromKey("training") == GameMode.Training && !row.Records && !row.Saves,
            "Training has a row in the mode table: not saved, not recorded as a run");

        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-training-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        try
        {
            int port = nextPort++;
            var host = new Session();
            host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            WaitForLobby(host, client);
            host.SetMode(GameMode.Training);
            PollBoth(host, client);
            Check(host.TrainingMode && client.TrainingMode, "the host picks Training and the friend's lobby shows it");
            client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
            host.StartMatch();
            PollBoth(host, client);
            Check(host.Screen == AppScreen.Lobby && client.Screen == AppScreen.Lobby && !host.Game.InRun,
                "START in Training starts no run (a drill starts from its page)");
            host.Leave(""); client.Leave("");
        }
        finally
        {
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
