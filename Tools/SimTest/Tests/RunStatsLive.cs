using System;
using Arena;

// The Run stats screen's numbers: the host sends everyone the run's tallies while it's going (Msg.RunStatsLive).
static partial class Program
{
    static void RunStatsLiveTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        var c = s[1].Game;
        Run(s, dt, 15, () => g.State == MatchState.Fighting);
        g.Players[1].Run.DamageDealt = 1234f;  // as if the host had been fighting
        g.Players[2].Run.Kills = 17;
        Run(s, dt, 1.5f);
        float hostDmg = g.Players[1].Run.DamageDealt, seen = c.Players[1].Run.DamageDealt;
        Console.WriteLine("  host's damage on the host {0:0}, as the client sees it {1:0}", hostDmg, seen);
        Check(seen >= 1234f && c.Players[2].Run.Kills >= 17,
            "the client sees the host's run numbers during the run (within about a second)");
        Check(!c.RunStatsReady, "live numbers don't count as the end-of-run ones");
        c.RequestPause(true);
        Run(s, dt, 0.5f);
        g.Players[1].Run.DamageDealt += 500f;
        c.RequestPause(false); Run(s, dt, 0.1f); c.RequestPause(true); Run(s, dt, 0.3f);
        float paused = c.Players[1].Run.DamageDealt;
        Check(g.Paused && Math.Abs(paused - g.Players[1].Run.DamageDealt) < 0.01f, "pausing sends everyone the exact numbers");
        c.RequestPause(false);
        Run(s, dt, 0.2f);
        s[0].Leave(""); s[1].Leave("");
    }
}
