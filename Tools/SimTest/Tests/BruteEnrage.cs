using System;
using Arena;

// Brute enrage: faster and harder-hitting the more health it has lost; other enemies unaffected.
static partial class Program
{
    static void BruteEnrageTest()
    {
        var b = new Enemy { Type = EnemyType.Brute, Hp = 100f, MaxHp = 100f };
        Check(Game.BruteSpeedMult(b) == 1f && Game.BruteDamageMult(b) == 1f, "brute enrage: full health, no bonus");
        b.Hp = 50f;
        Check(Math.Abs(Game.BruteSpeedMult(b) - 1.4f) < 0.001f && Math.Abs(Game.BruteDamageMult(b) - 1.3f) < 0.001f, "brute enrage: half health, +40% speed, +30% damage");
        b.Hp = 0f;
        Check(Math.Abs(Game.BruteSpeedMult(b) - 1.8f) < 0.001f && Math.Abs(Game.BruteDamageMult(b) - 1.6f) < 0.001f, "brute enrage: near death, +80% speed, +60% damage");
        b.Hp = 80f;
        float low = Game.BruteSpeedMult(b);
        b.Hp = 30f;
        Check(Game.BruteSpeedMult(b) > low, "brute enrage: grows as health falls");
        b.Hp = 90f;
        Check(Game.BruteSpeedMult(b) < low, "brute enrage: healing calms it");
        var g = new Enemy { Type = EnemyType.Grunt, Hp = 1f, MaxHp = 100f };
        Check(Game.BruteSpeedMult(g) == 1f && Game.BruteDamageMult(g) == 1f, "brute enrage: other enemies unaffected");
    }
}
