using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Hive Queen's faction (Core/HiveKin.cs, Docs/HiveFaction.md): eight enemies that play like the Warden's, a honey puddle
// instead of gas, a venom dart that stuns, and the same ten-wave chart in waves 11-20 of the 100-wave Campaign.
static partial class Program
{
    static void HiveTest(float dt)
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var kin = new Dictionary<EnemyType, EnemyType>
        {
            { EnemyType.Worker, EnemyType.Rioter }, { EnemyType.Soldier, EnemyType.Guard }, { EnemyType.CombShield, EnemyType.RiotShield },
            { EnemyType.HoneyLobber, EnemyType.GasGrenadier }, { EnemyType.Stinger, EnemyType.TaserGuard }, { EnemyType.Needler, EnemyType.SecurityGuard },
            { EnemyType.Bumble, EnemyType.Thug }, { EnemyType.Hornet, EnemyType.Shiv },
        };
        Check(kin.All(k => HiveKin.Of(k.Key) == k.Value && HiveKin.Is(k.Key)) && !HiveKin.Is(EnemyType.Rioter) && HiveKin.Of(EnemyType.Grunt) == EnemyType.Grunt,
            "every Hive enemy plays like one Warden enemy (Worker Rioter, Soldier Guard, Comb Shield Riot Shield, Honey Lobber Gas Grenadier, Stinger Taser Guard, Needler Security Guard, Bumble Thug, Hornet Shiv)");
        Check(kin.All(k => { var a = Defs.Get(k.Key); var b = Defs.Get(k.Value); return a.Hp == b.Hp && a.Speed == b.Speed && a.Radius == b.Radius && a.Melee == b.Melee && a.Contact == b.Contact && a.Xp == b.Xp; }),
            "each has its counterpart's health, speed, size, contact damage and XP");
        Check(kin.Keys.All(t => Game.EnemyNote(t).Length > 0 && BestiaryData.For(t) != null), "each has a Bestiary entry and a new-enemy note");
        Check(Game.IsBrute(EnemyType.Bumble) && Game.IsLunger(EnemyType.Hornet) && !Game.IsBrute(EnemyType.Worker), "the Bumble counts as a Brute and the Hornet as a Lunger");

