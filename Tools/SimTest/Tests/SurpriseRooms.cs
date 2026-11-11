using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Surprise rooms (Expansion.Surprise, Docs/SurpriseRooms.md step 1): five well-shaped rooms, set against the map where they close
// loops, the same on every machine, free.
static partial class Program
{
    static void ExpandingSurpriseTest()
    {
        string why;
        var none = new List<ExpansionRoom>();
        var shapes = (SurpriseShape[])Enum.GetValues(typeof(SurpriseShape));
        int[] tiles = { 96, 81, 54, 63, 47 };  // floor tiles: ring, cross, L, T, chamfer
        foreach (var s in shapes)
        {
            var b = Expansion.SurpriseBase(s);
            int n = 0;
            foreach (bool f in b) n += f ? 1 : 0;
            Check(n == tiles[(int)s] && n * 10000 <= Expansion.MaxArea, Expansion.SurpriseName(s) + " has " + n + " floor tiles (" + tiles[(int)s] + ")");
            // Four turns and a mirror keep the floor, and a quarter turn swaps the footprint.
            var t = Expansion.SurpriseMask(s, 1, true);
            int m = 0;
            foreach (bool f in t) m += f ? 1 : 0;
            Check(m == n && t.GetLength(0) == b.GetLength(1) && t.GetLength(1) == b.GetLength(0), Expansion.SurpriseName(s) + " keeps its floor when turned and mirrored");
            var opts = Expansion.SurpriseOptions(none, s);
            Check(opts.Count > 0, Expansion.SurpriseName(s) + " fits on the plain hub (" + opts.Count + " spots)");
            if (opts.Count == 0) continue;
            for (int i = 0; i < opts.Count; i += Math.Max(1, opts.Count / 12))
            {
                var o = opts[i];
                var w = Expansion.Build(o.Rooms);
                bool ok = o.Rooms.All(r => r.Price == 0 && r.Kind == RoomKind.Combat) && Expansion.ConnectedAreas(o.Rooms).All(c => c)
                          && Expansion.BoughtCount(o.Rooms) == 1 && w.Areas.Length == o.Rooms.Count + 1;
                for (int a = 0; a < o.Rooms.Count && ok; a++)
                {
                    bool hub = o.Rooms[a].X < (int)Game.Width && o.Rooms[a].X1 > 0 && o.Rooms[a].Y < (int)Game.Height && o.Rooms[a].Y1 > 0;
                    ok &= !hub;
                    for (int c = a + 1; c < o.Rooms.Count; c++)
                        ok &= o.Rooms[a].X1 <= o.Rooms[c].X || o.Rooms[c].X1 <= o.Rooms[a].X || o.Rooms[a].Y1 <= o.Rooms[c].Y || o.Rooms[c].Y1 <= o.Rooms[a].Y;
                }
                int floor = o.Rooms.Sum(r => r.W * r.H / 10000);
                Check(ok && floor == tiles[(int)s], Expansion.SurpriseName(s) + " spot " + i + ": free, one room, connected, no overlap, " + floor + " tiles");
            }
        }
        // The ring is a loop: its lane joins all the way round (every piece has two neighbours or more).
        var ring = Expansion.SurpriseOptions(none, SurpriseShape.Ring)[0].Rooms;
        var rw = Expansion.Build(ring);
        var deg = new int[ring.Count + 1];
        foreach (var d in rw.Doors) { deg[d.A]++; deg[d.B]++; }
        int ringPieces = 0, closed = 0;
        for (int a = 1; a <= ring.Count; a++) { ringPieces++; closed += deg[a] >= 2 ? 1 : 0; }
        Check(ring.Count >= 4 && closed == ringPieces, "the ring is four boxes or more with a way round (" + ring.Count + " boxes)");

        // Closing a loop: two rooms above the hub with a gap; the best spot touches both.
        string fw;
        var pair = new List<ExpansionRoom> { Expansion.Custom(none, 0, -600, 600, 600, out fw).Value };
        pair.Add(Expansion.Custom(pair, 1000, -600, 600, 600, out fw).Value);
        bool anyLoop = false;
        foreach (var s in shapes)
        {
            var o = Expansion.SurpriseOptions(pair, s);
            Check(o.Count > 0 && o.Zip(o.Skip(1), (p, q) => p.Score >= q.Score).All(x => x), Expansion.SurpriseName(s) + " options come best first");
            anyLoop |= o.Count > 0 && o[0].Loops >= 1;
        }
        Check(anyLoop, "with a gap between two rooms, some shape's best spot closes a loop");

        // Deterministic, free, and the pick is one of the best three.
        var a1 = Expansion.Surprise(7, 10, pair, SurpriseShape.Cross, out why);
        var a2 = Expansion.Surprise(7, 10, pair, SurpriseShape.Cross, out why);
        Check(a1.HasValue && a2.HasValue && a1.Value.Rooms[0].X == a2.Value.Rooms[0].X && a1.Value.Rooms[0].Y == a2.Value.Rooms[0].Y && a1.Value.Shape == a2.Value.Shape, "the same seed, wave and map give the same room");
        var top = Expansion.SurpriseOptions(pair, a1.Value.Shape).Take(Expansion.SurpriseTop).Select(o => o.Rooms[0].X + "," + o.Rooms[0].Y).ToList();
        Check(top.Contains(a1.Value.Rooms[0].X + "," + a1.Value.Rooms[0].Y), "it is one of the " + Expansion.SurpriseTop + " best spots");
        var picks = new HashSet<string>();
        for (int sd = 1; sd < 40; sd++)
        {
            var p = Expansion.Surprise(sd, 10, pair, SurpriseShape.Cross, out why);
            if (p.HasValue) picks.Add(p.Value.Rooms[0].X + "," + p.Value.Rooms[0].Y);
        }
        Check(picks.Count >= 2 && picks.Count <= Expansion.SurpriseTop, "other seeds pick other spots (" + picks.Count + ")");

        // A full map gets nothing.
        var full = new List<ExpansionRoom>();
        for (int i = 0; i < Expansion.MaxRooms; i++) full.Add(new ExpansionRoom { Parent = 0, X = 10000 + i * 400, Y = 0, W = 300, H = 300 });
        Check(Expansion.Surprise(1, 10, full, SurpriseShape.Ring, out why) == null && why != null, "a full map gets no surprise room: " + why);
    }
}
