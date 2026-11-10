using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Contraband (Contraband.cs, Docs/Contraband.md): a chest on the first wave after each boss, and rare-or-better items for its holder.
static partial class Program
{
    static void ContrabandTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var waveProps = typeof(Game).GetMethod("WaveProps", flags);
        var rollLoot = typeof(Game).GetMethod("RollChestLoot", flags);
        Check(Game.IsCycleStart(6) && Game.IsCycleStart(11) && Game.IsCycleStart(46) && !Game.IsCycleStart(1) && !Game.IsCycleStart(5) && !Game.IsCycleStart(7) && !Game.IsCycleStart(10), "the cycle starts are waves 6, 11, 16... (not wave 1, not a boss wave)");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        me.Perks = Perks.Empty; ally.Perks = Perks.Empty;
        g.PropsOn = true;  // (the simulation tests leave props off)

        // How many of 30 tries put a chest in the arena for a wave.
        Func<int, int> chests = wave =>
        {
            int n = 0, oldWave = g.Wave;
            g.Wave = wave; g.BossSlot = (wave) % 5 == 0 && (wave) > 0;
            for (int i = 0; i < 30; i++)
            {
                foreach (var key in g.Props.Keys.ToList()) g.Props.Remove(key);
                waveProps.Invoke(g, null);
                if (g.Props.Values.Any(p => p.Kind == PropKind.Chest)) n++;
            }
            foreach (var key in g.Props.Keys.ToList()) g.Props.Remove(key);
            g.Wave = oldWave; g.BossSlot = (oldWave) % 5 == 0 && (oldWave) > 0;
            return n;
        };

        Check(chests(6) < 30 && chests(11) < 30, "without Contraband the first wave after a boss gets only the usual chance of a chest");
        ally.Perks = new[] { PerkId.Contraband };
        Console.WriteLine("  chests in 30 tries with Contraband: wave 6 {0}, 11 {1}, 21 {2}; wave 7 {3}", chests(6), chests(11), chests(21), chests(7));
        Check(chests(6) == 30 && chests(11) == 30 && chests(21) == 30, "with anyone in the party holding Contraband, waves 6, 11 and 21 always have a chest");
        Check(chests(7) < 30 && chests(9) < 30, "other waves don't");
        var oldHunt = g.Hunt;
        g.Hunt = Hunts.All[0];
        Check(chests(6) < 30, "a Boss Hunt has no boss cycles, so no guaranteed chest");
        g.Hunt = oldHunt;
        ally.Perks = Perks.Empty;

        // Items: never uncommon for the holder; the rest of the party still rolls normally.
        Func<Player, int> uncommons = pl =>
        {
            int n = 0;
            for (int i = 0; i < 400; i++)
            {
                int u = (int)rollLoot.Invoke(g, new object[] { pl });
                if (u >= 0 && Upgrades.All[u].Rarity == Rarity.Uncommon) n++;
            }
            return n;
        };
        me.Perks = new[] { PerkId.Contraband };
        int holder = uncommons(me), other = uncommons(ally);
        Console.WriteLine("  uncommon chest items in 400 rolls: holder {0}, teammate {1}", holder, other);
        Check(holder == 0, "the holder's chest items are never uncommon");
        Check(other > 0, "a teammate without it still rolls uncommon items");
        host.Leave(""); client.Leave("");
    }
}
