using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Arena;

static partial class Program
{
    /// A breach wave's crowd as the game plays it: timed spawning on, the Campaign's first approach (the Warden's), a big
    /// grid map (six breaches), two bots fighting. Every 4 s: alive enemies by type, what the alive cap counts (Foes), the
    /// planted ones, and the host's sim ms a frame.
    /// run.sh 1 1 breachcrowd [seconds] [wave]
    static void BreachCrowdReport(float dt, int seconds, int wave)
    {
        TimedWaves.Enabled = true;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = wave }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForGame(host, client, dt);
        var g = host.Game; var c = client.Game;
        foreach (var gg in new[] { g, c }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Bot = true; }
        UseMap(new[] { g, c }, GridMap(PieceShape.TriI, PieceShape.TetT, PieceShape.TetL, PieceShape.TriL, PieceShape.TetO, PieceShape.Domino, PieceShape.TetS));
        var sw = new Stopwatch(); double bin = 0, worst = 0; int frames = 0, maxEn = 0;
        var input = new PlayerInput { AimAt = new V2(640, 360) };
        for (int i = 0; i < seconds * 60; i++)
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.Hp = p.MaxHp; p.Downed = false; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            sw.Restart(); host.Poll(); g.FixedStep(dt, input); g.Update(dt); sw.Stop();
            client.Poll(); c.FixedStep(dt, input); c.Update(dt);
            double ms = sw.Elapsed.TotalMilliseconds; bin += ms; worst = Math.Max(worst, ms); frames++;
            maxEn = Math.Max(maxEn, g.Enemies.Count);
            if (frames == 240)
            {
                var by = g.Enemies.Values.GroupBy(e => e.Type).OrderByDescending(q => q.Count()).Select(q => q.Key + " " + q.Count());
                Console.WriteLine("  t {0,4:0}s wave {1} {2,-12} breaches {3} alive {4,3} (max {5,3}) foes {6,3} | host {7,5:0.00} ms avg {8,5:0.0} worst | {9}",
                    i / 60f, g.Wave, g.State, g.BreachesOn ? g.ActiveBreaches.Length : 0, g.Enemies.Count, maxEn, g.Foes(), bin / frames, worst, string.Join(", ", by));
                bin = worst = 0; frames = 0; maxEn = 0;
            }
        }
        TimedWaves.Enabled = false;
    }
}
