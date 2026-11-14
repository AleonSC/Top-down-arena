using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Survival's balance (Docs/Survival.md, step 6): a duo of bots plays Survival to the end (or a time cap) and each run's
// time survived, phase reached, level, kills and damage are printed, with a summary and the clock's pressure at each minute.
//   run.sh 1 1 survbal [runs] [first seed] [bot skill 1-3] [players 1-3] [cap minutes]
static partial class Program
{
    sealed class SurvBalRun { public float Time, TakenPct; public int Phase, Level, Kills, Downs, MaxLive; public bool Capped; }

    static SurvBalRun PlaySurvival(float dt, int seed, BotSkill skill, int players, float capSeconds)
    {
        Game.SeedForTests(seed);
        var host = new Session { AutoStart = players, StartWave = 1 };
        host.Game.ProfileOverride = new Profile();
        host.Game.Bot = true; host.Game.BotLevel = skill; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        host.SetMode(false, false, false, true);
        var friends = new List<Session>();
        var roles = new[] { Role.Dps, Role.Healer };
        for (int i = 0; i < players - 1; i++)
        {
            var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = skill; c.Game.BotRerolls = false;
            c.Game.ProfileOverride = new Profile();
            c.Join("127.0.0.1", port, "B" + i, roles[i % roles.Length], 0);
            friends.Add(c);
        }
        if (players > 1) WaitForGame(host, friends[0], dt);
        else { host.RequestReady(true); host.StartMatch(); for (int f = 0; f < 600 && !host.Game.Running; f++) Step(host, dt); }
        var g = host.Game;
        var run = new SurvBalRun();
        for (int f = 0; f < 60 * 60 * 90 && g.State != MatchState.GameOver; f++)
        {
            Step(host, dt);
            foreach (var c in friends) Step(c, dt);
            run.MaxLive = Math.Max(run.MaxLive, g.Enemies.Count);
            if (g.SurvTime >= capSeconds) { run.Capped = true; break; }
        }
        run.Time = g.SurvTime;
        run.Phase = g.SurvPhase + 1;
        run.Level = g.Players.Values.Max(p => p.Level);
        run.Kills = g.Players.Values.Sum(p => p.Run.Kills);
        run.Downs = g.Players.Values.Sum(p => p.Run.Downs);
        run.TakenPct = g.Players.Values.Average(p => 100f * p.Run.DamageTaken / Math.Max(1f, p.MaxHp));
        host.Leave(""); foreach (var c in friends) c.Leave("");
        return run;
    }

    static void SurvivalBalanceReport(float dt, int runs, int firstSeed, BotSkill skill, int players, int capMinutes)
    {
        Console.WriteLine("  Survival, {0} bot(s) at skill {1}, {2} runs from seed {3}, cap {4} min.", players, (int)skill, runs, firstSeed, capMinutes);
        Console.WriteLine("  The clock's pressure (Grunts on screen) at 2, 5, 10, 15, 20, 30 min: {0}",
            string.Join("  ", new[] { 2, 5, 10, 15, 20, 30 }.Select(m => Survival.Pressure(m * 60f, players).ToString("0"))));
        var all = new List<SurvBalRun>();
        for (int i = 0; i < runs; i++)
        {
            var r = PlaySurvival(dt, firstSeed + i, skill, players, capMinutes * 60f);
            all.Add(r);
            Console.WriteLine("  seed {0,3}: {1,-8} {2,5:0.0} min  phase {3}  level {4,2}  kills {5,4}  downs {6}  taken {7,4:0}%  most alive {8}", firstSeed + i, r.Capped ? "capped" : "fell", r.Time / 60f, r.Phase, r.Level, r.Kills, r.Downs, r.TakenPct, r.MaxLive);
        }
        var t = all.Select(r => r.Time / 60f).OrderBy(x => x).ToList();
        Console.WriteLine("  time survived: min {0:0.0}  median {1:0.0}  max {2:0.0} min;  {3}/{4} reached the cap;  avg phase {5:0.0}, level {6:0.0}",
            t.First(), t[t.Count / 2], t.Last(), all.Count(r => r.Capped), all.Count, all.Average(r => r.Phase), all.Average(r => r.Level));
    }
}
