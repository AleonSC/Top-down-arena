using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Soldier's Callings (SoldierCallings.cs, Docs/Callings.md): the cards (9 Callings, 3 a role, 2 cards and a capstone
// each), each role offered only its own three, the level-5 guarantee; then in a game, on both screens: a Replace Calling
// swaps the weapon (Siege Cannon: its shells hurt and knock back), a second-slot Calling swaps with the key (Nano Sprayer:
// sent by the client, seen everywhere, heals allies in the mist), and the shoulder mounts work on their own (Flak Mount
// shoots down a bullet, Lifeline Tether heals the hurt ally, Shoulder Minigun hurts an enemy), and a Railspike charge.
static partial class Program
{
    static void SoldierCallingsTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var sc = Upgrades.All.Where(u => Game.SoldierCalling(u.Calling)).ToList();
        Check(sc.Count(u => u.CallingTier == 1) == 9 && sc.Count(u => u.CallingTier == 2) == 18 && sc.Count(u => u.CallingTier == 3) == 9,
            "9 Soldier Callings, each with 2 cards and a capstone");
        foreach (Role r in new[] { Role.Tank, Role.Healer, Role.Dps })
            Check(sc.Count(u => u.CallingTier == 1 && u.Role == r && u.Kit == Kit.Soldier) == 3
                && sc.Where(u => u.CallingTier == 1 && u.Role == r).Select(u => Game.WayOf(u.Calling)).Distinct().Count() == 3,
                r + ": 3 Callings, one Replace, one second slot, one auto-fire");
        Check(Defs.Weapons.Where(w => w.Calling != CallingId.None).All(w => w.Kit == Kit.Soldier) && Defs.WeaponOf(WeaponId.SiegeCannon).Calling == CallingId.SiegeCannon,
            "the Calling weapons are registered, with no lobby place");
        for (int k = 0; k < Defs.KitCount; k++)
            foreach (Role r in new[] { Role.Tank, Role.Healer, Role.Dps })
                for (int i = 0; i < Defs.WeaponLimit(r, (Kit)k); i++)
                    if (Defs.Weapon(r, (Kit)k, i).Calling != CallingId.None)
                        Check(false, "a Calling weapon in a lobby place: " + Defs.Weapon(r, (Kit)k, i).Name);

        var none = new List<int>();
        Func<Role, Kit, WeaponId, List<int>, OfferCtx> ctx = (r, k, w, taken) => new OfferCtx(r, Upgrades.FirstAbility(r, k, 0), taken, w);
        var tank = ctx(Role.Tank, Kit.Soldier, WeaponId.Scattergun, none);
        Check(Upgrades.Available(U("Call the Siege Cannon"), tank) && Upgrades.Available(U("Call the Flak Mount"), tank)
            && !Upgrades.Available(U("Call the Railspike"), tank) && !Upgrades.Available(U("Call the Defib Cannon"), tank) && !Upgrades.Available(U("Call the Spider"), tank),
            "a Tank Soldier is offered the Tank Callings only");
        Check(!Upgrades.Available(U("Call the Siege Cannon"), ctx(Role.Tank, Kit.Summoner, WeaponId.GolemStaff, none)), "a Summoner isn't offered the Soldier's");
        var siege = new List<int> { U("Call the Siege Cannon") };
        Check(!Upgrades.Available(U("Call the Flak Mount"), ctx(Role.Tank, Kit.Soldier, WeaponId.SiegeCannon, siege))
            && Upgrades.Available(U("Bigger Bore"), ctx(Role.Tank, Kit.Soldier, WeaponId.SiegeCannon, siege))
            && !Upgrades.Available(U("Bombardment"), ctx(Role.Tank, Kit.Soldier, WeaponId.SiegeCannon, siege)),
            "after Call the Siege Cannon: no other Calling, its cards, not yet the capstone");
        int at5 = 0;
        for (int seed = 0; seed < 200; seed++)
            if (Upgrades.RollOffer(new Random(seed), ctx(Role.Dps, Kit.Soldier, WeaponId.TwinRifle, none), 5).Any(id => Upgrades.All[id].CallingTier == 1 && Upgrades.All[id].Role == Role.Dps))
                at5++;
        Check(at5 == 200, "every level-5 offer shows a DPS Soldier one of their Callings (" + at5 + "/200)");

