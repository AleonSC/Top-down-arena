using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arena;

// The safety net for the spawner refactor (Docs/WaveRefactor.md, step 3): plays the first waves of a few modes headless on fixed
// seeds (a god-mode host, every enemy killed a few seconds after it appears) and writes every enemy's arrival to a text file:
// the frame, its type and where it stood. The golden copy is Tools/SimTest/Golden/spawndump.txt; a refactor step must leave it
// identical. `WAVEDUMP=update run.sh 1 1 spawndump` rewrites it (only when spawning is meant to change).
// Covers the three pacings (timed + portals, timed without portals, the legacy interval), a boss slot and Zombie Infection.
static partial class Program
{
    const string SpawnDumpFile = "Golden/spawndump.txt";

    static string SpawnDumpText()
    {
        var sb = new StringBuilder();
        // name, timed waves, portals, zombie horde, first wave, waves to play, seed
        Action<string, bool, bool, bool, int, int, int> scenario = (name, timed, portals, zombies, first, waves, salt) =>
        {
            Game.SeedForTests(70000 + salt);
            TimedWaves.Enabled = timed;
            SpawnPortals.Enabled = portals;
            var host = new Session { AutoStart = 1, StartWave = first };
            host.Host(nextPort++, "Host", Role.Tank);
            host.Diff.Set(DiffKnob.Campaign, 0f);
            host.StartMatch();
            var g = host.Game;
            if (zombies)
                g.Mods |= RunMod.ZombieInfection;  // (before the first wave starts)
            var seen = new Dictionary<int, int>();  // enemy id -> the frame it came
            int lastWave = -99, lastState = -1, played = 0;
            for (int f = 0; f < 60 * 60 * 12 && played < waves; f++)
            {
                Step(host, Dt);
                g.DevSetGod(1, true);
                foreach (var p in g.Players.Values)
                {
                    p.PendingPicks = p.PendingLegendary = p.PendingMods = 0;
                    p.LegendaryQueue.Clear();
                    p.Offer = null;
                }
                if (g.Wave != lastWave || (int)g.State != lastState)
                {
                    if (g.State == MatchState.Fighting && (g.Wave != lastWave || lastState != (int)MatchState.Fighting))
                    {
                        played++;
                        sb.Append(name).Append(" f").Append(f).Append(" wave ").Append(g.Wave).Append(g.BossSlot ? " boss" : "").Append(" remaining ").Append(g.Remaining).Append('\n');
                    }
                    lastWave = g.Wave;
                    lastState = (int)g.State;
                }
                if (played > waves)
                    break;
                foreach (var e in g.Enemies.Values.ToList())
                {
                    if (!seen.ContainsKey(e.Id))
                    {
                        seen[e.Id] = f;
                        sb.Append(name).Append(" f").Append(f).Append(' ').Append(e.Type).Append(" (").Append((int)e.Pos.X).Append(',').Append((int)e.Pos.Y).Append(") rem ").Append(g.Remaining).Append('\n');
                    }
                    else if (f - seen[e.Id] > 200 && e.Type != EnemyType.Portal)
                        e.Hp = 0f;
                }
            }
            sb.Append(name).Append(" end wave ").Append(g.Wave).Append(" score ").Append(g.Score).Append('\n');
            host.Leave("");
            TimedWaves.Enabled = false;
            SpawnPortals.Enabled = true;
        };
        scenario("portals", true, true, false, 1, 4, 1);
        scenario("timed", true, false, false, 1, 3, 2);
        scenario("interval", false, false, false, 1, 4, 3);
        scenario("bossslot", false, false, false, 5, 3, 4);
        scenario("zombie", false, false, true, 1, 3, 5);
        return sb.ToString();
    }

    static void SpawnDumpTest()
    {
        string now = SpawnDumpText();
        string again = SpawnDumpText();
        Check(now == again, "the spawn dump is repeatable (same seeds, same arrivals, twice over)");
        Console.WriteLine("  spawn dump: {0} lines, {1} KB", now.Split('\n').Length - 1, now.Length / 1024);
        if (Environment.GetEnvironmentVariable("WAVEDUMP") == "update" || !File.Exists(SpawnDumpFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SpawnDumpFile));
            File.WriteAllText(SpawnDumpFile, now);
            Console.WriteLine("  wrote " + SpawnDumpFile);
            return;
        }
        var want = File.ReadAllText(SpawnDumpFile).Split('\n');
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
        Check(bad == 0, "every enemy of every scenario arrives exactly as in " + SpawnDumpFile);
    }
}
