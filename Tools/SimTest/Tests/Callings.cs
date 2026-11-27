using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Callings (Callings.cs, Docs/Callings.md): the cards (3 Callings, 2 cards and a capstone each), the pool rules (one
// Calling a run, its cards only once you hold it, the capstone after both cards, Summoner cards only), the level-5
// guarantee and the role weighting; then in a game, on both screens: the spider (comes, bites and webs, falls and comes
// back), the fairy (heals the hurt ally), the demon (fireballs; Hellfire's burning ground) and Spider Queen's brood.
static partial class Program
{
    static void CallingsTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var callings = Upgrades.All.Where(u => u.Calling != CallingId.None && !Game.SoldierCalling(u.Calling)).ToList();  // (the Soldier's: SoldierCallings.cs)
        Check(callings.Count(u => u.CallingTier == 1) == 3 && callings.Count(u => u.CallingTier == 2) == 6 && callings.Count(u => u.CallingTier == 3) == 3,
            "3 Summoner Callings, each with 2 cards and a capstone");
        Check(callings.Where(u => u.CallingTier == 1).All(u => u.Kit == Kit.Summoner && u.Rarity == Rarity.Rare && u.MaxStacks == 1)
            && callings.Where(u => u.CallingTier == 3).All(u => u.Rarity == Rarity.VeryRare), "the Callings are Rare Summoner cards; the capstones Very Rare");

        AbilityId first = Upgrades.FirstAbility(Role.Tank, Kit.Summoner, 0);
        Func<List<int>, OfferCtx> ctx = taken => new OfferCtx(Role.Tank, first, taken, WeaponId.GolemStaff);
        var none = new List<int>();
        Check(U("Call the Spider") >= 0 && Upgrades.Available(U("Call the Spider"), ctx(none)) && Upgrades.Available(U("Call the Demon"), ctx(none))
            && !Upgrades.Available(U("Silk Snare"), ctx(none)) && !Upgrades.Available(U("Spider Queen"), ctx(none)),
            "with no Calling: every Calling is offered, no Calling's cards");
        var spider = new List<int> { U("Call the Spider") };
        Check(!Upgrades.Available(U("Call the Demon"), ctx(spider)) && !Upgrades.Available(U("Call the Spider"), ctx(spider))
            && Upgrades.Available(U("Silk Snare"), ctx(spider)) && Upgrades.Available(U("Thick Carapace"), ctx(spider))
            && !Upgrades.Available(U("Hellfire"), ctx(spider)) && Upgrades.CallingOf(spider) == CallingId.Spider,
            "after Call the Spider: no other Calling, the Spider's cards and not the Demon's");
        Check(!Upgrades.Available(U("Spider Queen"), ctx(new List<int> { U("Call the Spider"), U("Silk Snare") }))
            && Upgrades.Available(U("Spider Queen"), ctx(new List<int> { U("Call the Spider"), U("Silk Snare"), U("Thick Carapace") })),
            "Spider Queen is offered only once both Spider cards are held");
        AbilityId soldier = Upgrades.FirstAbility(Role.Tank, Kit.Soldier, 0);
        Check(!Upgrades.Available(U("Call the Spider"), new OfferCtx(Role.Tank, soldier, none, WeaponId.Scattergun)), "other classes aren't offered the Summoner's Callings");

        // The guarantee and the weighting.
        int at5 = 0, at4 = 0, spiders = 0, demons = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            if (Upgrades.RollOffer(new Random(seed), ctx(none), 5).Any(id => Upgrades.All[id].CallingTier == 1)) at5++;
            var o4 = Upgrades.RollOffer(new Random(seed), ctx(none), 4);
            if (o4.Any(id => Upgrades.All[id].CallingTier == 1)) at4++;
            var g5 = Upgrades.RollOffer(new Random(seed + 1000), ctx(none), 5).Where(id => Upgrades.All[id].CallingTier == 1).ToList();
            spiders += g5.Count(id => Upgrades.All[id].Calling == CallingId.Spider);
            demons += g5.Count(id => Upgrades.All[id].Calling == CallingId.Demon);
        }
        Console.WriteLine("  offers with a Calling: level 5 {0}/300, level 4 {1}/300; at level 5 a Tank saw the Spider {2} times, the Demon {3}", at5, at4, spiders, demons);
        Check(at5 == 300 && at4 < 300, "every level-5 offer shows a Calling to a Summoner without one (not every level-4 offer does)");
        Check(spiders > demons * 1.4f, "the Calling that suits your role turns up about twice as often");
        Check(!Upgrades.RollOffer(new Random(1), ctx(spider), 5).Any(id => Upgrades.All[id].CallingTier == 1), "no guarantee once you have a Calling");

        // In a game.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);
        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Caller", Role.Tank, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Summoner);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        V2 mePos = new V2(500f, 450f), allyPos = new V2(620f, 450f);
        var mine = new HashSet<int>();
        var pinned = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); pinned[e] = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in pinned) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; kv.Key.KnockLeft = V2.Zero; }
                foreach (var id in g.Bullets.Values.Where(b => b.Owner == 0).Select(b => b.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = allyPos; me.InvulnT = 1f; ally.InvulnT = 1f;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(0f, -200f) });
            }
        };
        Func<Game, MinionKind, Minion> find = (gm, k) => gm.Minions.Values.FirstOrDefault(m => m.Kind == k && (m.Companion || gm != g));  // (Companion is the host's)

        g.GrantUpgrade(me.Id, U("Call the Spider"));
        step(0.5f);
        var sp = find(g, MinionKind.Spider);
        Check(sp != null && find(client.Game, MinionKind.Spider) != null && sp.Owner == me.Id && sp.MaxHp > 0f, "Call the Spider: the spider comes (seen on client)");
        var prey = make(mePos + new V2(0f, -150f));
        step(2f);
        Console.WriteLine("  spider: prey hurt {0:0}, slowed {1}", prey.MaxHp - prey.Hp, prey.SlowT > 0f);
        Check(prey.Hp < prey.MaxHp && prey.SlowT > 0f && prey.SlowMult < 0.7f, "the spider leaps on an enemy near you, bites and webs it");
        hurtGuard.Invoke(g, new object[] { sp, 1e6f });
        step(0.2f);
        Check(find(g, MinionKind.Spider) == null, "the spider can be killed");
        step(Game.SpiderRespawn + 0.5f);
        Check(find(g, MinionKind.Spider) != null, "a fallen spider comes back after " + Game.SpiderRespawn + " s");
        g.GrantUpgrade(me.Id, U("Silk Snare")); g.GrantUpgrade(me.Id, U("Thick Carapace")); g.GrantUpgrade(me.Id, U("Spider Queen"));
        int lings = 0;
        for (int i = 0; i < 20 && lings == 0; i++) { step(0.05f); lings = g.Minions.Values.Count(m => m.Kind == MinionKind.Spider && !m.Companion); }
        Console.WriteLine("  queen: state {0}, queen {1}, lings {2}", g.State, me.Stats.SpiderQueen, lings);
        Check(lings == Game.QueenBrood, "Spider Queen: the spider lays " + Game.QueenBrood + " spiderlings");

        // The ally takes the Fairy.
        g.GrantUpgrade(ally.Id, U("Call the Fairy"));
        ally.Hp = ally.MaxHp * 0.4f;
        step(0.3f);
        float before = ally.Hp;
        step(2f);
        Console.WriteLine("  fairy: ally health {0:0} -> {1:0}", before, ally.Hp);
        Check(find(g, MinionKind.Fairy) != null && find(client.Game, MinionKind.Fairy) != null && ally.Hp > before + 3f, "Call the Fairy: a fairy comes and heals the hurt ally");

        // A Demon on a third Summoner is the same code: here the host swaps its Calling with a dev tool's worth of cards.
        foreach (var m in g.Minions.Values.Where(m => m.Owner == me.Id).ToList()) g.Minions.Remove(m.Id);
        me.Taken.RemoveAll(id => Upgrades.All[id].Calling != CallingId.None);
        g.GrantUpgrade(me.Id, U("Call the Demon"));
        g.GrantUpgrade(me.Id, U("Hellfire"));
        step(0.5f);
        float hp0 = prey.Hp;
        bool burnt = false;
        for (int i = 0; i < 30 && !burnt; i++) { step(0.1f); burnt = g.Zones.Values.Any(z => z.Kind == ZoneKind.FlameWall && z.Owner == me.Id); }
        step(1f);
        Console.WriteLine("  demon: prey hurt {0:0}, burning ground {1}", hp0 - prey.Hp, burnt);
        Check(find(g, MinionKind.Demon) != null && find(client.Game, MinionKind.Demon) != null && find(g, MinionKind.Spider) == null, "Call the Demon: the demon comes (seen on client), the old Calling's minion goes");
        Check(prey.Hp < hp0, "the demon's fireballs hurt enemies");
        Check(burnt, "Hellfire: its fireballs leave the ground burning");
        hostS.Leave(""); client.Leave("");
    }
}
