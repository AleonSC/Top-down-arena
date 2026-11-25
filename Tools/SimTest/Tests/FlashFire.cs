using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Flash Fire (FlashFire.cs), the DPS Summoner's 4th choice on the lobby's 4th key: the table and cards, the pick reaching
// the run and both screens, the real key path, the kindle (nothing lands at the cast), the ring reaching the near enemy
// before the far one, 40 and burning and slowed, nothing past the radius, heavy enemies slowed less, the pyre catching an
// enemy that walks onto it and ending on time, mastery (a 5 s pyre) and Backdraft (+25% radius, hotter burn).
static partial class Program
{
    static void FlashFireTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.FlashFire);
        Check(def.Name == "Flash Fire" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Summoner && def.Role == Role.Dps && def.Cooldown == 14f,
            "Flash Fire: a 14 s DPS Summoner 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Summoner) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 3) == AbilityId.FlashFire
            && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 0) == AbilityId.BatSwarm && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 2) == AbilityId.AlphaCall,
            "the DPS Summoner's 4th key is Flash Fire; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.FlashFire) && Upgrades.All.Any(u => u.Name == "Backdraft" && u.RequiresAbility == AbilityId.FlashFire && u.Rarity == Rarity.Rare),
            "Flash Fire is a very rare card for DPS Summoners who picked another, and Backdraft a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Pyro", Role.Dps, 0, 3, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.FlashFire && client.Game.Players[1].Slots[0].Id == AbilityId.FlashFire, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        var hold = new Dictionary<int, V2>();  // enemies pinned in place (the pyre test lets one walk)
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
                g.Minions.Clear();  // no wolves biting: only the fire hurts
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(100f, 0f) });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Action reset = () =>
        {
            mine.Clear(); hold.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            step(Game.FfPyreStart + Game.FfPyreMastered + 0.5f);  // any fire from before is over
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Pos = home; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.FlashFire); me.Stats.FfRadiusMult = 1f; me.Stats.FfBurnMult = 1f;
        };
        EnemyType heavyType = EnemyType.Grunt;
        foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) { if ((int)et >= Defs.Enemies.Length) continue; var d = Defs.Get(et); if (d != null && d.Heavy && !d.IsBoss) { heavyType = et; break; } }

        // The real key path: a press casts it; nothing lands during the kindle.
        reset();
        var near = make(home + new V2(30f, 0f), EnemyType.Grunt);
        var far = make(home + new V2(-140f, 0f), EnemyType.Grunt);
        var outside = make(home + new V2(0f, 240f), EnemyType.Grunt);
        var heavy = make(home + new V2(0f, -100f), heavyType);
        step(dt);
        foreach (var e in new[] { near, far, outside, heavy }) e.Hp = e.MaxHp;
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = home + new V2(100f, 0f) });
        Check(g.Effects.Count(f => f.Kind == FxKind.FlashFire) == 1, "pressing the ability key casts it: one fire effect");
        step(0.2f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.FlashFire) == 1, "the other screen sees the fire");
        Check(lost(near) == 0f && lost(far) == 0f, "nothing lands during the 0.3 s kindle");
        step(0.12f);  // just past the kindle: the ring has reached the near one, not yet the far one
        Check(lost(near) >= 40f && lost(near) < 50f && near.BurnT > 0f && near.SlowT > 0f && near.SlowMult < 0.65f,
            "the ring reaches the near enemy first: 40, burning and slowed 40% (" + lost(near).ToString("0") + ")");
        Check(lost(far) == 0f, "the far enemy isn't reached yet: the ring rolls outward");
        step(0.3f);
        Check(lost(far) >= 40f && far.BurnT > 0f, "the ring reaches the far edge (" + lost(far).ToString("0") + ")");
        Check(lost(outside) == 0f && outside.BurnT <= 0f && outside.SlowT <= 0f, "nothing past the radius");
        Check(heavy.SlowT > 0f && heavy.SlowMult > 0.75f, "a heavy enemy is slowed less (x" + heavy.SlowMult.ToString("0.00") + ")");
        float before = lost(near);
        step(1f);
        Check(lost(near) > before + 4f, "the burn hurts (" + (lost(near) - before).ToString("0.0") + " in 1 s)");

        // The pyre: an enemy walking onto the edge is set burning; it ends after 3 s.
        reset();
        var walker = make(home + new V2(0f, 260f), EnemyType.Grunt);
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(Game.FfPyreStart + 0.5f);
        Check(walker.BurnT <= 0f, "an enemy outside isn't burning");
        hold[walker.Id] = home + new V2(0f, Game.FfRadius + 4f);
        step(0.1f);
        Check(walker.BurnT > 0f && walker.SlowT > 0f && lost(walker) < 1f, "stepping onto the pyre sets it burning and slowed (no flash damage)");
        var late = make(home + new V2(Game.FfRadius, 0f), EnemyType.Grunt);
        step(Game.FfPyre + 0.5f);
        Check(late.BurnT <= Game.FfBurnTime - 0.5f, "the pyre is out after 3 s (the late enemy's burn is running down)");

        // Mastered: the pyre stands 5 s.
        reset();
        me.MasteryXp[AbilityId.FlashFire] = 99999f;
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(Game.FfPyreStart + 4.2f);
        var latecomer = make(home + new V2(0f, -Game.FfRadius), EnemyType.Grunt);
        step(0.1f);
        Check(latecomer.BurnT > 0f, "mastered, the pyre still burns 4.2 s in");

        // Backdraft: 25% farther, the burn 50% hotter.
        reset();
        me.Stats.FfRadiusMult = 1.25f; me.Stats.FfBurnMult = 1.5f;
        var edge = make(home + new V2(190f, 0f), EnemyType.Grunt);
        step(dt); edge.Hp = edge.MaxHp;
        hostAbility.Invoke(g, new object[] { 1, 0, home, false });
        step(0.7f);
        Check(lost(edge) >= 40f && edge.BurnDps > Game.FfBurnDps * 1.4f, "Backdraft: the fire reaches an enemy 190 px out and burns hotter");
    }
}
