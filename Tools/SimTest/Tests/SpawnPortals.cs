using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Spawn portals and surges (SpawnPortals.cs, TimedWaves.cs): bursts through portals, sealing them, flanks, the surge rhythm.
static partial class Program
{
    static void SpawnPortalsTest(float dt)
    {
        // The surge rhythm: a surge's slice of the window holds well over twice a lull's.
        Func<float, float> slice = c => TimedWaves.Share(c + 0.02f) - TimedWaves.Share(c - 0.02f);
        float surge = slice(TimedWaves.Surges[0]), lull = slice((TimedWaves.Surges[0] + TimedWaves.Surges[1]) / 2f);
        Console.WriteLine("  surge slice {0:0.000}, lull slice {1:0.000}", surge, lull);
        Check(surge > 2f * lull, "a surge brings enemies in much faster than the lull after it");
        Check(TimedWaves.InSurge(TimedWaves.Surges[1]) && !TimedWaves.InSurge(0.43f), "InSurge is true at a surge and false in a lull");

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var queueField = typeof(Game).GetField("spawnQueue", flags);

        // A timed wave comes in through portals.
        TimedWaves.Enabled = true;
        var s = StartBuffed(dt, 6, 10);
        var g = s[0].Game;
        Run(s, dt, 30, () => g.State == MatchState.Fighting);
        int portalsSeen = 0, flanks = 0, maxOpen = 0;
        var seen = new HashSet<int>();
        bool clientSaw = false;
        Run(s, dt, 45, null, () =>
        {
            int open = 0;
            foreach (var e in g.Enemies.Values)
                if (e.Type == EnemyType.Portal)
                {
                    open++;
                    if (seen.Add(e.Id))
                    {
                        portalsSeen++;
                        if (SpawnPortals.IsFlank(e.Phase))
                            flanks++;
                    }
                }
            maxOpen = Math.Max(maxOpen, open);
            clientSaw |= s[1].Game.Enemies.Values.Any(e => e.Type == EnemyType.Portal);
        });
        Console.WriteLine("  {0} portals in 45 s ({1} flanks), at most {2} open at once", portalsSeen, flanks, maxOpen);
        Check(portalsSeen >= 3, "a timed wave's enemies come in through several portals");
        Check(maxOpen <= SpawnPortals.SurgeOpenMax, "no more than " + SpawnPortals.SurgeOpenMax + " portals open at once");
        Check(clientSaw, "the client sees the portals (the enemy snapshot)");

        // A portal lets its whole burst out, then shuts with no reward.
        g.Enemies.Clear();
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        int scoreBefore = g.Score;
        var p = g.OpenPortal(new List<EnemyType> { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt }, false, new V2(200f, 200f));
        Run(s, dt, SpawnPortals.Appear + SpawnPortals.OpenTime + 3 * SpawnPortals.EmitEvery + SpawnPortals.CloseTime + 0.3f);
        int grunts = g.Enemies.Values.Count(e => e.Type == EnemyType.Grunt);
        Check(!g.Enemies.ContainsKey(p.Id) && g.PortalCargoCount() == 0, "an emptied portal shuts");
        Check(grunts + (g.Score - scoreBefore) / Defs.Get(EnemyType.Grunt).Score >= 3, "it let all three grunts out (" + grunts + " still up)");

        // Shot closed early: the rest of the burst never comes.
        g.Enemies.Clear();
        p = g.OpenPortal(new List<EnemyType> { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt }, false, new V2(200f, 200f));
        Run(s, dt, 0.4f);
        scoreBefore = g.Score;
        p.Hp = 0f;
        Run(s, dt, 0.1f);
        Check(!g.Enemies.ContainsKey(p.Id), "a portal shot to 0 health dies");
        Run(s, dt, 2f);
        Check(g.Enemies.Values.Count(e => e.Type == EnemyType.Grunt) == 0 && g.PortalCargoCount() == 0, "the burst inside a sealed portal is cancelled");
        Check(g.Score - scoreBefore >= 5 * SpawnPortals.SealScore, "sealing it pays out for each enemy it held");

        // A flank opens behind the way the party is aiming.
        g.Enemies.Clear();
        foreach (var pl in g.Players.Values)
        {
            pl.Pos = new V2(Game.Width / 2f, Game.Height / 2f);
            pl.Aim = 0f;  // facing right
        }
        int behind = 0, marked = 0;
        for (int k = 0; k < 6; k++)
        {
            var f = g.OpenPortal(new List<EnemyType> { EnemyType.Grunt }, true);
            if (f.Pos.X < Game.Width / 2f)
                behind++;
            if (SpawnPortals.IsFlank(f.Phase))
                marked++;
            g.Enemies.Remove(f.Id);
        }
        Check(marked == 6, "a flank portal is marked as one");
        Check(behind >= 5, "flank portals open behind the party (" + behind + " of 6)");

        TimedWaves.Enabled = false;
    }
}
