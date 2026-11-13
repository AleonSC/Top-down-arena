using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Singularity's faction, steps 1-3 (Core/Infall.cs, Docs/SingularityFaction.md): Debris spirals in, an Acolyte's three
// orbs meet where you stood, a Pulsar plants, flickers, sweeps its two beams a quarter turn and rests, a Graviton's
// Small Well drags you, an Orbiter slings its orbs, a Collapsar implodes, a Lens bends shots off its face, a White Dwarf
// turns your shots round; the Accretion ring closes in and the ten lessons; and they come in the waves before the Singularity.
static partial class Program
{
    static void InfallTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Game.IsInfall(EnemyType.Debris) && Game.IsInfall(EnemyType.VoidAcolyte) && Game.IsInfall(EnemyType.Pulsar) && !Game.IsInfall(EnemyType.Hornet),
            "Debris, Acolyte and Pulsar are the Infall");
        Check(Defs.Get(EnemyType.Debris).Melee && !Defs.Get(EnemyType.VoidAcolyte).Melee && WaveRecipeJob(EnemyType.Pulsar) == EnemyJob.Zoner,
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
        var ac = put(EnemyType.VoidAcolyte, new V2(620f, 400f));
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
        // Graviton: lobs a Small Well near you; once landed it drags you and bends shots; it's gone after MiniWellLife.
        g.DevSetGod(1, true);
        var gv = put(EnemyType.Graviton, new V2(560f, 400f));
        Enemy well = null;
        run(9f, () => { if (well == null) well = g.Enemies.Values.FirstOrDefault(o => o.Type == EnemyType.MiniWell); });
        Check(well != null && well.ParentId == gv.Id && well.Pos.DistanceTo(new V2(900f, 400f)) <= Game.GravitonScatter + 1f, "a Graviton lobs a Small Well near you");
        if (well != null)
        {
            var drift = (V2)typeof(Game).GetMethod("WellDriftAt", np).Invoke(g, new object[] { well.Pos + new V2(120f, 0f) });
            Check(drift.X < -10f, "a landed well drags a player toward it");
            Check(Game.JobOf(EnemyType.Graviton) == EnemyJob.Zoner && Defs.Get(EnemyType.MiniWell).Picker == PickerGroup.Hidden, "the Graviton is a Zoner; its well only comes from it");
            int id = well.Id;
            bool gone = false;
            run(Game.MiniWellLife + 0.5f, () => gone |= !g.Enemies.ContainsKey(id));
            Check(gone, "the well closes after " + Game.MiniWellLife + " s");
        }

        // Orbiter: slings fast orbs at you, counting its throws in Phase.
        var ob = put(EnemyType.Orbiter, new V2(640f, 400f));
        int fast = 0;
        var seenShots = new HashSet<int>();
        run(6f, () => { foreach (var b in g.Bullets.Values) if (b.Shooter == ob.Id && b.Kind == Defs.FastShot && seenShots.Add(b.Id)) fast++; });
        Console.WriteLine("  Orbiter: {0} orbs in 6 s", fast);
        Check(fast >= 2 && fast <= 4, "an Orbiter slings an orb about every " + Game.OrbiterEvery + " s");

        // Collapsar: killed, an Implosion pulls, bursts into a ring of 12 and goes; shots pass through it.
        var co = put(EnemyType.Collapsar, new V2(700f, 400f));
        V2 coAt = co.Pos;
        co.Hp = 0f;
        Enemy imp = null;
        run(0.05f, () => { if (imp == null) imp = g.Enemies.Values.FirstOrDefault(o => o.Type == EnemyType.Implosion); });
        Check(imp != null && imp.Pos.DistanceTo(coAt) < 60f && imp.Def.Xp == 0, "a killed Collapsar leaves an Implosion (no XP, so shots pass through it)");
        if (imp != null)
        {
            var pull = (V2)typeof(Game).GetMethod("WellDriftAt", np).Invoke(g, new object[] { imp.Pos + new V2(150f, 0f) });
            Check(pull.X < -20f, "the Implosion pulls players in");
            int iid = imp.Id, ring = 0;
            var ringIds = new HashSet<int>();
            run(Game.ImplosionTime + 0.3f, () => { foreach (var b in g.Bullets.Values) if (b.Shooter == iid && ringIds.Add(b.Id)) ring++; });
            Check(!g.Enemies.ContainsKey(iid) && ring == Game.ImplosionShots, "after " + Game.ImplosionTime + " s it bursts into a ring of " + Game.ImplosionShots + " and is gone (" + ring + ")");
        }
        // Lens: a shot at its face bends away and does nothing; a shot from the side lands.
        var ln = put(EnemyType.Lens, new V2(600f, 400f));
        ln.Speed = 0f;
        run(3f, null);  // it turns to face you (at 900, 400)
        Check(Math.Abs(MathX.Fmod(ln.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < 0.1f, "a Lens turns its face to you");
        var spawnB = typeof(Game).GetMethod("SpawnBullet", np);
        float hp0 = ln.Hp;
        var front = (Bullet)spawnB.Invoke(g, new object[] { Defs.TankShot, new V2(820f, 402f), new V2(-700f, 0f), 0f, 1.2f, 10f, me.Id, false, 0f });
        float closest = 1e9f;
        run(0.6f, () => { if (g.Bullets.ContainsKey(front.Id)) closest = Math.Min(closest, front.Pos.DistanceTo(ln.Pos)); });
        Console.WriteLine("  Lens: a shot at its face came within {0:0} px; health {1:0} of {2:0}", closest, ln.Hp, hp0);
        Check(ln.Hp >= hp0 && closest > ln.Radius, "a shot at a Lens's face bends round it and does nothing");
        Check(Game.LensFaces(ln, ln.Pos + new V2(100f, 0f)) && !Game.LensFaces(ln, ln.Pos + new V2(0f, 100f)), "its face is the front; its sides are open");
        var side = (Bullet)spawnB.Invoke(g, new object[] { Defs.TankShot, new V2(600f, 250f), new V2(0f, 700f), 0f, 1.2f, 10f, me.Id, false, 0f });
        run(0.4f, null);
        Check(ln.Hp < hp0, "a shot from its side lands");

        // White Dwarf: darts in, flashes, then shoves you and turns your shots round.
        var wd = put(EnemyType.WhiteDwarf, new V2(500f, 400f));
        bool flashed = false, pulsed = false;
        int turnedBack = 0;
        var mine = new List<int>();
        var knockField = typeof(Game).GetField("knockLeft", np);
        float knocked = 0f;
        run(4f, () =>
        {
            if (wd.Phase == 1) flashed = true;
            if (wd.Phase == 1 && wd.PhaseT < 0.1f && mine.Count == 0)  // a moment before the pulse: two of your shots near it
                for (int k = 0; k < 2; k++)
                    mine.Add(((Bullet)spawnB.Invoke(g, new object[] { Defs.TankShot, wd.Pos + new V2(60f, k * 20f), new V2(-50f, 0f), 0f, 2f, 10f, me.Id, false, 0f })).Id);
            if (wd.Phase == 2) pulsed = true;
            knocked = Math.Max(knocked, (float)knockField.GetValue(g));
            foreach (var b in g.Bullets.Values) if (b.Shooter == wd.Id && b.Owner == 0 && !mine.Contains(b.Id)) { mine.Add(b.Id); turnedBack++; }
        });
        Console.WriteLine("  White Dwarf: flashed {0}, pulsed {1}, turned {2} shots, knock {3:0} px", flashed, pulsed, turnedBack, knocked);
        Check(flashed && pulsed, "a White Dwarf darts in, flashes, then pulses");
        Check(turnedBack == 2, "the pulse turns your shots near it round as its own");
        // Bots: the White Dwarf's flash, a Small Well and an Implosion count as danger; a White Dwarf not flashing doesn't.
        var wdB = put(EnemyType.WhiteDwarf, new V2(800f, 400f));
        float calm = g.InfallBotDanger(wdB, new V2(860f, 400f), 8f);
        wdB.Phase = 1;
        float flash = g.InfallBotDanger(wdB, new V2(860f, 400f), 8f), far = g.InfallBotDanger(wdB, new V2(800f + Game.WhitePushRange + 80f, 400f), 8f);
        var mw = put(EnemyType.MiniWell, new V2(600f, 400f));
        var imB = put(EnemyType.Implosion, new V2(600f, 400f));
        Check(calm == 0f && flash > 0f && far == 0f && g.InfallBotDanger(mw, new V2(640f, 400f), 8f) > 0f && g.InfallBotDanger(imB, new V2(650f, 400f), 8f) > 0f,
            "bots see a White Dwarf's flash, a Small Well and an Implosion as danger");
        // A bot next to a landed Small Well walks away from it.
        var mw2 = put(EnemyType.MiniWell, new V2(900f, 400f));
        mw2.PhaseT = 10f;
        me.Pos = me.Disp = new V2(940f, 400f);
        g.Bot = true;
        for (int f = 0; f < 60 * 2; f++) { Quiet(); Step(host, dt); }
        g.Bot = false;
        Console.WriteLine("  a bot by a Small Well ends {0:0} px from it", me.Pos.DistanceTo(mw2.Pos));
        Check(me.Pos.DistanceTo(mw2.Pos) > 120f, "a bot by a Small Well gets clear of it");
        Check(knocked > 50f, "and shoves you back");
        // The Accretion ring: twelve in a ring round you with two gaps, turning and closing.
        Quiet(); g.Enemies.Clear(); g.Bullets.Clear();
        me.Pos = me.Disp = new V2(900f, 400f);
        var fin = Approaches.For(EnemyType.Singularity).Finale;
        typeof(Game).GetMethod("SpawnSquadAt", np).Invoke(g, new object[] { fin, null });
        var ringE = g.Enemies.Values.Where(o => o.SquadId != 0).ToList();
        foreach (var o in ringE) o.Hp = o.MaxHp = 1e6f;
        Func<float> meanR = () => ringE.Average(o => o.Pos.DistanceTo(me.Pos));
        float r0 = meanR();
        var angles = ringE.Select(o => (o.Pos - me.Pos).Angle).OrderBy(a => a).ToList();
        float widest = 0f;
        for (int i = 0; i < angles.Count; i++) widest = Math.Max(widest, (i + 1 < angles.Count ? angles[i + 1] : angles[0] + MathX.Tau) - angles[i]);
        float slot = MathX.Tau / Game.AccretionSlots;
        run(6f, null);
        float r1 = meanR();
        Console.WriteLine("  Accretion ring: {0} members, {1:0} px out at first, {2:0} px after 6 s; widest gap {3:0.0} slots", ringE.Count, r0, r1, widest / slot);
        Check(ringE.Count == 12 && ringE.Count(o => o.Type == EnemyType.Orbiter) == 4 && Math.Abs(r0 - Game.AccretionStart) < 40f, "the Accretion ring: eight Debris and four Orbiters in a ring round you");
        Check(Math.Abs(widest / slot - 2f) < 0.3f, "with a gap (two slots wide) where a slot is left open");
        Check(r1 < r0 - 70f && r1 > Game.AccretionEnd - 30f, "and it closes in");
        // Its band: the host keeps one while it closes, and the snapshot carries it to clients.
        Check(g.AccretionBands.Count == 1 && Math.Abs(g.AccretionBands[0].Radius - r1) < 30f && g.AccretionBands[0].Center.DistanceTo(me.Pos) < 1f,
            "the ring has a band (its middle and radius) while it closes");
        var ms = new System.IO.MemoryStream();
        typeof(Game).GetMethod("SyncAccretion", np).Invoke(g, new object[] { new NetSync(new System.IO.BinaryWriter(ms)) });
        var cgb = new Session().Game;
        ms.Position = 0;
        typeof(Game).GetMethod("SyncAccretion", np).Invoke(cgb, new object[] { new NetSync(new System.IO.BinaryReader(ms)) });
        Check(cgb.AccretionBands.Count == 1 && cgb.AccretionBands[0].Radius == g.AccretionBands[0].Radius && cgb.AccretionBands[0].Spin == g.AccretionBands[0].Spin, "and a client gets the same band");
        var band = g.AccretionBands[0];
        float gapAng = band.Spin + MathX.Tau * Game.AccretionGaps[0] / Game.AccretionSlots;
        Check(Game.InAccretionGap(band, gapAng) && !Game.InAccretionGap(band, gapAng + MathX.Tau / Game.AccretionSlots), "its gaps are where the open slots are");
        // A bot inside it walks out through a gap.
        g.Bot = true;
        for (int f = 0; f < 60 * 5; f++) { Quiet(); Step(host, dt); }
        g.Bot = false;
        float outR = me.Pos.DistanceTo(band.Center);
        Console.WriteLine("  a bot in the ring ends {0:0} px from its middle (the ring is {1:0} px out)", outR, g.AccretionBands.Count > 0 ? g.AccretionBands[0].Radius : 0f);
        Check(g.AccretionBands.Count == 0 || outR > g.AccretionBands[0].Radius + 20f, "a bot inside the ring gets out through a gap");
        foreach (var o in ringE) o.Hp = 0f;
        run(0.1f, null);
        run(Game.AccretionLife, null);
        Check(g.AccretionBands.Count == 0, "the band goes when the ring is gone");
        host.Leave("");

        // The cycle: ten lessons, the exam brings the ring.
        var ls = Approaches.For(EnemyType.Singularity).Lessons;
        Check(ls != null && ls.Length == 10 && ls[9].Finale && ls.Take(9).All(l => !l.Finale), "ten lessons; the tenth is the exam, with the Accretion ring");
        var stars = ls.Where(l => l.Star.HasValue).Select(l => l.Star.Value).ToList();
        Check(stars.SequenceEqual(new[] { EnemyType.VoidAcolyte, EnemyType.Lens, EnemyType.Graviton, EnemyType.Pulsar, EnemyType.Collapsar, EnemyType.Orbiter, EnemyType.WhiteDwarf }),
            "the new enemies come one a wave: Acolyte, Lens, Graviton, Pulsar, Collapsar, Orbiter, White Dwarf");

        // The faction: the three come in the waves before the Singularity (50-wave track: 16-19).
        var f0 = Approaches.For(EnemyType.Singularity);
        Check(f0 != null && f0.Members.Select(m => m.Type).SequenceEqual(new[] { EnemyType.Debris, EnemyType.VoidAcolyte, EnemyType.Lens, EnemyType.Graviton, EnemyType.Orbiter, EnemyType.Pulsar, EnemyType.Collapsar, EnemyType.WhiteDwarf })
            && f0.Squads.Select(q => q.Name).SequenceEqual(new[] { "Infall", "Binary", "Gravity trap", "Lens array" }),
            "the Singularity's faction: eight enemies like the Warden's (Debris, Acolyte, Lens, Graviton, Orbiter, Pulsar, Collapsar, White Dwarf); four squads");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var seen = new HashSet<EnemyType>();
        var early = new HashSet<EnemyType>();
        for (int w = 16; w <= 19; w++)
            for (int seed = 0; seed < 12; seed++)
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
        Check(f0.Members.All(m => seen.Contains(m.Type)), "waves 16-19 bring all of them");
        Check(early.Count == 0, "none come before the Singularity's approach");
    }

    static EnemyJob WaveRecipeJob(EnemyType t) => Game.JobOf(t);
}
