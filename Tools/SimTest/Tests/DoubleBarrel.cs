using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Soldier DPS's fourth weapon, the Double Barrel (DoubleBarrel.cs).
static partial class Program
{
    /// Left then right barrel (9 pellets each, pierce 2), two shells then a break-open reload, no firing while it is open, Alt fires both
    /// at once, damage falls off with distance, and the other screen sees the shells and the reload.
    static void DoubleBarrelTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gunner", Role.Dps, 3, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.DoubleBarrel && Defs.WeaponCount(Role.Dps, Kit.Soldier) == 4, "the Soldier DPS's fourth key is the Double Barrel (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        int pellets = 0;
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                pellets += g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind == Defs.DpsShot && b.Life0 - b.Life < dt * 1.5f);
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // one click: a barrel, then the other 0.32 s later, then the reload
        var near = Dummy(from + new V2(70f, 0f));
        V2 aimNear = near.Pos;
        Step(dt * 2f, aimNear, true, false);
        Check(me.DbUsed == 1 && me.DbBarrel == 1 && pellets == Game.DbPellets, "the first click fires the left barrel: 9 pellets, one shell spent, the right is next (used " + me.DbUsed + ", pellets " + pellets + ")");
        Check(near.Hp < near.MaxHp, "the pellets hit");
        Step(0.2f, aimNear, true, false);
        Check(me.DbUsed == 1, "the next barrel waits out the 0.32 s between shots");
        Step(0.2f, aimNear, true, false);
        Check(me.DbUsed >= 2 && me.DbReload > 0f, "the second barrel fires and the gun breaks open to reload (reload " + me.DbReload.ToString("0.00") + ")");
        int before = pellets;
        Step(0.4f, aimNear, true, false);
        Check(pellets == before && me.DbReload > 0f, "nothing fires while it is open");
        Check(client.Game.Players[1].DbReload > 0f && client.Game.Players[1].DbUsed == me.DbUsed, "the other screen sees the reload");
        Step(0.6f, aimNear, true, false);
        Check(me.DbUsed <= 1 && pellets > before, "it closes loaded and fires again");

        // damage falls off with distance: the same volley on a near and a far dummy
        Check(Math.Abs(Game.DbFalloff(100f) - 1f) < 1e-4f && Math.Abs(Game.DbFalloff(400f) - Game.DbMinMult) < 1e-4f && Game.DbFalloff(270f) < 0.8f && Game.DbFalloff(270f) > 0.5f, "the falloff is full to 140 px and 35% at 400 px");
        Step(1.5f, aimNear, false, false);  // (let it reload)
        g.Enemies.Clear(); g.Bullets.Clear(); me.DbUsed = 0; me.DbReload = 0f; me.FireCd = 0f;
        var nearD = Dummy(from + new V2(70f, 0f));
        Step(dt * 2f, nearD.Pos, true, false);
        float nearLoss = nearD.MaxHp - nearD.Hp;
        Step(0.6f, nearD.Pos, false, false);
        g.Enemies.Clear(); g.Bullets.Clear(); me.DbUsed = 0; me.DbReload = 0f; me.FireCd = 0f;
        var far = Dummy(from + new V2(370f, 0f));
        for (int i = 0; i < 4; i++)  // (a few volleys: some pellets miss at range)
        {
            me.DbUsed = 0; me.DbReload = 0f; me.FireCd = 0f;
            Step(dt * 2f, far.Pos, true, false);
            Step(0.3f, far.Pos, false, false);
        }
        float farLoss = (far.MaxHp - far.Hp) / 4f;
        Check(nearLoss > 40f && farLoss > 0f && nearLoss > farLoss * 2f, "a volley at 70 px does far more than one at 370 px (" + nearLoss.ToString("0") + " vs " + farLoss.ToString("0") + ")");

        // pierce: the second enemy in a line is hit too
        g.Enemies.Clear(); g.Bullets.Clear(); me.DbUsed = 0; me.DbReload = 0f; me.FireCd = 0f;
        var a = Dummy(from + new V2(60f, 0f)); var b2 = Dummy(from + new V2(120f, 0f));
        Step(dt * 2f, b2.Pos, true, false);
        Step(0.3f, b2.Pos, false, false);
        Check(a.Hp < a.MaxHp && b2.Hp < b2.MaxHp, "pellets pass through the first enemy to the one behind it");

        // Alt: both barrels at once, only with both shells loaded
        g.Enemies.Clear(); g.Bullets.Clear(); me.DbUsed = 0; me.DbReload = 0f; me.FireCd = 0f; pellets = 0;
        Step(dt * 4f, from + new V2(300f, 0f), false, true);
        Check(me.DbUsed == 2 && me.DbReload > 0f && pellets == 2 * Game.DbPellets, "Alt fires both barrels at once: 18 pellets (" + pellets + ") and the reload starts");
        Step(1.2f, from + new V2(300f, 0f), false, false);
        me.DbUsed = 1; me.FireCd = 0f; pellets = 0;
        Step(dt * 4f, from + new V2(300f, 0f), false, true);
        Check(pellets == 0 && me.DbUsed == 1, "Alt does nothing with a shell spent");
    }
}
