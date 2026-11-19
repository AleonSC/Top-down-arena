using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Chest nodes (Docs/Nodes.md): a vector chest opens into vector pickups, and nodes sit at the middle of a cell (the room that claims one holds it in its middle).
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

        // A claimed vector chest: nothing until someone walks up, then vector pickups and the node is spent, on both screens.
        foreach (var gg in new[] { g, cg }) gg.Nodes.Clear();
        UseMap(new[] { g, cg }, GridMap(PieceShape.Mono));
        var cell = g.Expanded[4];  // (a single cell east of the core)
        var mid = new V2((cell.X + cell.X1) / 2f, (cell.Y + cell.Y1) / 2f);
        g.Nodes.Add(new MapNode { X = (short)mid.X, Y = (short)mid.Y, Kind = NodeKind.VectorChest });
        typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
        g.State = MatchState.Fighting;
        foreach (var p in g.Players.Values) p.Pos = p.Disp = new V2(800f, 450f);
        Run(s, dt, 1f, () => cg.Nodes.Count == 1, noPicks);
        Check(cg.Nodes.Count == 1 && cg.Nodes[0].Kind == NodeKind.VectorChest && !g.Nodes[0].Spent, "the vector chest reaches the client, shut");
        Check(g.NodeSpot(0).Equals(mid) && cg.NodeSpot(0).Equals(g.NodeSpot(0)), "it stands in the middle of its room, on both screens (" + g.NodeSpot(0).X + "," + g.NodeSpot(0).Y + ")");
        int coins0 = g.Coins.Values.Count(c => c.Vec);
        int vec0 = g.Players.Values.Sum(p => p.Vectors);
        var me = g.Players[1];
        me.Pos = me.Disp = g.NodeSpot(0) + new V2(20f, 0f);  // (a claimed chest stands in its room's middle)
        Run(s, dt, 0.3f, () => g.Nodes[0].Spent, noPicks);
        Check(g.Nodes[0].Spent && g.Coins.Values.Count(c => c.Vec) - coins0 + (g.Players.Values.Sum(p => p.Vectors) > vec0 ? Game.VectorChestPickups : 0) >= Game.VectorChestPickups,
            "walking up to it opens it: " + Game.VectorChestPickups + " vector pickups burst out");
        Run(s, dt, 3f, () => g.Players.Values.Sum(p => p.Vectors) >= vec0 + Game.VectorChestPickups * Game.VectorDropSmall, noPicks);
        Check(g.Players.Values.Sum(p => p.Vectors) >= vec0 + Game.VectorChestPickups * Game.VectorDropSmall || g.Players.Values.All(p => p.Vectors >= p.VectorCap),
            "and the players pick up " + Game.VectorChestPickups * Game.VectorDropSmall + " vectors (" + vec0 + " -> " + g.Players.Values.Sum(p => p.Vectors) + ")");
        Run(s, dt, 1f, () => cg.Nodes.Count == 1 && cg.Nodes[0].Spent, noPicks);
        Check(cg.Nodes[0].Spent, "the client sees it spent");
        int coins1 = g.Coins.Count;
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Nodes[0].Spent && g.Coins.Values.Count(c => c.Vec) <= coins1, "it opens only once");
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
            new MapNode { X = (short)(W + 150), Y = 550, Kind = NodeKind.VectorChest },
            new MapNode { X = (short)(W + 850), Y = 350, Kind = NodeKind.Shop },
            new MapNode { X = (short)(W + 5000), Y = 450, Kind = NodeKind.Chest },  // (nobody has built round it)
        };
        var mid = new V2(W + 500f, 450f);
        var wd = Expansion.Build(built, null, null, nodes);
        Check(wd.NodeSpots != null && wd.NodeSpots.Length == nodes.Count, "the world gives every node a spot");
        Check(wd.NodeSpots[0].Equals(mid) && wd.NodeSpots[1].Equals(mid) && wd.NodeSpots[2].Equals(mid), "a claimed chest, relic and vector chest stand in the room's middle");
        Check(wd.NodeSpots[3].Equals(nodes[3].Pos) && wd.NodeSpots[4].Equals(nodes[4].Pos), "a shop node and an unclaimed chest stay where they are");
    }
}
