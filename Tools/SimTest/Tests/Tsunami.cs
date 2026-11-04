using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Tsunami (Tsunami.cs): the wave, wet, the slow, lightning arcing to every wet enemy, wearing off, and the mastered return.
static partial class Program
{
    static void TsunamiTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        var host = HostAs(new Session { AutoStart = 1 }, nextPort++, false, "Solo", Role.Dps, 1, 2, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Slots[0].Id == AbilityId.Tsunami && me.Weapon.Id == WeaponId.ArcLightning, "the Elementalist DPS's third ability is Tsunami (and the second weapon is Arc Lightning)");
        g.Enemies.Clear(); g.Bullets.Clear();
        Func<V2, Enemy> make = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at => { var e = make(at); spots[e] = at; return e; };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) { kv.Key.Pos = kv.Value; } me.Pos = new V2(200f, 450f); me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        me.Pos = new V2(200f, 450f);
        var near = pin(new V2(500f, 450f)); var far = pin(new V2(1000f, 520f)); var off = pin(new V2(500f, 850f)); var dry = pin(new V2(80f, 450f));  // behind me: the wave never reaches it
        me.Aim = 0f;
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        bool fx = g.Effects.Any(e => e.Kind == FxKind.Tsunami);
        step(2.4f);
        float nearHit = near.MaxHp - near.Hp, farHit = far.MaxHp - far.Hp;
        Console.WriteLine("  tsunami: near {0:0.0} (wet {1:0.0}), far {2:0.0} (wet {3:0.0}), off the line {4:0.0} (wet {5:0.0})", nearHit, near.WetT, farHit, far.WetT, off.MaxHp - off.Hp, off.WetT);
        Check(fx, "casting Tsunami broadcasts the wave");
        Check(Math.Abs(nearHit - Game.TsunamiDamage * me.Stats.DmgMult) < 0.5f && Math.Abs(farHit - nearHit) < 0.5f, "the wave nicks everything it crosses for 12, once each");
        Check(near.WetT > 4f && far.WetT > 4f && off.WetT == 0f && dry.WetT == 0f && off.Hp == off.MaxHp, "what it crosses is wet; what it misses stays dry and unhurt");
        Check(Math.Abs(near.SlowMult - (1f - Game.WetSlow)) < 0.01f, "wet slows by 30%");

        // Lightning anywhere arcs to every wet enemy: an Arc Lightning strip hits the dry enemy beside me.
        float n0 = near.Hp, f0 = far.Hp, d0 = dry.Hp, o0 = off.Hp;
        me.Aim = MathX.Pi;
        volley.Invoke(g, new object[] { me, me.Aim, 0 });
        float dryHit = d0 - dry.Hp, nearArc = n0 - near.Hp, farArc = f0 - far.Hp;
        Console.WriteLine("  arc lightning on a dry enemy {0:0.0}: wet enemies {1:0.0} / {2:0.0}, dry bystander {3:0.0}", dryHit, nearArc, farArc, o0 - off.Hp);
        Check(dryHit > 20f && Math.Abs(nearArc - dryHit * Game.WetArcShare) < 0.5f && Math.Abs(farArc - nearArc) < 0.5f, "Arc Lightning arcs to every wet enemy on the map for 40% of its hit, however far");
        Check(off.Hp == o0, "a dry enemy that wasn't in the strip takes nothing");
        float again = near.Hp;
        volley.Invoke(g, new object[] { me, me.Aim, 0 });
        Check(near.Hp == again, "a wet enemy can't be arced again within 0.4 s");
        step(0.5f);
        me.Aim = MathX.Pi;  // (stepping turned me back to the cursor)
        again = near.Hp;
        volley.Invoke(g, new object[] { me, me.Aim, 0 });
        Check(near.Hp < again, "...but can be a moment later");

        // It wears off, and the slow with it.
        step(Game.WetTime + 1f);
        Check(near.WetT == 0f && near.SlowMult == 1f, "wet wears off after 8 s, and so does the slow");

        // Mastered: wet lasts 14 s, and the wave rolls back wetting what it missed (no damage on the way back).
        var late = pin(new V2(350f, 450f));
        me.Aim = 0f;
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        masteredField.SetValue(g, false);
        step(0.3f);
        var behind = pin(new V2(260f, 450f));  // appears behind the wave's front: the first pass won't touch it
        step(0.4f);
        float lateHit = late.MaxHp - late.Hp;
        Check(lateHit > 5f && near.WetT > Game.WetTime, "a mastered Tsunami's wet lasts 14 s");
        behind.Hp = behind.MaxHp; behind.WetT = 0f;
        step(4.5f);
        Check(behind.WetT > Game.WetTime && behind.MaxHp - behind.Hp < 0.5f, "the mastered wave rolls back and wets what it missed");
        // Bullet Rain (the Elementalist DPS ultimate): enemies in its zone are wet and hurt, and it wears off 3 s after they leave.
        var addZone = typeof(Game).GetMethod("AddZone", flags);
        g.Enemies.Clear(); spots.Clear();
        var soaked = pin(new V2(900f, 450f)); var outside = pin(new V2(300f, 150f));
        addZone.Invoke(g, new object[] { ZoneKind.BulletRain, me, new V2(900f, 450f), Game.BulletRainRadius, 2f });
        step(1f);
        Check(soaked.WetT > 2f && soaked.MaxHp - soaked.Hp > 30f && outside.WetT == 0f && outside.Hp == outside.MaxHp, "Bullet Rain wets and hurts what is in its zone and leaves the rest dry");
        step(1.2f + Game.BulletRainWetTime + 0.5f);
        Check(soaked.WetT == 0f, "the rain's wet wears off a few seconds after the zone ends");
        host.Leave("");
    }
}
