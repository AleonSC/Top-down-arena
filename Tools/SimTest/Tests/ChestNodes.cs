using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Chest nodes (Docs/Nodes.md, Docs/Chests.md): a chest node holds a real chest, a Vector one opens into vector pickups, and nodes sit at the middle of a cell (the room that claims one holds it in its middle).
static partial class Program
{
    static void ExpandingVectorChestTest(float dt)
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
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); }

        // Surprise rooms drop no chest node (owner Nov 19): over a few seeds, none appears outside their walls.
        var give = typeof(Game).GetMethod("GiveSurpriseRoom", flags);
        var seedField = typeof(Game).GetField("expandSeed", flags);
        int gotChest = 0, tries = 0;
        for (int sd = 1; sd <= 30; sd++)
        {
            UseMap(new[] { g }, Expansion.GridCore()); g.Nodes.Clear(); seedField.SetValue(g, sd);
            g.BossSlot = true; g.State = MatchState.Intermission;
            give.Invoke(g, null);
            if (g.Expanded.Count == 4) continue;
            tries++;
            if (g.Nodes.Count > 0 && Expansion.IsChest(g.Nodes[0].Kind) && !Expansion.NodeClaimed(g.Expanded, g.Nodes[0])) gotChest++;
        }
        Check(tries >= 20 && gotChest == 0, "surprise rooms drop no chest node (" + gotChest + " of " + tries + ")");

        // A claimed chest node in the inner tier holds a Vector chest on half the seeds (the old vector chest node, now one chest): a real chest prop that
        // bursts into vector pickups when touched, spends the node, and never holds the wave.
        foreach (var gg in new[] { g, cg }) gg.Nodes.Clear();
        UseMap(new[] { g, cg }, GridMap(PieceShape.Mono));
        var cell = g.Expanded[4];  // (a single cell east of the core)
        var mid = new V2((cell.X + cell.X1) / 2f, (cell.Y + cell.Y1) / 2f);
        int vseed = 1;
        while (Expansion.NodeChestKind(vseed, (int)mid.X, (int)mid.Y, true) != ChestKind.Vector && vseed < 200) vseed++;
        seedField.SetValue(g, vseed);
        Check(Expansion.NodeChestKind(vseed, (int)mid.X, (int)mid.Y, true) == ChestKind.Vector && Expansion.NodeTier((int)mid.X, (int)mid.Y) == 0, "an inner-tier chest node holds a Vector chest on some seed (" + vseed + ")");
        g.Nodes.Add(new MapNode { X = (short)mid.X, Y = (short)mid.Y, Kind = NodeKind.Chest });
        typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
        g.State = MatchState.Fighting;
        foreach (var p in g.Players.Values) p.Pos = p.Disp = new V2(800f, 450f);
        Run(s, dt, 1f, () => cg.Nodes.Count == 1 && cg.Props.Values.Any(p => p.Kind == PropKind.Chest), noPicks);
        Check(cg.Nodes.Count == 1 && cg.Nodes[0].Kind == NodeKind.Chest && !g.Nodes[0].Spent, "the chest node reaches the client, shut");
        Check(g.NodeSpot(0).Equals(mid) && cg.NodeSpot(0).Equals(g.NodeSpot(0)), "it stands in the middle of its room, on both screens (" + g.NodeSpot(0).X + "," + g.NodeSpot(0).Y + ")");
        var chest = g.Props.Values.FirstOrDefault(p => p.Kind == PropKind.Chest);
        Check(chest != null && chest.Chest == ChestKind.Vector && chest.Pos.Equals(mid) && g.Props.Values.Count(p => p.Kind == PropKind.Chest) == 1, "it holds one real Vector chest prop");
        Check(!g.PropsHoldWave(), "a node's chest never holds the wave");
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Props.Values.Count(p => p.Kind == PropKind.Chest) == 1, "ticking again does not stack a second chest on it");
        int coins0 = g.Coins.Values.Count(c => c.Vec);
        int vec0 = g.Players.Values.Sum(p => p.Vectors);
        var me = g.Players[1];
        me.Pos = me.Disp = mid + new V2(10f, 0f);
        Run(s, dt, 0.5f, () => g.Nodes[0].Spent, noPicks);
        Check(g.Nodes[0].Spent && g.Coins.Values.Count(c => c.Vec) - coins0 + (g.Players.Values.Sum(p => p.Vectors) > vec0 ? Game.VectorChestPickups : 0) >= Game.VectorChestPickups,
            "touching it opens it: " + Game.VectorChestPickups + " vector pickups burst out and the node is spent");
        Run(s, dt, 3f, () => g.Players.Values.Sum(p => p.Vectors) >= vec0 + Game.VectorChestPickups * Game.VectorDropSmall, noPicks);
        Check(g.Players.Values.Sum(p => p.Vectors) >= vec0 + Game.VectorChestPickups * Game.VectorDropSmall || g.Players.Values.All(p => p.Vectors >= p.VectorCap),
            "and the players pick up " + Game.VectorChestPickups * Game.VectorDropSmall + " vectors (" + vec0 + " -> " + g.Players.Values.Sum(p => p.Vectors) + ")");
        Run(s, dt, 1f, () => cg.Nodes.Count == 1 && cg.Nodes[0].Spent, noPicks);
        Check(cg.Nodes[0].Spent, "the client sees it spent");
        int coins1 = g.Coins.Count;
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Nodes[0].Spent && g.Coins.Values.Count(c => c.Vec) <= coins1 && g.Props.Values.Count(p => p.Kind == PropKind.Chest && !p.Open) == 0, "it opens only once, and the spent node puts no chest back");

        // The one spawn call: a chest with a hook calls it once as it opens; a plain one holds the wave; the legacy vector chest node is a chest node now.
        int hooked = 0;
        var hc = g.SpawnChest(new V2(300f, 300f), ChestKind.Gold, c => hooked++, false);
        var pc = g.SpawnChest(new V2(600f, 300f), ChestKind.Upgrade);
        Check(g.PropsHoldWave() && g.Props.ContainsKey(pc.Id) && !hc.Open, "SpawnChest makes a chest; an ordinary one holds the wave");
        g.Props.Remove(pc.Id);
        Check(!g.PropsHoldWave(), "a chest that does not hold the wave lets it end");
        me.Pos = me.Disp = hc.Pos;
        Run(s, dt, 0.5f, () => hc.Open, noPicks);
        Run(s, dt, 0.3f, null, noPicks);
        Check(hc.Open && hooked == 1, "its hook runs once, as it opens (" + hooked + ")");
        {  // an older save's vector chest node loads as a chest node, and its shop and Black Market nodes are dropped
            var old = RunSave.Parse(new RunSave { Wave = 6, HasExpansion = true, Seats = { new SeatSave() } }.ToText() + "\nnode VectorChest 850 2150 0\nnode Shop 100 100 0\nnode BlackMarket 200 200 0\nnode Tower 400 400 0\nnode Vein 500 500 0\nnode Relic 300 300 0\n");
            Check(old != null && old.Nodes.Count == 4 && old.Nodes[0].Kind == NodeKind.Chest && old.Nodes[1].Kind == NodeKind.Chest && old.Nodes[2].Kind == NodeKind.Chest && old.Nodes[3].Kind == NodeKind.Relic, "an older save's vector chest, scout tower and gold vein nodes load as chest nodes; its shop and Black Market nodes are dropped");
        }
    }

    // Owner Nov 18: a claimed chest or relic stands in the middle of its room (World.NodeSpots), clear of a card room's obstacles.
    static void ExpandingNodeCentreTest()
    {
        string why = null;
        int W = (int)Game.Width;
        var built = new List<ExpansionRoom>();
        var room = Box(W, 200, 1000, 500);
        Check(room.HasValue, "a 10 x 5 room east of the start room (" + why + ")");
        built.Add(room.Value);
        var nodes = new List<MapNode>
        {
            new MapNode { X = (short)(W + 150), Y = 350, Kind = NodeKind.Chest },
            new MapNode { X = (short)(W + 850), Y = 550, Kind = NodeKind.Relic },
            new MapNode { X = (short)(W + 150), Y = 550, Kind = NodeKind.Chest },
            new MapNode { X = (short)(W + 850), Y = 350, Kind = NodeKind.Rule },
            new MapNode { X = (short)(W + 5000), Y = 450, Kind = NodeKind.Chest },  // (nobody has built round it)
        };
        var mid = new V2(W + 500f, 450f);
        var wd = Expansion.Build(built, null, null, nodes);
        Check(wd.NodeSpots != null && wd.NodeSpots.Length == nodes.Count, "the world gives every node a spot");
        Check(wd.NodeSpots[0].Equals(mid) && wd.NodeSpots[1].Equals(mid) && wd.NodeSpots[2].Equals(mid), "a claimed chest, relic and second chest stand in the room's middle");
        Check(wd.NodeSpots[3].Equals(nodes[3].Pos) && wd.NodeSpots[4].Equals(nodes[4].Pos), "a shop node and an unclaimed chest stay where they are");
        // Owner Nov 19: in a grid room the node stands in the middle of the cell it was built in (a 2 x 2 room: one of its four cells, not the room's middle).
        int gx = Expansion.GridX0 + 4 * Expansion.GridCell, gy = Expansion.GridY0, c = Expansion.GridCell;
        var grid = new List<ExpansionRoom> { Box(gx, gy, 2 * c, 2 * c).Value };
        var gnodes = new List<MapNode>
        {
            new MapNode { X = (short)(gx + c + c / 2 - 40), Y = (short)(gy + c + c / 2 + 30), Kind = NodeKind.Chest },
            new MapNode { X = (short)(gx + c / 2), Y = (short)(gy + c / 2), Kind = NodeKind.Relic },
        };
        var gw = Expansion.Build(grid, null, null, gnodes);
        Check(gw.NodeSpots[0].Equals(new V2(gx + 1.5f * c, gy + 1.5f * c)), "a chest in a 2 x 2 grid room stands in its own cell's middle (" + gw.NodeSpots[0].X + "," + gw.NodeSpots[0].Y + ")");
        Check(gw.NodeSpots[1].Equals(new V2(gx + 0.5f * c, gy + 0.5f * c)), "a relic in the top-left cell stands in that cell's middle");
    }
}
