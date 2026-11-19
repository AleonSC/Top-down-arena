using System;
using System.Collections.Generic;
using System.IO;
using Arena;

// Diag.cs: a failure the game recovers from (a save folder it can't write, a file it can't read) still reaches the
// log, once per distinct message, and a failing log never breaks the caller.
static partial class Program
{
    static void DiagTest()
    {
        var got = new List<string>();
        var oldSink = Diag.Sink;
        Diag.Reset();
        Diag.Sink = got.Add;
        try
        {
            Diag.Warn("test: something", new IOException("disk full"));
            Diag.Warn("test: something", new IOException("disk full"));
            Check(got.Count == 1 && got[0] == "test: something: IOException: disk full", "a recovered failure is logged once with its exception (" + got.Count + ")");

            // A profile saved into a "folder" that is really a file: Save fails, returns false, and says why in the log.
            string blocker = Path.Combine(Path.GetTempPath(), "arena-diag-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "not a folder");
            try
            {
                bool ok = new Profile().SaveTo(blocker);
                Check(!ok && got.Count == 2 && got[1].StartsWith("profile: couldn't save to " + blocker), "a profile that can't be saved is reported in the log (" + (got.Count > 1 ? got[1] : "nothing") + ")");
            }
            finally { File.Delete(blocker); }

            Diag.Sink = msg => throw new InvalidOperationException("log broken");
            Diag.Warn("test: another");
            Check(true, "a log that throws doesn't break the caller");
        }
        finally
        {
            Diag.Sink = oldSink;
            Diag.Reset();
        }
    }
}
