using System;
using System.Collections.Generic;

namespace Arena
{
    /// Where a failure the game recovers from is reported (file, save and sound errors that fall back quietly), so it
    /// still shows in Player.log. The front end sets Sink (ArenaApp: Debug.LogWarning); without one nothing is written.
    /// Each distinct message is reported once, so a failure that repeats every frame or file doesn't flood the log.
    public static class Diag
    {
        public static Action<string> Sink;
        static readonly HashSet<string> seen = new HashSet<string>();

        /// Reports `what` (a short description of what was being done) with the exception's message. Thread-safe
        /// (the sound bank builds on a worker thread); never throws.
        public static void Warn(string what, Exception ex = null)
        {
            string msg = ex == null ? what : what + ": " + ex.GetType().Name + ": " + ex.Message;
            lock (seen)
                if (!seen.Add(msg))
                    return;
            try { Sink?.Invoke(msg); }
            catch (Exception) { }  // (the log itself failing must never break the caller)
        }

        /// How many distinct messages were reported (tests).
        public static int Count { get { lock (seen) return seen.Count; } }

        /// Forgets what was reported (tests).
        public static void Reset() { lock (seen) seen.Clear(); }
    }
}
