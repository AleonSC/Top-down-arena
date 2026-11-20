using System;
using System.Collections.Generic;

namespace Arena
{
    /// One hand-made layout for a single 600 px lattice cell (Docs/Mockups/cell-layouts.html): its walls, the slots where sleeping
    /// groups can stand and the slots where room traps can be. Numbers are cell-local (0..600). The chest always stands in the
    /// cell's middle (`RoomChests.CellMiddles`), so no wall touches it; the four door lanes (the middle of each side) stay open.
    public sealed class CellLayout
    {
        public string Name;
        public int[][] Walls;     // each: x, y, w, h
        public int[][] Sleepers;  // each: x, y (a sleeping group's middle)
        public int[][] Traps;     // each: x, y, wall (1: leans on a wall, so a turret is likelier there)
    }

    /// The layouts that replace the random obstacles in grid rooms (owner Nov 20): `Pick` seeds one per cell from its place on
    /// the lattice, so every machine builds the same room. Rooms off the lattice keep `Expansion.AddCover`.
    public static class CellLayouts
    {
        public const int Cell = 600;

        static int[] W(int x, int y, int w, int h) => new[] { x, y, w, h };
        static int[] P(int x, int y) => new[] { x, y };
        static int[] T(int x, int y, int wall) => new[] { x, y, wall };

        public static readonly CellLayout[] All =
        {
            new CellLayout { Name = "Open Plaza",
                Walls = new[] { W(40, 40, 80, 40), W(480, 520, 80, 40) },
                Sleepers = new[] { P(130, 130), P(470, 470) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(160, 300, 0), T(440, 300, 0) } },
            new CellLayout { Name = "Four Pillars",
                Walls = new[] { W(150, 150, 44, 44), W(406, 150, 44, 44), W(150, 406, 44, 44), W(406, 406, 44, 44) },
                Sleepers = new[] { P(120, 120), P(480, 480) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(300, 200, 0), T(300, 400, 0) } },
            new CellLayout { Name = "Vault",
                Walls = new[] { W(230, 240, 140, 20), W(230, 340, 140, 20), W(240, 230, 20, 40), W(340, 230, 20, 40), W(240, 330, 20, 40), W(340, 330, 20, 40) },
                Sleepers = new[] { P(120, 120), P(480, 120) },
                Traps = new[] { T(100, 300, 1), T(500, 300, 1), T(300, 130, 0), T(300, 470, 0) } },
            new CellLayout { Name = "Side Alcoves",
                Walls = new[] { W(140, 170, 320, 22), W(140, 408, 320, 22) },
                Sleepers = new[] { P(110, 300), P(490, 300) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(200, 300, 0), T(400, 300, 0) } },
            new CellLayout { Name = "Corner Nooks",
                Walls = new[] { W(50, 50, 90, 20), W(50, 50, 20, 90), W(460, 50, 90, 20), W(530, 50, 20, 90), W(50, 530, 90, 20), W(50, 460, 20, 90), W(460, 530, 90, 20), W(530, 460, 20, 90) },
                Sleepers = new[] { P(120, 120), P(480, 480) },
                Traps = new[] { T(300, 150, 0), T(300, 450, 0), T(150, 300, 0), T(450, 300, 0), T(300, 60, 1) } },
            new CellLayout { Name = "Crossfire",
                Walls = new[] { W(130, 200, 260, 22), W(210, 378, 260, 22) },
                Sleepers = new[] { P(120, 120), P(120, 480) },
                Traps = new[] { T(540, 100, 1), T(540, 500, 1), T(300, 250, 0), T(300, 350, 0) } },
            new CellLayout { Name = "Maze Lite",
                Walls = new[] { W(100, 120, 200, 22), W(300, 120, 22, 120), W(300, 358, 22, 120), W(300, 458, 200, 22), W(120, 250, 22, 100), W(458, 250, 22, 100) },
                Sleepers = new[] { P(110, 70), P(480, 535) },
                Traps = new[] { T(60, 300, 1), T(540, 300, 1), T(210, 300, 0), T(400, 300, 0) } },
            new CellLayout { Name = "Pillar Field",
                Walls = new[] { W(110, 110, 36, 36), W(260, 110, 36, 36), W(410, 110, 36, 36), W(110, 260, 36, 36), W(410, 260, 36, 36), W(110, 410, 36, 36), W(260, 410, 36, 36), W(410, 410, 36, 36) },
                Sleepers = new[] { P(200, 200), P(355, 355), P(200, 400) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(60, 300, 1), T(185, 330, 0), T(380, 330, 0) } },
            new CellLayout { Name = "Bunker",
                Walls = new[] { W(190, 190, 220, 20), W(190, 390, 220, 20), W(190, 190, 20, 70), W(190, 340, 20, 70), W(390, 190, 20, 70), W(390, 340, 20, 70) },
                Sleepers = new[] { P(120, 120), P(480, 120), P(120, 480) },
                Traps = new[] { T(300, 60, 1), T(300, 540, 1), T(150, 300, 0), T(450, 300, 0) } },
            new CellLayout { Name = "Barricades",
                Walls = new[] { W(60, 150, 330, 22), W(210, 230, 330, 22), W(60, 410, 330, 22) },
                Sleepers = new[] { P(120, 330), P(480, 480) },
                Traps = new[] { T(60, 60, 1), T(540, 540, 1), T(430, 330, 0), T(150, 350, 0) } },
            new CellLayout { Name = "Diagonal Cross",
                Walls = new[] { W(120, 120, 60, 60), W(420, 120, 60, 60), W(120, 420, 60, 60), W(420, 420, 60, 60), W(230, 230, 30, 30), W(340, 230, 30, 30), W(230, 340, 30, 30), W(340, 340, 30, 30) },
                Sleepers = new[] { P(300, 120), P(300, 480), P(120, 300), P(480, 300) },
                Traps = new[] { T(150, 300, 0), T(450, 300, 0), T(300, 150, 0), T(300, 450, 0) } },
            new CellLayout { Name = "Fortress Corners",
                Walls = new[] { W(40, 40, 150, 24), W(40, 40, 24, 150), W(410, 40, 150, 24), W(536, 40, 24, 150), W(40, 536, 150, 24), W(40, 410, 24, 150), W(410, 536, 150, 24), W(536, 410, 24, 150), W(240, 240, 120, 20), W(240, 340, 120, 20) },
                Sleepers = new[] { P(140, 140), P(460, 460) },
                Traps = new[] { T(300, 130, 0), T(300, 470, 0), T(130, 300, 0), T(470, 300, 0) } },
        };

