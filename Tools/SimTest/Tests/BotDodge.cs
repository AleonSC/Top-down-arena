using System;
using Arena;

// The smart bot's Dodge (BotDodge.cs): none in the calm, a sprint to cross the arena (not every cooldown), and evasive
// dodges from walls of shots it can't walk out of or parry, kept to its skill's gap.
static partial class Program
{
    static void BotDodgeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        foreach (var level in new[] { BotSkill.Casual, BotSkill.Good, BotSkill.Expert })
        {
            var (s, g, bot) = BotWeaponSetup(dt, level, Role.Dps, 0);
            bool broken = false;
            Action step = () =>
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                g.Players[2].Pos = new V2(60f, 60f);
                if (broken) bot.ParryBroken = 1f;  // no parry: the dodge is all it has
            };

            // Calm, already where it wants to be: no dodge, no sprint.
            bot.Pos = center;
            Run(s, dt, 3f, null, step);
            int calmDodges = g.BotDodges, calmSprints = g.BotSprints;
            Check(calmDodges == 0 && calmSprints == 0, level + " bot doesn't dodge or sprint in the calm");

            // Far from the middle with nothing about: it sprints there, once, not every cooldown.
            bot.Pos = new V2(80f, 80f);
            float before = bot.Pos.DistanceTo(center);
            Run(s, dt, 1.2f, null, step);
            int sprints = g.BotSprints - calmSprints;
            float covered = before - bot.Pos.DistanceTo(center);
            Console.WriteLine("  {0}: {1} sprint(s) crossing, {2:0} px in 1.2 s (walking {3:0})", level, sprints, covered, 1.2f * bot.Speed);
            Check(sprints == 1, level + " bot sprints once to cross the arena");
            Check(covered > 1.2f * bot.Speed * 1.2f, level + " bot's sprint gets it there faster than walking");

            // Walls of shots from the right, too wide to walk out of, with the parry broken: it dodges, within its gap.
            Run(s, dt, 3f, null, step);
            bot.Pos = center;
            broken = true;
            int dodges0 = g.BotDodges, volleys = 0;
            const float span = 10f;
            for (float t = 0f; t < span; t += dt)
            {
                if (MathX.Fmod(t, 1.2f) < dt)
                {
                    volleys++;
                    for (int k = -5; k <= 5; k++)
                        spawnShot.Invoke(g, new object[] { Defs.SmallShot, bot.Pos + new V2(450f, k * 20f), new V2(-560f, 0f), 0f, 2f, 10f, -1, false, 0f });
                }
                Run(s, dt, dt, null, step);
            }
            int dodges = g.BotDodges - dodges0;
            Game.DodgeSkillOf(level, out _, out float gap, out _);
            Console.WriteLine("  {0}: {1} evasive dodges over {2} volleys ({3} s apart at least)", level, dodges, volleys, gap);
            Check(dodges >= 2, level + " bot dodges walls of shots it can't walk out of");
            Check(dodges <= span / Math.Max(gap, Game.DodgeCd) + 1, level + " bot keeps to its gap between dodges");
            s[0].Leave(""); s[1].Leave("");
        }
    }
}
