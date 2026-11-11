using System;
using System.Collections.Generic;
using Arena;

// Timed waves (TimedWaves.cs): a regular wave's enemies arrive over a window, ramping up, holding and tapering off.
static partial class Program
{
    static void TimedWavesTest(float dt)
    {
        // The arrival curve: 0 at the start, 1 at the end, never going back, and slower at both ends than in the middle.
        float prev = 0f;
        bool rising = true;
        for (int i = 1; i <= 100; i++)
        {
            float v = TimedWaves.Share(i / 100f);
            rising &= v >= prev;
            prev = v;
        }
        float first = TimedWaves.Share(0.1f), mid = TimedWaves.Share(0.4f) - TimedWaves.Share(0.3f), last = 1f - TimedWaves.Share(0.9f);
        Check(TimedWaves.Share(0f) == 0f && TimedWaves.Share(1f) == 1f && rising, "the arrival curve runs from 0 to 1 and never goes back");
        Check(first < mid && last < mid, "the wave ramps up and tapers off (" + first.ToString("0.00") + " / " + mid.ToString("0.00") + " / " + last.ToString("0.00") + ")");

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var squadField = typeof(Game).GetField("squadQueue", flags);
        var s = StartBuffed(dt, 3, 10);
        var g = s[0].Game;
        TimedWaves.Enabled = true;
        Run(s, dt, 30, () => g.State == MatchState.Fighting);
        int total = ((List<EnemyType>)queueField.GetValue(g)).Count;
        foreach (var q in (List<SquadDef>)squadField.GetValue(g))
            total += q.Members.Length;
        Run(s, dt, 4, () => false);
        int left4 = ((List<EnemyType>)queueField.GetValue(g)).Count;
        int maxAlive = 0;
        float t = 4f;
        for (; t < 70f && (((List<EnemyType>)queueField.GetValue(g)).Count > 0 || ((List<SquadDef>)squadField.GetValue(g)).Count > 0); t += 1f)
        {
            Run(s, dt, 1, () => false);
            maxAlive = Math.Max(maxAlive, g.Foes());
        }
        Console.WriteLine("  timed wave {0}: {1} enemies, {2} still queued after 4 s, queue empty at {3:0} s, most alive at once {4}", g.Wave, total, left4, t, maxAlive);
        Check(left4 > 0, "a timed wave does not spawn everything at once");
        Check(t >= 25f && t <= 60f, "a timed wave's spawning lasts most of its window (" + t.ToString("0") + " s)");
        Check(maxAlive <= TimedWaves.AliveCap(g.Wave) + 8, "no more than the alive cap at once (" + maxAlive + ")");
        TimedWaves.Enabled = false;
    }
}
