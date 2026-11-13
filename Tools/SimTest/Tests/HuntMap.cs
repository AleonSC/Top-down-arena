using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The hunt map (HuntMap.cs, Docs/HuntMap.md): its shape, links, node types, progress and restore, over many seeds.
static partial class Program
{
    static void HuntMapTest()
    {
        const int seeds = 3000;
        int rowsBad = 0, noOut = 0, noIn = 0, far = 0, cross = 0, rule = 0, badTypes = 0, nodes = 0, multi = 0, elites = 0, fights = 0, noChoiceRows = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var m = HuntMap.Generate(seed);
            if (m.Rows.Length != HuntMap.Steps) { rowsBad++; continue; }
            for (int st = 0; st < HuntMap.Steps; st++)
            {
                var row = m.Rows[st];
                bool edge = st == 0 || st == HuntMap.Steps - 1;
                if (edge ? row.Length != 1 || row[0].Lane != 1 : row.Length < 2 || row.Length > 3 || row.Select(n => n.Lane).Distinct().Count() != row.Length || row.Zip(row.Skip(1), (a, b) => a.Lane < b.Lane).Contains(false))
                    rowsBad++;
                if (st == HuntMap.Steps - 2 && !(row.Length == 2 && row[0].Lane == 0 && row[1].Lane == 2)) rowsBad++;
                if ((st == HuntMap.Steps - 1) != (row[0].Type == HuntNodeType.Boss)) badTypes++;
                if (st > 0 && st < HuntMap.Steps - 1 && row.All(n => n.Type == HuntNodeType.Elite)) badTypes++;
                if (st > 0 && st < HuntMap.Steps - 1 && !row.Any(n => n.Type == HuntNodeType.Skirmish)) badTypes++;
                if ((st == 0 || st == HuntMap.Steps - 2) && row.Any(n => n.Type != HuntNodeType.Skirmish && n.Type != HuntNodeType.Boss)) badTypes++;
                if (st == HuntMap.Steps / 2 - 1 && !row.Any(n => n.Type == HuntNodeType.Elite)) rule++;
                for (int i = 0; i < row.Length; i++)
                {
                    var n = row[i];
                    if (n.Step != st || n.Index != i) rowsBad++;
                    if (st < HuntMap.Steps - 1)
                    {
                        nodes++;
                        if (n.Type == HuntNodeType.Elite) elites++;
                        if (n.Next.Length == 0) noOut++;
                        if (n.Next.Length >= 2) multi++;
                        if (n.Next.Distinct().Count() != n.Next.Length) rowsBad++;
                        foreach (int j in n.Next)
                            if (Math.Abs(m.Rows[st + 1][j].Lane - n.Lane) > 1) far++;
                    }
                    else if (n.Next.Length != 0) noOut++;
                    if (st > 0 && !m.Rows[st - 1].Any(a => a.Next.Contains(i))) noIn++;
                }
                if (st < HuntMap.Steps - 1)
                {
                    if (row.All(n => n.Next.Length == 1)) noChoiceRows++;
                    foreach (var a in row) foreach (var b in row)
                        foreach (int ja in a.Next) foreach (int jb in b.Next)
                            if ((a.Lane - b.Lane) * (m.Rows[st + 1][ja].Lane - m.Rows[st + 1][jb].Lane) < 0) cross++;
                }
            }
            fights += m.Rows.Sum(r => r.Count(n => n.Fight));
        }
        Check(rowsBad == 0, "every map has 10 rows: one node in the middle first and last, the row before the boss on the outer lanes, 2-3 nodes in the rest, lanes left to right");
        Check(noOut == 0 && noIn == 0, "every node leads somewhere and every node can be reached (so every route ends at the boss)");
        Check(far == 0, "a link never goes more than one lane over");
        Check(cross == 0, "no two links of a row gap cross (3000 seeds)");
        Check(badTypes == 0 && rule == 0, "the first row and the row before the boss are Skirmishes, the boss is last, the middle row has an Elite, no row is all Elites, every row has a Skirmish");
        Console.WriteLine("  info: {0:P0} of nodes have 2+ ways on, {1:P0} are Elites, {2} rows with no choice at all", multi / (double)nodes, elites / (double)nodes, noChoiceRows);

