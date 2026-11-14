using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Storm Herald's faction, steps 1-4 (Core/Squall.cs, Docs/StormFaction.md): a Dust Devil shoves you on touch and veers
// off, a Squall Gunner's three shots leave upwind of you and curve back to where you stood, a Stormcaller calls an amber
// strike under you that hurts if you stay; a Grounder grounds shots and claps them back, a Pylon pair's arc, a Lineman's
// mark; a Thunderhead's row of strikes, a wave's Ball Lightning (slows hold it, its shots are its own); the Storm front's
// sweeping wall; and the eight come in the waves before the Storm Herald, the Storm front on the last.
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

        // --- Step 2 ---------------------------------------------------------------------------------------------------
        // Grounder: shots on its face are grounded and charge it; full, it claps a ring with two gaps.
        g.DevSetGod(1, true);
        var gr = put(EnemyType.Grounder, new V2(700f, 400f));
        for (int f = 0; f < 60 * 3; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        Check(Game.GrounderFaces(gr, me.Pos) && !Game.GrounderFaces(gr, gr.Pos * 2f - me.Pos), "a Grounder turns its shield to you; its back is open");
        var spawnB = typeof(Game).GetMethod("SpawnBullet", np);
        float grHp = gr.Hp;
        for (int k = 0; k < 3; k++)
            spawnB.Invoke(g, new object[] { Defs.DpsShot, gr.Pos + (me.Pos - gr.Pos).Normalized() * 60f, (gr.Pos - me.Pos).Normalized() * 600f, 0f, 1f, 10f, 1, false, 0f });
        for (int f = 0; f < 20; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        Console.WriteLine("  Grounder: 3 shots on its face, charge {0}, lost {1:0} hp", gr.Phase, grHp - gr.Hp);
        Check(gr.Phase == 3 && gr.Hp == grHp, "shots on its face are grounded: no damage, each charges it");
        g.Bullets.Clear();
        var ground = typeof(Game).GetMethod("GroundShot", np);
        for (int k = 3; k < Game.GrounderFull; k++) ground.Invoke(g, new object[] { gr });
        typeof(Game).GetMethod("FirePendingClaps", np).Invoke(g, null);  // (the clap fires after Collide)
        var clap = g.Bullets.Values.Where(b => b.Shooter == gr.Id).ToList();
        int gaps = 0;
        var angs = clap.Select(b => b.Vel.Angle).OrderBy(a => a).ToList();
        for (int i = 0; i < angs.Count; i++) { float next = i + 1 < angs.Count ? angs[i + 1] : angs[0] + MathX.Tau; if (next - angs[i] > MathX.Tau / Game.ClapShots * 1.5f) gaps++; }
        Console.WriteLine("  Grounder: clap of {0} shots, {1} gaps", clap.Count, gaps);
        Check(gr.Phase == 0 && clap.Count == Game.ClapShots - 2 * Game.ClapGap && gaps == 2, "full, it claps a ring back with two gaps, and its charge empties");

        // Pylon: comes as a pair; plants either side of you, flickers, fires along the line; kill one and the line is gone.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp = 1e6f;
        put(EnemyType.Pylon, new V2(600f, 300f));
        var pylons = g.Enemies.Values.Where(e => e.Type == EnemyType.Pylon).ToList();
        Check(pylons.Count == 2 && pylons[0].LinkId == pylons[1].Id && pylons[1].LinkId == pylons[0].Id, "a Pylon comes with its partner");
        bool flick = false, fired = false, hurtLine = false, straddle = false, synced = true;
        float pyHp = me.Hp;
        for (int f = 0; f < 60 * 12 && !fired; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            flick |= pylons[0].Phase == Game.PylonFlicker;
            synced &= pylons[0].Phase == pylons[1].Phase;
            if (pylons[0].Phase == Game.PylonFiring) { fired = true; hurtLine = me.Hp < pyHp; straddle = (pylons[0].Pos - home).Normalized().Dot((pylons[1].Pos - home).Normalized()) < -0.5f; }
        }
        Console.WriteLine("  Pylons: flickered {0}, fired {1}, either side of you {2}, hurt you {3}", flick, fired, straddle, hurtLine);
        Check(flick && fired && synced, "the pair plants, flickers, then fires, in step");
        Check(straddle && hurtLine, "they plant either side of you, and the arc hurts on the line");
        typeof(Game).GetMethod("HurtEnemy", np).Invoke(g, new object[] { pylons[1], 1e6f, 1 });
        for (int f = 0; f < 10; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        var lone = g.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Pylon);
        Check(lone != null && g.PylonPartner(lone) == null && Game.PylonLineDistance(lone, null, home) > 1e8f, "kill one and the line is gone");

        // Lineman: marks you; alone, the mark only bursts into shots (no chain, no damage).
        g.DevSetGod(1, true);
        var ln = put(EnemyType.Lineman, new V2(640f, 400f));
        bool marked = false;
        for (int f = 0; f < 60 * 8 && !marked; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); marked = ln.Phase == 1 && ln.LinkId == me.Id; }
        Check(marked, "a Lineman marks you");
        g.Bullets.Clear();
        int burst = 0;
        for (int f = 0; f < 60 * (Game.LineMark + 0.3f) && burst == 0; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            burst = g.Bullets.Values.Count(b => b.Shooter == ln.Id && b.Pos.DistanceTo(home) < 60f);
        }
        Console.WriteLine("  Lineman: the mark burst into {0} shots round you", burst);
        Check(ln.Phase == 0 && burst == Game.LineBurst, "when it runs out, shots burst out round the marked player");

        // --- Step 3 ---------------------------------------------------------------------------------------------------
        // Thunderhead: a row of strikes ahead of it toward you, landing one after another; standing on it hurts.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp = 1e6f;
        var th = put(EnemyType.Thunderhead, new V2(640f, 400f));
        List<Hazard> row = null;
        for (int f = 0; f < 60 * 5 && row == null; f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            var hs = g.Hazards.Values.Where(h => h.Kind == Game.HazardDownburst).ToList();
            if (hs.Count > 0) row = hs.OrderBy(h => h.T).ToList();
        }
        Check(row != null && row.Count == Game.HeadRow, "a Thunderhead drops a row of three strikes");
        if (row != null && row.Count == Game.HeadRow)
        {
            V2 toYou = (home - th.Pos).Normalized();
            bool ahead = row.All(h => (h.Pos - th.Pos).Normalized().Dot(toYou) > 0.95f);
            bool order = row[0].Pos.DistanceTo(th.Pos) < row[1].Pos.DistanceTo(th.Pos) && row[1].T > row[0].T + 0.15f && row[2].T > row[1].T + 0.15f;
            Check(ahead && order, "the row is ahead of it toward you, and lands nearest first, one after another");
            V2 stand = row[1].Pos;
            float hpT = me.Hp;
            for (int f = 0; f < 60 * 1.6f; f++) { Quiet(); me.Pos = me.Disp = stand; Step(host, dt); }
            Console.WriteLine("  Thunderhead: standing on its row cost {0:0} hp", hpT - me.Hp);
            Check(me.Hp < hpT, "standing on the row hurts");
        }

        // Ball Lightning from a wave: slows and freezes hold it, its pop is its own (not the boss's), and it pays like a Shiv.
        g.DevSetGod(1, true);
        var bl = put(EnemyType.BallLightning, new V2(600f, 400f));
        bl.FrozenT = 1f;
        V2 b0p = bl.Pos;
        for (int f = 0; f < 30; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        Check(bl.Pos.DistanceTo(b0p) < 1f, "a frozen Ball Lightning stays put");
        for (int f = 0; f < 60; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); }
        Check(bl.Pos.DistanceTo(b0p) > 20f, "then drifts at you again");
        var killXp = (float)typeof(Game).GetMethod("KillXp", np).Invoke(g, new object[] { bl });
        Check(Math.Abs(killXp - Game.BallWaveXp) < 0.01f, "a wave's Ball Lightning pays " + Game.BallWaveXp + " XP");
        g.Bullets.Clear();
        var popped = new List<Bullet>();
        for (int f = 0; f < 60 * 8 && popped.Count == 0; f++)
        {
            Quiet(); me.Pos = me.Disp = home + new V2(300f, 0f); Step(host, dt);
            popped = g.Bullets.Values.Where(b => b.Source == EnemyType.BallLightning).ToList();
        }
        Console.WriteLine("  Ball Lightning: popped into {0} shots, boss shots {1}", popped.Count, popped.Count(b => b.FromBoss));
        Check(popped.Count == 8 && popped.All(b => !b.FromBoss), "it pops into 8 shots of its own, not the boss's");

        // --- Step 4: the Storm front ----------------------------------------------------------------------------------
        g.DevSetGod(1, true);
        Quiet();
        g.Enemies.Clear(); g.Hazards.Clear(); g.Bullets.Clear();
        var fh = Approaches.For(EnemyType.StormHerald);
        typeof(Game).GetMethod("SpawnSquadAt", np).Invoke(g, new object[] { fh.Finale, (V2?)new V2(300f, 400f) });
        var callers = g.Enemies.Values.Where(e => e.Type == EnemyType.Stormcaller).ToList();
        Check(callers.Count == 6 && g.Enemies.Values.Count(e => e.Type == EnemyType.Grounder) == 2, "the Storm front: six Stormcallers and two Grounders");
        var seenH = new HashSet<int>();
        var columns = new List<List<Hazard>>();
        for (int f = 0; f < 60 * (Game.FrontFirst + Game.FrontEvery * 6 + 0.1f); f++)
        {
            Quiet(); me.Pos = me.Disp = home; Step(host, dt);
            var fresh = g.Hazards.Values.Where(h => seenH.Add(h.Id)).ToList();
            if (fresh.Count > 0) columns.Add(fresh);
        }
        Console.WriteLine("  Storm front: {0} columns so far, sizes {1}", columns.Count, string.Join(" ", columns.Select(c => c.Count)));
        Check(columns.Count >= 5 && columns.All(c => c.Count == Game.FrontSlots - Game.FrontGap && c.All(h => h.Kind == Game.HazardCaller)),
            "it sweeps columns of strikes, two slots open in each (and the callers call none of their own)");
        if (columns.Count >= 2)
        {
            V2 step = columns[1].Aggregate(V2.Zero, (a, h) => a + h.Pos) / columns[1].Count - columns[0].Aggregate(V2.Zero, (a, h) => a + h.Pos) / columns[0].Count;
            Check(step.Length > Game.FrontStep * 0.5f, "each column lands further along than the last");
        }
        foreach (var c in callers) typeof(Game).GetMethod("HurtEnemy", np).Invoke(g, new object[] { c, 1e6f, 1 });
        int before = seenH.Count;
        for (int f = 0; f < 60 * 2; f++) { Quiet(); me.Pos = me.Disp = home; Step(host, dt); foreach (var h in g.Hazards.Values) seenH.Add(h.Id); }
        Check(seenH.Count == before, "kill the callers and the front ends");
        host.Leave("");

        // The faction: the three come in the waves before the Storm Herald (50-wave track: 36-39).
        var f0 = Approaches.For(EnemyType.StormHerald);
        Check(f0 != null && f0.Members.Length == 8 && f0.Members.All(m => Game.IsSquall(m.Type) || m.Type == EnemyType.BallLightning) && f0.Squads.Length == 5,
            "the Storm Herald's faction: all eight; squads Squall line, Storm cell, Power line, Supercell, Grounding wall");
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
                foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(t => Game.IsSquall(t) || t == EnemyType.BallLightning))
                    seen.Add(t);
            }
        for (int w = 2; w <= 34; w++)
        {
            Game.SeedForTests(9700 + w);
            var cg = new Session().Game;
            cg.Wave = w;
            build.Invoke(cg, new object[] { w });
            foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(((List<SquadDef>)squadQ.GetValue(cg)).SelectMany(x => x.Members)).Where(t => Game.IsSquall(t) || t == EnemyType.BallLightning))
                early.Add(t);
        }
        Console.WriteLine("  waves 36-39 bring: " + string.Join(", ", seen));
        Check(fh.Lessons != null && fh.Lessons.Length == 10 && fh.Lessons[9].Finale && fh.Finale.Form == SquadForm.StormFront, "ten lessons, the last the Storm front");
        // The Long Campaign's ten waves before the Storm Herald (71-80): one lesson each, in the chart's order; the exam last.
        var firstL = new Dictionary<EnemyType, int>();
        var squadNamesL = new Dictionary<int, HashSet<string>>();
        const int lo = 71;
        for (int sw = lo; sw < lo + 10; sw++)
        {
            Game.SeedForTests(8700 + sw);
            var cg = new Session().Game;
            cg.LongRun = true; cg.Wave = (sw + 1) / 2; cg.Leg = (sw - 1) % 2; cg.BossSlot = false;
            build.Invoke(cg, new object[] { cg.Wave });
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in ((List<EnemyType>)spawnQ.GetValue(cg)).Concat(sq.SelectMany(x => x.Members)).Where(t => Game.IsSquall(t) || t == EnemyType.BallLightning))
                if (!firstL.ContainsKey(t)) firstL[t] = sw - lo + 1;
            squadNamesL[sw - lo + 1] = new HashSet<string>(sq.Select(x => x.Name));
        }
        Console.WriteLine("  long campaign waves " + lo + "-" + (lo + 9) + ", first lessons: " + string.Join(", ", firstL.OrderBy(k => k.Value).Select(k => k.Key + " " + k.Value)));
        Func<EnemyType, int, bool> firstAtL = (t, w) => firstL.ContainsKey(t) && firstL[t] == w;
        Check(firstAtL(EnemyType.DustDevil, 1) && firstAtL(EnemyType.SquallGunner, 2) && firstAtL(EnemyType.Grounder, 3) && firstAtL(EnemyType.Stormcaller, 4)
            && firstAtL(EnemyType.Pylon, 5) && firstAtL(EnemyType.Thunderhead, 6) && firstAtL(EnemyType.Lineman, 7) && firstAtL(EnemyType.BallLightning, 8),
            "the Storm Herald's ten lessons: Dust Devil, Squall Gunner, Grounder, Stormcaller, Pylon, Thunderhead, Lineman, Ball Lightning (the chart's order)");
        Check(squadNamesL[10].Contains("Storm front") && squadNamesL[10].Contains("Squall line") && Enumerable.Range(1, 9).All(w => !squadNamesL[w].Contains("Storm front")),
            "the tenth is the exam: the Storm front and a big Squall line, only there");
        Check(seen.Count == 8, "waves 36-39 bring all eight");
        Check(early.Count == 0, "none come before the Storm Herald's approach");
    }
}
