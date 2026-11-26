using System;
using System.Linq;
using Arena;

// The Fireball's variants, the Waterball and the Earthball (ElementBalls.cs).
static partial class Program
{
    /// The Fireball's Variants window lists both; the Waterball rolls through a line of enemies and soaks each (no burn);
    /// the Earthball stops on the first, stuns it (shorter on a heavy, shortest on a boss) and its rubble hurts the one beside it.
    static void ElementBallsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        Check(Variants.VariantOf(WeaponId.Fireball, 0) == WeaponId.Waterball && Variants.VariantOf(WeaponId.Fireball, 1) == WeaponId.Earthball,
            "the Fireball's Variants window holds the Waterball (tier I) and the Earthball (tier II)");
        Check(Defs.Weapon(Role.Dps, Kit.Elementalist, 4).Id == WeaponId.Waterball && Defs.Weapon(Role.Dps, Kit.Elementalist, 5).Id == WeaponId.Earthball
            && Defs.WeaponCount(Role.Dps, Kit.Elementalist) == 4, "they are the Elementalist DPS's picks 4 and 5, with no lobby key of their own");
        Check(Variants.Blurb(WeaponId.Waterball).Length > 0 && Variants.Blurb(WeaponId.Earthball).Length > 0, "both have a Variants window blurb");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 4, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.Waterball, "picking 4 plays the Waterball (" + me.Weapon.Id + ")");
        V2 from = new V2(200f, 450f);
        Func<EnemyType, V2, Enemy> dummy = (type, at) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.Speed = 0f; e.FireT = 99f; return e; };
        Func<Enemy[], V2[], float, bool> play = (ds, spots, secs) =>
        {
            bool seen = false;
            for (int f = 0; f < secs * 60; f++)
            {
                for (int k = 0; k < ds.Length; k++) ds[k].Pos = spots[k];
                me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) });
                if (ds.Length > 0 && ds[0].StunT > 0f) seen = true;
            }
            return seen;
        };

        // The Waterball: through every enemy in the line, each wet, none burning.
        g.Enemies.Clear(); g.Bullets.Clear();
        var line = new[] { from + new V2(150f, 0f), from + new V2(230f, 10f), from + new V2(310f, -10f) };
        var row = line.Select(at => dummy(EnemyType.Grunt, at)).ToArray();
        me.Pos = from;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var ball = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1);
        Check(ball != null && ball.Kind == Defs.WaterballShot, "the Waterball fires a waterball");
        play(row, line, 1.2f);
        Console.WriteLine("  waterball: hits {0:0} / {1:0} / {2:0}; wet {3:0.0} / {4:0.0} / {5:0.0} s", row[0].MaxHp - row[0].Hp, row[1].MaxHp - row[1].Hp, row[2].MaxHp - row[2].Hp, row[0].WetT, row[1].WetT, row[2].WetT);
        Check(row.All(e => e.Hp < e.MaxHp && e.WetT > 2f), "it rolls through every enemy in its path and soaks each");
        Check(row.All(e => e.BurnDps <= 0f && e.BurnT <= 0f), "it sets nothing burning");

        // The Earthball: stops on the first, stuns it, and the rubble hurts the enemy beside it.
        me.Weapon = Defs.WeaponOf(WeaponId.Earthball);
        Func<EnemyType, float> stunOn = type =>
        {
            g.Enemies.Clear(); g.Bullets.Clear();
            var spots = new[] { from + new V2(160f, 0f), from + new V2(160f, 50f), from + new V2(320f, 0f) };
            var ds = new[] { dummy(type, spots[0]), dummy(EnemyType.Grunt, spots[1]), dummy(EnemyType.Grunt, spots[2]) };
            me.Pos = from;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            float peak = 0f;
            for (int f = 0; f < 60; f++)
            {
                for (int k = 0; k < ds.Length; k++) ds[k].Pos = spots[k];
                me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) });
                peak = Math.Max(peak, ds[0].StunT);
            }
            if (type == EnemyType.Grunt)
            {
                Console.WriteLine("  earthball: first {0:0}, beside {1:0} (rubble), behind {2:0}; stun {3:0.00} s", ds[0].MaxHp - ds[0].Hp, ds[1].MaxHp - ds[1].Hp, ds[2].MaxHp - ds[2].Hp, peak);
                Check(ds[0].Hp < ds[0].MaxHp && ds[1].Hp < ds[1].MaxHp, "the Earthball hits the first enemy and its rubble hurts the one beside it");
                Check(ds[2].Hp >= ds[2].MaxHp, "it stops on the first: the enemy behind is untouched");
            }
            return peak;
        };
        var heavyType = HeavyFoe(moving: true);
        float grunt = stunOn(EnemyType.Grunt), heavy = stunOn(heavyType);
        Console.WriteLine("  earthball stun: grunt {0:0.00} s, {1} (heavy) {2:0.00} s", grunt, heavyType, heavy);
        Check(grunt > 0.6f, "it stuns a regular enemy for about 0.8 s");
        Check(heavy > 0f && heavy < grunt, "a heavy is stunned for less");
        host.Leave("");
    }
}
