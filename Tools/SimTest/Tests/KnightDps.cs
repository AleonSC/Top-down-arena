using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The DPS Knight's 1st abilities (the Gauntlet, Bladed Dive) and the Drawbridge (now the Tank Knight's key-2 ultimate).
static partial class Program
{
    static void KnightDpsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);

        Check(Upgrades.FirstAbility(Role.Dps, Kit.Knight, 0) == AbilityId.BladedDive && Upgrades.Ability(AbilityId.BladedDive).Name == "Bladed Dive"
            && Upgrades.Ability(AbilityId.Gauntlet).Name == "The Gauntlet" && Upgrades.Ability(AbilityId.CaltropRun).Name == "Caltrop Run"
            && Upgrades.Ability(AbilityId.Drawbridge).Name == "Drawbridge" && Upgrades.Ultimate(Role.Tank, Kit.Knight, 2) == AbilityId.Drawbridge,
            "the DPS Knight's abilities keep their slots under their new names; the Drawbridge is the Tank Knight's key 2");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Dps", Role.Dps, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Dps2", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.FrozenT = 99f; e.Pos = at; keep.Add(e); return e;
        };
        Action<float, V2> Step = (secs, aimAt) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                Nap(1);
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aimAt }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) }); client.Game.Update(dt);
                Clear();
            }
        };
        Action Reset = () =>
        {
            foreach (var e in keep) e.Hp = 0f;
            keep.Clear();
            Clear();
            foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
            g.Zones.Clear();
            Step(0.1f, new V2(1000f, 450f));
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
        };
        Func<V2, V2, Bullet> Shot = (at, vel) => (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, at, vel, 0f, 5f, 10f, 0, false, 0f });
        Clear(); Step(0.2f, new V2(1000f, 450f));

        // --- The Gauntlet ---
        Reset();
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.Gauntlet;
        var foe = Mob(EnemyType.Sniper, new V2(680f, 450f), 300f);
        var pack = Mob(EnemyType.Grunt, new V2(540f, 458f), 300f);
        Step(0.05f, new V2(680f, 450f));
        cast.Invoke(g, new object[] { 1, 0, new V2(700f, 450f), false });  // (not consumed yet: the host spends the charge and starts the cooldown)
        float packHp = pack.Hp;
        Step(0.9f, new V2(680f, 450f));
        Console.WriteLine("  gauntlet: challenge {0:0.0} s, you at ({1:0},{2:0}), the pack took {3:0} and slid to y {4:0}", foe.ChallengeT, me.Pos.X, me.Pos.Y, packHp - pack.Hp, pack.Pos.Y);
        Check(foe.ChallengeT > 3f && foe.ChallengeBy == 1 && client.Game.Enemies[foe.Id].ChallengeT > 3f, "the gauntlet Challenges the first enemy along the aim (and everyone sees the crest)");
        Check(me.Pos.DistanceTo(foe.Pos) < Game.GauntletStopShort + 12f, "you charge to the Challenged enemy");
        Check(packHp - pack.Hp >= Game.GauntletBowlDamage * 0.9f && pack.Pos.Y > 470f, "the charge bowls the enemy in the way aside");
        Check(me.Slots[0].Timer > 5f && me.GauntletDown, "the cooldown runs, and the gauntlet lies where it hit");
        foe.Hp = 0f;
        Step(0.2f, new V2(680f, 450f));
        Check(me.Slots[0].Timer <= 0.01f && !me.GauntletDown, "killing the Challenged enemy brings the gauntlet back and resets the cooldown");
        Reset();
        me.Slots[0].Id = AbilityId.Gauntlet;
        cast.Invoke(g, new object[] { 1, 0, new V2(700f, 100f), false });
        Step(0.6f, new V2(700f, 100f));
        Check(me.Slots[0].Timer > 5f && !me.GauntletDown, "a miss costs the full cooldown");
        // The client's own game is carried by the host's Msg.Carry.
        Reset();
        var cp = client.Game.Players[2];
        var dp = g.Players[2];
        dp.Pos = cp.Pos = new V2(300f, 250f);
        dp.Slots[0].Id = AbilityId.Gauntlet;
        var foe2 = Mob(EnemyType.Sniper, new V2(560f, 250f), 300f);
        Step(0.1f, new V2(1000f, 450f));
        cast.Invoke(g, new object[] { 2, 0, new V2(700f, 250f), true });
        Step(1f, new V2(1000f, 450f));
        Console.WriteLine("  gauntlet (client): carried to ({0:0},{1:0})", cp.Pos.X, cp.Pos.Y);
        Check(cp.Pos.DistanceTo(foe2.Pos) < Game.GauntletStopShort + 20f, "a client's charge is carried out on its own game");

        // --- Bladed Dive ---
        Reset();
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.BladedDive;
        var near = Mob(EnemyType.Brute, new V2(470f, 470f), 1000f);
        var weak = Mob(EnemyType.Grunt, new V2(560f, 430f), 100f); weak.Hp = 15f;
        var off = Mob(EnemyType.Grunt, new V2(560f, 700f), 100f);   // well off the path
        me.Slots[0].Timer = 10f; me.Slots[0].Charges = 0;           // as if the press had spent it
        cast.Invoke(g, new object[] { 1, 0, new V2(640f, 450f), true });
        bool untouchable = me.InvulnT > 0f;
        Step(0.6f, new V2(640f, 450f));
        Console.WriteLine("  bladed dive: you at ({0:0},{1:0}); near took {2:0}, weak {3:0}, off {4:0}; charges {5}", me.Pos.X, me.Pos.Y, 1000f - near.Hp, weak.Hp, off.Hp, me.Slots[0].Charges);
        Check(untouchable && me.Pos.DistanceTo(new V2(640f, 450f)) < 12f, "Bladed Dive rushes you to the cursor, untouchable");
        Check(near.Hp < 1000f - Game.BladedDamage * 1.5f && weak.Hp <= 0f && off.Hp >= 100f, "its cuts hit the enemies along the way (more than once) and leave the ones off the path alone");
        Check(me.Slots[0].Charges > 0 || me.Slots[0].Timer < 0.05f, "a kill with it resets the cooldown");
        Reset();
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.BladedDive;
        var tough = Mob(EnemyType.Brute, new V2(500f, 450f), 1000f);
        me.Slots[0].Timer = 10f; me.Slots[0].Charges = 0;
        cast.Invoke(g, new object[] { 1, 0, new V2(640f, 450f), true });
        Step(0.6f, new V2(640f, 450f));
        Check(tough.Hp < 1000f && me.Slots[0].Charges == 0 && me.Slots[0].Timer > 9f, "without a kill the cooldown stands");

        // --- The Drawbridge ---
        Reset();
        me.Pos = new V2(300f, 450f);
        var crushed = Mob(EnemyType.Brute, new V2(560f, 452f), 1e6f);
        var edge = Mob(EnemyType.Grunt, new V2(640f, 520f), 1e6f);
        var clear = Mob(EnemyType.Grunt, new V2(700f, 250f), 1e6f);
        me.Slots[Upgrades.UltSlot].Id = AbilityId.Drawbridge;
        me.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(900f, 450f), true });
        Step(0.5f, new V2(900f, 450f));
        Check(1e6f - crushed.Hp < 1f && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Drawbridge), "the bridge is still falling (everyone sees it)");
        Step(0.8f, new V2(900f, 450f));
        Console.WriteLine("  drawbridge: crushed took {0:0}, edge took {1:0} and slid to y {2:0}, clear took {3:0}", 1e6f - crushed.Hp, 1e6f - edge.Hp, edge.Pos.Y, 1e6f - clear.Hp);
        Check(1e6f - crushed.Hp >= Game.BridgeCrush * 0.9f, "the slam crushes what's in the lane");
        Check(1e6f - edge.Hp >= Game.BridgeEdgeDamage * 0.9f && 1e6f - edge.Hp < Game.BridgeCrush && edge.Pos.Y > 530f, "an enemy at the edge is hurt less and thrown aside");
        Check(1e6f - clear.Hp < 1f, "an enemy well clear isn't touched");
        Check(crushed.Pos.Y > 450f + Game.BridgeHalfWidth || crushed.Pos.Y < 450f - Game.BridgeHalfWidth || crushed.Pos.X > 830f, "a survivor in the lane is put off the bridge");
        clear.Pos = new V2(600f, 460f);
        Step(0.1f, new V2(900f, 450f));
        Check(Math.Abs(clear.Pos.Y - 450f) >= Game.BridgeHalfWidth, "enemies can't step onto the bridge");
        var railShot = Shot(new V2(620f, 300f), new V2(0f, 320f));
        Step(0.7f, new V2(900f, 450f));
        Check(!g.Bullets.ContainsKey(railShot.Id) && railShot.Pos.Y < 450f - Game.BridgeHalfWidth + 12f, "enemy shots stop at the rail");
        clear.Pos = new V2(600f, 455f);  // shoved onto the rail again
        Step(0.6f, new V2(900f, 450f));
        Check(1e6f - clear.Hp >= Game.BridgeRailDps * Game.BridgeRailTick * 0.9f, "the rails grind an enemy pressed against them");
        Step(6f, new V2(900f, 450f));
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.Drawbridge), "the bridge gives way after its time");
        host.Leave(""); client.Leave("");
    }
}
