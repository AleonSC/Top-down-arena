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
            if (n.Kind != NodeKind.Chest || Expansion.NodeClaimed(hall, n) || off < 350 || off > 550 || !hubFar || !Expansion.NodeReachable(hall, n)
                || !Expansion.RoomChestFor(seed, hall, new List<MapNode>(), 1, false).Value.Equals(n))
                bad++;
            if (Expansion.RoomChestFor(seed, hall, new List<MapNode>(), 1).HasValue)
                rolled++;
        }
        Check(found == 400 && bad == 0, "a forced roll always finds a chest spot 3 to 5 tiles out from the hallway, clear of the start room, buildable to, the same each time (" + found + " found, " + bad + " bad)");
        Check(rolled >= 20 && rolled <= 64, "a hallway drops one about 10% of the time (" + rolled + " of 400)");

        var two = new List<MapNode> { new MapNode { X = 5000, Y = -2000, Kind = NodeKind.Chest }, new MapNode { X = 5000, Y = 3000, Kind = NodeKind.Chest } };
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
}
