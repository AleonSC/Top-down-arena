using System;
using System.Collections;
using System.IO;
using System.Linq;
using Arena;

// Checkpoints and lives (Core/Checkpoints.cs, Docs/Checkpoints.md): a boss beaten leaves a checkpoint; a wipe with a life
// left offers a retry from it (a life used, the score cut); leaving ends the run; out of lives it's a plain game over.
static partial class Program
{
    static void CheckpointsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        string folder = Path.Combine(Path.GetTempPath(), "arena-checkpoints-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        SaveStore.Folder = folder;
        try
        {
            // The numbers: lives by difficulty, saved with the run, shown in the Continue list.
            Check(Difficulty.Of(DifficultyLevel.Easy)[DiffKnob.Lives] == 3f && Difficulty.Of(DifficultyLevel.Normal)[DiffKnob.Lives] == 2f
                && Difficulty.Of(DifficultyLevel.Hard)[DiffKnob.Lives] == 1f && Difficulty.Of(DifficultyLevel.Nightmare)[DiffKnob.Lives] == 0f,
                "lives by difficulty: Easy 3, Normal 2, Hard 1, Nightmare 0");
            var made = new RunSave { Id = "lv", Wave = 12, LivesUsed = 1 };
            made.Seats.Add(new SeatSave { Name = "A" });
            var back = RunSave.Parse(made.ToText(), "lv");
            Console.WriteLine("  save: lives used {0}; '{1}'", back?.LivesUsed, back?.Summary);
            Check(back != null && back.LivesUsed == 1 && back.Summary.EndsWith("1 life"), "a save keeps the lives used, and its row shows the lives left");

            // A run from wave 4: the boss after wave 5 leaves a checkpoint as wave 6 starts.
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 4 }, port, false, "Host", Role.Tank);
            var client = JoinAs(port, false, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game; var cg = client.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            var spawnQ = (IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            var squadQ = (IList)typeof(Game).GetField("squadQueue", flags).GetValue(g);
            Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
            Action Clear = () =>
            {
                NoPicks();
                if (g.PathOffer != null) { g.ChoosePath(0); cg.ChoosePath(0); }
                spawnQ.Clear(); squadQ.Clear();
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
            };
            Check(g.LivesMax == 2 && g.LivesLeft == 2 && cg.LivesLeft == 2, "a Normal run has 2 lives (on every game)");
            Check(g.RunId != null && SaveStore.ReadCheckpoint(g.RunId) == null, "no checkpoint before the first boss");
            g.Score = 1000;  // (so the score cut shows)
            Run(s, dt, 120f, () => g.State == MatchState.Fighting && g.Wave == 6, Clear);
            var cp = SaveStore.ReadCheckpoint(g.RunId);
            Console.WriteLine("  checkpoint: wave {0}, boss beaten {1}, score {2}", cp?.Wave, cp?.BossSlot, cp?.Score);
            Check(cp != null && cp.Wave == 5 && cp.BossSlot && cp.Score >= 1000, "beating the boss leaves a checkpoint (the start of the wave after it)");

            // After the checkpoint: an extra upgrade and score, then everyone goes down.
            int cpScore = cp.Score;
            var friend = g.Players[2];
            int fortitude = Array.FindIndex(Upgrades.All, u => u.Name == "Fortitude");
            int takenAtCp = friend.Taken.Count;
            g.GrantUpgrade(2, fortitude);
            Action Wipe = () =>
            {
                g.DevSetGod(1, false); g.DevSetGod(2, false);
                for (int i = 0; i < 3; i++) spawnQ.Add(EnemyType.Grunt);  // (the wave isn't over, so nobody gets back up)
                foreach (var p in g.Players.Values) { p.Hp = 0f; p.Downed = true; p.ReviveToken = false; p.SecondWind = false; }
            };
            Wipe();
            Run(s, dt, 3f, () => g.State == MatchState.GameOver);
            Run(s, dt, 0.5f);
            var onDisk = SaveStore.List().FirstOrDefault(x => x.Id == g.RunId);
            Console.WriteLine("  wipe 1: state {0}, retry {1} from wave {2} (client {3}, {4}); save on disk: lives used {5}, score {6} (checkpoint {7})",
                g.State, g.RetryReady, g.RetryWave, cg.RetryReady, cg.RetryWave, onDisk?.LivesUsed, onDisk?.Score, cpScore);
            Check(g.State == MatchState.GameOver && g.RetryReady && cg.RetryReady && g.RetryWave == 6 && cg.RetryWave == 6, "a wipe with a life left offers a retry from wave 6 (every game knows)");
            Check(onDisk != null && onDisk.LivesUsed == 1 && onDisk.Score == (int)Math.Round(cpScore * Game.LifeScoreCut) && onDisk.Wave == 5,
                "the save waits at the checkpoint with a life used and the score cut, so Continue can retry it later");

            // Retry: back at wave 6 with the checkpoint's build, a life used, on both games.
            string runId = g.RunId;
            host.RetryRun();
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
            Run(s, dt, 0.5f, null, NoPicks);
            friend = g.Players.Values.First(p => p.Name == "Friend");
            Console.WriteLine("  retried: wave {0} (client {1}), lives used {2} (client {3}), left {4}; friend's upgrades {5} (at the checkpoint {6}); same run {7}",
                g.Wave, cg.Wave, g.LivesUsed, cg.LivesUsed, g.LivesLeft, friend.Taken.Count, takenAtCp, g.RunId == runId);
            Check(g.Running && g.Wave == 6 && cg.Wave == 6 && g.LivesUsed == 1 && cg.LivesUsed == 1 && g.LivesLeft == 1, "retrying plays wave 6 again with a life used, on every game");
            Check(friend.Taken.Count == takenAtCp && g.RunId == runId && g.Score == (int)Math.Round(cpScore * Game.LifeScoreCut), "the run is back as it was at the checkpoint (the later upgrade is gone), the same run, the score cut");

            // Second wipe: the last life. Third: no lives left, a plain game over: the save and its checkpoint go.
            Wipe();
            Run(s, dt, 3f, () => g.State == MatchState.GameOver);
            Run(s, dt, 0.3f);
            Check(g.RetryReady && g.LivesLeft == 1, "the second wipe offers the last life");
            host.RetryRun();
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            Run(s, dt, 30f, () => g.State == MatchState.Fighting, NoPicks);
            Check(g.LivesUsed == 2 && g.LivesLeft == 0, "the last life is used");
            Wipe();
            Run(s, dt, 3f, () => g.State == MatchState.GameOver);
            Run(s, dt, 0.3f);
            Console.WriteLine("  out of lives: retry {0}, saves {1}, checkpoint {2}", g.RetryReady, SaveStore.List().Count, SaveStore.ReadCheckpoint(runId) != null);
            Check(!g.RetryReady && !cg.RetryReady && SaveStore.List().All(x => x.Id != runId) && SaveStore.ReadCheckpoint(runId) == null,
                "out of lives it's a plain game over: the save and its checkpoint are gone");
            host.Leave(""); client.Leave("");

            // A retry left untaken: Play again (a new run) ends the old one for good.
            port = nextPort++;
            host = HostAs(new Session { AutoStart = 2, StartWave = 4 }, port, false, "Host", Role.Tank);
            client = JoinAs(port, false, "Friend", Role.Dps);
            s = new[] { host, client };
            WaitForGame(host, client, dt);
            g = host.Game; cg = client.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            spawnQ = (IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            squadQ = (IList)typeof(Game).GetField("squadQueue", flags).GetValue(g);
            Run(s, dt, 120f, () => g.State == MatchState.Fighting && g.Wave == 6, Clear);
            runId = g.RunId;
            Wipe();
            Run(s, dt, 3f, () => g.State == MatchState.GameOver);
            Run(s, dt, 0.3f);
            Check(g.RetryReady, "(a second run offers its retry)");
            host.StartMatch();  // Play again
            Run(s, dt, 1f);
            Console.WriteLine("  play again instead: old save kept {0}, checkpoint kept {1}; new run {2}", SaveStore.List().Any(x => x.Id == runId), SaveStore.ReadCheckpoint(runId) != null, g.RunId != runId);
            Check(SaveStore.List().All(x => x.Id != runId) && SaveStore.ReadCheckpoint(runId) == null && g.RunId != runId && !g.RetryReady,
                "Play again instead of retrying ends the old run for good (its save and checkpoint go)");
            host.Leave(""); client.Leave("");

            // Deathless, and no lives outside saved runs.
            var won = new Game(new Session());
            var hunt = new Game(new Session()) { Hunt = Hunts.All[0] };
            bool deathless0 = won.Deathless;
            won.LivesUsed = 1;
            Check(deathless0 && !won.Deathless && hunt.LivesMax == 0, "a run is Deathless until a life is used; a Boss Hunt has no lives");
        }
        finally
        {
            SaveStore.Folder = null;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
