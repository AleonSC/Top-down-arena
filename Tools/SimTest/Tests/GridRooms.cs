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
        var empty = new List<ExpansionRoom>();
        var rooms = Expansion.GridOptions(empty, GridShape.Room);
        Check(rooms.Count == 6, "six door spots round the start room: two top, two bottom, one each side (" + rooms.Count + ")");
        string why;
        Check(rooms.All(o => o.Room.W == 600 && o.Room.H == 600 && Expansion.Custom(empty, o.Room.X, o.Room.Y, 600, 600, out _).HasValue && Expansion.GridLegal(empty, o.Room.X, o.Room.Y, 600, 600, out _)),
            "each is a 6 x 6 room Expansion.Custom accepts, and the host's grid check agrees");
        var right = rooms.First(o => o.Side == 3);
        Check(right.Room.X == 1600 && right.Room.Y % 100 == 0 && right.SX == 1600, "the right wall's spot hangs off x = 1600 on the 100 px grid");
        Check(!Expansion.GridLegal(empty, right.Room.X, right.Room.Y + 100, 600, 600, out why) && why == "Not on a door spot", "a room slid off its door spot is refused (" + why + ")");
        Check(!Expansion.GridLegal(empty, right.Room.X, right.Room.Y, 700, 600, out why) && why == "Not a grid room size", "an odd size is refused (" + why + ")");
        foreach (GridShape sh in Enum.GetValues(typeof(GridShape)))
        {
            var o = Expansion.GridOptions(empty, sh);
            Check(o.Count > 0 && o.All(q => q.Room.W == Expansion.GridSizes[(int)sh][0] && q.Room.H == Expansion.GridSizes[(int)sh][1]), Expansion.GridName(sh) + ": " + o.Count + " spots, all its size, priced from " + o.Min(q => q.Room.Price) + " vectors");
        }

        // A chain: a room on the right wall brings its own spots (top, bottom, right), and none overlaps what is built.
        var map = new List<ExpansionRoom> { right.Room };
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
            Check(chain != null && chain.Count <= 2, "  two rooms along the door spots claim it (" + (chain == null ? "none" : chain.Count + " rooms") + ")");
            if (chain != null)
            {
                var w = Expansion.Build(chain, null, null, new List<MapNode> { n.Value });
                Check(Expansion.NodeClaimed(chain, n.Value) && w.NodeSpots[0].Equals(n.Value.Pos), "  claimed, the fixture stands at the node: the middle of its room");
            }
            nodes.Add(n.Value);
        }
        var cn = Expansion.GridChestFor(5, new List<ExpansionRoom> { right.Room }, new List<MapNode>(), 1, false);
        Check(cn.HasValue && Expansion.IsChest(cn.Value.Kind) && !Expansion.NodeClaimed(new List<ExpansionRoom> { right.Room }, cn.Value)
              && Expansion.GridOptions(new List<ExpansionRoom> { right.Room }, GridShape.Room).Any(o => o.Mid.Equals(cn.Value.Pos)), "a room chest in Grid mode sits at the middle of a free cell against the new room");
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
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.RoomCards, 0f); gg.Diff.Set(DiffKnob.BuildVote, 0f); gg.Diff.Set(DiffKnob.GridRooms, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Check(g.GridRoomsOn && cg.GridRoomsOn && g.FreeBuild && !g.BuildVoting && !g.CanPush, "grid rooms on: builds go up at once, no vote, no wall push");
        g.Diff.Set(DiffKnob.RoomCards, 1f);
        Check(!g.GridRoomsOn && g.RoomCardsOn, "room cards win over grid rooms");
        g.Diff.Set(DiffKnob.RoomCards, 0f);
        g.Diff.Set(DiffKnob.BuildVote, 1f);
        Check(!g.BuildVoting, "and the team vote never applies to them");
        g.Diff.Set(DiffKnob.BuildVote, 0f);

        g.Players[2].Vectors = g.Players[1].Vectors = 200;
        g.Players[2].VectorCap = g.Players[1].VectorCap = 200;
        // A drawn room that is not on a door spot is refused; a grid room goes up for its price.
        cg.ProposeExpandRoom(1600, 100, 800, 600);
        cg.ProposeExpandRoom(1600, 300, 600, 600);
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Expanded.Count == 0 && cg.Expanded.Count == 0, "a drawn room and a room off its door spot are refused");
        var spot = Expansion.GridOptions(g.Expanded, GridShape.Hall).First(o => o.Side == 3);
        int v2 = g.Players[2].Vectors;
        cg.ProposeExpandRoom(spot.Room.X, spot.Room.Y, spot.Room.W, spot.Room.H);
        Run(s, dt, 1f, () => cg.Expanded.Count == 1, noPicks);
        Check(g.Expanded.Count == 1 && cg.Expanded.Count == 1 && g.Expanded[0].Owner == 2 && g.Expanded[0].X == spot.Room.X && g.Expanded[0].H == 1200, "a friend's hall on the right wall's spot is built at once on both screens");
        Run(s, dt, 1f, () => cg.Players[2].Vectors == g.Players[2].Vectors);
        Check(g.Players[2].Vectors == v2 - spot.Room.Price, "and they pay its new wall (" + spot.Room.Price + " vectors)");
        // A theme is refused, and a bot builds only on door spots.
        int before = g.Expanded.Count;
        g.ProposeExpandRoom(spot.Room.X1, spot.Room.Y, 600, 600, PathId.BurningGrounds);
        Check(g.Expanded.Count == before, "a themed room is refused in grid mode");
        var bot = typeof(Game).GetMethod("BotGridBuild", flags);
        for (int i = 0; i < 12; i++) bot.Invoke(g, null);
        Check(g.Expanded.Count > before && g.Expanded.Skip(1).All(r => Expansion.GridLegal(g.Expanded.Where(q => !q.Equals(r)).Take(g.Expanded.IndexOf(r)).ToList(), r.X, r.Y, r.W, r.H, out _)),
            "a bot builds rooms, each on a door spot (" + g.Expanded.Count + " rooms)");
    }
}
