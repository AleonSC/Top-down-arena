using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Devourer's faction, The Larder, step 1 (Core/LarderFaction.cs, Docs/Approaches.md "The Larder"): the Gnawling plays
// like a Rioter, the Butcher like a Guard that chops a half-circle, the Gulper like a Thug that breathes you in; step 2:
// the Carcass Bearer (a Riot Shield whose broken meat is a morsel a Gnawling eats) and the Bile Spitter (a Gas Grenadier's bile).
static partial class Program
{
    static void LarderTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kin = new Dictionary<EnemyType, EnemyType>
        {
            { EnemyType.Gnawling, EnemyType.Rioter }, { EnemyType.Butcher, EnemyType.Guard }, { EnemyType.Gulper, EnemyType.Thug },
            { EnemyType.CarcassBearer, EnemyType.RiotShield }, { EnemyType.BileSpitter, EnemyType.GasGrenadier }, { EnemyType.MawPriest, EnemyType.TaserGuard },
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && LarderKin.Is(k.Key) && !HiveKin.Is(k.Key) && !QuarryKin.Is(k.Key)) && !LarderKin.Is(EnemyType.Thug),
            "each Larder enemy plays like one Warden enemy (Gnawling Rioter, Butcher Guard, Gulper Thug, Carcass Bearer Riot Shield, Bile Spitter Gas Grenadier, Maw Priest Taser Guard)");
        Check(new[] { EnemyType.Gnawling, EnemyType.Butcher, EnemyType.CarcassBearer, EnemyType.BileSpitter }.All(t => { var a = Defs.Get(t); var b = Defs.Get(kin[t]); return a.Hp == b.Hp && a.Speed == b.Speed && a.Contact == b.Contact && a.Xp == b.Xp; }),
            "the Gnawling, Butcher, Carcass Bearer and Bile Spitter have their counterpart's health, speed, contact damage and XP");
        Check(Defs.Get(EnemyType.Gulper).Hp > Defs.Get(EnemyType.Thug).Hp && Game.IsBrute(EnemyType.Gulper), "the Gulper is a tougher Brute (180 health)");
        Check(kin.Keys.Concat(new[] { EnemyType.Gutworm, EnemyType.HookThrower }).All(t => Game.EnemyNote(t).Length > 0 && BestiaryData.For(t) != null && LarderKin.Is(t)), "each has a Bestiary entry and a new-enemy note");
        Check(Game.IsHook(EnemyType.HookThrower) && Game.IsNonShooter(EnemyType.HookThrower) && !Game.IsRetired(EnemyType.HookThrower), "the Hook Thrower throws like the Hookman, and isn't retired");

        // --- Playing them: a solo host, one enemy at a time -------------------------------------------------------------------
        Game.SeedForTests(4417);
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
            me.Hp = me.MaxHp; me.StunT = 0f; me.InvulnT = 0f;
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f; e.Spawning = false; e.Elite = false; e.Traits = 0; e.Hp = 1e6f;
            return e;
        };
        Action<float, Func<bool>> run = (secs, stop) => { for (int f = 0; f < secs * 60; f++) { Quiet(); Step(host, dt); if (stop != null && stop()) break; } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        var gn = put(EnemyType.Gnawling, new V2(300f, 400f));
        float d0 = gn.Pos.DistanceTo(me.Pos);
        run(2f, null);
        Check(gn.Pos.DistanceTo(me.Pos) < d0 - 100f, "a Gnawling closes in on you");

        // The Butcher: out of range it walks at you; close, it stands, raises its cleaver, then chops the half-circle in front.
        var far = put(EnemyType.Butcher, new V2(300f, 400f));
        run(1f, null);
        Check(far.LarderStage == Game.LarderIdle && far.Pos.X > 340f, "a Butcher out of range walks at you without chopping");
        g.DevSetGod(1, false);
        me.Pos = me.Disp = new V2(900f, 400f);
        var bu = put(EnemyType.Butcher, new V2(840f, 400f));
        run(0.3f, () => bu.LarderStage != Game.LarderIdle);
        Check(bu.LarderStage == Game.LarderWinding && Game.LarderPlanted(bu), "close in, it raises its cleaver");
        V2 bpos = bu.Pos;
        float hp0 = me.Hp;
        run(1f, () => bu.LarderStage == Game.LarderIdle);
        Check(bu.Pos.DistanceTo(bpos) < 1f, "it stands still through the wind-up and the chop");
        Check(me.Hp < hp0, "the chop hurts you in front of it (" + (hp0 - me.Hp).ToString("0") + ")");
        Check(bu.LarderStage == Game.LarderIdle && bu.LarderCd > 0f, "then it rests before the next chop");
        // Stepping behind it during the wind-up, the chop misses.
        me.Pos = me.Disp = new V2(900f, 400f);
        var bb = put(EnemyType.Butcher, new V2(840f, 400f));
        run(0.3f, () => bb.LarderStage != Game.LarderIdle);
        me.Pos = me.Disp = bb.Pos + V2.FromAngle(bb.Facing + MathX.Pi) * 60f;
        hp0 = me.Hp;
        run(0.5f, () => bb.LarderStage == Game.LarderIdle);
        Check(me.Hp >= hp0 - 0.01f || me.Pos.DistanceTo(bb.Pos) < bb.Radius + me.Def.HitR + 2f, "behind it, the chop misses");

        // The Gulper: in range it swells, then breathes in; the cone pulls you toward its mouth, outside it nothing does.
        me.Pos = me.Disp = new V2(900f, 400f);
        var gu = put(EnemyType.Gulper, new V2(720f, 400f));
        run(0.5f, () => gu.LarderStage != Game.LarderIdle);
        Check(gu.LarderStage == Game.LarderWinding, "in range a Gulper's cheeks swell");
        V2 gat = gu.Pos;
        run(1f, () => gu.LarderStage == Game.LarderActing);
        Check(Game.Gulping(gu), "then it breathes in");
        V2 pull = g.DevourPull(me.Pos);
        Check(pull.Length > Game.GulpPull - 1f && pull.X < 0f, "in the cone you're pulled toward its mouth (" + pull.Length.ToString("0") + " px/s)");
        Check(g.DevourPull(gu.Pos + V2.FromAngle(gu.Facing + 1.2f) * 120f).Length < 0.01f, "out of the cone, beside it, nothing pulls");
        Check(g.DevourPull(gu.Pos + V2.FromAngle(gu.Facing) * (Game.GulpReach + 30f)).Length < 0.01f, "past its reach, nothing pulls");
        float before = me.Pos.DistanceTo(gu.Pos);
        hp0 = me.Hp;
        run(1.2f, () => gu.LarderStage == Game.LarderIdle);
        Check(gu.Pos.DistanceTo(gat) < 1f, "it stands still while it breathes in");
        Check(me.Pos.DistanceTo(gu.Pos) < before - 60f, "you were drawn in (" + before.ToString("0") + " to " + me.Pos.DistanceTo(gu.Pos).ToString("0") + " px)");
        // Reaching the mouth, it bites and the breath ends.
        me.Pos = me.Disp = new V2(900f, 400f);
        var gm = put(EnemyType.Gulper, new V2(720f, 400f));
        run(1.2f, () => Game.Gulping(gm));
        me.Pos = me.Disp = gm.Pos + V2.FromAngle(gm.Facing) * (gm.Radius + 6f);
        hp0 = me.Hp;
        run(0.1f, () => gm.LarderStage == Game.LarderIdle);
        Check(me.Hp < hp0 && gm.LarderStage == Game.LarderIdle, "at its mouth it bites, and the breath ends");
        g.DevSetGod(1, true);

        // --- Step 2 ------------------------------------------------------------------------------------------------------------
        // The Carcass Bearer: broken, its meat drops in front as a morsel; a Gnawling nearby goes for it, eats it and grows.
        me.Pos = me.Disp = new V2(1100f, 400f);
        var cb = put(EnemyType.CarcassBearer, new V2(500f, 400f));
        Check(Math.Abs(cb.ShieldMax - cb.MaxHp * Game.RiotShieldShare) < 0.01f, "a Carcass Bearer's meat holds 4x its health");
        cb.Facing = 0f;
        typeof(Game).GetMethod("WearRiotShield", np).Invoke(g, new object[] { cb, cb.ShieldMax + 1f });
        var morsel = g.Enemies.Values.FirstOrDefault(x => x.Type == EnemyType.Morsel);
        Check(Game.RiotShieldBroken(cb) && morsel != null && morsel.Pos.X > cb.Pos.X, "broken, its meat drops in front of it as a morsel");
        var gnaw = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Gnawling, new V2(500f, 640f), 0f, 0, false });
        gnaw.SpawnT = 0f; gnaw.Spawning = false; gnaw.Elite = false; gnaw.Traits = 0;
        float gMax = gnaw.MaxHp;
        run(4f, () => gnaw.LarderStage == Game.GnawFed);
        Check(!g.Enemies.ContainsKey(morsel.Id) && gnaw.LarderStage == Game.GnawFed, "a Gnawling nearby goes for the morsel and eats it");
        Check(Math.Abs(gnaw.MaxHp - gMax * Game.GnawFeed) < 0.01f, "fed, it has 50% more health (" + gMax + " to " + gnaw.MaxHp + ")");
        // With nobody to eat it, you squash a morsel by walking over it.
        var cb2 = put(EnemyType.CarcassBearer, new V2(500f, 400f));
        cb2.Facing = 0f;
        typeof(Game).GetMethod("WearRiotShield", np).Invoke(g, new object[] { cb2, cb2.ShieldMax + 1f });
        var m2 = g.Enemies.Values.First(x => x.Type == EnemyType.Morsel);
        run(0.5f, null);
        me.Pos = me.Disp = m2.Pos;
        run(0.3f, () => !g.Enemies.ContainsKey(m2.Id));
        Check(!g.Enemies.ContainsKey(m2.Id) || m2.Hp <= 0f, "walking over a morsel squashes it");

        // The Bile Spitter: a glob lands where you stand as a bile pool that burns.
        g.DevSetGod(1, false);
        me.Pos = me.Disp = new V2(800f, 400f);
        var bs = put(EnemyType.BileSpitter, new V2(520f, 400f));
        PathPatch pool = null;
        float hpB = me.Hp;
        run(9f, () => { pool = pool ?? g.Patches.Values.FirstOrDefault(p => p.Kind == PatchKind.Bile && p.Active); return pool != null && me.Hp < hpB; });
        Check(pool != null && pool.Pos.DistanceTo(me.Pos) < 60f && Math.Abs(pool.Radius - Game.SpitPoolR) < 0.01f, "a Bile Spitter's glob lands on you (standing still) as a " + (Game.SpitPoolR * 2f) + " px pool");
        Check(me.Hp < hpB, "the pool burns you (" + (hpB - me.Hp).ToString("0.0") + ")");
        bs.Pos = new V2(200f, 200f);
        run(Game.SpitPoolLife + 1.5f, () => { bs.FireT = 99f; bs.Winding = false; return false; });  // (it holds its fire; the wave stays on)
        Check(!g.Patches.Values.Any(p => p.Kind == PatchKind.Bile), "the pool dries up after " + Game.SpitPoolLife + " s");
        g.DevSetGod(1, true);

        // --- Step 3 ------------------------------------------------------------------------------------------------------------
        // The Gutworm: dives (can't be hurt), chases you underground, marks the ground, then erupts: a bite and a ring.
        g.DevSetGod(1, false);
        me.Pos = me.Disp = new V2(900f, 400f);
        var gw = put(EnemyType.Gutworm, new V2(700f, 400f));
        gw.Hp = gw.MaxHp;
        run(0.3f, () => Game.GutwormUnder(gw));
        Check(Game.GutwormUnder(gw), "a Gutworm dives");
        float gwHp = gw.Hp;
        typeof(Game).GetMethod("HurtEnemy", np).Invoke(g, new object[] { gw, 30f, me.Id });
        Check(gw.Hp == gwHp, "underground it can't be hurt");
        float gd0 = gw.Pos.DistanceTo(me.Pos);
        run(Game.GutChase + 0.1f, () => gw.LarderStage == Game.LarderActing);
        Check(gw.LarderStage == Game.LarderActing && gw.Pos.DistanceTo(me.Pos) < Math.Min(40f, gd0 - 100f), "its ripple chases you down, then stops and marks the ground");
        V2 mark = gw.Pos;
        float hpG = me.Hp;
        bool ring = false;
        run(Game.GutMark + 0.2f, () => { foreach (var b in g.Bullets.Values) ring |= b.Source == EnemyType.Gutworm; return ring; });
        Check(gw.Pos.DistanceTo(mark) < 1f, "it stays put while it's marked");
        Check(ring && !Game.GutwormUnder(gw) && g.Bullets.Values.Count(b => b.Source == EnemyType.Gutworm) == Game.GutRing, "then it erupts with a ring of " + Game.GutRing);
        Check(me.Hp < hpG, "standing in the mark, you're bitten (" + (hpG - me.Hp).ToString("0") + ")");
        typeof(Game).GetMethod("HurtEnemy", np).Invoke(g, new object[] { gw, 30f, me.Id });
        Check(gw.Hp < gwHp, "on the surface it can be hurt");

        // The Hook Thrower: shows its line, throws, a hit hurts and reels you in.
        me.Pos = me.Disp = new V2(800f, 400f);
        var ht = put(EnemyType.HookThrower, new V2(560f, 400f));
        ht.PhaseT = 0f;
        run(1f, () => ht.Phase == 1);
        Check(ht.Phase == 1, "a Hook Thrower shows its line");
        float hpH = me.Hp, dH = me.Pos.DistanceTo(ht.Pos);
        bool reeled = false;
        run(2f, () => { reeled |= ht.Phase == 3; return reeled; });
        run(0.4f, null);
        Check(reeled && me.Hp < hpH, "its hook hits and hurts (" + (hpH - me.Hp).ToString("0") + ")");
        Check(me.Pos.DistanceTo(ht.Pos) < dH - 50f && me.Pos.DistanceTo(ht.Pos) > dH - Game.HookThrowYank - 20f, "and reels you in about " + Game.HookThrowYank + " px (" + dH.ToString("0") + " to " + me.Pos.DistanceTo(ht.Pos).ToString("0") + ")");

        // The Maw Priest: shots into its maw heal it, shots in its back do more; it turns slowly; it fires a fan of three.
        g.DevSetGod(1, true);
        me.Pos = me.Disp = new V2(900f, 400f);
        var mp = put(EnemyType.MawPriest, new V2(600f, 400f));
        mp.Facing = 0f;
        Check(Game.InPriestMaw(mp, me.Pos) && !Game.InPriestMaw(mp, mp.Pos - new V2(200f, 0f)), "its maw is in front of it");
        var blocks = typeof(Game).GetMethod("BlocksShot", np);
        var hurt = typeof(Game).GetMethod("HurtEnemy", np);
        mp.Hp = mp.MaxHp * 0.5f;
        float h0 = mp.Hp;
        blocks.Invoke(g, new object[] { mp, mp.Pos + new V2(100f, 0f) });
        hurt.Invoke(g, new object[] { mp, 10f, me.Id });
        Check(Math.Abs(mp.Hp - (h0 + 5f)) < 0.01f, "a shot into its maw heals it for half");
        h0 = mp.Hp;
        blocks.Invoke(g, new object[] { mp, mp.Pos - new V2(100f, 0f) });
        hurt.Invoke(g, new object[] { mp, 10f, me.Id });
        Check(h0 - mp.Hp > 12f, "a shot in its back does more (" + (h0 - mp.Hp).ToString("0.0") + " of 10)");
        mp.Hp = 1e6f; mp.MaxHp = 1e6f;
        me.Pos = me.Disp = mp.Pos - new V2(300f, 0f);  // walk round behind it
        run(1f, null);
        float turned = Math.Abs(MathX.Fmod(mp.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
        Check(turned < MathX.Pi - 0.2f && turned > 0.3f, "it turns slowly toward you (" + (turned * 180f / MathX.Pi).ToString("0") + "° in 1 s)");
        int fan = 0;
        run(6f, () => { fan = g.Bullets.Values.Count(b => b.Source == EnemyType.MawPriest); return fan >= 3; });
        Check(fan == 3, "it fires a fan of three");
        host.Leave("");

        // --- The approach: the Devourer's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Devourer);
        Check(fac != null && fac.Members.Select(m => m.Type).SequenceEqual(new[] { EnemyType.Gnawling, EnemyType.Butcher, EnemyType.Gulper, EnemyType.CarcassBearer, EnemyType.BileSpitter, EnemyType.Gutworm, EnemyType.HookThrower, EnemyType.MawPriest })
            && new[] { "Feeding Frenzy", "Larder Wall", "Meat Hooks", "Gullet", "Bile Rain" }.All(n => fac.Squads.Any(s => s.Name == n)), "the Devourer has a faction: all eight, with five squads");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var larder = new HashSet<EnemyType>(fac.Members.Select(m => m.Type));
        var seen = new HashSet<EnemyType>();
        int devWave = BossSchedule.Campaign.NextBossWaveFrom(1);
        for (int n = 1; n < 200 && BossSchedule.Campaign.BossAt(devWave) != EnemyType.Devourer; n++)
            devWave = BossSchedule.Campaign.NextBossWaveFrom(devWave + 1);
        Check(BossSchedule.Campaign.BossAt(devWave) == EnemyType.Devourer, "the Campaign has a Devourer wave (" + devWave + ")");
        for (int n = devWave - 4; n < devWave; n++)
        for (int seed = 0; seed < 6; seed++)
        {
            Game.SeedForTests(9300 + n * 7 + seed);
            var cg = new Session().Game;
            cg.Wave = n;
            build.Invoke(cg, new object[] { n });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(larder.Contains))
                seen.Add(t);
        }
        Console.WriteLine("  seen before wave " + devWave + ": " + string.Join(", ", seen));
        Check(seen.SetEquals(larder), "the waves before the Devourer bring all eight");
    }
}
