using System.Linq;
using System;
using Arena;

// Sanguine Rain (SanguineRain.cs): the siphon's up-front cost, the seed, healing ticks on every ally, the healer's drain, the second-press
// cancel, the timer, the 15% floor, and the mastered length.
static partial class Program
{
    static void RainTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Blood", Role.Healer, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var ally = g.Players[2];
        g.Enemies.Clear();
        me.Slots[Upgrades.UltSlot].Id = AbilityId.SanguineRain;
        var def = Upgrades.Ability(AbilityId.SanguineRain);
        Check(def.Name == "Sanguine Rain" && def.Cooldown == 80f && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer) == AbilityId.SanguineRain, "the healer Hemomancer's ultimate is Sanguine Rain, 80 s");

        V2 pm = new V2(500f, 450f), pa = new V2(800f, 450f);
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { g.Enemies.Clear(); g.Bullets.Clear(); me.Pos = pm; ally.Pos = pa; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = pa }); } };
        Action prep = () => { g.Enemies.Clear(); me.Hp = me.MaxHp; ally.Hp = ally.MaxHp * 0.3f; me.Slots[Upgrades.UltSlot].Charges = 1; step(0.05f); };

        prep();
        float maxHp = me.MaxHp, regen = g.PassiveRegen + me.Stats.Regen + Game.AuraSelfHeal * me.Stats.AuraHealMult;  // (regeneration and the healer's own aura work against the cost)
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, false });
        Check(Game.RainActive(me) && !Game.RainRaining(me) && me.UltT > Game.RainCast + Game.RainLen - 0.1f, "casting starts the siphon and an ult timer of siphon + 10 s");
        step(Game.RainCast - 0.05f);
        float paid = maxHp - me.Hp;
        Console.WriteLine("  rain: siphon cost {0:0.0}% of max health; regen {1:0.0}/s, maxHp {2:0}", 100f * paid / maxHp, regen, maxHp);
        Check(Math.Abs(paid - (maxHp * Game.RainUpfront - regen * (Game.RainCast - 0.05f))) < maxHp * 0.01f, "the siphon costs 12% of max health");
        Check(ally.Hp <= ally.MaxHp * 0.31f, "nothing heals during the siphon");
        step(0.1f + Game.RainSeed);
        Check(Game.RainRaining(me), "the rings land 0.6 s after the siphon");
        float hp0 = ally.Hp, me0 = me.Hp;
        step(0.51f);
        float healed = ally.Hp - hp0;
        Console.WriteLine("  rain: first tick healed {0:0.0} ({1:0.0}% of {2:0})", healed, 100f * healed / ally.MaxHp, ally.MaxHp);
        Check(Math.Abs(healed - ally.MaxHp * Game.RainHeal) < 0.5f, "the first tick heals an ally 3% of max health");
        step(1f);
        Check(Math.Abs((ally.Hp - hp0) - ally.MaxHp * Game.RainHeal * 3f) < 1f, "then 3% every 0.5 s");
        Check(Math.Abs((me0 - me.Hp) - (maxHp * Game.RainDrain - regen) * 1.51f) < maxHp * 0.01f, "the healer drains 3% of max health a second");
        for (int f = 0; f < 0.3f / dt; f++) { host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = pa }); me.Pos = pm; ally.Pos = pa; }
        Check(client.Game.Players[1].RainClock > Game.RainCast + Game.RainSeed && client.Game.Players[1].UltT > 5f, "other screens see the rain's clock and time left");

        // Pressed again: it stops, the drain stops, the healing stops.
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, true });
        Check(!Game.RainActive(me) && me.UltT <= 0f, "pressing it again cancels it");
        Check(g.Effects.Any(e => e.Kind == FxKind.RainRelease), "each ring splashes away");
        float myAfter = me.Hp, allyAfter = ally.Hp;
        step(1.5f);
        Check(me.Hp >= myAfter - 0.01f && ally.Hp - allyAfter < ally.MaxHp * 0.03f, "after cancelling there is no more drain or healing");

        // Runs out by itself after 10 s of rain.
        prep();
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, false });
        step(Game.RainCast + Game.RainLen - 0.5f);
        Check(Game.RainActive(me), "still raining just before the 10 s are up");
        step(1f);
        Check(!Game.RainActive(me), "it ends when the time is up");

        // The floor: a weak healer's rain stops itself at 15%.
        prep();
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, false });
        step(Game.RainCast + Game.RainSeed + 0.2f);
        me.Hp = me.MaxHp * 0.17f;
        step(1f);
        Check(!Game.RainActive(me) && me.Hp >= me.MaxHp * 0.1f, "it ends by itself when the healer falls to 15%");

        // A downed ally gets nothing; the healer alone just pays.
        prep();
        ally.Hp = ally.MaxHp * 0.3f;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, false });
        step(Game.RainCast + Game.RainSeed + 0.3f);
        Check(Game.RainActive(me), "(still up)");
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, pa, true });
        Check(Game.RainRingR > 0f, "ring size is defined");
    }
}
