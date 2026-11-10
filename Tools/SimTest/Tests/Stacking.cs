using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Power stacking (Docs/PowerStacking.md): diminishing returns on the cards you can take any number of times, and wounds
// (a share of every hit that can't be healed back until the wave is cleared).
static partial class Program
{
    static void StackingTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        Func<string, int, List<int>> Copies = (name, n) => Enumerable.Repeat(U(name), n).ToList();

        // Diminishing returns.
        var hair = Upgrades.All[U("Hair Trigger")];
        Check(Upgrades.Diminishes(hair) && Math.Abs(Upgrades.CopyStrength(hair, 1) - 1f) < 1e-4f && Math.Abs(Upgrades.CopyStrength(hair, 2) - 0.8f) < 1e-4f
            && Math.Abs(Upgrades.CopyStrength(hair, 5) - 0.5f) < 1e-4f, "each extra copy of an unlimited card is worth less: 100%, 80%, ... 50% for the 5th");
        float one = Upgrades.Compute(Copies("Hair Trigger", 1)).FireRateMult;
        float fourteen = Upgrades.Compute(Copies("Hair Trigger", 14)).FireRateMult;
        float sum = 0f;
        for (int n = 1; n <= 14; n++) sum += 1f / (1f + Upgrades.DiminishStep * (n - 1));
        Console.WriteLine("  Hair Trigger x14: fire rate x{0:0.00} (was x{1:0.00})", fourteen, 1f + 0.2f * 14);
        Check(Math.Abs(one - 1.2f) < 1e-4f && Math.Abs(fourteen - (1f + 0.2f * sum)) < 1e-3f && fourteen < 2.4f, "the first copy is full strength; fourteen Hair Triggers come to x" + fourteen.ToString("0.00") + " fire rate, not x3.8");
        float hollow8 = Upgrades.Compute(Copies("Hollow Points", 8)).DmgMult;
        var mixed = Copies("Hollow Points", 2); mixed.Add(U("Sharpened Rounds"));
        float mixedDmg = Upgrades.Compute(mixed).DmgMult;
        Check(hollow8 < 2f && Math.Abs(mixedDmg - (1f + 0.2f + 0.16f + 0.1f)) < 1e-3f, "Hollow Points x8 is x" + hollow8.ToString("0.00") + " damage; different cards count apart (2 Hollow Points + 1 Sharpened Rounds = x" + mixedDmg.ToString("0.00") + ")");
        float cd6 = Upgrades.Compute(Copies("Clarity", 6)).CdMult;
        Check(cd6 > 0.45f && cd6 < 0.6f, "six Clarity cut cooldowns to " + (cd6 * 100f).ToString("0") + "% (was " + (Math.Pow(0.85, 6) * 100).ToString("0") + "%)");
        var magnum = Upgrades.All[U("Magnum Rounds")];
        Check(!Upgrades.Diminishes(magnum) && Math.Abs(Upgrades.Compute(Copies("Magnum Rounds", 4)).ShotSizeMult - 1.6f) < 1e-4f, "cards with a stack limit keep full strength (Magnum Rounds x4: +60%)");
        Check(Upgrades.CopyNote(hair, Copies("Hair Trigger", 2)) == "Copy 3: 67% strength" && Upgrades.CopyNote(hair, new List<int>()) == "", "an offer of a card you have says what this copy is worth");

        // Wounds.
        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var damage = typeof(Game).GetMethod("Damage", flags);
        var heal = typeof(Game).GetMethod("Heal", flags);
        var waveCleared = typeof(Game).GetMethod("WaveCleared", flags);
        g.Bot = false;
        var p = g.Players[1];
        p.Hp = p.MaxHp; p.InvulnT = 0f; p.ShieldHp = 0f;
        float before = p.Hp;
        damage.Invoke(g, new object[] { p, 50f, "test" });
        float took = before - p.Hp;
        Check(took > 0f && Math.Abs(p.Wound - Game.WoundShare * took) < 0.01f, "a hit leaves a wound of " + (Game.WoundShare * 100f) + "% of the damage (" + took.ToString("0.0") + " taken, " + p.Wound.ToString("0.0") + " wound)");
        heal.Invoke(g, new object[] { p, 9999f, null, "test" });
        Check(Math.Abs(p.Hp - (p.MaxHp - p.Wound)) < 0.01f && p.HealCap < p.MaxHp, "healing stops at max health less the wound (" + p.Hp.ToString("0.0") + " / " + p.MaxHp.ToString("0") + ")");
        Run(s, dt, 2f);
        Check(Math.Abs(p.Hp - p.HealCap) < 0.05f, "regeneration stops there too");
        var cp = s[1].Game.Players[1];
        Check(Math.Abs(cp.Wound - p.Wound) < 0.01f, "the other player's game knows the wound (for the health bars)");
        // A Healer's overheal shield only takes what goes past full health, not the wound's share.
        var healer = g.Players[2];
        healer.Hp = healer.MaxHp; healer.InvulnT = 0f; healer.ShieldHp = 0f;
        p.InvulnT = 0f;
        damage.Invoke(g, new object[] { p, 20f, "test" });
        float gap = p.MaxHp - p.Hp;
        p.ShieldHp = 0f;
        heal.Invoke(g, new object[] { p, gap - 1f, healer, "test" });
        Check(p.ShieldHp <= 0.01f, "healing blocked by the wound doesn't turn into overheal shield");
        // Cleared: the wound goes.
        waveCleared.Invoke(g, null);
        Check(p.Wound == 0f && p.HealCap == p.MaxHp, "the wound heals once the wave is cleared");
        Run(s, dt, 0.4f);
        Check(cp.Wound == 0f, "...on every screen");
        s[0].Leave(""); s[1].Leave("");
    }
}
