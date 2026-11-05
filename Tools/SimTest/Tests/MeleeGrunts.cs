using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The melee grunts (MeleeGrunts.cs): Buzzsaw and Spike Block from wave 5, the zig-zagging Spiker.
static partial class Program
{
    static void MeleeGruntsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // Waves: neither new grunt before wave 5, both can come from it.
        {
            var s = StartBuffed(dt, 1, 0);
            var g = s[0].Game;
            var build = typeof(Game).GetMethod("BuildWave", flags);
            var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            Func<int, HashSet<EnemyType>> seen = wave =>
            {
                var set = new HashSet<EnemyType>();
                for (int i = 0; i < 40; i++) { build.Invoke(g, new object[] { wave }); foreach (var t in queue) set.Add(t); }
                return set;
            };
            var early = seen(3).Concat(seen(4)).ToList();
            var late = seen(5).Concat(seen(6)).Concat(seen(7)).ToList();
            Check(!early.Contains(EnemyType.Buzzsaw) && !early.Contains(EnemyType.SpikeBlock), "no Buzzsaw or Spike Block before wave 5");
            Check(late.Contains(EnemyType.Buzzsaw) && late.Contains(EnemyType.SpikeBlock), "waves 5 to 7 bring Buzzsaws and Spike Blocks");
            queue.Clear();
            s[0].Leave(""); s[1].Leave("");
        }

        {
            var s = StartBuffed(dt, 5, 0);
            var g = s[0].Game;
            g.Bot = s[1].Game.Bot = false;
            Run(s, dt, 6, () => g.State == MatchState.Fighting);
            var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
            var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            var p = g.Players[1];
            var other = g.Players[2];
            other.Pos = new V2(60f, 60f);
            var keep = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(60f, 840f), 0f, 0, false });
            keep.MaxHp = keep.Hp = 1e6f; keep.Speed = 0f; keep.SpawnT = 0f;
            Func<EnemyType, V2, Enemy> put = (t, at) => { var z = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false }); z.SpawnT = 0f; z.TargetId = p.Id; z.RetargetT = 1e6f; return z; };
            Action clearOut = () => { queue.Clear(); foreach (var e in g.Enemies.Values.ToList()) if (e != keep) g.Enemies.Remove(e.Id); };
            Action calm = () => { queue.Clear(); keep.Hp = keep.MaxHp; foreach (var q in g.Players.Values) { q.PendingPicks = 0; q.Offer = null; q.Hp = q.MaxHp; } };
            clearOut();
            V2 home = new V2(Game.Width / 2f, Game.Height / 2f);

            // Spiker: zig-zags (the side flips) and still gets there.
            p.Pos = home;
            var sw = put(EnemyType.Spiker, home + new V2(-600f, 0f));
            sw.Speed = 135f;
            int flips = 0; byte lastSide = 0; float maxOff = 0f;
            Run(s, dt, 3f, () => sw.Pos.DistanceTo(p.Pos) < 40f, () =>
            {
                calm(); p.Pos = home;
                if (sw.Phase != 0 && sw.Phase != lastSide) { if (lastSide != 0) flips++; lastSide = sw.Phase; }
                float off = Math.Abs(MathX.Fmod((p.Pos - sw.Pos).Angle - sw.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (sw.Pos.DistanceTo(p.Pos) > 200f) maxOff = Math.Max(maxOff, off);
            });
            Console.WriteLine("  Spiker: {0} side flips, widest swing {1:0} deg", flips, maxOff * 57.3f);
            Check(flips >= 4, "Spiker: swings from side to side every 0.2 to 0.45 s");
            Check(maxOff > 0.6f && maxOff < 1.3f, "Spiker: heads 40 to 65 degrees off the straight line while far away");
            Run(s, dt, 4f, () => sw.Pos.DistanceTo(p.Pos) < 40f, () => { calm(); p.Pos = home; });
            Check(sw.Pos.DistanceTo(p.Pos) < 40f, "Spiker: zig-zagging, it still reaches you");
            clearOut();

            // Swarmer: the plain one. It walks a straight line at you, with no swing, and has no tricks.
            p.Pos = home;
            var pl = put(EnemyType.Swarmer, home + new V2(-600f, 0f));
            float worstOff = 0f;
            Run(s, dt, 6f, () => pl.Pos.DistanceTo(p.Pos) < 40f, () =>
            {
                calm(); p.Pos = home;
                worstOff = Math.Max(worstOff, Math.Abs(pl.Pos.Y - home.Y));
            });
            Check(pl.Pos.DistanceTo(p.Pos) < 40f, "Swarmer: walks straight up to you");
            Check(worstOff < 5f, "Swarmer: no zig-zag, it stays on the straight line (" + worstOff.ToString("0.0") + " px off)");
            Check(Defs.Get(EnemyType.Swarmer).Hp < Defs.Get(EnemyType.Spiker).Hp, "Swarmer: weaker than the Spiker");
            clearOut();

            // Buzzsaw: lots of small touches.
            p.Pos = home;
            p.InvulnT = 0f;
            var saw = put(EnemyType.Buzzsaw, home + new V2(5f, 0f));
            saw.MaxHp = saw.Hp = 1e6f;
            float before = p.Run.DamageTaken;
            Run(s, dt, 1f, null, () => { queue.Clear(); keep.Hp = keep.MaxHp; p.Hp = p.MaxHp; });
            float taken = p.Run.DamageTaken - before;
            Console.WriteLine("  Buzzsaw: {0:0} damage in a second on top of you", taken);
            Check(taken >= 20f && taken <= 60f, "Buzzsaw: about eight small touches a second (4 each, before the difficulty's scaling)");
            clearOut();

            // Spike Block: closes in, winds up with the line locked, leaps for a big hit, then rests.
            p.Pos = home;
            var blk = put(EnemyType.SpikeBlock, home + new V2(-420f, 0f));
            blk.MaxHp = blk.Hp = 1e6f;
            before = p.Run.DamageTaken;
            bool wound = false, leapt = false, rested = false, locked = true, rockedSide = false;
            float lockAng = 0f, bestSide = 0f, hit = 0f;
            Run(s, dt, 10f, () => rested, () =>
            {
                queue.Clear(); keep.Hp = keep.MaxHp; p.Pos = home; p.Hp = p.MaxHp;
                if (blk.Phase == 0) bestSide = Math.Max(bestSide, Math.Abs(blk.Pos.Y - home.Y));
                if (blk.Phase == 1) { if (!wound) lockAng = blk.Facing; wound = true; locked &= Math.Abs(blk.Facing - lockAng) < 0.01f; }
                if (blk.Phase == 2) { leapt = true; hit = Math.Max(hit, p.Run.DamageTaken - before); }
                if (blk.Phase == 3 && leapt) rested = true;
            });
            rockedSide = bestSide > 12f;
            Console.WriteLine("  Spike Block: wound up {0}, leapt {1}, hit for {2:0}, drifted {3:0} px off line", wound, leapt, hit, bestSide);
            Check(rockedSide, "Spike Block: it rocks from side to side as it approaches");
            Check(wound && locked, "Spike Block: it stops to wind up and keeps its line");
            Check(leapt && hit >= 30f, "Spike Block: the leap lands for a big hit (35 before scaling)");
            Check(rested, "Spike Block: it rests after the leap");

            s[0].Leave(""); s[1].Leave("");
        }
    }
}
