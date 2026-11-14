using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Arena;

// The safety net for the enemy behaviour refactor (Docs/EnemyBehaviours.md): every non-boss enemy type, alone against a
// god-mode host who stands still and shoots at it, for AiTraceSeconds on a fixed seed. Each frame hashes every enemy it
// brought (position, Phase, Facing, health) and every bullet (kind, position), so moving, shooting, specials and blocking
// all count. The golden copy is Tools/SimTest/Golden/aitrace.txt, one line per type; a migration step must leave it
// identical. `AITRACE=update run.sh 1 1 aitrace` rewrites it (only when behaviour is meant to change).
static partial class Program
{
    const string AiTraceFile = "Golden/aitrace.txt";
    const float AiTraceSeconds = 12f;

    static string AiTraceOne(EnemyType type)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queue = typeof(Game).GetField("spawnQueue", flags);
        Game.SeedForTests(80000 + (int)type);
        TimedWaves.Enabled = false;
        var host = new Session { AutoStart = 1, StartWave = 3 };
        for (int tries = 0; !host.Host(nextPort++, "Host", Role.Tank) && tries < 20; tries++) { }  // a port still held by an earlier run: take the next
        host.Diff.Set(DiffKnob.Campaign, 0f);
        host.StartMatch();
        var g = host.Game;
        try
        {
            for (int f = 0; f < 60 * 10 && g.State != MatchState.Fighting; f++)
                Step(host, Dt);
            if (g.State != MatchState.Fighting)
                return type + " no-fight";
            ((List<EnemyType>)queue.GetValue(g)).Clear();
            ClearSquads(g);
            var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1500f, 80f), 0f, 0, false });
            foreach (var e0 in g.Enemies.Values)
                if (e0 != anchor)
                    e0.Hp = 0f;
            Step(host, Dt);
            g.Bullets.Clear();
            var me = g.Players.Values.First();
            V2 home = new V2(800f, 450f);
            var e = (Enemy)spawn.Invoke(g, new object[] { type, home + new V2(-320f, 0f), 0f, 0, false });
            e.SpawnT = 0f;
            e.Hp = e.MaxHp = 1e6f;
            int first = e.Id;
            ulong h = 1469598103934665603UL;
            Action<double> mix = x => { h ^= (ulong)(long)Math.Round(x * 100.0); h *= 1099511628211UL; };
            int shots = 0;
            var seen = new HashSet<int>();
            for (int f = 0; f < AiTraceSeconds * 60; f++)
            {
                ((List<EnemyType>)queue.GetValue(g)).Clear();
                ClearSquads(g);
                g.DevSetGod(me.Id, true);
                me.Pos = home;
                me.PendingPicks = me.PendingLegendary = me.PendingMods = 0;
                me.LegendaryQueue.Clear();
                me.Offer = null;
                anchor.FrozenT = 1f;
                anchor.FireT = 99f;
                anchor.Pos = new V2(1500f, 80f);
                anchor.Hp = anchor.MaxHp;
                host.Poll();
                g.FixedStep(Dt, new PlayerInput { AimAt = e.Pos, Fire = true });  // (Step's input never fires)
                g.Update(Dt);
                g.Sounds.Clear();
                foreach (var o in g.Enemies.Values.OrderBy(x => x.Id))
                {
                    if (o == anchor || o.Id < first)
                        continue;
                    mix((int)o.Type); mix(o.Pos.X); mix(o.Pos.Y); mix(o.Phase); mix(o.Facing); mix(o.Hp);
                }
                foreach (var b in g.Bullets.Values.OrderBy(x => x.Id))
                {
                    mix(b.Kind); mix(b.Pos.X); mix(b.Pos.Y);
                    if (b.Kind >= Defs.SmallShot && seen.Add(b.Id))
                        shots++;
                }
            }
            return string.Format("{0} {1:x16} at ({2},{3}) phase {4} hp {5:0} shots {6}", type, h, (int)e.Pos.X, (int)e.Pos.Y, e.Phase,
                1e6f - e.Hp, shots);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            return type + " error " + inner.GetType().Name;
        }
        finally
        {
            host.Leave("");
            TimedWaves.Enabled = false;
        }
    }

    /// Does `a` move with the same kind of part as `b` (a faction enemy and the Warden enemy whose role it plays)? Brain.cs
    static bool SameMove(EnemyType a, EnemyType b)
    {
        var ma = Game.BrainOf(a)?.Move;
        var mb = Game.BrainOf(b)?.Move;
        return ma != null && mb != null && ma.GetType() == mb.GetType();
    }

    static IEnumerable<EnemyType> AiTraceTypes()
    {
        for (int i = 0; i < Defs.Enemies.Length; i++)
        {
            var d = Defs.Enemies[i];
            if (d == null || d.IsBoss || (EnemyType)i == EnemyType.Portal)
                continue;
            yield return (EnemyType)i;
        }
    }

    static string AiTraceText(int max = int.MaxValue)
    {
        var sb = new StringBuilder();
        foreach (var t in AiTraceTypes().Take(max))
            sb.Append(AiTraceOne(t)).Append('\n');
        return sb.ToString();
    }

    static void AiTraceTest()
    {
        bool timed = TimedWaves.Enabled;
        string now = AiTraceText();
        string again = AiTraceText(6);
        TimedWaves.Enabled = timed;
        Check(now.StartsWith(again), "the AI trace is repeatable (same seeds, same frames, twice over)");
        Console.WriteLine("  ai trace: {0} enemy types, {1} s each", now.Split('\n').Length - 1, AiTraceSeconds);
        if (Environment.GetEnvironmentVariable("AITRACE") == "update" || !File.Exists(AiTraceFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AiTraceFile));
            File.WriteAllText(AiTraceFile, now);
            Console.WriteLine("  wrote " + AiTraceFile);
            return;
        }
        var want = File.ReadAllText(AiTraceFile).Split('\n');
        var have = now.Split('\n');
        int bad = 0;
        for (int i = 0; i < Math.Max(want.Length, have.Length); i++)
        {
            string a = i < want.Length ? want[i] : "(missing)", b = i < have.Length ? have[i] : "(missing)";
            if (a == b)
                continue;
            if (++bad <= 8)
                Console.WriteLine("  changed\n    was: {0}\n    now: {1}", a, b);
        }
        Check(bad == 0, "every enemy type behaves exactly as in " + AiTraceFile + (bad > 0 ? " (" + bad + " types changed)" : ""));
    }
}
