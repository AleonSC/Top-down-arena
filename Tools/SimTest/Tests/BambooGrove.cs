using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Bamboo Grove (BambooGrove.cs), the Tank Ninja's 4th choice on the lobby's 4th key: the table and cards, the pick reaching
// the run and both screens, the real key path, the wind-up (nothing lands at the cast), the wave reaching the near enemy
// before the far one, 40 and a stun, the bleed only once the stun ends, nothing past the radius, heavy enemies stunned
// for less, the fence holding enemies out and stopping enemy shots, mastery (a 6 s fence) and Rhizome (+30% radius).
static partial class Program
{
    static void BambooGroveTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.BambooGrove);
        Check(def.Name == "Bamboo Grove" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Ninja && def.Role == Role.Tank && def.Cooldown == 14f,
            "Bamboo Grove: a 14 s Tank Ninja 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Ninja) == 4 && Upgrades.FirstAbility(Role.Tank, Kit.Ninja, 3) == AbilityId.BambooGrove
            && Upgrades.FirstAbility(Role.Tank, Kit.Ninja, 0) == AbilityId.ShadowClone && Upgrades.FirstAbility(Role.Tank, Kit.Ninja, 2) == AbilityId.CeremonialArmor,
            "the Tank Ninja's 4th key is Bamboo Grove; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.BambooGrove) && Upgrades.All.Any(u => u.Name == "Rhizome" && u.RequiresAbility == AbilityId.BambooGrove && u.Rarity == Rarity.Rare),
            "Bamboo Grove is a very rare card for Tank Ninjas who picked another, and Rhizome a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Grower", Role.Tank, 0, 3, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.BambooGrove && client.Game.Players[1].Slots[0].Id == AbilityId.BambooGrove, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        var hold = new Dictionary<int, V2>();  // enemies pinned in place (the fence test lets one walk)
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; e.Pos = at; mine.Add(e.Id); hold[e.Id] = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; if (hold.ContainsKey(e.Id)) e.Pos = hold[e.Id]; }
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(100f, 0f) });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Action reset = () =>
        {
            mine.Clear(); hold.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            step(Game.BgFenceStart + Game.BgFenceMastered + 0.5f);  // any grove from before is over
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Pos = home; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.BambooGrove); me.Stats.BgRadiusMult = 1f; me.Stats.BgBleedMult = 1f;
        };
        var heavyType = HeavyFoe();

        // The real key path: a press casts it; nothing lands during the wind-up.
        reset();
        var near = make(home + new V2(40f, 0f), EnemyType.Grunt);
        var far = make(home + new V2(-125f, 0f), EnemyType.Grunt);
        var outside = make(home + new V2(0f, 230f), EnemyType.Grunt);
        var heavy = make(home + new V2(0f, -90f), heavyType);
        step(dt);
        foreach (var e in new[] { near, far, outside, heavy }) e.Hp = e.MaxHp;
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = home + new V2(100f, 0f) });
        Check(g.Effects.Count(f => f.Kind == FxKind.BambooGrove) == 1, "pressing the ability key casts it: one grove effect");
        step(0.2f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.BambooGrove) == 1, "the other screen sees the grove");
        Check(lost(near) == 0f && lost(far) == 0f, "nothing lands during the 0.35 s wind-up");
        step(0.2f);  // 0.4 s in: the front has reached the near one, not yet the far one
        Check(lost(near) >= 40f && lost(near) < 60f && near.StunT > 0f, "the wave reaches the near enemy first: 40 and a stun (" + lost(near).ToString("0") + ")");
        Check(lost(far) == 0f, "the far enemy isn't reached yet: the wave races outward");
        step(0.4f);
        Check(lost(far) >= 40f && far.StunT > 0f, "the wave reaches the far edge (" + lost(far).ToString("0") + ")");
        Check(lost(outside) == 0f && outside.StunT <= 0f, "nothing past the radius");
        Check(heavy.StunT > 0f && heavy.StunT < 0.6f && near.StunT > 0.6f, "a heavy enemy is stunned for less (" + heavy.StunT.ToString("0.00") + " s vs " + near.StunT.ToString("0.00") + " s)");
        Check(near.BleedT <= 0f, "no bleed while stunned");
        step(1.0f);
        Check(near.BleedT > 0f && near.BleedDps > 0f, "the bleed starts once the stun ends");
        float before = lost(near);
        step(1f);
        Check(lost(near) > before + 4f, "the bleed hurts (" + (lost(near) - before).ToString("0.0") + " in 1 s)");

        // The fence: an enemy walking in from outside is held there, and an enemy shot stops on it.
        reset();
        var walker = make(home + new V2(0f, 250f), EnemyType.Grunt);
        hold.Remove(walker.Id);
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(Game.BgFenceStart + 0.05f);
        for (int f = 0; f < 1.5f / dt; f++)
        {
            walker.Pos = walker.Pos + new V2(0f, -200f * dt);  // keeps pushing in
            step(dt);
        }
        Check(walker.Pos.DistanceTo(home) > Game.BgRadius, "the fence holds an enemy outside (" + walker.Pos.DistanceTo(home).ToString("0") + " px)");
        var stops = typeof(Game).GetMethod("GroveStops", flags);
        var shot = new Bullet { Pos = home + new V2(Game.BgRadius, 0f), Radius = 4f };
        var inside = new Bullet { Pos = home + new V2(60f, 0f), Radius = 4f };
        Check((bool)stops.Invoke(g, new object[] { shot }) && !(bool)stops.Invoke(g, new object[] { inside }), "an enemy shot on the fence's line stops; one well inside doesn't");
        step(Game.BgFence);
        Check(!(bool)stops.Invoke(g, new object[] { shot }), "the fence is down after 3.2 s");

        // Mastered: the fence stands 6 s.
        reset();
        me.MasteryXp[AbilityId.BambooGrove] = 99999f;
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(Game.BgFenceStart + 4.5f);
        Check((bool)stops.Invoke(g, new object[] { shot }), "mastered, the fence still stands 4.5 s in");

        // Rhizome: 30% farther.
        reset();
        me.Stats.BgRadiusMult = 1.3f;
        var edge = make(home + new V2(170f, 0f), EnemyType.Grunt);
        step(dt); edge.Hp = edge.MaxHp;
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(1.2f);
        Check(lost(edge) >= 40f, "Rhizome: the grove reaches an enemy 170 px out");
    }
}
