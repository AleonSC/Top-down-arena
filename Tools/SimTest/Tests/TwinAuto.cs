using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Soldier DPS's fourth weapon, the Twin Rifle Auto (TwinAuto.cs).
static partial class Program
{
    /// Each gun picks the nearest enemy on its own side and fires with no trigger; Alt locks both on the enemy nearest the cursor
    /// (crossfire rounds) until it dies or Alt is pressed again; the other screen sees the guns and the lock.
    static void TwinAutoTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gunner", Role.Dps, 4, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.TwinRifleAuto, "the Soldier DPS's fourth weapon is the Twin Rifle Auto (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), aimAt = new V2(700f, 450f);  // facing right: your left is up the screen (arena y runs down)
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Func<IEnumerable<Bullet>> Mine = () => g.Bullets.Values.Where(b => b.Kind == Defs.DpsShot && b.Owner == 1);
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Enemy up = Dummy(new V2(520f, 340f)), down = Dummy(new V2(520f, 560f));
        bool sawShot = false;
        Action<float, V2, bool> Step = (secs, cursor, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = false, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                if (Mine().Any()) sawShot = true;
            }
        };

        Step(0.8f, aimAt, false);
        Check(sawShot, "it fires with nobody pulling the trigger");
        Check(me.TwinAimL < -0.2f && me.TwinAimR > 0.2f, "the left gun turns to the enemy up the screen and the right to the one below (" + me.TwinAimL.ToString("0.00") + ", " + me.TwinAimR.ToString("0.00") + ")");
        Check(up.Hp < up.MaxHp && down.Hp < down.MaxHp, "both enemies are being hit, each by its own gun");
        Check(me.TwinLock == 0, "nothing is locked yet");
        Check(client.Game.Players[1].TwinAimL == me.TwinAimL && client.Game.Players[1].TwinAimR == me.TwinAimR, "the other screen sees both guns");

        // Alt with the cursor by the lower enemy: both guns lock on it, and every round is crossfire.
        g.Bullets.Clear();
        Step(dt * 3f, down.Pos, true);
        Check(me.TwinLock == down.Id, "Alt locks the enemy nearest the cursor");
        Step(0.5f, down.Pos, false);
        Func<int, float> Off = gun => Math.Abs(MathX.Fmod((down.Pos - g.AutoGrip(me, me.Pos, gun)).Angle - g.AutoHeading(me, gun) + MathX.Pi, MathX.Tau) - MathX.Pi);
        Check(Off(0) < 0.1f && Off(1) < 0.1f, "both guns swing onto it (off by " + Off(0).ToString("0.00") + " and " + Off(1).ToString("0.00") + " rad)");
        sawShot = false; Step(0.2f, down.Pos, false);
        Check(sawShot && Mine().All(b => b.Crossfire), "locked rounds are crossfire");
        Check(client.Game.Players[1].TwinLock == down.Id, "the other screen sees the lock");
        float upHp = up.Hp;
        Step(0.5f, down.Pos, false);
        Check(up.Hp == upHp, "the other enemy is left alone while locked");

        // It dies: the lock lets go and the guns go back to picking their own.
        down.Hp = 0f; down.Pos = new V2(-999f, -999f);
        Step(0.3f, aimAt, false);
        Check(me.TwinLock == 0, "the lock lets go when its enemy dies");
        Step(0.5f, aimAt, false);

        // A second press lets go by hand; a press with nothing near the cursor locks nothing.
        Step(0.3f, aimAt, false);
        Step(dt * 3f, up.Pos, true);
        Check(me.TwinLock == up.Id, "Alt locks again");
        Step(0.3f, up.Pos, false);
        Step(dt * 3f, up.Pos, true);
        Check(me.TwinLock == 0, "Alt again lets go");
        Step(0.3f, new V2(100f, 100f), false);
        Step(dt * 3f, new V2(100f, 100f), true);
        Check(me.TwinLock == 0, "no lock with the cursor nowhere near an enemy");
    }
}
