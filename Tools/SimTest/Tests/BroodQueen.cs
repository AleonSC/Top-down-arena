using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Brood Queen (BroodQueen.cs): the DPS Summoner's key 3 on the lobby's Ultimate row (the pick reaching the run), the drop at the
// cursor (kept within range, the client seeing her), the landing (40 in 70 px and a web, nothing before it, nothing outside), her
// hunt and venom bites, her taunt, the broodlings (spread onto other enemies, latched and biting, never more than 8), the web spit
// slowing a pack, the climb away bursting the brood on time, going down while she drops, mastered, Hatchery.
static partial class Program
{
    static void BroodQueenTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BroodQueen);
        Check(def.Name == "Brood Queen" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Kit == Kit.Summoner && def.Role == Role.Dps && def.Capstone != null,
            "Brood Queen is the DPS Summoner's ultimate on key 3, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Summoner) == AbilityId.Legion && Upgrades.Ultimate(Role.Dps, Kit.Summoner, 2) == AbilityId.MammothFall
            && Upgrades.Ultimate(Role.Dps, Kit.Summoner, 3) == AbilityId.BroodQueen,
            "Legion stays the default, key 2 Mammoth Fall, key 3 Brood Queen");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Hatchery");
        Check(card != null && card.RequiresAbility == AbilityId.BroodQueen && card.Rarity == Rarity.Rare, "its Rare card is Hatchery");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Weaver", Role.Dps, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].Ult == 3 && client.Lobby[1].Ult == 3 && host.Lobby[1].UltId == AbilityId.BroodQueen, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.BroodQueen && client.Game.Players[1].Slots[U].Id == AbilityId.BroodQueen, "in the match the host's F is Brood Queen, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (where, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, where, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = where; return e;
        };
        V2 mePos = new V2(300f, 450f), aim = new V2(600f, 450f);
        Func<Minion, bool> ours = m => m.Kind == MinionKind.BroodQueen || m.Kind == MinionKind.Broodling;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.KnockLeft = V2.Zero; kv.Key.FireT = 99f; }
                foreach (var id in g.Minions.Values.Where(m => !ours(m)).Select(m => m.Id).ToList()) g.Minions.Remove(id);  // (the wolves would bite the pinned enemies)
                me.Pos = mePos;
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Func<Minion> queen = () => g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.BroodQueen);
        Func<List<Minion>> lings = () => g.Minions.Values.Where(m => m.Kind == MinionKind.Broodling).ToList();
        Func<List<Zone>> webs = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.BroodWeb).ToList();
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear(); g.Minions.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.UltT = 0f; me.Downed = false; me.Pos = mePos; me.Aim = 0f;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(600f, 450f);
            g.Players[2].Pos = new V2(300f, 900f);
            g.State = MatchState.Fighting;
        };
        Func<Minion, float> mult = q => me.Stats.DmgMult * q.Power;

        // --- The drop: at the cursor, within range ---------------------------------------------------------------------------
        reset();
        aim = new V2(1200f, 450f);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var qn = queen();
        float total = Game.BqTotal(Game.BqTime);
        Check(qn != null && Math.Abs(qn.Duration - total) < 1e-3f && Math.Abs(me.UltT - total) < 1e-3f, "casting drops her: 0.8 + 10 + 0.9 s");
        Console.WriteLine("  brood queen: aiming 900 px away drops her {0:0} px out", qn != null ? (qn.Pos - mePos).Length : -1f);
        Check(qn != null && Math.Abs((qn.Pos - mePos).Length - Game.BqRange) < 2f, "she drops toward the cursor but within 360 px");
        step(0.1f);
        var cq = client.Game.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.BroodQueen);
        Check(cq != null && cq.Pos.CloserThan(qn.Pos, 2f), "the client sees her");

        // --- The landing -------------------------------------------------------------------------------------------------------
        reset();
        V2 at = aim;
        var under = pin(at + new V2(30f, 0f), EnemyType.Grunt);
        var outside = pin(at + new V2(0f, 260f), EnemyType.Grunt);  // out of the landing and her hunt
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        qn = queen();
        step(Game.BqDrop - 0.1f);
        Check(under.Hp == under.MaxHp, "nothing is hurt while she drops");
        float before = under.Hp;
        step(0.1f + dt * 2);
        Console.WriteLine("  brood queen: the landing took {0:0} (x{1:0.00}); webs {2}", before - under.Hp, mult(qn), webs().Count);
        Check(Math.Abs(before - under.Hp - Game.BqLandDmg * mult(qn)) < 0.25f * Game.BqLandDmg * mult(qn), "the landing hits what's under her for 40");
        Check(webs().Any(w => w.Pos.CloserThan(at, 1f) && Math.Abs(w.Radius - Game.BqLandRadius) < 0.1f), "and leaves a 70 px web there");
        Check(outside.Hp == outside.MaxHp, "nothing outside the landing is touched");
        step(0.2f);
        Check(under.SlowT > 0f && under.SlowMult <= 1f - Game.BqWebSlow + 0.01f, "an enemy on the web is slowed 50%");

        // --- The hunt, the taunt and the brood ------------------------------------------------------------------------------
        reset();
        var bitten = pin(at + new V2(60f, 0f), EnemyType.Grunt);
        var others = new[] { pin(at + new V2(-200f, 150f), EnemyType.Grunt), pin(at + new V2(250f, -200f), EnemyType.Grunt), pin(at + new V2(-300f, -250f), EnemyType.Grunt) };
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        qn = queen();
        step(Game.BqDrop + 1.5f);
        float b0 = bitten.Hp;
        int maxOut = 0;
        bool latched = false, onOthers = false, clientLings = false;
        var firstGoals = new Dictionary<int, int>();
        for (int i = 0; i < 40; i++)
        {
            step(0.1f);
            var ls = lings();
            maxOut = Math.Max(maxOut, ls.Count);
            latched |= ls.Any(l => l.Stacks == 1);
            onOthers |= ls.Any(l => l.Stacks == 1 && others.Any(o => o.Id == l.GoalEnemy));
            foreach (var l in ls) if (l.GoalEnemy != 0 && !firstGoals.ContainsKey(l.Id) && firstGoals.Count < 3) firstGoals[l.Id] = l.GoalEnemy;
            clientLings |= client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Broodling);
        }
        Console.WriteLine("  brood queen: 4 s of hunting took {0:0} off her prey (bleed {1:0.0}/s); broodlings out at most {2}; others lost {3}",
            b0 - bitten.Hp, bitten.BleedDps, maxOut, string.Join(", ", others.Select(o => (o.MaxHp - o.Hp).ToString("0"))));
        Check(qn.GoalEnemy == bitten.Id && qn.Stacks == 2, "she goes for the nearest enemy and bites it");
        Check(b0 - bitten.Hp > Game.BqBite * mult(qn) * 5f && bitten.BleedDps > 0f, "her bites hurt it and add a venom bleed");
        Check(bitten.ImpTauntT > 0f && bitten.ImpTauntId == qn.Id, "an enemy near her is taunted onto her");
        Check(maxOut >= 4 && maxOut <= Game.BqAlive, "she sends out broodlings, never more than 8 (" + maxOut + ")");
        Check(latched && onOthers && firstGoals.Count == 3 && firstGoals.Values.All(id => id != bitten.Id), "the broodlings latch onto the other enemies first, not the one she's biting");
        Check(others.All(o => o.Hp < o.MaxHp), "every other enemy is bitten by broodlings");
        Check(clientLings, "the client sees the broodlings");

        // --- The web spit --------------------------------------------------------------------------------------------------
        reset();
        var pack = new List<Enemy>();
        for (int i = 0; i < 4; i++)
            pack.Add(pin(at + new V2(200f + 20f * i, -150f + 15f * (i % 2)), EnemyType.Grunt));
        var lone = pin(at + new V2(120f, 60f), EnemyType.Grunt);  // (she walks toward the pack to bite this one)
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        step(Game.BqDrop + 1.6f);
        var spat = webs().FirstOrDefault(w => Math.Abs(w.Radius - Game.BqWebRadius) < 0.1f);
        Check(spat != null && pack.Any(p => p.Pos.CloserThan(spat.Pos, 6f)), "she spits a web at the biggest pack");
        step(0.1f);
        Check(pack.All(p => p.SlowT > 0f), "everything in the pack is slowed");

        // --- The climb ---------------------------------------------------------------------------------------------------------
        step(Game.BqDrop + Game.BqTime - 2.5f + 0.05f);
        qn = queen();
        Check(qn != null && qn.Stacks == 3 && lings().Count == 0, "she climbs away and the brood bursts");
        step(Game.BqClimb + 0.1f);
        Check(queen() == null && me.UltT <= 0.01f, "she has gone on time");

        // --- Going down while she drops --------------------------------------------------------------------------------------
        reset();
        var victim = pin(at, EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        step(0.3f);
        me.Downed = true;
        step(dt * 2);
        me.Downed = false;
        step(Game.BqDrop);
        Check(queen() == null && victim.Hp == victim.MaxHp, "going down while she drops cancels it");

        // --- Mastered, and Hatchery ------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, aim });
        qn = queen();
        Check(qn != null && Math.Abs(qn.Duration - Game.BqTotal(Game.BqTimeMastered)) < 1e-3f, "mastered: she stays 13 s");
        reset();
        card.Apply(me.Stats);
        var tank = pin(at + new V2(40f, 0f), EnemyType.Grunt);
        var weak = new List<Enemy>();
        for (int i = 0; i < 3; i++)
        {
            var w = pin(at + new V2(-250f, -100f + 100f * i), EnemyType.Grunt);
            w.Hp = w.MaxHp = 10f;
            weak.Add(w);
        }
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        qn = queen();
        int peak = 0;
        bool hatched = false;
        for (int i = 0; i < 40; i++)
        {
            int n = lings().Count;
            step(0.1f);
            int k = lings().Count;
            peak = Math.Max(peak, k);
            hatched |= weak.Any(w => w.Hp <= 0f) && k > n;
        }
        Console.WriteLine("  brood queen, Hatchery: weak ones down {0}, broodlings at most {1}", weak.Count(w => w.Hp <= 0f || !g.Enemies.ContainsKey(w.Id)), peak);
        Check(hatched, "Hatchery: a broodling's kill hatches another");
        Check(peak <= Game.BqAlive + Game.BqHatchCap, "and never more than 12 out");
    }
}
