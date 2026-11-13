using System;
using System.Collections.Generic;

namespace Arena
{
    /// One drawn link of the hunt map: right-angle lines only, from the bottom of a node down to the top of the next
    /// (x, y pairs: down, across, down).
    public class HuntLink
    {
        public int Step, From, To;     // the row and the indexes of the two nodes
        public float[] Pts;            // x0,y0, x1,y1, x2,y2, x3,y3
    }

    /// Where the lines of a hunt map run (Docs/HuntMap.md), worked out from pixel positions so the screen and the tests share it.
    /// A node with two or three links in or out gives each its own spot along its edge, and each link in a row gap turns at
    /// its own height (picked to cross the fewest others), so no two lines run over each other.
    public static class HuntMapLayout
    {
        public static List<HuntLink> Links(HuntMap map, float[] laneX, float[] rowY, float half, float bossHalf)
        {
            var all = new List<HuntLink>();
            for (int step = 0; step < HuntMap.Steps - 1; step++)
            {
                var row = map.Rows[step];
                var ls = new List<HuntLink>();
                for (int i = 0; i < row.Length; i++)
                    foreach (int j in row[i].Next)
                        ls.Add(new HuntLink { Step = step, From = i, To = j });
                if (ls.Count == 0)
                    continue;
                float[] xs = new float[ls.Count], xt = new float[ls.Count];
                float y0 = rowY[step] + Half(row[0], half, bossHalf), y1 = rowY[step + 1] - Half(map.Rows[step + 1][0], half, bossHalf);
                for (int i = 0; i < row.Length; i++)   // where each link leaves its node, left to right by where it goes
                {
                    var mine = new List<int>();
                    for (int k = 0; k < ls.Count; k++) if (ls[k].From == i) mine.Add(k);
                    mine.Sort((a, b) => laneX[map.Rows[step + 1][ls[a].To].Lane].CompareTo(laneX[map.Rows[step + 1][ls[b].To].Lane]));
                    float[] o = Spots(mine.Count, half);
                    for (int n = 0; n < mine.Count; n++) xs[mine[n]] = laneX[row[i].Lane] + o[n];
                }
                for (int j = 0; j < map.Rows[step + 1].Length; j++)   // where each link enters its node, left to right by where it comes from
                {
                    var mine = new List<int>();
                    for (int k = 0; k < ls.Count; k++) if (ls[k].To == j) mine.Add(k);
                    mine.Sort((a, b) => laneX[row[ls[a].From].Lane].CompareTo(laneX[row[ls[b].From].Lane]));
                    float[] o = Spots(mine.Count, Half(map.Rows[step + 1][j], half, bossHalf));
                    for (int n = 0; n < mine.Count; n++) xt[mine[n]] = laneX[map.Rows[step + 1][j].Lane] + o[n];
                }
                float[] yb = Bends(ls.Count, xs, xt, y0, y1);
                for (int k = 0; k < ls.Count; k++)
                {
                    ls[k].Pts = new[] { xs[k], y0, xs[k], yb[k], xt[k], yb[k], xt[k], y1 };
                    all.Add(ls[k]);
                }
            }
            return all;
        }

        static float Half(HuntNode n, float half, float bossHalf) => n.Type == HuntNodeType.Boss ? bossHalf : half;

        static float[] Spots(int k, float half)
        {
            if (k <= 1) return new float[] { 0f };
            if (k == 2) return new[] { -0.45f * half, 0.45f * half };
            var o = new float[k];
            for (int i = 0; i < k; i++) o[i] = (i - (k - 1) / 2f) * 1.3f * half / (k - 1);
            return o;
        }

        /// A height for each link's sideways run, spread evenly between the two rows, in the order that crosses least.
        static float[] Bends(int n, float[] xs, float[] xt, float y0, float y1)
        {
            float pad = Math.Max(4f, (y1 - y0) * 0.16f), lo = y0 + pad, hi = y1 - pad;
            var slot = new float[n];
            for (int k = 0; k < n; k++) slot[k] = n == 1 ? (lo + hi) / 2f : lo + (hi - lo) * k / (n - 1);
            var best = new float[n];
            for (int k = 0; k < n; k++) best[k] = slot[k];
            if (n < 2 || n > 7)
                return best;
            var perm = new int[n];
            var used = new bool[n];
            int bestCost = int.MaxValue;
            var yb = new float[n];
            Action<int> rec = null;
            rec = d =>
            {
                if (d == n)
                {
                    int cost = 0;
                    for (int a = 0; a < n; a++)
                        for (int b = a + 1; b < n && cost < bestCost; b++)
                            cost += Cost(xs[a], xt[a], y0, y1, yb[a], xs[b], xt[b], yb[b]);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        Array.Copy(yb, best, n);
                    }
                    return;
                }
                for (int q = 0; q < n; q++)
                    if (!used[q])
                    {
                        used[q] = true;
                        yb[d] = slot[q];
                        rec(d + 1);
                        used[q] = false;
                    }
            };
            rec(0);
            return best;
        }

        /// How badly two links clash: 1 for each crossing, 6 for a stretch where they run on top of each other.
        public static int Cost(float xsA, float xtA, float y0, float y1, float ybA, float xsB, float xtB, float ybB)
        {
            int n = 0;
            n += HitsAcross(xsA, xtA, ybA, xsB, y0, ybB) + HitsAcross(xsA, xtA, ybA, xtB, ybB, y1);
            n += HitsAcross(xsB, xtB, ybB, xsA, y0, ybA) + HitsAcross(xsB, xtB, ybB, xtA, ybA, y1);
            if (xsA == xsB || xtA == xtB || xsA == xtB || xtA == xsB)   // vertical pieces on the same x
            {
                if (xsA == xsB) n += Overlap(y0, ybA, y0, ybB);
                if (xtA == xtB) n += Overlap(ybA, y1, ybB, y1);
                if (xsA == xtB) n += Overlap(y0, ybA, ybB, y1);
                if (xtA == xsB) n += Overlap(ybA, y1, y0, ybB);
            }
            return n;
        }

        // A sideways run from xa to xb at height yh crossing a vertical piece at x from ya to yb (strictly inside both).
        static int HitsAcross(float xa, float xb, float yh, float x, float ya, float yb) =>
            x > Math.Min(xa, xb) + 0.01f && x < Math.Max(xa, xb) - 0.01f && yh > ya + 0.01f && yh < yb - 0.01f ? 1 : 0;

        static int Overlap(float a0, float a1, float b0, float b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 0.5f ? 6 : 0;
    }
}
