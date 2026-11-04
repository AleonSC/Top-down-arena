using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Soul Lantern (Summoner Healer): a lantern links to the player nearest your cursor, heals them and flames enemies.
// Deployable Medkit (Soldier Healer): thrown at the cursor, it bursts into healing orbs anyone can pick up.
static partial class Program
{
    static void HealerKitTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);

        // ---- Soul Lantern, alone: it links to you ----
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Healer, 0, 2, Kit.Summoner);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            Check(me.Slots[0].Id == AbilityId.SoulLantern, "the Summoner Healer's third Space ability is Soul Lantern");
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(480f, 450f), 0f, 0, false });
            dummy.Hp = dummy.MaxHp = 1e5f; dummy.SpawnT = 0f;
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (e != dummy) e.Hp = 0f;
                    dummy.Pos = new V2(480f, 450f); dummy.FrozenT = 1f;
                    me.Pos = new V2(300f, 450f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(300f, 100f) }); g.Update(dt);
                }
            };
            run(0.2f);
            me.Hp = me.MaxHp * 0.4f;
            cast.Invoke(g, new object[] { 1, 0, me.Pos, false });
            Func<Minion> Lantern = () => g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.SoulLantern);
            var lamp = Lantern();
            Check(lamp != null && lamp.LinkTo == 1, "alone, the lantern links to you");
            float hp0 = me.Hp, dummyHp = dummy.Hp;
            run(2f);
            Console.WriteLine("  soul lantern: healed you {0:0.0} and did {1:0} damage in 2 s; {2:0} px from you", me.Hp - hp0, dummyHp - dummy.Hp, lamp.Pos.DistanceTo(me.Pos));
            Check(me.Hp > hp0 + 10f, "the lantern heals its linked player");
            Check(dummy.Hp < dummyHp - 15f, "the lantern flames a nearby enemy");
            Check(lamp.Pos.DistanceTo(me.Pos) < 70f, "the lantern hovers by its player");
            run(6f);
            Check(Lantern() == null, "the lantern fades when the link ends");
            host.Leave("");
        }

        // ---- Soul Lantern with an ally: it links to the player nearest the cursor; mastered, they can't go down ----
        {
            var s = StartBuffed(dt, 1, 0);
            s[0].Game.Bot = s[1].Game.Bot = false;
            Run(s, dt, 2);
            var hg = s[0].Game;
            var healer = hg.Players[1];
            var ally = hg.Players[2];
            healer.Slots[0].Id = AbilityId.SoulLantern; healer.Slots[0].Charges = 1;
            cast.Invoke(hg, new object[] { 1, 0, ally.Pos, false });
            var lamp = hg.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.SoulLantern);
            Check(lamp != null && lamp.LinkTo == 2, "the lantern links to the ally nearest your cursor");
            Run(s, dt, 0.5f);
            var seen = s[1].Game.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.SoulLantern);
            Check(seen != null && seen.LinkTo == 2, "the link is replicated to the other player");
            lamp.Mastered = true;
            var damage = typeof(Game).GetMethod("Damage", flags, null, new[] { typeof(Player), typeof(float), typeof(string) }, null);
            ally.Hp = 5f; ally.InvulnT = 0f; ally.DevGod = false;
            damage.Invoke(hg, new object[] { ally, 500f, "Test" });
            Check(!ally.Downed && ally.Hp >= 1f, "a mastered lantern keeps its linked player standing");
            s[0].Leave(""); s[1].Leave("");
        }

        // ---- Deployable Medkit ----
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Healer, 0, 2, Kit.Soldier);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            var me = g.Players[1];
            g.DevSetGod(1, true);
            Check(me.Slots[0].Id == AbilityId.Medkit, "the Soldier Healer's third Space ability is the Deployable Medkit");
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) e.Hp = 0f;
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(700f, 450f) }); g.Update(dt);
                }
            };
            me.Pos = new V2(300f, 450f);
            run(0.1f);
            me.Hp = me.MaxHp * 0.3f;
            cast.Invoke(g, new object[] { 1, 0, new V2(600f, 450f), false });
            Check(g.Bullets.Values.Any(b => b.Kind == Defs.MedkitShot), "the medkit flies out toward the cursor");
            run(0.7f);
            var orbs = g.Orbs.Values.Where(o => o.Med).ToList();
            Check(orbs.Count == 6 && orbs.All(o => o.Heal == 15f), "it bursts into 6 healing orbs worth 15 each");
            Check(orbs.All(o => o.Pos.DistanceTo(new V2(600f, 450f)) < 90f), "the orbs land round where it did");
            float hp0 = me.Hp;
            me.Pos = orbs[0].Pos;
            run(0.3f);
            Console.WriteLine("  medkit: walking over the orbs healed {0:0.0}", me.Hp - hp0);
            Check(me.Hp >= hp0 + 14.9f && g.Orbs.Values.Count(o => o.Med) < 6, "walking over an orb picks it up and heals");
            run(7f);
            Check(!g.Orbs.Values.Any(o => o.Med), "unclaimed orbs vanish after 6 s");
            host.Leave("");
        }
    }
}
