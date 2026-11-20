using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

static partial class Program
{
    /// SolidGrid (the walls by cell, for quick line tests on a big map) gives the same answers as testing every wall: the
    /// line tests SegBlocked runs and the wall clearing of SteerDir's ends (ClearOfWalls).
    static void SolidGridTest()
    {
        var rooms = GridMap(PieceShape.TriI, PieceShape.TetT, PieceShape.TetL, PieceShape.TriL, PieceShape.TetO, PieceShape.Domino, PieceShape.TetS);
        var w = Expansion.Build(rooms, Expansion.GridCoreCuts());
        var d = w.With(LayoutId.Open);
        var grid = SolidGrid.For(d);
        Check(grid != null && d.Solids.Length > 100, "a grown map's walls are filed by cell (" + d.Solids.Length + " walls, " + d.Waypoints.Length + " corners)");
        Check(SolidGrid.For(d) == grid, "built once per map");
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var s in d.Solids) { x0 = Math.Min(x0, s.Pos.X); y0 = Math.Min(y0, s.Pos.Y); x1 = Math.Max(x1, s.Pos.X); y1 = Math.Max(y1, s.Pos.Y); }
        var rng = new Random(7);
        Func<V2> anywhere = () => new V2(x0 - 300f + (float)rng.NextDouble() * (x1 - x0 + 600f), y0 - 300f + (float)rng.NextDouble() * (y1 - y0 + 600f));
        float[] radii = { 0f, 6f, 15.4f, 30f, SolidGrid.Margin };
        int lines = 0, same = 0, blocked = 0;
        for (int i = 0; i < 20000; i++)
        {
            V2 a = anywhere(), b = i % 4 == 0 ? a + V2.FromAngle((float)rng.NextDouble() * 6.3f) * 60f : i % 4 == 1 ? d.Waypoints[rng.Next(d.Waypoints.Length)] : anywhere();
            if (i % 9 == 0) b = new V2(a.X, b.Y);  // (straight up and down, and across: the cell walk's edge cases)
            if (i % 9 == 1) b = new V2(b.X, a.Y);
            float r = radii[i % radii.Length];
            bool brute = d.Solids.Any(s => !s.Edge && Game.SolidBlocks(s, a, b, r));
            lines++;
            if (grid.Blocks(a, b, r) == brute) same++;
            if (brute) blocked++;
        }
        Check(same == lines, "every line test matches testing every wall (" + same + "/" + lines + ", " + blocked + " blocked)");
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var clear = typeof(Game).GetMethod("ClearOfWalls", flags);
        var push = typeof(Game).GetMethod("PushClear", flags);
        int spots = 0, match = 0;
        for (int i = 0; i < 4000; i++)
        {
            V2 p = i % 2 == 0 ? anywhere() : d.Waypoints[rng.Next(d.Waypoints.Length)] + V2.FromAngle((float)rng.NextDouble() * 6.3f) * (float)rng.NextDouble() * 40f;
            float r = radii[i % radii.Length] - 0.5f;
            if (r < 0f) r = 0f;
            V2 got = (V2)clear.Invoke(null, new object[] { d, p, r });
            V2 want = p;
            for (int pass = 0; pass < 2; pass++)
                foreach (var s in d.Solids)
                    if (!s.Edge)
                        want = (V2)push.Invoke(null, new object[] { s, want, r + 0.5f });
            spots++;
            if (got.X == want.X && got.Y == want.Y) match++;
        }
        Check(match == spots, "clearing a spot of the walls lands where testing every wall does (" + match + "/" + spots + ")");
    }
}
