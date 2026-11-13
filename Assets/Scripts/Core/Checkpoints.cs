using System;

namespace Arena
{
    /// Lives (Docs/Checkpoints.md, Docs/RunLength.md step 5). When everyone goes down and the run has lives left (the
    /// difficulty's Lives: Easy 3, Normal 2, Hard 1, Nightmare 0), a RESPAWN / GAME OVER choice comes up before the results
    /// (Game/LivesUi.cs): Respawn replays the wave they went down on, the run exactly as it was when that wave started (its
    /// save), a life used. Game over ends the run as usual (its save goes, the loss is recorded); closing the game keeps the
    /// save at the wave's start, so Continue respawns it later. A run won without using a life is Deathless. Every boss
    /// beaten also leaves a checkpoint (`<run>.checkpoint`, SaveStore), the fallback when the wave's save is missing.
    /// Saved runs only: not Boss Hunts, Quick Play, the Practice Range, the Boss Lab or the lobby preview.
    public partial class Game
    {
        /// Lives used this run (replicated; saved).
        public int LivesUsed;

        /// The respawn choice is up (replicated), and the shown wave it replays.
        public bool RetryReady;
        public int RetryWave;

        /// Host: the run as it will be retried (the wave's start, a life used).
        RunSave retrySave;
        bool retrying;  // host: the retry is starting (the old match stops without being recorded)

        /// The run's lives (the difficulty's number; none outside saved runs).
        public int LivesMax => Hunt != null || Quick || Practice != null || ShowcaseMode || Unrecorded ? 0 : (int)Math.Round(D(DiffKnob.Lives));
        public int LivesLeft => Math.Max(0, LivesMax - LivesUsed);

        /// Won without using a life (and with lives to use).
        public bool Deathless => LivesUsed == 0 && LivesMax > 0;

        /// Host, as a run is lost: if it has a life left and a save of the wave's start (else the last checkpoint), get the
        /// respawn ready instead of ending it. The run's save becomes the retry point (so closing the game keeps it). True if
        /// a respawn is on offer.
        bool OfferRetry()
        {
            if (!IsHost || RunId == null || LivesLeft <= 0 || !RecordsRuns)
                return false;
            var cp = SaveStore.Read(RunId) ?? SaveStore.ReadCheckpoint(RunId);
            if (cp == null)
                return false;
            cp.LivesUsed = LivesUsed + 1;
            cp.SavedAt = DateTime.UtcNow;
            if (!SaveStore.Write(cp))
                return false;
            retrySave = cp;
            RetryReady = true;
            RetryWave = cp.ShownWave + 1;
            DoLog("a life left: the run can respawn at wave " + RetryWave + " (" + (LivesLeft - 1) + " more after that)");
            return true;
        }

        /// Host: the player chose Game over: the run ends for good and the results come up.
        public void GiveUpRetry() => SettleRetry();

        /// Host (Session.RetryRun): the save to restart from, once; null if there's none on offer.
        public RunSave TakeRetry()
        {
            if (!IsHost || !RetryReady || retrySave == null)
                return null;
            var s = retrySave;
            retrySave = null;
            RetryReady = false;
            retrying = true;
            return s;
        }

        /// Host, before a new match starts and as the match stops: a retry still on offer ends the run for good, unless it's
        /// being retried or the game is closing (then its save waits at the checkpoint for Continue).
        public void SettleRetry(string how = "left")
        {
            if (!IsHost || retrying || !RetryReady)
                return;
            RetryReady = false;
            retrySave = null;
            if (how == "quit")
                return;
            EndRun(false);  // RunSaves.cs: the save and checkpoint go, the loss is recorded
        }
    }
}
