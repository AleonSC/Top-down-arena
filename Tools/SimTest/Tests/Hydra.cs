using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Hydra (Summoner DPS, replaces Sacrifice): rises at the cursor, its heads spit venom at different enemies.
static partial class Program
{
    static void HydraTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        foreach (bool mastered in new[] { false, true })
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 0, 1, Kit.Summoner);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            Check(me.Slots[0].Id == AbilityId.Hydra, "the Summoner DPS's second Space ability is Hydra");
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            var dummies = new List<Enemy>();
            V2[] spots = { new V2(560f, 300f), new V2(560f, 450f), new V2(560f, 600f), new V2(640f, 450f) };
            foreach (var sp in spots)
            {
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, sp, 0f, 0, false });
                e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; dummies.Add(e);
            }
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (!dummies.Contains(e)) e.Hp = 0f;
                    for (int i = 0; i < dummies.Count; i++) { dummies[i].Pos = spots[i]; dummies[i].FrozenT = 1f; }
                    me.Pos = new V2(300f, 450f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(300f, 450f) }); g.Update(dt);
                }
            };
            run(0.2f);
            if (mastered) me.MasteryXp[AbilityId.Hydra] = Game.MasteryXpFor[Game.MasteryMax - 1];
            cast.Invoke(g, new object[] { 1, 0, new V2(400f, 450f), false });
            var hydra = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Hydra);
            Check(hydra != null && Game.HydraHeads(hydra) == (mastered ? 4 : 3), mastered ? "a mastered hydra has four heads" : "a hydra has three heads");
            Check(hydra != null && hydra.Pos.DistanceTo(new V2(400f, 450f)) < 1f, "the hydra rises at the cursor");
            float[] hp0 = dummies.Select(e => e.Hp).ToArray();
            run(3f);
            int hurt = 0;
            for (int i = 0; i < dummies.Count; i++) if (dummies[i].Hp < hp0[i] - 5f) hurt++;
            Console.WriteLine("  hydra{0}: {1} of 4 enemies hit in 3 s, {2:0} damage in total", mastered ? " (mastered)" : "", hurt, hp0.Sum() - dummies.Sum(e => e.Hp));
            Check(hurt >= (mastered ? 4 : 3), "every head spits at a different enemy");
            Check(hp0.Sum() - dummies.Sum(e => e.Hp) > 3f * 10f * (mastered ? 4f : 3f) / 0.6f * 0.5f, "the hydra deals its damage");
            run(9f);
            Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.Hydra), "the hydra sinks after its time");
            host.Leave("");
        }
    }
}
