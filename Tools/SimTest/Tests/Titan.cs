using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Titan (Tank Summoner ultimate): rises for 1.4 s without hitting or blocking, stomps at the nearest enemy, winds up
// 0.4 s before each slam (90 damage, 1.4 s apart), stays 17 s (27 s and 1.3x with the capstone) and leaves rubble.
static partial class Program
{
    static void TitanTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("CastTitan", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        foreach (bool cap in new[] { false, true })
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 1, Kit.Summoner);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            V2 home = new V2(400f, 450f);
            var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, home + new V2(330f, 0f), 0f, 0, false });
            dummy.Hp = dummy.MaxHp = 1e6f; dummy.SpawnT = 0f; dummy.FrozenT = 1e6f;
            float secs = 0f; bool slammed = false; var wind = new List<float>(); var hits = new List<float>();
            float last = dummy.Hp;
            Action<float> run = s =>
            {
                for (int f = 0; f < s * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (e != dummy) e.Hp = 0f;
                    me.Pos = home - new V2(150f, 0f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(300f, 0f) }); g.Update(dt);
                    secs += dt;
                    var t = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Titan);
                    if (t != null && t.Wind > 0f && (wind.Count == 0 || secs - wind[wind.Count - 1] > 0.5f)) wind.Add(secs);
                    if (dummy.Hp < last - 60f) { hits.Add(secs); slammed = true; }  // companions nibble; only a slam is this big
                    last = dummy.Hp;
                }
            };
            run(0.2f);
            me.Aim = 0f;
            cast.Invoke(g, new object[] { me, cap });
            secs = 0f;
            var titan = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Titan);
            Check(titan != null, "the ultimate summons a titan");
            Check(Math.Abs(titan.Duration - (cap ? Game.TitanCapTime : Game.TitanTime)) < 0.01f, cap ? "the capstone titan stays 27 s" : "the titan stays 17 s");
            Check(Math.Abs(titan.Scale - (cap ? 1.3f : 1f)) < 0.01f, cap ? "the capstone titan is 1.3x" : "the titan is full size");
            Check(titan.Def.Radius >= 140f && titan.Def.BlockRadius >= 140f, "it is a colossus: 140 px");
            Check(!Game.TitanBlocking(titan), "it blocks nothing while it rises");
            run(Game.TitanRise - 0.2f);
            Check(!slammed, "it does not slam while it rises");
            run(5f);
            Console.WriteLine("  titan{0}: {1} slams, first hit {2:0.00} s, hits at {3}", cap ? " (capstone)" : "", hits.Count, hits.Count > 0 ? hits[0] : -1f, string.Join(" ", hits.Select(h => h.ToString("0.0"))));
            Check(slammed, "it walks to the enemy and slams it");
            Check(Game.TitanBlocking(g.Minions.Values.First(m => m.Kind == MinionKind.Titan)), "once risen it blocks bullets");
            Check(wind.Count >= 1 && hits.Count >= 1 && hits[0] > wind[0] + Game.TitanWindup - 0.05f, "a wind-up comes before the first slam lands");
            float dmg = 1e6f - dummy.Hp;
            Check(dmg >= 90f * hits.Count - 1f, "each slam does at least 90 damage");
            if (hits.Count >= 2) Check(hits[1] - hits[0] > 1.2f, "slams come about 1.4 s apart");
            run((cap ? Game.TitanCapTime : Game.TitanTime) + 1f);
            Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.Titan), "the titan crumbles when its time is up");
            host.Leave("");
        }
    }
}
