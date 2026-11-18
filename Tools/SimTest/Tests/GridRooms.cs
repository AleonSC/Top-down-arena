using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Grid rooms (Docs/GridRooms.md): rooms snap to door spots on built rooms' walls in four fixed sizes; the host refuses anything else;
// nodes and room chests sit at the middle of a grid cell so claiming them puts the fixture in the room's middle.
static partial class Program
{
    static void GridRoomsTest()
    {
        var empty = Expansion.GridCore();  // (the start room with its core: 4 x 3 cells)
        Check(empty.All(Expansion.IsGridCore) && Expansion.BoughtCount(empty) == 0, "the core is four free rooms that don't count towards the room cap");
        var cw = Expansion.Build(empty, Expansion.GridCoreCuts());
        Check(cw.Doors.Count(d => d.Width >= 400) == 8 && cw.Doors.Any(d => d.Width == 1600), "the core's inner walls are opened: eight wide gaps, the hub's walls full width (" + string.Join(",", cw.Doors.Select(d => d.Width)) + ")");
        Func<float, float, bool> walled = (px, py) => cw.WallsFor(false).Any(c => Math.Abs(c.Pos.X - px) <= c.HW && Math.Abs(c.Pos.Y - py) <= c.HH);
        Check(!walled(800f, 0f) && !walled(800f, 900f) && !walled(0f, 450f) && !walled(1600f, 450f) && walled(800f, -400f), "no wall stands across the middle of the hub's four sides, but the core's outer wall does: one open room");
        var rooms = Expansion.GridOptions(empty, GridShape.Room);
        Check(rooms.Count == 14, "fourteen door spots round the core: four top, four bottom, three each side (" + rooms.Count + ")");
        Check(rooms.All(o => (o.Room.X - Expansion.GridX0) % 600 == 0 && (o.Room.Y - Expansion.GridY0) % 600 == 0), "every room sits on the one lattice");
        string why;
        Check(rooms.All(o => o.Room.W == 600 && o.Room.H == 600 && Expansion.Custom(empty, o.Room.X, o.Room.Y, 600, 600, out _).HasValue && Expansion.GridLegal(empty, o.Room.X, o.Room.Y, 600, 600, out _)),
            "each is a 6 x 6 room Expansion.Custom accepts, and the host's grid check agrees");
        var right = rooms.First(o => o.Side == 3);
        Check(right.Room.X == 2000 && right.SX == 2000, "the right wall's spots hang off x = 2000, the core's edge");
        Check(!Expansion.GridLegal(empty, right.Room.X, right.Room.Y + 100, 600, 600, out why) && why == "Not on a door spot", "a room slid off its door spot is refused (" + why + ")");
        Check(!Expansion.GridLegal(empty, right.Room.X, right.Room.Y, 700, 600, out why) && why == "Not a grid room size", "an odd size is refused (" + why + ")");
        foreach (GridShape sh in Enum.GetValues(typeof(GridShape)))
        {
            var o = Expansion.GridOptions(empty, sh);
            Check(o.Count > 0 && o.All(q => q.Room.W == Expansion.GridSizes[(int)sh][0] && q.Room.H == Expansion.GridSizes[(int)sh][1]), Expansion.GridName(sh) + ": " + o.Count + " spots, all its size, priced from " + o.Min(q => q.Room.Price) + " vectors");
        }

        // A chain: a room on the right wall brings its own spots (top, bottom, right), and none overlaps what is built.
        var map = new List<ExpansionRoom>(empty) { right.Room };
        var next = Expansion.GridOptions(map, GridShape.Room);
        Check(next.Count > rooms.Count - 1 && next.Any(o => o.Room.X == right.Room.X1 && o.Room.Y == right.Room.Y), "the new room has spots of its own: another room goes straight out from it");
        Check(next.All(o => Expansion.Custom(map, o.Room.X, o.Room.Y, o.Room.W, o.Room.H, out _).HasValue), "every spot on the grown map is still a legal room");

        // Nodes: the ring round the hub and a later cycle each sit at the middle of a room-sized cell that building out along the grid reaches.
        var nodes = new List<MapNode>();
        var kinds = new[] { NodeKind.Chest, NodeKind.Relic, NodeKind.VectorChest, NodeKind.Chest, NodeKind.Relic, NodeKind.Chest };
        for (int i = 0; i < kinds.Length; i++)
        {
            var n = Expansion.GridNodeFor(7, 0, empty, nodes, kinds[i], i);
            Check(n.HasValue, "ring node " + i + " found a cell");
            if (!n.HasValue) continue;
            Check(!Expansion.NodeClaimed(empty, n.Value) && nodes.All(o => o.Pos.DistanceTo(n.Value.Pos) >= Expansion.NodeApart), "  it is outside the map and " + Expansion.NodeApart / 100 + " tiles from the others");
            List<ExpansionRoom> chain = GridChainTo(empty, n.Value, 3);
            Check(chain != null && chain.Count - 4 <= 2, "  two rooms along the door spots claim it (" + (chain == null ? "none" : chain.Count - 4 + " rooms") + ")");
            if (chain != null)
            {
                var w = Expansion.Build(chain, null, null, new List<MapNode> { n.Value });
                Check(Expansion.NodeClaimed(chain, n.Value) && w.NodeSpots[0].Equals(n.Value.Pos), "  claimed, the fixture stands at the node: the middle of its room");
            }
            nodes.Add(n.Value);
        }
        var one = new List<ExpansionRoom>(empty) { right.Room };
        var cn = Expansion.GridChestFor(5, one, new List<MapNode>(), 1, false);
        Check(cn.HasValue && Expansion.IsChest(cn.Value.Kind) && !Expansion.NodeClaimed(one, cn.Value)
              && Expansion.GridOptions(one, GridShape.Room).Any(o => o.Mid.Equals(cn.Value.Pos)), "a room chest in Grid mode sits at the middle of a free cell against the new room");
    }

