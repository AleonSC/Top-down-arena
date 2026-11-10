using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Expanding arena's rooms and offers (Expansion.cs, Docs/ExpandingArena.md step 2): the same offers from the same
// seed, rooms that never overlap or touch except through their own door, a connected map with open doors and steering
// spots either side of them, a size cap, and a mix of shapes and kinds.
static partial class Program
{
    static void ExpansionTest()
    {
        var none = new List<ExpansionRoom>();
        var a = Expansion.Offers(11, 5, none);
        var b = Expansion.Offers(11, 5, none);
        Check(a.Count == 3 && a.SequenceEqual(b), "the same seed, round and map give the same three offers");
        Check(Enumerable.Range(1, 40).Any(s => !Expansion.Offers(s, 5, none).SequenceEqual(a)) && !Expansion.Offers(11, 10, none).SequenceEqual(a), "other seeds and rounds give other offers");
        Check(a.All(o => o.Parent == 0 && o.Price > 0 && o.Kind != RoomKind.Shop), "the first offers all join the hub, cost something and none is a shop");

        var hubOnly = Expansion.Build(none);
        Check(hubOnly.Areas.Length == 1 && hubOnly.Doors.Length == 0 && hubOnly.WallsFor(false).Length == 4 && hubOnly.Max.X == Game.Width && hubOnly.Max.Y == Game.Height,
            "with nothing bought the world is the hub alone, walled in");

        int badFit = 0, badTree = 0, badDoor = 0, badWall = 0, badSteer = 0, badCap = 0, worlds = 0, offersSeen = 0;
        var shapes = new HashSet<string>(); var kinds = new HashSet<RoomKind>(); var floors = new HashSet<PathId>();
        int maxShops = 0; int prevPriceBad = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var built = new List<ExpansionRoom>();
            var rng = new Random(seed);
            for (int round = 5; ; round += 5)
            {
                var offers = Expansion.Offers(seed, round, built);
                if (built.Count >= Expansion.MaxRooms) { badCap += offers.Count; break; }
                if (offers.Count == 0) break;
                offersSeen += offers.Count;
                foreach (var o in offers) { shapes.Add(o.Shape); kinds.Add(o.Kind); if (o.Kind == RoomKind.Floor) floors.Add(o.Floor); }
                var pick = offers[rng.Next(offers.Count)];
                if (built.Count > 0 && pick.Price <= Expansion.PriceFor(pick.Kind, pick.W, pick.H, 0)) prevPriceBad++;
                built.Add(pick);
            }
            worlds++;
            var w = Expansion.Build(built);
            maxShops = Math.Max(maxShops, built.Count(r => r.Kind == RoomKind.Shop));
            // no overlaps, and only the parent shares a wall
            for (int i = 0; i < w.Areas.Length; i++)
                for (int j = i + 1; j < w.Areas.Length; j++)
                {
                    var p = w.Areas[i]; var q = w.Areas[j];
                    bool overlap = p.Min.X < q.Max.X && q.Min.X < p.Max.X && p.Min.Y < q.Max.Y && q.Min.Y < p.Max.Y;
                    bool sharesSide = ((p.Max.X == q.Min.X || q.Max.X == p.Min.X) && Math.Min(p.Max.Y, q.Max.Y) > Math.Max(p.Min.Y, q.Min.Y))
                                   || ((p.Max.Y == q.Min.Y || q.Max.Y == p.Min.Y) && Math.Min(p.Max.X, q.Max.X) > Math.Max(p.Min.X, q.Min.X));
                    bool doored = w.Doors.Any(d => d.Joins(i) && d.Joins(j));
                    if (overlap || (sharesSide && !doored)) badFit++;
                }
            if (w.Max.X - w.Min.X > Expansion.MaxWidth || w.Max.Y - w.Min.Y > Expansion.MaxHeight) badCap++;
            // connected: every room reachable from the hub through doors (a tree: one door per room)
            var seen = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                foreach (var d in w.Doors.Where(d => d.Joins(cur)))
                    if (seen.Add(d.Other(cur))) queue.Enqueue(d.Other(cur));
            }
            if (seen.Count != w.Areas.Length || w.Doors.Length != w.Areas.Length - 1) badTree++;
            // each door sits on both rooms' shared wall, DoorW inside it, and is open (no solid) in the walls and the floor plan
            var plan = w.With(LayoutId.Open);
            foreach (var d in w.Doors)
            {
                var pa = w.Areas[d.A]; var pb = w.Areas[d.B];
                bool onBoth = pa.Contains(d.Pos, 1f) && pb.Contains(d.Pos, 1f);
                float run = d.Vertical ? Math.Min(pa.Max.Y, pb.Max.Y) - Math.Max(pa.Min.Y, pb.Min.Y) : Math.Min(pa.Max.X, pb.Max.X) - Math.Max(pa.Min.X, pb.Min.X);
                if (!onBoth || run < Expansion.MinDoorRun || plan.Solids.Any(sd => Layouts.Inside(sd, d.Pos, 30f))) badDoor++;
                if (!plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos + d.Across * Rooms.DoorPad) < 1f) || !plan.Waypoints.Any(wp => wp.DistanceTo(d.Pos - d.Across * Rooms.DoorPad) < 1f)) badSteer++;
            }
            // every room's centre is in the open and in its own area
            for (int i = 0; i < w.Areas.Length; i++)
                if (plan.Solids.Any(sd => Layouts.Inside(sd, w.Areas[i].Mid, 0f)) || w.AreaAt(w.Areas[i].Mid) != i) badWall++;
        }
        Check(offersSeen > 600, "offers keep coming until the cap (" + offersSeen + " seen over " + worlds + " maps)");
        Check(badFit == 0, "rooms never overlap and share a wall only through their own door (" + badFit + " bad)");
        Check(badTree == 0, "every map is connected, one door per bought room (" + badTree + " bad)");
        Check(badDoor == 0 && badSteer == 0, "every door sits on both rooms' shared wall, is open, and has a steering spot each side (" + badDoor + "/" + badSteer + " bad)");
        Check(badWall == 0, "every room's middle is open ground in its own area (" + badWall + " bad)");
        Check(badCap == 0, "the map stops at " + Expansion.MaxRooms + " rooms and its size cap (" + badCap + " over)");
        Check(shapes.Count == Expansion.Shapes.Length && kinds.Count == 3 && floors.Count >= 4, "the offers use every shape (" + shapes.Count + "), every kind and several floors (" + floors.Count + ")");
        Check(maxShops >= 1 && maxShops <= 3, "shops turn up, a few at most (" + maxShops + ")");
        Check(prevPriceBad == 0, "each room costs more than the same room would have at the start");
    }
}
