using System;
using System.IO;
using System.Linq;
using Arena;

// Records > Recent runs: the run's curve (RunTimeline.cs, Docs/RecentRuns.md).
static partial class Program
{
    static RunTimeline FakeTimeline()
    {
        var t = new RunTimeline { Won = true, Wave = 3, Seconds = 90f, Mode = "Endless", Class = "Knight Tank", Diff = "Normal", Mods = "Tough Crowd|Nightfall" };
        int a = t.PlayerIndex(1, "Anthony Wyatt", true);
        for (int i = 0; i <= 6; i++)
        {
            var s = t.Begin(i * 15f, 1 + i / 2, i == 5);
            var r = new RunStats { DamageDealt = i * 1500f, DamageTaken = i * 100f, HealingDone = i * 10f, Kills = i * 7, Downs = i >= 4 ? 1 : 0, ShotsFired = i * 100, ShotsHit = i * 100 / 2 };
            t.Set(s, a, r, 100f - i * 10f);
            if (i == 3)
                t.PlayerIndex(2, "Mira", false);  // joins after the third sample
            if (i >= 3)
                t.Set(s, 1, new RunStats { DamageDealt = (i - 3) * 800f }, 80f);
        }
        return t;
    }

    static void RecentRunsTest(float dt)
    {
        // Rates are differences between samples; Hp is a level.
        var t = FakeTimeline();
        var dps = t.Rates(0, TL.Dealt);
        Check(dps.Length == 7 && dps[0] == 0f && Math.Abs(dps[1] - 100f) < 0.01f && Math.Abs(dps[6] - 100f) < 0.01f, "damage per second is the difference between samples over their gap");
        Check(Math.Abs(t.Total(0, TL.Dealt) - 9000f) < 0.01f && Math.Abs(t.Series(0, TL.Hp)[3] - 70f) < 0.01f, "totals are the last sample, health is read as it was");
        Check(t.DownSamples(0).SequenceEqual(new[] { 4 }), "the sample where a player went down is found");
        Check(t.Samples[1].Get(1, TL.Dealt) == 0f && Math.Abs(t.Total(1, TL.Dealt) - 2400f) < 0.01f, "a player who joined later reads as zero before they came");
        var acc = t.AccuracyPerWave(0);
        Check(acc.Length == 4 && acc.All(v => Math.Abs(v - 50f) < 0.01f), "accuracy per wave is hits over shots in each wave");

        // The text reads back the same, including names and modifiers with spaces.
        var back = RunTimeline.Parse(t.ToText());
        Check(back != null && back.Samples.Count == 7 && back.Players.Count == 2 && back.Players[0].Name == "Anthony Wyatt" && back.Players[0].Mine && back.Mods == "Tough Crowd|Nightfall"
            && back.Won && back.Wave == 3 && Math.Abs(back.Seconds - 90f) < 0.01f && back.Class == "Knight Tank" && back.Mode == "Endless", "a timeline's text reads back the same");
        Check(back != null && back.Samples[1].V.Length == 1 && back.Samples[5].Get(1, TL.Dealt) == 1600f && back.Samples[5].Boss && !back.Samples[4].Boss, "a late joiner and the boss flag survive saving");
        string text = t.ToText();
        Check(RunTimeline.Parse(text.Substring(0, text.Length / 2)) == null, "a cut-off file is refused");
        Check(RunTimeline.Parse(text.Replace("runlog 1", "runlog 99")) == null, "a newer format is refused");
        Check(RunTimeline.Parse("garbage") == null, "a file that isn't a timeline is refused");

        // The profile's log line carries the run's summary, and older lines still load.
        var pr = new Profile();
        pr.Recent.Add(new RankLog { Wave = 12, Result = 0, Xp = 500, MultPct = 120, Mods = "Hard", Class = "Knight Tank", Mode = "Endless", Seconds = 640.5f, Dealt = 81234f, Taken = 9000f, Heal = 1200f, Players = 2, File = "20260101-120000-ab12" });
        pr.Recent.Add(new RankLog { Wave = 5, Result = 1, Xp = 100, MultPct = 100 });
        var pb = Profile.Parse(pr.ToText());
        Check(pb.Recent.Count == 2 && pb.Recent[0].Class == "Knight Tank" && pb.Recent[0].Mode == "Endless" && Math.Abs(pb.Recent[0].Seconds - 640.5f) < 0.01f && pb.Recent[0].Players == 2
            && pb.Recent[0].File == "20260101-120000-ab12" && Math.Abs(pb.Recent[0].Dealt - 81234f) < 1f && pb.Recent[1].File == "" && pb.Recent[1].Class == "", "the profile's run log keeps class, mode, time, damage and the curve's file");
        var old = Profile.Parse("arena-profile 5\nrlog 7 1 300 150 Tough_Crowd\nend 1\n");
        Check(old.Recent.Count == 1 && old.Recent[0].Wave == 7 && old.Recent[0].Mods == "Tough Crowd" && old.Recent[0].File == "", "an older run log line (no summary) still loads");

        // Files: saved, loaded, trimmed to the newest few.
        string folder = Path.Combine(Path.GetTempPath(), "arena-runs-test-" + Guid.NewGuid().ToString("N"));
        string was = Profile.Folder;
        Profile.Folder = folder;
        try
        {
            var one = FakeTimeline();
            one.Id = "20260101-000000-aaaa";
            Check(RunTimelineStore.Save(one, 3) == one.Id && RunTimelineStore.Load(one.Id) != null && RunTimelineStore.Load(one.Id).Samples.Count == 7, "a timeline is saved and loaded by its id");
            Check(RunTimelineStore.Load("../profile") == null && RunTimelineStore.Load("nothing") == null && RunTimelineStore.Load("") == null, "a bad id or a missing file loads as nothing");
            for (int i = 1; i <= 4; i++)
            {
                var more = FakeTimeline();
                more.Id = "2026010" + (i + 1) + "-000000-bbbb";
                RunTimelineStore.Save(more, 3);
            }
            var files = Directory.GetFiles(Path.Combine(folder, "runs"), "*.run").Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Check(files.Length == 3 && files[0].StartsWith("20260103") && RunTimelineStore.Load(one.Id) == null, "only the newest runs' curves are kept");
            var tiny = new RunTimeline();
            tiny.PlayerIndex(1, "x", true);
            tiny.Begin(0f, 1, false);
            Check(RunTimelineStore.Save(tiny, 3) == "", "a run with fewer than two samples isn't kept");
        }
        finally
        {
            Profile.Folder = was;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }

        // A real run: the sampler runs in every game and the end keeps the curve; the profile points at it.
        string folder2 = Path.Combine(Path.GetTempPath(), "arena-runs-test-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = folder2;
        try
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 1 };
            var hp = host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            var cp = client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game;
            Run(s, dt, 26f, () => false, () => { foreach (var pl in g.Players.Values) pl.InvulnT = 1f; });
            foreach (var pl in g.Players.Values) { pl.Downed = true; pl.ReviveToken = false; }
            typeof(Game).GetMethod("CheckGameOver", flags).Invoke(g, null);
            Run(s, dt, 1f, () => cp.RunsLost > 0 && hp.RunsLost > 0);
            var log = cp.Recent.Count > 0 ? cp.Recent[0] : null;
            var tl = log == null ? null : RunTimelineStore.Load(log.File);
            Console.WriteLine("  client's run: {0}", tl == null ? "no curve" : tl.Samples.Count + " samples, " + tl.Players.Count + " players, " + tl.Seconds.ToString("0") + " s");
            Check(log != null && log.File.Length > 0 && log.Mode.Length > 0 && log.Class.Length > 0 && log.Seconds > 20f && log.Players == 2, "the run's end fills the log with class, mode, length and players");
            Check(tl != null && tl.Samples.Count >= 3 && tl.Players.Count == 2 && tl.Players.Count(p => p.Mine) == 1 && tl.Players[tl.Mine].Name == "Friend" && !tl.Won, "the client's curve has samples for the whole party and knows which player is them");
            Check(tl != null && tl.Samples.All(x => x.T >= 0f) && tl.Samples.Zip(tl.Samples.Skip(1), (a, b) => b.T > a.T).All(x => x), "sample times only go forward");
            var hl = hp.Recent.Count > 0 ? RunTimelineStore.Load(hp.Recent[0].File) : null;
            Check(hl != null && hl.Players[hl.Mine].Name == "Host" && hl.Samples.Count >= 3, "the host keeps its own curve too");
            host.Leave(""); client.Leave("");

            // Quitting mid-run still lands in Recent runs (result 4, no XP, with its curve).
            var qh = new Session { AutoStart = 2, StartWave = 1 };
            qh.Host(nextPort++, "Host", Role.Tank);
            var qc = new Session();
            var qp = qc.Game.ProfileOverride = new Profile();
            qc.Join("127.0.0.1", qh.Port, "Quitter", Role.Dps);
            var qs = new[] { qh, qc };
            WaitForGame(qh, qc, dt);
            Run(qs, dt, 25f, () => false, () => { foreach (var pl in qh.Game.Players.Values) pl.InvulnT = 1f; });
            var xpBefore = qp.Xp;
            qc.Leave(""); qh.Leave("");
            var ql = qp.Recent.Count > 0 ? qp.Recent[0] : null;
            Check(ql != null && ql.Result == 4 && ql.Xp == 0 && qp.Xp == xpBefore && ql.Seconds > 20f && ql.Class.Length > 0, "a run you quit is logged as quit, with no XP");
            Check(ql != null && RunTimelineStore.Load(ql.File) != null, "a run you quit keeps its curve");
            Check(Profile.Parse(qp.ToText()).Recent[0].Result == 4, "a quit run's log line reads back");
        }
        finally
        {
            Profile.Folder = was;
            try { Directory.Delete(folder2, true); } catch (IOException) { }
        }
    }
}
