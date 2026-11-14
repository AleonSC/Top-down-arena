using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The hunt map's balance (Docs/HuntMap.md, step 6): a duo of Good bots plays the same maps along different routes (a safe one, an
// Elite one, a random one), and each run's outcome, time, damage taken and build are printed, with a summary per route.
//   run.sh 1 1 huntbal [maps] [first seed] [bot skill 1-3]
static partial class Program
{
    sealed class HuntBalRun
    {
        public string Route = "", Ending = "";
        public bool Won;
        public float Time, TakenPct, Clues;
        public int Wave, Downs, Level, Elites, Fights;
    }

    // How a bot ranks a node when it votes: the higher the better.
    static readonly Dictionary<string, Func<HuntNodeType, int>> huntPolicies = new Dictionary<string, Func<HuntNodeType, int>>
    {
        { "safe",   t => t == HuntNodeType.Rest ? 9 : t == HuntNodeType.Shop ? 8 : t == HuntNodeType.Skirmish ? 7 : t == HuntNodeType.Contraband ? 5 : t == HuntNodeType.Gamble ? 3 : t == HuntNodeType.Ambush ? 2 : 0 },
        { "elite",  t => t == HuntNodeType.Elite ? 9 : t == HuntNodeType.Contraband ? 8 : t == HuntNodeType.Ambush ? 7 : t == HuntNodeType.Gamble ? 6 : t == HuntNodeType.Skirmish ? 3 : 0 },
        { "random", null },
        { "plain",  null },   // (no map at all: the ten waves in order, for comparison)
    };

    // Each fighting wave by its node type, over every run of the report: length, damage taken (% of max HP, averaged over the duo) and downs.
    static readonly Dictionary<string, List<float[]>> huntNodeWaves = new Dictionary<string, List<float[]>>();

    static HuntBalRun PlayHuntMap(float dt, int seed, string policy, BotSkill skill)
    {
        Game.SeedForTests(seed);
        var pol = huntPolicies[policy];
        var prng = new Random(seed * 31 + 7);
        var host = new Session { AutoStart = 2, StartWave = 1 };
        var hp = host.Game.ProfileOverride = new Profile();
        hp.BossBeaten(EnemyType.Boss, 5);
        host.Game.Bot = true; host.Game.BotLevel = skill; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        host.SetMode(false, true);
        host.SetHuntMapMode(policy != "plain");
        host.Game.HuntMapSeed = seed;
        var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = skill; c.Game.BotRerolls = false;
        c.Game.ProfileOverride = new Profile();
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        WaitForGame(host, c, dt);
        var g = host.Game;
        Func<HuntMap, int[], int> pick = (m, ch) =>
        {
            if (pol == null) return prng.Next(ch.Length);
            int best = 0;
            for (int i = 1; i < ch.Length; i++)
                if (pol(m.Rows[m.Step][ch[i]].Type) > pol(m.Rows[m.Step][ch[best]].Type)) best = i;
            return best;
        };
        if (pol != null) { host.Game.HuntMapBotPick = pick; c.Game.HuntMapBotPick = pick; }
        var run = new HuntBalRun();
        var types = new List<string>();
        int lastStep = 0;
        float idle = 0f, waveStart = 0f;
        int curWave = -1, wavesDowns0 = 0;
        string curType = null;
        var taken0 = new Dictionary<int, float>();
        for (int f = 0; f < 60 * 60 * 40 && g.State != MatchState.GameOver && g.State != MatchState.Victory; f++)
        {
            Step(host, dt); Step(c, dt);
            // (the bots don't walk to chests: after 20 s with nothing left to fight, take the treasure for them, as a person would)
            idle = g.State == MatchState.Fighting && g.Foes() == 0 ? idle + dt : 0f;
            if (idle > 20f)
            {
                foreach (var pr in g.Props.Values.Where(pr => pr.Kind == PropKind.Chest).ToList()) g.Props.Remove(pr.Id);
                g.Loots.Clear();
                idle = 0f;
            }
            if (g.HuntRoute != null && g.HuntRoute.Step > lastStep)
            {
                for (int st = lastStep; st < g.HuntRoute.Step; st++)
                    types.Add(HuntMap.Name(g.HuntRoute.Rows[st][g.HuntRoute.Path[st]].Type).Substring(0, 2));
                lastStep = g.HuntRoute.Step;
            }
            bool fighting = g.State == MatchState.Fighting && !g.BossNow;
            if (fighting && g.Wave != curWave)
            {
                curWave = g.Wave;
                waveStart = g.RunTime;
                var now = g.HuntNodeAt(g.Wave);
                curType = HuntMap.Name(now) + (g.HuntRoute != null && g.HuntRoute.Current != null && g.HuntRoute.Current.Type == HuntNodeType.Gamble ? " (Gamble)" : "");
                foreach (var p in g.Players.Values) taken0[p.Id] = p.Run.DamageTaken;
                wavesDowns0 = g.Players.Values.Sum(p => p.Run.Downs);
            }
            if (curType != null && g.State == MatchState.Intermission)
            {
                List<float[]> l;
                if (!huntNodeWaves.TryGetValue(curType, out l)) huntNodeWaves[curType] = l = new List<float[]>();
                l.Add(new[] { g.RunTime - waveStart, g.Players.Values.Average(p => 100f * (p.Run.DamageTaken - taken0[p.Id]) / Math.Max(1f, p.MaxHp)), g.Players.Values.Sum(p => p.Run.Downs) - wavesDowns0 });
                curType = null;
            }
            if (g.RunTime > 60f * 40f) break;   // a hunt that long is stalled
        }
        run.Won = g.State == MatchState.Victory;
        run.Time = g.RunTime;
        run.Wave = g.Wave;
        run.Downs = g.Players.Values.Sum(p => p.Run.Downs);
        run.TakenPct = g.Players.Values.Average(p => 100f * p.Run.DamageTaken / Math.Max(1f, p.MaxHp));
        run.Level = g.Players.Values.Max(p => p.Level);
        run.Clues = g.HuntRoute == null ? 0 : g.HuntRoute.Clues;
        run.Elites = types.Count(t => t == "El");
        run.Fights = types.Count(t => t != "Re" && t != "Sh");
        run.Route = string.Join(" ", types);
        run.Ending = (run.Won ? "WON" : g.State == MatchState.GameOver ? "lost" : "stalled") + " wave " + g.Wave;
        host.Leave(""); c.Leave("");
        return run;
    }

