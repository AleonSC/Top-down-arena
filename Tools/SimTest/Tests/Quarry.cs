using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Colossus's faction, The Quarry, step 1 (Core/QuarryFaction.cs, Docs/Approaches.md "The Quarry"): Rubble plays like a
// Rioter, the Quarryman like a Guard, the Stomper like a Thug that stomps out a small ring of push blocks with one gap.
// Step 2: the Slab Bearer (a Riot Shield whose broken slab is cover) and the Rock Hurler (a Gas Grenadier with a boulder).
// Step 3: the Driller (a Shiv whose drill leaves a crack), the Geomancer (sends a crack) and the Cinder Priest (a magma bolt, lava).
static partial class Program
{
    static void QuarryTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kin = new Dictionary<EnemyType, EnemyType>
        {
            { EnemyType.Rubble, EnemyType.Rioter }, { EnemyType.Quarryman, EnemyType.Guard }, { EnemyType.Stomper, EnemyType.Thug },
            { EnemyType.SlabBearer, EnemyType.RiotShield }, { EnemyType.RockHurler, EnemyType.GasGrenadier },
            { EnemyType.Driller, EnemyType.Shiv }, { EnemyType.Geomancer, EnemyType.TaserGuard }, { EnemyType.CinderPriest, EnemyType.TaserGuard },
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && QuarryKin.Is(k.Key) && !HiveKin.Is(k.Key)) && !QuarryKin.Is(EnemyType.Thug),
            "each Quarry enemy plays like one Warden enemy (Rubble Rioter, Quarryman Guard, Stomper Thug, Slab Bearer Riot Shield, Rock Hurler Gas Grenadier, Driller Shiv, Geomancer and Cinder Priest Taser Guard)");
        Check(new[] { EnemyType.Rubble, EnemyType.Quarryman, EnemyType.SlabBearer, EnemyType.RockHurler, EnemyType.Driller, EnemyType.Geomancer, EnemyType.CinderPriest }.All(t => { var a = Defs.Get(t); var b = Defs.Get(kin[t]); return a.Hp == b.Hp && a.Speed == b.Speed && a.Contact == b.Contact && a.Xp == b.Xp; }),
            "all but the Stomper have their counterpart's health, speed, contact damage and XP");
        Check(Defs.Get(EnemyType.Stomper).Hp > Defs.Get(EnemyType.Thug).Hp && Game.IsBrute(EnemyType.Stomper), "the Stomper is a tougher Brute (180 health)");
        Check(kin.Keys.All(t => Game.EnemyNote(t).Length > 0 && BestiaryData.For(t) != null), "each has a Bestiary entry and a new-enemy note");

        // --- Playing them: a solo host, one enemy at a time -------------------------------------------------------------------
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
            me.Hp = me.MaxHp; me.StunT = 0f;
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f; e.Spawning = false; e.Elite = false; e.Traits = 0;
            return e;
        };
        Action<float, Func<bool>> run = (secs, stop) => { for (int f = 0; f < secs * 60; f++) { Quiet(); Step(host, dt); if (stop != null && stop()) break; } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        foreach (var t in new[] { EnemyType.Rubble, EnemyType.Quarryman })
        {
            var e = put(t, new V2(300f, 400f));
            float d0 = e.Pos.DistanceTo(me.Pos);
            run(2f, null);
            Check(e.Pos.DistanceTo(me.Pos) < d0 - 100f, "a " + Defs.Get(t).Name + " closes in on you");
        }

        // A Stomper out of range walks at you and doesn't stomp.
        var far = put(EnemyType.Stomper, new V2(200f, 400f));
        run(1.5f, null);
        Check(far.Stomp == null && far.Pos.X > 240f, "a Stomper out of range walks at you without stomping");

        // In range: it plants its feet with the gap showing, then the ring spreads, hurts you once and fades.
        g.DevSetGod(1, false);
        var sm = put(EnemyType.Stomper, new V2(720f, 400f));
        sm.Hp = 1e6f;
        run(0.5f, () => sm.Stomp != null);
        Check(sm.Stomp != null && sm.Stomp.Warn > 0f && Game.StomperPlanted(sm), "within range it lifts its foot: the ring winds up");
        V2 planted = sm.Pos;
        float gapOff = Math.Abs(MathX.Fmod(sm.Stomp.Gap1 - (me.Pos - sm.Pos).Angle + MathX.Pi, MathX.Tau) - MathX.Pi);
        Check(gapOff > 0.2f && gapOff < 1f, "its gap is a short step to one side of you (" + gapOff.ToString("0.00") + " rad)");
        run(0.6f, () => sm.Stomp.Warn <= 0f);
        Check(sm.Pos.DistanceTo(planted) < 1f, "it stands still while its foot is up");
        float hp0 = me.Hp;
        StompRing ring = sm.Stomp;
        bool hit = false;
        run(2f, () => { hit |= ring.Hit.Contains(me.Id); return sm.Stomp == null; });
        Check(hit && me.Hp < hp0, "the ring passes over you outside the gap and hurts you once");
        Check(sm.Stomp == null && Game.RingBack(ring) > Game.StomperReach, "the ring fades at StomperReach (" + Game.StomperReach + " px)");
        Check(me.Pos.DistanceTo(planted) < Game.RingBack(ring) + 1f, "you end up behind the ring, not carried along");

        // Standing in the gap, it passes you by.
        var sg = put(EnemyType.Stomper, new V2(720f, 400f));
        sg.Hp = 1e6f;
        me.Pos = me.Disp = new V2(900f, 400f);
        run(0.5f, () => sg.Stomp != null);
        Check(sg.Stomp != null, "it stomps again");
        float dist = me.Pos.DistanceTo(sg.Pos);
        me.Pos = me.Disp = sg.Pos + V2.FromAngle(sg.Stomp.Gap1) * dist;  // step into the gap
        hp0 = me.Hp;
        var ring2 = sg.Stomp;
        run(2f, () => sg.Stomp == null);
        Check(!ring2.Hit.Contains(me.Id), "standing in the gap, the ring passes you by");

        // The Slab Bearer: its shield is a Riot Shield's; broken, the slab drops in front as cover that stops shots both ways.
        g.DevSetGod(1, true);
        me.Pos = me.Disp = new V2(900f, 400f);
        var sb = put(EnemyType.SlabBearer, new V2(500f, 400f));
        Check(Math.Abs(sb.ShieldMax - sb.MaxHp * Game.RiotShieldShare) < 0.01f, "a Slab Bearer's slab holds 4x its health");
        sb.Facing = 0f;
        typeof(Game).GetMethod("WearRiotShield", np).Invoke(g, new object[] { sb, sb.ShieldMax + 1f });
        var slab = g.Patches.Values.FirstOrDefault(p => p.Kind == PatchKind.Slab);
        Check(Game.RiotShieldBroken(sb) && slab != null && slab.Pos.X > sb.Pos.X, "broken, the slab drops in front of it (a Slab patch)");
        var spawnB = typeof(Game).GetMethod("SpawnBullet", np);
        var mine = (Bullet)spawnB.Invoke(g, new object[] { Defs.TankShot, slab.Pos + new V2(60f, 0f), new V2(-600f, 0f), 0f, 2f, 5f, me.Id, false, 0f });
        var theirs = (Bullet)spawnB.Invoke(g, new object[] { Defs.SmallShot, slab.Pos - new V2(60f, 0f), new V2(600f, 0f), 0f, 2f, 5f, 0, false, 0f });
        float hp1 = sb.Hp;
        run(0.2f, null);
        Check(!g.Bullets.ContainsKey(mine.Id) && !g.Bullets.ContainsKey(theirs.Id) && sb.Hp == hp1, "the dropped slab stops your shots and theirs");
        run(Game.SlabCoverLife + 0.3f, null);
        Check(!g.Patches.Values.Any(p => p.Kind == PatchKind.Slab), "the slab is gone after " + Game.SlabCoverLife + " s");

        // The Rock Hurler: a boulder lands where you stand, hurts, bursts into shards and leaves a pile.
        g.DevSetGod(1, false);
        me.Pos = me.Disp = new V2(800f, 400f);
        var rh = put(EnemyType.RockHurler, new V2(520f, 400f));
        rh.Hp = 1e6f;
        PathPatch pile = null;
        bool shards = false;
        float hp2 = me.Hp;
        run(9f, () => { pile = pile ?? g.Patches.Values.FirstOrDefault(p => p.Kind == PatchKind.Rubble && p.Active); foreach (var b in g.Bullets.Values) shards |= b.Source == EnemyType.RockHurler; return pile != null && shards; });
        Check(pile != null && pile.Pos.DistanceTo(me.Pos) < 80f, "a Rock Hurler's boulder lands on you (standing still) as a rubble pile");
        Check(shards && me.Hp < hp2, "it hurts and bursts into shards");
        g.DevSetGod(1, true);

        // The Driller: its leap drills a crack along its path, which erupts a moment after it stops.
        me.Pos = me.Disp = new V2(900f, 400f);
        var dr = put(EnemyType.Driller, new V2(640f, 400f));
        dr.Hp = 1e6f;
        run(6f, () => dr.Crack != null && dr.Phase != 2 && dr.Crack.Len >= dr.Crack.Max);
        Check(dr.Crack != null && dr.Crack.Len > 60f, "a Driller's leap leaves a crack along its path (" + (dr.Crack != null ? dr.Crack.Len.ToString("0") : "none") + " px)");
        g.DevSetGod(1, false);
        if (dr.Crack != null)
            me.Pos = me.Disp = dr.Crack.From + V2.FromAngle(dr.Crack.Ang) * (dr.Crack.Len * 0.5f);
        float hp3 = me.Hp;
        run(1f, () => dr.Crack == null);
        Check(dr.Crack == null && me.Hp < hp3, "the crack erupts and hurts whoever stands on it");
        g.DevSetGod(1, true);

        // The Geomancer sends a crack at you; standing on it, it hurts when it erupts.
        g.DevSetGod(1, false);
        me.Pos = me.Disp = new V2(860f, 400f);
        var geo = put(EnemyType.Geomancer, new V2(560f, 400f));
        geo.Hp = 1e6f;
        float hp4 = me.Hp;
        ColCrack gk = null;
        run(8f, () => { gk = gk ?? geo.Crack; me.Pos = me.Disp = new V2(860f, 400f); return gk != null && geo.Crack == null; });
        Check(gk != null && Math.Abs(MathX.Fmod(gk.Ang - (me.Pos - geo.Pos).Angle + MathX.Pi, MathX.Tau) - MathX.Pi) < 0.3f, "a Geomancer sends a crack at you");
        Check(geo.Crack == null && me.Hp < hp4, "it erupts under you");
        g.DevSetGod(1, true);

        // The Cinder Priest's magma bolt leaves lava where it stops.
        me.Pos = me.Disp = new V2(860f, 400f);
        var cp = put(EnemyType.CinderPriest, new V2(600f, 400f));
        cp.Hp = 1e6f;
        bool bolt = false;
        run(8f, () => { foreach (var b in g.Bullets.Values) bolt |= b.Kind == Defs.MagmaBolt; return g.Patches.Values.Any(p => p.Kind == PatchKind.Lava); });
        Check(bolt && Defs.IsEnemyShot(Defs.MagmaBolt), "a Cinder Priest fires a magma bolt (Defs.MagmaBolt)");
        Check(g.Patches.Values.Any(p => p.Kind == PatchKind.Lava), "it leaves a lava pool where it stops");
        g.DevSetGod(1, true);
        host.Leave("");

        // --- The approach: the Colossus's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Colossus);
        Check(fac != null && fac.Members.Length == 8 && new[] { "Landslip", "Dig crew", "Quarry", "Wall-in", "Fault line" }.All(n => fac.Squads.Any(s => s.Name == n)),
            "the Colossus has all eight in its faction, with Landslip, Dig crew, Quarry, Wall-in and Fault line");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var quarry = new HashSet<EnemyType>(fac.Members.Select(m => m.Type));
        var seen = new HashSet<EnemyType>();
        int colWave = BossSchedule.Campaign.NextBossWaveFrom(1);
        for (int n = 1; n < 200 && BossSchedule.Campaign.BossAt(colWave) != EnemyType.Colossus; n++)
            colWave = BossSchedule.Campaign.NextBossWaveFrom(colWave + 1);
        Check(BossSchedule.Campaign.BossAt(colWave) == EnemyType.Colossus, "the Campaign has a Colossus wave (" + colWave + ")");
        for (int n = colWave - 4; n < colWave; n++)
        for (int seed = 0; seed < 8; seed++)  // (a wave shows at most 6 types: several seeds to meet all eight)
        {
            Game.SeedForTests(9100 + n * 31 + seed);
            var cg = new Session().Game;
            cg.Wave = n;
            build.Invoke(cg, new object[] { n });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(quarry.Contains))
                seen.Add(t);
        }
        Console.WriteLine("  seen before wave " + colWave + ": " + string.Join(", ", seen));
        Check(seen.SetEquals(quarry), "the waves before the Colossus bring all eight");

        Check(fac.Finale != null && fac.Finale.Name == "Avalanche" && fac.Finale.Form == SquadForm.RiotLine
            && fac.Finale.Members.Count(t => t == EnemyType.SlabBearer) == 4 && fac.Finale.Members.Count(t => t == EnemyType.Stomper) == 2 && fac.Finale.Members.Count(t => t == EnemyType.RockHurler) == 2,
            "the last wave brings the Avalanche: four Slab Bearers as a wall, two Stompers and two Rock Hurlers behind");

        // --- The Avalanche marches as a wall --------------------------------------------------------------------------------
        Game.SeedForTests(4323);
        var ah = new Session { AutoStart = 1, StartWave = 1 };
        ah.Host(nextPort++, "Host", Role.Tank);
        ah.Diff.Set(DiffKnob.Campaign, 0f);
        ah.StartMatch();
        var ag = ah.Game;
        for (int f = 0; f < 60 * 20 && ag.State != MatchState.Fighting; f++)
            Step(ah, dt);
        foreach (var q in new[] { (IList)typeof(Game).GetField("spawnQueue", np).GetValue(ag), (IList)typeof(Game).GetField("squadQueue", np).GetValue(ag) })
            q.Clear();
        ag.Enemies.Clear();
        ag.DevSetGod(1, true);
        var am = ag.Players[1];
        am.Pos = am.Disp = new V2(1300f, 450f);
        typeof(Game).GetMethod("SpawnSquad", np, null, new[] { typeof(SquadDef) }, null)?.Invoke(ag, new object[] { fac.Finale });
        var wall = ag.Enemies.Values.Where(e => e.Type == EnemyType.SlabBearer).ToList();
        var back = ag.Enemies.Values.Where(e => e.Type == EnemyType.Stomper || e.Type == EnemyType.RockHurler).ToList();
        for (int f = 0; f < 60 * 2; f++) { am.Pos = am.Disp = new V2(1300f, 450f); Step(ah, dt); }
        bool behind = wall.Count == 4 && back.Count == 4 && back.All(b => wall.Min(w => w.Pos.DistanceTo(am.Pos)) < b.Pos.DistanceTo(am.Pos));
        Check(behind, "the Avalanche marches as a wall: the Stompers and Rock Hurlers keep behind the slabs (" + wall.Count + " slabs, " + back.Count + " behind)");
        ah.Leave("");

        // --- The ten-wave chart in waves 41-50 of the Campaign (the same order as the Warden's) --------------------------
        var quarrySet = new HashSet<EnemyType>(fac.Members.Select(m => m.Type));
        var first = new Dictionary<EnemyType, int>();
        var squadNames = new Dictionary<int, HashSet<string>>();
        for (int sw = 41; sw <= 50; sw++)
        {
            Game.SeedForTests(8000 + sw);
            var cg = new Session().Game;
            cg.LongRun = true; cg.Wave = (sw + 1) / 2; cg.Leg = (sw - 1) % 2; cg.BossSlot = false;
            build.Invoke(cg, new object[] { cg.Wave });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(quarrySet.Contains))
                if (!first.ContainsKey(t)) first[t] = sw;
            squadNames[sw] = new HashSet<string>(sq.Select(x => x.Name));
        }
        Console.WriteLine("  first waves: " + string.Join(", ", first.OrderBy(k => k.Value).Select(k => k.Key + " " + k.Value)));
        Func<EnemyType, int, bool> firstAt = (t, w) => first.ContainsKey(t) && first[t] == w;
        Check(firstAt(EnemyType.Rubble, 41) && firstAt(EnemyType.Quarryman, 42) && firstAt(EnemyType.Stomper, 43) && firstAt(EnemyType.Geomancer, 44)
            && firstAt(EnemyType.SlabBearer, 46) && firstAt(EnemyType.RockHurler, 47) && firstAt(EnemyType.CinderPriest, 48),
            "waves 41-50: Rubble on 41, Quarryman 42, Stomper 43, Geomancer 44, Slab Bearer 46, Rock Hurler 47, Cinder Priest 48 (the chart's order)");
        Check(first.ContainsKey(EnemyType.Driller) && first[EnemyType.Driller] <= 45, "the Driller is out by wave 45");
        Check(squadNames[50].Contains("Avalanche") && squadNames[50].Contains("Landslip") && Enumerable.Range(41, 9).All(wv => !squadNames[wv].Contains("Avalanche")), "wave 50 is the exam: the Avalanche and a big Landslip, only there");
    }
}
