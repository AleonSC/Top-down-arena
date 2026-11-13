using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Singularity's faction, step 1 (Core/Infall.cs, Docs/SingularityFaction.md): Debris spirals in, an Acolyte's three
// orbs meet where you stood, a Pulsar plants, flickers, sweeps its two beams a quarter turn and rests; and the three
// come in the waves before the Singularity.
static partial class Program
{
    static void InfallTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Game.IsInfall(EnemyType.Debris) && Game.IsInfall(EnemyType.Acolyte) && Game.IsInfall(EnemyType.Pulsar) && !Game.IsInfall(EnemyType.Hornet),
            "Debris, Acolyte and Pulsar are the Infall");
        Check(Defs.Get(EnemyType.Debris).Melee && !Defs.Get(EnemyType.Acolyte).Melee && WaveRecipeJob(EnemyType.Pulsar) == EnemyJob.Zoner,
            "Debris is melee, the Acolyte shoots, the Pulsar is a Zoner");

        Game.SeedForTests(4322);
        var host = new Session { AutoStart = 1, StartWave = 1 };
        host.Host(nextPort++, "Host", Role.Tank);
        host.Diff.Set(DiffKnob.Campaign, 0f);
        host.StartMatch();
        var g = host.Game;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", np);
        var queues = new[] { (IList)typeof(Game).GetField("spawnQueue", np).GetValue(g), (IList)typeof(Game).GetField("squadQueue", np).GetValue(g) };
        Action Quiet = () => { foreach (var q in queues) q.Clear(); };
        for (int f = 0; f < 60 * 20 && g.State != MatchState.Fighting; f++)
            Step(host, dt);
        Check(g.State == MatchState.Fighting, "the match started");
        var me = g.Players[1];
        Func<EnemyType, V2, Enemy> put = (t, at) =>
        {
            Quiet();
            g.Enemies.Clear(); g.Bullets.Clear(); g.Patches.Clear();
            me.Hp = me.MaxHp;
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f; e.Spawning = false;
            return e;
        };
        Action<float, Action> run = (secs, each) => { for (int f = 0; f < secs * 60; f++) { Quiet(); me.Pos = me.Disp = new V2(900f, 400f); Step(host, dt); each?.Invoke(); } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        // Debris: closes in, but off the straight line (a spiral).
        var db = put(EnemyType.Debris, new V2(300f, 400f));
        float d0 = db.Pos.DistanceTo(me.Pos), off = 0f;
        run(2.5f, () => off = Math.Max(off, Math.Abs(db.Pos.Y - 400f)));
        Console.WriteLine("  Debris: {0:0} px closer, {1:0} px off the line", d0 - db.Pos.DistanceTo(me.Pos), off);
        Check(db.Pos.DistanceTo(me.Pos) < d0 - 150f && off > 60f, "Debris closes in on a curve, not a straight line");

        // Acolyte: three orbs, two curving opposite ways, all meeting where you stood.
        var ac = put(EnemyType.Acolyte, new V2(620f, 400f));
        var volley = new List<Bullet>();
        run(6f, () => { if (volley.Count == 0) { var bs = g.Bullets.Values.Where(b => b.Shooter == ac.Id).ToList(); if (bs.Count >= 3) volley = bs.Select(b => new Bullet { Pos = b.Pos, Vel = b.Vel, AngVel = b.AngVel, Life = b.Life }).ToList(); } });
        Check(volley.Count == 3, "an Acolyte fires a volley of three orbs");
        if (volley.Count == 3)
        {
            var curving = volley.Where(b => Math.Abs(b.AngVel) > 0.01f).ToList();
            Check(curving.Count == 2 && Math.Sign(curving[0].AngVel) != Math.Sign(curving[1].AngVel), "two of them curve, opposite ways");
            // Fly them on their own (no hits) and see how close each comes to where you stood.
            V2 target = me.Pos;
            float worst = 0f;
            foreach (var b0 in volley)
            {
                V2 p = b0.Pos, v = b0.Vel;
                float best = 1e9f;
                for (float t = 0f; t < b0.Life; t += 0.005f) { v = v.Rotated(b0.AngVel * 0.005f); p += v * 0.005f; best = Math.Min(best, p.DistanceTo(target)); }
                worst = Math.Max(worst, best);
            }
            Console.WriteLine("  Acolyte: the orbs pass within {0:0.0} px of where you stood", worst);
            Check(worst < 30f, "all three orbs meet where you stood");
        }

        // Pulsar: walks in, plants, flickers, sweeps a quarter turn standing still, rests.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp = 1e6f;
        var pu = put(EnemyType.Pulsar, new V2(560f, 400f));
        bool warned = false, swept = false, rested = false, hurt = false, still = true;
        float faceAtSweep = 0f, faceEnd = 0f, turned = -1f;
        V2 planted = V2.Zero;
        byte last = 0;
        run(12f, () =>
        {
            if (pu.Phase == 1 && last != 1) { warned = true; planted = pu.Pos; }
            if (pu.Phase == 2 && last != 2) { swept = true; faceAtSweep = pu.Facing; }
            if (pu.Phase == 2) { faceEnd = pu.Facing; still &= pu.Pos.DistanceTo(planted) < 1f; hurt |= me.Hp < me.MaxHp; }
            if (pu.Phase == 3 && last == 2 && turned < 0f) turned = Math.Abs(faceEnd - faceAtSweep);
            if (pu.Phase == 3) rested = true;
            if (pu.Phase == 1) still &= pu.Pos.DistanceTo(planted) < 1f;
            last = pu.Phase;
        });
        Console.WriteLine("  Pulsar: warned {0}, swept {1:0.00} rad, rested {2}, hurt you {3}", warned, turned, rested, hurt);
        Check(warned && swept && rested, "a Pulsar plants and flickers, sweeps, then rests");
        Check(Math.Abs(turned - Game.PulsarArc) < 0.1f && still, "it sweeps a quarter turn while standing still");
        Check(hurt, "the sweep passes through you and hurts");
        Check(Game.PulsarBeamDistance(pu, pu.Pos + V2.FromAngle(pu.Facing) * 300f) < 1f && Game.PulsarBeamDistance(pu, pu.Pos - V2.FromAngle(pu.Facing) * 300f) < 1f,
            "it has two beams, one each way");
        host.Leave("");

        // The faction: the three come in the waves before the Singularity (50-wave track: 16-19).
        var f0 = Approaches.For(EnemyType.Singularity);
        Check(f0 != null && f0.Members.Select(m => m.Type).SequenceEqual(new[] { EnemyType.Debris, EnemyType.Acolyte, EnemyType.Pulsar }) && f0.Squads.Length == 2,
            "the Singularity's faction: Debris, Acolyte, Pulsar; squads Infall and Binary");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var seen = new HashSet<EnemyType>();
        var early = new HashSet<EnemyType>();
        for (int w = 16; w <= 19; w++)
            for (int seed = 0; seed < 4; seed++)
            {
                Game.SeedForTests(9100 + w * 10 + seed);
                var cg = new Session().Game;
                cg.Wave = w;
                build.Invoke(cg, new object[] { w });
                var q = ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members));
                foreach (var t in q.Where(Game.IsInfall))
                    seen.Add(t);
            }
        for (int w = 2; w <= 14; w++)
        {
            Game.SeedForTests(9300 + w);
            var cg = new Session().Game;
            cg.Wave = w;
            build.Invoke(cg, new object[] { w });
            foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(Game.IsInfall))
                early.Add(t);
        }
        Console.WriteLine("  waves 16-19 bring: " + string.Join(", ", seen));
        Check(seen.Count == 3, "waves 16-19 bring all three");
        Check(early.Count == 0, "none come before the Singularity's approach");
    }

    static EnemyJob WaveRecipeJob(EnemyType t) => Game.JobOf(t);
}
