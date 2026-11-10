using System;
using System.IO;
using System.Linq;
using Arena;

// The balance test (F1 > Balance, Core/BalanceTest.cs): five waves with a fresh recording, then a report.
static partial class Program
{
    static void BalanceTestRun(float dt)
    {
        string dir = Path.Combine(Path.GetTempPath(), "arena-balance-" + Guid.NewGuid().ToString("N"));
        var host = HostAs(new Session { AutoStart = 1, StartWave = 1 }, nextPort++, false, "Tester", Role.Dps);
        var g = host.Game;
        g.BalanceFolder = dir;
        host.StartMatch();
        g.DevSetGod(1, true);
        Check(!g.TestRunning && g.Test == null, "no test is running at the start");
        Check(g.DevStartBalanceTest(1, true, true, true) && g.TestRunning, "a test starts from a normal run");
        Check(g.Recorder != null && g.Recorder.IsTest && g.Recorder.Path.StartsWith(dir), "its recording goes to the balance folder");
        for (int f = 0; f < 60 * 60 * 25 && !g.Test.Done; f++) Step(host, dt);
        var t = g.Test;
        Console.WriteLine("  test: done {0}, wave {1}, report tiles {2}, tables {3}", t.Done, g.Wave, t.Report != null ? t.Report.Tiles.Count : 0, t.Report != null ? t.Report.Tables.Count : 0);
        Check(t.Done && t.Report != null && t.Report.Cleared, "the five waves play out and the report is built");
        Check(g.Wave >= t.LastWave && g.Recorder == null, "the recording closes when the fifth wave clears");
        var rep = t.Report;
        Check(rep.Tiles.Count == 6 && rep.Tables.Count >= 5 && rep.Findings.Count > 0, "the report has tiles, tables and findings");
        var waves = rep.Tables.First(x => x.Name == "Waves");
        Check(waves.Rows.Count == 5, "one row per wave (" + waves.Rows.Count + ")");
        var src = rep.Tables.First(x => x.Name == "Weapons and abilities");
        Check(src.Rows.Count > 0 && src.Rows.Any(r => r[0].StartsWith("Weapon: ")), "damage is credited to the player's weapon");
        Check(rep.Tables.First(x => x.Name == "Enemies").Rows.Count > 0, "enemies are listed");
        Check(rep.AsText().Contains("Waves") && rep.AsText().Length > 200, "the summary copies out as text");

        string json = File.ReadAllText(t.Path);
        Check(json.Contains("\"balance-test\"") && json.Contains("\"damageBySource\"") && json.Contains("\"format\": 4"), "the file is format 4 and marked as a balance test");
        int open = json.Count(c => c == '{') - json.Count(c => c == '}'), openL = json.Count(c => c == '[') - json.Count(c => c == ']');
        Check(open == 0 && openL == 0, "the file's brackets balance");
        Check(json.Contains("\"end\"") && json.Contains("balance test cleared"), "the file says the test was cleared");

        // Ending it early keeps what was gathered.
        Check(g.DevStartBalanceTest(1, false, false, false) && g.TestRunning, "a second test can start");
        for (int f = 0; f < 60 * 8; f++) Step(host, dt);
        g.DevEndBalanceTest("ended by the player");
        Check(g.Test.Done && g.Test.Report != null && !g.Test.Report.Cleared && g.Recorder == null, "ending early gives a report that isn't marked cleared");
        host.Leave("");
        try { Directory.Delete(dir, true); } catch (Exception) { }
    }
}
