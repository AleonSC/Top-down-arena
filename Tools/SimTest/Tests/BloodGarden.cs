using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Blood Garden (BloodGarden.cs): key 3 of the Healer Hemomancer's Ultimate row reaching the run; the cast paying 30% of
// current health and flinging 12 seeds 60-260 px out (16 mastered); the saplings rooting after their flight; an enemy in an
// aura slowed and siphoned (not one outside); the bulb filling, bursting at 60 into a blood orb anyone can drink that heals
// 25; an unfilled sapling withering after 12 s; the client seeing the saplings and their bulbs; the bot's rule.
static partial class Program
{
    static void BloodGardenTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 3 of the Healer Hemomancer's Ultimate row -----------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BloodGarden);
        Check(def.Name == "Blood Garden" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Healer && def.Kit == Kit.Hemomancer
            && def.HealthCost == 0.3f && def.CostsCurrent && def.Capstone != null,
            "Blood Garden is the Healer Hemomancer's ultimate on key 3, 80 s, 30% of current health");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 3) == AbilityId.BloodGarden && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 2) == AbilityId.HeartOfTheSwarm
            && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer) == AbilityId.SanguineRain && Upgrades.ClampUlt(Role.Healer, Kit.Hemomancer, 3) == 3,
            "key 3 is Blood Garden; Heart of the Swarm stays on key 2 and Sanguine Rain the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Garden", Role.Healer, 0, 0, Kit.Hemomancer, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Hemomancer, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.BloodGarden, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BloodGarden && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.BloodGarden,
            "in the run both players' ultimate is Blood Garden, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        V2 home = new V2(900f, 700f);
        var keep = new HashSet<Enemy>();
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.ToList())
                    if (!keep.Contains(g.Enemies[id]))
                        g.Enemies.Remove(id);
                foreach (var e in keep)
                    if (g.Enemies.ContainsKey(e.Id))
                    {
                        e.FireT = 99f;
                        e.FrozenT = 99f;
                    }
                g.Bullets.Clear();
                me.Pos = home;
                me.Hp = me.MaxHp;  // (full, so the orbs wait for the teammate)
                ally.Pos = home + new V2(0f, 2000f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(200f, 0f) });
            }
        };
        Action reset = () =>
        {
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.InvulnT = 0f; me.BgBursts = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.BloodGarden);
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out)
            g.State = MatchState.Fighting;
            foreach (var e in keep) g.Enemies.Remove(e.Id);
            keep.Clear();
            g.Zones.Clear(); g.Orbs.Clear();
        };
        Func<V2, float, Enemy> foe = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.Pos = e.Disp = at; e.FireT = 99f;
            keep.Add(e);
            return e;
        };
        Func<List<Zone>> saplings = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.BloodSapling).ToList();
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(200f, 0f), false });

        // --- The cast: the price and the seeds ----------------------------------------------------------------------------
        reset();
        me.Hp = me.MaxHp * 0.8f;
        step(0.05f);
        float hp0 = me.Hp;
        cast();
        var sap = saplings();
        Console.WriteLine("  garden: paid {0:0.0} of {1:0.0}; {2} seeds, {3:0}-{4:0} px out", hp0 - me.Hp, hp0, sap.Count,
            sap.Min(z => z.Pos.DistanceTo(home)), sap.Max(z => z.Pos.DistanceTo(home)));
        Check(Math.Abs((hp0 - me.Hp) - hp0 * Game.BgCost) < 0.5f, "casting pays 30% of current health");
        Check(sap.Count == Game.BgSeeds && sap.All(z => z.Pos.DistanceTo(home) >= Game.BgMin - 1f && z.Pos.DistanceTo(home) <= Game.BgMax + 1f && !Game.BgRooted(z)),
            "12 seeds fly 60-260 px out, none rooted yet");
        step(Game.BgFlyMax + 0.3f);
        Check(saplings().All(Game.BgRooted), "every seed has landed and sprouted after its flight");

        // --- The siphon: an enemy in an aura is slowed and drained; one outside isn't ---------------------------------------
        reset();
        cast();
        step(Game.BgFlyMax + 0.3f);
        var z0 = saplings().OrderBy(z => z.Pos.DistanceTo(home)).First();
        var inside = foe(z0.Pos + new V2(20f, 0f), 1e6f);
        V2 farAt = home + new V2(0f, -900f);
        var outside = foe(farAt, 1e6f);
        step(1.05f);
        float drained = 1e6f - inside.Hp;
        Console.WriteLine("  garden: in an aura {0:0.0} HP in 1 s (bulb {1:0.00}), slowed to {2:0.00}; outside {3:0.0}", drained, z0.GuardFrac, inside.SlowMult, 1e6f - outside.Hp);
        Check(drained >= Game.BgSiphon * 0.9f && drained <= Game.BgSiphon * 3.5f, "an enemy in an aura is siphoned about 12 HP a second (more where auras overlap)");
        Check(inside.SlowT > 0f && Math.Abs(inside.SlowMult - (1f - Game.BgSlow)) < 0.01f, "and slowed 40%");
        Check(outside.Hp == 1e6f, "an enemy in no aura isn't siphoned");
        Check(z0.GuardFrac > 0f && Math.Abs(z0.GuardFrac - z0.Soaked / Game.BgBulb) < 1e-3f, "the siphoned health fills the bulb");

        // The client sees the saplings and their bulbs (Aim, GuardFrac).
        step(0.2f);
        Run(s, dt, 1, () => cg.Zones.ContainsKey(z0.Id) && cg.Zones[z0.Id].GuardFrac > 0f);
        Zone cz;
        Check(cg.Zones.Values.Count(z => z.Kind == ZoneKind.BloodSapling) == saplings().Count && cg.Zones.TryGetValue(z0.Id, out cz) && Math.Abs(cz.Aim - z0.Aim) < 1e-3f && cz.GuardFrac > 0f,
            "the client's game has the saplings, their landing and their bulbs");

        // --- The bloom: a full bulb bursts into an orb anyone can drink -----------------------------------------------------
        step(Game.BgBulb / Game.BgSiphon + 0.6f);
        var orb = g.Orbs.Values.FirstOrDefault(o => o.Blood && o.Owner == 0);
        Console.WriteLine("  garden: bursts {0}, orb {1}, sapling gone {2}", me.BgBursts, orb != null ? orb.Heal.ToString("0.0") : "none", !g.Zones.ContainsKey(z0.Id));
        Check(me.BgBursts >= 1 && !g.Zones.ContainsKey(z0.Id), "a bulb that drinks 60 HP bursts and its sapling is gone");
        Check(orb != null && Math.Abs(orb.Heal - Game.BgOrbHeal) < 0.01f, "it drops a blood orb anyone can drink, healing 25");
        ally.Hp = ally.MaxHp * 0.4f;
        float a0 = ally.Hp;
        if (orb != null)
            for (int f = 0; f < 120 && g.Orbs.ContainsKey(orb.Id); f++)
            {
                ally.Pos = orb.Pos;
                me.Pos = home;
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home });
            }
        Check(orb != null && !g.Orbs.ContainsKey(orb.Id) && ally.Hp - a0 >= Game.BgOrbHeal - 0.5f, "a teammate drinks it for 25");

        // --- Withering: an unfilled sapling is gone after 12 s ------------------------------------------------------------
        reset();
        cast();
        step(Game.BgFlyMax + Game.BgLife + 0.4f);
        Check(saplings().Count == 0 && me.BgBursts == 0 && g.Orbs.Count == 0, "with no enemies near, the saplings wither after 12 s and drop nothing");

        // Mastered: 16 seeds.
        reset();
        me.MasteryXp[AbilityId.BloodGarden] = Game.MasteryXpFor[Game.MasteryMax - 1];
        step(0.05f);
        cast();
        Check(saplings().Count == Game.BgSeedsMastered, "mastered: 16 seeds");

        // The bot's rule.
        me.Hp = me.MaxHp;
        Check(def.Bot(new BotSense { Me = me, NearEnemies = 5 }) && !def.Bot(new BotSense { Me = me, NearEnemies = 1 }), "a bot casts it in a crowd");
        me.Hp = me.MaxHp * 0.3f;
        Check(!def.Bot(new BotSense { Me = me, NearEnemies = 6 }), "but not when low on health");

        host.Leave(""); client.Leave("");
    }
}
