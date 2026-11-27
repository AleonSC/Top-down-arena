using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Clot Crystals (ClotCrystals.cs), the Healer Hemomancer's 4th choice on the lobby's 4th key: the table and cards, the pick
// reaching the run and both screens, the real key path (15% health, three crystals round you), nothing eaten while they rise,
// a jerk at a shot in reach that eats it (its real damage off the crystal, you untouched), shots out of reach left alone, one
// crystal a shot, a worn-out crystal shattering, the other screen seeing the hit, the 12 s melt giving back 10% of what is
// left, mastery (a 4th crystal) and Hard Clot (+30% health, +3 s).
static partial class Program
{
    static void ClotCrystalsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        var def = Upgrades.Ability(AbilityId.ClotCrystals);
        Check(def.Name == "Clot Crystals" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Hemomancer && def.Role == Role.Healer
            && def.Cooldown == Game.CryCooldown && def.HealthCost == Game.CryCost, "Clot Crystals: a Healer Hemomancer 1st ability on the 4th key, 15% health");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Hemomancer) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Hemomancer, 3) == AbilityId.ClotCrystals
            && Enumerable.Range(0, 3).All(i => Upgrades.FirstAbility(Role.Healer, Kit.Hemomancer, i) != AbilityId.ClotCrystals),
            "the Healer Hemomancer's 4th key is Clot Crystals; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.ClotCrystals) && Upgrades.All.Any(u => u.Name == "Hard Clot" && u.RequiresAbility == AbilityId.ClotCrystals && u.Rarity == Rarity.Rare),
            "Clot Crystals is a card for Healer Hemomancers who picked another, and Hard Clot a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Clotter", Role.Healer, 0, 3, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.ClotCrystals && client.Game.Players[1].Slots[0].Id == AbilityId.ClotCrystals, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);
        foreach (var q in g.Players.Values) if (q != me) q.Pos = new V2(150f, 150f);

        V2 home = new V2(600f, 450f);
        var mine = new HashSet<int>();
        Action<float, bool> step = (secs, press) =>
        {
            for (int f = press ? -1 : 0; f < Math.Max(1, (int)Math.Round(secs / dt)); f++)  // (a press starts with a frame let go: the key is read on its way down)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                me.Pos = home;
                foreach (var q in g.Players.Values) if (q != me) q.Pos = new V2(150f, 150f);
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { Ability = press && f == 0, AimAt = home + new V2(100f, 0f) });
                g.Update(dt); client.Game.Update(dt);
            }
        };
        Func<V2, V2, Bullet> fire = (at, vel) => (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, at, vel, 0f, 6f, 10f, 0, false, 0f });
        Func<Game, List<Minion>> crystals = gm => gm.Minions.Values.Where(m => m.Kind == MinionKind.ClotCrystal).ToList();
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
            foreach (var m in crystals(g)) g.Minions.Remove(m.Id);
            var keeper = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, home + new V2(-450f, -330f), 0f, 0, false });  // keeps the wave going
            keeper.Hp = keeper.MaxHp = 1e6f; keeper.SpawnT = 0f; keeper.FireT = 99f; mine.Add(keeper.Id);
            step(dt, false);
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Hp = me.MaxHp; me.Downed = false;
            me.MasteryXp.Remove(AbilityId.ClotCrystals); me.Stats.CryHpMult = 1f; me.Stats.CryBonusTime = 0f;
        };

        // The real key path: 15% health, three crystals round you; the other screen sees them.
        reset();
        float hp0 = me.Hp;
        step(dt, true);
        var cs = crystals(g);
        Check(cs.Count == Game.CryCount && cs.All(m => m.Owner == me.Id && m.Pos.DistanceTo(home) < Game.CryRing + 2f), "pressing the ability key grows three crystals round you (" + cs.Count + ")");
        Check(Math.Abs(hp0 - me.Hp - me.MaxHp * Game.CryCost) < 1f, "it costs 15% of max health (" + (hp0 - me.Hp).ToString("0.0") + ")");
        Check(cs.All(m => m.MaxHp >= Game.CryHp * 0.99f && m.BlockHp == m.MaxHp), "each has 150 health (" + cs[0].MaxHp.ToString("0") + ")");
        Check(cs.All(m => Math.Abs(m.T - Game.CryLife) < 0.05f), "they last 12 s");

        // Nothing is eaten while they rise; a shot flying through a rising crystal's spot hits you.
        var early = fire(home + new V2(0f, -60f), new V2(0f, 0f));
        step(0.1f, false);
        Check(g.Bullets.ContainsKey(early.Id), "a rising crystal doesn't go for shots");
        g.Bullets.Clear();
        step(Game.CryRise, false);
        Check(client.Game.Minions.Values.Count(m => m.Kind == MinionKind.ClotCrystal) == Game.CryCount, "the other screen sees the crystals");
        Check(crystals(g).All(m => m.Stacks == Game.CryIdle), "risen, they wait round you");

        // A shot in reach: the nearest crystal shudders, jerks at it and eats it; you're untouched.
        float hpMe = me.Hp;
        var shot = fire(home + new V2(-60f, -110f), new V2(160f, 0f));  // crossing above you, 110-127 px out
        bool told = false, jerked = false;
        for (int i = 0; i < 40 && g.Bullets.ContainsKey(shot.Id); i++)
        {
            step(dt, false);
            told |= crystals(g).Any(m => m.Stacks == Game.CryTelling);
            jerked |= crystals(g).Any(m => m.Stacks == Game.CryJerking);
        }
        Check(!g.Bullets.ContainsKey(shot.Id), "a crystal eats a shot crossing within reach");
        Check(told && jerked, "it shudders, then jerks at it");
        var hurt = crystals(g).Where(m => m.BlockHp < m.MaxHp - 0.5f).ToList();
        float took = hurt.Count == 1 ? hurt[0].MaxHp - hurt[0].BlockHp : 0f;
        Check(hurt.Count == 1 && took >= 9.99f, "the shot's damage comes off that crystal (" + took.ToString("0.0") + ")");
        Check(me.Hp >= hpMe - 0.01f, "you're untouched");
        step(0.1f, false);
        var seen = client.Game.Minions.Values.Where(m => m.Kind == MinionKind.ClotCrystal && m.GuardFrac < 0.999f).ToList();
        Check(seen.Count == 1 && Math.Abs(seen[0].GuardFrac - hurt[0].BlockHp / hurt[0].MaxHp) < 0.01f, "the other screen sees its health drop");

        // A shot out of reach is left alone.
        step(0.3f, false);
        var far = fire(home + new V2(-100f, -200f), new V2(160f, 0f));
        step(0.6f, false);
        Check(g.Bullets.ContainsKey(far.Id) && crystals(g).All(m => m.Pos.DistanceTo(home) < Game.CryRing + 12f), "a shot 200 px out is left alone");
        g.Bullets.Clear();

        // One crystal a shot: two shots side by side, two crystals go.
        step(0.3f, false);
        var a = fire(home + new V2(-40f, 100f), new V2(150f, 0f));
        var b = fire(home + new V2(-40f, 118f), new V2(150f, 0f));
        int most = 0;
        bool shared = false;
        for (int i = 0; i < 40 && (g.Bullets.ContainsKey(a.Id) || g.Bullets.ContainsKey(b.Id)); i++)
        {
            step(dt, false);
            var ids = crystals(g).Where(m => m.CryBullet != 0).Select(m => m.CryBullet).ToList();
            shared |= ids.Count != ids.Distinct().Count();
            most = Math.Max(most, ids.Count);
        }
        Check(!shared, "no two crystals go for the same shot");
        Check(!g.Bullets.ContainsKey(a.Id) && !g.Bullets.ContainsKey(b.Id) && most == 2, "two shots, two crystals: both eaten (" + most + " at once)");

        // A worn-out crystal shatters.
        step(0.3f, false);
        foreach (var m in crystals(g)) m.BlockHp = 1f;
        var last = fire(home + new V2(-60f, -100f), new V2(160f, 0f));
        step(0.5f, false);
        Check(!g.Bullets.ContainsKey(last.Id) && crystals(g).Count == Game.CryCount - 1, "a crystal out of health shatters (" + crystals(g).Count + " left)");
        Check(g.Effects.Any(f => f.Kind == FxKind.ClotShatter), "the shatter shows");

        // At 12 s they melt back into you: 10% of the health they have left.
        reset();
        step(dt, true);
        foreach (var m in crystals(g)) m.BlockHp = 100f;
        step(Game.CryLife - 0.3f, false);
        Check(crystals(g).Count == Game.CryCount && crystals(g).All(m => m.Stacks == Game.CryMelting), "near the end they melt toward you");
        me.Hp = me.MaxHp * 0.5f;
        float before = me.Hp;
        step(0.5f, false);
        float back = me.Hp - before;
        Check(!crystals(g).Any(), "gone after 12 s");
        Check(Math.Abs(back - Game.CryCount * 100f * Game.CryRefund) < 3f, "melting back gives 10% of their health left (" + back.ToString("0.0") + ")");

        // Mastered: a 4th crystal.
        reset();
        me.MasteryXp[AbilityId.ClotCrystals] = 99999f;
        step(dt, true);
        Check(crystals(g).Count == Game.CryCountMastered, "mastered: four crystals");

        // Hard Clot: 30% more health and 3 s longer.
        reset();
        me.Stats.CryHpMult = 1.3f; me.Stats.CryBonusTime = 3f;
        step(dt, true);
        cs = crystals(g);
        Check(cs.All(m => m.MaxHp >= Game.CryHp * 1.3f * 0.99f && Math.Abs(m.T - (Game.CryLife + 3f)) < 0.05f), "Hard Clot: 30% more health and 3 s longer");
    }
}
