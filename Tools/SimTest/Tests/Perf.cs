using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Arena;

static partial class Program
{
    /// Frame cost of a heavy wave: Quick Play with a boss's approach (default the Twin Reapers), from wave 7 (a breach
    /// wave: Breaches.Flood times the enemies), a host and a friend; players are kept alive. Prints the host's and the
    /// friend's sim time per frame (FixedStep + Update + Poll) against the enemy and bullet counts.
    /// run.sh 1 1 perf [seconds] [boss enum name] [wave]
    static void PerfReport(float dt, int seconds, string boss, int wave)
    {
        QuickPlay.OrderOverride = new[] { (EnemyType)Enum.Parse(typeof(EnemyType), boss), EnemyType.Boss, EnemyType.HiveQueen };
        int port = nextPort++;
        var host = new Session { StartWave = wave };
        host.Game.ProfileOverride = new Profile();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Game.ProfileOverride = new Profile();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        WaitForLobby(host, client);
        PollBoth(host, client);
        client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game; var c = client.Game;
        string why;
        var rooms = new List<ExpansionRoom>();
        foreach (var box in new[] { new[] { 1600, 100, 800, 600 }, new[] { -900, 100, 800, 600 } })
        {
            var r = Expansion.Custom(rooms, box[0], box[1], box[2], box[3], out why);
            if (r.HasValue) rooms.Add(r.Value);
        }
        foreach (var gg in new[] { g, c }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(rooms); }
        Console.WriteLine("  quick {0}, wave {1}, breach wave {2}", g.Quick, g.Wave, Breaches.IsBreachWave(g.Wave));
        var swH = new Stopwatch(); var swC = new Stopwatch();
        double binH = 0, binC = 0, worstH = 0, worstC = 0; int frames = 0, maxEn = 0, maxB = 0;
        var input = new PlayerInput { AimAt = new V2(640, 360) };
        for (int i = 0; i < seconds * 60; i++)
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.Hp = p.MaxHp; p.Downed = false; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            swH.Restart(); host.Poll(); g.FixedStep(dt, input); g.Update(dt); swH.Stop();
            swC.Restart(); client.Poll(); c.FixedStep(dt, input); c.Update(dt); swC.Stop();
            double h = swH.Elapsed.TotalMilliseconds, cc = swC.Elapsed.TotalMilliseconds;
            binH += h; binC += cc; worstH = Math.Max(worstH, h); worstC = Math.Max(worstC, cc); frames++;
            maxEn = Math.Max(maxEn, g.Enemies.Count); maxB = Math.Max(maxB, g.Bullets.Count);
            if (frames == 120)
            {
                Console.WriteLine("  t {0,5:0}s wave {1,2} {2,-9} enemies {3,3} (max {4,3}) bullets {5,4} (max {6,4}) breaches {7} | host {8,6:0.00} ms avg {9,6:0.0} worst | friend {10,6:0.00} avg {11,6:0.0} worst",
                    i / 60f, g.Wave, g.State, g.Enemies.Count, maxEn, g.Bullets.Count, maxB, g.BreachesOn ? g.ActiveBreaches.Length : 0, binH / frames, worstH, binC / frames, worstC);
                binH = binC = worstH = worstC = 0; frames = 0; maxEn = maxB = 0;
            }
        }
        QuickPlay.OrderOverride = null;
    }
}
