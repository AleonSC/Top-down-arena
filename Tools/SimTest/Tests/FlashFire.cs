using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Flash Fire (FlashFire.cs), the DPS Elementalist's third ultimate on key 2 of the lobby's Ultimate row: the table and
// card, the pick reaching the run and both screens, the kindle (nothing lands at the cast), the ring reaching the near enemy
// before the far one (100), nothing past the radius, the ground hurting and slowing what's in it (heavy enemies less), the
// fire going out on time and leaving what's in it burning (not what walked out), mastery (11 s) and Backdraft.
static partial class Program
{
    static void FlashFireTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        var def = Upgrades.Ability(AbilityId.FlashFire);
        Check(def.Name == "Flash Fire" && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Kit == Kit.Elementalist && def.Role == Role.Dps && def.Cooldown == 80f,
            "Flash Fire is the DPS Elementalist's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 2) == AbilityId.FlashFire && Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 1) == AbilityId.LivingBolt
            && Upgrades.Ultimate(Role.Dps, Kit.Elementalist) == AbilityId.BulletRain && Upgrades.ClampUlt(Role.Dps, Kit.Elementalist, 2) == 2
            && Upgrades.AbilityLimit(Role.Healer, Kit.Summoner) == 3,
            "key 2 is Flash Fire; Bullet Rain and Living Bolt keep keys 0 and 1; the Healer Summoner keeps three abilities");
        Check(Upgrades.All.Any(u => u.Name == "Backdraft" && u.RequiresAbility == AbilityId.FlashFire && u.Rarity == Rarity.Rare),
            "Backdraft is a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 3, StartWave = 3 };
        hostS.Host(port, "Pyro", Role.Dps, 0, 0, Kit.Elementalist, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        Run(s, dt, 3, () => hostS.Lobby.Count == 2 && client.Lobby.Count == 2 && !hostS.Game.Running);
        Run(s, dt, 2, () => client.Lobby[1].Ult == 2);
        Check(hostS.Lobby[1].UltId == AbilityId.FlashFire && client.Lobby[1].Ult == 2, "the pick reaches the host and the other player");
        hostS.StartMatch();
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.FlashFire && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.FlashFire,
            "in the run the ultimate is Flash Fire, on both screens");
        foreach (var q in g.Players.Values) g.DevSetGod(q.Id, true);

        V2 home = new V2(600f, 500f);
        var mine = new HashSet<int>();
        var hold = new Dictionary<int, V2>();
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
                g.Minions.Clear();
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(100f, 0f) });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Action reset = () =>
        {
            mine.Clear(); hold.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            step(Game.FfFireStart + Game.FfLifeMastered + 1f);  // any fire from before is out
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.UltT = 0f; me.Pos = home; me.Downed = false; me.Hp = me.MaxHp;
            me.Stats.FfRadiusMult = 1f; me.Stats.FfBurnMult = 1f; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home, false });
        var heavyType = HeavyFoe();
        float dmg = me.Stats.DmgMult;

        // The kindle, then the ring rolling out.
        reset();
        var near = make(home + new V2(40f, 0f), EnemyType.Grunt);
        var far = make(home + new V2(-235f, 0f), EnemyType.Grunt);
        var outside = make(home + new V2(0f, 330f), EnemyType.Grunt);
        var heavy = make(home + new V2(0f, -150f), heavyType);
        step(dt);
        foreach (var e in new[] { near, far, outside, heavy }) { e.Hp = e.MaxHp; e.BurnT = 0f; }
        cast();
        Check(g.Effects.Count(f => f.Kind == FxKind.FlashFire) == 1, "casting: one fire effect");
        step(0.3f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.FlashFire) == 1, "the other screen sees the fire");
        Check(lost(near) == 0f && lost(far) == 0f, "nothing lands during the 0.5 s kindle");
        step(0.25f);  // 0.55 s: the ring has reached the near one, not yet the far one
        Check(lost(near) >= 100f * dmg - 1f && lost(near) < 110f * dmg && near.SlowT > 0f && near.SlowMult < 0.65f,
            "the ring reaches the near enemy first: 100 and slowed 40% (" + lost(near).ToString("0") + ")");
        Check(lost(far) == 0f, "the far enemy isn't reached yet: the ring rolls outward");
        step(0.3f);
        Check(lost(far) >= 100f * dmg - 1f, "the ring reaches the far edge (" + lost(far).ToString("0") + ")");
        Check(lost(outside) == 0f && outside.SlowT <= 0f, "nothing past the radius");
        Check(heavy.SlowT > 0f && heavy.SlowMult > 0.75f, "a heavy enemy is slowed less (x" + heavy.SlowMult.ToString("0.00") + ")");

        // The ground: 25 a second while it burns, no burn yet.
        float before = lost(near);
        step(2f);
        float ground = (lost(near) - before) / 2f;
        Check(ground > 22f * dmg && ground < 28f * dmg && near.BurnT <= 0f, "the ground hurts 25 a second, and doesn't set them burning yet (" + ground.ToString("0.0") + "/s)");
        var leaver = near;
        hold[leaver.Id] = home + new V2(400f, 0f);  // walks out before it ends
        step(Game.FfFireStart + Game.FfLife - 2.85f - 0.1f);  // just before it goes out (2.85 s in so far)
        Check(far.BurnT <= 0f && g.Effects.Any(f => f.Kind == FxKind.FlashFire), "still burning 8 s in, nobody set burning yet");
        step(0.3f);
        Check(far.BurnT > Game.FfBurnTime - 0.5f && far.BurnDps > 11f * dmg, "as it goes out, what's in it is left burning (12 a second for 5 s)");
        Check(leaver.BurnT <= 0f && outside.BurnT <= 0f, "what walked out (or was never in it) isn't");
        float after = lost(far);
        step(1f);
        Check(lost(far) - after > 10f * dmg && lost(far) - after < 14f * dmg, "the fire's out: only the burn hurts now (" + (lost(far) - after).ToString("0.0") + ")");

        // Mastered: 11 s.
        reset();
        me.MasteryXp[AbilityId.FlashFire] = 1e6f;
        var stay = make(home + new V2(100f, 0f), EnemyType.Grunt);
        step(dt); stay.Hp = stay.MaxHp;
        cast();
        me.MasteryXp.Remove(AbilityId.FlashFire);
        step(Game.FfFireStart + 10f);
        Check(stay.BurnT <= 0f && g.Effects.Any(f => f.Kind == FxKind.FlashFire), "mastered, it still burns 10 s in");
        step(1.3f);
        Check(stay.BurnT > 0f, "and goes out after 11 s");

        // Backdraft: 20% farther, the burn 50% hotter.
        reset();
        me.Stats.FfRadiusMult = 1.2f; me.Stats.FfBurnMult = 1.5f;
        var edge = make(home + new V2(300f, 0f), EnemyType.Grunt);
        step(dt); edge.Hp = edge.MaxHp;
        cast();
        step(Game.FfFireStart + Game.FfLife + 0.2f);
        Check(lost(edge) >= 100f * dmg && edge.BurnDps > 17f * dmg, "Backdraft: the fire reaches an enemy 300 px out and leaves it burning hotter");
    }
}
