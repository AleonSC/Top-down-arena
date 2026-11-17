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
            string why;
            var r = Expansion.Custom(list, x, y, w, h, out why);
            Check(r.HasValue, "room " + x + "," + y + " " + w + "x" + h + " fits" + (why != null ? " (" + why + ")" : ""));
            list.Add(r.Value);
            return r.Value;
        };
        Func<int, int, ExpansionRoom> box = (w, h) => new ExpansionRoom { W = w, H = h };
        Check(Math.Abs(Expansion.RoomChestChance(new[] { box(300, 1200) }) - 0.10f) < 0.001f
              && Math.Abs(Expansion.RoomChestChance(new[] { box(600, 600) }) - 0.25f) < 0.001f
              && Math.Abs(Expansion.RoomChestChance(new[] { box(1200, 1200) }) - 0.40f) < 0.001f,
            "the chance: a hallway 10%, a 6 x 6 room 25%, a 12 x 12 room 40%");

        int W = (int)Game.Width, H = (int)Game.Height;
        var hall = new List<ExpansionRoom>();
        var h1 = add(hall, W, 0, 1600, 300);  // a 16-tile hallway east off the start room
        int found = 0, bad = 0, rolled = 0;
        for (int seed = 1; seed <= 400; seed++)
        {
            var nd = Expansion.RoomChestFor(seed, hall, new List<MapNode>(), 1, false);
            if (!nd.HasValue) continue;
            found++;
            var n = nd.Value;
            int dx = Math.Max(Math.Max(h1.X - n.X, n.X - h1.X1), 0), dy = Math.Max(Math.Max(h1.Y - n.Y, n.Y - h1.Y1), 0);
            int off = Math.Max(dx, dy);
            bool hubFar = Math.Max(Math.Max(-n.X, n.X - W), Math.Max(-n.Y, n.Y - H)) >= Expansion.StartClear;
            if (!Expansion.IsChest(n.Kind) || Expansion.NodeClaimed(hall, n) || off < 350 || off > 550 || !hubFar || !Expansion.NodeReachable(hall, n)
                || !Expansion.RoomChestFor(seed, hall, new List<MapNode>(), 1, false).Value.Equals(n))
                bad++;
            if (Expansion.RoomChestFor(seed, hall, new List<MapNode>(), 1).HasValue)
                rolled++;
        }
        int vecKind = Enumerable.Range(1, 400).Count(sd => { var q = Expansion.RoomChestFor(sd, hall, new List<MapNode>(), 1, false); return q.HasValue && q.Value.Kind == NodeKind.VectorChest; });
        Check(vecKind >= 150 && vecKind <= 250, "about half are vector chests (" + vecKind + " of 400)");
        Check(found == 400 && bad == 0, "a forced roll always finds a chest spot 3 to 5 tiles out from the hallway, clear of the start room, buildable to, the same each time (" + found + " found, " + bad + " bad)");
        Check(rolled >= 20 && rolled <= 64, "a hallway drops one about 10% of the time (" + rolled + " of 400)");

        var two = new List<MapNode> { new MapNode { X = 5000, Y = -2000, Kind = NodeKind.Chest }, new MapNode { X = 5000, Y = 3000, Kind = NodeKind.VectorChest } };
        Check(Enumerable.Range(1, 50).All(s => !Expansion.RoomChestFor(s, hall, two, 1, false).HasValue), "no chest while two wait unclaimed");
        var spentOne = new List<MapNode> { two[0], new MapNode { X = 5000, Y = 3000, Kind = NodeKind.Chest, Spent = true } };
        Check(Enumerable.Range(1, 50).Any(s => Expansion.RoomChestFor(s, hall, spentOne, 1, false).HasValue), "an opened chest doesn't count");

        // Two branches with a gap between them: the chest goes in the gap, so the room that claims it touches both.
        var map = new List<ExpansionRoom>();
        add(map, W, 0, 2200, 300);        // A: a hallway east along the top
        add(map, W, 500, 1200, 400);      // D: a room east off the start room, lower down
        var nr = add(map, W + 1900, 300, 300, 1200);  // N, the new one: a hallway south off A's far end
        int gapSide = 0, total = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            var nd = Expansion.RoomChestFor(seed, map, new List<MapNode>(), 1, false);
            if (!nd.HasValue) continue;
            total++;
            if (nd.Value.X < nr.X) gapSide++;
        }
        Check(total > 0 && gapSide == total, "the chest sits on the side facing the other branch (" + gapSide + " of " + total + ")");
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
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); }

        // Surprise rooms roll: over a few seeds, some of them drop a chest node outside their walls.
        var give = typeof(Game).GetMethod("GiveSurpriseRoom", flags);
        var seedField = typeof(Game).GetField("expandSeed", flags);
        int gotChest = 0, tries = 0;
        for (int sd = 1; sd <= 30; sd++)
        {
            g.Expanded.Clear(); g.Nodes.Clear(); seedField.SetValue(g, sd);
            g.BossSlot = true; g.State = MatchState.Intermission;
            give.Invoke(g, null);
            if (g.Expanded.Count == 0) continue;
            tries++;
            if (g.Nodes.Count > 0 && Expansion.IsChest(g.Nodes[0].Kind) && !Expansion.NodeClaimed(g.Expanded, g.Nodes[0])) gotChest++;
        }
        Check(tries >= 20 && gotChest >= 1 && gotChest < tries, "surprise rooms roll chests too (none within 10 tiles of the start room) (" + gotChest + " of " + tries + ")");

        // A claimed vector chest: nothing until someone walks up, then vector pickups and the node is spent, on both screens.
        foreach (var gg in new[] { g, cg }) { gg.Expanded.Clear(); gg.Nodes.Clear(); }
        string why;
        var room = Expansion.Custom(g.Expanded, (int)Game.Width, 200, 1000, 500, out why);
        Check(room.HasValue, "a room east of the start room (" + why + ")");
        g.Expanded.Add(room.Value);
        g.Nodes.Add(new MapNode { X = (short)(Game.Width + 550), Y = 450, Kind = NodeKind.VectorChest });
        typeof(Game).GetField("expWorld", flags).SetValue(g, null);
        typeof(Game).GetMethod("BroadcastExpansion", flags).Invoke(g, null);
        g.State = MatchState.Fighting;
        foreach (var p in g.Players.Values) p.Pos = p.Disp = new V2(800f, 450f);
        Run(s, dt, 1f, () => cg.Nodes.Count == 1, noPicks);
        Check(cg.Nodes.Count == 1 && cg.Nodes[0].Kind == NodeKind.VectorChest && !g.Nodes[0].Spent, "the vector chest reaches the client, shut");
        int coins0 = g.Coins.Values.Count(c => c.Vec);
        int vec0 = g.Players.Values.Sum(p => p.Vectors);
        var me = g.Players[1];
        me.Pos = me.Disp = g.Nodes[0].Pos + new V2(20f, 0f);
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
}
