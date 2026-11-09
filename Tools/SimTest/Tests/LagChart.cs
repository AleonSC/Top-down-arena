using System;
using System.IO;
using System.Linq;
using Arena;

// The lag chart (Core/LagStats.cs, LagNet.cs; drawn by Game/LagChartUi.cs). First LagStats on its own with a made-up
// clock: columns, a stall, the ping totals and p95, the 1% low, quality, freezing, marks, the summary and the CSV log.
// Then a real host and client: pings four times a second, every player's ping shared, bytes counted, snapshots noted and
// a wave start marked.
static partial class Program
{
    static void LagChartTest(float dt)
    {
        // --- LagStats alone ---------------------------------------------------------------
        var lag = new LagStats();
        string dir = Path.Combine(Path.GetTempPath(), "arena-lag-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        lag.LogPath = Path.Combine(dir, "test.csv");
        double t0 = 50.0;
        lag.Reset(t0);
        double nextSnap = t0, nextPing = t0;
        int pingsSent = 0;
        int frame = 0;
        for (double t = t0; t < t0 + 20.0; t += 1.0 / 100.0)
        {
            double s = t - t0;
            lag.AddFrame(t, frame++ % 50 == 0 ? 0.05f : 0.01f);  // 100 fps with a 50 ms hitch every 50 frames (2%)
            lag.AddBytes(t, 200, 20);
            if (t >= nextSnap)
            {
                lag.NoteSnapshot(t, 1000);
                nextSnap = t + (Math.Abs(s - 12.0) < 0.03 ? 0.4 : 0.05);  // one 400 ms stall around 12 s
            }
            if (t >= nextPing)
            {
                lag.AddPing(t, pingsSent % 10 == 9 ? 200f : 40f);  // one ping in ten at 200 ms
                pingsSent++;
                nextPing = t + 0.25;
            }
            if (Math.Abs(s - 3.0) < 0.004)
                lag.Mark(t, LagMarkKind.Wave, "W2");
        }
        double end = t0 + 20.0;
        lag.Advance(end);
        Console.WriteLine("  lag: {0} columns, {1} pings (avg {2:0.0}, p95 {3:0}, max {4:0}), stalls {5} (worst {6:0} ms), fps {7:0} (1% low {8:0}), in {9} B",
            lag.Filled, lag.PingCount, lag.AvgPing, lag.P95Ping, lag.MaxPing, lag.Stalls, lag.WorstGapMs, lag.AvgFps, lag.FpsLow1, lag.TotalIn);
        Check(lag.Filled == 80, "20 s makes 80 quarter-second columns");
        Check(lag.PingCount == pingsSent && Math.Abs(lag.MinPing - 40f) < 0.01f && Math.Abs(lag.MaxPing - 200f) < 0.01f, "every ping is counted, min and max");
        Check(Math.Abs(lag.P95Ping - 200f) < 0.01f && Math.Abs(LagStats.Percentile(new System.Collections.Generic.List<float> { 1, 2, 3, 4 }, 0.5f) - 2f) < 0.01f, "p95 and the percentile");
        Check(lag.Stalls == 1 && lag.WorstGapMs > 380f && lag.WorstGapMs < 420f, "the 400 ms snapshot gap is one stall");
        bool stallCol = Enumerable.Range(0, lag.Filled).Any(i => lag.GapAt(i) > LagStats.StallMs), normalCols = Enumerable.Range(0, lag.Filled).Count(i => lag.GapAt(i) > 0f && lag.GapAt(i) < 70f) > 60;
        Check(stallCol && normalCols, "the strip shows the stall in its column and ~50 ms gaps elsewhere");
        Check(lag.AvgFps > 88f && lag.AvgFps < 97f && lag.FpsLow1 > 15f && lag.FpsLow1 < 25f, "the average frame rate and the 1% low (the hitches)");
        Check(Math.Abs(lag.RecentKbIn - 200 * 100 / 1024f) < 0.5f, "bandwidth in KB/s");
        Check(lag.Marks.Count == 1 && lag.Marks[0].Label == "W2", "a mark is kept");
        Check(lag.Quality(end, false) == LagQuality.Good, "40 ms, no recent stall, 100 fps: Good");
        lag.AddPing(end, 170f); lag.AddPing(end + 0.1, 170f);
        for (int i = 1; i <= 12; i++) lag.AddPing(end + i * 0.25, 170f);
        Check(lag.Quality(end + 3.1, false) == LagQuality.Poor, "170 ms for three seconds: Poor");

        // Freezing holds the strip still while the totals keep counting.
        lag.Frozen = true;
        float before = lag.PingAt(lag.Filled - 1);
        int count = lag.PingCount;
        double stripEnd = lag.StripEnd;
        for (int i = 0; i < 8; i++) lag.AddPing(end + 3.5 + i * 0.25, 90f);
        lag.Advance(end + 6.0);
        Check(lag.PingAt(lag.Filled - 1) == before && lag.StripEnd == stripEnd && lag.PingCount == count + 8, "frozen: the strip holds, the totals count on");
        lag.Frozen = false;

        lag.PlayerPing[1] = 0f; lag.PlayerPing[2] = 63f;
        string sum = lag.Summary(end + 6.0, false, id => id == 1 ? "Ann" : "Bo");
        Console.Write(sum.Replace("\n", "\n    ").Insert(0, "    "));
        Console.WriteLine();
        Check(sum.Contains("Ping: min 40") && sum.Contains("Stalls") && sum.Contains("1% low") && sum.Contains("Bo 63 ms") && sum.Contains("version " + Session.ProtocolVersion),
            "the copied summary has the ping, stalls, frame rate, players and version");

        lag.Flush(end + 6.0);
        string[] csv = File.ReadAllLines(lag.LogPath);
        Console.WriteLine("  lag: the CSV has {0} lines; line 2: {1}", csv.Length, csv.Length > 1 ? csv[1] : "-");
        Check(csv[0] == LagStats.CsvHeader && csv.Length > 80 && csv.Any(l => l.EndsWith(",W2")), "the CSV log: header, a row per column, the mark");
        Directory.Delete(dir, true);

        // --- Over the network --------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank, 0, 0);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0);
        var ss = new[] { host, client };
        WaitForGame(host, client, dt);
        Run(ss, dt, 8, () => host.Game.State == MatchState.Fighting);
        Run(ss, dt, 3);
        var cl = client.Game.Lag;
        var hl = host.Game.Lag;
        Console.WriteLine("  lag net: client {0} pings (last {1:0.0} ms), {2} players' pings, snapshot {3:0} B, in {4} B; host out {5} B, players {6}; marks {7}",
            cl.PingCount, cl.LastPing, cl.PlayerPing.Count, cl.LastSnapBytes, cl.TotalIn, host.Net.BytesOut, hl.PlayerPing.Count, string.Join(" ", cl.Marks.Select(m => m.Label)));
        Check(cl.PingCount >= 3 / Game.PingEvery - 2, "the client pings four times a second");
        Check(cl.PlayerPing.Count == 2 && cl.PlayerPing.ContainsKey(1) && cl.PlayerPing.ContainsKey(2), "the host shares every player's ping (Msg.NetPings)");
        Check(hl.PlayerPing.Count == 2 && hl.PingCount > 0, "the host keeps them too, and plots the worst guest's");
        Check(cl.LastSnapBytes > 0f && cl.TotalIn > 0 && host.Net.BytesOut > 0 && client.Net.BytesIn > 0, "snapshots and bytes are counted");
        Check(cl.Marks.Any(m => m.Kind == LagMarkKind.Wave) && hl.Marks.Any(m => m.Kind == LagMarkKind.Wave), "the wave start is marked on both");
        host.Leave(""); client.Leave("");
    }
}
