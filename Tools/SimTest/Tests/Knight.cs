using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Knight class.
static partial class Program
{
    // The Knight: melee weapons, bullet cutting, per-weapon extra projectiles, Barrage waves, mods, and every ability.
    static void KnightTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);

        // Tables: every role has three Knight weapons, abilities and an ultimate.
        Check(Defs.Weapon(Role.Tank, Kit.Knight, 0).Name == "Warhammer" && Defs.Weapon(Role.Healer, Kit.Knight, 2).Name == "Censer" && Defs.Weapon(Role.Dps, Kit.Knight, 2).Name == "Rapier"
            && Defs.Weapon(Role.Dps, Kit.Summoner, 2).Name == "Imp Caller" && Defs.Weapon(Role.Healer, Kit.Soldier, 1).Name == "Revolver", "Knight weapons sit in the role/class tables without shifting the others");
        Check(Upgrades.RoleAbility(Role.Tank, Kit.Knight, 1) == AbilityId.LeapSlam && Upgrades.RoleAbility(Role.Healer, Kit.Knight, 2) == AbilityId.LayOnHands
            && Upgrades.RoleAbility(Role.Dps, Kit.Summoner, 2) == AbilityId.AlphaCall && Upgrades.Ultimate(Role.Healer, Kit.Knight) == AbilityId.HallowedBlade
            && Upgrades.Ultimate(Role.Dps, Kit.Mage) == AbilityId.MeteorStorm, "Knight abilities and ultimates map to the right role and class");
        int wall = U("Tower Shield");
        Check(Upgrades.Available(wall, Role.Tank, AbilityId.ShieldWall, new List<int>()) && !Upgrades.Available(wall, Role.Tank, AbilityId.Barrier, new List<int>()),
            "Knight ability upgrades are only offered with their ability");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Tank", Role.Tank, 0, 0, Kit.Knight);
        var client = new Session();
        client.Join("127.0.0.1", port, "Dps", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Kit == Kit.Knight && me.Weapon.Name == "Warhammer" && client.Game.Players[2].Weapon.Name == "Twin Blades" && me.MaxHp > Defs.Get(Role.Tank).Hp * 1.1f,
            "a Knight Tank and Knight DPS start with their weapons and armoured health");

        Enemy dummy = null;
        var keep = new HashSet<Enemy>();
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> Dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; keep.Add(e); return e; };
        Action<float, V2> Swing = (secs, aimAt) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = aimAt }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) }); client.Game.Update(dt);
                Clear();
                if (dummy != null) { dummy.FrozenT = 1f; dummy.Pos = new V2(700f, 450f); }
            }
        };
        Clear(); Run(s, dt, 0.2f);
        dummy = Dummy(new V2(700f, 450f));
        Func<int, float> Measure = (weapon) =>
        {
            me.Weapon = Defs.Weapon(me.Role, Kit.Knight, weapon);
            me.Pos = s[0].Game.Players[1].Pos = new V2(700f - Game.MeleeRange(me.Weapon.Id) * 0.7f - dummy.Radius, 450f);
            float hp = dummy.Hp;
            Swing(3f, dummy.Pos);
            return (hp - dummy.Hp) / 3f;
        };

        // Every Knight weapon hurts things in reach; a target out of reach is safe.
        var dps = new List<string>();
        bool allHit = true;
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
        {
            me.Role = role;
            for (int w = 0; w < 3; w++) { float d = Measure(w); allHit &= d > 10f; dps.Add(me.Weapon.Name + " " + (int)d); }
        }
        me.Role = Role.Tank;
        Console.WriteLine("  knight dps: " + string.Join(", ", dps));
        Check(allHit, "all nine Knight weapons hit enemies in reach");
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 0);
        me.Pos = new V2(700f - 260f, 450f);
        float far = dummy.Hp; Swing(2f, dummy.Pos);
        Check(dummy.Hp == far, "swings don't reach enemies far away");

        // Swings cut small bullets out of the air; red aimed shots get through.
        me.Pos = new V2(560f, 450f);
        var small = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(640f, 450f), new V2(-1f, 0f), 0f, 5f, 10f, 0, false, 0f });
        var red = (Bullet)shoot.Invoke(g, new object[] { Defs.PierceShot, new V2(640f, 470f), new V2(-1f, 0f), 0f, 5f, 10f, 0, false, 0f });
        Swing(1f, new V2(700f, 450f));
        Check(!g.Bullets.ContainsKey(small.Id) && g.Bullets.ContainsKey(red.Id), "swings cut small bullets out of the air, but not red aimed shots");
        g.Bullets.Remove(red.Id);

        // Extra projectiles: each weapon its own way.
        me.Role = Role.Dps;
        float rapier1 = Measure(2);
        var second = Dummy(new V2(720f, 450f));
        float before2 = second.Hp;
        g.GrantUpgrade(1, U("Multishot"));
        Swing(0.2f, dummy.Pos);
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 2);
        me.Pos = new V2(700f - 100f, 450f);
        for (int f = 0; f < 120; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(710f, 450f) }); dummy.FrozenT = second.FrozenT = 1f; dummy.Pos = new V2(700f, 450f); second.Pos = new V2(720f, 450f); }
        bool skewer = second.Hp < before2;
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 1);
        int waves0 = g.Bullets.Values.Count(b => b.Kind == Defs.KnightWave);
        for (int f = 0; f < 50; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(710f, 450f) }); }
        bool beams = g.Bullets.Values.Any(b => b.Kind == Defs.KnightWave && b.Owner == 1);
        second.Hp = 0f;
        Console.WriteLine("  Multishot: rapier skewers a 2nd enemy {0}; greatsword throws beams {1}", skewer, beams);
        Check(skewer && beams, "extra projectiles upgrade each Knight weapon its own way");
        me.Role = Role.Tank;

        // Barrage: a sword wave per stack. Incendiary Rounds: melee hits set enemies on fire.
        g.GrantUpgrade(1, U("Barrage"));
        g.GrantUpgrade(1, U("Incendiary Rounds"));
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 1);
        me.Pos = new V2(620f, 450f);
        foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
        bool waved = false;
        for (int f = 0; f < 90; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = dummy.Pos }); dummy.Pos = new V2(700f, 450f); waved |= g.Bullets.Values.Any(b => b.Kind == Defs.KnightWave); }
        Console.WriteLine("  barrage waves {0}, dummy burning {1} (fan {2}, burn {3})", waved, dummy.BurnT > 0f, me.Stats.Fan, me.Stats.BurnFrac);
        Check(waved && dummy.BurnT > 0f, "Barrage throws sword waves and weapon mods apply to melee hits");
        Check(Upgrades.DescFor(Upgrades.All[U("Piercing Rounds")], me.Weapon).Contains("reach") && Upgrades.DescFor(Upgrades.All[U("Multishot")], me.Weapon).Contains("knockback"),
            "upgrade cards describe what they do for a Knight weapon");

        // The Lance shoves what it hits far back.
        foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
        var shoved = Dummy(new V2(900f, 600f));
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 1);
        me.Pos = new V2(780f, 600f);
        V2 shovedFrom = shoved.Pos;
        for (int f = 0; f < 45; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(1000f, 600f) }); shoved.FrozenT = 1f; Clear(); me.Pos = new V2(780f, 600f); }
        Console.WriteLine("  lance: pushed a Brute {0:0} px", shoved.Pos.X - shovedFrom.X);
        Check(me.Weapon.Name == "Lance" && shoved.Pos.X - shovedFrom.X > 120f, "the Lance jabs enemies far back");
        shoved.Hp = 0f; keep.Remove(shoved);
        // Its hitbox is a box straight ahead: something off to the side at the same distance isn't hit.
        var ahead = Dummy(new V2(920f, 600f));
        var beside = Dummy(new V2(880f, 600f + Game.LanceHalfWidth + 60f));
        float aheadHp = ahead.Hp, besideHp = beside.Hp;
        for (int f = 0; f < 45; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(1000f, 600f) }); ahead.FrozenT = beside.FrozenT = 1f; ahead.Pos = new V2(920f, 600f); beside.Pos = new V2(880f, 600f + Game.LanceHalfWidth + 60f); Clear(); me.Pos = new V2(780f, 600f); }
        Console.WriteLine("  lance box: ahead took {0:0}, beside took {1:0}", aheadHp - ahead.Hp, besideHp - beside.Hp);
        Check(ahead.Hp < aheadHp && beside.Hp == besideHp, "the Lance hits a box straight ahead, not a cone");
        ahead.Hp = beside.Hp = 0f; keep.Remove(ahead); keep.Remove(beside);
        // And it stuns what it hits for a second (not bosses).
        var stunned = Dummy(new V2(900f, 600f));
        var lanceBoss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, new V2(900f, 600f), 0f, 0, false });
        lanceBoss.Hp = lanceBoss.MaxHp = 1e6f; lanceBoss.SpawnT = 0f; keep.Add(lanceBoss);
        float stunAt = 0f, bossFrozen = 0f;
        for (int f = 0; f < 45; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(1000f, 600f) }); Clear(); me.Pos = new V2(780f, 600f);
            stunAt = Math.Max(stunAt, stunned.FrozenT); bossFrozen = Math.Max(bossFrozen, lanceBoss.FrozenT);
        }
        Console.WriteLine("  lance stun: {0:0.00} s on a Brute, {1:0.00} s on the Warden", stunAt, bossFrozen);
        Check(stunAt > 0.9f && bossFrozen == 0f, "the Lance stuns what it hits for 1 s (not bosses)");
        stunned.Hp = lanceBoss.Hp = 0f; keep.Remove(stunned); keep.Remove(lanceBoss);

        // Abilities (cast directly; all players are invulnerable to everything but what's tested).
        Func<AbilityId, int> Slot = id => { var p = g.Players.Values.First(q => q.Slots.Any(sl => sl.Id == id)); return Array.FindIndex(p.Slots, sl => sl.Id == id); };
        Action<int, AbilityId, V2> Cast = (pid, id, at) =>
        {
            var p = g.Players[pid];
            p.Slots[0].Id = id;
            cast.Invoke(g, new object[] { pid, 0, at, true });
        };
        // Shield Wall: blocks from the front, not from behind.
        g.DevSetGod(1, false);
        me.Pos = s[0].Game.Players[1].Pos = new V2(400f, 450f);
        Cast(1, AbilityId.ShieldWall, me.Pos);
        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(600f, 450f) }); }
        var front = (Bullet)shoot.Invoke(g, new object[] { Defs.BigShot, new V2(470f, 450f), new V2(-300f, 0f), 0f, 5f, 10f, 0, false, 0f });
        for (int f = 0; f < 20; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(600f, 450f) }); Clear(); }
        float tookFront = me.Run.DamageTaken;
        me.InvulnT = 0f;
        var back = (Bullet)shoot.Invoke(g, new object[] { Defs.BigShot, new V2(330f, 450f), new V2(300f, 0f), 0f, 5f, 10f, 0, false, 0f });
        for (int f = 0; f < 30; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(600f, 450f) }); Clear(); }
        for (int f = 0; f < 10; f++) { Thread.Sleep(2); client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(600f, 450f) }); }
        Console.WriteLine("  shield wall: front shot gone {0}, damage from front {1:0}, from behind {2:0}", !g.Bullets.ContainsKey(front.Id), tookFront, me.Run.DamageTaken - tookFront);
        Check(!g.Bullets.ContainsKey(front.Id) && tookFront == 0f && me.Run.DamageTaken > tookFront, "Shield Wall stops bullets from the front but not from behind");
        Check(client.Game.Players[1].WallT > 0f, "the other player sees the Shield Wall go up");
        // Unbreakable halves damage.
        var damage = typeof(Game).GetMethod("Damage", flags);
        me.InvulnT = 0f; float t0 = me.Run.DamageTaken; damage.Invoke(g, new object[] { me, 40f, "test" }); float plain = me.Run.DamageTaken - t0;
        Cast(1, AbilityId.Unbreakable, me.Pos);
        me.InvulnT = 0f; t0 = me.Run.DamageTaken; damage.Invoke(g, new object[] { me, 40f, "test" }); float tough = me.Run.DamageTaken - t0;
        Check(Math.Abs(tough - plain * 0.5f) < 0.5f, "Unbreakable halves the damage you take");
        // Last Stand: can't be downed.
        me.Slots[Upgrades.UltSlot].Id = AbilityId.LastStand;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, true });
        me.InvulnT = 0f; damage.Invoke(g, new object[] { me, 5000f, "test" });
        Check(!me.Downed && me.Hp >= 1f, "Last Stand keeps you from going down");
        g.DevSetGod(1, true);
        me.UltT = 0f;

        // Leap Slam (from the client: its own game moves it, the host lands the slam there).
        var cp = client.Game.Players[2];
        var dp = g.Players[2];
        dp.Pos = cp.Pos = new V2(400f, 300f);
        var landing = Dummy(new V2(640f, 300f));
        float landHp = landing.Hp;
        cp.Slots[0].Id = dp.Slots[0].Id = AbilityId.LeapSlam;
        cp.Slots[0].Charges = 1;
        for (int f = 0; f < 60; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) });
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(640f, 300f), Ability = f < 2 }); client.Game.Update(dt);
            landing.FrozenT = 1f; landing.Pos = new V2(640f, 300f);
            Clear(); dummy.FrozenT = 1f;
        }
        Console.WriteLine("  leap slam: client moved to ({0:0},{1:0}), landing dummy took {2:0}", cp.Pos.X, cp.Pos.Y, landHp - landing.Hp);
        Check(cp.Pos.DistanceTo(new V2(640f, 300f)) < 30f && landing.Hp < landHp, "Leap Slam carries the player to the cursor and hits where they land");
        landing.Hp = 0f;

        // Lunge: two charges, hits along the way.
        cp.RoleAbility = dp.RoleAbility = AbilityId.Lunge;
        g.GrantUpgrade(2, U("Vitality"));  // recomputes slots and charges on both games
        Run(s, dt, 0.3f, null, () => Clear());
        Check(dp.Slots[0].MaxCharges == 2 && cp.Slots[0].MaxCharges == 2, "Lunge has two charges");

        // Healer: Consecrate heals, Lay on Hands revives, Guardian's Oath shields.
        dp.Hp = dp.MaxHp * 0.3f;
        dp.Pos = cp.Pos = me.Pos + new V2(40f, 0f);
        Cast(1, AbilityId.Consecrate, me.Pos);
        float hp0 = dp.Hp;
        Run(s, dt, 1f, null, () => { Clear(); dp.Pos = me.Pos + new V2(40f, 0f); });
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.Consecrate) && dp.Hp > hp0 + 5f, "Consecrate blesses the ground and heals allies on it");
        Cast(1, AbilityId.GuardiansOath, dp.Pos);
        Check(dp.ShieldHp >= 99f && me.ShieldHp >= 49f, "Guardian's Oath shields the ally near your cursor (and you)");
        dp.Downed = true; dp.Hp = 0f;
        Cast(1, AbilityId.LayOnHands, me.Pos);
        Check(!dp.Downed && dp.Hp > dp.MaxHp * 0.4f, "Lay on Hands revives a downed ally nearby");

        // DPS: Whirlwind spins, Battle Fury speeds swings; ultimates.
        me.Pos = new V2(640f, 450f);
        float w0 = dummy.Hp;
        Cast(1, AbilityId.Whirlwind, me.Pos);
        Run(s, dt, 1f, null, () => { Clear(); dummy.FrozenT = 1f; dummy.Pos = new V2(700f, 450f); });
        Check(dummy.Hp < w0 && client.Game.Players[1].WhirlT > 0f, "Whirlwind hits everything around you (and others see it spinning)");
        var fireRate = typeof(Game).GetMethod("FireRate", flags);
        float fr0 = (float)fireRate.Invoke(g, new object[] { me });
        Cast(1, AbilityId.BattleFury, me.Pos);
        Check((float)fireRate.Invoke(g, new object[] { me }) > fr0 * 1.35f, "Battle Fury makes you swing 40% faster");
        me.Slots[Upgrades.UltSlot].Id = AbilityId.HallowedBlade;
        var under = Dummy(new V2(900f, 300f)); float u0 = under.Hp;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(900f, 300f), true });
        Run(s, dt, 1f, null, () => { Clear(); under.FrozenT = 1f; under.Pos = new V2(900f, 300f); });
        Check(u0 - under.Hp > 150f && g.Zones.Values.Any(z => z.Kind == ZoneKind.Consecrate && z.Pos.DistanceTo(new V2(900f, 300f)) < 1f), "Hallowed Blade lands for big damage and blesses the ground");
        under.Hp = 0f;
        me.Slots[Upgrades.UltSlot].Id = AbilityId.BladeTempest;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, true });
        foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
        Swing(0.5f, dummy.Pos);
        Check(g.Bullets.Values.Count(b => b.Kind == Defs.KnightWave && b.Pierce > 50) >= 3, "Blade Tempest hurls piercing sword beams with each swing");
        host.Leave(""); client.Leave("");
    }
}