        // The lines of the screen (HuntMapLayout.cs): right angles only, between the nodes, and none runs over another.
        float[] lx = { 100f, 230f, 360f }, ry = new float[HuntMap.Steps];
        for (int k = 0; k < ry.Length; k++) ry[k] = 60f + k * 60f;
        int diag = 0, outside = 0, overlap = 0, linkCount = 0, wrongEnds = 0, crossings = 0;
        for (int seed = 1; seed <= 600; seed++)
        {
            var m = HuntMap.Generate(seed);
            var links = HuntMapLayout.Links(m, lx, ry, 15f, 20f);
            linkCount += links.Count;
            if (links.Count != m.Rows.Take(HuntMap.Steps - 1).SelectMany(r => r).Sum(n => n.Next.Length)) wrongEnds++;
            foreach (var l in links)
            {
                var p = l.Pts;
                for (int k = 0; k < 6; k += 2) if (p[k] != p[k + 2] && p[k + 1] != p[k + 3]) diag++;
                var a = m.Rows[l.Step][l.From]; var b = m.Rows[l.Step + 1][l.To];
                float ha = a.Type == HuntNodeType.Boss ? 20f : 15f, hb = b.Type == HuntNodeType.Boss ? 20f : 15f;
                if (Math.Abs(p[0] - lx[a.Lane]) > ha || Math.Abs(p[6] - lx[b.Lane]) > hb || p[1] != ry[l.Step] + ha || p[7] != ry[l.Step + 1] - hb) wrongEnds++;
                if (p[3] <= p[1] || p[3] >= p[7]) outside++;
            }
            for (int a = 0; a < links.Count; a++)
                for (int b = a + 1; b < links.Count; b++)
                {
                    if (links[a].Step != links[b].Step) continue;
                    var A = links[a].Pts; var B = links[b].Pts;
                    int c = HuntMapLayout.Cost(A[0], A[6], A[1], A[7], A[3], B[0], B[6], B[3]);
                    if (c >= 6) overlap++; else crossings += c;
                }
        }
        Check(diag == 0 && wrongEnds == 0 && outside == 0, "every drawn link is right angles only, leaves its node's bottom edge and enters the next one's top edge (600 maps)");
        Check(overlap == 0, "no two drawn links run on top of each other");
        Console.WriteLine("  info: {0} links drawn, {1} places where two cross at a right angle", linkCount, crossings);

        // The same seed gives the same map, other seeds other maps.
        Func<HuntMap, string> Sig = m => string.Join("|", m.Rows.Select(r => string.Join(",", r.Select(n => (int)n.Type + "@" + n.Lane + ">" + string.Join("", n.Next)))));
        Check(Sig(HuntMap.Generate(42)) == Sig(HuntMap.Generate(42)), "the same seed makes the same map");
        Check(Enumerable.Range(1, 200).Select(s => Sig(HuntMap.Generate(s))).Distinct().Count() > 150, "different seeds make different maps");

        // Walking a map: picks must be links of the cleared node, a node must be picked before it is cleared, elites give clues (up to 3), the boss ends it.
        var w = HuntMap.Generate(7);
        Check(w.Step == 0 && w.Choices().SequenceEqual(new[] { 0 }) && !w.Complete && w.Current == null, "a new map starts at the first node with nothing picked");
        Check(!w.Clear(), "nothing is cleared before a pick");
        Check(!w.Pick(1) && w.Pick(0) && !w.Pick(0) && w.Current != null && w.Current.Step == 0, "only the first node can be picked, and only once");
        Check(w.Clear() && w.Step == 1 && w.Current == null, "clearing it moves to the next row");
        var rng = new Random(3);
        for (int guard = 0; guard < 20 && !w.Complete; guard++)
        {
            var ch = w.Choices();
            foreach (int bad in Enumerable.Range(0, w.Rows[w.Step].Length).Where(i => !ch.Contains(i)))
                Check(!w.Pick(bad), "a node that isn't linked from the cleared one can't be picked");
            w.Pick(ch[rng.Next(ch.Length)]);
            w.Clear();
        }
        Check(w.Complete && w.Step == HuntMap.Steps && w.Choices().Length == 0 && w.Path.Length == HuntMap.Steps, "a walk ends with the boss cleared after 10 rows");
        int elitesOnPath = Enumerable.Range(0, HuntMap.Steps).Count(st => w.Rows[st][w.Path[st]].Type == HuntNodeType.Elite);
        Check(w.Clues == Math.Min(HuntMap.MaxClues, elitesOnPath), "clues = Elites cleared, up to 3");
        Check(w.Rows[HuntMap.Steps - 1][w.Path[HuntMap.Steps - 1]].Type == HuntNodeType.Boss, "the last node on the path is the boss");

        // A route through only Elites caps the clues at 3.
        var e = HuntMap.Generate(11);
        int cleared = 0;
        while (!e.Complete)
        {
            var ch = e.Choices();
            int pick = ch.FirstOrDefault(i => e.Rows[e.Step][i].Type == HuntNodeType.Elite);
            e.Pick(ch.Contains(pick) && e.Rows[e.Step][pick].Type == HuntNodeType.Elite ? pick : ch[0]);
            e.Clear(); cleared++;
        }
        Check(e.Clues <= HuntMap.MaxClues, "clues never go past 3");

        // Save and restore: the seed and the path rebuild the map and the progress.
        var half = HuntMap.Generate(99);
        for (int k = 0; k < 4; k++) { half.Pick(half.Choices()[0]); half.Clear(); }
        var back = HuntMap.Restore(99, half.Path);
        Check(back != null && back.Step == 4 && back.Clues == half.Clues && Sig(back) == Sig(half) && back.Choices().SequenceEqual(half.Choices()), "the seed and the cleared path restore the map and the progress");
        Check(HuntMap.Restore(99, new[] { 0, 5 }) == null, "a path that doesn't fit the map is refused");
    }
}
