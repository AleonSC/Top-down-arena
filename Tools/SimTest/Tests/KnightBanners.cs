using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Knight banner ultimates (KnightBanners.cs): Aegis Standard (Tank key 3), Pilgrim's Standard (Healer key 1) and War Standard
// (DPS key 2): the lobby keys, the throw and the ring, the Aegis shield (fills, soaks, waits, regenerates, fades outside),
// the Pilgrim's heal, the War Standard's attack speed and damage on both screens, pressing again to lift it (cooldown starts then),
// lifting on going down, and 60 s at most.
static partial class Program
{
    static void KnightBannersTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var hurt = typeof(Game).GetMethods(flags).First(m => m.Name == "HurtEnemy" && m.GetParameters().Length >= 3 && m.GetParameters()[0].ParameterType == typeof(Enemy));

        Check(Upgrades.Ultimate(Role.Tank, Kit.Knight, 3) == AbilityId.AegisStandard && Upgrades.Ultimate(Role.Healer, Kit.Knight, 1) == AbilityId.PilgrimsStandard
            && Upgrades.Ultimate(Role.Dps, Kit.Knight, 2) == AbilityId.WarStandard
            && Upgrades.Ability(AbilityId.AegisStandard).Cooldown == 75f && Upgrades.Ability(AbilityId.PilgrimsStandard).Cooldown == 80f && Upgrades.Ability(AbilityId.WarStandard).Cooldown == 70f,
            "the banners are the Tank Knight's key 3, the Healer Knight's key 1 and the DPS Knight's key 2 (75, 80 and 70 s)");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Ally", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players.Values.First(p => p != me);
        g.DevSetGod(1, true); g.DevSetGod(ally.Id, true);
        var keep = new HashSet<Enemy>();
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        V2 mine = new V2(500f, 450f), allyAt = new V2(620f, 450f);
        Action<float> Step = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                me.Pos = mine; ally.Pos = allyAt;
                Player mirror;
                if (client.Game.Players.TryGetValue(ally.Id, out mirror)) mirror.Pos = allyAt;  // (the client moves its own player)
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); client.Game.Update(dt);
                me.Pos = mine; ally.Pos = allyAt;
                Clear();
            }
        };
        Func<AbilityId, Zone> Plant = id =>
        {
            g.Zones.Clear();
            var slot = me.Slots[Upgrades.UltSlot];
            slot.Id = id; slot.Charges = 1; slot.Timer = 0f;
            cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(580f, 450f), false });
            return g.Zones.Values.FirstOrDefault(z => Game.IsBanner(z.Kind));
        };
        Clear(); Step(0.2f);

        // --- Aegis Standard ---
        var aegis = Plant(AbilityId.AegisStandard);
        Check(aegis != null && aegis.Kind == ZoneKind.AegisStandard && aegis.Pos.DistanceTo(new V2(580f, 450f)) < 1f && aegis.Radius == Game.StdShieldRadius,
            "the Aegis Standard lands at the cursor (within 160 px) with a 220 px ring");
        Check(!Game.InBanner(aegis, allyAt) && me.Slots[Upgrades.UltSlot].Charges == 0, "nothing while it's still in the air; the charge is spent");
        Step(0.8f);
        Check(Math.Abs(ally.StdShieldHp - Game.StdShield) < 0.5f && Math.Abs(me.StdShieldHp - Game.StdShield) < 0.5f, "once planted, everyone inside wears an 80-point shield");
        Step(0.3f);
        Check(client.Game.Players[ally.Id].StdShieldHp > 70f && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.AegisStandard), "the other screen sees the banner and the shield");
        g.DevSetGod(ally.Id, false);
        float hp0 = ally.Hp;
        damage.Invoke(g, new object[] { ally, 30f, "test" });
        Check(ally.Hp >= hp0 - 0.01f && ally.StdShieldHp < Game.StdShield - 5f, "a hit comes off the shield, not health");
        float worn = ally.StdShieldHp;
        Step(1f);
        Check(ally.StdShieldHp <= worn + 0.5f, "no regeneration for 1.5 s after a hit");
        Step(1.5f);
        Check(ally.StdShieldHp > worn + 10f, "then it regenerates (25 a second)");
        damage.Invoke(g, new object[] { ally, ally.StdShieldHp + 10f, "test" });
        Check(ally.StdShieldHp <= 0f && ally.Hp < hp0, "a big hit breaks it and the rest reaches health");
        ally.Hp = ally.MaxHp; g.DevSetGod(ally.Id, true);
        Step(2f);
        allyAt = new V2(1100f, 450f); Step(0.1f);
        float outside = ally.StdShieldHp;
        Step(2f);
        Check(outside > 0f && ally.StdShieldHp <= 0f, "outside the ring the shield fades away (" + outside.ToString("0.0") + " -> " + ally.StdShieldHp.ToString("0.0") + ")");
        allyAt = new V2(620f, 450f);

        // --- pressing again lifts it; the cooldown starts then ---
        var slotA = me.Slots[Upgrades.UltSlot];
        float timerBefore = slotA.Timer;
        Step(2f);
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(580f, 450f), false });
        Check(!Game.BannerOn(aegis) && aegis.Aim > 0f && slotA.Timer > Game.StdShieldCooldown * 0.95f && slotA.Charges == 0,
            "pressing again lifts it (it folds away) and the full cooldown starts then");
        Step(1f);
        Check(!g.Zones.Values.Any(z => Game.IsBanner(z.Kind)), "the folded banner is gone");

        // --- Pilgrim's Standard ---
        var pil = Plant(AbilityId.PilgrimsStandard);
        Step(0.7f);
        g.DevSetGod(ally.Id, false);
        ally.Hp = ally.MaxHp * 0.3f;
        float before = ally.Hp;
        Step(2f);
        float healed = ally.Hp - before;
        Check(healed > ally.MaxHp * Game.PilgrimHeal * 1.5f && healed < ally.MaxHp * Game.PilgrimHeal * 2.6f, "Pilgrim's Standard heals 4% of max health a second (" + healed.ToString("0.0") + " in 2 s, max " + ally.MaxHp.ToString("0") + ")");
        allyAt = new V2(1100f, 450f); Step(0.1f);
        before = ally.Hp; Step(1f);
        Check(ally.Hp - before < 0.5f, "...only inside the ring (" + (ally.Hp - before).ToString("0.0") + ")");
        allyAt = new V2(620f, 450f);
        g.DevSetGod(ally.Id, true);

        // --- War Standard ---
        Check(Math.Abs(g.WarFireMult(ally) - 1f) < 0.001f, "no War Standard: no buff");
        var war = Plant(AbilityId.WarStandard);
        Step(0.8f);
        Check(Math.Abs(g.WarFireMult(ally) - (1f + Game.WarFire)) < 0.001f && Math.Abs(client.Game.WarFireMult(client.Game.Players[ally.Id]) - (1f + Game.WarFire)) < 0.001f,
            "inside a War Standard allies attack 40% faster, on both screens");
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(900f, 300f), 0f, 0, false });
        dummy.SpawnT = 0f; dummy.Hp = dummy.MaxHp = 10000f; dummy.FrozenT = 99f; keep.Add(dummy);
        var args = hurt.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        args[0] = dummy; args[1] = 100f; args[2] = ally.Id;
        hurt.Invoke(g, args);
        float inDmg = 10000f - dummy.Hp;
        allyAt = new V2(1100f, 450f); Step(0.1f);
        dummy.Hp = 10000f; hurt.Invoke(g, args);
        float outDmg = 10000f - dummy.Hp;
        Check(Math.Abs(inDmg / outDmg - (1f + Game.WarDmg)) < 0.02f && Math.Abs(g.WarFireMult(ally) - 1f) < 0.001f,
            "...and hit 25% harder (" + inDmg.ToString("0") + " vs " + outDmg.ToString("0") + "); outside, nothing");
        allyAt = new V2(620f, 450f);

        // --- it comes down on its own: going down, and after 60 s ---
        g.DevSetGod(1, false);
        damage.Invoke(g, new object[] { me, 100000f, "test" });
        Step(0.1f);
        Check(me.Downed && !Game.BannerOn(war) && me.Slots[Upgrades.UltSlot].Timer > Game.WarCooldown * 0.95f, "going down lifts it, and the cooldown starts");
        me.Downed = false; me.Hp = me.MaxHp; g.DevSetGod(1, true);
        Step(0.3f);
        var timed = Plant(AbilityId.WarStandard);
        Step(Game.BannerTime - 1f);
        Check(Game.BannerOn(timed), "it stands for most of a minute...");
        Step(1.5f);
        Check(!Game.BannerOn(timed), "...and comes down after 60 s");

        host.Leave(""); client.Leave("");
    }
}
