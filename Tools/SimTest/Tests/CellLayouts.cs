using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Cell layouts (Core/CellLayouts.cs): a grid room's cells use hand-made layouts, not random obstacles.
static partial class Program
{
    static void CellLayoutsTest()
    {
        int bad = 0;
        foreach (var l in CellLayouts.All)
        {
            // the middle and the four door lanes are open; every wall is inside the cell
            Func<float, float, float, bool> hit = (x, y, pad) => l.Walls.Any(w => x > w[0] - pad && x < w[0] + w[2] + pad && y > w[1] - pad && y < w[1] + w[3] + pad);
            if (hit(300, 300, 40)) { bad++; Console.WriteLine("  " + l.Name + ": the chest's middle is walled"); }
            foreach (var d in new[] { new[] { 300, 0 }, new[] { 300, 600 }, new[] { 0, 300 }, new[] { 600, 300 } })
                if (hit(d[0], d[1], 50)) { bad++; Console.WriteLine("  " + l.Name + ": a door lane is walled"); }
            if (l.Walls.Any(w => w[0] < 0 || w[1] < 0 || w[0] + w[2] > 600 || w[1] + w[3] > 600)) { bad++; Console.WriteLine("  " + l.Name + ": a wall leaves its cell"); }
            if (l.Sleepers.Length == 0 || l.Traps.Length == 0) { bad++; Console.WriteLine("  " + l.Name + ": no slots"); }
            foreach (var s in l.Sleepers)
                if (hit(s[0], s[1], 24)) { bad++; Console.WriteLine("  " + l.Name + ": a sleeper slot is in a wall"); }
            foreach (var t in l.Traps)
                if (hit(t[0], t[1], 20) && t[2] == 0) { bad++; Console.WriteLine("  " + l.Name + ": a trap slot is in a wall"); }
        }
        Check(CellLayouts.All.Length == 12 && bad == 0, "twelve layouts: the middle and door lanes open, walls and slots inside the cell (" + bad + " bad)");

        var rooms = new List<ExpansionRoom>(Expansion.GridCore());
        var spot = Expansion.PlacementsFor(rooms, PieceShape.TetO).First();
        rooms.AddRange(spot.Rooms);
        var w1 = Expansion.Build(rooms, Expansion.GridCoreCuts());
        var w2 = Expansion.Build(rooms, Expansion.GridCoreCuts());
        Check(CellLayouts.OnLattice(spot.Rooms[0]) && w1.Cover.Length > 0 && w1.Cover.Length == w2.Cover.Length && w1.Cover.Where((c, i) => c.Pos.DistanceTo(w2.Cover[i].Pos) > 0.01f).Count() == 0,
            "a grid piece's cover is its cells' layouts, the same on every build (" + w1.Cover.Length + " walls)");
        var seen = new HashSet<int>();
        for (int cx = -5; cx < 15; cx++)
            for (int cy = -5; cy < 15; cy++)
                seen.Add(CellLayouts.Pick(cx, cy));
        Check(seen.Count == CellLayouts.All.Length, "every layout turns up across the lattice");

        var slots = CellLayouts.SleeperSlots(spot.Rooms);
        Check(slots.Count >= 4 && slots.All(p => spot.Rooms.Any(r => p.X > r.X && p.X < r.X1 && p.Y > r.Y && p.Y < r.Y1)), "a four-cell piece has sleeper slots in its cells (" + slots.Count + ")");
        bool clear = true;
        foreach (var p in slots)
            for (int k = 0; k < 6; k++)
                clear &= !CellLayouts.Blocked(CellLayouts.Clear(DormantGroups.Spot(p, k, 6), p), 20f);
        Check(clear, "a group of six at any slot stands clear of the walls");
    }
}