        // --- Playing them: a solo host, one enemy at a time -------------------------------------------------------------------
        Game.SeedForTests(4321);
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
            e.SpawnT = 0f; e.Spawning = false;
            return e;
        };
        Action<float, Action> run = (secs, each) => { for (int f = 0; f < secs * 60; f++) { Quiet(); Step(host, dt); each?.Invoke(); } };
        me.Pos = me.Disp = new V2(900f, 400f);
        g.DevSetGod(1, true);

        // The Worker comes at you.
        var w = put(EnemyType.Worker, new V2(300f, 400f));
        float d0 = w.Pos.DistanceTo(me.Pos);
        run(2f, null);
        Check(w.Pos.DistanceTo(me.Pos) < d0 - 100f, "a Worker closes in on you");

        // The Honey Lobber's puddle slows instead of hurting: honey, never gas.
        var lob = put(EnemyType.HoneyLobber, new V2(600f, 400f));
        bool honey = false, gas = false;
        run(9f, () => { foreach (var p in g.Patches.Values) { honey |= p.Kind == PatchKind.Honey; gas |= p.Kind == PatchKind.Gas; } });
        Check(honey && !gas, "a Honey Lobber lobs a honey puddle, not gas");

        // The Stinger's dart is its own bullet, and it stuns like the Fork Bolt.
        var st = put(EnemyType.Stinger, new V2(560f, 400f));
        bool dart = false;
        run(8f, () => { foreach (var b in g.Bullets.Values) dart |= b.Kind == Defs.StingBolt; });
        Check(dart && Defs.IsEnemyShot(Defs.StingBolt), "a Stinger fires a venom dart (Defs.StingBolt)");
        g.DevSetGod(1, false);
        put(EnemyType.Worker, new V2(100f, 100f)).Hp = 1e6f;  // (something alive, so the wave goes on)
        typeof(Game).GetMethod("SpawnBullet", np).Invoke(g, new object[] { Defs.StingBolt, me.Pos - new V2(10f, 0f), new V2(260f, 0f), 0f, 3f, 8f, 0, false, 0f });
        run(0.3f, null);
        Check(me.StunT > 0f || me.Hp < me.MaxHp, "the dart hits and stuns you");
        g.DevSetGod(1, true);

        // The Needler: a magazine of seven, tracer rounds.
        var nd = put(EnemyType.Needler, new V2(560f, 400f));
        Check(nd.Phase == Game.SecurityMag, "a Needler starts with a full magazine of seven");
        bool tracer = false;
        run(8f, () => { foreach (var b in g.Bullets.Values) tracer |= b.Kind == Defs.TracerRound; });
        Check(tracer && nd.Phase < Game.SecurityMag, "it fires tracer rounds and its magazine runs down");

        // The Comb Shield: the shield holds four times its health, like the Riot Shield's.
        var cs = put(EnemyType.CombShield, new V2(300f, 400f));
        Check(Math.Abs(cs.ShieldMax - cs.MaxHp * Game.RiotShieldShare) < 0.01f && Game.RiotCrack(cs) == 0, "a Comb Shield's plate holds 4x its health");

        // The Hornet winds up and lunges, as the Shiv does.
        var ho = put(EnemyType.Hornet, new V2(560f, 400f));
        bool wind = false, leap = false;
        run(6f, () => { wind |= ho.Phase == 1; leap |= ho.Phase == 2; });
        Check(wind && leap, "a Hornet winds up and lunges");
        host.Leave("");

        // --- The ten-wave chart in waves 11-20 of the Campaign (the same order as the Warden's) ----------------------------
        var build = typeof(Game).GetMethod("BuildWave", np);
        var spawnQ = typeof(Game).GetField("spawnQueue", np);
        var squadQ = typeof(Game).GetField("squadQueue", np);
        var hiveSet = new HashSet<EnemyType>(Approaches.For(EnemyType.HiveQueen).Members.Select(m => m.Type));
        var seen = new HashSet<EnemyType>();
        var first = new Dictionary<EnemyType, int>();
        var squadNames = new Dictionary<int, HashSet<string>>();
        for (int s = 11; s <= 20; s++)
        {
            Game.SeedForTests(8000 + s);
            var cg = new Session().Game;
            cg.LongRun = true; cg.Wave = (s + 1) / 2; cg.Leg = (s - 1) % 2; cg.BossSlot = false;
            build.Invoke(cg, new object[] { cg.Wave });
            var q = (List<EnemyType>)spawnQ.GetValue(cg);
            var sq = (List<SquadDef>)squadQ.GetValue(cg);
            foreach (var t in q.Concat(sq.SelectMany(x => x.Members)).Where(hiveSet.Contains))
                if (!first.ContainsKey(t)) first[t] = s;
            squadNames[s] = new HashSet<string>(sq.Select(x => x.Name));
        }
        Console.WriteLine("  first waves: " + string.Join(", ", first.OrderBy(k => k.Value).Select(k => k.Key + " " + k.Value)));
        Check(first.ContainsKey(EnemyType.Worker) && first[EnemyType.Worker] == 11 && first[EnemyType.Soldier] == 12 && first[EnemyType.Bumble] == 13 && first[EnemyType.Needler] == 14
            && first[EnemyType.CombShield] == 16 && first[EnemyType.HoneyLobber] == 17 && first[EnemyType.Stinger] == 18,
            "waves 11-20: Worker on 11, Soldier 12, Bumble 13, Needler 14, Comb Shield 16, Honey Lobber 17, Stinger 18 (the chart's order)");
        Check(squadNames[20].Contains("Royal guard") && squadNames[20].Contains("Swarm") && Enumerable.Range(11, 9).All(wv => !squadNames[wv].Contains("Royal guard")), "wave 20 is the exam: the Royal guard and a big Swarm, only there");
    }
}
