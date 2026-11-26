using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A piece (Docs/GridPieces.md): a polyomino of 1 to 4 cells (a cell is a 6 x 6 tile room, Expansion.GridCell). New values go at the end.
    public enum PieceShape : byte { Mono, Domino, TriI, TriL, TetI, TetO, TetT, TetS, TetL }

    /// One legal placement of a piece: the open room it builds (its boxes, Price 0), which of the piece's turns and mirrors it is, where its cell box starts,
    /// and the door spot it hangs off.
    public struct Placement
    {
        public PieceShape Shape;
        public int Orient;     // index into Expansion.PieceShapeOrients(piece)
        public int X, Y;       // the top-left of the piece's cell box, world px, on the lattice
        public List<ExpansionRoom> Rooms;
        public byte Side;      // the parent's side it hangs off: 0 top, 1 bottom, 2 left, 3 right
        public int SX, SY;     // the door spot's middle (world px)
        public V2 Mid;         // the middle of the piece's cell box
    }

    /// A cell just outside a built room's wall on a lattice line: where a piece's cell can sit against that wall.
    public struct GridSpot
    {
        public byte Side;
        public int X, Y;       // the cell's top-left, world px
        public int SX, SY;     // the middle of the cell's edge on the wall
    }

    public static partial class Expansion
    {
        static readonly int[][][] pieceShapeBase =
        {
            new[] { new[] { 0, 0 } },
            new[] { new[] { 0, 0 }, new[] { 1, 0 } },
            new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 } },
            new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 1, 1 } },
            new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 } },
            new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 } },
            new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 1, 1 } },
            new[] { new[] { 1, 0 }, new[] { 2, 0 }, new[] { 0, 1 }, new[] { 1, 1 } },
            new[] { new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 1, 2 } },
        };

        static List<int[][]>[] pieceShapeOrients;

        public static string PieceShapeName(PieceShape p)
        {
            switch (p)
            {
                case PieceShape.Mono: return "Single";
                case PieceShape.Domino: return "Domino";
                case PieceShape.TriI: return "Line of 3";
                case PieceShape.TriL: return "Corner";
                case PieceShape.TetI: return "Line of 4";
                case PieceShape.TetO: return "Square";
                case PieceShape.TetT: return "T";
                case PieceShape.TetS: return "S";
                default: return "L";
            }
        }

        public static int PieceShapeCells(PieceShape p) => pieceShapeBase[(int)p].Length;

        /// Every distinct turn and mirror of a piece (a Square has 1, a Line 2, a T 4, an L 8): each a list of {col, row} cells with the box's corner at {0, 0}.
        /// The same order everywhere, so an orientation is a small index.
        public static List<int[][]> PieceShapeOrients(PieceShape p)
        {
            if (pieceShapeOrients == null)
            {
                var all = new List<int[][]>[pieceShapeBase.Length];
                for (int i = 0; i < all.Length; i++)
                {
                    all[i] = new List<int[][]>();
                    var keys = new HashSet<string>();
                    for (int mirror = 0; mirror < 2; mirror++)
                        for (int turn = 0; turn < 4; turn++)
                        {
                            var cells = new List<int[]>();
                            foreach (var c in pieceShapeBase[i])
                            {
                                int x = mirror == 1 ? -c[0] : c[0], y = c[1];
                                for (int t = 0; t < turn; t++) { int nx = -y; y = x; x = nx; }
                                cells.Add(new[] { x, y });
                            }
                            int mx = int.MaxValue, my = int.MaxValue;
                            foreach (var c in cells) { mx = Math.Min(mx, c[0]); my = Math.Min(my, c[1]); }
                            foreach (var c in cells) { c[0] -= mx; c[1] -= my; }
                            cells.Sort((a, b) => a[1] != b[1] ? a[1].CompareTo(b[1]) : a[0].CompareTo(b[0]));
                            var key = string.Join(";", cells.ConvertAll(c => c[0] + "," + c[1]));
                            if (keys.Add(key))
                                all[i].Add(cells.ToArray());
                        }
                }
                pieceShapeOrients = all;
            }
            return pieceShapeOrients[(int)p];
        }

        /// A piece orientation's size in cells {cols, rows}.
        public static int[] PieceShapeSize(int[][] cells)
        {
            int w = 0, h = 0;
            foreach (var c in cells) { w = Math.Max(w, c[0] + 1); h = Math.Max(h, c[1] + 1); }
            return new[] { w, h };
        }

        static bool[,] PieceShapeMask(int[][] cells)
        {
            int n = GridCell / Step, w = PieceShapeSize(cells)[0], h = PieceShapeSize(cells)[1];
            var m = new bool[w * n, h * n];
            foreach (var c in cells)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        m[c[0] * n + i, c[1] * n + j] = true;
            return m;
        }

        /// The open room this piece builds with its cell box's corner at (x, y) (on the lattice): its boxes, the first holding the room, Price 0, if it is legal
        /// there (no overlap, a door's worth of shared wall with the connected map, the size caps), else null with `why`.
        public static List<ExpansionRoom> PieceShapeRooms(IList<ExpansionRoom> built, PieceShape piece, int orient, int x, int y, out string why)
        {
            why = null;
            var orients = PieceShapeOrients(piece);
            if (orient < 0 || orient >= orients.Count) { why = "No such turn"; return null; }
            if (GridMod(x, GridX0) != 0 || GridMod(y, GridY0) != 0) { why = "Off the grid"; return null; }
            if (BoughtCount(built) >= MaxRooms) { why = "The map is full"; return null; }
            var made = FromTiles(built, PieceShapeMask(orients[orient]), x, y, PathId.None, out why);
            if (made == null)
                return null;
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Price = 0;
                q.Surprise = 0;
                made[i] = q;
            }
            return made;
        }

        /// The cells just outside each built room's lattice walls where a piece's cell can sit: a door spot per cell along a wall.
        public static List<GridSpot> GridSpots(IList<ExpansionRoom> built)
        {
            var res = new List<GridSpot>();
            var seen = new HashSet<long>();
            var linked = ConnectedAreas(built);
            for (int a = 1; a <= built.Count; a++)
            {
                if (built[a - 1].Floating && !linked[a])
                    continue;  // (a sealed floating room, like a waiting mini-boss room, has no door spots until a piece reaches it)
                if (IsHospital(built[a - 1]))
                    continue;  // (a Field Hospital's cell is there for a minute: nothing is built off it)
                BoxOf(built, a, out int x0, out int y0, out int x1, out int y1);
                int pedestals = PedestalShop(built, a) ? ShopPedestalWalls(built, a) : 0;
                for (int side = 0; side < 4; side++)
                {
                    if (pedestals == 1 && side >= 2 || pedestals == 2 && side < 2)
                        continue;  // (nothing is built off a pedestal shop's pedestal walls: ShopPedestals.cs)
                    bool across = side < 2;
                    int line = side == 0 ? y0 : side == 1 ? y1 : side == 2 ? x0 : x1;
                    if (GridMod(line, across ? GridY0 : GridX0) != 0)
                        continue;
                    int lo = across ? x0 : y0, hi = across ? x1 : y1, along = across ? GridX0 : GridY0;
                    for (int c = lo - GridMod(lo, along); c < hi; c += GridCell)
                    {
                        int cx = across ? c : side == 2 ? x0 - GridCell : x1, cy = across ? (side == 0 ? y0 - GridCell : y1) : c;
                        if (seen.Add(((long)cx << 32) ^ (uint)cy))
                            res.Add(new GridSpot { Side = (byte)side, X = cx, Y = cy, SX = across ? c + GridCell / 2 : line, SY = across ? line : c + GridCell / 2 });
                    }
                }
            }
            return res;
        }

        /// The piece a surprise shape becomes in a Grid run (owner Nov 18: boss surprise rooms and breach rooms on the lattice): a Stash one cell, the
        /// boss gifts tetrominoes (the L hall an L, the T hall and the Cross a T, the Ring and the Chamfer a square), the rest a domino.
        public static PieceShape PieceShapeOfSurprise(SurpriseShape s)
        {
            switch (s)
            {
                case SurpriseShape.Stash: case SurpriseShape.Tiny: return PieceShape.Mono;
                case SurpriseShape.LHall: return PieceShape.TetL;
                case SurpriseShape.THall: case SurpriseShape.Cross: return PieceShape.TetT;
                case SurpriseShape.Ring: case SurpriseShape.Chamfer: return PieceShape.TetO;
                case SurpriseShape.LongHall: return PieceShape.TetI;
                default: return PieceShape.Domino;
            }
        }

        /// SurpriseOptions on the lattice: every place the shape's piece can go (one of whose rooms covers `through`, if given), marked as that surprise shape,
        /// free. Same order on every machine.
        public static List<SurpriseOption> GridSurpriseOptions(IList<ExpansionRoom> built, SurpriseShape shape, V2? through = null)
        {
            var res = new List<SurpriseOption>();
            foreach (var o in PlacementsFor(built, PieceShapeOfSurprise(shape)))
            {
                if (through.HasValue)
                {
                    bool hit = false;
                    foreach (var q in o.Rooms)
                        hit |= through.Value.X > q.X && through.Value.X < q.X1 && through.Value.Y > q.Y && through.Value.Y < q.Y1;
                    if (!hit)
                        continue;
                }
                var rooms = new List<ExpansionRoom>(o.Rooms.Count);
                foreach (var q in o.Rooms)
                    rooms.Add(Free(q, shape));
                res.Add(new SurpriseOption { Rooms = rooms, Shape = shape, Price = 0, X = o.X, Y = o.Y });
            }
            return res;
        }

        /// A surprise room's pick among the lattice's places (the wanted shape's piece, else the other gifts' pieces): seeded among the best few.
        public static SurpriseOption? SurpriseFor(int seed, int wave, IList<ExpansionRoom> built, SurpriseShape wanted, out string why, V2? through = null)
        {
            why = null;
            var tries = new List<SurpriseShape> { wanted };
            foreach (var s in new[] { SurpriseShape.Chamfer, SurpriseShape.THall, SurpriseShape.LHall, SurpriseShape.Cross, SurpriseShape.Ring, SurpriseShape.Box })
                if (!tries.Contains(s))
                    tries.Add(s);
            var rng = new DetRandom(seed * 7919 + wave * 104729 + built.Count + 13);
            foreach (var shape in tries)
            {
                var options = GridSurpriseOptions(built, shape, through);
                if (options.Count > 0)
                    return options[rng.Int(Math.Min(SurpriseTop, options.Count))];
            }
            why = BoughtCount(built) >= MaxRooms ? "The map is full" : "No spot fits a surprise room";
            return null;
        }

        /// Mystery door rooms (MysteryDoors.cs): every place a piece can go, as surprise options priced by its cells (Mystery.PriceForCells).
        public static List<SurpriseOption> GridMysteryOptions(IList<ExpansionRoom> built, PieceShape piece)
        {
            var res = new List<SurpriseOption>();
            foreach (var o in PlacementsFor(built, piece))
            {
                res.Add(new SurpriseOption { Rooms = o.Rooms, Shape = SurpriseShape.Box, Price = Mystery.PriceForCells(PieceShapeCells(piece)), X = o.X, Y = o.Y });
            }
            return res;
        }

        /// Every place this piece can go: each turn and mirror with one of its cells on a door spot, only what PieceShapeRooms accepts, each once.
        public static List<Placement> PlacementsFor(IList<ExpansionRoom> built, PieceShape piece)
        {
            var res = new List<Placement>();
            if (BoughtCount(built) >= MaxRooms)
                return res;
            var orients = PieceShapeOrients(piece);
            var seen = new HashSet<string>();
            foreach (var spot in GridSpots(built))
                for (int o = 0; o < orients.Count; o++)
                    foreach (var cell in orients[o])
                    {
                        int x = spot.X - cell[0] * GridCell, y = spot.Y - cell[1] * GridCell;
                        if (!seen.Add(o + ":" + x + "," + y))
                            continue;
                        var rooms = PieceShapeRooms(built, piece, o, x, y, out _);
                        if (rooms == null)
                            continue;
                        var size = PieceShapeSize(orients[o]);
                        res.Add(new Placement
                        {
                            Shape = piece, Orient = o, X = x, Y = y, Rooms = rooms, Side = spot.Side, SX = spot.SX, SY = spot.SY,
                            Mid = new V2(x + size[0] * GridCell / 2f, y + size[1] * GridCell / 2f),
                        });
                    }
            return res;
        }
    }
}
