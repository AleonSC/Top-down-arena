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

            // A drill: START DRILL starts Move and Aim for the party, each with their own beacons.
            var hostProfile = host.Game.ProfileOverride;
            host.StartDrill(0);
            WaitForGame(host, client, dt);
            var g = host.Game;
            var c = client.Game;
            Check(g.DrillOn && c.DrillOn && g.Drill == 0 && c.Drill == 0 && g.Mode == GameMode.Training && g.DrillRunners.Count == 2 && client.Screen == AppScreen.Game
                && g.ArenaKind == ArenaLayout.Single && g.Enemies.Count == 0,
                "START DRILL starts the drill on every machine, a runner each, on the classic arena with no enemies");
            var me = g.Players[host.MyId];
            var mine = g.DrillRunners[host.MyId];
            var hurt = typeof(Game).GetMethod("HurtEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool othersBounce = true, stood = true;
            int steps = 0;
            Run(new[] { host, client }, dt, 30f, () => mine.Done, () =>
            {
                if (Training.MoveShootStep(mine.Step))
                {
                    if (mine.TargetId != 0 && g.Enemies.TryGetValue(mine.TargetId, out var tgt))
                    {
                        var start = tgt.Pos;
                        float hp0 = tgt.Hp;
                        hurt.Invoke(g, new object[] { tgt, 9999f, client.MyId });  // the friend's hit: nothing
                        othersBounce &= tgt.Hp == hp0;
                        hurt.Invoke(g, new object[] { tgt, 9999f, host.MyId });
                    }
                }
                else
                    me.Pos = Training.MoveBeacon(mine.Step, Game.Width, Game.Height);
                steps = Math.Max(steps, mine.Step);
                stood &= me.Hp == me.MaxHp;
            });
            Run(new[] { host, client }, dt, 0.5f);
            var seen = c.DrillRunners.TryGetValue(host.MyId, out var cs) ? cs : null;
            Check(mine.Done && mine.Step == Training.MoveSteps && mine.Time > 0f && othersBounce && seen != null && seen.Done && Math.Abs(seen.Time - mine.Time) < 0.5f
                && !c.DrillRunners[client.MyId].Done,
                "the host reaches the three beacons and brings down its two targets (the friend's hits don't count); the friend sees the finish and is still on its own");
            Check(hostProfile.DrillBestTime("move") > 0f && Math.Abs(hostProfile.DrillBestTime("move") - mine.Time) < 0.05f && client.Game.ProfileOverride.DrillBestTime("move") == 0f,
                "the finish is kept as the host's best time, and not the friend's");
            host.StartDrill(0);
            Run(new[] { host, client }, dt, 0.5f);
            Check(g.DrillOn && !g.DrillRunners[host.MyId].Done && g.DrillRunners[host.MyId].Step == 0 && hostProfile.DrillBestTime("move") > 0f,
                "Retry starts the drill over and keeps the best time");
            host.ReturnToLobby();
            WaitForLobby(host, client);
            Check(!g.DrillOn && !c.DrillOn && host.Screen == AppScreen.Lobby && client.Screen == AppScreen.Lobby && host.TrainingMode,
                "Back ends the drill to the lobby, still in Training");
            host.Leave(""); client.Leave("");
        }
        finally
        {
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
