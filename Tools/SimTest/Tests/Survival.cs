using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arena;

// Survival mode's spawner (Survival.cs, Docs/Survival.md): the curves, the faction phases, the cap, the squads, determinism.
static partial class Program
{
    // Runs the spawner for `seconds`; the "field" kills `killPerSec` Grunts' worth of threat a second (oldest first).
    static List<(float t, SurvivalSpawn s)> SurvivalRun(Survival sv, float seconds, float killPerSec, out int maxLive, out float maxThreat)
    {
        var all = new List<(float, SurvivalSpawn)>();
        var live = new List<float>();
        maxLive = 0; maxThreat = 0f;
        const float dt = 1f / 30f;
        for (float t = 0f; t < seconds; t += dt)
        {
            float kill = killPerSec * dt;
            while (live.Count > 0 && kill > 0f) { float c = live[0]; if (c <= kill) { kill -= c; live.RemoveAt(0); } else { live[0] -= kill; kill = 0f; } }
            foreach (var s in sv.Step(dt, live.Sum(), live.Count)) { live.Add(Survival.Cost(s.Type)); all.Add((sv.Time, s)); }
            maxLive = Math.Max(maxLive, live.Count); maxThreat = Math.Max(maxThreat, live.Sum());
        }
        return all;
    }

    static void SurvivalTest()
    {
        // Records: longest run overall and per class, the furthest phase, saved and read back; not a Campaign run.
        var rec = new Profile();
        rec.SurvivalEnded("Knight Tank", 300f, 2);
        rec.SurvivalEnded("Knight Tank", 200f, 1);
        rec.SurvivalEnded("Elementalist DPS", 450f, 3);
        Check(rec.SurvivalPlays == 3 && rec.SurvivalBestTime == 450f && rec.SurvivalBestPhase == 3 && rec.SurvivalBest("Knight Tank") == 300f && rec.SurvivalBest("Elementalist DPS") == 450f && rec.SurvivalBest("Healer") == 0f, "records: plays, best time overall and per class, best phase");
        var back = Profile.Parse(rec.ToText());
        Check(back.SurvivalPlays == 3 && back.SurvivalBestTime == 450f && back.SurvivalBestPhase == 3 && back.SurvivalBest("Knight Tank") == 300f && back.SurvivalBest("Elementalist DPS") == 450f, "records survive saving and reading back (class names with spaces too)");
        Check(rec.RunsWon == 0 && rec.RunsLost == 0 && rec.BestWave == 0, "a Survival run is not a Campaign run");

        // The curves.
        bool rising = true;
        for (float t = 0f; t < 3600f; t += 10f) if (Survival.Pressure(t + 10f) < Survival.Pressure(t) || Survival.EliteShare(t + 10f) < Survival.EliteShare(t) || Survival.WaveAt(t + 10f) < Survival.WaveAt(t)) rising = false;
        Check(rising, "pressure, the Elite share and the wave scale never fall as the clock runs");
        Check(Survival.WaveAt(0f) == 1 && Survival.WaveAt(900f) == 31, "the scale starts at wave 1 and is wave 31 at 15 minutes");
        Check(Survival.Pressure(900f) - Survival.Pressure(600f) > Survival.Pressure(600f) - Survival.Pressure(300f), "pressure is steeper after ten minutes");
        Check(Survival.EliteShare(0f) == 0f && Survival.EliteShare(99999f) == 0.4f, "the Elite share starts at 0 and stops at 40%");
        Check(Survival.Pressure(100f, 2) >= Survival.Pressure(100f, 1) * 1.45f && Survival.Pressure(100f, 3) > Survival.Pressure(100f, 2), "more players, more pressure (1.5x, 2x)");
        Check(Survival.Cost(EnemyType.Grunt) == 1f && Survival.Cost(EnemyType.Brute) > Survival.Cost(EnemyType.Swarmer), "a Grunt costs 1, a Brute more than a Swarmer");

        // Phases: the basic faction alone, then one more every four minutes.
        Check(Survival.PhaseAt(0f) == 0 && Survival.PhaseAt(239f) == 0 && Survival.PhaseAt(240f) == 1 && Survival.PhaseAt(480f) == 2 && Survival.PhaseAt(99999f) == 4, "a faction phase opens every 240 s (up to 4)");
        Check(Survival.FactionsAt(0f).Count == 1 && Survival.FactionsAt(0f)[0] == BasicFaction.Def && Survival.FactionsAt(1000f).Count == 5, "the basic faction first, four more by the end");

        // A run.
        var sv = new Survival(7, 1);
        var run = SurvivalRun(sv, 900f, 1.2f, out int maxLive, out float maxThreat);
        Check(!run.Any(r => r.t < Survival.Grace), "nothing spawns in the first 3 s");
        Check(run.Count > 200, "a fifteen-minute run spawns plenty (" + run.Count + ")");
        Check(maxLive <= Survival.MaxLive, "never more than " + Survival.MaxLive + " alive (" + maxLive + ")");
        Check(maxThreat <= Survival.Pressure(900f) + 6f * 6f + 30f, "the live threat stays near the budget (peak " + maxThreat.ToString("0") + ")");
        Check(!run.Any(r => Game.IsRetired(r.s.Type)), "no retired enemy is ever spawned");
        Check(!run.Any(r => r.t < Survival.RangedFrom && !Defs.Get(r.s.Type).Melee), "the first two minutes are melee only: no shooter, alone or in a squad");
        Check(run.Any(r => r.t >= Survival.RangedFrom + Survival.RangedRamp && !Defs.Get(r.s.Type).Melee), "shooters join after that");
        Check(Survival.Pressure(0f) < 0.5f * Survival.Pressure(120f) && Survival.Pressure(120f) < Survival.Pressure(300f), "the budget starts slow: under half of its two-minute level at the start");
        var early = run.Where(r => r.t < 240f && r.s.Squad == null).Select(r => r.s.Type).Distinct().ToList();
        Check(early.All(t => BasicFaction.Types.Contains(t)), "before four minutes only the basic faction's enemies come as singles");
        var wardenOnly = Approaches.For(EnemyType.Boss).Members.Select(m => m.Type).Where(t => !BasicFaction.Types.Contains(t)).ToList();
        Check(run.Any(r => r.t >= 240f && wardenOnly.Contains(r.s.Type)) && !run.Any(r => r.t < 240f && wardenOnly.Contains(r.s.Type)), "the Warden's enemies join at four minutes, not before");
        var squads = run.Where(r => r.s.Squad != null).Select(r => r.t).ToList();
        Check(squads.Count > 0 && run.Where(r => r.s.Squad != null).Select(r => r.s.Squad).Distinct().Count() >= 3, "squads arrive on the timer, in a few kinds");
        Check(run.Any(r => r.s.Elite) && !run.Any(r => r.t < 10f && r.s.Elite), "Elites show up as the clock runs, none in the first seconds");
        Check(run.All(r => r.s.Wave == Survival.WaveAt(r.t) || r.s.Wave == Survival.WaveAt(r.t - 0.1f)), "each spawn scales with the wave of its moment");

        // A party that cannot kill anything is not buried: the cap holds.
        var stuck = SurvivalRun(new Survival(3, 3), 600f, 0f, out int stuckLive, out _);
        Check(stuckLive <= Survival.MaxLive, "with nobody killing, the field still stops at the cap (" + stuckLive + ")");

        // Same seed, same run; another seed, another one.
        var again = SurvivalRun(new Survival(7, 1), 900f, 1.2f, out _, out _);
        var other = SurvivalRun(new Survival(8, 1), 900f, 1.2f, out _, out _);
        Check(again.Count == run.Count && again.Zip(run, (a, b) => a.s.Type == b.s.Type && a.t == b.t && a.s.Elite == b.s.Elite).All(x => x), "the same seed gives the same run");
        Check(other.Count != run.Count || other.Zip(run, (a, b) => a.s.Type == b.s.Type).Contains(false), "another seed gives another run");

        // Killing faster brings more.
        var slow = SurvivalRun(new Survival(7, 1), 600f, 0.5f, out _, out _);
        var fast = SurvivalRun(new Survival(7, 1), 600f, 3f, out _, out _);
        Check(fast.Count > slow.Count, "killing faster brings more enemies (" + slow.Count + " at 0.5/s, " + fast.Count + " at 3/s)");
    }
}

