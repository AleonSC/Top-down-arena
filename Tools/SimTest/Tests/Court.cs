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
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && CourtKin.Is(k.Key) && !HiveKin.Is(k.Key) && !QuarryKin.Is(k.Key)) && !CourtKin.Is(EnemyType.Shiv),
            "each Court enemy plays like one Warden enemy (Fencer Shiv, Blade Dancer Guard, Parrier Riot Shield, Second Security Guard, Marshal Gas Grenadier, Gallant Taser Guard)");
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
        host.Leave("");

        // --- The approach: the Duelist's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Duelist);
        Check(fac != null && fac.Members.Length == 6 && new[] { "Salon", "Seconds", "Guard line" }.All(n => fac.Squads.Any(s => s.Name == n)),
            "the Duelist has steps 1 and 2's six in its faction, with the Salon, Seconds and Guard line squads");
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
        Check(seen.SetEquals(court), "the waves before the Duelist bring all six (no more basic placeholder faction)");
    }
}
