using System;
using System.IO;
using System.Linq;
using Arena;

// The boss gate (Core/BossGate.cs): in the break before a boss, a gate opens in the hub; the fight waits until every
// standing player is in it (no Ready, no countdown), and bots walk in on their own.
static partial class Program
{
    static void BossGateTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var move = typeof(Game).GetMethod("MovePlayer", flags);
        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-gate-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        Session.QuickDefault = true;
        try
        {
            int port = nextPort++;
            var host = new Session();
            host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForLobby(host, client);
            PollBoth(host, client);
            client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
            host.StartMatch();
            WaitForGame(host, client, dt);
            var g = host.Game;
            var c = client.Game;
            Action keep = () =>
            {
                foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
                foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f;
            };
            bool earlyGate = false;
            Run(s, dt, 500f, () => g.GateUp, () => { keep(); earlyGate |= g.GateUp && g.Wave < 10; });
            Run(s, dt, 0.3f, null, keep);
            Console.WriteLine("  gate: wave {0}, state {1}, client sees it {2}", g.Wave, g.State, c.GateUp);
            Check(g.GateUp && c.GateUp && g.Wave == 10 && !g.BossSlot && g.State == MatchState.Intermission && !earlyGate && c.GatePos.DistanceTo(g.GatePos) < 1f,
                "the boss gate opens in the break after wave 10 (not before), on every screen");

            // Nobody in it: the wave waits, whatever the countdown and Auto-ready say.
            V2 far = g.GatePos + new V2(300f, 0f);
            Action<int, V2> put = (id, at) => { move.Invoke(g, new object[] { g.Players[id], at }); };
            put(1, far); put(2, far + new V2(0f, 80f));
            Run(s, dt, Game.IntermissionTime + 3f, null, keep);
            Check(g.State == MatchState.Intermission && g.GateUp && g.GateFill == 0f, "with nobody in it the boss waits (the countdown and Auto-ready don't start it)");

            // One of two in: still waiting, and the fill never completes.
            put(2, g.GatePos);
            float topFill = 0f;
            Run(s, dt, 4f, null, () => { keep(); topFill = Math.Max(topFill, g.GateFill); });
            int alive, inside;
            g.GateCount(out alive, out inside);
            Check(g.State == MatchState.Intermission && inside == 1 && alive == 2 && topFill == 0f, "one of two players in it: still waiting");

            // The host's player is a bot: it walks in, and the boss comes.
            g.Bot = true;
            float t = 0f;
            Run(s, dt, 20f, () => g.State == MatchState.Fighting, () => { keep(); t += dt; });
            Run(s, dt, 0.5f, null, keep);
            Console.WriteLine("  the bot walked in and the boss started after {0:0.0} s; boss slot {1}, gate up {2} (client {3})", t, g.BossSlot, g.GateUp, c.GateUp);
            Check(g.State == MatchState.Fighting && g.BossSlot && !g.GateUp && !c.GateUp, "everyone in: the boss fight starts and the gate closes on every screen; a bot walks in by itself");
            host.Leave(""); client.Leave("");
        }
        finally
        {
            Session.QuickDefault = false;
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
