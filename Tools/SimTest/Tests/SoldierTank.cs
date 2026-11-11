using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Tank Soldier's Alt fires (TankAlts.cs): Double Barrel, Proximity Shell, Tear Gas.
static partial class Program
{
    /// Double Barrel: point-blank hits load two shells at most, Alt dumps both as one wide blast and kicks you back, putting the
    /// Scattergun away empties them. Proximity Shell: lands at the cursor, waits, bursts when an enemy steps on it, a cooldown.
    /// Tear Gas: lands after its flight, then suppresses enemies in it to the cap and slows them. The screens see all of it.
    static void TankAltTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.Scattergun, "the Tank Soldier's first weapon is the Scattergun");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), aimAt = new V2(470f, 450f), foePos = aimAt;
        bool hold = true;
        var heard = new HashSet<Sfx>();  // what the other screen played
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                if (hold) me.Pos = from;
                queue.Clear();
                foreach (var e in g.Enemies.Values) { e.Pos = foePos; e.FireT = 99f; }
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aimAt, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                foreach (var c in client.Game.Sounds) heard.Add(c.Sfx);
                client.Game.Sounds.Clear();
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // --- Double Barrel
        Check(me.ScatShells == Game.ScatShellMax, "the Scattergun starts with all its shells");
        foePos = from + new V2(0f, 400f);
        g.Bullets.Clear();
        hold = false;
        me.Pos = from;
        float x0 = me.Pos.X;
        Step(dt * 2f, false, true);
        int pellets = g.Bullets.Values.Count(b => b.Kind == Defs.TankShot && b.Owner == 1);
        Step(0.3f, false, false);
        Console.WriteLine("  double barrel: {0} pellets, kicked back {1:0} px", pellets, x0 - me.Pos.X);
        Check(pellets == Game.ScatDumpPellets, "one shell a press (" + pellets + " pellets)");
        Check(me.ScatShells == Game.ScatShellMax - 1, "one shell is spent");
        Check(x0 - me.Pos.X > Game.ScatKick * 0.8f, "the recoil kicks you back");
        Check(client.Game.Players[1].ScatShells == Game.ScatShellMax - 1, "the other screen sees it");
        Step(Game.ScatRegen + 0.2f, false, false);
        Check(me.ScatShells == Game.ScatShellMax, "a shell loads back over time");
        me.ScatShells = 0;
        Step(0.3f, false, true);
        Check(g.Bullets.Values.Count(b => b.Kind == Defs.TankShot && b.Owner == 1) == 0, "Alt does nothing with no shells loaded");
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 1);
        Step(0.1f, false, false);
        Check(me.ScatShells == Game.ScatShellMax, "putting the Scattergun away refills it");

        var dummy = Dummy(foePos);

        // --- Proximity Shell
        Check(me.Weapon.Id == WeaponId.FlakCannon, "the second weapon is the Flak Cannon");
        aimAt = new V2(700f, 450f);
        foePos = new V2(700f, 750f);  // the dummy waits well away
        g.Bullets.Clear();
        Step(dt * 2f, false, true);
        Step(1f, false, false);
        var mine = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.FlakMine && z.Owner == 1);
        Check(mine != null && mine.Pos.DistanceTo(aimAt) < 5f, "Alt lays a shell at the cursor");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.FlakMine), "the other screen sees it");
        Step(dt * 2f, false, true);
        Step(0.2f, false, false);
        Check(g.Zones.Values.Count(z => z.Kind == ZoneKind.FlakMine) == 1, "a second press inside the cooldown lays nothing");
        float hp0 = dummy.Hp;
        foePos = aimAt + new V2(0f, 20f);
        Step(0.3f, false, false);
        Console.WriteLine("  proximity shell: the burst took {0:0}", hp0 - dummy.Hp);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.FlakMine), "an enemy stepping on it sets it off");
        Check(hp0 - dummy.Hp >= Game.MineDamage * me.Stats.DmgMult - 1f, "the burst hurts for at least 45");

        // --- Tear Gas
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 2);
        Check(me.Weapon.Id == WeaponId.RiotRepeater, "the third weapon is the Riot Repeater");
        foePos = aimAt;
        dummy.Suppress = 0f;
        Step(dt * 2f, false, true);
        Step(0.2f, false, false);
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.TearGas) && dummy.Suppress == 0f, "the canister is still in the air: nothing yet");
        Step(0.8f, false, false);
        Console.WriteLine("  tear gas: suppress {0:0.00}, slow x{1:0.00}", dummy.Suppress, dummy.SlowMult);
        Check(dummy.Suppress >= Game.SuppressMax - Game.SuppressFade * 0.05f, "the gas suppresses an enemy in it to the cap");
        Check(dummy.SlowT > 0f && dummy.SlowMult <= 1f - Game.TearGasSlow + 0.001f, "and slows it");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.TearGas), "the other screen sees the cloud");
        Step(Game.TearGasFlight + Game.TearGasLife, false, false);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.TearGas), "the cloud clears");
        foreach (var cue in new[] { Sfx.TankDouble, Sfx.TankShellLoad, Sfx.TankMineLaunch, Sfx.TankMineArm, Sfx.TankMineBurst, Sfx.TankGasLaunch, Sfx.TankGasHiss })
            Check(heard.Contains(cue), "the other screen hears " + cue);
        Check(!heard.Contains(Sfx.ShotCannon), "Double Barrel has its own blast, not the Flak Cannon's");
        host.Leave(""); client.Leave("");
    }
}
