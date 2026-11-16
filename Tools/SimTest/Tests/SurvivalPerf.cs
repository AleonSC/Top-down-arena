using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Arena;

// Survival's frame cost: two bots (kept alive) play Survival with the clock started late, so the field fills to the
// cap; every 5 s prints the host's and the friend's sim ms a frame against enemies, bullets, props and particles.
//   run.sh 1 1 survperf [seconds] [start clock min]
static partial class Program
{
    static void SurvivalPerfReport(float dt, int seconds, int startMin)
    {
        Game.SeedForTests(3);
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.ProfileOverride = new Profile(); host.Game.Bot = true;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        host.SetMode(GameMode.Survival);
        var c = new Session(); c.Game.Bot = true; c.Game.ProfileOverride = new Profile();
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        WaitForGame(host, c, dt);
        var g = host.Game;
        for (int f = 0; f < 600 && !g.SurvivalOn; f++) { Step(host, dt); Step(c, dt); }
        for (int f = 0; f < 600 && g.Surv == null; f++) { Step(host, dt); Step(c, dt); }
        g.Surv.Time = startMin * 60f;
        var swH = new Stopwatch(); var swC = new Stopwatch();
        double binH = 0, binC = 0, worstH = 0, worstC = 0; int frames = 0;
        for (int i = 0; i < seconds * 60; i++)
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.Hp = p.MaxHp; p.Downed = false; }
            swH.Restart(); Step(host, dt); swH.Stop();
            swC.Restart(); Step(c, dt); swC.Stop();
            double h = swH.Elapsed.TotalMilliseconds, cc = swC.Elapsed.TotalMilliseconds;
            binH += h; binC += cc; worstH = Math.Max(worstH, h); worstC = Math.Max(worstC, cc); frames++;
            if (frames == 300)
            {
                Console.WriteLine("  clock {0,5:0}s {1,-12} enemies {2,3} bullets {3,4} props {4,3} patches {5,3} | host {6,6:0.00} ms avg {7,6:0.0} worst | friend {8,6:0.00} avg {9,6:0.0} worst",
                    g.SurvTime, g.State, g.Enemies.Count, g.Bullets.Count, g.Props.Count, g.Patches.Count, binH / frames, worstH, binC / frames, worstC);
                binH = binC = worstH = worstC = 0; frames = 0;
            }
        }
        host.Leave(""); c.Leave("");
    }
}
