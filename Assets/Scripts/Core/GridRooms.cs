using System;
using System.Collections.Generic;

namespace Arena
{
    /// A grid room's footprint (Docs/GridRooms.md): the lobby's Building: Grid. New values go at the end.
    public enum GridShape : byte { Room, Gauntlet, Hall, Arena }

    /// One legal grid placement: the room it builds, which room's wall it hangs off, and the door spot it snaps to.
    public struct GridOption
    {
        public ExpansionRoom Room;
        public byte Side;     // the parent's side it hangs off: 0 top, 1 bottom, 2 left, 3 right
        public int SX, SY;    // the door spot on that wall (world px): the middle of the shared stretch
        public V2 Mid => new V2((Room.X + Room.X1) / 2f, (Room.Y + Room.Y1) / 2f);
    }

    public static partial class Expansion
    {
        public const int GridCell = 600;  // one cell: a 6 x 6 tile room
        public const int GridX0 = -400, GridY0 = -400;  // the lattice origin: the core's top-left corner. Every grid room's walls sit on lines GridCell apart from here.

        /// Width and height in px of each GridShape: a Room is one cell, a Gauntlet two across, a Hall two down, an Arena two by two.
        public static readonly int[][] GridSizes = { new[] { 600, 600 }, new[] { 1200, 600 }, new[] { 600, 1200 }, new[] { 1200, 1200 } };

        public static string GridName(GridShape s) => s == GridShape.Gauntlet ? "Gauntlet" : s == GridShape.Hall ? "Hall" : s == GridShape.Arena ? "Arena" : "Room";

        public static string GridBlurb(GridShape s)
            => s == GridShape.Gauntlet ? "Two cells across: a long fight with room to run."
             : s == GridShape.Hall ? "Two cells down: a long lane for kiting and sightlines."
             : s == GridShape.Arena ? "Four cells: the big one for boss fights and crowds."
             : "One cell: 6 x 6 tiles, the standard room.";

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
            if (r.Price != 0 || r.Owner != 0 || r.Card != 0 || r.Leg != 0)
                return false;
            foreach (var c in GridCore())
                if (c.X == r.X && c.Y == r.Y && c.W == r.W && c.H == r.H)
                    return true;
            return false;
        }

        static int GridMod(int v, int origin) => ((v - origin) % GridCell + GridCell) % GridCell;

        /// Every spot a room of this shape can go: each cell along the outside of a built room's wall (a wall on a lattice line) is a door spot, and the room
        /// hangs off it flush with the wall, in the first alignment that fits (so a big room can sit a cell to either side). Only spots Expansion.Custom accepts
        /// (no overlap, a door's worth of shared wall, the caps) are returned, each room once.
        public static List<GridOption> GridOptions(IList<ExpansionRoom> built, GridShape shape)
        {
            var res = new List<GridOption>();
            if (BoughtCount(built) >= MaxRooms)
                return res;
            int w = GridSizes[(int)shape][0], h = GridSizes[(int)shape][1];
            var seen = new HashSet<long>();
            for (int a = 1; a <= built.Count; a++)
            {
                if (built[a - 1].Floating || IsPush(built[a - 1]))
                    continue;
                BoxOf(built, a, out int x0, out int y0, out int x1, out int y1);
                for (int side = 0; side < 4; side++)
                {
                    bool across = side < 2;  // top and bottom walls run across
                    int line = side == 0 ? y0 : side == 1 ? y1 : side == 2 ? x0 : x1;
                    if (GridMod(line, across ? GridY0 : GridX0) != 0)
                        continue;  // (a wall off the lattice, like a surprise room's: no door spots)
                    int lo = across ? x0 : y0, hi = across ? x1 : y1, along = across ? GridX0 : GridY0;
                    for (int c = lo - GridMod(lo, along); c < hi; c += GridCell)
                    {
                        int span = across ? w : h;
                        for (int k = 0; k < span / GridCell; k++)
                        {
                            int x = across ? c - k * GridCell : side == 2 ? x0 - w : x1;
                            int y = across ? (side == 0 ? y0 - h : y1) : c - k * GridCell;
                            var room = Custom(built, x, y, w, h, out _);
                            if (!room.HasValue)
                                continue;
                            if (seen.Add(((long)x << 32) ^ (uint)y))
                                res.Add(new GridOption
                                {
                                    Room = room.Value, Side = (byte)side,
                                    SX = across ? c + GridCell / 2 : line, SY = across ? line : c + GridCell / 2,
                                });
                            break;
                        }
                    }
                }
            }
            return res;
        }

