using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS Hemomancer's 1st weapon, the Needle Swarm (NeedleSwarm.cs).
static partial class Program
{
    /// The swarm comes out full and free; missing needles are siphoned for health (not under 15%); an enemy shot coming in through the
    /// ring breaks a needle and stops; held Fire grows a tendril to the enemy nearest the cursor (pulled back if let go early), which
    /// wraps it: cuts, slow, bleed, until it dies; up to three at once; Alt throws a fan of needles out of the swarm, with a cooldown;
    /// the other screen sees the count and the tendrils; going down or putting the weapon away loses the swarm.
    static void NeedleSwarmTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Bleeder", Role.Dps, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.NeedleSwarm && me.Weapon.Name == "Needle Swarm", "the Hemomancer DPS's 1st weapon is the Needle Swarm (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(600f, 450f);
        Func<V2, float, Enemy> Dummy = (at, hp) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = hp; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.Speed = 0f; }
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        V2 away = from + new V2(0f, -300f);

        // full and free when it comes out
        Step(dt * 3f, away, false, false);
        Check(me.NsCount == Game.NsBase, "the swarm comes out full (" + me.NsCount + " of " + Game.NsBase + ")");

        // the siphon: missing needles cost health, never under 15%
        me.Hp = me.MaxHp; me.NsCount = 100;
        float hp0 = me.Hp;
        Step(1f, away, false, false);
        int got = me.NsCount - 100;
        float per = (hp0 - me.Hp) / Math.Max(1, got), want = me.MaxHp * Game.NsCost;
        Check(got >= 28 && got <= 34, "missing needles are siphoned back, about 32 a second (" + got + ")");
        Check(per > want * 0.7f && per < want * 1.1f, "each costs " + (Game.NsCost * 100f).ToString("0.##") + "% of max health (" + per.ToString("0.00") + " of " + want.ToString("0.00") + ")");
        me.Hp = me.MaxHp * 0.1f; me.NsCount = 100;
        Step(0.5f, away, false, false);
        Check(me.NsCount == 100, "no siphon under 15% health (" + me.NsCount + ")");
        me.Hp = me.MaxHp;
        Step(3f, away, false, false);

        // a shot coming in through the ring breaks a needle and stops
        me.Hp = me.MaxHp * 0.1f;  // (under the siphon's floor, so the broken needle isn't straight back)
        int before = me.NsCount;
        float hpShot = me.Hp;
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(200f, 0f), new V2(-300f, 0f), 0f, 3f, 10f, 0, false, 0f });
        Step(0.9f, away, false, false);
        Check(!g.Bullets.ContainsKey(shot.Id) && me.NsCount == before - 1 && me.Hp >= hpShot - 0.01f, "an enemy shot stops on the ring and breaks a needle (" + before + " -> " + me.NsCount + ", health " + hpShot.ToString("0") + " -> " + me.Hp.ToString("0") + ")");
        Check(g.Effects.Any(e => e.Kind == FxKind.NeedleSwarm && e.Pos2.Y < 0.5f) || client.Game.Effects.Any(e => e.Kind == FxKind.NeedleSwarm), "the break is shown (FxKind.NeedleSwarm)");
        me.Hp = me.MaxHp;
        Step(3f, away, false, false);

        // held Fire: a tendril grows to the enemy nearest the cursor; let go early and it pulls back
        var a = Dummy(from + new V2(210f, 0f), 1e6f);
        var b = Dummy(from + new V2(0f, 200f), 1e6f);
        Step(0.3f, a.Pos, true, false);
        Check(me.NsTen[0] == a.Id && me.NsLen[0] > 0f && me.NsBound[0] == 0, "holding Fire grows a tendril to the enemy nearest the cursor (" + me.NsLen[0].ToString("0") + " px)");
        Check(!me.NsTen.Contains(b.Id), "one tendril grows at a time: the other enemy waits");
        Step(1f, a.Pos, false, false);
        Check(me.NsTen.All(x => x == 0), "let go before it arrives and it pulls back in");

        // held on: it arrives and wraps the enemy, cuts, slows and bleeds it
        g.Enemies.Remove(b.Id);
        float aHp = a.Hp;
        Step(2.5f, a.Pos, true, false);
        Check(me.NsTen[0] == a.Id && me.NsBound[0] > 0, "held on, it arrives and wraps the enemy (" + me.NsBound[0] + " needles)");
        Step(1.5f, a.Pos, false, false);
        Check(me.NsTen[0] == a.Id && me.NsBound[0] == Game.NsWrap, "it keeps the enemy wrapped after Fire is let go, " + Game.NsWrap + " needles (" + me.NsBound[0] + ")");
        float t0 = a.Hp;
        Step(1f, a.Pos, false, false);
        float dps = t0 - a.Hp;
        Check(dps > Game.NsWrap * me.Weapon.Damage * 0.8f, "the wrap cuts it, " + Game.NsWrap + " x " + me.Weapon.Damage + " a second plus the bleed (" + dps.ToString("0") + " in 1 s)");
        Check(a.SlowT > 0f && a.SlowMult <= Game.NsSlow + 0.01f, "and slows it to half speed");
        Check(aHp > a.Hp, "it was hurt (" + (aHp - a.Hp).ToString("0") + ")");

        // it lets go when the enemy dies
        a.Hp = 30f;
        Step(1f, a.Pos, false, false);
        Check(!g.Enemies.ContainsKey(a.Id) || a.Hp <= 0f, "the wrapped enemy dies");
        Check(me.NsTen.All(x => x == 0), "and the tendril lets go");

        // up to three at once; the other screen sees them
        g.Enemies.Clear();
        var _ = new[] { Dummy(from + new V2(150f, 0f), 1e6f), Dummy(from + new V2(-150f, 0f), 1e6f), Dummy(from + new V2(0f, 150f), 1e6f), Dummy(from + new V2(0f, -150f), 1e6f) };
        Step(3f, from + new V2(150f, 0f), true, false);
        Check(me.NsTen.Count(x => x != 0) == Game.NsTendrils, "holding on, the next enemies get the next tendrils, three at once (" + me.NsTen.Count(x => x != 0) + ")");
        Check(me.NsBound.Sum() <= me.NsCount * Game.NsShare + 1, "wrapping no more than 45% of the swarm between them (" + me.NsBound.Sum() + " of " + me.NsCount + ")");
        var other = client.Game.Players[1];
        Check(Math.Abs(other.NsCount - me.NsCount) <= 3 && other.NsTen.SequenceEqual(me.NsTen) && Enumerable.Range(0, 3).All(k => Math.Abs(other.NsBound[k] - me.NsBound[k]) <= 2 && Math.Abs(other.NsLen[k] - me.NsLen[k]) < 20f), "the other screen sees the swarm's count and the tendrils");
        Step(0.5f, away, false, false);
        g.Enemies.Clear();
        Step(0.5f, away, false, false);

        // Alt: a fan of needles out of the swarm, with a cooldown
        me.Hp = me.MaxHp;
        Step(4f, away, false, false);
        g.Bullets.Clear();
        int full = me.NsCount;
        var target = Dummy(from + new V2(250f, 0f), 1e6f);
        Step(dt * 4f, target.Pos, false, true);
        int thrown = g.Bullets.Values.Count(x => x.Owner == me.Id && x.Kind == Defs.BloodShot);
        Check(full - me.NsCount >= Game.NsBurst - 3 && thrown == Game.NsBurst, "Alt throws " + Game.NsBurst + " needles out of the swarm (" + (full - me.NsCount) + " gone, " + thrown + " in flight)");
        Step(0.4f, target.Pos, false, false);
        Check(target.Hp < target.MaxHp - Game.NsBurstDmg * 5f, "they hit the enemy they were thrown at (" + (target.MaxHp - target.Hp).ToString("0") + ")");
        int after = me.NsCount;
        Step(dt * 4f, target.Pos, false, true);
        Check(me.NsCount >= after, "Alt again during its cooldown does nothing");

        // putting the weapon away, or going down, loses the swarm; it comes back full when taken out again
        var keep = me.Weapon;
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Implosion);
        Step(dt * 2f, away, false, false);
        Check(me.NsCount == 0 && !me.NsReady, "putting the weapon away loses the swarm");
        me.Weapon = keep;
        Step(dt * 2f, away, false, false);
        Check(me.NsCount == Game.NsBase, "and it comes back full when taken out again");
        g.Enemies.Clear();
        host.Leave("");
    }
}
