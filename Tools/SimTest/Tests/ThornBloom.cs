using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Thorn Bloom (ThornBloom.cs), the DPS Hemomancer's 4th choice on the lobby's 4th key: the table and cards, the pick reaching
// the run, the real key path, the 12% cost, twelve needles on both screens, a needle landing on arrival (not at the cast) for
// 45 and a bleed stack and stopping on the first enemy, the tendril chain (3 jumps, 20% less each, never the same enemy
// twice), mastery (5 jumps) and Thornwork (+1 jump, +20%).
static partial class Program
{
    static void ThornBloomTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.ThornBloom);
        Check(def.Name == "Thorn Bloom" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Hemomancer && def.Role == Role.Dps && def.Cooldown == 8f && def.HealthCost == 0.12f,
            "Thorn Bloom: an 8 s DPS Hemomancer 1st ability on the 4th key, costing 12% health");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Hemomancer) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Hemomancer, 3) == AbilityId.ThornBloom
            && Upgrades.FirstAbility(Role.Dps, Kit.Hemomancer, 0) == AbilityId.ArterialSpray && Upgrades.FirstAbility(Role.Dps, Kit.Hemomancer, 2) == AbilityId.VeinRush,
            "the DPS Hemomancer's 4th key is Thorn Bloom; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.ThornBloom) && Upgrades.All.Any(u => u.Name == "Thornwork" && u.RequiresAbility == AbilityId.ThornBloom && u.Rarity == Rarity.Rare),
            "Thorn Bloom is a very rare card for DPS Hemomancers who picked another, and Thornwork a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Bleeder", Role.Dps, 0, 3, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.ThornBloom && client.Game.Players[1].Slots[0].Id == AbilityId.ThornBloom, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f), aimAt = home + new V2(300f, 0f);
        var mine = new HashSet<int>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.FrozenT = 99f; }
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Enemy[] row = null;
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Pos = home; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.ThornBloom); me.Stats.TbExtraJumps = 0; me.Stats.TbDmgMult = 1f;
            make(new V2(1500f, 120f));  // far off: the wave goes on
            // A row along the first needle's line: the needle stops on the first, the tendril chains down the rest, 120 px apart.
            row = Enumerable.Range(0, 6).Select(i => make(home + new V2(150f + 120f * i, 0f))).ToArray();
            step(dt);
            foreach (var e in row) e.Hp = e.MaxHp;
        };

        // The real key path: a press of the ability key casts it and pays 12%.
        reset();
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = aimAt });
        float paid = me.MaxHp - me.Hp;
        Check(Math.Abs(paid - me.MaxHp * 0.12f) < 0.6f, "pressing the ability key casts it and costs 12% of your max health (" + paid.ToString("0.0") + ")");
        Check(g.Effects.Count(f => f.Kind == FxKind.ThornNeedle) == 12 && g.Effects.Count(f => f.Kind == FxKind.ThornBloom) == 1, "twelve needles burst out");
        Check(lost(row[0]) == 0f, "nothing lands at the cast: the needles are still flying");
        step(0.15f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.ThornNeedle) == 12 && client.Game.Effects.Count(f => f.Kind == FxKind.ThornTendril) == 3,
            "the other screen sees the twelve needles and three tendril jumps");
        Check(lost(row[0]) >= 45f && lost(row[0]) < 45f * 1.5f && row[0].BleedStacks >= 1, "the needle lands on arrival: 45 (Blood Price on top) and a bleed stack (" + lost(row[0]).ToString("0") + ")");
        float back = me.Hp - (me.MaxHp - paid);
        Console.WriteLine("  thorn bloom: one needle struck, {0:0.0} health back", back);
        Check(Math.Abs(back - me.MaxHp * Game.TbNeedleHeal) < 0.3f, "each needle that strikes heals you 1% of your max health");
        step(0.8f);
        float l1 = lost(row[1]), l2 = lost(row[2]), l3 = lost(row[3]);
        Console.WriteLine("  thorn bloom: needle {0:0}, tendrils {1:0}, {2:0}, {3:0}, then {4:0}", lost(row[0]), l1, l2, l3, lost(row[4]));
        Check(l1 >= 30f && l1 < 30f * 1.5f && Math.Abs(l2 / l1 - 0.8f) < 0.03f && Math.Abs(l3 / l2 - 0.8f) < 0.03f, "the tendril chains on: 30, then 20% less a jump");
        Check(lost(row[4]) == 0f && lost(row[5]) == 0f, "3 jumps and no more; the row behind the first is never struck by the needle");
        var tendrils = g.Effects.Where(f => f.Kind == FxKind.ThornTendril).OrderBy(f => Game.TbTendrilDelay(f)).ToList();
        Check(tendrils.Count == 3 && tendrils[1].Pos.DistanceTo(tendrils[0].Pos2) < 1f && tendrils[2].Pos.DistanceTo(tendrils[1].Pos2) < 1f, "each jump starts where the last one landed");

        // Two needles on one enemy: each lands, but the tendrils never reach the same enemy twice.
        reset();
        var big = make(home + new V2(0f, 60f));
        big.Radius = 60f;  // wide enough for several needles
        foreach (var e in row) { mine.Remove(e.Id); g.Enemies.Remove(e.Id); }
        var near = make(home + new V2(-100f, 150f));
        hostAbility.Invoke(g, new object[] { 1, 0, aimAt, false });
        step(1.2f);
        int needlesIn = g.Effects.Count(f => f.Kind == FxKind.ThornNeedle && f.Radius > 0.5f);
        Check(needlesIn >= 2 && lost(big) >= 45f * needlesIn * 0.99f, "several needles can land on one big enemy (" + needlesIn + ")");
        Check(g.Effects.Count(f => f.Kind == FxKind.ThornTendril && f.Pos2.DistanceTo(near.Pos) < 1f) == 1, "the tendrils reach the nearby enemy once, not once a needle");

        // Mastered: 5 jumps.
        reset();
        me.MasteryXp[AbilityId.ThornBloom] = 99999f;
        hostAbility.Invoke(g, new object[] { 1, 0, aimAt, false });
        step(1.4f);
        Check(lost(row[4]) > 0f && lost(row[5]) > 0f, "mastered, the tendril jumps 5 times");

        // Thornwork: one more jump and 20% harder.
        reset();
        me.Stats.TbExtraJumps = 1; me.Stats.TbDmgMult = 1.2f;
        hostAbility.Invoke(g, new object[] { 1, 0, aimAt, false });
        step(1.2f);
        Check(lost(row[4]) > 0f && lost(row[5]) == 0f && lost(row[0]) >= 54f, "Thornwork: one more jump, and the needle hits 20% harder");
    }
}