        /// Is this box one of the grid's placements (any shape)? The host checks every room built in Grid mode with it.
        public static bool GridLegal(IList<ExpansionRoom> built, int x, int y, int w, int h, out string why)
        {
            why = null;
            bool sized = false;
            for (int s = 0; s < GridSizes.Length; s++)
            {
                if (GridSizes[s][0] != w || GridSizes[s][1] != h)
                    continue;
                sized = true;
                foreach (var o in GridOptions(built, (GridShape)s))
                    if (o.Room.X == x && o.Room.Y == y)
                        return true;
            }
            why = sized ? "Not on a door spot" : "Not a grid room size";
            return false;
        }

        /// A node for a cycle in Grid mode: the middle of a room-sized cell a few rooms out, so building out along the grid claims it with the
        /// node at the room's middle. `ring` (0 and up) is the first cycle's ring round the hub: each starts off a different side. Null if no cell fits.
        public static MapNode? GridNodeFor(int seed, int cycle, IList<ExpansionRoom> built, IList<MapNode> nodes, NodeKind kind, int ring = -1)
        {
            var rng = new DetRandom(seed * 7351 + cycle * 977 + (ring + 1) * 131 + (int)kind * 17 + 3);
            var map = new List<ExpansionRoom>(built);
            int depth = ring >= 0 ? 2 : 2 + rng.Int(2);
            var start = new V2(Game.Width / 2f, Game.Height / 2f);
            for (int step = 1; step <= depth; step++)
            {
                var opts = GridOptions(map, GridShape.Room);
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
                map.Add(pick.Room);
            }
            return null;
        }

        /// A chest for the room just built (its last `count` boxes) in Grid mode: the same roll as RoomChestFor, but the chest sits at the middle of
        /// a room-sized cell against the new room, so building that cell claims it with the chest in its middle.
        public static MapNode? GridChestFor(int seed, IList<ExpansionRoom> built, IList<MapNode> nodes, int count, bool roll = true)
        {
            if (count <= 0 || count > built.Count || ChestsWaiting(built, nodes) >= RoomChestWaiting)
                return null;
            int first = built.Count - count;
            var boxes = new List<ExpansionRoom>();
            for (int i = first; i < built.Count; i++)
                boxes.Add(built[i]);
            var rng = new DetRandom(seed * 6151 + built.Count * 1291 + 17);
            if (roll && rng.Float() >= RoomChestChance(boxes))
                return null;
            var kind = rng.Float() < RoomChestVectorShare ? NodeKind.VectorChest : NodeKind.Chest;
            var opts = GridOptions(built, GridShape.Room).FindAll(o =>
            {
                bool touches = false;
                foreach (var b in boxes)
                    touches |= SharedRun(o.Room.X, o.Room.Y, o.Room.X1, o.Room.Y1, b.X, b.Y, b.X1, b.Y1) > 0;
                if (!touches)
                    return false;
                foreach (var nd in nodes)
                    if (nd.Pos.DistanceTo(o.Mid) < NodeApart + NodeHalf(nd))
                        return false;
                return true;
            });
            if (opts.Count == 0)
                return null;
            var pick = opts[rng.Int(opts.Count)];
            return new MapNode { X = (short)pick.Mid.X, Y = (short)pick.Mid.Y, Kind = kind };
        }
    }

    public partial class Game
    {
        /// The run builds on the grid (the lobby's Building: Grid, Docs/GridRooms.md): rooms snap to door spots on the walls of rooms already built.
        public bool GridRoomsOn => ExpandingOn && !RoomCardsOn && D(DiffKnob.GridRooms) >= 0.5f;

        /// Host, every frame: a Grid run's bare map (a new run, or after a map reset) gets its core: four free rooms round the start room, the walls between opened.
        void TickGridCore()
        {
            if (!IsHost || !GridRoomsOn || Expanded.Count > 0)
                return;
            Expanded.AddRange(Expansion.GridCore());
            Openings.AddRange(Expansion.GridCoreCuts());
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            BroadcastExpansion();
        }

        /// Bot: a random affordable room on a door spot, one in three a bigger one.
        void BotGridBuild()
        {
            var shape = (GridShape)(rng.Next(3) == 0 ? 1 + rng.Next(3) : 0);
            var opts = Expansion.GridOptions(Expanded, shape);
            if (opts.Count == 0)
                return;
            var o = opts[rng.Next(opts.Count)];
            if (o.Room.Price <= SpendableVectors(Me))
                ProposeExpandRoom(o.Room.X, o.Room.Y, o.Room.W, o.Room.H);
        }
    }
}
