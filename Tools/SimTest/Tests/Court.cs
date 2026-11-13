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
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && CourtKin.Is(k.Key) && !HiveKin.Is(k.Key) && !QuarryKin.Is(k.Key)) && !CourtKin.Is(EnemyType.Shiv),
            "each Court enemy plays like one Warden enemy (Fencer Shiv, Blade Dancer Guard, Parrier Riot Shield)");
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
        host.Leave("");

        // --- The approach: the Duelist's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Duelist);
        Check(fac != null && fac.Members.Length == 3 && new[] { "Salon", "Guard line" }.All(n => fac.Squads.Any(s => s.Name == n)),
            "the Duelist has the step-1 three in its faction, with the Salon and Guard line squads");
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
        for (int seed = 0; seed < 4; seed++)
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
        Check(seen.SetEquals(court), "the waves before the Duelist bring all three (no more basic placeholder faction)");
    }
}
