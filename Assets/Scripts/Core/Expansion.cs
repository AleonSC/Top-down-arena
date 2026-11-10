using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a room of the Expanding arena does (Docs/ExpandingArena.md). Floor rooms carry a path's ground and hazards.
    public enum RoomKind : byte { Combat, Shop, Floor }

    /// One bought room: a box attached to its parent room (area index; 0 is the hub) with a door on their shared wall.
    /// Stored as the resolved box, so rebuilding the map needs no random numbers: the map is the hub plus this list.
    public struct ExpansionRoom
    {
        public int Parent;
        public int X, Y, W, H;
        public RoomKind Kind;
        public PathId Floor;
        public int Price;       // what the team paid (or, in an offer, what it costs)
        public int X1 => X + W;
        public int Y1 => Y + H;
        public string Shape => Expansion.ShapeName(W, H);
    }

    /// Deterministic random numbers (a plain xorshift), so every game builds the same offers from the same seed.
    public sealed class DetRandom
    {
        uint s;
        public DetRandom(int seed) { s = (uint)seed * 2654435761u + 0x9E3779B9u; if (s == 0) s = 1; for (int i = 0; i < 4; i++) Next(); }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public int Int(int n) => n <= 1 ? 0 : (int)(Next() % (uint)n);
        public float Float() => (Next() & 0xFFFFFF) / (float)0x1000000;
    }

    /// The Expanding arena's maps (Docs/ExpandingArena.md, step 2): room shapes, the seeded offers for each milestone and
    /// the World built from the hub plus the rooms bought so far. The map is a tree (every room has one door to its
    /// parent), rooms never overlap or touch except through their own door, and it stops growing at a size cap.
    public static class Expansion
    {
        public const int MaxRooms = 12;          // bought rooms (the hub is extra)
        public const int MaxWidth = 6400, MaxHeight = 4000;  // the whole map's bounding box
        public const int Step = 100;             // rooms slide along a wall in steps of this
        public const int MinDoorRun = 300;       // a room must share at least this much wall with its parent (a door, plus a margin)
        public const int Milestone = 5;          // an offer every this many waves

        public static readonly int[][] Shapes =
        {
            new[] { 600, 400 }, new[] { 800, 600 }, new[] { 1000, 700 }, new[] { 900, 900 },
            new[] { 1200, 300 }, new[] { 300, 900 },
        };
        static readonly string[] shapeNames = { "Closet", "Room", "Chamber", "Plaza", "Hall", "Corridor" };

        /// The floors a Floor room can carry (paths whose ground and hazards read well as a room).
        public static readonly PathId[] Floors = { PathId.SwarmFields, PathId.SniperRidge, PathId.EliteGauntlet, PathId.BurningGrounds, PathId.Sanctuary, PathId.FrozenLake, PathId.BloodMoon };

        public static string ShapeName(int w, int h)
        {
            for (int i = 0; i < Shapes.Length; i++)
                if (Shapes[i][0] == w && Shapes[i][1] == h)
                    return shapeNames[i];
            return "Room";
        }

        static int ShapeIndex(int w, int h)
        {
            for (int i = 0; i < Shapes.Length; i++)
                if (Shapes[i][0] == w && Shapes[i][1] == h)
                    return i;
            return -1;
        }

        /// A room's price: bigger costs more, a shop and a floor cost a little extra, and each room already bought makes the next dearer.
        public static int PriceFor(RoomKind kind, int w, int h, int built)
        {
            float p = 40f + w * h / 12000f;
            p *= kind == RoomKind.Shop ? 1.3f : kind == RoomKind.Floor ? 1.15f : 1f;
            p *= 1f + 0.2f * built;
            return (int)Math.Round(p / 5f) * 5;
        }

        // --- Geometry ----------------------------------------------------------------------------------------------

        /// The box of area i (0 is the hub).
        static void BoxOf(IList<ExpansionRoom> rooms, int area, out int x0, out int y0, out int x1, out int y1)
        {
            if (area == 0) { x0 = 0; y0 = 0; x1 = (int)Game.Width; y1 = (int)Game.Height; return; }
            var r = rooms[area - 1];
            x0 = r.X; y0 = r.Y; x1 = r.X1; y1 = r.Y1;
        }

        static bool Overlaps(int ax0, int ay0, int ax1, int ay1, int bx0, int by0, int bx1, int by1)
            => ax0 < bx1 && bx0 < ax1 && ay0 < by1 && by0 < ay1;

        /// Two boxes sharing a stretch of wall (touching along an edge, not just at a corner).
        static bool Touches(int ax0, int ay0, int ax1, int ay1, int bx0, int by0, int bx1, int by1)
        {
            bool sideX = (ax1 == bx0 || bx1 == ax0) && Math.Min(ay1, by1) > Math.Max(ay0, by0);
            bool sideY = (ay1 == by0 || by1 == ay0) && Math.Min(ax1, bx1) > Math.Max(ax0, bx0);
            return sideX || sideY;
        }

        /// Can a box (x0,y0,w,h) join `parent` with its door? Not overlapping or touching any other room, inside the size cap.
        static bool Fits(IList<ExpansionRoom> rooms, int parent, int x0, int y0, int w, int h)
        {
            int x1 = x0 + w, y1 = y0 + h;
            int minX = Math.Min(0, x0), minY = Math.Min(0, y0), maxX = Math.Max((int)Game.Width, x1), maxY = Math.Max((int)Game.Height, y1);
            for (int i = 0; i < rooms.Count; i++)
            {
                minX = Math.Min(minX, rooms[i].X); minY = Math.Min(minY, rooms[i].Y);
                maxX = Math.Max(maxX, rooms[i].X1); maxY = Math.Max(maxY, rooms[i].Y1);
            }
            if (maxX - minX > MaxWidth || maxY - minY > MaxHeight)
                return false;
            for (int a = 0; a <= rooms.Count; a++)
            {
                int bx0, by0, bx1, by1;
                BoxOf(rooms, a, out bx0, out by0, out bx1, out by1);
                if (Overlaps(x0, y0, x1, y1, bx0, by0, bx1, by1))
                    return false;
                if (a != parent && Touches(x0, y0, x1, y1, bx0, by0, bx1, by1))
                    return false;
            }
            return true;
        }

        static int Doors(IList<ExpansionRoom> rooms, int area)
        {
            int n = area == 0 ? 0 : 1;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Parent == area)
                    n++;
            return n;
        }

        // --- Offers ------------------------------------------------------------------------------------------------

        /// Up to `count` different rooms the team can buy at this milestone, the same on every machine for the same
        /// seed, round and rooms bought. Empty at the size cap.
        public static List<ExpansionRoom> Offers(int seed, int round, IList<ExpansionRoom> built, int count = 3)
        {
            var offers = new List<ExpansionRoom>();
            if (built.Count >= MaxRooms)
                return offers;
            var rng = new DetRandom(seed * 7919 + round * 104729 + built.Count);
            int shops = 0;
            var usedShape = new int[Shapes.Length];
            var usedFloor = new HashSet<PathId>();
            foreach (var r in built)
            {
                shops += r.Kind == RoomKind.Shop ? 1 : 0;
                int si = ShapeIndex(r.W, r.H);
                if (si >= 0) usedShape[si]++;
                if (r.Kind == RoomKind.Floor) usedFloor.Add(r.Floor);
            }
            for (int attempt = 0; attempt < 300 && offers.Count < count; attempt++)
            {
                // A parent: rooms with few doors are likelier, so the map branches rather than strings out.
                float total = 0f;
                for (int a = 0; a <= built.Count; a++) total += 1f / (1f + Doors(built, a));
                float pick = rng.Float() * total;
                int parent = 0;
                for (int a = 0; a <= built.Count; a++)
                {
                    pick -= 1f / (1f + Doors(built, a));
                    if (pick <= 0f) { parent = a; break; }
                }
                // A shape: ones not bought yet are three times as likely.
                float st = 0f;
                for (int i = 0; i < Shapes.Length; i++) st += usedShape[i] == 0 ? 3f : 1f;
                float sp = rng.Float() * st;
                int shape = 0;
                for (int i = 0; i < Shapes.Length; i++)
                {
                    sp -= usedShape[i] == 0 ? 3f : 1f;
                    if (sp <= 0f) { shape = i; break; }
                }
                int w = Shapes[shape][0], h = Shapes[shape][1];
                // A side, and where along it (the room slides in steps, keeping at least MinDoorRun of wall shared).
                int bx0, by0, bx1, by1;
                BoxOf(built, parent, out bx0, out by0, out bx1, out by1);
                int side = rng.Int(4);  // 0 north, 1 east, 2 south, 3 west
                int x, y;
                if (side == 0 || side == 2)
                {
                    int lo = bx0 - w + MinDoorRun, hi = bx1 - MinDoorRun;
                    x = lo + rng.Int((hi - lo) / Step + 1) * Step;
                    y = side == 0 ? by0 - h : by1;
                }
                else
                {
                    int lo = by0 - h + MinDoorRun, hi = by1 - MinDoorRun;
                    y = lo + rng.Int((hi - lo) / Step + 1) * Step;
                    x = side == 1 ? bx1 : bx0 - w;
                }
                if (!Fits(built, parent, x, y, w, h))
                    continue;
                // A kind: a shop while the map is short of them, a floor (an unused one first), else a fight.
                float wShop = shops < 1 + built.Count / 6 && built.Count >= 1 ? 3f : 0f, wFloor = 3f, wCombat = 4f;
                float kp = rng.Float() * (wShop + wFloor + wCombat);
                var kind = kp < wShop ? RoomKind.Shop : kp < wShop + wFloor ? RoomKind.Floor : RoomKind.Combat;
                var floor = PathId.None;
                if (kind == RoomKind.Floor)
                {
                    var fresh = new List<PathId>();
                    foreach (var f in Floors) if (!usedFloor.Contains(f)) fresh.Add(f);
                    floor = fresh.Count > 0 ? fresh[rng.Int(fresh.Count)] : Floors[rng.Int(Floors.Length)];
                }
                var offer = new ExpansionRoom { Parent = parent, X = x, Y = y, W = w, H = h, Kind = kind, Floor = floor, Price = PriceFor(kind, w, h, built.Count) };
                bool dup = false;
                foreach (var o in offers)
                    dup |= (o.W == w && o.H == h && o.Kind == kind) || (o.Parent == parent && Math.Abs(o.X - x) + Math.Abs(o.Y - y) < 400);
                if (!dup)
                    offers.Add(offer);
            }
            return offers;
        }

        // --- The world ---------------------------------------------------------------------------------------------

        /// The World for the hub plus these rooms. With no rooms it is the hub alone, walled (the Single world has no walls).
        public static World Build(IList<ExpansionRoom> rooms)
        {
            int n = rooms.Count + 1;
            var areas = new RoomArea[n];
            var doors = new List<RoomDoor>();
            var spans = new List<WallSpan>();
            int minX = 0, minY = 0, maxX = (int)Game.Width, maxY = (int)Game.Height;
            for (int a = 0; a < n; a++)
            {
                int x0, y0, x1, y1;
                BoxOf(rooms, a, out x0, out y0, out x1, out y1);
                minX = Math.Min(minX, x0); minY = Math.Min(minY, y0); maxX = Math.Max(maxX, x1); maxY = Math.Max(maxY, y1);
                var kind = a == 0 ? RoomKind.Combat : rooms[a - 1].Kind;
                areas[a] = new RoomArea
                {
                    Name = a == 0 ? "Hub" : rooms[a - 1].Shape + (kind == RoomKind.Shop ? " (shop)" : kind == RoomKind.Floor ? " (" + rooms[a - 1].Floor + ")" : ""),
                    Min = new V2(x0, y0), Max = new V2(x1, y1), Hub = a == 0, Hall = a > 0 && Math.Min(x1 - x0, y1 - y0) <= 300,
                    Kind = kind, Floor = a == 0 ? PathId.None : rooms[a - 1].Floor,
                };
                // The four walls; the one facing the parent is the parent's own (it carries the door).
                int px0 = 0, py0 = 0, px1 = 0, py1 = 0;
                bool west = true, east = true, north = true, south = true;
                if (a > 0)
                {
                    var r = rooms[a - 1];
                    BoxOf(rooms, r.Parent, out px0, out py0, out px1, out py1);
                    int ox0 = Math.Max(x0, px0), ox1 = Math.Min(x1, px1), oy0 = Math.Max(y0, py0), oy1 = Math.Min(y1, py1);
                    if (x0 == px1) { west = false; doors.Add(new RoomDoor { Pos = new V2(x0, (oy0 + oy1) / 2f), Vertical = true, A = r.Parent, B = a }); }
                    else if (x1 == px0) { east = false; doors.Add(new RoomDoor { Pos = new V2(x1, (oy0 + oy1) / 2f), Vertical = true, A = r.Parent, B = a }); }
                    else if (y0 == py1) { north = false; doors.Add(new RoomDoor { Pos = new V2((ox0 + ox1) / 2f, y0), A = r.Parent, B = a }); }
                    else { south = false; doors.Add(new RoomDoor { Pos = new V2((ox0 + ox1) / 2f, y1), A = r.Parent, B = a }); }
                }
                if (west) spans.Add(new WallSpan(true, x0, y0, y1));
                if (east) spans.Add(new WallSpan(true, x1, y0, y1));
                if (north) spans.Add(new WallSpan(false, y0, x0, x1));
                if (south) spans.Add(new WallSpan(false, y1, x0, x1));
            }
            return new World
            {
                Name = "Expanding",
                Areas = areas, Doors = doors.ToArray(), Portals = new WorldPortal[0], Spans = spans.ToArray(), Cover = new Solid[0],
                Min = new V2(minX, minY), VaultMin = new V2(minX, minY), Max = new V2(maxX, maxY),
                Hub = 0, Vault = -1,
            };
        }
    }
}
