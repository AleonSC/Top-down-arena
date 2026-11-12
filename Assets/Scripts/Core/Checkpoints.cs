using System;

namespace Arena
{
    /// Checkpoints and lives (Docs/Checkpoints.md, Docs/RunLength.md step 5). Every boss beaten leaves a checkpoint: the
    /// save written as the next wave starts is also kept as `<run>.checkpoint` (SaveStore). When everyone goes down and the
    /// run has lives left (the difficulty's Lives: Easy 3, Normal 2, Hard 1, Nightmare 0), the game over screen offers
    /// RETRY FROM WAVE n: the run comes back exactly as it was at the checkpoint, a life used and the score cut by a
    /// quarter. Leaving instead ends the run as usual (its save and checkpoint go, the loss is recorded); closing the game
    /// keeps the save at the checkpoint, so Continue retries it later. A run won without using a life is Deathless.
    /// Saved runs only: not Boss Hunts, the Practice Range, the Boss Lab or the lobby preview.
    public partial class Game
    {
        public const float LifeScoreCut = 0.75f;  // the score kept for each life used

        /// Lives used this run (replicated; saved).
        public int LivesUsed;

        /// The game over screen can retry from the checkpoint (replicated), and the shown wave it starts at.
        public bool RetryReady;
        public int RetryWave;

        /// Host: the run as it will be retried (the checkpoint, a life used, the score cut).
        RunSave retrySave;
        bool retrying;  // host: the retry is starting (the old match stops without being recorded)

        /// The run's lives (the difficulty's number; none outside saved runs).
        public int LivesMax => Hunt != null || Quick || Practice != null || ShowcaseMode || Unrecorded ? 0 : (int)Math.Round(D(DiffKnob.Lives));
        public int LivesLeft => Math.Max(0, LivesMax - LivesUsed);

        /// Won without using a life (and with lives to use).
        public bool Deathless => LivesUsed == 0 && LivesMax > 0;

        /// Host, as a run is lost: if it has a life left and a checkpoint, get the retry ready instead of ending it. The
        /// run's save becomes the retry point (so closing the game keeps it). True if a retry is on offer.
        bool OfferRetry()
        {
            if (!IsHost || RunId == null || LivesLeft <= 0 || !RecordsRuns)
                return false;
            var cp = SaveStore.ReadCheckpoint(RunId);
            if (cp == null)
                return false;
            cp.LivesUsed = LivesUsed + 1;
            cp.Score = (int)Math.Round(cp.Score * LifeScoreCut);
            cp.SavedAt = DateTime.UtcNow;
            if (!SaveStore.Write(cp))
                return false;
            retrySave = cp;
            RetryReady = true;
            RetryWave = cp.ShownWave + 1;
            DoLog("a life left: the run can be retried from wave " + RetryWave + " (" + (LivesLeft - 1) + " more after that)");
            return true;
        }

        /// Host (Session.RetryRun): the checkpoint to restart from, once; null if there's none on offer.
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