    static void HuntMapBalanceReport(float dt, int maps, int firstSeed, BotSkill skill, string only)
    {
        Console.WriteLine("  Good bot duo (skill {0}), {1} maps from seed {2}. Route = the nodes cleared (Sk Skirmish, El Elite, Am Ambush, Co Contraband, Re Rest, Sh Shop, Ga Gamble).", (int)skill, maps, firstSeed);
        var all = new Dictionary<string, List<HuntBalRun>>();
        foreach (var policy in huntPolicies.Keys.Where(k => only == null || only.Split(',').Contains(k)))
        {
            var runs = all[policy] = new List<HuntBalRun>();
            for (int m = 0; m < maps; m++)
            {
                var r = PlayHuntMap(dt, firstSeed + m, policy, skill);
                runs.Add(r);
                Console.WriteLine("  {0,-6} seed {1,3}: {2,-14} {3,5:0} s  taken {4,4:0}%  downs {5}  level {6,2}  clues {7}  {8}", policy, firstSeed + m, r.Ending, r.Time, r.TakenPct, r.Downs, r.Level, r.Clues, r.Route);
            }
        }
        Console.WriteLine("  node type          | waves | avg length s | avg dmg taken % max HP | downs");
        foreach (var kv in huntNodeWaves.OrderBy(k => k.Key))
            Console.WriteLine("  {0,-18} | {1,5} | {2,12:0} | {3,22:0} | {4,5}", kv.Key, kv.Value.Count, kv.Value.Average(x => x[0]), kv.Value.Average(x => x[1]), (int)kv.Value.Sum(x => x[2]));
        Console.WriteLine("  route   | won   | avg time | avg dmg taken % | avg downs | avg level | avg fights | avg Elites");
        foreach (var kv in all)
        {
            var r = kv.Value;
            Console.WriteLine("  {0,-7} | {1,2}/{2,-2} | {3,7:0} s | {4,15:0} | {5,9:0.0} | {6,9:0.0} | {7,10:0.0} | {8,10:0.0}", kv.Key, r.Count(x => x.Won), r.Count, r.Average(x => x.Time), r.Average(x => x.TakenPct), r.Average(x => (float)x.Downs), r.Average(x => (float)x.Level), r.Average(x => (float)x.Fights), r.Average(x => (float)x.Elites));
        }
    }
}
