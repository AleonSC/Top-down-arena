using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Steam (Steam.cs): a wet, burning enemy turns to steam (both used up), the cloud scalds and jams what is in it a second steam nearby refreshes the cloud, the cloud ends after 3 s, and the other screen sees it.
static partial class Program
{
    static void SteamTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
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
        Func<int> clouds = () => g.Zones.Values.Count(z => z.Kind == ZoneKind.Steam);

        var lit = pin(EnemyType.Grunt, new V2(800f, 450f)); var beside = pin(EnemyType.Grunt, new V2(860f, 450f));
        var away = pin(EnemyType.Grunt, new V2(1200f, 450f));
        var wetOnly = pin(EnemyType.Grunt, new V2(400f, 300f));
        wetOnly.WetT = 5f;
        step(0.2f);
        Check(clouds() == 0 && wetOnly.WetT > 0f, "wet alone makes no steam");

        lit.WetT = 5f; lit.BurnT = 3f; lit.BurnDps = 1f; lit.BurnBy = me.Id;
        step(dt * 2);
        var cloud = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Steam);
        Check(cloud != null && cloud.Owner == me.Id && cloud.Pos.DistanceTo(lit.Pos) < 5f, "wet and burning at once turns to steam, credited to whoever lit it");
        Check(lit.WetT == 0f && lit.BurnT == 0f, "the wet and the burn are both used up");
        lit.WetT = 5f; lit.BurnT = 3f; lit.BurnBy = me.Id;
        step(dt * 2);
        Check(lit.WetT > 0f && clouds() == 1, "an enemy can't steam again within 1 s");
        lit.WetT = lit.BurnT = 0f;

        float b0 = beside.Hp, a0 = away.Hp;
        step(1f);
        float want = Game.SteamDps + Game.SteamDpsWave * g.Wave, took = b0 - beside.Hp;
        Console.WriteLine("  steam: an enemy inside takes {0:0.0} a second (want {1:0.0}), one outside {2:0.0}; jam {3:0.00}", took, want, a0 - away.Hp, beside.SmokeT);
        Check(Math.Abs(took - want) < want * 0.2f && away.Hp == a0, "enemies inside are scalded, enemies outside aren't");
        Check(beside.SmokeT > 0f && away.SmokeT == 0f, "enemies inside are jammed");
        step(0.3f);
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Steam), "the other screen sees the cloud");

        // A second steam close by refreshes the cloud instead of stacking another.
        float left = cloud.T;
        var near = pin(EnemyType.Grunt, new V2(830f, 450f));
        near.WetT = 5f; near.PoolT = 2f; near.BurnPool = 10f; near.BurnBy = me.Id;
        step(dt * 2);
        Check(clouds() == 1 && cloud.T > left + 0.5f, "steam near a cloud refreshes it instead of adding one");

        step(Game.SteamTime + 0.5f);
        Check(clouds() == 0, "the cloud ends after 3 s");
        host.Leave(""); client.Leave("");
    }
}
