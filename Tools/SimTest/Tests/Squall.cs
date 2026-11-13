using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Storm Herald's faction, step 1 (Core/Squall.cs, Docs/StormFaction.md): a Dust Devil shoves you on touch and veers
// off, a Squall Gunner's three shots leave upwind of you and curve back to where you stood, a Stormcaller calls an amber
// strike under you that hurts if you stay; and the three come in the waves before the Storm Herald.
static partial class Program
{
    static void SquallTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Game.IsSquall(EnemyType.DustDevil) && Game.IsSquall(EnemyType.SquallGunner) && Game.IsSquall(EnemyType.Stormcaller) && !Game.IsSquall(EnemyType.RiotCaptain),
            "Dust Devil, Squall Gunner and Stormcaller are the Squall");
        Check(Defs.Get(EnemyType.DustDevil).Melee && !Defs.Get(EnemyType.SquallGunner).Melee && WaveRecipeJob(EnemyType.Stormcaller) == EnemyJob.Zoner,
            "the Dust Devil is melee, the Gunner shoots, the Stormcaller is a Zoner");

        Game.SeedForTests(4401);
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
            g.Enemies.Clear(); g.Bullets.Clear(); g.Hazards.Clear();
            me.Hp = me.MaxHp;
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f; e.Spawning = false;
            return e;
        };
        V2 home = new V2(900f, 400f);
        me.Hp = me.MaxHp = 1e6f;

        // Dust Devil: reaches you, hurts, shoves you away from it, then veers off.
        var dd = put(EnemyType.DustDevil, new V2(500f, 400f));
        me.Pos = me.Disp = home;
        bool touched = false, veered = false;
        float hpBefore = me.Hp, shoved = 0f;
        for (int f = 0; f < 60 * 5 && !veered; f++)
        {
            Quiet();
            Step(host, dt);
            if (!touched && dd.Phase == 1) { touched = true; }
            if (touched) shoved = Math.Max(shoved, me.Pos.DistanceTo(home));
            if (touched && dd.Phase == 0) veered = true;
        }
        Console.WriteLine("  Dust Devil: touched {0}, lost {1:0} hp, shoved {2:0} px, veered {3}", touched, hpBefore - me.Hp, shoved, veered);
        Check(touched && me.Hp < hpBefore, "a Dust Devil reaches you and hurts");
        Check(shoved > Game.DevilShove * 0.6f, "its touch shoves you about " + Game.DevilShove + " px");
        Check(veered, "then it veers off and comes again");

        // Squall Gunner: three shots, all curving the same way, leaving off to one side and crossing where you stood.
        g.DevSetGod(1, true);
        var gu = put(EnemyType.SquallGunner, new V2(620f, 400f));
        var volley = new List<Bullet>();
        V2 stood = home;
        for (int f = 0; f < 60 * 6 && volley.Count == 0; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            var bs = g.Bullets.Values.Where(b => b.Shooter == gu.Id).ToList();
            if (bs.Count >= 3) volley = bs.Select(b => new Bullet { Pos = b.Pos, Vel = b.Vel, AngVel = b.AngVel, Life = b.Life }).ToList();
        }
        Check(volley.Count == 3, "a Squall Gunner fires a volley of three");
        if (volley.Count == 3)
        {
            Check(volley.All(b => Math.Abs(b.AngVel) > 0.01f && Math.Sign(b.AngVel) == Math.Sign(volley[0].AngVel)), "all three curve, the same way (its wind)");
            V2 toYou = (stood - gu.Pos).Normalized();
            float worst = 0f, leave = 0f;
            foreach (var b0 in volley)
            {
                { V2 u = b0.Vel.Normalized(); leave = Math.Max(leave, (float)Math.Acos(Math.Max(-1f, Math.Min(1f, u.X * toYou.X + u.Y * toYou.Y)))); }
                V2 p = b0.Pos, v = b0.Vel;
                float best = 1e9f;
                for (float t = 0f; t < b0.Life; t += 0.005f) { v = v.Rotated(b0.AngVel * 0.005f); p += v * 0.005f; best = Math.Min(best, p.DistanceTo(stood)); }
                worst = Math.Max(worst, best);
            }
            Console.WriteLine("  Squall Gunner: leaves {0:0.00} rad off your line, passes within {1:0.0} px of where you stood", leave, worst);
            Check(leave > 0.3f, "the shots leave upwind of you, not at you");
            Check(worst < 40f, "and curve back across where you stood");
        }

        // Stormcaller: a strike lands where you stood; it hurts if you stay, not if you step out.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp = 1e6f;
        put(EnemyType.Stormcaller, new V2(560f, 400f));
        Hazard strike = null;
        for (int f = 0; f < 60 * 6 && strike == null; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            strike = g.Hazards.Values.FirstOrDefault(h => h.Kind == Game.HazardCaller);
        }
        Check(strike != null && strike.Pos.DistanceTo(home) < 5f && Math.Abs(strike.Duration - Game.CallerWarn) < 0.01f, "a Stormcaller calls a strike under you with a 1.1 s warning");
        float hp0 = me.Hp;
        for (int f = 0; f < 60 * 1.3f; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        Console.WriteLine("  Stormcaller: standing in the ring cost {0:0} hp", hp0 - me.Hp);
        Check(me.Hp < hp0, "standing in the ring hurts");
        put(EnemyType.Stormcaller, new V2(560f, 400f));
        strike = null;
        for (int f = 0; f < 60 * 6 && strike == null; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            strike = g.Hazards.Values.FirstOrDefault(h => h.Kind == Game.HazardCaller);
        }
        hp0 = me.Hp;
        for (int f = 0; f < 60 * 1.3f; f++) { Quiet(); me.Pos = me.Disp = home + new V2(0f, 120f); Step(host, dt); }
        Check(strike != null && me.Hp == hp0, "stepping out of the ring dodges it");
        host.Leave("");

        // The faction: the three come in the waves before the Storm Herald (50-wave track: 36-39).
        var f0 = Approaches.For(EnemyType.StormHerald);
        Check(f0 != null && f0.Members.Select(m => m.Type).SequenceEqual(new[] { EnemyType.DustDevil, EnemyType.SquallGunner, EnemyType.Stormcaller }) && f0.Squads.Length == 2,
            "the Storm Herald's faction: Dust Devil, Squall Gunner, Stormcaller; squads Squall line and Storm cell");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var seen = new HashSet<EnemyType>();
        var early = new HashSet<EnemyType>();
        for (int w = 36; w <= 39; w++)
            for (int seed = 0; seed < 4; seed++)
            {
                Game.SeedForTests(9500 + w * 10 + seed);
                var cg = new Session().Game;
                cg.Wave = w;
                build.Invoke(cg, new object[] { w });
                foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(Game.IsSquall))
                    seen.Add(t);
            }
        for (int w = 2; w <= 34; w++)
        {
            Game.SeedForTests(9700 + w);
            var cg = new Session().Game;
            cg.Wave = w;
            build.Invoke(cg, new object[] { w });
            foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(Game.IsSquall))
                early.Add(t);
        }
        Console.WriteLine("  waves 36-39 bring: " + string.Join(", ", seen));
        Check(seen.Count == 3, "waves 36-39 bring all three");
        Check(early.Count == 0, "none come before the Storm Herald's approach");
    }
}
