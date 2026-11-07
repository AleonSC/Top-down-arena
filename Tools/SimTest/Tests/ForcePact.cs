using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Force Pact (Tank Summoner, replaces Guardian Spirit): turns the enemy nearest your cursor into a thrall that fights near you,
// falls and comes back, and moves to another type when you cast again. Mastered: two thralls.
static partial class Program
{
    static void ForcePactTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        foreach (bool mastered in new[] { false, true })
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 2, Kit.Summoner);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            Check(me.Slots[0].Id == AbilityId.GuardianSpirit && Upgrades.Ability(AbilityId.GuardianSpirit).Name == "Force Pact", "the Summoner Tank's third Space ability is Force Pact");
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            var keep = new List<Enemy>();
            Func<EnemyType, V2, Enemy> make = (type, pos) =>
            {
                var e = (Enemy)spawn.Invoke(g, new object[] { type, pos, 0f, 0, false });
                e.Hp = e.MaxHp = type == EnemyType.Boss ? 1e5f : 1e5f; e.SpawnT = 0f; e.FrozenT = 1e3f; keep.Add(e); return e;
            };
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                    me.Pos = new V2(300f, 450f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(300f, 450f) }); g.Update(dt);
                }
            };
            Func<int, Minion> thrall = slot => g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Thrall && m.PactSlot == slot);
            Func<int> thralls = () => g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Thrall);
            run(0.2f);
            if (mastered) me.MasteryXp[AbilityId.GuardianSpirit] = Game.MasteryXpFor[Game.MasteryMax - 1];
            string tag = mastered ? " (mastered)" : "";

            // A boss on its own can't be taken.
            var boss = make(EnemyType.Boss, new V2(700f, 300f));
            me.Slots[0].Timer = 0f; cast.Invoke(g, new object[] { 1, 0, boss.Pos, false });
            run(1.5f);
            Check(thralls() == 0 && g.Enemies.ContainsKey(boss.Id), "a boss can't be taken");
            keep.Remove(boss); g.Enemies.Remove(boss.Id);

            // Cast on a Brute: it leaves the wave and comes back as your thrall after the chains.
            var brute = make(EnemyType.Brute, new V2(600f, 450f));
            int bruteId = brute.Id;
            me.Slots[0].Timer = 0f; cast.Invoke(g, new object[] { 1, 0, new V2(610f, 450f), false });
            Check(!g.Enemies.ContainsKey(bruteId), "the pact takes the enemy out of the wave");
            run(0.4f);
            Check(thralls() == 0, "it is still binding");
            run(ForcePactBindTime());
            var t0 = thrall(0);
            Check(t0 != null && t0.Pact == (byte)EnemyType.Brute, "then a Brute thrall stands where it was");
            Check(t0 != null && Math.Abs(t0.MaxHp - (Defs.Get(EnemyType.Brute).Hp * 2.5f + 15f * g.Wave)) < 0.5f, "its health is the type's 2.5x plus 15 a wave");

            // It fights enemies near you, not ones far away.
            var near = make(EnemyType.Grunt, new V2(420f, 450f));
            var far = make(EnemyType.Grunt, new V2(1500f, 450f));
            run(4f);
            Console.WriteLine("  thrall{0}: {1:0} damage to the enemy near you, {2:0} to the one far away", tag, near.MaxHp - near.Hp, far.MaxHp - far.Hp);
            Check(near.MaxHp - near.Hp > 20f && far.Hp == far.MaxHp, "it fights what is near you and doesn't wander off");
            keep.Remove(near); keep.Remove(far); g.Enemies.Remove(near.Id); g.Enemies.Remove(far.Id);

            // Cast again on a Sniper.
            var sniper = make(EnemyType.Sniper, new V2(700f, 300f));
            me.Slots[0].Timer = 0f; cast.Invoke(g, new object[] { 1, 0, sniper.Pos, false });
            run(ForcePactBindTime() + 0.2f);
            if (mastered)
                Check(thrall(0) != null && thrall(0).Pact == (byte)EnemyType.Sniper && thrall(1) != null && thrall(1).Pact == (byte)EnemyType.Brute && thralls() == 2, "mastered, the pact keeps the last two types: a Sniper and the Brute");
            else
                Check(thralls() == 1 && thrall(0).Pact == (byte)EnemyType.Sniper, "casting again moves the pact to the new type; the old thrall is gone");

            // A thrall that falls is gone for 15 s, then comes back as the same type.
            var down = thrall(0);
            hurtGuard.Invoke(g, new object[] { down, 1e5f });
            run(0.2f);
            Check(thrall(0) == null && me.PactCd[0] > Game.GuardRespawn - 1f, "a fallen thrall is gone until its timer ends");
            run(1f);
            Check(me.PactRez > 0, "and its respawn ring shows");
            me.PactCd[0] = 0.2f;
            run(0.6f);
            Check(thrall(0) != null && thrall(0).Pact == (byte)EnemyType.Sniper, "then the same type returns");
            host.Leave("");
        }
    }

    static float ForcePactBindTime() => Game.ForcePactBind + 0.1f;
}
