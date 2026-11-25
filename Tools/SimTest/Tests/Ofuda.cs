using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Ninja's Ofuda (Ofuda.cs).
static partial class Program
{
    /// Fire: a Healing Talisman bends onto a hurt teammate late in its flight and heals them; one that touches an enemy does
    /// no damage but shoves it and erases small enemy shots near. Alt: a Blast Ofuda sticks to the enemy it hits, rides it
    /// (the other screen sees which) and blows for the weapon's damage; one that misses lands and blows later; its cooldown.
    static void OfudaTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Paper", Role.Healer, 2, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.Ofuda, "the Healer Ninja's third weapon is the Ofuda (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        V2 mateAt = new V2(400f, 900f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action Clear = () => { queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Zones.Clear(); };
        Action<float, V2, bool, bool> Step = (secs, aim, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; mate.Pos = mateAt; client.Game.Players[2].Pos = mateAt; queue.Clear();
                foreach (var x in s)
                {
                    x.Poll();
                    var input = x == host ? new PlayerInput { AimAt = aim, Fire = fire && f == 0, WeaponAction = alt && f < 3 } : new PlayerInput { AimAt = mateAt + new V2(300f, 0f) };
                    x.Game.FixedStep(dt, input);
                }
            }
        };
        Clear();
        Step(0.5f, from + new V2(300f, 0f), false, false);

        // a talisman aimed past a hurt teammate bends onto it near the end of its flight and heals it
        mateAt = from + new V2(320f, 70f);
        Step(0.3f, from + new V2(300f, 0f), false, false);
        mate.Hp = mate.MaxHp * 0.4f;
        float mh = mate.Hp;
        Step(1.4f, from + new V2(400f, 0f), true, false);
        float healed = mate.Hp - mh;
        Check(healed >= Game.TalismanHeal + Game.TalismanBurstHeal - 0.5f, "a talisman aimed past a hurt teammate bends onto it and heals it (" + healed.ToString("0.0") + ")");
        Check(g.Effects.Any(fx => fx.Kind == FxKind.TalismanBurst), "it bursts (FxKind.TalismanBurst)");
        // a full-health teammate isn't chased: the talisman flies on straight
        mate.Hp = mate.MaxHp;
        g.Bullets.Clear();
        Step(dt * 2f, from + new V2(400f, 0f), true, false);
        var straight = g.Bullets.Values.FirstOrDefault(b => b.Kind == Defs.OfudaShot);
        Step(0.9f, from + new V2(400f, 0f), false, false);
        Check(straight != null && Math.Abs(straight.Vel.Angle) < 0.01f, "a talisman doesn't bend toward a teammate at full health");
        mateAt = new V2(400f, 900f);
        Step(0.6f, from + new V2(300f, 0f), false, false);

        // a talisman on an enemy: no damage, a shove, and small enemy shots near it erased
        Clear();
        var grunt = Dummy(from + new V2(150f, 0f));
        V2 g0 = grunt.Pos;
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(170f, 30f), new V2(0f, 1f), 0f, 5f, 10f, 0, false, 0f });
        var farShot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(170f, 260f), new V2(0f, 1f), 0f, 5f, 10f, 0, false, 0f });
        Step(0.6f, grunt.Pos, true, false);
        float pushed = grunt.Pos.DistanceTo(g0);
        Check(grunt.Hp >= 1e6f, "a talisman does no damage (" + (1e6f - grunt.Hp) + ")");
        Check(pushed > Game.TalismanShove * 0.6f, "it shoves the enemy it bursts on (" + pushed.ToString("0") + " px)");
        Check(!g.Bullets.ContainsKey(near.Id) && g.Bullets.ContainsKey(farShot.Id), "it erases small enemy shots in its burst, not ones outside");

        // Alt at an enemy: the Blast Ofuda sticks, rides it (seen on the other screen) and blows
        Clear();
        var target = Dummy(from + new V2(200f, 0f));
        Step(0.55f, target.Pos, false, true);
        var stuck = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BlastOfuda);
        Check(stuck != null && stuck.Side == target.Id, "a Blast Ofuda sticks to the enemy it hits");
        Step(0.1f, target.Pos, false, false);
        var seen = client.Game.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BlastOfuda);
        Check(seen != null && seen.Side == target.Id, "the other screen sees which enemy it rides");
        target.Pos += new V2(0f, 60f);
        Step(dt, target.Pos, false, true);  // pressed again at once: still cooling down
        Check(stuck != null && stuck.Pos.DistanceTo(target.Pos) <= target.Radius + 1f, "it moves with its enemy");
        Step(0.2f, target.Pos, false, false);
        Check(g.Zones.Values.Count(z => z.Kind == ZoneKind.BlastOfuda) + g.Bullets.Values.Count(b => b.Kind == Defs.BlastOfudaShot) == 1, "Alt again during its cooldown throws nothing");
        Step(Game.BlastFuse, target.Pos, false, false);
        float full = me.Weapon.Damage * me.Stats.DmgMult;
        float took = 1e6f - target.Hp;
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.BlastOfuda), "it blows when its fuse runs out");
        Check(Math.Abs(took - full) < full * 0.15f, "the blast deals the weapon's damage to its enemy (" + took.ToString("0.0") + " of " + full + ")");

        // Alt at empty ground: it lands at the end of its flight and blows later, less at the rim
        Clear();
        Step(Game.BlastCd, from + new V2(0f, -300f), false, false);
        Step(0.9f, from + new V2(0f, -600f), false, true);
        var landed = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BlastOfuda);
        float landAt = landed != null ? landed.Pos.DistanceTo(from) : -1f;
        Check(landed != null && landed.Side == 0 && Math.Abs(landAt - Game.BlastRange) < 30f, "a Blast Ofuda that hits nothing lands at the end of its flight (" + landAt.ToString("0") + " px)");
        var rim = Dummy(landed != null ? landed.Pos + new V2(landed.Radius - 2f, 0f) : from);
        Step(Game.BlastGroundFuse, from, false, false);
        float rimTook = 1e6f - rim.Hp;
        Check(rimTook > full * (Game.BlastEdge - 0.1f) && rimTook < full * 0.8f, "the landed charm blows later, less at the rim (" + rimTook.ToString("0.0") + ")");
        Check(Defs.WeaponOf(WeaponId.Ofuda).Desc.Contains("Alternative Fire"), "the weapon's text names its Alt");
    }
}