static partial class Program
{
    // Survival played over the network: the mode flag, the clock, spawns, the pick freeze, the wipe.
    static void SurvivalNetTest(float dt)
    {
        int port = nextPort++;
        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-survival-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        var host = new Session();
        host.Game.ProfileOverride = new Profile();
        host.Game.PropsOn = true;
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Game.ProfileOverride = new Profile();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForLobby(host, client);
        PollBoth(host, client);
        host.SetMode(false, false, false, true);
        PollBoth(host, client);
        Check(host.SurvivalMode && client.SurvivalMode && !host.QuickMode && !host.HuntMode, "the lobby's Survival mode shows in the friend's lobby");
        client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game; var c = client.Game;
        Check(g.SurvivalOn && c.SurvivalOn && g.Hunt == null && !g.Quick, "both games play a Survival run");
        Run(s, dt, 60f, () => g.Surv != null && g.State == MatchState.Fighting && g.SurvTime > 8f);
        Check(g.Surv != null && g.State == MatchState.Fighting, "the countdown ends and the clock starts: Fighting with a spawner");
        int live = g.Enemies.Count;
        Check(live > 0, "enemies are coming in with no wave (" + live + ")");
        Run(s, dt, 1f);
        Check(c.SurvTime > 8f && Math.Abs(c.SurvTime - g.SurvTime) < 1.5f, "the clock reaches the friend through the snapshot (" + c.SurvTime.ToString("0.0") + " s)");
        Check(g.Wave == Survival.WaveAt(g.SurvTime), "enemies scale with the clock");

        // The big room: the same world on both machines, four times the arena, walls and props in it, the camera held inside it.
        Check(g.RoomsOn && c.RoomsOn && g.CurrentWorld == SurvivalRoom.World && c.CurrentWorld == SurvivalRoom.World, "both games play in Survival's big room");
        Check(g.ArenaMax.X == Game.Width * 2f && g.ArenaMax.Y == Game.Height * 2f && g.ArenaMin.X == 0f, "the room is 3200 x 1800: four times today's arena");
        var vc = g.CurrentWorld.ViewCentre(new V2(100f, 100f), 800f, 450f, false);
        var vm = g.CurrentWorld.ViewCentre(new V2(1600f, 900f), 800f, 450f, false);
        Check(vc.X == 800f && vc.Y == 450f && vm.X == 1600f && vm.Y == 900f, "the camera follows you and stops at the room's edges");
        Check(g.Props.Count >= 120 && g.Props.Values.Count(pr => pr.Kind == PropKind.Crate || pr.Kind == PropKind.HeavyCrate) >= 90, "the room is full of crates (" + g.Props.Count + " props)");
        var layout = g.ActiveLayout;
        Check(layout != null && layout.Solids.Length >= 4 + 4 + 8, "walls round the room and in the middle and corners (" + (layout == null ? 0 : layout.Solids.Length) + " pieces)");
        Check(g.Players.Values.All(pr => pr.Pos.X > 1000f && pr.Pos.X < 2200f && pr.Pos.Y > 500f && pr.Pos.Y < 1300f && !layout.Solids.Any(so => Layouts.Inside(so, pr.Pos, 20f))), "everyone starts near the middle of the room, clear of the walls");
        foreach (var pr in g.Players.Values) pr.InvulnT = 1000f;
        var seen = new System.Collections.Generic.HashSet<int>(g.Enemies.Keys);
        int newSpawns = 0, inView = 0, inWalls = 0;
        Run(s, dt, 40f, null, () =>
        {
            foreach (var e in g.Enemies.Values)
                if (seen.Add(e.Id))
                {
                    newSpawns++;
                    if (g.Players.Values.Any(pr => Math.Abs(e.Pos.X - pr.Pos.X) < 800f && Math.Abs(e.Pos.Y - pr.Pos.Y) < 450f)) inView++;
                    if (layout.Solids.Any(so => Layouts.Inside(so, e.Pos, 0f))) inWalls++;
                }
            foreach (var pr in g.Players.Values) { pr.Pos = pr.Disp = new V2(1300f, 700f); pr.Hp = pr.MaxHp; }  // (stand still in the middle)
        });
        Check(newSpawns > 5 && inView == 0, "enemies come in outside everyone's view (" + newSpawns + " spawned, " + inView + " in view)");
        Check(inWalls == 0, "and never inside a wall");

        // A level-up pick freezes the world until it is made.
        foreach (var p in g.Players.Values) { p.InvulnT = 100f; }
        var me = g.Me;
        me.PendingPicks = 1;
        Run(s, dt, 2f, () => g.State == MatchState.Intermission && me.Offer != null);
        Check(g.State == MatchState.Intermission && me.Offer != null, "a pending pick holds the match and offers cards");
        float t0 = g.SurvTime;
        var pos = g.Enemies.Values.Select(e => e.Pos).ToList();
        Run(s, dt, 1.5f);
        int moved = g.Enemies.Values.Select(e => e.Pos).Take(pos.Count).Zip(pos, (a, b) => Math.Abs(a.X - b.X) > 1f || Math.Abs(a.Y - b.Y) > 1f).Count(x => x);  // (an enemy still arriving finishes its entrance)
        Check(Math.Abs(g.SurvTime - t0) < 0.001f && moved <= 2, "the clock and the enemies stand still while it is held (" + moved + " of " + pos.Count + " moved)");
        g.PickUpgrade(0);
        Run(s, dt, 3f, () => g.State == MatchState.Fighting);
        Check(g.State == MatchState.Fighting && me.PendingPicks == 0, "after the pick the fight goes on");
        Run(s, dt, 2f);
        Check(g.SurvTime > t0 + 1f, "and the clock runs again");

        // Everyone down ends the run: no wave to clear, no win.
        foreach (var p in g.Players.Values) { p.InvulnT = 0f; p.Hp = 0f; p.Downed = true; p.ReviveToken = false; p.SecondWind = false; }
        Run(s, dt, 3f, () => g.State == MatchState.GameOver);
        Check(g.State == MatchState.GameOver, "everyone down ends the run");
        Run(s, dt, 1f);
        var hprof = g.Profile;
        Check(hprof.SurvivalPlays == 1 && hprof.SurvivalBestTime > 8f && hprof.RunsLost == 0 && hprof.BestWave == 0, "the run is recorded as a Survival run (time " + hprof.SurvivalBestTime.ToString("0") + " s), not a Campaign one");
        Check(hprof.Recent.Count > 0 && hprof.Recent[0].Mode == "Survival", "it has a Recent runs card");
        Profile.Folder = wasFolder;
        try { Directory.Delete(dir, true); } catch { }
    }
}
