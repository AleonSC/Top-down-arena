using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Wind reactions (Wind.cs): Wind Chill (wet + wind: chill, a third freezes, then a while with no wind chill), Fan the Flames
// (burning + wind: the burn spreads at 60% to the nearest three downwind, never upwind), once a second per enemy, nothing on a
// dry, unlit enemy, and a real source (a Gust Gate) reacting.
static partial class Program
{
    static void WindTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var wind = typeof(Game).GetMethod("WindHit", flags);
        var host = HostAs(new Session { AutoStart = 1 }, nextPort++, false, "Solo", Role.Healer, 1, 1, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        g.Bot = false;
        g.Enemies.Clear(); g.Bullets.Clear(); g.Zones.Clear();
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; spots[e] = at; return e; };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = new V2(100f, 850f); me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(100f, 100f) }); } };
        Action<Enemy, V2> blow = (e, dir) => wind.Invoke(g, new object[] { e, dir });

        // Dry and unlit: nothing.
        var plain = pin(new V2(300f, 300f));
        blow(plain, new V2(1f, 0f));
        Check(plain.FrostChill == 0 && plain.WindCd == 0f && plain.SlowMult == 1f, "wind on a dry, unlit enemy does nothing");

        // Wind Chill.
        var wet = pin(new V2(600f, 300f));
        wet.WetT = 30f;
        blow(wet, new V2(1f, 0f));
        Check(wet.FrostChill == 1 && Math.Abs(wet.SlowMult - (1f - Game.FlChillSlow)) < 0.01f, "wind on a wet enemy chills it (+1 chill, the 30% slow)");
        blow(wet, new V2(1f, 0f));
        Check(wet.FrostChill == 1, "an enemy reacts to wind at most once a second");
        step(1.05f); blow(wet, new V2(1f, 0f));
        step(1.05f); blow(wet, new V2(1f, 0f));
        Console.WriteLine("  wind chill: frozen {0:0.0} s, chill {1}, immune {2:0.0} s", wet.FrozenT, wet.FrostChill, wet.WindChillImmuneT);
        Check(wet.FrozenT > 2f && wet.FrostChill == 0 && wet.WindChillImmuneT > 5f, "the third wind chill freezes it");
        step(3f);
        Check(wet.WetT > 0f, "it is still wet after it thaws");
        blow(wet, new V2(1f, 0f));
        Check(wet.FrostChill == 0, "after a wind freeze, wind can't chill it again for a while");

        // Fan the Flames.
        g.Enemies.Clear(); spots.Clear();
        var fire = pin(new V2(800f, 450f));
        var d1 = pin(new V2(860f, 450f)); var d2 = pin(new V2(850f, 520f)); var d3 = pin(new V2(890f, 400f)); var d4 = pin(new V2(910f, 450f));
        var up = pin(new V2(740f, 450f)); var far = pin(new V2(1100f, 450f));
        fire.BurnT = 3f; fire.BurnDps = 20f; fire.BurnBy = me.Id;
        blow(fire, new V2(1f, 0f));
        var lit = new[] { d1, d2, d3, d4 }.Count(e => e.BurnT > 0f);
        Console.WriteLine("  fan: downwind lit {0} of 4 (dps {1:0.0}), upwind {2:0.0}, far {3:0.0}", lit, d1.BurnDps, up.BurnT, far.BurnT);
        Check(lit == Game.FanTargets && d4.BurnT == 0f, "wind on a burning enemy spreads its fire to the three nearest downwind");
        Check(Math.Abs(d1.BurnDps - 20f * Game.FanShare) < 0.01f && d1.BurnBy == me.Id && Math.Abs(d1.BurnT - 3f) < 0.01f, "at 60% strength, for the time it had left, credited to whoever lit it");
        Check(up.BurnT == 0f && far.BurnT == 0f, "never upwind, never beyond 120 px");

        // A real source: a Gust Gate's steady push.
        g.Enemies.Clear(); spots.Clear(); g.Zones.Clear();
        var soaked = pin(new V2(500f, 600f));
        soaked.WetT = 30f;
        typeof(Game).GetMethod("AddZone", flags).Invoke(g, new object[] { ZoneKind.GustGate, me, new V2(500f, 600f), Game.GgRadius, 8f });
        step(0.2f);
        Check(soaked.FrostChill == 1, "a Gust Gate's wind chills a wet enemy in it");
        step(2.3f);
        Check(soaked.FrozenT > 0f || soaked.WindChillImmuneT > 0f, "...and keeps chilling it once a second until it freezes");
        host.Leave("");
    }
}
