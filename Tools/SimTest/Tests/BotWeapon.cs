using System;
using System.Collections.Generic;
using Arena;

// The smart bot's parry (BotParry.cs) and weapon Alts (BotWeaponAlt.cs).
static partial class Program
{
    /// A host with a smart bot at `level` and one still friend far off; the bot stands mid-arena, the friend in a corner.
    static (Session[] s, Game g, Player bot) BotWeaponSetup(float dt, BotSkill level, Role role, int weapon, Kit kit = Kit.Soldier)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Bot", role, weapon, 0, kit);
        var client = JoinAs(port, false, "Ally", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.Bot = true; g.BotLevel = level; g.BotRerolls = false;
        client.Game.Bot = false;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        return (s, g, g.Players[1]);
    }

    static void BotParryTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        foreach (var level in new[] { BotSkill.Good, BotSkill.Expert })
        {
            var (s, g, bot) = BotWeaponSetup(dt, level, Role.Dps, 0);
            int needles = 0, volleys = 0;
            var seen = new HashSet<int>();
            Action step = () =>
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                g.Players[2].Pos = new V2(60f, 60f);
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == bot.Id && b.Kind == Defs.ParryNeedle && seen.Add(b.Id)) needles++;
            };
            // Calm first: nothing coming, so the shield stays down.
            bot.Pos = center;
            Run(s, dt, 1.5f, null, step);
            int calmRaises = g.BotParries;
            // A wall of shots from the right every 1.2 s, too wide to walk out of in time: block it.
            for (float t = 0f; t < 9f; t += dt)
            {
                if (MathX.Fmod(t, 1.2f) < dt)
                {
                    volleys++;
                    for (int k = -4; k <= 4; k++)
                    {
                        V2 from = bot.Pos + new V2(230f, k * 22f);
                        spawnShot.Invoke(g, new object[] { Defs.SmallShot, from, new V2(-520f, 0f), 0f, 2f, 10f, -1, false, 0f });
                    }
                }
                Run(s, dt, dt, null, step);
            }
            Console.WriteLine("  {0}: {1} raises in the calm, {2} raises and {3} perfect-parry needles over {4} volleys", level, calmRaises, g.BotParries - calmRaises, needles, volleys);
            Check(calmRaises == 0, level + " bot keeps the shield down with nothing coming");
            Check(g.BotParries - calmRaises >= volleys / 2, level + " bot raises the parry against shots it can't dodge");
            Check(needles >= volleys, level + " bot's parries are timed perfect (shots go back as needles)");
        }
    }

    static void BotAltTest(float dt)
    {
    }
}
