using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Water Ward (WaterWard.cs): the Healer Elementalist's 4th choice (pick 3 reaching the run), 2 drops a cast falling then circling,
// up to 10 (a press at 10 spends nothing), even spacing, a splash on touching an enemy (damage + wet round it), a burst on eating a
// blockable shot (wet, no damage; red shots pass), the mastered reach, replication, and going down.
static partial class Program
{
    static void WaterWardTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        var def = Upgrades.Ability(AbilityId.WaterWard);
        Check(def.Name == "Water Ward" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Elementalist && def.Role == Role.Healer && def.Cooldown == 3f,
            "Water Ward: a 3 s Healer Elementalist 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Elementalist) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Elementalist, 3) == AbilityId.WaterWard
            && Upgrades.FirstAbility(Role.Healer, Kit.Elementalist, 0) == AbilityId.HealingRain && Content.ThreeAbilitiesFallBack(),
            "the Healer Elementalist's 4th key is Water Ward; its first three are unchanged, and a class without a 4th still has three");
        Check(Upgrades.All.Any(u => u.RequiresAbility == AbilityId.WaterWard && u.Name == "Deep Current"), "Water Ward has its Rare card, Deep Current");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Elementalist", Role.Healer, 0, 3, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.FirstAbility == AbilityId.WaterWard && me.Slots[0].Id == AbilityId.WaterWard, "picking the 4th choice brings Water Ward into the run as the 1st ability");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(500f, 450f), aim = new V2(900f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; e.WetT = 0f; spots[e] = at; return e;
        };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; } me.Pos = mePos; me.Aim = 0f; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); } };
        Action reset = () => { g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.Pos = mePos; me.Aim = 0f; me.WwOff.Clear(); me.WwFall.Clear(); };
        Action press = () => { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; hostAbility.Invoke(g, new object[] { 1, 0, aim, false }); };

        // The cast: two drops fall, then circle.
        reset();
        pin(new V2(1500f, 880f));  // (out of the way, so the wave doesn't end)
        press();
        Check(me.WwOff.Count == 2 && me.WwFall.All(f => f > 0f), "a cast calls 2 drops, falling at first");
        Check(me.Slots[0].Charges == 0 || me.Slots[0].Timer > 0f, "the cooldown is running");
        step(0.3f);
        Check(!Game.WwLanded(me, 0) || !Game.WwLanded(me, 1), "they're still falling 0.3 s in");
        step(0.4f);
        Check(Game.WwLanded(me, 0) && Game.WwLanded(me, 1), "both have landed by 0.7 s");
        Check(client.Game.Players[1].WwOff.Count == 2, "other screens see the drops");
        float d0 = Game.WwDrop(me, 0).DistanceTo(mePos), d1 = Game.WwDrop(me, 1).DistanceTo(mePos);
        Check(Math.Abs(d0 - Game.WwOrbit) < 0.5f && Math.Abs(d1 - Game.WwOrbit) < 0.5f, "they circle you 54 px out");
        float a0 = me.WwAng;
        step(0.5f);
        Check(Math.Abs(MathX.Fmod(me.WwAng - a0, MathX.Tau) - Game.WwSpin * 0.5f) < 0.05f, "the ring turns, once every 2.4 s");

        // Stacking to 10, then a press spends nothing; spacing evens out.
        for (int i = 0; i < 4; i++) { press(); step(0.2f); }
        Check(me.WwOff.Count == Game.WwMax, "five casts make 10 drops");
        me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        Check(me.WwOff.Count == Game.WwMax && me.Slots[0].Charges == 1, "a press at 10 does nothing and doesn't spend the cooldown");
        step(2.5f);
        var offs = me.WwOff.Select(o => MathX.Fmod(o, MathX.Tau)).OrderBy(o => o).ToList();
        float worst = 0f;
        for (int i = 0; i < offs.Count; i++)
        {
            float gap = (i + 1 < offs.Count ? offs[i + 1] : offs[0] + MathX.Tau) - offs[i];
            worst = Math.Max(worst, Math.Abs(gap - MathX.Tau / 10f));
        }
        Console.WriteLine("  water ward: 10 drops, worst gap off by {0:0.000} rad", worst);
        Check(worst < 0.05f, "the drops spread out evenly round you");
        Check(client.Game.Players[1].WwOff.Count == 10, "other screens see all 10");

        // A splash: a drop touching an enemy hurts and soaks what's round it, and is used up.
        var touch = pin(mePos + new V2(Game.WwOrbit + 8f, 0f));
        var near = pin(mePos + new V2(Game.WwOrbit + 40f, 30f));
        var far = pin(mePos + new V2(-300f, 0f));
        for (int f = 0; f < 60 && !g.Effects.Any(e => e.Kind == FxKind.WwSplash); f++)
            step(dt);
        var splash = g.Effects.FirstOrDefault(e => e.Kind == FxKind.WwSplash);
        Check(splash != null && Math.Abs(splash.Radius - Game.WwSplashR) < 0.01f, "a drop touching an enemy splashes");
        float hit = touch.MaxHp - touch.Hp, D = Game.WwSplashDamage * me.Stats.DmgMult;
        Console.WriteLine("  water ward: splash hit {0:0.0} of {1:0.0}", hit, D);
        Check(hit >= D - 0.5f && touch.WetT > 3f, "the enemy it touched takes 12 and is soaked wet for 4 s");
        Check(near.Hp < near.MaxHp && near.WetT > 3f, "an enemy within 52 px of the splash is hurt and soaked too");
        Check(far.Hp == far.MaxHp && far.WetT == 0f, "enemies away from it aren't");
        Check(me.WwOff.Count < 10, "the drop is used up");

        // A burst: a drop eating a shot soaks what's round it (no damage); red shots pass.
        reset();
        pin(new V2(1500f, 880f));
        for (int i = 0; i < 5; i++) { press(); step(0.2f); }
        step(2.5f);
        var mist = pin(mePos + new V2(Game.WwOrbit + 45f, -40f));
        int before = me.WwOff.Count, shots = 0;
        for (int k = 0; k < 6 && !g.Effects.Any(e => e.Kind == FxKind.WwBurst); k++, shots++)
        {
            shoot.Invoke(g, new object[] { Defs.SmallShot, mePos + new V2(220f, 0f), new V2(-300f, 0f), 0f, 6f, 5f, 0, false, 0f });
            step(0.6f);
        }
        var burst = g.Effects.FirstOrDefault(e => e.Kind == FxKind.WwBurst);
        Check(burst != null && Math.Abs(burst.Radius - Game.WwBurstR) < 0.01f, "a drop eats an enemy shot and bursts (" + shots + " shots)");
        Check(me.WwOff.Count < before, "the drop is used up");
        Check(mist.Hp == mist.MaxHp && mist.WetT > 3f, "the burst soaks the enemy near it without hurting it");
        reset();
        pin(new V2(1500f, 880f));
        for (int i = 0; i < 5; i++) { press(); step(0.2f); }
        step(2.5f);
        for (int k = 0; k < 6; k++)
        {
            shoot.Invoke(g, new object[] { Defs.PierceShot, mePos + new V2(220f, 0f), new V2(-300f, 0f), 0f, 6f, 5f, 0, false, 0f });
            step(0.4f);
        }
        Check(me.WwOff.Count + g.Effects.Count(e => e.Kind == FxKind.WwSplash) == 10 && !g.Effects.Any(e => e.Kind == FxKind.WwBurst),
            "red aimed shots aren't blocked");  // (the wave's own enemies may walk into a drop meanwhile)

        // Mastered: the splash reaches farther.
        reset();
        me.MasteryXp[AbilityId.WaterWard] = 99999f;
        press();
        step(0.8f);
        me.MasteryXp.Remove(AbilityId.WaterWard);
        Check(me.WwWide, "mastered casts are marked");
        pin(mePos + new V2(Game.WwOrbit + 8f, 0f));
        for (int f = 0; f < 120 && !g.Effects.Any(e => e.Kind == FxKind.WwSplash); f++)
            step(dt);
        var big = g.Effects.FirstOrDefault(e => e.Kind == FxKind.WwSplash);
        Check(big != null && Math.Abs(big.Radius - Game.WwSplashR * Game.WwMasteredReach) < 0.01f, "mastered: splashes reach 40% farther");

        // Going down: the drops are gone.
        press();
        me.Downed = true;
        step(dt);
        Check(me.WwOff.Count == 0, "going down takes the drops away");
        me.Downed = false;
        s[0].Leave(""); s[1].Leave("");
    }
}
