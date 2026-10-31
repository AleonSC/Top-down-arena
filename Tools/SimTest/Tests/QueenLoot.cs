using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Hive Queen's legendaries: Hive Drones, Royal Dive and Bomber Escort (QueenLoot.cs).
static partial class Program
{
    static void QueenLootTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);

        // Her pool: the three new cards; the old three stay in the game but are never offered.
        var own = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].Boss == EnemyType.HiveQueen).Select(i => Upgrades.All[i].Name).ToArray();
        bool neverOffered = true;
        var rng = new Random(5);
        for (int k = 0; k < 300; k++)
        {
            var offer = Upgrades.LegendaryOffer(rng, (EnemyType)rng.Next(0, 60), Role.Dps, AbilityId.Dash, new List<int>());
            neverOffered &= !offer.Any(i => Upgrades.All[i].Name == "Swarm Rounds" || Upgrades.All[i].Name == "Royal Jelly" || Upgrades.All[i].Name == "Brood Mother");
        }
        Console.WriteLine("  the Queen's legendaries: {0}; the old ones never offered in 300 boss offers: {1}", string.Join(", ", own), neverOffered);
        Check(own.Length == 3 && own.Contains("Hive Drones") && own.Contains("Royal Dive") && own.Contains("Bomber Escort")
            && Upgrades.All[U("Royal Dive")].Grants == AbilityId.RoyalDive && U("Swarm Rounds") >= 0 && neverOffered,
            "the Hive Queen offers Hive Drones, Royal Dive and Bomber Escort; Swarm Rounds, Royal Jelly and Brood Mother are kept but never offered");

        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        g.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[2];                // the joining player, as the host sees them
        var cp = s[1].Game.Players[2];        // and as their own game does
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Action clear = () => { ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g); foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; keep.Add(e); pins[e] = at; return e; };
        V2 home = new V2(500f, 450f);
        Action<float, PlayerInput> drive = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                clear();
                foreach (var e in keep) { e.FireT = 9f; e.Speed = 0f; if (pins.ContainsKey(e)) e.Pos = pins[e]; }
                g.State = MatchState.Fighting;
                s[0].Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 100f) }); g.Update(dt);
                s[1].Poll(); s[1].Game.FixedStep(dt, f == 0 ? input : new PlayerInput { AimAt = input.AimAt }); s[1].Game.Update(dt);
                if (f % 2 == 0) Thread.Sleep(1);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); drive(0.1f, new PlayerInput()); };
        cp.Pos = cp.Disp = hp.Pos = home;
        drive(0.3f, new PlayerInput());

        // Hive Drones: two circle you; a kill sends one to sting the nearest enemy, and it comes home to heal you.
        g.GrantUpgrade(2, U("Hive Drones"));
        drive(1.5f, new PlayerInput { AimAt = home });
        Func<Game, List<Minion>> drones = gm => gm.Minions.Values.Where(m => m.Owner == 2 && m.Kind == MinionKind.HiveDrone).ToList();
        int hostDrones = drones(g).Count, clientDrones = drones(s[1].Game).Count;
        float ring = drones(g).Count > 0 ? drones(g).Max(m => m.Pos.DistanceTo(hp.Pos)) : 0f;
        Console.WriteLine("  hive drones: {0} round the friend (their game sees {1}), at most {2:0} px out", hostDrones, clientDrones, ring);
        Check(hostDrones == 2 && clientDrones == 2 && ring < 60f, "Hive Drones: two of the Queen's drones circle you (seen on your own game too)");
        var victim = dummy(home + new V2(150f, 0f));
        var next = dummy(home + new V2(230f, 60f));
        g.DevSetGod(2, false);  // (god mode keeps you topped up; nothing here hurts the friend)
        hp.Hp = hp.MaxHp * 0.5f;
        float hp0 = hp.Hp;
        hurt.Invoke(g, new object[] { victim, 1e7f, 2 });  // the friend kills it
        keep.Remove(victim);
        bool went = false;
        for (int f = 0; f < 60 * 3; f++)
        {
            drive(1f / 60f, new PlayerInput { AimAt = home });
            went |= drones(g).Any(m => m.Sent && !m.Returning);
        }
        float stung = next.MaxHp - next.Hp, healed = hp.Hp - hp0;
        float regen = (Game.PassiveRegen + hp.Stats.Regen) * 3f;
        g.DevSetGod(2, true);
        bool allHome = drones(g).All(m => !m.Sent);
        Console.WriteLine("  hive drones: a kill sent one out {0}; it stung the next enemy for {1:0.0} (3 x {2}); all home {3}; the friend healed {4:0.0} ({5:0.0} of it regen)",
            went, stung, Game.DroneSting * hp.Stats.DmgMult, allHome, healed, regen);
        Check(went && Math.Abs(stung - 3f * Game.DroneSting * hp.Stats.DmgMult) < 0.5f && allHome && Math.Abs(healed - regen - Game.HiveDroneHeal) < 1f,
            "after a kill a drone flies to the nearest enemy, stings it three times and comes home to heal you");
        reset();

        // Royal Dive: on a free key; you're carried to the cursor, untouchable; the crash hurts and knocks back,
        // and leaves a honey pool that slows enemies and heals allies.
        g.GrantUpgrade(2, U("Royal Dive"));
        drive(0.3f, new PlayerInput { AimAt = home });
        int slot = Array.FindIndex(hp.Slots, k => k.Id == AbilityId.RoyalDive);
        Check(slot >= 1 && slot <= 3 && cp.Slots[slot].Id == AbilityId.RoyalDive, "Royal Dive goes on a Q / E / Shift key");
        foreach (var m in drones(g)) g.Minions.Remove(m.Id);  // keep the numbers clean: no drones meanwhile
        hp.Taken.Remove(U("Hive Drones"));
        g.GrantUpgrade(2, U("Vitality"));
        cp.Pos = cp.Disp = hp.Pos = home;
        V2 land = home + new V2(300f, 0f);
        var hit = dummy(land + new V2(40f, 0f));
        drive(0.05f, new PlayerInput { AimAt = land });
        s[1].Poll(); s[1].Game.FixedStep(dt, new PlayerInput { AbilityQ = slot == 1, AbilityE = slot == 2, AbilityShift = slot == 3, AimAt = land });
        drive(0.15f, new PlayerInput { AimAt = land });
        bool untouchable = hp.InvulnT > 0f;
        pins.Remove(hit);  // let the knockback move it
        drive(0.7f, new PlayerInput { AimAt = land });
        float crash = hit.MaxHp - hit.Hp, knocked = hit.Pos.DistanceTo(land);
        var pool = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.HoneyPool);
        bool clientPool = s[1].Game.Zones.Values.Any(z => z.Kind == ZoneKind.HoneyPool);
        Console.WriteLine("  royal dive: untouchable in the air {0}; landed {1:0} px from the cursor (host) / {2:0} (own game); crash {3:0.0}; the enemy now {4:0} px out; honey pool {5} (friend sees it {6})",
            untouchable, hp.Pos.DistanceTo(land), cp.Pos.DistanceTo(land), crash, knocked, pool != null ? pool.Radius.ToString("0") + " px" : "none", clientPool);
        Check(untouchable && cp.Pos.DistanceTo(land) < 8f && Math.Abs(crash - Game.RoyalDiveDamage * hp.Stats.DmgMult) < 1f && knocked > 110f && pool != null && clientPool,
            "Royal Dive carries you to the cursor untouchable, and the crash hurts, knocks back and leaves a honey pool");
        if (pool != null)
        {
            var slowed = dummy(pool.Pos + new V2(20f, 20f));
            hp.Hp = hp.MaxHp * 0.5f;
            float before = hp.Hp;
            drive(1f, new PlayerInput { AimAt = land });
            Console.WriteLine("  honey pool: enemy in it slowed to {0:0.00}; the friend standing in it healed {1:0.0} in a second", slowed.SlowMult, hp.Hp - before);
            Check(slowed.SlowT > 0f && slowed.SlowMult <= 0.61f && hp.Hp - before >= 5.5f, "the honey pool slows enemies in it and heals allies standing in it");
        }
        reset();

        // Bomber Escort: every 5 s a bomber flies from you to the toughest enemy within 400 px and bursts on it.
        g.GrantUpgrade(2, U("Bomber Escort"));
        cp.Pos = cp.Disp = hp.Pos = home;
        var weak = dummy(home + new V2(200f, -80f)); weak.Hp = weak.MaxHp = 5e5f;
        var tough = dummy(home + new V2(260f, 90f));
        hp.BomberCd = 0f;
        float tough0 = tough.Hp, weak0 = weak.Hp;
        int seen = 0, clientSeen = 0;
        for (int f = 0; f < 60 * 2; f++)
        {
            drive(1f / 60f, new PlayerInput { AimAt = home });
            seen = Math.Max(seen, g.Minions.Values.Count(m => m.Owner == 2 && m.Kind == MinionKind.HiveBomber));
            clientSeen = Math.Max(clientSeen, s[1].Game.Minions.Values.Count(m => m.Owner == 2 && m.Kind == MinionKind.HiveBomber));
        }
        float bombed = tough0 - tough.Hp, spared = weak0 - weak.Hp;
        Console.WriteLine("  bomber escort: {0} bomber out (friend sees {1}); the toughest enemy took {2:0.0}, the other {3:0.0}; next in {4:0.0} s",
            seen, clientSeen, bombed, spared, hp.BomberCd);
        Check(seen == 1 && clientSeen == 1 && Math.Abs(bombed - Kits.Get(MinionKind.HiveBomber).Damage * hp.Stats.DmgMult) < 1f && spared == 0f && hp.BomberCd > 2.5f,
            "Bomber Escort sends a bomber from you to the toughest enemy near you, bursting on it");

        foreach (var x in s) x.Leave("");
    }
}
