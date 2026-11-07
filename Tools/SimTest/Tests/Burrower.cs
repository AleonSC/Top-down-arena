using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Earthquake (Tank Summoner): a 25-damage slam round you, then a Burrower breaks out at the cursor, tunnels through the pack
// and bites each enemy it passes under once (30). Mastered: it also freezes what it bites.
static partial class Program
{
    static void BurrowerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        foreach (bool mastered in new[] { false, true })
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 1, Kit.Summoner);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            Check(me.Slots[0].Id == AbilityId.Earthquake, "the Summoner Tank's second Space ability is Earthquake");
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            V2[] spots = { new V2(380f, 450f), new V2(560f, 470f), new V2(650f, 430f), new V2(560f, 700f) };
            var dummies = new List<Enemy>();
            foreach (var sp in spots)
            {
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, sp, 0f, 0, false });
                e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; dummies.Add(e);
            }
            float maxFrozen = 0f;
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (!dummies.Contains(e)) e.Hp = 0f;
                    for (int i = 0; i < dummies.Count; i++) { dummies[i].Pos = spots[i]; maxFrozen = Math.Max(maxFrozen, dummies[i].FrozenT); }
                    foreach (int id in g.Minions.Values.Where(m => m.Kind != MinionKind.Burrower).Select(m => m.Id).ToList()) g.Minions.Remove(id);  // the Golem would hit the dummies too
                    me.Pos = new V2(300f, 450f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(500f, 450f) }); g.Update(dt);
                }
            };
            run(0.2f);
            if (mastered) me.MasteryXp[AbilityId.Earthquake] = Game.MasteryXpFor[Game.MasteryMax - 1];
            float[] hp0 = dummies.Select(e => e.Hp).ToArray();
            maxFrozen = 0f;
            cast.Invoke(g, new object[] { 1, 0, new V2(500f, 450f), false });
            var bur = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Burrower);
            Check(bur != null && bur.Pos.DistanceTo(new V2(500f, 450f)) < 1f, "the Burrower breaks out at the cursor");
            float slam = hp0[0] - dummies[0].Hp;
            Check(slam > 20f && slam < 40f, "the slam hurts an enemy within 140 px (" + slam.ToString("0") + ")");
            Check(dummies[1].Hp == hp0[1], "enemies out of the slam's reach aren't hurt yet");
            run(0.9f);
            Check(dummies[1].Hp == hp0[1] && dummies[2].Hp == hp0[2], "the Burrower hurts nothing while it is breaking out");
            run(3.5f);
            float mult = mastered ? 1.5f : 1.15f;  // mastery adds some damage
            float[] lost = dummies.Select((e, i) => hp0[i] - e.Hp - (i == 0 ? slam : 0f)).ToArray();
            Console.WriteLine("  burrower{0}: bites {1}, froze for up to {2:0.0} s", mastered ? " (mastered)" : "", string.Join(" / ", lost.Select(x => x.ToString("0"))), maxFrozen);
            Check(lost[1] > 25f && lost[2] > 25f, "it bites the enemies in its path");
            Check(lost.All(x => x < 1f || (x > 25f && x < 30f * mult)), "it bites each enemy it passes under once (30 damage, no repeats)");
            Check(lost.Count(x => x > 25f) >= 3, "it keeps steering at the nearest enemy it hasn't bitten");
            Check(mastered ? maxFrozen > 0.5f : maxFrozen < 0.1f, mastered ? "a mastered Earthquake's bite freezes" : "an unmastered bite doesn't freeze");
            run(Game.BurrowerTime);
            Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.Burrower), "the Burrower is gone after its time");
            host.Leave("");
        }
    }
}
