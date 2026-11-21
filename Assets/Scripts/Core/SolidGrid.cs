using System;
using System.Collections.Generic;

namespace Arena
{
    /// A floor plan's walls and pillars (the edge wall aside) sorted into square cells, so a line test (SegBlocked) checks
    /// only the walls in the cells the line crosses instead of every wall on the map. A grown Expanding map has about 300
    /// walls and 1000 corners, and every walking enemy tests lines against them each tick (LayoutRules.cs SteerDir).
    /// Exact: the same answer as testing every wall, for anything up to Margin wide.
    public sealed class SolidGrid
    {
        public const float Cell = 128f;
        public const float Margin = 48f;  // the widest body (r) it answers for; wider ones test every wall
        const float Slack = 2f;           // walls are filed this much wider still, for rounding where a line meets a cell corner
        const int MinSolids = 24;         // fewer walls than this: testing them all is as quick

        public Solid[] Of;  // the Solids array it was built from (a new array: a new grid)
        float x0, y0;
        int cols, rows;
        int[][] cells;
        int[] seen;
        int stamp;

        /// The grid for this plan's walls, built once per Solids array; null when the plan has too few walls to need one.
        public static SolidGrid For(LayoutDef d)
        {
            if (d.Solids == null || d.Solids.Length < MinSolids)
                return null;
            if (d.Grid == null || d.Grid.Of != d.Solids)
                d.Grid = new SolidGrid(d.Solids);
            return d.Grid;
        }

        SolidGrid(Solid[] solids)
        {
            Of = solids;
            seen = new int[solids.Length];
            float pad = Margin + Slack;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var s in solids)
            {
                if (s.Edge)
                    continue;
                float hw = (s.Round ? s.R : s.HW) + pad, hh = (s.Round ? s.R : s.HH) + pad;
                minX = Math.Min(minX, s.Pos.X - hw); maxX = Math.Max(maxX, s.Pos.X + hw);
                minY = Math.Min(minY, s.Pos.Y - hh); maxY = Math.Max(maxY, s.Pos.Y + hh);
            }
            if (minX > maxX)
            {
                cols = rows = 0;
                return;
            }
            x0 = minX;
            y0 = minY;
            cols = Math.Max(1, (int)Math.Ceiling((maxX - minX) / Cell));
            rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / Cell));
            var lists = new List<int>[cols * rows];
            for (int i = 0; i < solids.Length; i++)
            {
                var s = solids[i];
                if (s.Edge)
                    continue;
                float hw = (s.Round ? s.R : s.HW) + pad, hh = (s.Round ? s.R : s.HH) + pad;
                int cx0 = ColOf(s.Pos.X - hw), cx1 = ColOf(s.Pos.X + hw), cy0 = RowOf(s.Pos.Y - hh), cy1 = RowOf(s.Pos.Y + hh);
                for (int cy = cy0; cy <= cy1; cy++)
                    for (int cx = cx0; cx <= cx1; cx++)
                        (lists[cy * cols + cx] ?? (lists[cy * cols + cx] = new List<int>())).Add(i);
            }
            cells = new int[lists.Length][];
            for (int i = 0; i < lists.Length; i++)
                cells[i] = lists[i] == null ? Array.Empty<int>() : lists[i].ToArray();
        }

        int ColOf(float x) => Math.Max(0, Math.Min(cols - 1, (int)Math.Floor((x - x0) / Cell)));
        int RowOf(float y) => Math.Max(0, Math.Min(rows - 1, (int)Math.Floor((y - y0) / Cell)));

        /// The walls filed in p's cell (in Solids order): every wall within Margin of p is among them. Empty off the grid.
        public int[] CellAt(V2 p)
        {
            if (cols == 0 || p.X < x0 || p.Y < y0 || p.X >= x0 + cols * Cell || p.Y >= y0 + rows * Cell)
                return Array.Empty<int>();
            return cells[RowOf(p.Y) * cols + ColOf(p.X)];
        }

        /// Does the line a to b, for a body of radius r (at most Margin), run into a wall or pillar (not the edge wall)?
        /// Each wall is tested with LayoutRules.cs SolidBlocks.
        public bool Blocks(V2 a, V2 b, float r)
        {
            if (cols == 0)
                return false;
            // Clip the line to the grid: past it there are no walls to meet.
            float t0 = 0f, t1 = 1f, dx = b.X - a.X, dy = b.Y - a.Y;
            float gx1 = x0 + cols * Cell, gy1 = y0 + rows * Cell;
            if (!Clip(-dx, a.X - x0, ref t0, ref t1) || !Clip(dx, gx1 - a.X, ref t0, ref t1)
                || !Clip(-dy, a.Y - y0, ref t0, ref t1) || !Clip(dy, gy1 - a.Y, ref t0, ref t1))
                return false;
            V2 p = new V2(a.X + dx * t0, a.Y + dy * t0), q = new V2(a.X + dx * t1, a.Y + dy * t1);
            if (++stamp == int.MaxValue)
            {
                Array.Clear(seen, 0, seen.Length);
                stamp = 1;
            }
            // Walk the cells the clipped line crosses, in order (Amanatides and Woo).
            int cx = ColOf(p.X), cy = RowOf(p.Y), ex = ColOf(q.X), ey = RowOf(q.Y);
            float ldx = q.X - p.X, ldy = q.Y - p.Y;
            int sx = ldx > 0f ? 1 : -1, sy = ldy > 0f ? 1 : -1;
            float tMaxX = Math.Abs(ldx) < 1e-6f ? float.MaxValue : ((x0 + (cx + (ldx > 0f ? 1 : 0)) * Cell) - p.X) / ldx;
            float tMaxY = Math.Abs(ldy) < 1e-6f ? float.MaxValue : ((y0 + (cy + (ldy > 0f ? 1 : 0)) * Cell) - p.Y) / ldy;
            float tdX = Math.Abs(ldx) < 1e-6f ? float.MaxValue : Cell / Math.Abs(ldx);
            float tdY = Math.Abs(ldy) < 1e-6f ? float.MaxValue : Cell / Math.Abs(ldy);
            for (int steps = cols + rows + 2; steps > 0; steps--)
            {
                if (CellBlocks(cy * cols + cx, a, b, r))
                    return true;
                if (cx == ex && cy == ey)
                    return false;
                if (tMaxX < tMaxY)
                {
                    cx += sx;
                    tMaxX += tdX;
                }
                else
                {
                    cy += sy;
                    tMaxY += tdY;
                }
                if (cx < 0 || cx >= cols || cy < 0 || cy >= rows)
                    break;
            }
            return CellBlocks(ey * cols + ex, a, b, r);  // (rounding walked off the line: its end's cell all the same)
        }

        bool CellBlocks(int cell, V2 a, V2 b, float r)
        {
            foreach (int i in cells[cell])
            {
                if (seen[i] == stamp)
                    continue;
                seen[i] = stamp;
                if (Game.SolidBlocks(Of[i], a, b, r))
                    return true;
            }
            return false;
        }

        // Liang-Barsky: keeps t0..t1 inside one edge of the box; false when nothing is left.
        static bool Clip(float den, float num, ref float t0, ref float t1)
        {
            if (Math.Abs(den) < 1e-9f)
                return num >= 0f;
            float t = num / den;
            if (den < 0f)
            {
                if (t > t1)
                    return false;
                if (t > t0)
                    t0 = t;
            }
            else
            {
                if (t < t0)
                    return false;
                if (t < t1)
                    t1 = t;
            }
            return true;
        }
    }
}
