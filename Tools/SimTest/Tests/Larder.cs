using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Devourer's faction, The Larder, step 1 (Core/LarderFaction.cs, Docs/Approaches.md "The Larder"): the Gnawling plays
// like a Rioter, the Butcher like a Guard that chops a half-circle, the Gulper like a Thug that breathes you in.
static partial class Program
{
    static void LarderTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kin = new Dictionary<EnemyType, EnemyType>
        {
            { EnemyType.Gnawling, EnemyType.Rioter }, { EnemyType.Butcher, EnemyType.Guard }, { EnemyType.Gulper, EnemyType.Thug },
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && LarderKin.Is(k.Key) && !HiveKin.Is(k.Key) && !QuarryKin.Is(k.Key)) && !LarderKin.Is(EnemyType.Thug),
            "each Larder enemy plays like one Warden enemy (Gnawling Rioter, Butcher Guard, Gulper Thug)");
        Check(new[] { EnemyType.Gnawling, EnemyType.Butcher }.All(t => { var a = Defs.Get(t); var b = Defs.Get(kin[t]); return a.Hp == b.Hp && a.Speed == b.Speed && a.Contact == b.Contact && a.Xp == b.Xp; }),
            "the Gnawling and the Butcher have their counterpart's health, speed, contact damage and XP");
        Check(Defs.Get(EnemyType.Gulper).Hp > Defs.Get(EnemyType.Thug).Hp && Game.IsBrute(EnemyType.Gulper), "the Gulper is a tougher Brute (180 health)");
        Check(kin.Keys.All(t => Game.EnemyNote(t).Length > 0 && BestiaryData.For(t) != null), "each has a Bestiary entry and a new-enemy note");

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
        host.Leave("");

        // --- The approach: the Devourer's faction in the waves before it -----------------------------------------------------
        var fac = Approaches.For(EnemyType.Devourer);
        Check(fac != null && fac.Members.Select(m => m.Type).SequenceEqual(new[] { EnemyType.Gnawling, EnemyType.Butcher, EnemyType.Gulper })
            && fac.Squads.Any(s => s.Name == "Feeding Frenzy"), "the Devourer has a faction: Gnawling, Butcher, Gulper, with the Feeding Frenzy squad");
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
        {
            Game.SeedForTests(9300 + n);
            var cg = new Session().Game;
            cg.Wave = n;
            build.Invoke(cg, new object[] { n });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(larder.Contains))
                seen.Add(t);
        }
        Console.WriteLine("  seen before wave " + devWave + ": " + string.Join(", ", seen));
        Check(seen.SetEquals(larder), "the waves before the Devourer bring Gnawlings, Butchers and Gulpers");
    }
}
