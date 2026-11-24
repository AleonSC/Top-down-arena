using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Slowed vs Chilled (Chill.cs): a plain slow only slows (Slowed, never Chilled, never frozen); an ice hit slows and chills; the
// third chill freezes and lifts the slows; bosses are only slowed (shown chilled); steady ice adds a stack a second at most;
// Frost Nova's deep slow shows chilled, not frozen; heavies don't show slowed; the other screen sees both flags.
static partial class Program
{
    static void ChillTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var chill = typeof(Game).GetMethod("Chill", flags);
        var slow = typeof(Game).GetMethod("Slow", flags | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
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
        Func<EnemyType, V2, Enemy> pin = (type, at) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; spots[e] = at; return e; };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = new V2(200f, 850f); me.Hp = me.MaxHp; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(200f, 100f) }); client.Game.FixedStep(dt, new PlayerInput()); } };
        Func<Enemy, int, float, float, bool, bool> ice = (e, stacks, t, mult, gated) => (bool)chill.Invoke(g, new object[] { e, stacks, t, mult, gated, 0f });
        Func<Enemy, Enemy> seen = e => client.Game.Enemies[e.Id];

        // A plain slow: Slowed only.
        var plain = pin(EnemyType.Grunt, new V2(300f, 300f));
        slow.Invoke(g, new object[] { plain, 30f, 0.05f });  // even a very strong slow
        step(0.3f);
        Check(plain.SlowMult < 0.1f && plain.FrostChill == 0 && plain.FrozenT <= 0f, "a plain slow only slows: no chill, no freeze");
        Check(seen(plain).Slowed && !seen(plain).Chilled && !seen(plain).Frozen, "it shows Slowed, not Chilled or Frozen");

        // An ice hit: slowed and chilled; the third freezes and lifts the slow.
        var cold = pin(EnemyType.Grunt, new V2(500f, 300f));
        ice(cold, 1, 3f, 0.7f, false);
        step(0.3f);
        Check(cold.FrostChill == 1 && Math.Abs(cold.SlowMult - 0.7f) < 0.01f, "an ice hit slows and adds a chill");
        Check(seen(cold).Chilled && seen(cold).Slowed && seen(cold).FrostChill == 1, "it shows Chilled (with its pip) and Slowed");
        ice(cold, 1, 3f, 0.7f, false);
        bool froze = ice(cold, 1, 3f, 0.7f, false);
        Check(froze && cold.FrozenT > 2f && cold.FrostChill == 0 && cold.SlowMult == 1f && cold.SlowT2 == 0f, "the third chill freezes it and lifts its slows");
        step(0.3f);
        Check(seen(cold).Frozen && !seen(cold).Chilled && !seen(cold).Slowed, "frozen shows Frozen only");
        Check(!ice(cold, 1, 3f, 0.7f, false) && cold.FrostChill == 0, "ice on a frozen enemy adds nothing");

        // Bosses: slowed only, shown chilled.
        g.Enemies.Clear(); spots.Clear();
        var boss = pin(EnemyType.Grunt, new V2(700f, 300f));
        boss.Def = new EnemyDef { Name = boss.Def.Name, IsBoss = true, Radius = boss.Def.Radius, Speed = boss.Def.Speed, Hp = boss.Def.Hp };
        for (int k = 0; k < 4; k++) ice(boss, 1, 3f, 0.7f, false);
        step(0.3f);
        Check(boss.FrostChill == 0 && boss.FrozenT <= 0f && boss.IceSlowT > 0f, "a boss is only slowed by ice, never stacked or frozen");
        Check(seen(boss).Chilled, "...and shown chilled");

        // Steady ice: a stack a second at most.
        var steady = pin(EnemyType.Grunt, new V2(900f, 300f));
        ice(steady, 1, 0.3f, 0.6f, true); ice(steady, 1, 0.3f, 0.6f, true); ice(steady, 1, 0.3f, 0.6f, true);
        Check(steady.FrostChill == 1, "steady ice adds one stack a second at most");
        step(1.05f);
        ice(steady, 1, 0.3f, 0.6f, true);
        Check(steady.FrostChill == 2, "...and another a second later");

        // A Blizzard chills (gated); Frost Nova's deep slow shows chilled, not frozen.
        g.Enemies.Clear(); spots.Clear(); g.Zones.Clear();
        var inStorm = pin(EnemyType.Grunt, new V2(800f, 450f));
        typeof(Game).GetMethod("AddZone", flags).Invoke(g, new object[] { ZoneKind.Blizzard, me, new V2(800f, 450f), 120f, 10f });
        step(0.5f);
        Check(inStorm.FrostChill == 1, "a Blizzard chills what's in it");
        step(2.1f);
        Console.WriteLine("  blizzard: frozen {0:0.0} s after 2.6 s in it", inStorm.FrozenT);
        Check(inStorm.FrozenT > 0f, "...a stack a second, so it freezes on the third");
        g.Zones.Clear();
        var nova = pin(EnemyType.Grunt, new V2(400f, 600f));
        ice(nova, 1, 3f, 0.1f, false);  // as Frost Nova does (Minions.cs)
        step(0.3f);
        Check(seen(nova).Chilled && !seen(nova).Frozen, "a deep 0.1x ice slow shows chilled, not frozen");

        // Heavies shrug off movement slows: not shown slowed.
        var brute = pin(EnemyType.Grunt, new V2(1200f, 600f));
        brute.Def = new EnemyDef { Name = brute.Def.Name, Heavy = true, Radius = brute.Def.Radius, Speed = brute.Def.Speed, Hp = brute.Def.Hp };
        slow.Invoke(g, new object[] { brute, 5f, 0.5f });
        step(0.3f);
        Check(!seen(brute).Slowed, "a heavy enemy isn't shown slowed (it ignores movement slows)");
        host.Leave(""); client.Leave("");
    }
}
