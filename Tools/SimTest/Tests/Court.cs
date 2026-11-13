using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Duelist's faction, the Masked Court, step 1 (Core/CourtFaction.cs, Docs/Mockups/duelist-faction.html): the Fencer plays
// like a Shiv with a short hop, the Blade Dancer like a Guard that lunges and leaves the Duelist's blades, the Parrier like a
// Riot Shield that sends shots on its front back at whoever fired them.
static partial class Program
{
    static void CourtTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kin = new Dictionary<EnemyType, EnemyType>
        {
            { EnemyType.Fencer, EnemyType.Shiv }, { EnemyType.BladeDancer, EnemyType.Guard }, { EnemyType.Parrier, EnemyType.RiotShield },
            { EnemyType.Second, EnemyType.SecurityGuard }, { EnemyType.Marshal, EnemyType.GasGrenadier }, { EnemyType.Gallant, EnemyType.TaserGuard },
            { EnemyType.Harlequin, EnemyType.Thug }, { EnemyType.Mummer, EnemyType.Shiv },
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && CourtKin.Is(k.Key) && !HiveKin.Is(k.Key) && !QuarryKin.Is(k.Key)) && !CourtKin.Is(EnemyType.Shiv),
            "each Court enemy plays like one Warden enemy (Fencer Shiv, Blade Dancer Guard, Parrier Riot Shield, Second Security Guard, Marshal Gas Grenadier, Gallant Taser Guard, Harlequin Thug, Mummer Shiv)");
        Check(Game.IsLunger(EnemyType.Fencer) && kin.Keys.All(t => Game.EnemyNote(t).Length > 0 && BestiaryData.For(t) != null), "the Fencer is a lunger; each has a Bestiary entry and a new-enemy note");

        // --- Playing them: a solo host, one enemy at a time -------------------------------------------------------------------
        Game.SeedForTests(4330);
        var host = new Session { AutoStart = 1, StartWave = 1 };
        host.Host(nextPort++, "Host", Role.Tank);
        host.Diff.Set(DiffKnob.Campaign, 0f);
        host.StartMatch();
        var g = host.Game;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", np);
        var shoot = typeof(Game).GetMethod("SpawnBullet", np);
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
        Action<float, Func<bool>> run = (secs, stop) => { for (int f = 0; f < secs * 60; f++) { Quiet(); me.Pos = me.Disp = new V2(900f, 400f); Step(host, dt); if (stop != null && stop()) break; } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        // A Fencer closes in, shows a short line, then hops a short way.
        var fe = put(EnemyType.Fencer, new V2(500f, 400f));
        fe.Hp = 1e6f;
        run(4f, () => fe.Phase == 1);
        float startD = fe.Pos.DistanceTo(me.Pos);
        Check(fe.Phase == 1 && startD < Game.FencerStart + 10f, "a Fencer closes to within " + Game.FencerStart + " px before showing its line (" + startD.ToString("0") + ")");
        int warnFrames = 0;
        run(2f, () => { warnFrames++; return fe.Phase == 2; });
        V2 from = fe.Pos;
        Check(fe.Phase == 2 && Math.Abs(warnFrames / 60f - Game.FencerWarn) < 0.06f, "its line shows for " + Game.FencerWarn + " s (" + (warnFrames / 60f).ToString("0.00") + ")");
        run(1f, () => fe.Phase == 3);
        float hop = fe.Pos.DistanceTo(from);
        Check(fe.Phase == 3 && hop > 60f && hop < 140f, "then it hops about 100 px (" + hop.ToString("0") + "), far shorter than a Shiv's leap");

        // A Blade Dancer shows the Duelist's warning line, lunges and leaves blades that fire out sideways.
        var bd = put(EnemyType.BladeDancer, new V2(620f, 400f));
        bd.Hp = 1e6f;
        run(1f, () => Game.DancerBusy(bd));
        Check(bd.Duel != null && bd.Duel.Dash == DuelDash.Dancer && bd.Duel.DashWarn > 0f, "a Blade Dancer in range shows its warning line");
        V2 at0 = bd.Pos;
        run(0.4f, null);
        Check(bd.Pos.DistanceTo(at0) < 1f, "it stands still while the line shows");
        run(1f, () => !Game.DancerBusy(bd));
        int blades = bd.Duel.Blades.Count;
        Check(!Game.DancerBusy(bd) && blades == 3 && bd.Pos.X > at0.X + 150f, "it lunges down the line (" + (bd.Pos.X - at0.X).ToString("0") + " px) and leaves 3 blades (" + blades + ")");
        run(Game.DancerBladeHang + 0.1f, null);
        var bladeShots = g.Bullets.Values.Where(b => b.Source == EnemyType.BladeDancer).ToList();
        Check(bd.Duel.Blades.Count == 0 && bladeShots.Count == 6 && bladeShots.All(b => Math.Abs(b.Vel.Y) > 5f * Math.Abs(b.Vel.X)),
            "after " + Game.DancerBladeHang + " s each blade fires one shot out of each side, across the line (" + bladeShots.Count + " shots)");
        float rest = bd.DanceT;
        Check(rest > Game.DancerEvery - 1.5f, "then it rests before its next lunge (" + rest.ToString("0.0") + " s)");

        // A Parrier sends a shot on its front straight back at whoever fired it; one from behind lands.
        var pa = put(EnemyType.Parrier, new V2(750f, 400f));
        pa.Facing = 0f;
        float hp0 = pa.Hp, sh0 = pa.ShieldHp;
        shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(820f, 400f), new V2(-700f, 0f), 0f, 2f, 10f, 1, false, 0f });
        run(0.2f, () => g.Bullets.Values.Any(b => b.Source == EnemyType.Parrier));
        var back = g.Bullets.Values.FirstOrDefault(b => b.Source == EnemyType.Parrier);
        Check(back != null && back.Vel.X > 250f && pa.Hp == hp0 && pa.ShieldHp < sh0, "a shot on a Parrier's front is parried back at you (and wears its guard, not it)");
        pa = put(EnemyType.Parrier, new V2(750f, 400f));
        pa.Facing = 0f;
        hp0 = pa.Hp;
        shoot.Invoke(g, new object[] { Defs.DpsShot, pa.Pos - new V2(45f, 0f), new V2(700f, 0f), 0f, 2f, 10f, 1, false, 0f });
        run(0.1f, () => pa.Hp < hp0);
        Check(pa.Hp < hp0 && !g.Bullets.Values.Any(b => b.Source == EnemyType.Parrier), "a shot from behind lands, and nothing comes back");

        // --- Step 2 ---------------------------------------------------------------------------------------------------------
        // A Second winds up, then throws an aimed fan of five daggers.
        var se = put(EnemyType.Second, new V2(650f, 400f));
        bool wound = false;
        run(6f, () => { wound |= se.Winding; return g.Bullets.Values.Any(b => b.Kind == Defs.ThrownDagger); });
        var daggers = g.Bullets.Values.Where(b => b.Kind == Defs.ThrownDagger).ToList();
        float mid = daggers.Count > 0 ? daggers.Average(b => b.Vel.Angle) : 9f;
        float spread = daggers.Count > 1 ? daggers.Max(b => b.Vel.Angle) - daggers.Min(b => b.Vel.Angle) : 0f;
        Check(wound && daggers.Count == Game.SecondFan && Math.Abs(mid) < 0.15f && Math.Abs(spread - Game.SecondSpread * (Game.SecondFan - 1)) < 0.02f,
            "a Second winds up, then throws an aimed fan of five daggers (" + daggers.Count + ", " + spread.ToString("0.00") + " rad wide)");
        run(Game.SecondEvery + 1.5f, () => g.Bullets.Values.Count(b => b.Kind == Defs.ThrownDagger) > Game.SecondFan);
        Check(g.Bullets.Values.Count(b => b.Kind == Defs.ThrownDagger) > Game.SecondFan && se.Phase > 0, "it throws again a fan later, and never runs dry");

        // A Marshal draws a circle round you: it forms, holds you in, and its edge hurts.
        var ma = put(EnemyType.Marshal, new V2(500f, 400f));
        run(10f, () => g.Patches.Values.Any(p => p.Kind == PatchKind.DuelCircle));
        var circle = g.Patches.Values.FirstOrDefault(p => p.Kind == PatchKind.DuelCircle);
        Check(circle != null && circle.Pos.DistanceTo(me.Pos) < 5f && circle.R0 == Game.MarshalR && !circle.Active, "a Marshal draws a " + Game.MarshalR + " px circle round where you stand, forming first");
        run(Game.MarshalWarn + 0.05f, null);
        var keep = typeof(Game).GetMethod("KeepInMarshalCircles", np);
        V2 held = (V2)keep.Invoke(g, new object[] { me.Pos, me.Pos + new V2(300f, 0f), me.Def.BodyR });
        V2 free = (V2)keep.Invoke(g, new object[] { circle.Pos + new V2(400f, 0f), circle.Pos + new V2(500f, 0f), me.Def.BodyR });
        Check(circle.Active && held.DistanceTo(circle.Pos) <= circle.R0 - me.Def.BodyR + 0.5f && free.X == circle.Pos.X + 500f, "once formed it holds you in (and leaves anyone outside alone)");
        g.DevSetGod(1, false);
        float mhp = me.Hp;
        for (int f = 0; f < 60; f++) { Quiet(); me.Pos = me.Disp = circle.Pos + new V2(circle.R0 - me.Def.BodyR - 1f, 0f); Step(host, dt); }
        Check(me.Hp < mhp && mhp - me.Hp <= Game.MarshalDamage * 2f + 1f, "leaning on its edge hurts, " + Game.MarshalDamage + " at most every " + Game.MarshalHurtEvery + " s (" + (mhp - me.Hp).ToString("0") + " in 1 s)");
        g.DevSetGod(1, true);
        run(Game.MarshalHold, null);
        Check(!g.Patches.Values.Any(p => p.Kind == PatchKind.DuelCircle), "after " + Game.MarshalHold + " s the circle is gone");

        // A Gallant salutes, then lunges; a perfect parry as it arrives leaves it open (+50%).
        var ga = put(EnemyType.Gallant, new V2(600f, 400f));
        ga.Hp = 1e6f;
        run(6f, () => ga.Duel != null && ga.Duel.SaluteT > 0f);
        Check(ga.Duel != null && ga.Duel.SaluteT > 0f && Game.GallantBusy(ga) && !g.Bullets.Values.Any(b => b.Source == EnemyType.Gallant), "a Gallant in range salutes (it doesn't shoot)");
        V2 sal = ga.Pos;
        bool parried = false;
        for (int i = 0; i < 180 && !parried; i++)
        {
            var dsg = ga.Duel;
            bool striking = dsg.Dash == DuelDash.Salute;
            me.Pos = me.Disp = new V2(900f, 400f);
            host.Poll();
            g.FixedStep(dt, new PlayerInput { AimAt = ga.Pos, Parry = striking });
            g.Update(dt);
            parried = dsg.StaggerT > 0f;
        }
        var hurtM = typeof(Game).GetMethod("HurtEnemy", np, null, new[] { typeof(Enemy), typeof(float), typeof(int) }, null);
        float gb0 = ga.Hp;
        hurtM?.Invoke(g, new object[] { ga, 100f, 1 });
        Check(parried && Game.DuelOpen(ga) && ga.Pos.DistanceTo(sal) > 100f, "it lunges at you, and a perfect parry as it arrives knocks it off balance");
        Check(hurtM != null && Math.Abs(gb0 - ga.Hp - 100f * Game.DuelOpenMult) < 1f, "while it reels it takes 50% more damage (" + (gb0 - ga.Hp).ToString("0") + " from a 100 hit)");
        g.HoldParry = false;

        // --- Step 3 ---------------------------------------------------------------------------------------------------------
        // A Harlequin's Crossing: a line through you and a dash, a vault, the crossing line and a dash; blades on both.
        var hq = put(EnemyType.Harlequin, new V2(700f, 400f));
        hq.Hp = 1e6f;
        run(3f, () => hq.Duel != null && hq.Duel.Dash == DuelDash.Cross);
        Check(hq.Duel != null && hq.Duel.Dash == DuelDash.Cross && hq.Duel.DashWarn > 0f && Game.HarlequinBusy(hq), "a Harlequin in range shows its first line through you");
        float a1 = hq.Duel.DashAng;
        V2 crossAt = hq.CrossAt;
        int maxBlades = 0;
        float a2 = a1;
        bool second = false;
        run(4f, () => { maxBlades = Math.Max(maxBlades, hq.Duel.Blades.Count); if (hq.Duel.Step == 2 && hq.Duel.Dash == DuelDash.Cross) { second = true; a2 = hq.Duel.DashAng; } return second && hq.Duel.Dash == DuelDash.None; });
        float turn = Math.Abs(MathX.Fmod(a2 - a1 + MathX.Pi, MathX.Tau) - MathX.Pi);
        var crossShots = g.Bullets.Values.Count(b => b.Source == EnemyType.Harlequin);
        Check(second && Math.Abs(turn - MathX.Pi / 2f) < 0.35f, "then it vaults and dashes the crossing line, about a right angle to the first (" + turn.ToString("0.00") + " rad)");
        run(1.5f, null);
        crossShots = Math.Max(crossShots, g.Bullets.Values.Count(b => b.Source == EnemyType.Harlequin));
        Check(maxBlades >= 4 && crossShots >= 8 && crossAt.DistanceTo(me.Pos) < 5f, "both arms leave blades that fire out sideways (" + crossShots + " shots), crossing where you stood");

        // A Mummer blinks into three that circle you; only its own shots are solid; its copies give nothing and go with it.
        var mu = put(EnemyType.Mummer, new V2(650f, 400f));
        mu.Hp = 1e6f;
        run(4f, () => Game.MummerMasked(mu));
        var copies = g.Enemies.Values.Where(c => c.Type == EnemyType.MummerCopy && c.ParentId == mu.Id).ToList();
        Check(Game.MummerMasked(mu) && copies.Count == 2 && copies.All(c => Math.Abs(c.Pos.DistanceTo(me.Pos) - Game.MummerRing) < 5f) && Math.Abs(mu.Pos.DistanceTo(me.Pos) - Game.MummerRing) < 5f,
            "a Mummer in range blinks into three, " + Game.MummerRing + " px round you");
        Check(Defs.Get(EnemyType.MummerCopy).Name == Defs.Get(EnemyType.Mummer).Name && Defs.Get(EnemyType.MummerCopy).Xp == 0 && copies.All(c => c.Hp <= 1f), "its copies look the same by name, have 1 health and give no XP");
        V2 p0 = mu.Pos;
        run(2f, null);
        var solid = g.Bullets.Values.Where(b => b.Kind == Defs.SmallShot && b.Source == EnemyType.Mummer).Count();
        var hollow = g.Bullets.Values.Where(b => b.Kind == Defs.HollowShot && b.Source == EnemyType.Mummer).Count();
        Check(mu.Pos.DistanceTo(p0) > 20f && solid >= 1 && hollow >= 2 * solid && Defs.BulletDamage(Defs.HollowShot) == 0f, "they circle you and fire: its shots are solid, the copies' hollow (" + solid + " solid, " + hollow + " hollow)");
        int xp0 = me.Run.Kills;
        var oneCopy = g.Enemies.Values.First(c => c.Type == EnemyType.MummerCopy);
        hurtM.Invoke(g, new object[] { oneCopy, 5f, 1 });
        run(0.1f, null);
        Check(!g.Enemies.ContainsKey(oneCopy.Id) && me.Run.Kills == xp0, "a copy shot down just puffs (no kill counted)");
        mu.Hp = 1f;
        hurtM.Invoke(g, new object[] { mu, 5f, 1 });
        run(0.1f, null);
        Check(!g.Enemies.Values.Any(c => c.Type == EnemyType.MummerCopy || c.Type == EnemyType.Mummer), "kill the real one and its copies vanish");
        var mu2 = put(EnemyType.Mummer, new V2(650f, 400f));
        mu2.Hp = 1e6f;
        run(4f, () => Game.MummerMasked(mu2));
        run(Game.MummerMask + 0.2f, null);
        Check(!Game.MummerMasked(mu2) && !g.Enemies.Values.Any(c => c.Type == EnemyType.MummerCopy), "after " + Game.MummerMask + " s its copies vanish and it plays on");
        host.Leave("");

        // --- The approach: the Duelist's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Duelist);
        Check(fac != null && fac.Members.Length == 8 && new[] { "Salon", "Seconds", "Masked ball", "Guard line" }.All(n => fac.Squads.Any(s => s.Name == n)),
            "the Duelist has all eight in its faction, with the Salon, Seconds, Masked ball and Guard line squads");
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var court = new HashSet<EnemyType>(fac.Members.Select(m => m.Type));
        var seen = new HashSet<EnemyType>();
        int duelWave = BossSchedule.Campaign.NextBossWaveFrom(1);
        for (int n = 1; n < 200 && BossSchedule.Campaign.BossAt(duelWave) != EnemyType.Duelist; n++)
            duelWave = BossSchedule.Campaign.NextBossWaveFrom(duelWave + 1);
        Check(BossSchedule.Campaign.BossAt(duelWave) == EnemyType.Duelist, "the Campaign has a Duelist wave (" + duelWave + ")");
        for (int n = duelWave - 4; n < duelWave; n++)
        for (int seed = 0; seed < 8; seed++)  // (a wave shows at most 6 types: several seeds to meet them all)
        {
            Game.SeedForTests(9300 + n * 31 + seed);
            var cg = new Session().Game;
            cg.Wave = n;
            build.Invoke(cg, new object[] { n });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(court.Contains))
                seen.Add(t);
        }
        Console.WriteLine("  seen before wave " + duelWave + ": " + string.Join(", ", seen));
        Check(seen.SetEquals(court), "the waves before the Duelist bring all eight (no more basic placeholder faction)");

        Check(fac.Finale != null && fac.Finale.Name == "Ring of Honour" && fac.Finale.Form == SquadForm.Honour
            && fac.Finale.Members.Count(t => t == EnemyType.Fencer) == 10 && fac.Finale.Members.Count(t => t == EnemyType.Gallant) == 2,
            "the last wave brings the Ring of Honour: ten Fencers and two Gallants");

        // --- The Ring of Honour: a ring round you that closes; opposite pairs of Fencers lunge in, in turn --------------------
        Game.SeedForTests(4331);
        var rh = new Session { AutoStart = 1, StartWave = 1 };
        rh.Host(nextPort++, "Host", Role.Tank);
        rh.Diff.Set(DiffKnob.Campaign, 0f);
        rh.StartMatch();
        var rg = rh.Game;
        for (int f = 0; f < 60 * 20 && rg.State != MatchState.Fighting; f++)
            Step(rh, dt);
        var rq = new[] { (IList)typeof(Game).GetField("spawnQueue", np).GetValue(rg), (IList)typeof(Game).GetField("squadQueue", np).GetValue(rg) };
        foreach (var q in rq) q.Clear();
        rg.Enemies.Clear();
        rg.DevSetGod(1, true);
        var rm = rg.Players[1];
        V2 rmid = new V2(800f, 450f);
        rm.Pos = rm.Disp = rmid;
        typeof(Game).GetMethod("SpawnSquad", np, null, new[] { typeof(SquadDef) }, null)?.Invoke(rg, new object[] { fac.Finale });
        var ring = rg.Enemies.Values.Where(e => e.Type == EnemyType.Fencer || e.Type == EnemyType.Gallant).ToList();
        foreach (var e in ring) e.Hp = 1e6f;
        float r0 = ring.Count > 0 ? ring.Average(e => e.Pos.DistanceTo(rmid)) : 0f;
        Check(ring.Count == 12 && ring.All(e => Math.Abs(e.Pos.DistanceTo(rmid) - Game.HonourStart) < 40f), "it forms up in a ring " + Game.HonourStart + " px round you (" + ring.Count + ", " + r0.ToString("0") + " px)");
        int maxAtOnce = 0, lungeStarts = 0;
        var wasIdle = ring.ToDictionary(e => e.Id, e => true);
        for (int f = 0; f < 60 * 8; f++)
        {
            foreach (var q in rq) q.Clear();
            rm.Pos = rm.Disp = rmid;
            Step(rh, dt);
            var fen = ring.Where(e => e.Type == EnemyType.Fencer && rg.Enemies.ContainsKey(e.Id)).ToList();
            maxAtOnce = Math.Max(maxAtOnce, fen.Count(e => e.Phase == 1 || e.Phase == 2));
            foreach (var e in fen) { if (e.Phase == 1 && wasIdle[e.Id]) lungeStarts++; wasIdle[e.Id] = e.Phase == 0; }
        }
        var fencersNow = ring.Where(e => e.Type == EnemyType.Fencer && e.Phase == 0).ToList();
        float r8 = fencersNow.Count > 0 ? fencersNow.Average(e => e.Pos.DistanceTo(rmid)) : 0f;
        Console.WriteLine("  ring of honour: {0} lunges in 8 s, at most {1} at once, ring now {2:0} px", lungeStarts, maxAtOnce, r8);
        Check(lungeStarts >= 6 && maxAtOnce <= 4 && r8 < r0 - 40f, "it closes in, and Fencers lunge in by opposite pairs, not all at once");
        rh.Leave("");

        // --- The ten-wave chart in the Long Campaign (the same order as the Warden's) --------------------------------------
        var firstC = new Dictionary<EnemyType, int>();
        var squadNamesC = new Dictionary<int, HashSet<string>>();
        const int lo = 51;  // (the Long Campaign: the Colossus's cycle is waves 41-50, the Duelist's 51-60)
        for (int sw = lo; sw < lo + 10; sw++)
        {
            Game.SeedForTests(8100 + sw);
            var cg = new Session().Game;
            cg.LongRun = true; cg.Wave = (sw + 1) / 2; cg.Leg = (sw - 1) % 2; cg.BossSlot = false;
            build.Invoke(cg, new object[] { cg.Wave });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(court.Contains))
                if (!firstC.ContainsKey(t)) firstC[t] = sw - lo + 1;
            squadNamesC[sw - lo + 1] = new HashSet<string>(sq.Select(x => x.Name));
        }
        Console.WriteLine("  long campaign waves " + lo + "-" + (lo + 9) + ", first lessons: " + string.Join(", ", firstC.OrderBy(k => k.Value).Select(k => k.Key + " " + k.Value)));
        Func<EnemyType, int, bool> firstAtC = (t, w) => firstC.ContainsKey(t) && firstC[t] == w;
        Check(firstAtC(EnemyType.Fencer, 1) && firstAtC(EnemyType.BladeDancer, 2) && firstAtC(EnemyType.Harlequin, 3) && firstAtC(EnemyType.Second, 4)
            && firstAtC(EnemyType.Parrier, 6) && firstAtC(EnemyType.Marshal, 7) && firstAtC(EnemyType.Gallant, 8) && firstC.ContainsKey(EnemyType.Mummer) && firstC[EnemyType.Mummer] <= 5,
            "the Duelist's ten lessons: Fencer, Blade Dancer, Harlequin, Second, Mummer (by 5), Parrier, Marshal, Gallant (the chart's order)");
        Check(squadNamesC[10].Contains("Ring of Honour") && squadNamesC[10].Contains("Salon") && Enumerable.Range(1, 9).All(w => !squadNamesC[w].Contains("Ring of Honour")),
            "the tenth is the exam: the Ring of Honour and a big Salon, only there");
    }
}
