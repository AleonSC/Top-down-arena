using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// A report (no pass/fail): the Campaign's shown waves 1-20 built by the real wave builder, next to the chart in Docs/Approaches.md
// (the Warden's intro table, then the handover). `run.sh 1 1 chartcheck`.
static partial class Program
{
    static void ChartCheckReport()
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = typeof(Game).GetMethod("BuildWave", np);
        var singles = typeof(Game).GetField("spawnQueue", np);
        var squads = typeof(Game).GetField("squadQueue", np);
        var warden = Approaches.For(EnemyType.Boss);
        var W = new HashSet<EnemyType>(warden.Members.Select(m => m.Type));
        // What the chart says (Docs/Approaches.md): the new enemy trickling in on each wave, and the squads in use.
        string[] star = { "Rioter", "Guard", "Thug", "SecurityGuard", "-", "RiotShield", "GasGrenadier", "TaserGuard", "-", "-" };
        string[] chartSquads = { "none", "Rioter pack", "Rioter pack, Guard pair", "Yard, Guard pair", "Yard, Patrol", "Yard, Patrol", "Yard, Cell block", "Yard, Cell block", "every squad", "Riot Line + Breakout" };
        float[] leftover = { 0.40f, 0.20f, 0.08f };
        string[] leftoverTypes = { "Rioter Guard Thug SecurityGuard", "Rioter Guard Thug", "Rioter Guard" };
        var seen = new HashSet<EnemyType>();
        Console.WriteLine("  shown  track/leg  Warden%  squads in the wave                    new Warden types (singles in 2nd half only?)   chart: new | squads");
        for (int s = 1; s <= 20; s++)
        {
            Game.SeedForTests(5000 + s);
            var g = new Session().Game;
            g.LongRun = true;
            g.Wave = (s + 1) / 2;
            g.Leg = (s - 1) % 2;
            g.BossSlot = false;
            build.Invoke(g, new object[] { g.Wave });
            var q = (List<EnemyType>)singles.GetValue(g);
            var sq = (List<SquadDef>)squads.GetValue(g);
            int total = q.Count + sq.Sum(x => x.Members.Length);
            int w = q.Count(W.Contains) + sq.Sum(x => x.Members.Count(W.Contains));
            var types = new HashSet<EnemyType>(q.Concat(sq.SelectMany(x => x.Members)).Where(W.Contains));
            var fresh = types.Where(t => !seen.Contains(t)).ToList();
            string where = "";
            foreach (var t in fresh)
            {
                int first = q.IndexOf(t), last = q.LastIndexOf(t);
                bool inSquad = sq.Any(x => x.Members.Contains(t));
                where += t + (first >= 0 ? "@" + first + "-" + last + "/" + q.Count + " " : "") + (first >= 0 ? (first >= q.Count - WaveLesson.StarPicks ? " last" : " not last") : "") + (inSquad ? " (squad)" : "") + "; ";
            }
            seen.UnionWith(types);
            string sqNames = string.Join(", ", sq.GroupBy(x => x.Name).Select(x => x.Key + (x.Count() > 1 ? "x" + x.Count() : "")));
            string chart = s <= 10 ? star[s - 1] + " | " + chartSquads[s - 1] : s <= 13 ? "Warden leftovers " + (int)(leftover[s - 11] * 100) + "%: " + leftoverTypes[s - 11] + ", no squads | Hive B1.." : "Warden gone | Hive squads";
            Console.WriteLine("  {0,5}  {1,3}/{2}    {3,5:0}%   {4,-36}  {5,-46} {6}", s, g.Wave, g.Leg, total == 0 ? 0 : 100f * w / total, sqNames.Length > 36 ? sqNames.Substring(0, 36) : sqNames, where.Length > 46 ? where.Substring(0, 46) : where, chart);
        }
        Console.WriteLine("  Hive Queen faction in code: " + (Approaches.For(EnemyType.HiveQueen) != null ? "yes" : "NO (the chart's B1-B5 are placeholders; nothing starts at wave 11)"));
    }
}
