using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The wave editor's designs (Core/WaveDesign.cs): the .waves file, and playing them in the Practice Range.
static partial class Program
{
    static void WaveDesignTest(float dt)
    {
        // The file: what's written reads back the same; bad lines are skipped and named.
        var set = new WaveSet { Name = "drills" };
        set.Points[1] = new V2(60f, 300f);  // B moved
        var w1 = new DesignWave { Level = 6 };
        var rush = new DesignGroup { Name = "Breakout rush", From = "B", At = 0f, Every = 0.5f };
        for (int i = 0; i < 4; i++) rush.Members.Add(EnemyType.Rioter);
        rush.Members.Add(EnemyType.Guard);
        w1.Groups.Add(rush);
        var pair = new DesignGroup { Name = "Gas pair", From = "CD", At = 3f, Arrive = ArriveMode.Together };
        pair.Members.Add(EnemyType.GasGrenadier); pair.Members.Add(EnemyType.GasGrenadier);
        w1.Groups.Add(pair);
        w1.Groups.Add(new DesignGroup { Name = "Riot line", Squad = "Riot line", From = "A", At = 5f });
        w1.Props.Add(new DesignProp { Kind = PropKind.Crate, Pos = new V2(610f, 330f) });
        w1.Props.Add(new DesignProp { Kind = PropKind.Barrel, Pos = new V2(800f, 640f) });
        set.Waves.Add(w1);
        var w2 = new DesignWave { Level = 7 };
        var g2 = new DesignGroup { Name = "Grunts", From = "E", At = 0f, Arrive = ArriveMode.Line };
        for (int i = 0; i < 3; i++) g2.Members.Add(EnemyType.Grunt);
        w2.Groups.Add(g2);
        set.Waves.Add(w2);
        string text = set.ToText();
        List<string> problems;
        var back = WaveSet.Parse(text, out problems);
        Console.WriteLine(text.Split('\n').Take(14).Aggregate("", (a, l) => a + "  | " + l + "\n").TrimEnd());
        Check(problems.Count == 0 && back.ToText() == text && back.Waves.Count == 2 && back.Waves[0].Groups.Count == 3 && back.Waves[0].Props.Count == 2
              && back.Points[1].X == 60f && back.Waves[0].Groups[2].Squad == "Riot line" && back.Waves[0].Groups[1].Arrive == ArriveMode.Together,
            "a .waves file reads back exactly as it was written (points, groups, squads, props)");
        var bad = WaveSet.Parse("wave 1 level 3\n  group X | Grunt x2, Wombat | from A | at 1\n  prop sofa 10,10\n  group Y | squad Nope | from B\n", out problems);
        Check(problems.Count == 3 && bad.Waves[0].Groups.Count == 1 && bad.Waves[0].Groups[0].Members.Count == 2,
            "a damaged file loads what it can and names the rest (" + string.Join("; ", problems) + ")");

        // Played in the Practice Range, as the editor's Test does it.
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Dps);
        host.Plan = set.ToPlan();
        host.DesignTest = set.Copy();
        host.SetMode(true, false);
        host.AutoStart = 1;
        host.StartMatch();
        var g = host.Game;
        g.DevSetGod(1, true);
        var spawned = new List<KeyValuePair<float, Enemy>>();
        var firstPos = new Dictionary<int, V2>();  // where each enemy appeared (they walk off after)
        var seen = new HashSet<int>();
        float t = 0f;
        bool started = false;
        for (int i = 0; i < 60 * 12; i++)
        {
            Step(host, dt);
            if (g.State == MatchState.Fighting) { started = true; t += dt; }
            foreach (var e in g.Enemies.Values)
                if (seen.Add(e.Id))
                {
                    spawned.Add(new KeyValuePair<float, Enemy>(t, e));
                    firstPos[e.Id] = e.Pos;
                }
            if (started && t > 7f)
                break;
        }
        Func<EnemyType, IEnumerable<KeyValuePair<float, Enemy>>> of = type => spawned.Where(kv => kv.Value.Type == type);
        var rioters = of(EnemyType.Rioter).ToList();
        Console.WriteLine("  wave 1 arrivals (level {0}): {1}", g.Wave, string.Join(", ", spawned.Select(kv => kv.Value.Type + "@" + kv.Key.ToString("0.0") + "s")));
        Console.WriteLine("  where: " + string.Join(", ", spawned.Take(7).Select(kv => kv.Value.Type + " " + (int)firstPos[kv.Value.Id].X + "," + (int)firstPos[kv.Value.Id].Y)));
        Check(g.Design != null && g.Wave == 6 && rioters.Count == 4 && rioters.All(kv => firstPos[kv.Value.Id].DistanceTo(set.Points[1]) < 80f)
              && rioters[3].Key - rioters[0].Key > 1.2f && rioters[3].Key - rioters[0].Key < 1.8f,
            "a designed wave plays at its level: one by one from its point, as far apart as set");
        var gas = of(EnemyType.GasGrenadier).ToList();
        Check(gas.Count == 2 && gas.All(kv => kv.Key > 2.9f && kv.Key < 3.3f) && gas.Any(kv => firstPos[kv.Value.Id].DistanceTo(set.Points[2]) < 100f) && gas.Any(kv => firstPos[kv.Value.Id].DistanceTo(set.Points[3]) < 100f),
            "a group arrives together at its time, sharing out its spawn points");
        var shields = of(EnemyType.RiotShield).ToList();
        Check(shields.Count == 4 && shields.All(kv => kv.Key > 4.9f && kv.Value.SquadId != 0) && shields.Select(kv => kv.Value.SquadId).Distinct().Count() == 1,
            "a squad group brings the squad as the game knows it (the Riot line, in formation, at its time)");
        Check(g.Props.Count == 2 && g.Props.Values.Any(p => p.Kind == PropKind.Crate && p.Pos.DistanceTo(new V2(610f, 330f)) < 1f), "the wave's props are laid out where they were placed");

