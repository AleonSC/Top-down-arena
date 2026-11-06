using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Vein Tithe (VeinTithe.cs): the tendrils to every enemy on screen, held enemies that can't move, the per-enemy health drain, 40% less
// damage dealt, the let-go at 30 s, going down, dead enemies dropping out, and the mastered length.
static partial class Program
{
    static void TitheTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Blood", Role.Tank, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var ally = g.Players[2];
        g.DevSetGod(2, true);
        me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodRite;
        var def = Upgrades.Ability(AbilityId.BloodRite);
        Check(def.Name == "Vein Tithe" && def.Cooldown == 75f && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer) == AbilityId.BloodRite, "the tank Hemomancer's ultimate is Vein Tithe, 75 s");

        V2 mePos = new V2(300f, 450f);
        var mine = new HashSet<int>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; mine.Add(e.Id); return e;
        };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id); g.Bullets.Clear(); me.Pos = mePos; ally.Pos = new V2(200f, 300f); host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        Action reset = () => { mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.TitheClock = 0f; me.Tithed.Clear(); me.Slots[Upgrades.UltSlot].Charges = 1; me.Pos = mePos; masteredField.SetValue(g, false); };
        Func<float, float> dealt = amount => { var t = make(new V2(1400f, 800f)); float h = t.Hp; hurt.Invoke(g, new object[] { t, amount, 1 }); float d = h - t.Hp; g.Enemies.Remove(t.Id); return d; };

        // Casting: every enemy on screen is held, except one still spawning.
        reset();
        var foes = new List<Enemy>();
        for (int i = 0; i < 5; i++) foes.Add(make(new V2(600f + i * 150f, 300f + (i % 2) * 300f)));
        var spawning = make(new V2(900f, 700f)); spawning.SpawnT = 5f;
        float normal = dealt(100f);
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        Check(me.UltT == Game.TitheLen && Game.TitheActive(me) && me.Tithed.Count == 5 && !me.Tithed.Contains(spawning.Id), "casting holds every enemy on screen (not one still spawning) for 30 s");
        Check(me.Tithed.Select(id => g.Enemies[id].Pos.DistanceTo(mePos)).SequenceEqual(me.Tithed.Select(id => g.Enemies[id].Pos.DistanceTo(mePos)).OrderBy(d => d)), "the tendrils reach the nearest first");
        float cut = dealt(100f);
        Console.WriteLine("  tithe: damage {0:0.0} -> {1:0.0}", normal, cut);
        Check(Math.Abs(cut / normal - Game.TitheOutputMult) < 0.01f, "you deal 40% less damage while it lasts");

        // Held enemies don't move or shoot.
        var before = foes.Select(e => e.Pos).ToList();
        step(3f);
        Check(foes.Select((e, i) => e.Pos.DistanceTo(before[i])).Max() < 1f && foes.All(e => e.GraspT > 0f), "held enemies stay where they are");
        Check(client.Game.Players[1].Tithed.Count == 5 && client.Game.Players[1].TitheClock > 1f, "other screens see the tendrils");

        // The drain: 0.25% of max health a second per enemy, after the tendrils have reached.
        float regen = g.PassiveRegen + me.Stats.Regen;
        float h0 = me.Hp = me.MaxHp * 0.9f;
        step(4f);
        float lost = h0 - me.Hp;
        float want = me.MaxHp * Game.TitheCost * 5f * 4f - regen * 4f;
        Console.WriteLine("  tithe: lost {0:0.0} in 4 s (expect about {1:0.0}); max {2:0}", lost, want, me.MaxHp);
        Check(Math.Abs(lost - want) < me.MaxHp * 0.01f + 1f, "five enemies cost 1.25% of max health a second");
        Check(me.InvulnT <= 0.01f, "the drain gives no hit grace");

        // A dead enemy drops out of the hold and the bill.
        foes[0].Hp = 0f;
        step(0.2f);
        Check(!me.Tithed.Contains(foes[0].Id) && me.Tithed.Count <= 4, "a dead enemy is let go and stops costing");

        // It lets go at 30 s: one snap each.
        step(Game.TitheLen - 7.2f + 0.4f);
        Check(!Game.TitheActive(me) && me.Tithed.Count == 0 && me.UltT <= 0f, "it ends when the 30 s are up");
        Check(g.Effects.Count(e => e.Kind == FxKind.TitheSnap) >= 4, "every held enemy gets a tendril letting go");
        me.Hp = me.MaxHp;
        float after = dealt(100f);
        Check(Math.Abs(after - normal) < 0.01f, "damage is back to normal");

        // Going down ends it at once, and the enemies are freed.
        reset();
        make(new V2(700f, 450f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        step(Game.TitheReach + 0.1f);
        me.Hp = 0.01f * me.MaxHp * 0.01f;
        step(1f);
        Check(me.Downed && !Game.TitheActive(me) && me.Tithed.Count == 0, "if the drain takes you down it ends");
        reset();

        // Bosses cost four times as much; mastered costs 25% less and lasts 40 s.
        make(new V2(800f, 450f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float one = g.TitheRate(me);
        Check(Math.Abs(one - Game.TitheCost) < 1e-6f, "one enemy costs 0.25% of max health a second");
        reset();
        make(new V2(800f, 450f));
        me.MasteryXp[AbilityId.BloodRite] = 1e6f;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        Check(me.UltT == Game.TitheLenMastered && Math.Abs(g.TitheRate(me) - Game.TitheCost * Game.TitheMasteredCost) < 1e-6f, "mastered it lasts 40 s and costs 25% less");
        me.MasteryXp.Remove(AbilityId.BloodRite);
        reset();
        Check(Game.TitheBossMult == 4f && Game.TitheMasteredCost == 0.75f, "a boss costs four times as much and mastered costs 25% less");
        Check(Game.TitheGrown(0f, 0) == 0f && Game.TitheGrown(Game.TitheReach, 5) >= 0.99f, "every tendril has fully grown by 1.2 s");
    }
}
