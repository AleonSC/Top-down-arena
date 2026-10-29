using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Soul Link (Soldier Healer): with an ally it tethers to them; alone it makes a Soul Ghost to link to.
static partial class Program
{
    static void SoulGhostTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Healer, 0, 2, Kit.Soldier);
        var g = host.Game;
        g.Bot = false;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Slots[0].Id == AbilityId.SoulLink, "the Soldier Healer's third Space ability is Soul Link");
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(650f, 450f), 0f, 0, false });
        dummy.Hp = dummy.MaxHp = 1e5f; dummy.SpawnT = 0f;
        Action<float> run = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                ((List<EnemyType>)queueField.GetValue(g)).Clear();
                foreach (var e in g.Enemies.Values) if (e != dummy) e.Hp = 0f;  // just the one enemy, and the wave goes on
                dummy.Pos = new V2(650f, 450f); dummy.FrozenT = 1f;
                me.Pos = new V2(300f, 450f);
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(300f, 100f) }); g.Update(dt);
            }
        };
        run(0.2f);
        cast.Invoke(g, new object[] { 1, 0, me.Pos, false });
        Func<Minion> Ghost = () => g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.SoulGhost);
        var ghost = Ghost();
        Check(ghost != null && me.LinkT > 0f && ghost.MaxHp > me.MaxHp * 0.4f, "alone, Soul Link makes a Soul Ghost of you to link to");
        float dummyHp = dummy.Hp;
        run(2f);
        Console.WriteLine("  soul ghost: {0:0} damage to an enemy in 2 s; {1:0} px from you", dummyHp - dummy.Hp, ghost.Pos.DistanceTo(me.Pos));
        Check(dummy.Hp < dummyHp - 40f && ghost.Pos.DistanceTo(me.Pos) < 90f, "the ghost keeps beside you and shoots enemies");
        float h0 = ghost.Hp;
        hurtGuard.Invoke(g, new object[] { ghost, 40f });
        float took = h0 - ghost.Hp;
        float h1 = ghost.Hp;
        run(1f);
        Console.WriteLine("  soul ghost: took {0:0} of a 40 hit; the link healed it {1:0.0} in 1 s", took, ghost.Hp - h1);
        Check(Math.Abs(took - 20f) < 0.01f && ghost.Hp > h1 + 5f, "the link halves the damage the ghost takes and heals it");
        run(Upgrades.Ability(AbilityId.SoulLink).Cooldown);
        Check(Ghost() == null && me.LinkT <= 0f, "the ghost fades when the link ends");
        host.Leave("");

        // With an ally, Soul Link tethers to them as before: no ghost.
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 2);
        var hg = s[0].Game;
        var healer = hg.Players[1];
        healer.Slots[0].Id = AbilityId.SoulLink; healer.Slots[0].Charges = 1;
        cast.Invoke(hg, new object[] { 1, 0, healer.Pos, false });
        Check(healer.LinkTo == 2 && !hg.Minions.Values.Any(m => m.Kind == MinionKind.SoulGhost), "with an ally, Soul Link tethers to them (no ghost)");
        s[0].Leave(""); s[1].Leave("");
    }
}
