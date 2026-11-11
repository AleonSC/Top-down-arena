using System.Collections.Generic;

namespace Arena
{
    /// A floor plan's box solids merged into one shape for drawing (LayoutLook.cs): the boxes cut on a grid of their
    /// own edges, the covered cells joined into non-overlapping fill boxes, and the outside edges joined into long
    /// lines. Cached by the boxes' content, so it's rebuilt only when the walls change.
    public sealed class SolidUnion
    {
        public float[] Fills = new float[0];  // x, y, w, h per box; no two overlap
        public float[] Edges = new float[0];  // x0, y0, x1, y1 per outside edge (flat or upright, x0 <= x1, y0 <= y1)
        int key, count;

        /// The union of the boxes (not discs) with Edge == edge, reusing cache while they're unchanged.
        public static SolidUnion For(Solid[] solids, bool edge, ref SolidUnion cache)
        {
            int n = 0, h = 17;
            foreach (var s in solids)
            {
                if (s.Round || s.Edge != edge)
                    continue;
                n++;
                h = h * 31 + s.Pos.X.GetHashCode();
                h = h * 31 + s.Pos.Y.GetHashCode();
                h = h * 31 + s.HW.GetHashCode();
                h = h * 31 + s.HH.GetHashCode();
            }
            if (cache != null && cache.key == h && cache.count == n)
                return cache;
            var boxes = new List<Solid>(n);
            foreach (var s in solids)
                if (!s.Round && s.Edge == edge)
                    boxes.Add(s);
            cache = Build(boxes);
            cache.key = h;
            cache.count = n;
            return cache;
        }

        static float Snap(float v) { return (float)System.Math.Round(v * 4f) / 4f; }  // quarter pixels: near-equal edges meet

        public static SolidUnion Build(List<Solid> boxes)
        {
            var u = new SolidUnion();
            if (boxes.Count == 0)
                return u;
            var xset = new SortedSet<float>();
            var yset = new SortedSet<float>();
            foreach (var b in boxes)
            {
                xset.Add(Snap(b.Pos.X - b.HW)); xset.Add(Snap(b.Pos.X + b.HW));
                yset.Add(Snap(b.Pos.Y - b.HH)); yset.Add(Snap(b.Pos.Y + b.HH));
            }
            var xs = new List<float>(xset);
            var ys = new List<float>(yset);
            int nx = xs.Count - 1, ny = ys.Count - 1;
            if (nx <= 0 || ny <= 0)
                return u;
            var inside = new bool[nx, ny];
            foreach (var b in boxes)
            {
                int i0 = xs.BinarySearch(Snap(b.Pos.X - b.HW)), i1 = xs.BinarySearch(Snap(b.Pos.X + b.HW));
                int j0 = ys.BinarySearch(Snap(b.Pos.Y - b.HH)), j1 = ys.BinarySearch(Snap(b.Pos.Y + b.HH));
                for (int i = i0; i < i1; i++)
                    for (int j = j0; j < j1; j++)
                        inside[i, j] = true;
            }

            // Fills: the covered runs of each row, a run stretched down while the rows below have the same run.
            var fills = new List<float>();
            var open = new Dictionary<long, int>();  // run (i0, i1) -> its fill's index, for the row above
            for (int j = 0; j < ny; j++)
            {
                var next = new Dictionary<long, int>();
                for (int i = 0; i < nx; )
                {
                    if (!inside[i, j]) { i++; continue; }
                    int i0 = i;
                    while (i < nx && inside[i, j])
                        i++;
                    long runKey = ((long)i0 << 32) | (uint)i;
                    int at;
                    if (open.TryGetValue(runKey, out at))
                        fills[at + 3] = ys[j + 1] - fills[at + 1];
                    else
                    {
                        at = fills.Count;
                        fills.Add(xs[i0]); fills.Add(ys[j]); fills.Add(xs[i] - xs[i0]); fills.Add(ys[j + 1] - ys[j]);
                    }
                    next[runKey] = at;
                }
                open = next;
            }

            // Edges: a cell side with inside on one side only, joined along the line.
            var edges = new List<float>();
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i < nx; )
                {
                    if (!Flip(inside, nx, ny, i, j, true)) { i++; continue; }
                    int i0 = i;
                    while (i < nx && Flip(inside, nx, ny, i, j, true))
                        i++;
                    edges.Add(xs[i0]); edges.Add(ys[j]); edges.Add(xs[i]); edges.Add(ys[j]);
                }
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j < ny; )
                {
                    if (!Flip(inside, nx, ny, i, j, false)) { j++; continue; }
                    int j0 = j;
                    while (j < ny && Flip(inside, nx, ny, i, j, false))
                        j++;
                    edges.Add(xs[i]); edges.Add(ys[j0]); edges.Add(xs[i]); edges.Add(ys[j]);
                }
            u.Fills = fills.ToArray();
            u.Edges = edges.ToArray();
            return u;
        }

        /// Whether the grid line between two cells is an outside edge: flat = the line above cell (i, j), else the
        /// line left of it.
        static bool Flip(bool[,] inside, int nx, int ny, int i, int j, bool flat)
        {
            bool a = flat ? (j > 0 && inside[i, j - 1]) : (i > 0 && inside[i - 1, j]);
            bool b = flat ? (j < ny && inside[i, j]) : (i < nx && inside[i, j]);
            return a != b;
        }
    }
}
