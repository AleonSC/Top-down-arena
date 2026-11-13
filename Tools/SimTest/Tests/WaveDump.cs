using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Arena;

// The safety net for the wave and spawn refactor (Docs/WaveRefactor.md, step 0): builds every wave of every mode on fixed seeds
// and writes what BuildWave queued (singles in order, then squads, then the headliners) to one text file. The golden copy is
// Tools/SimTest/Golden/wavedump.txt; a refactor step must leave it identical. `WAVEDUMP=update run.sh 1 1 wavedump` rewrites it
// (only when a wave is meant to change). Covers BuildWave only: the breach flood, rooms, timing and spawn positions come later.
static partial class Program
{
    const string WaveDumpFile = "Golden/wavedump.txt";

    static string WaveDumpText()
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = typeof(Game).GetMethod("BuildWave", np);
        var singles = typeof(Game).GetField("spawnQueue", np);
        var squads = typeof(Game).GetField("squadQueue", np);
        var sb = new StringBuilder();

        Func<IEnumerable<EnemyType>, string> runs = list =>
        {
            var o = new StringBuilder();
            EnemyType? last = null;
            int n = 0;
            Action flush = () => { if (last.HasValue) o.Append(o.Length > 0 ? " " : "").Append(last.Value).Append(n > 1 ? "*" + n : ""); };
            foreach (var t in list)
            {
                if (last.HasValue && last.Value == t) { n++; continue; }
                flush();
                last = t; n = 1;
            }
            flush();
            return o.ToString();
        };

        // One scenario: a name, how to set a fresh game up, the waves to build (and whether to build the boss's own wave too).
        Action<string, Action<Game>, int, int, int> scenario = (name, setup, from, to, salt) =>
        {
            for (int n = from; n <= to; n++)
                for (int pass = 0; pass < 2; pass++)  // 0: the regular wave n, 1: the boss slot after it
                {
                    Game.SeedForTests(90000 + salt * 1000 + n * 2 + pass);
                    var g = new Session().Game;  // fresh, so its rolls start from the seed
                    setup(g);
                    g.Wave = n;
                    g.BossSlot = pass == 1;
                    if (pass == 1 && !g.IsBossWave(n))
                        continue;
                    if (pass == 0 && !g.Bosses.Slotted && g.IsBossWave(n))
                        continue;  // (a schedule without a slot: the boss wave is the wave)
                    build.Invoke(g, new object[] { n });
                    var q = (List<EnemyType>)singles.GetValue(g);
                    var sq = (List<SquadDef>)squads.GetValue(g);
                    sb.Append(name).Append(" w").Append(n).Append(pass == 1 ? " boss" : "").Append(": ").Append(runs(q));
                    var heads = g.Headliners;
                    foreach (var s in sq)
                        sb.Append(" | ").Append(runs(s.Members));
                    if (heads.Count > 0 && n >= 13 && pass == 0)
                        sb.Append(" # ").Append(runs(heads));
                    sb.Append('\n');
                }
        };

        scenario("campaign", g => { }, 1, 60, 1);
        scenario("quick", g => { g.Quick = true; g.QuickOrder = new[] { EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus }; }, 1, 25, 2);
        scenario("hunt", g => { g.Hunt = Hunts.Get(EnemyType.Boss); }, 1, 10, 3);
        scenario("campaign100", g => { g.LongRun = true; g.Leg = 0; }, 1, 50, 4);
        scenario("campaign100leg2", g => { g.LongRun = true; g.Leg = 1; }, 1, 30, 5);
        scenario("zombie", g => { g.Mods |= RunMod.ZombieInfection; }, 1, 30, 6);
        scenario("hard", g => { g.Diff.Set(DiffKnob.ApproachShare, 0f); }, 1, 40, 7);
        foreach (PathId id in Enum.GetValues(typeof(PathId)))
        {
            if (id == PathId.None)
                continue;
            PathId pid = id;
            scenario("path-" + pid, g => { g.CurrentPath = pid; g.PathFrom = 6; g.PathTo = 10; }, 6, 10, 10 + (int)pid);
        }
        return sb.ToString();
    }

    static void WaveDumpTest()
    {
        string now = WaveDumpText();
        string again = WaveDumpText();
        Check(now == again, "the wave dump is repeatable (same seeds, same waves, twice over)");
        Console.WriteLine("  wave dump: {0} lines, {1} KB", now.Split('\n').Length - 1, now.Length / 1024);
        if (Environment.GetEnvironmentVariable("WAVEDUMP") == "update" || !File.Exists(WaveDumpFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WaveDumpFile));
            File.WriteAllText(WaveDumpFile, now);
            Console.WriteLine("  wrote " + WaveDumpFile);
            return;
        }
        var want = File.ReadAllText(WaveDumpFile).Split('\n');
        var have = now.Split('\n');
        int bad = 0;
        for (int i = 0; i < Math.Max(want.Length, have.Length) && bad < 5; i++)
        {
            string a = i < want.Length ? want[i] : "(missing)", b = i < have.Length ? have[i] : "(missing)";
            if (a != b)
            {
                bad++;
                Console.WriteLine("  line {0}\n    was: {1}\n    now: {2}", i + 1, a, b);
            }
        }
        Check(bad == 0, "every wave of every mode is built exactly as in " + WaveDumpFile);
    }
}
