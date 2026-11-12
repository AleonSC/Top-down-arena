using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Twin Rifle's second variant, Out of Ammo (OutOfAmmo.cs).
static partial class Program
{
    /// Nothing is shot; the chain runs on its own on a close enemy in order (left bash, cross cut, right bash, reverse cut, rifle clap); each rifle
    /// swats one bullet and recovers (two at once, the third leaks); Alt spins (cuts, swats everything, then a cooldown); the other screen sees it.
    static void OutOfAmmoTest(float dt)
    {
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var shoot = typeof(Game).GetMethod("SpawnBullet", bf);
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
        Check(me.Weapon.Id == WeaponId.OutOfAmmo, "the Soldier DPS's fifth pick is Out of Ammo (" + me.Weapon.Id + ")");
        Check(Defs.WeaponCount(Role.Dps, Kit.Soldier) == 3, "it has no lobby key of its own (the Twin Rifle's Variants window offers it)");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        V2 from = new V2(400f, 450f), aimAt = new V2(700f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        var moves = new List<int>();
        int lastSeq = me.AmmoSeq;
        Action<float, bool, V2?> Step = (secs, alt, pin) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                if (pin != null) foreach (var e in g.Enemies.Values) e.Pos = pin.Value;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aimAt, Fire = false, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                if (me.AmmoSeq != lastSeq) { lastSeq = me.AmmoSeq; moves.Add(me.AmmoMove); }
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        var dummy = Dummy(new V2(from.X + 60f, from.Y));

        // The chain, with no trigger pulled.
        Step(3.2f, false, dummy.Pos);
        Check(!g.Bullets.Values.Any(b => b.Owner == 1), "nothing is shot");
        Check(moves.Count >= 5 && moves.Take(5).SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "the chain runs left bash, cross cut, right bash, reverse cut, rifle clap (" + string.Join(",", moves) + ")");
        float dealt = dummy.MaxHp - dummy.Hp;
        Check(dealt >= 79f * 0.99f, "the five hits dealt " + dealt.ToString("0") + " (10 + 12 + 10 + 12 + 35)");
        Check(client.Game.Players[1].AmmoSeq == me.AmmoSeq, "the other screen sees the moves (" + client.Game.Players[1].AmmoSeq + " of " + me.AmmoSeq + ")");

        // Nothing close: it stands down. An enemy behind you is not in the cone.
        g.Enemies.Clear(); moves.Clear();
        var behind = Dummy(new V2(from.X - 50f, from.Y));
        Step(1f, false, behind.Pos);
        Check(moves.Count == 0 && behind.Hp == behind.MaxHp, "an enemy behind you is left alone");

        // Swats: three bullets at once, one per rifle, the third leaks until a rifle has recovered.
        g.Enemies.Clear(); g.Bullets.Clear(); me.AmmoSwatCdL = me.AmmoSwatCdR = 0f;
        Dummy(new V2(1300f, 200f));  // (a wave with nobody left clears the bullets)
        Action<V2> Bullet = at => shoot.Invoke(g, new object[] { (byte)10, at, new V2(-1f, 0f) * 40f, 0f, 3f, 5f, 0, false, 0f });
        Bullet(new V2(from.X + 50f, from.Y - 20f)); Bullet(new V2(from.X + 50f, from.Y + 20f)); Bullet(new V2(from.X + 55f, from.Y));
        int before = g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
        Step(dt * 2f, false, null);
        int after = g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
        Check(before == 3 && after == 1, "each rifle swats one bullet: 3 -> " + after);
        Check(me.SwatSeqL > 0 && me.SwatSeqR > 0, "both rifles swung");
        Step(0.7f, false, null);
        Check(g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)) == 0, "a rifle that has recovered swats the next");

        // Alt: the spin cuts everything round you and swats every bullet in reach.
        g.Enemies.Clear(); g.Bullets.Clear(); me.AmmoCd = 0f;
        var a = Dummy(new V2(from.X + 55f, from.Y - 30f)); var b2 = Dummy(new V2(from.X - 50f, from.Y + 35f));
        for (int i = 0; i < 6; i++) { float ang = i * 1.0472f; Bullet(from + V2.FromAngle(ang) * 60f); }
        Step(dt * 4f, true, null);
        Check(me.AmmoSpin > 0f || me.AmmoCd > 0f, "Alt starts the spin");
        Check(!g.Bullets.Values.Any(b => Defs.IsEnemyShot(b.Kind)), "the spin swats every bullet in reach, however many");
        Step(1.3f, false, null);
        Check(a.Hp < a.MaxHp && b2.Hp < b2.MaxHp, "the spin cuts enemies on both sides (" + (a.MaxHp - a.Hp).ToString("0") + " and " + (b2.MaxHp - b2.Hp).ToString("0") + ")");
        Check(me.AmmoSpin == 0f && me.AmmoCd > 3f, "the spin ends, and Alt cools down (" + me.AmmoCd.ToString("0.0") + " s)");
        float cd = me.AmmoCd;
        Step(dt * 4f, true, null);
        Check(me.AmmoSpin == 0f, "Alt does nothing while it cools down");
    }
}
