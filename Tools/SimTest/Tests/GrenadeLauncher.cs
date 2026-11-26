using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Tank Soldier's fourth weapon, the Grenade Launcher (GrenadeLauncher.cs).
static partial class Program
{
    /// HE grenades lobbed to the cursor burst there (full in the middle, less at the rim), six to a drum then a reload; Alt swaps to the
    /// smoke drum; a canister opens into a cloud that jams a regular enemy (no shots) and slows it, while a mini-boss is only slowed;
    /// three canisters, one back every 6 s; the other screen sees the drum and the cloud.
    static void GrenadeLauncherTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Grenadier", Role.Tank, 3, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.GrenadeLauncher && Defs.WeaponCount(Role.Tank, Kit.Soldier) == 4, "the Tank Soldier's fourth key is the Grenade Launcher (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        int enemyShots = 0;
        Enemy watched = null;  // (only this enemy's shots are counted, when set)
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                enemyShots += g.Bullets.Values.Count(b => b.Owner == 0 && b.Life0 - b.Life < dt * 1.5f && (watched == null || b.Pos.DistanceTo(watched.Pos) < watched.Radius + 30f));
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // HE: one grenade flies to the cursor and bursts there, hurting what's close more than what's at the rim
        var mid = Dummy(EnemyType.Grunt, from + new V2(250f, 0f));
        var rim = Dummy(EnemyType.Grunt, from + new V2(250f, 60f));
        var away = Dummy(EnemyType.Grunt, from + new V2(250f, 200f));
        Step(dt * 2f, mid.Pos, true, false);
        Check(me.GlUsed == 1 && g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.GrenadeShot), "a click lobs one HE grenade (used " + me.GlUsed + ")");
        Check(mid.Hp >= mid.MaxHp, "it flies over enemies: nothing is hurt on the way");
        Step(1.0f, mid.Pos, false, false);
        float midLoss = mid.MaxHp - mid.Hp, rimLoss = rim.MaxHp - rim.Hp;
        Check(midLoss > 30f && rimLoss > 0f && rimLoss < midLoss && away.Hp >= away.MaxHp, "the burst hurts the middle most, the rim less, and nothing far off (" + midLoss.ToString("0") + ", " + rimLoss.ToString("0") + ")");

        // six to a drum, then the reload, and nothing fires meanwhile
        g.Enemies.Clear(); g.Bullets.Clear();
        Step(2.6f, from + new V2(250f, 0f), true, false);
        Check(me.GlUsed == Game.GlDrum && me.GlReload > 0f, "the drum empties after six and reloads (used " + me.GlUsed + ", reload " + me.GlReload.ToString("0.00") + ")");
        Check(client.Game.Players[1].GlReload > 0f && client.Game.Players[1].GlUsed == me.GlUsed, "the other screen sees the reload");
        int shot = me.GlShot;
        Step(Math.Max(dt, me.GlReload - 0.15f), from + new V2(250f, 0f), true, false);
        Check(me.GlShot == shot, "nothing fires while it reloads");
        Step(0.5f, from + new V2(250f, 0f), false, false);
        Check(me.GlUsed == 0 && me.GlReload <= 0f, "it comes back full");

        // Alt: lobs a smoke canister while the HE drum stays loaded
        g.Bullets.Clear(); g.Zones.Clear();
        Step(dt * 4f, from + new V2(250f, 0f), false, true);
        Check(me.GlMode == 0 && me.GlSmokeUsed == 1 && g.Zones.Values.Any(z => z.Kind == ZoneKind.SmokeShell), "Alt lobs a smoke canister without swapping");
        Step(0.5f, from + new V2(250f, 0f), false, false);
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.SmokeShell), "the other screen sees the canister");
        g.Zones.Clear(); me.GlSmokeUsed = 0; me.GlCharge = 0f;

        // a regular enemy shoots until a cloud opens on it; then it's jammed and slowed. A mini-boss is only slowed.
        var miniType = FirstEnemy(d => d.Mini);
        var shooter = Dummy(EnemyType.Grunt, from + new V2(250f, 0f));
        var mini = Dummy(miniType, from + new V2(270f, 50f));
        shooter.FireT = 0.1f;
        enemyShots = 0;
        watched = shooter;
        Step(1.2f, from + new V2(600f, -300f), false, false);
        Check(enemyShots > 0, "the grunt shoots while no cloud is on it (" + enemyShots + ")");
        mini.Pos = shooter.Pos + new V2(20f, 50f);  // (it wandered while the grunt shot)
        Step(dt * 2f, shooter.Pos - new V2(Game.GlSmokeHop1 + Game.GlSmokeHop2, 0f), false, true);
        var cloud = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.SmokeShell);
        Check(cloud != null && me.GlSmokeUsed == 1, "Alt lobs a smoke canister");
        Step(1.5f, from, false, false);
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.SmokeShell), "the other screen has the cloud");
        enemyShots = 0;
        Step(1.5f, from, false, false);
        Check(shooter.SmokeT > 0f && enemyShots == 0, "in the cloud the grunt is jammed: no shots (" + enemyShots + ")");
        Check(shooter.WadeT > 0f && Math.Abs(shooter.WadeMult - (1f - Game.GlSmokeSlow)) < 0.01f, "and slowed 45% (" + shooter.WadeMult.ToString("0.00") + ")");
        Check(mini.SmokeT <= 0f && mini.WadeT > 0f && Math.Abs(mini.WadeMult - (1f - Game.GlSmokeBossSlow)) < 0.01f, "a mini-boss in it is only slowed 30% (" + mini.WadeMult.ToString("0.00") + ")");
        Check(client.Game.InGlSmoke(client.Game.Enemies[shooter.Id]) && !client.Game.InGlSmoke(client.Game.Enemies[mini.Id]), "the other screen can tell who's jammed");

        // the cloud ends; the canisters come back one every 6 s
        Step(4.5f, from, false, false);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.SmokeShell), "the cloud is gone after its 6 s");
        enemyShots = 0;
        shooter.FireT = 0.1f;
        Step(1.2f, from, false, false);
        Check(enemyShots > 0, "out of the smoke the grunt shoots again");
        g.Enemies.Clear();
        Check(me.GlSmokeUsed == 0, "the canister came back after 6 s (" + me.GlSmokeUsed + " spent)");
        for (int i = 0; i < 4; i++)
        {
            Step(dt * 2f, from + new V2(200f, 0f), false, true);
            Step(0.7f, from + new V2(200f, 0f), false, false);
        }
        Check(me.GlSmokeUsed == Game.GlSmokeMax, "three canisters, then it's dry (" + me.GlSmokeUsed + ")");

        // put away and back: ready again
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0);
        Step(dt * 2f, from, false, false);
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 3);
        Check(me.GlMode == 0 && me.GlSmokeUsed == 0 && me.GlUsed == 0, "put away, it comes back on the HE drum with everything loaded");
    }
}