        /// The lattice cell index (column, row) holding this point.
        public static void CellOf(float x, float y, out int cx, out int cy)
        {
            cx = (int)Math.Floor((x - Expansion.GridX0) / (double)Cell);
            cy = (int)Math.Floor((y - Expansion.GridY0) / (double)Cell);
        }

        /// The layout a cell uses: seeded from its place on the lattice.
        public static int Pick(int cx, int cy)
        {
            var rng = new DetRandom(cx * 73856093 ^ cy * 19349663 ^ 0x5bd1e995);
            return rng.Int(All.Length);
        }

        /// A box that sits whole cells on the lattice (what grid pieces are).
        public static bool OnLattice(ExpansionRoom r)
            => r.W > 0 && r.H > 0 && r.W % Cell == 0 && r.H % Cell == 0 && Mod(r.X - Expansion.GridX0) == 0 && Mod(r.Y - Expansion.GridY0) == 0;

        static int Mod(int v) => ((v % Cell) + Cell) % Cell;

        /// Every cell origin (top-left corner) of this box.
        public static IEnumerable<V2> CellOrigins(ExpansionRoom r)
        {
            for (int y = r.Y; y + Cell <= r.Y1; y += Cell)
                for (int x = r.X; x + Cell <= r.X1; x += Cell)
                    yield return new V2(x, y);
        }

        /// A room's walls from its cells' layouts.
        public static void AddWalls(List<Solid> to, ExpansionRoom r)
        {
            foreach (var o in CellOrigins(r))
            {
                CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                foreach (var w in All[Pick(cx, cy)].Walls)
                    to.Add(World.Box(o.X + w[0], o.Y + w[1], o.X + w[0] + w[2], o.Y + w[1] + w[3]));
            }
        }

        /// The middles of every sleeper slot in these boxes (lattice boxes only).
        public static List<V2> SleeperSlots(IList<ExpansionRoom> boxes)
        {
            var list = new List<V2>();
            foreach (var b in boxes)
                if (OnLattice(b))
                    foreach (var o in CellOrigins(b))
                    {
                        CellOf(o.X + 1, o.Y + 1, out int cx, out int cy);
                        foreach (var s in All[Pick(cx, cy)].Sleepers)
                            list.Add(new V2(o.X + s[0], o.Y + s[1]));
                    }
            return list;
        }

        /// Is this point (grown by pad) inside a wall of the layout of the cell it is in?
        public static bool Blocked(V2 p, float pad)
        {
            CellOf(p.X, p.Y, out int cx, out int cy);
            float ox = Expansion.GridX0 + cx * Cell, oy = Expansion.GridY0 + cy * Cell;
            foreach (var w in All[Pick(cx, cy)].Walls)
                if (p.X > ox + w[0] - pad && p.X < ox + w[0] + w[2] + pad && p.Y > oy + w[1] - pad && p.Y < oy + w[1] + w[3] + pad)
                    return true;
            return false;
        }

        /// Where a group's member stands: the wanted spot, or one nearer the slot's middle if a wall is there.
        public static V2 Clear(V2 want, V2 mid)
        {
            for (int s = 0; s <= 4; s++)
            {
                V2 p = mid + (want - mid) * (1f - s * 0.25f);
                if (!Blocked(p, 22f))
                    return p;
            }
            return mid;
        }
    }
}
