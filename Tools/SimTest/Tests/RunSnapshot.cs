using System;
using System.IO;
using System.Linq;
using System.Text;
using Arena;

// Run snapshots (Core/RunSnapshot.cs, the F6 / F7 dev tool): a snapshot loaded back plays on exactly as the run did from there.
static partial class Program
{
    /// Everything that moves, as one number: players, enemies, bullets, score, wave, state.
    static long SnapPrint(Game g)
    {
        unchecked
        {
            long h = 17;
            Func<float, long> f = x => (long)Math.Round(x * 100f);
            h = h * 31 + g.Wave;
            h = h * 31 + (int)g.State;
            h = h * 31 + g.Score;
            foreach (var p in g.Players.Values)
                h = ((h * 31 + p.Id) * 31 + f(p.Pos.X)) * 31 + f(p.Pos.Y) + f(p.Hp) * 7 + p.Level;
            foreach (var e in g.Enemies.Values.OrderBy(e => e.Id))
                h = (((h * 31 + e.Id) * 31 + (int)e.Type) * 31 + f(e.Pos.X)) * 31 + f(e.Pos.Y) + f(e.Hp) * 7;
            foreach (var b in g.Bullets.Values.OrderBy(b => b.Id))
                h = (h * 31 + b.Id) * 31 + f(b.Pos.X) + f(b.Pos.Y) * 3;
            return h;
        }
    }

    static long[] SnapTrace(Session s, float dt, int ticks)
    {
        var t = new long[ticks];
        for (int i = 0; i < ticks; i++)
        {
            Step(s, dt);
            t[i] = SnapPrint(s.Game);
        }
        return t;
    }

    static void RunSnapshotTest(float dt)
    {
        RunSnapshotTest(dt, false);
        RunSnapshotTest(dt, true);
    }

    static void RunSnapshotTest(float dt, bool campaign)
    {
        Console.WriteLine(campaign ? "  Campaign (Expanding arena):" : "  Random waves:");
        Game.SeedForTests(campaign ? 4343 : 4242);
        var s = HostAs(new Session { AutoStart = 1, StartWave = 3 }, nextPort++, true, "Solo", Role.Dps);
        if (campaign)
            s.Diff.Set(DiffKnob.Campaign, 1f);
        s.StartMatch();
        var g = s.Game;
        Run(new[] { s }, dt, 30f, () => g.State == MatchState.Fighting && g.Enemies.Count >= 4);
        Run(new[] { s }, dt, 3f);
        Check(g.State == MatchState.Fighting && g.Enemies.Count > 0, "a solo run is mid-wave (" + g.Enemies.Count + " enemies)");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var bytes = RunSnapshot.Capture(g, "test");
        long ms = watch.ElapsedMilliseconds;
        Check(bytes.Length > 1000, "a snapshot is taken (" + (bytes.Length / 1024) + " KB in " + ms + " ms)");

        long before = SnapPrint(g);
        int enemies = g.Enemies.Count, wave = g.Wave, score = g.Score;
        const int ticks = 60 * 8;
        var first = SnapTrace(s, dt, ticks);
        Check(first.Distinct().Count() > 10, "the run moves on after the snapshot");

        string notes;
        string err = RunSnapshot.Restore(g, bytes, out notes);
        Check(err == null, "the snapshot loads" + (err != null ? ": " + err : notes.Length > 0 ? " (" + notes + ")" : ""));
        Check(SnapPrint(g) == before && g.Enemies.Count == enemies && g.Wave == wave && g.Score == score, "loading puts the run back exactly as it was");
        var second = SnapTrace(s, dt, ticks);
        int diverge = -1;
        for (int i = 0; i < ticks; i++)
            if (first[i] != second[i]) { diverge = i; break; }
        Check(diverge < 0, "from there it plays the same 8 s again, tick for tick" + (diverge >= 0 ? " (split at tick " + diverge + ")" : ""));

        // A second load of the same bytes works too (the snapshot isn't used up), and a kill-all is undone.
        g.DevKillAll(g.MyId);
        Step(s, dt);
        RunSnapshot.Restore(g, bytes, out notes);
        Check(SnapPrint(g) == before, "the same snapshot loads again (enemies killed since come back)");
        Check(g.Players.Values.All(p => ReferenceEquals(g.Players[p.Id], p)) && ReferenceEquals(g.Me, g.Players[g.MyId]), "your player is found again");

        // Files: saved, listed with their header, loaded, the oldest beyond the cap removed.
        string old = Profile.Folder;
        Profile.Folder = Path.Combine(Path.GetTempPath(), "arena-snaptest-" + Environment.TickCount);
        try
        {
            string path = RunSnapshot.SaveFile(g, "Wave " + g.ShownWave);
            var list = RunSnapshot.List();
            Check(path != null && list.Count == 1 && list[0].Wave == g.ShownWave && list[0].Enemies == enemies && list[0].Name == "Wave " + g.ShownWave,
                "a snapshot file is saved and listed with its wave and enemy count");
            Check(RunSnapshot.Rename(path, "  Boss bug repro ") && RunSnapshot.List()[0].Name == "Boss bug repro" && RunSnapshot.List()[0].Wave == list[0].Wave
                && !RunSnapshot.Rename(path, "   "), "a snapshot can be renamed (a blank name is refused)");
            Step(s, dt);
            err = RunSnapshot.LoadFile(g, path, out notes);
            Check(err == null && SnapPrint(g) == before, "it loads from the file (after the rename too)");
            File.WriteAllBytes(Path.Combine(RunSnapshot.Folder, "junk" + RunSnapshot.Ext), Encoding.UTF8.GetBytes("nope"));
            Check(RunSnapshot.List().Count == 1, "a file that isn't a snapshot isn't listed");
            err = RunSnapshot.LoadFile(g, Path.Combine(RunSnapshot.Folder, "junk" + RunSnapshot.Ext), out notes);
            Check(err != null && SnapPrint(g) == before, "a bad file fails cleanly and leaves the run alone");
        }
        finally
        {
            try { Directory.Delete(Profile.Folder, true); } catch (Exception) { }
            Profile.Folder = old;
        }
        Check(RunSnapshot.CantLoad(g, s) == null, "a solo host can load");
        s.Leave("");
    }
}
