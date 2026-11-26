using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Elementalist's earth and light statuses and its element fixes: Brittle (Brittle.cs: more damage taken, frozen it
// shatters; Meteor, the Earthball), Dazzled (Dazzled.cs: shots stray; Chain Light, Lumen Ward), Flame Wall sets enemies
// burning, Healing Rain soaks them, Vortex's wind reacts (Wind.cs), and the other screen sees Brittle and Dazzled.
static partial class Program
{
    static void ElementStatusTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var embrittle = typeof(Game).GetMethod("Embrittle", flags);
        var ballHit = typeof(Game).GetMethod("ElementBallHit", flags);
        var cone = typeof(Game).GetMethod("LightCone", flags);
        var lumen = typeof(Game).GetMethod("CastLumenWard", flags);
        var vortex = typeof(Game).GetMethod("CastVortex", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var acting = typeof(Game).GetField("actingEnemy", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Caster", Role.Dps, 1, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        var me = g.Players[1];
        g.DevSetGod(1, true);
        g.Enemies.Clear(); g.Bullets.Clear(); g.Zones.Clear();
        var spots = new Dictionary<Enemy, V2>();
        Func<EnemyType, V2, Enemy> pin = (type, at) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; spots[e] = at; return e; };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = new V2(200f, 800f); me.Hp = me.MaxHp; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(200f, 100f) }); client.Game.FixedStep(dt, new PlayerInput()); } };
        Action clear = () => { g.Enemies.Clear(); g.Zones.Clear(); g.Bullets.Clear(); spots.Clear(); };

        // Brittle: 20% more damage, for 5 s.
        var cracked = pin(EnemyType.Grunt, new V2(800f, 300f)); var plain = pin(EnemyType.Grunt, new V2(1000f, 300f));
        embrittle.Invoke(g, new object[] { cracked, me.Id, Game.BrittleTime });
        float c0 = cracked.Hp, p0 = plain.Hp;
        hurt.Invoke(g, new object[] { cracked, 100f, me.Id });
        hurt.Invoke(g, new object[] { plain, 100f, me.Id });
        float ratio = (c0 - cracked.Hp) / (p0 - plain.Hp);
        Console.WriteLine("  brittle takes {0:0.00}x", ratio);
        Check(Math.Abs(ratio - Game.BrittleMult) < 0.01f, "a brittle enemy takes 20% more damage");
        step(0.3f);
        Check(client.Game.Enemies[cracked.Id].Brittle && !client.Game.Enemies[plain.Id].Brittle, "the other screen sees who is brittle");
        step(Game.BrittleTime);
        Check(cracked.BrittleT == 0f && !client.Game.Enemies[cracked.Id].Brittle, "Brittle wears off after 5 s");

        // Brittle and frozen: it shatters, the ice and the cracks used up, half the burst on its neighbours.
        clear();
        var ice = pin(EnemyType.Grunt, new V2(800f, 300f)); var near = pin(EnemyType.Grunt, new V2(860f, 300f)); var far = pin(EnemyType.Grunt, new V2(1200f, 300f));
        ice.FrozenT = 5f;
        embrittle.Invoke(g, new object[] { ice, me.Id, Game.BrittleTime });
        float i0 = ice.Hp;
        step(dt * 2);
        float burst = Game.ShatterDmg + Game.ShatterDmgWave * g.Wave;
        Console.WriteLine("  shatter: {0:0.0} on it, {1:0.0} beside, {2:0.0} far (want {3:0.0})", i0 - ice.Hp, near.MaxHp - near.Hp, far.MaxHp - far.Hp, burst);
        Check(Math.Abs(i0 - ice.Hp - burst) < 1f, "a frozen brittle enemy shatters for the burst");
        Check(Math.Abs(near.MaxHp - near.Hp - burst * Game.ShatterSplash) < 1f && far.Hp == far.MaxHp, "half the burst hits enemies within 90 px, none farther");
        Check(ice.BrittleT == 0f && !Game.IceFrozen(ice), "the shatter uses up the cracks and breaks the ice");

        // Meteor: what it lands on is cracked first, so the blast lands 20% harder.
        clear();
        var under = pin(EnemyType.Grunt, new V2(800f, 300f));
        var met = g.AddZone(ZoneKind.Meteor, me, new V2(800f, 300f), 120f, 0.2f);
        float m0 = under.Hp;
        step(0.4f);
        float blast = Kits.Get(ZoneKind.Meteor).BlastDamage * met.DmgMult;
        Console.WriteLine("  meteor: {0:0.0} (base {1:0.0})", m0 - under.Hp, blast);
        Check(under.BrittleT > 0f && Math.Abs(m0 - under.Hp - blast * Game.BrittleMult) < 1f, "Meteor makes what it hits brittle, and its blast counts it");

        // The Earthball cracks what it hits and what's in the rubble.
        clear();
        var struck = pin(EnemyType.Grunt, new V2(800f, 300f)); var rubble = pin(EnemyType.Grunt, new V2(850f, 300f)); var clear2 = pin(EnemyType.Grunt, new V2(1200f, 300f));
        ballHit.Invoke(g, new object[] { new Bullet { Kind = Defs.EarthballShot, Owner = me.Id, Pos = struck.Pos, Splash = 80f }, struck });
        Check(struck.BrittleT > 0f && rubble.BrittleT > 0f && clear2.BrittleT == 0f, "the Earthball makes the enemy and the rubble round it brittle");

        // Dazzled: Chain Light dazzles what's in its cone; a dazzled enemy's shots stray up to 25 degrees.
        clear();
        var lit = pin(EnemyType.Grunt, new V2(200f, 650f)); var dark = pin(EnemyType.Grunt, new V2(1200f, 300f));
        cone.Invoke(g, new object[] { me, -MathX.Pi / 2f, 40f, 0 });
        Check(lit.DazzleT > Game.DazzleTime - 0.1f && dark.DazzleT == 0f, "Chain Light dazzles the enemies in its cone");
        step(0.3f);
        Check(client.Game.Enemies[lit.Id].Dazzled && !client.Game.Enemies[dark.Id].Dazzled, "the other screen sees who is dazzled");
        Func<Enemy, float> spread = shooter =>
        {
            acting.SetValue(g, shooter);
            float most = 0f;
            for (int k = 0; k < 200; k++)
            {
                var b = (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, shooter.Pos, new V2(300f, 0f), 0f, 1f, 5f, 0, false, 0f });
                most = Math.Max(most, Math.Abs((float)Math.Atan2(b.Vel.Y, b.Vel.X)));
            }
            acting.SetValue(g, null);
            g.Bullets.Clear();
            return most;
        };
        lit.DazzleT = 5f;
        float wide = spread(lit), straight = spread(dark);
        Console.WriteLine("  shots stray up to {0:0.0} deg dazzled, {1:0.0} deg not", wide * 180f / MathX.Pi, straight * 180f / MathX.Pi);
        Check(wide > Game.DazzleStray * 0.7f && wide <= Game.DazzleStray + 0.001f && straight < 0.001f, "a dazzled enemy's shots stray up to 25 degrees; others fly straight");
        step(Game.DazzleTime + 2.1f);
        Check(lit.DazzleT == 0f, "Dazzled wears off");

        // Lumen Ward dazzles enemies standing in its light.
        clear();
        var inLight = pin(EnemyType.Grunt, new V2(200f, 450f)); var outside = pin(EnemyType.Grunt, new V2(1300f, 800f));
        lumen.Invoke(g, new object[] { me, new V2(200f, 100f), false });
        step(1.2f);
        Check(inLight.DazzleT > 0f && outside.DazzleT == 0f, "Lumen Ward dazzles the enemies in its light");

        // Flame Wall sets what's in it burning; Healing Rain soaks enemies.
        clear();
        var walled = pin(EnemyType.Grunt, new V2(800f, 300f)); var rained = pin(EnemyType.Grunt, new V2(1200f, 300f));
        g.AddZone(ZoneKind.FlameWall, me, walled.Pos, 90f, 5f);
        g.AddZone(ZoneKind.HealingRain, me, rained.Pos, 130f, 5f);
        step(0.3f);
        Check(walled.BurnT > 0f && walled.BurnBy == me.Id, "Flame Wall sets the enemies in it burning");
        Check(rained.WetT > 0f && rained.WetT <= Game.RainWetTime, "Healing Rain soaks the enemies in it");

        // Vortex's wind reacts: a wet enemy in it is chilled (Wind Chill, Wind.cs).
        clear();
        var swirled = pin(EnemyType.Grunt, new V2(300f, 800f));
        swirled.WetT = 30f;
        me.Slots[Upgrades.UltSlot].Id = AbilityId.Vortex;
        vortex.Invoke(g, new object[] { me, false });
        step(1f);
        Check(swirled.FrostChill > 0 || Game.IceFrozen(swirled), "Vortex's wind chills a wet enemy in it");

        host.Leave(""); client.Leave("");
    }
}
