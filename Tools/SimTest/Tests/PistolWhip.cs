using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Revolver's Alt, the Pistol Whip (PistolWhip.cs).
static partial class Program
{
    /// Alt with the Revolver swings a wide arc in front: it hurts and shoves back what's in it, destroys enemy bullets in it (big
    /// ones too) but not behind you, cools down 4 s, works from the client's key, and the other screen sees its look.
    static void PistolWhipTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
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
        me.Weapon = friend.Weapon = Defs.WeaponOf(WeaponId.Pistol);
        client.Game.Players[1].Weapon = client.Game.Players[2].Weapon = Defs.WeaponOf(WeaponId.Pistol);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), farAway = new V2(900f, 200f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Func<byte, V2, Bullet> Shot = (kind, at) => (Bullet)shoot.Invoke(g, new object[] { kind, at, V2.Zero, 0f, 5f, 10f, -1, false, 0f });
        Action<float, bool, bool> Step = (secs, hostAlt, clientAlt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; friend.Pos = client.Game.Players[2].Pos = farAway; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = from + new V2(300f, 0f), WeaponAction = hostAlt && f < 3 }
                    : new PlayerInput { AimAt = farAway + new V2(300f, 0f), WeaponAction = clientAlt && f < 3 }); x.Game.Update(dt); }
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(0.2f, false, false);

        var front = Dummy(from + new V2(90f, 0f));
        var side = Dummy(from + new V2(10f, 100f));    // beside you: inside the 190 degree arc
        var behind = Dummy(from + new V2(-110f, 0f));
        var small = Shot(Defs.SmallShot, from + new V2(100f, -40f));
        var big = Shot(Defs.BigShot, from + new V2(60f, 70f));
        var back = Shot(Defs.SmallShot, from + new V2(-90f, 10f));
        V2 frontWas = front.Pos;
        Step(0.25f, true, false);
        Check(front.Hp < front.MaxHp && side.Hp < side.MaxHp, "the whip hurts what's in front and beside you (front " + (front.MaxHp - front.Hp).ToString("0") + ", side " + (side.MaxHp - side.Hp).ToString("0") + ")");
        Check(front.MaxHp - front.Hp >= Defs.WeaponOf(WeaponId.Pistol).Damage * 1.5f, "it hits harder than a round");
        Check(behind.Hp == behind.MaxHp, "it misses what's behind you");
        Check(front.Pos.X > frontWas.X + 25f, "it shoves what it hits back (" + (front.Pos.X - frontWas.X).ToString("0") + " px)");
        Check(!g.Bullets.ContainsKey(small.Id) && !g.Bullets.ContainsKey(big.Id), "it destroys enemy shots in the arc, big ones too");
        Check(g.Bullets.ContainsKey(back.Id), "a shot behind you is left alone");
        Check(client.Game.Effects.Any(fx => fx.Kind == FxKind.PistolWhip && (int)fx.Radius == 1), "the other screen sees the whip");

        // the cooldown
        float hp = front.Hp;
        me.WhipCd = 0.5f;  // (the host's own cooldown; your key's is the local one, still running)
        Step(0.3f, true, false);
        Check(front.Hp == hp, "pressed again too soon: nothing");
        Check(g.WhipCooldown(me) > 0f && g.WhipCooldown(me) < Game.WhipCd, "the screens can tell how long until it's ready (" + g.WhipCooldown(me).ToString("0.0") + " s)");
        Step(Game.WhipCd, false, false);
        front.Pos = from + new V2(90f, 0f);
        Step(0.25f, true, false);
        Check(front.Hp < hp, "after the cooldown it swings again");

        // from the client's key
        var theirs = Dummy(farAway + new V2(90f, 0f));
        Step(0.3f, false, true);
        Check(theirs.Hp < theirs.MaxHp, "a client's Alt whips too");

        // not with another weapon
        me.Weapon = Defs.WeaponOf(WeaponId.MendersBow);
        g.Enemies.Clear(); Step(Game.WhipCd + 0.1f, false, false);
        var bowDummy = Dummy(from + new V2(90f, 0f));
        Step(0.3f, true, false);
        Check(bowDummy.Hp == bowDummy.MaxHp, "the Mender's Bow's Alt isn't a whip");
    }
}
