using System;
using System.Linq;
using Arena;

// The between-waves ready system and the wave's numbers and awards (Core/WaveReady.cs).
static partial class Program
{
    static void WaveReadyTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        var c = s[1].Game;
        Run(s, dt, 15, () => g.State == MatchState.Fighting);
        // Pretend the host did the work this wave: more kills and damage than the client.
        g.Players[1].Run.Kills += 10; g.Players[1].Run.DamageDealt += 500f;
        g.Players[2].Run.Kills += 3; g.Players[2].Run.DamageDealt += 100f;
        Run(s, dt, 60, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        Check(g.State == MatchState.Intermission, "the wave is cleared");
        Run(s, dt, 0.5f);
        var rep = g.WaveReport;
        Check(rep != null && rep.Wave == g.Wave && rep.Lines.Count == 2, "the host has the wave's summary: " + (rep == null ? "none" : rep.Lines.Count + " lines"));
        var l1 = rep.For(1); var l2 = rep.For(2);
        Check(l1.Kills >= 10 && l1.Damage >= 500f && l2.Kills >= 3, "each player's kills and damage are this wave's: " + l1.Kills + "/" + l2.Kills);
        Check((l1.Awards & WaveAward.MostKills) != 0 && (l1.Awards & WaveAward.MostDamage) != 0 && (l2.Awards & (WaveAward.MostKills | WaveAward.MostDamage)) == 0,
            "the player with the most kills and damage gets those awards, and the other doesn't");
        Check(c.WaveReport != null && c.WaveReport.Wave == rep.Wave && c.WaveReport.For(1).Awards == l1.Awards, "the client gets the same summary and awards");
        Check(!g.Players[1].Ready && !g.Players[2].Ready, "nobody is ready as a break begins");

        // Everyone on auto-ready (the default): the break runs on its usual countdown.
        Run(s, dt, 15, () => g.State == MatchState.Fighting, () => { foreach (var p in g.Players.Values) p.PendingPicks = 0; });
        Check(g.State == MatchState.Fighting, "with everyone on auto-ready the next wave starts by itself");

        // Switch to manual: the next break waits.
        g.SetMyAutoReady(false); c.SetMyAutoReady(false);
        Run(s, dt, 0.5f);
        Check(!g.Players[1].AutoReady && !g.Players[2].AutoReady && !c.Players[1].AutoReady && !c.Players[2].AutoReady, "Auto-ready off reaches the host and the other player");
        Run(s, dt, 60, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        Run(s, dt, 15, () => !g.AnyonePicking());
        float t0 = g.StateTimer;
        Run(s, dt, 6f);
        Check(g.State == MatchState.Intermission && Math.Abs(g.StateTimer - t0) < 0.01f, "nobody ready: the break waits (timer still {0:0.0})".Replace("{0:0.0}", g.StateTimer.ToString("0.0")));
        g.SetMyReady(true);
        Run(s, dt, 0.5f);
        Check(g.Players[1].Ready && c.Players[1].Ready, "the host's Ready reaches the client");
        Run(s, dt, 6f);
        Check(g.State == MatchState.Intermission, "one of two ready: still waiting");
        c.SetMyReady(true);
        Run(s, dt, 0.5f);
        Check(g.AllReady() && g.State == MatchState.Intermission, "both ready: the short countdown runs");
        Run(s, dt, Game.ReadyCountdown + 1.5f, () => g.State == MatchState.Fighting);
        Check(g.State == MatchState.Fighting, "the next wave starts within the ready countdown");
        Check(!g.Players[1].Ready || g.Wave > rep.Wave, "(a new wave is on)");

        // Ready can be taken back while the others wait.
        Run(s, dt, 60, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        Run(s, dt, 15, () => !g.AnyonePicking());
        g.SetMyReady(true); Run(s, dt, 0.3f);
        g.SetMyReady(false); Run(s, dt, 0.3f);
        Check(!g.Players[1].Ready && !c.Players[1].Ready, "Ready can be undone");
        // One on auto-ready counts as ready.
        c.SetMyAutoReady(true); g.SetMyReady(true);
        Run(s, dt, Game.ReadyCountdown + 1.5f, () => g.State == MatchState.Fighting);
        Check(g.State == MatchState.Fighting, "one on auto-ready and the other pressing Ready starts the wave");
        s[0].Leave(""); s[1].Leave("");
    }
}