        // In a game: you a Tank Soldier, the client a Healer Soldier.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Gunner", Role.Tank, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Medic", Role.Healer, 0, 0, Kit.Soldier);
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(new[] { hostS, client }, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        V2 mePos = new V2(500f, 450f), allyPos = new V2(600f, 450f);
        var mine = new HashSet<int>();
        var pinned = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); pinned[e] = at; return e;
        };
        bool fire = false, keepBullets = false;
        V2 aim = mePos + new V2(0f, -200f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in pinned) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                if (!keepBullets)
                    foreach (var id in g.Bullets.Values.Where(b => b.Owner == 0).Select(b => b.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = allyPos; client.Game.Players[2].Pos = allyPos; me.InvulnT = 1f; ally.InvulnT = 1f;
                hostS.Poll(); client.Poll();
                client.Game.FixedStep(dt, new PlayerInput { AimAt = allyPos + new V2(-200f, 0f) }); client.Game.Update(dt);
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire }); g.Update(dt);
            }
        };

        // Replace: the Siege Cannon.
        var lobby = me.Weapon.Id;
        g.GrantUpgrade(me.Id, U("Call the Siege Cannon"));
        step(0.3f);
        Check(me.Weapon.Id == WeaponId.SiegeCannon && client.Game.Players[1].Weapon.Id == WeaponId.SiegeCannon && me.LobbyWeapon.Id == lobby,
            "Call the Siege Cannon: it replaces your weapon, on both screens");
        Check(me.Stats.SpeedMult < 0.95f, "carrying it slows you");
        var prey = make(mePos + new V2(0f, -180f));
        fire = true;
        step(2f);
        fire = false;
        Console.WriteLine("  siege: prey hurt {0:0}", prey.MaxHp - prey.Hp);
        Check(prey.Hp < prey.MaxHp - 40f, "Siege Cannon shells hurt what they hit");

        // Auto-fire: the Flak Mount shoots down a bullet near you (the dev tool's way: the Calling swapped).
        me.Taken.RemoveAll(id => Upgrades.All[id].Calling != CallingId.None);
        g.GrantUpgrade(me.Id, U("Call the Flak Mount"));
        step(0.2f);
        Check(me.Weapon.Id == lobby, "a mount Calling leaves your lobby weapon in your hands");
        keepBullets = true;
        var shot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(120f, 0f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f });
        step(1f);
        keepBullets = false;
        Check(!g.Bullets.ContainsKey(shot.Id), "the Flak Mount shoots down an enemy bullet near you");

        // Auto-fire: the Lifeline Tether heals the hurt ally.
        me.Taken.RemoveAll(id => Upgrades.All[id].Calling != CallingId.None);
        g.GrantUpgrade(me.Id, U("Call the Lifeline Tether"));
        ally.Hp = ally.MaxHp * 0.4f;
        step(0.2f);
        float before = ally.Hp;
        step(1.5f);
        Console.WriteLine("  tether: ally {0:0} -> {1:0}, on {2} (client sees {3})", before, ally.Hp, me.TetherTo, client.Game.Players[1].TetherTo);
        Check(ally.Hp > before + 5f && me.TetherTo == ally.Id && client.Game.Players[1].TetherTo == ally.Id, "the Lifeline Tether heals the hurt ally (its beam seen on the client)");

        // Auto-fire: the Shoulder Minigun.
        me.Taken.RemoveAll(id => Upgrades.All[id].Calling != CallingId.None);
        g.GrantUpgrade(me.Id, U("Call the Shoulder Minigun"));
        float hp0 = prey.Hp;
        step(2f);
        Console.WriteLine("  minigun: spin {0}, prey hurt {1:0}", me.MountSpin, hp0 - prey.Hp);
        Check(me.MountSpin == 255 && prey.Hp < hp0 - 20f, "the Shoulder Minigun spins up and hurts the nearest enemy");

        // Replace with a charge: the Railspike.
        me.Taken.RemoveAll(id => Upgrades.All[id].Calling != CallingId.None);
        g.GrantUpgrade(me.Id, U("Call the Railspike"));
        step(0.2f);
        hp0 = prey.Hp;
        me.BowRelease = 3f;  // a full charge let go
        step(0.5f);
        Console.WriteLine("  railspike: prey hurt {0:0}", hp0 - prey.Hp);
        Check(me.Weapon.Id == WeaponId.Railspike && prey.Hp < hp0 - Game.SpikeMax * 0.8f, "a full Railspike charge hits hard");

        // Second slot: the client's Nano Sprayer, swapped with the key from the client's game.
        g.GrantUpgrade(ally.Id, U("Call the Nano Sprayer"));
        step(0.3f);
        var cAlly = client.Game.Players[2];
        Check(ally.Weapon.Id != WeaponId.NanoSprayer && cAlly.Weapon.Id != WeaponId.NanoSprayer, "a second-slot Calling keeps your lobby weapon out at first");
        client.Game.FixedStep(dt, new PlayerInput { AimAt = allyPos + new V2(-200f, 0f), SwapWeapon = true });
        step(0.3f);
        Check(ally.SecondOut && ally.Weapon.Id == WeaponId.NanoSprayer && cAlly.Weapon.Id == WeaponId.NanoSprayer && client.Game.Players[2].Weapon.Id == WeaponId.NanoSprayer,
            "Swap weapon (sent by the client): the Nano Sprayer is out, on both screens");
        client.Game.FixedStep(dt, new PlayerInput { AimAt = allyPos + new V2(-200f, 0f), SwapWeapon = true });
        step(0.3f);
        Check(!ally.SecondOut && ally.Weapon.Id == ally.LobbyWeapon.Id, "Swap weapon again: back to the lobby weapon");
        hostS.Leave(""); client.Leave("");
    }
}
