using System;

namespace Arena
{
    /// The balance test (F1 > Balance): from the match you are in, jump to a wave and play five of them with your own
    /// build while a fresh recording (format 4, Docs/BalanceTest.md) keeps every number. When the last wave clears,
    /// the report is built and the recording closes. Host only, nothing is sent over the network.
    public class BalanceRun
    {
        public int FirstWave, Waves, LastWave;
        public bool Bot, Fine, Refill, Done;
        public BalanceReport Report;
        public string Path = "";
        public float Started;
    }

    public partial class Game
    {
        public const int BalanceWaves = 5;

        /// Where test recordings go (set by the app), and the test being run or just finished.
        public string BalanceFolder;
        public BalanceRun Test;

        public bool TestRunning => Test != null && !Test.Done;

        /// Host: restart the recording as a test, then jump to its first wave. False if a test can't start here.
        public bool DevStartBalanceTest(int firstWave, bool bot, bool fine, bool refill)
        {
            if (!IsHost || State == MatchState.Idle || Practice != null || Hunt != null || BalanceFolder == null || Players.Count == 0)
                return false;
            if (Recorder != null && !Recorder.Ended)
                Recorder.End(this, "balance test started");
            firstWave = Math.Max(1, Math.Min(MaxWave - BalanceWaves + 1, firstWave));
            Test = new BalanceRun { FirstWave = firstWave, Waves = BalanceWaves, LastWave = firstWave + BalanceWaves - 1, Bot = bot, Fine = fine, Refill = refill, Started = RunTime };
            Recorder = new SessionRecorder(BalanceFolder);
            Recorder.MarkTest(firstWave, BalanceWaves, bot, fine);
            Recorder.Begin(this, firstWave);
            Test.Path = Recorder.Path;
            if (bot)
            {
                Bot = true;
                BotLevel = BotSkill.Good;
            }
            DevResetDps();
            DevJumpToWave(firstWave);
            DevUsed("balance test: waves " + firstWave + " to " + Test.LastWave + (bot ? ", bot plays" : ""));
            return true;
        }

        /// The player ended it (or the match did): close the recording with what was gathered.
        public void DevEndBalanceTest(string how)
        {
            if (!TestRunning)
                return;
            FinishTest(false, how);
        }

        void FinishTest(bool cleared, string how)
        {
            var rec = Recorder;
            if (rec == null || !rec.IsTest)
            {
                Test = null;
                return;
            }
            rec.End(this, cleared ? "balance test cleared" : how);
            Test.Report = rec.BuildReport(cleared, how);
            Test.Done = true;
            Test.Path = rec.Path;
            Recorder = null;  // later waves are not part of the test
        }

        /// Called when a wave is cleared (WaveCleared), after the recorder closed it.
        void TestWaveCleared()
        {
            if (!TestRunning)
                return;
            if (Wave >= Test.LastWave)
            {
                FinishTest(true, "");
                return;
            }
            if (Test.Refill)
                foreach (var p in Players.Values)
                    if (!p.Downed)
                        p.Hp = p.MaxHp;
        }

        /// Called when the run ends in defeat or victory before the last test wave.
        void TestRunEnded(string how)
        {
            if (TestRunning)
                FinishTest(false, how);
        }
    }
}
