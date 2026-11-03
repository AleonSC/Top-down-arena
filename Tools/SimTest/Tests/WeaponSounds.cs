using System.Linq;
using System;
using Arena;

// Weapon sounds for weapons that fire no bullets: the Repulsion Cannon's blast, and Chain Light's cone and arcs.
static partial class Program
{
    static void WeaponSoundsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // The Repulsion Cannon: its own blast sound, and not the shared shockwave sweep.
        var host = HostAs(new Session { AutoStart = 1 }, nextPort++, false, "Solo", Role.Healer, 0, 0, Kit.Soldier);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.LightBolt, "the Soldier Healer's first weapon is the Repulsion Cannon");
        g.Enemies.Clear(); g.Bullets.Clear();
        me.Pos = new V2(200f, 450f);
        var e1 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(320f, 450f), 0f, 0, false });
        e1.SpawnT = 0f; e1.Hp = e1.MaxHp = 1e5f;
        g.Sounds.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        int blasts = g.Sounds.Count(s => s.Sfx == Sfx.ShotRepulse), sweeps = g.Sounds.Count(s => s.Sfx == Sfx.Shockwave);
        Console.WriteLine("  repulsion cannon: {0} blast sound(s), {1} shockwave sound(s)", blasts, sweeps);
        Check(blasts == 1 && sweeps == 0, "a Repulsion Cannon shot plays its own blast sound once");

        // Chain Light: one cast sound for the cone and one for the arcs, however many enemies are lit.
        var host2 = HostAs(new Session { AutoStart = 1 }, nextPort++, false, "Solo", Role.Healer, 1, 0, Kit.Elementalist);
        var g2 = host2.Game;
        host2.StartMatch();
        var cl = g2.Players[1];
        g2.DevSetGod(1, true);
        Check(cl.Weapon.Id == WeaponId.ChainLight, "the Elementalist Healer's second weapon is Chain Light");
        g2.Enemies.Clear(); g2.Bullets.Clear();
        cl.Pos = new V2(200f, 450f);
        for (int k = 0; k < 4; k++)
        {
            var e = (Enemy)spawn.Invoke(g2, new object[] { EnemyType.Grunt, new V2(330f + 20f * k, 440f + 14f * k), 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = 1e5f;
        }
        g2.Sounds.Clear();
        volley.Invoke(g2, new object[] { cl, 0f, 0 });
        int cone = g2.Sounds.Count(s => s.Sfx == Sfx.SpellChainCone), arcs = g2.Sounds.Count(s => s.Sfx == Sfx.SpellChainArc);
        Console.WriteLine("  chain light: {0} cone sound(s), {1} arc sound(s) for 4 enemies", cone, arcs);
        Check(cone == 1 && arcs == 1, "Chain Light plays a cast sound for its cone and one for its arcs (not one per enemy)");

        // With nothing in the cone there are no arcs to hear.
        g2.Enemies.Clear();
        g2.Sounds.Clear();
        volley.Invoke(g2, new object[] { cl, 0f, 0 });
        Check(g2.Sounds.Count(s => s.Sfx == Sfx.SpellChainCone) == 1 && g2.Sounds.Count(s => s.Sfx == Sfx.SpellChainArc) == 0,
            "an empty cone still flashes (the cast sound) but has no arcs");
    }
}
