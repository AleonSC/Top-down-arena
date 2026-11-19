using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Room chests (owner Nov 17): building a room may drop a chest node 3 to 5 tiles outside its wall (Expansion.RoomChestFor),
// hallways rarely, big rooms more, at most 2 waiting, on the side facing other rooms so claiming it joins branches.
static partial class Program
{
    static void ExpandingRoomChestTest()
    {
        Func<List<ExpansionRoom>, int, int, int, int, ExpansionRoom> add = (list, x, y, w, h) =>
        {
            string why = null;
            var r = Box(x, y, w, h);
            Check(r.HasValue, "room " + x + "," + y + " " + w + "x" + h + " fits" + (why != null ? " (" + why + ")" : ""));
            list.Add(r.Value);
            return r.Value;
        };
        Func<int, int, ExpansionRoom> box = (w, h) => new ExpansionRoom { W = w, H = h };
        Check(Math.Abs(Expansion.RoomChestChance(new[] { box(300, 1200) }) - 0.10f) < 0.001f
              && Math.Abs(Expansion.RoomChestChance(new[] { box(600, 600) }) - 0.25f) < 0.001f
              && Math.Abs(Expansion.RoomChestChance(new[] { box(1200, 1200) }) - 0.40f) < 0.001f,
            "the chance: a hallway 10%, a 6 x 6 room 25%, a 12 x 12 room 40%");

        // On the grid: a chest for the room just built sits at the middle of a free cell against it, so building that cell claims it.
        var hall = GridMap(PieceShape.TetI);  // (a line of four cells east of the core)
        int found = 0, bad = 0, rolled = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var nd = Expansion.GridChestFor(seed, hall, new List<MapNode>(), 1, false);
            if (!nd.HasValue) continue;
            found++;
            var n = nd.Value;
            bool onCell = Expansion.PlacementsFor(hall, PieceShape.Mono).Any(o => o.Mid.Equals(n.Pos));
            if (!Expansion.IsChest(n.Kind) || Expansion.NodeClaimed(hall, n) || !onCell || !Expansion.GridChestFor(seed, hall, new List<MapNode>(), 1, false).Value.Equals(n))
                bad++;
            if (Expansion.GridChestFor(seed, hall, new List<MapNode>(), 1).HasValue)
                rolled++;
        }
        int vecKind = Enumerable.Range(1, 400).Count(sd => { var q = Expansion.GridChestFor(sd, hall, new List<MapNode>(), 1, false); return q.HasValue && q.Value.Kind == NodeKind.VectorChest; });
        Check(vecKind >= 150 && vecKind <= 250, "about half are vector chests (" + vecKind + " of 400)");
        Check(found == 400 && bad == 0, "a forced roll always finds a chest at the middle of a free cell against the new room, unclaimed, the same each time (" + found + " found, " + bad + " bad)");
        float chance = Expansion.RoomChestChance(hall.Skip(4).ToList());
        Check(rolled >= 400 * chance * 0.6f && rolled <= 400 * chance * 1.4f, "the roll drops one at about the room's chance (" + rolled + " of 400, " + (chance * 100f).ToString("0") + "%)");

        var two = new List<MapNode> { new MapNode { X = 5000, Y = -2000, Kind = NodeKind.Chest }, new MapNode { X = 5000, Y = 3000, Kind = NodeKind.VectorChest } };
        Check(Enumerable.Range(1, 50).All(sd => !Expansion.GridChestFor(sd, hall, two, 1, false).HasValue), "no chest while two wait unclaimed");
        var spentOne = new List<MapNode> { two[0], new MapNode { X = 5000, Y = 3000, Kind = NodeKind.Chest, Spent = true } };
        Check(Enumerable.Range(1, 50).Any(sd => Expansion.GridChestFor(sd, hall, spentOne, 1, false).HasValue), "an opened chest doesn't count");
    }

    // In a run: a surprise room rolls a chest too, and a claimed vector chest bursts into vector pickups when walked up to.
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

        // Surprise rooms roll: over a few seeds, some of them drop a chest node outside their walls.
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
        Check(tries >= 20 && gotChest >= 1 && gotChest < tries, "surprise rooms roll chests too (none within 10 tiles of the start room) (" + gotChest + " of " + tries + ")");

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