        // The wave isn't over until the last group has come; then the next designed wave.
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Step(host, dt);
        Check(g.State == MatchState.Fighting || g.DesignPending == 0, "a designed wave waits for its groups still to come");
        for (int i = 0; i < 60 * 30 && !(g.State == MatchState.Fighting && g.Wave == 7); i++)
        {
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var p in g.Players.Values) { p.PendingPicks = 0; p.Offer = null; }
            Step(host, dt);
        }
        for (int i = 0; i < 30; i++) Step(host, dt);
        var grunts = g.Enemies.Values.Where(e => e.Type == EnemyType.Grunt).ToList();
        Check(g.Wave == 7 && grunts.Count == 3 && grunts.All(e => e.Pos.DistanceTo(set.Points[4]) < 120f),
            "then the next designed wave, its group side by side at its point");
        host.Leave("");
        Check(host.DesignTest == null && host.Game.Design == null, "leaving drops the design under test");
    }
}

static partial class Program
{
    /// The designed wave sets built into the game (Tools/WaveGen/build.sh) match their files in Data/Waves exactly:
    /// fails if a file was added, changed or removed without running the script.
    static void WaveGenTest()
    {
        string dir = System.IO.Path.Combine("..", "..", "Data", "Waves");
        var files = System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "*.waves").Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0];
        var built = DesignedWaves.Names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Console.WriteLine("  Data/Waves: {0}; built in: {1}", string.Join(", ", files), string.Join(", ", built));
        Check(files.SequenceEqual(built), "every .waves file in Data/Waves is built into the game, and nothing else (run Tools/WaveGen/build.sh)");
        var stale = new List<string>();
        foreach (var name in built.Intersect(files))
        {
            List<string> problems;
            var fromFile = WaveSet.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, name + ".waves")), out problems);
            var inGame = DesignedWaves.Get(name);
            if (problems.Count > 0 || inGame == null || inGame.ToText() != fromFile.ToText())
                stale.Add(name);
        }
        Check(stale.Count == 0, "each built-in wave set matches its file exactly" + (stale.Count > 0 ? " (out of date: " + string.Join(", ", stale) + "; run Tools/WaveGen/build.sh)" : ""));
        var a = DesignedWaves.Get(built.FirstOrDefault() ?? "");
        if (a != null)
        {
            a.Waves.Clear();
            Check(DesignedWaves.Get(built[0]).Waves.Count > 0 && DesignedWaves.Get("no such set") == null, "Get hands out a fresh copy each time (and null for an unknown name)");
        }
    }
}
