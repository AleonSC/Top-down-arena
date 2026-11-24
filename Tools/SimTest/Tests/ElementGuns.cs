using System;
using System.Linq;
using Arena;

// The Flamethrower's variants, the Water Gun and the Gale Gun (ElementGuns.cs).
static partial class Program
{
    /// The Flamethrower's Variants window lists both; a Water Gun jet stops on the first enemy and splashes it and the enemies
    /// near it wet (one farther away stays dry); a Gale Gun gust passes through, slicing (bleed) and slowing (Slowed, not
    /// Chilled), a third of the damage behind the first.
    static void ElementGunsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        Check(Variants.VariantOf(WeaponId.Flamethrower, 0) == WeaponId.WaterGun && Variants.VariantOf(WeaponId.Flamethrower, 1) == WeaponId.GaleGun,
            "the Flamethrower's Variants window holds the Water Gun (tier I) and the Gale Gun (tier II)");
        Check(Defs.Weapon(Role.Tank, Kit.Elementalist, 4).Id == WeaponId.WaterGun && Defs.Weapon(Role.Tank, Kit.Elementalist, 5).Id == WeaponId.GaleGun
            && Defs.WeaponCount(Role.Tank, Kit.Elementalist) == 4, "they are the Elementalist Tank's picks 4 and 5, with no lobby key of their own");
        Check(Variants.Blurb(WeaponId.WaterGun).Length > 0 && Variants.Blurb(WeaponId.GaleGun).Length > 0, "both have a Variants window blurb");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 4, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        g.Bot = false;
        Check(me.Weapon.Id == WeaponId.WaterGun, "picking 4 plays the Water Gun (" + me.Weapon.Id + ")");
        V2 from = new V2(200f, 450f);
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.Speed = 0f; e.FireT = 99f; return e; };
        Action<Enemy[], V2[], float, bool> play = (es, at, secs, fire) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                for (int k = 0; k < es.Length; k++) es[k].Pos = at[k];
                me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f), Fire = fire });
            }
        };

        // The Water Gun.
        g.Enemies.Clear(); g.Bullets.Clear();
        var spots = new[] { from + new V2(160f, 0f), from + new V2(160f, 45f), from + new V2(160f, 160f) };
        var ds = spots.Select(dummy).ToArray();
        me.Pos = from;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var jet = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1);
        Check(jet != null && jet.Kind == Defs.WaterJet, "the Water Gun fires a water jet");
        play(ds, spots, 0.6f, false);
        Console.WriteLine("  water gun: hit {0:0.0} wet {1:0.0} s; beside {2:0.0} hp wet {3:0.0} s; far wet {4:0.0} s", ds[0].MaxHp - ds[0].Hp, ds[0].WetT, ds[1].MaxHp - ds[1].Hp, ds[1].WetT, ds[2].WetT);
        Check(ds[0].Hp < ds[0].MaxHp && ds[0].WetT > 2f, "a jet hits the first enemy and soaks it");
        Check(ds[1].WetT > 2f && ds[1].Hp == ds[1].MaxHp, "the splash soaks the enemy beside it (without hurting it)");
        Check(ds[2].WetT == 0f, "an enemy out of the splash stays dry");
        Check(ds[0].BurnT <= 0f, "it sets nothing burning");

        // The Gale Gun.
        me.Weapon = Defs.WeaponOf(WeaponId.GaleGun);
        g.Enemies.Clear(); g.Bullets.Clear();
        spots = new[] { from + new V2(100f, 0f), from + new V2(160f, 0f) };
        ds = spots.Select(dummy).ToArray();
        me.Pos = from;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        Check(g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.GaleShot), "the Gale Gun fires gusts");
        play(ds, spots, 0.5f, false);
        float first = ds[0].MaxHp - ds[0].Hp, second = ds[1].MaxHp - ds[1].Hp;
        Console.WriteLine("  gale gun: first {0:0.0}, behind {1:0.0}; bleeding {2}/{3}, slow {4:0.00}/{5:0.00}", first, second, ds[0].BleedT > 0f, ds[1].BleedT > 0f, ds[0].SlowMult, ds[1].SlowMult);
        Check(first > 0f && second > 0f, "gusts pass through enemies");
        Check(ds[0].BleedT > 0f && ds[1].BleedT > 0f, "they slice what they pass (it bleeds)");
        play(ds, spots, 0.2f, true);
        Check(ds[0].SlowMult < 1f && ds[0].FrostChill == 0, "and slow it (Slowed, never chilled)");
        host.Leave("");
    }
}
