using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Revolver's Fan stance: the Pistol Whip and Fan the Hammer (PistolWhip.cs).
static partial class Program
{
    /// Alt switches the Revolver to the Fan stance (seen on the other screen, and from a client's key). There a click whips
    /// (stuns the pack in front, cuts small shots, misses what's behind), a click straight after fans the hammer (six rounds
    /// in a cone, harder on stunned enemies, then a reload), raising the parry after a whip starts the combo over, holding the
    /// parry and clicking fans, and raising the parry cuts a clicked fan.
    static void PistolWhipTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var bonus = typeof(Game).GetMethod("FanStunBonus", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Medic", Role.Healer, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var me = g.Players[1];
        var friend = g.Players[2];
        me.Weapon = friend.Weapon = Defs.WeaponOf(WeaponId.Revolver);
        client.Game.Players[1].Weapon = client.Game.Players[2].Weapon = Defs.WeaponOf(WeaponId.Revolver);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), farAway = new V2(900f, 200f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Func<byte, V2, Bullet> Shot = (kind, at) => (Bullet)shoot.Invoke(g, new object[] { kind, at, V2.Zero, 0f, 5f, 10f, -1, false, 0f });
        var mine = new HashSet<int>();
        int fired = 0;
        // Step: `click` frames (from 0) on which Fire goes down for 2 frames; `parry` holds it over [p0, p1); `alt` presses Alt at frame 0.
        Action<float, int[], int, int, bool, bool> Step = (secs, clicks, p0, p1, alt, clientAlt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; friend.Pos = client.Game.Players[2].Pos = farAway; queue.Clear();
                bool fire = clicks != null && clicks.Any(c => f >= c && f < c + 2);
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = from + new V2(300f, 0f), Fire = fire, Parry = f >= p0 && f < p1, WeaponAction = alt && f < 3 }
                    : new PlayerInput { AimAt = farAway + new V2(300f, 0f), WeaponAction = clientAlt && f < 3 }); x.Game.Update(dt); }
                foreach (var b in g.Bullets.Values) if (b.Owner == 1 && mine.Add(b.Id)) fired++;
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(0.2f, null, 0, 0, false, false);

        // the switch
        Check(me.RevStance == 0, "the Revolver starts in the Gunslinger stance");
        Step(0.4f, null, 0, 0, true, false);
        Check(me.RevStance == 1 && client.Game.Players[1].RevStance == 1, "Alt switches to the Fan stance (the other screen sees it)");

        // the whip
        var front = Dummy(from + new V2(90f, 0f));
        var up = Dummy(from + new V2(100f, -18f));
        var down = Dummy(from + new V2(100f, 18f));
        var behind = Dummy(from + new V2(-110f, 0f));
        var small = Shot(Defs.SmallShot, from + new V2(90f, -40f));
        var back = Shot(Defs.SmallShot, from + new V2(-90f, 10f));
        fired = 0;
        Step(0.15f, new[] { 0 }, 0, 0, false, false);
        Console.WriteLine("  fan stance: the whip hit {0:0} / {1:0} / {2:0}, stunned {3:0.00} s; fired {4}", front.MaxHp - front.Hp, up.MaxHp - up.Hp, down.MaxHp - down.Hp, front.StunT, fired);
        Check(front.Hp < front.MaxHp && up.Hp < up.MaxHp && down.Hp < down.MaxHp && fired == 0, "a click whips the whole pack in front, without firing");
        Check(front.StunT > 0.5f && up.StunT > 0.5f && down.StunT > 0.5f, "the whip stuns every enemy it hits");
        Check(behind.Hp == behind.MaxHp && behind.StunT <= 0f, "it misses what's behind you");
        Check(!g.Bullets.ContainsKey(small.Id) && g.Bullets.ContainsKey(back.Id), "it cuts a small shot in front, not one behind");
        Check(client.Game.Effects.Any(fx => fx.Kind == FxKind.PistolWhip && (int)fx.Radius == 1), "the other screen sees the whip");

        // Fan the Hammer, straight after
        float hpWas = front.Hp;
        var before = new HashSet<int>(g.Bullets.Keys);
        Step(0.6f, new[] { 0 }, 0, 0, false, false);
        var fan = g.Bullets.Values.Where(b => b.Owner == 1 && !before.Contains(b.Id)).ToList();
        Console.WriteLine("  fan stance: a second click fanned {0} rounds (fired {1}); the front grunt took {2:0}", fan.Count, fired, hpWas - front.Hp);
        Check(fired == Game.RevFanRounds, "a click right after the whip fans the hammer: " + Game.RevFanRounds + " rounds (" + fired + ")");
        Check(front.Hp < hpWas, "the fan hits the stunned pack");
        var stunned = new Enemy { StunT = 1f }; var free = new Enemy();
        var round = new Bullet { FanRound = true };
        Check((float)bonus.Invoke(g, new object[] { round, stunned }) == Game.RevFanStunMult && (float)bonus.Invoke(g, new object[] { round, free }) == 1f
            && (float)bonus.Invoke(g, new object[] { new Bullet(), stunned }) == 1f, "fan rounds hit stunned enemies 25% harder (only fan rounds)");
        Check(me.RevFanReload > 0f && client.Game.Players[1].RevFanReload > 0f, "then the hammer reloads (seen on the other screen)");

        // reloading: the second click whips again
        g.Bullets.Clear(); fired = 0;
        me.RevFanReload = 0.8f;
        Step(0.5f, new[] { 0, 18 }, 0, 0, false, false);
        Check(fired == 0 && me.RevStep != Game.RevFan, "while the fan reloads, a click after the whip whips again");
        Step(1.2f, null, 0, 0, false, false);

        // parry after a whip: the combo starts over
        fired = 0;
        Step(0.7f, new[] { 0, 30 }, 15, 22, false, false);
        Check(fired == 0 && me.RevStep == Game.RevWhip || fired == 0 && me.RevStep == Game.RevLinked, "raising the parry after a whip starts the combo over: the next click whips (" + fired + " fired)");
        Step(0.8f, null, 0, 0, false, false);

        // parry + click: fan any time
        fired = 0;
        Step(0.7f, new[] { 10 }, 0, 40, false, false);
        Check(fired == Game.RevFanRounds, "holding the parry and clicking fans the hammer (" + fired + " rounds)");
        Step(1.4f, null, 0, 0, false, false);

        // a clicked fan, cut by the parry
        fired = 0;
        Step(0.8f, new[] { 0, 14 }, 20, 40, false, false);
        Check(fired > 0 && fired < Game.RevFanRounds, "raising the parry cuts a clicked fan (" + fired + " of " + Game.RevFanRounds + " rounds)");
        Step(1.4f, null, 0, 0, false, false);

        // back to the Gunslinger
        Step(0.4f, null, 0, 0, true, false);
        Check(me.RevStance == 0, "Alt again goes back to the Gunslinger stance");

        // from the client's key
        Step(0.4f, null, 0, 0, false, true);
        Check(friend.RevStance == 1, "a client's Alt switches their stance too");

        // not with another weapon
        me.Weapon = Defs.WeaponOf(WeaponId.MendersBow);
        g.Enemies.Clear(); Step(0.5f, null, 0, 0, false, false);
        var bowDummy = Dummy(from + new V2(90f, 0f));
        int whips = g.Effects.Count(fx => fx.Kind == FxKind.PistolWhip);
        Step(0.3f, null, 0, 0, true, false);
        Check(g.Effects.Count(fx => fx.Kind == FxKind.PistolWhip) == whips && g.Bullets.Values.Any(b => b.Kind == Defs.ScatterArrow) | bowDummy.Hp < bowDummy.MaxHp,
            "the Mender's Bow's Alt isn't the Revolver's (it's Scatter)");
        host.Leave(""); client.Leave("");
    }
}
