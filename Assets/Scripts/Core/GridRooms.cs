using System;
using System.Collections.Generic;

namespace Arena
{
    public static partial class Expansion
    {
        public const int GridCell = 600;  // one cell: a 6 x 6 tile room
        public const int GridX0 = -400, GridY0 = -400;  // the lattice origin: the core's top-left corner. Every grid room's walls sit on lines GridCell apart from here.

        /// The core (Docs/GridRooms.md): the start room made 4 x 3 cells (2400 x 1800) by four rooms round it, built free when a Grid run begins, with the
        /// walls between them and the start room open. Every wall of the core has a door spot on each cell.
        public static List<ExpansionRoom> GridCore() => new List<ExpansionRoom>
        {
            new ExpansionRoom { Parent = 0, X = -400, Y = -400, W = 2400, H = 400, Kind = RoomKind.Combat },
            new ExpansionRoom { Parent = 0, X = -400, Y = 900, W = 2400, H = 500, Kind = RoomKind.Combat },
            new ExpansionRoom { Parent = 0, X = -400, Y = 0, W = 400, H = 900, Kind = RoomKind.Combat },
            new ExpansionRoom { Parent = 0, X = 1600, Y = 0, W = 400, H = 900, Kind = RoomKind.Combat },
        };

        /// The walls inside the core opened up, so the core is one open room.
        public static List<WallCut> GridCoreCuts() => new List<WallCut>
        {
            new WallCut { Vertical = false, At = 0, From = 0, Till = 1600 }, new WallCut { Vertical = false, At = 900, From = 0, Till = 1600 },
            new WallCut { Vertical = true, At = 0, From = 0, Till = 900 }, new WallCut { Vertical = true, At = 1600, From = 0, Till = 900 },
            new WallCut { Vertical = false, At = 0, From = -400, Till = 0 }, new WallCut { Vertical = false, At = 0, From = 1600, Till = 2000 },
            new WallCut { Vertical = false, At = 900, From = -400, Till = 0 }, new WallCut { Vertical = false, At = 900, From = 1600, Till = 2000 },
        };

        /// One of the core's four rooms (they cost nothing and don't count towards the room cap).
        public static bool IsGridCore(ExpansionRoom r)
        {
            if (r.Price != 0 || r.Owner != 0 || r.Piece != 0)
                return false;
            foreach (var c in GridCore())
                if (c.X == r.X && c.Y == r.Y && c.W == r.W && c.H == r.H)
                    return true;
            return false;
        }

        static int GridMod(int v, int origin) => ((v - origin) % GridCell + GridCell) % GridCell;

        /// Where along a shared wall run [lo, hi] its door goes: on a lattice wall, the middle of the cell nearest the run's middle (a run of an even
        /// number of cells has its middle on a cell line; the tie goes to the cell on the new room's side, `toward`), else the run's middle.
        static float GridDoorAlong(int lo, int hi, int line, int lineOrigin, int alongOrigin, float toward)
        {
            float mid = (lo + hi) / 2f;
            if (GridMod(line, lineOrigin) != 0)
                return mid;
            float half = Rooms.DoorW / 2f, best = mid, bestD = float.MaxValue;
            int k = (int)Math.Floor((mid - alongOrigin) / GridCell);
            for (int j = k - 1; j <= k + 1; j++)
            {
                float c = alongOrigin + j * GridCell + GridCell / 2f;
                if (c - half < lo || c + half > hi)
                    continue;
                float d = Math.Abs(c - mid) * 2f + (Math.Abs(c - toward) < Math.Abs(mid - toward) ? 0f : 1f);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// A plain door on a lattice wall that isn't in a cell's middle (a short run, like a cell beside the core's corner touching two of its boxes) goes when
        /// the same room has a door in that cell's middle into a room opened up to the first (a wide gap between them): one door a cell into one open room.
        static void DropSecondGridDoors(List<RoomDoor> doors)
        {
            for (int i = doors.Count - 1; i >= 0; i--)
            {
                var d = doors[i];
                if (d.Width > 0f || !OnGridLine(d) || CellMiddle(d))
                    continue;
                float along = d.Vertical ? d.Pos.Y : d.Pos.X;
                int origin = d.Vertical ? GridY0 : GridX0;
                float cell = origin + (float)Math.Floor((along - origin) / GridCell) * GridCell + GridCell / 2f;
                foreach (var o in doors)
                {
                    if (o.Width > 0f || o.Vertical != d.Vertical || o.B != d.B || o.A == d.A || !(d.Vertical ? o.Pos.X == d.Pos.X && o.Pos.Y == cell : o.Pos.Y == d.Pos.Y && o.Pos.X == cell))
                        continue;
                    bool open = false;
                    foreach (var g in doors)
                        open |= g.Width > 0f && g.Joins(o.A) && g.Joins(d.A);
                    if (open)
                    {
                        doors.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        static bool OnGridLine(RoomDoor d) => d.Vertical ? GridMod((int)d.Pos.X, GridX0) == 0 && d.Pos.X == (int)d.Pos.X : GridMod((int)d.Pos.Y, GridY0) == 0 && d.Pos.Y == (int)d.Pos.Y;

        static bool CellMiddle(RoomDoor d)
        {
            float along = d.Vertical ? d.Pos.Y : d.Pos.X;
            return along == (int)along && GridMod((int)along, d.Vertical ? GridY0 : GridX0) == GridCell / 2;
        }

        /// A node for a cycle in Grid mode: the middle of a room-sized cell a few rooms out, so building out along the grid claims it with the
        /// node at the room's middle. `ring` (0 and up) is the first cycle's ring round the hub: each starts off a different side. Null if no cell fits.
        public static MapNode? GridNodeFor(int seed, int cycle, IList<ExpansionRoom> built, IList<MapNode> nodes, NodeKind kind, int ring = -1)
        {
            var rng = new DetRandom(seed * 7351 + cycle * 977 + (ring + 1) * 131 + (int)kind * 17 + 3);
            var map = new List<ExpansionRoom>(built);
            int depth = ring >= 0 ? 2 : 2 + rng.Int(2);
            var start = Game.ArenaMid;
            for (int step = 1; step <= depth; step++)
            {
                var opts = PlacementsFor(map, PieceShape.Mono);
                if (ring >= 0 && step == 1)
                {
                    var side = opts.FindAll(o => o.Side == ring % 4);
                    if (side.Count > 0)
                        opts = side;
                }
                if (step == depth)
                    opts = opts.FindAll(o =>
                    {
                        foreach (var nd in nodes)
                            if (nd.Pos.DistanceTo(o.Mid) < NodeApart + NodeHalf(nd))
                                return false;
                        return true;
                    });
                if (opts.Count == 0)
                    return null;
                opts.Sort((p, q) => q.Mid.DistanceTo(start).CompareTo(p.Mid.DistanceTo(start)));  // (the outer half: the chain heads away from the start)
                var pick = opts[rng.Int(Math.Max(1, opts.Count / 2))];
                if (step == depth)
                    return new MapNode { X = (short)pick.Mid.X, Y = (short)pick.Mid.Y, Kind = kind };
                map.AddRange(pick.Rooms);
            }
            return null;
        }
    }

    public partial class Game
    {
        /// Host, every frame: a Grid run's bare map (a new run, or after a map reset) gets its core: four free rooms round the start room, the walls between opened.
        void TickGridCore()
        {
            if (!IsHost || !ExpandingOn || Expanded.Count > 0)
                return;
            Expanded.AddRange(Expansion.GridCore());
            Openings.AddRange(Expansion.GridCoreCuts());
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            BroadcastExpansion();
        }
    }
}
