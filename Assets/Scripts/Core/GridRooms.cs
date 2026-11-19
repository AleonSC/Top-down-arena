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
            if (r.Price != 0 || r.Owner != 0 || r.Leg != 0)
                return false;
            foreach (var c in GridCore())
                if (c.X == r.X && c.Y == r.Y && c.W == r.W && c.H == r.H)
                    return true;
            return false;
        }

        static int GridMod(int v, int origin) => ((v - origin) % GridCell + GridCell) % GridCell;

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