    // The shortest chain of Room-shape rooms (at most `depth`) built along door spots whose last room is the cell the node is the middle of; null if none.
    static List<ExpansionRoom> GridChainTo(List<ExpansionRoom> built, MapNode n, int depth)
    {
        for (int d = 1; d <= depth; d++)
        {
            var r = GridChainExact(built, n, d);
            if (r != null) return r;
        }
        return null;
    }

    static List<ExpansionRoom> GridChainExact(List<ExpansionRoom> built, MapNode n, int depth)
    {
        foreach (var o in Expansion.GridOptions(built, GridShape.Room))
        {
            var m = new List<ExpansionRoom>(built) { o.Room };
            if (depth == 1 ? o.Mid.Equals(n.Pos) : GridChainExact(m, n, depth - 1) != null)
                return depth == 1 ? m : GridChainExact(m, n, depth - 1);
        }
        return null;
    }

    static void GridRoomsGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.RoomCards, 0f); gg.Diff.Set(DiffKnob.GridRooms, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4 && g.Expanded.All(Expansion.IsGridCore) && g.Openings.Count == 8 && cg.Openings.Count == 8, "a Grid run's bare map gets its core, with the walls inside open, on both screens");
        Check(g.GridRoomsOn && cg.GridRoomsOn && g.FreeBuild && !g.CanPush, "grid rooms on: builds go up at once, no wall push");
        g.Diff.Set(DiffKnob.RoomCards, 1f);
        Check(!g.GridRoomsOn && g.RoomCardsOn, "room cards win over grid rooms");
        g.Diff.Set(DiffKnob.RoomCards, 0f);

        g.Players[2].Vectors = g.Players[1].Vectors = 200;
        g.Players[2].VectorCap = g.Players[1].VectorCap = 200;
        // A drawn room that is not on a door spot is refused; a grid room goes up for its price.
        cg.ProposeExpandRoom(1600, 100, 800, 600);
        cg.ProposeExpandRoom(2000, 100, 600, 600);
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4, "a drawn room and a room off its door spot are refused");
        // A Grid run builds from piece cards (Docs/GridPieces.md step 3): even a box on a door spot is refused as a drawing, and the bots don't draw.
        var spot = Expansion.GridOptions(g.Expanded, GridShape.Hall).First(o => o.Side == 3);
        cg.ProposeExpandRoom(spot.Room.X, spot.Room.Y, spot.Room.W, spot.Room.H);
        typeof(Game).GetMethod("BotGridBuild", flags).Invoke(g, null);
        Run(s, dt, 1f, null, noPicks);
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4, "a room drawn on a door spot is refused: pieces only");
    }
}
