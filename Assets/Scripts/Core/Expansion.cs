using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a room of the Expanding arena does (Docs/ExpandingArena.md). Floor rooms carry a path's ground and hazards.
    public enum RoomKind : byte { Combat, Shop, Floor, Vendor, Gatekeeper, Opening, Link }  // (Vendor and Gatekeeper: the floating rooms, Docs/FloatingRooms.md; Opening and Link: only in a round's proposals, a stretch of wall to open or a portal pair)

    /// A stretch of interior wall the team opened (the Expanding arena): a vertical wall at x = At from y = From to Till, or a horizontal one at y = At.
    public struct WallCut : IEquatable<WallCut>
    {
        public bool Vertical;
        public short At, From, Till;
        public bool Equals(WallCut o) => Vertical == o.Vertical && At == o.At && From == o.From && Till == o.Till;
        public int Length => Till - From;
    }

    /// A portal pair the team bought (the Expanding arena, Docs/RoomBuilding2.md step 2): two spots on the 100 px grid, each in a room.
    public struct PortalLink : IEquatable<PortalLink>
    {
        public short AX, AY, BX, BY;
        public V2 A => new V2(AX, AY);
        public V2 B => new V2(BX, BY);
        public bool Equals(PortalLink o) => AX == o.AX && AY == o.AY && BX == o.BX && BY == o.BY;
    }

    /// One bought room: a box attached to its parent room (area index; 0 is the hub) with a door on their shared wall.
    /// Stored as the resolved box, so rebuilding the map needs no random numbers: the map is the hub plus this list.
    public struct ExpansionRoom
    {
        public int Parent;
        public int X, Y, W, H;
        public RoomKind Kind;
        public PathId Floor;
        public int Price;       // what it costs in vectors (Expansion.Cost; the drawer pays if it wins the vote)
        public int Owner;       // the player who drew it (0: none)
        public bool Floating;   // placed by the game off the map (a shop, vendor or Gatekeeper), not drawn: sealed until a built room touches it
        public byte Leg;        // a bent hallway's second or third leg (1, 2): it opens full width onto the leg just before it in the list (Expansion.Hall)
        public int X1 => X + W;
        public int Y1 => Y + H;
        public string Shape => Expansion.ShapeName(W, H);
    }

    /// A room modifier the team holds (Docs/RoomModifiers.md): spent once on a Combat room to make it a Shop or a Floor room.
    public struct RoomToken
    {
        public RoomKind Kind;   // Shop or Floor
        public PathId Floor;    // for a Floor token
        public string Name => Kind == RoomKind.Shop ? "Shop kit" : Paths.Get(Floor).Name + " floor";
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
        public const int MaxTokens = 6;          // room kits the team can hold
        public const int MaxRooms = 24;          // bought rooms (the hub is extra)
        public const int MaxWidth = 6400, MaxHeight = 4000;  // the whole map's bounding box
        public const int Step = 100;             // rooms slide along a wall in steps of this
        public const int MinDoorRun = 300;       // a room must share at least this much wall with its parent (a door, plus a margin)
        public const int Milestone = 5;          // an offer every this many waves
        public const int MinSide = 300;          // a drawn room's shortest side (a hallway is this wide)
        public const int MaxSide = 1200, MaxArea = 960000;  // and its limits
        public const int HallMax = 2400;         // a hallway (MinSide wide) may run this long
        public const int MaxLegs = 3;            // a bent hallway: up to this many legs, joined end to end, built in one go

        public static readonly int[][] Shapes =
        {
            new[] { 600, 400 }, new[] { 800, 600 }, new[] { 1000, 700 }, new[] { 900, 900 },
            new[] { 1200, 300 }, new[] { 300, 900 },
        };
        static readonly string[] shapeNames = { "Closet", "Room", "Chamber", "Plaza", "Hall", "Corridor" };

        /// The floors a Floor room can carry (paths whose ground and hazards read well as a room).
        public static readonly PathId[] Floors = { PathId.SwarmFields, PathId.SniperRidge, PathId.EliteGauntlet, PathId.BurningGrounds, PathId.Sanctuary, PathId.FrozenLake, PathId.BloodMoon };

        /// A Floor token for this wave: a floor not on the map or in the tokens held yet when one is left (else any), seeded so every machine agrees.
        public static RoomToken FloorToken(int seed, int wave, IList<ExpansionRoom> built, IList<RoomToken> held)
        {
            var free = new List<PathId>();
            foreach (var f in Floors)
            {
                bool used = false;
                foreach (var r in built)
                    used |= r.Kind == RoomKind.Floor && r.Floor == f;
                foreach (var t in held)
                    used |= t.Kind == RoomKind.Floor && t.Floor == f;
                if (!used)
                    free.Add(f);
            }
            var pool = free.Count > 0 ? free.ToArray() : Floors;
            return new RoomToken { Kind = RoomKind.Floor, Floor = pool[new DetRandom(seed * 31 + wave).Int(pool.Length)] };
        }

        /// Can this token turn this bought room (index into the list) into its kind? Combat rooms only: not a floating room, not one already changed.
        public static bool CanRetype(IList<ExpansionRoom> built, int index, RoomToken token, out string why)
        {
            why = null;
            if (index < 0 || index >= built.Count) { why = "No such room"; return false; }
            if (token.Kind != RoomKind.Shop && token.Kind != RoomKind.Floor) { why = "Not a room kit"; return false; }
            if (token.Kind == RoomKind.Floor && Array.IndexOf(Floors, token.Floor) < 0) { why = "Not a floor"; return false; }
            var r = built[index];
            if (r.Floating) { why = "Floating rooms keep their job"; return false; }
            if (r.Kind != RoomKind.Combat) { why = "Already changed: one modifier per room"; return false; }
            return true;
        }

        /// The room with the token applied (call CanRetype first).
        public static ExpansionRoom Retype(ExpansionRoom room, RoomToken token)
        {
            room.Kind = token.Kind;
            room.Floor = token.Kind == RoomKind.Floor ? token.Floor : PathId.None;
            return room;
        }

        public static string ShapeName(int w, int h)
        {
            for (int i = 0; i < Shapes.Length; i++)
                if (Shapes[i][0] == w && Shapes[i][1] == h)
                    return shapeNames[i];
            return Math.Min(w, h) <= MinSide ? "Hallway" : "Room";  // (a drawn one)
        }

        static int ShapeIndex(int w, int h)
        {
            for (int i = 0; i < Shapes.Length; i++)
                if (Shapes[i][0] == w && Shapes[i][1] == h)
                    return i;
            return -1;
        }

        /// (Dormant since the vector economy: a gold price from the purse days. Rooms now cost Cost vectors.) A room's price: bigger costs more, a shop and a floor cost a little extra, and each room already bought makes the next dearer.
        public static int PriceFor(RoomKind kind, int w, int h, int built)
        {
            float p = 120f + w * h / 4000f;
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

        /// How much wall two boxes share (0 when they only meet at a corner or don't touch).
        static int SharedRun(int ax0, int ay0, int ax1, int ay1, int bx0, int by0, int bx1, int by1)
        {
            if (ax1 == bx0 || bx1 == ax0)
                return Math.Max(0, Math.Min(ay1, by1) - Math.Max(ay0, by0));
            if (ay1 == by0 || by1 == ay0)
                return Math.Max(0, Math.Min(ax1, bx1) - Math.Max(ax0, bx0));
            return 0;
        }

        /// Can a box go here? It must not overlap a room, must share at least MinDoorRun of wall with some room (that
        /// room is its parent: the longest shared wall; a shorter touch just stays wall), and the map stays inside
        /// its size cap. Every other room it shares MinDoorRun of wall with gets a door too: that closes a loop.
        public static bool Placement(IList<ExpansionRoom> rooms, int x0, int y0, int w, int h, out int parent, out int loops, out string why)
        {
            parent = -1; loops = 0; why = null;
            int x1 = x0 + w, y1 = y0 + h;
            if (BoughtCount(rooms) >= MaxRooms) { why = "The map is full"; return false; }
            var linked = ConnectedAreas(rooms);
            int minX = Math.Min(0, x0), minY = Math.Min(0, y0), maxX = Math.Max((int)Game.Width, x1), maxY = Math.Max((int)Game.Height, y1);
            for (int i = 0; i < rooms.Count; i++)
            {
                minX = Math.Min(minX, rooms[i].X); minY = Math.Min(minY, rooms[i].Y);
                maxX = Math.Max(maxX, rooms[i].X1); maxY = Math.Max(maxY, rooms[i].Y1);
            }
            if (maxX - minX > MaxWidth || maxY - minY > MaxHeight) { why = "The map would get too big"; return false; }
            int best = 0;
            for (int a = 0; a <= rooms.Count; a++)
            {
                int bx0, by0, bx1, by1;
                BoxOf(rooms, a, out bx0, out by0, out bx1, out by1);
                if (Overlaps(x0, y0, x1, y1, bx0, by0, bx1, by1)) { why = "Overlaps a room"; return false; }
                int run = SharedRun(x0, y0, x1, y1, bx0, by0, bx1, by1);  // (a shorter stretch is fine: it stays wall, no door)
                if (run >= MinDoorRun)
                {
                    loops++;
                    if (linked[a] && run > best) { best = run; parent = a; }  // (a floating room nobody has connected can't be the parent)
                }
            }
            if (parent < 0) { why = "Must share at least " + MinDoorRun + " px of wall with the connected map"; return false; }
            loops--;  // (the parent's door isn't a loop)
            return true;
        }

        /// What a room costs in vectors (Docs/VectorBuilding.md): its new wall length in tiles, the perimeter minus every wall it
        /// shares with a room already there, so sharing walls is cheaper and a loop pays. A hallway (MinSide wide) pays half, rounded up:
        /// it is nearly all wall, so a full price made it the dearest thing to build.
        public static int Cost(IList<ExpansionRoom> built, int x, int y, int w, int h)
        {
            int shared = 0;
            for (int a = 0; a <= built.Count; a++)
            {
                int bx0, by0, bx1, by1;
                BoxOf(built, a, out bx0, out by0, out bx1, out by1);
                shared += SharedRun(x, y, x + w, y + h, bx0, by0, bx1, by1);
            }
            int tiles = (2 * (w + h) - shared) / Step;
            if (Math.Min(w, h) <= MinSide)
                tiles = (tiles + 1) / 2;
            return Math.Max(1, tiles);
        }

        public static int Cost(IList<ExpansionRoom> built, ExpansionRoom r) => Cost(built, r.X, r.Y, r.W, r.H);

        /// Rooms the team has drawn (floating rooms, placed by the game, don't count towards the cap).
        public static int BoughtCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            foreach (var r in rooms)
                if (!r.Floating)
                    n++;
            return n;
        }

        /// Which areas (0 = the hub) are on the connected map: reachable from the hub through walls shared for a door's width. A floating
        /// room joins it once a built room touches it.
        public static bool[] ConnectedAreas(IList<ExpansionRoom> rooms)
        {
            int n = rooms.Count + 1;
            var seen = new bool[n];
            var queue = new Queue<int>();
            seen[0] = true;
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                int ax0, ay0, ax1, ay1;
                BoxOf(rooms, a, out ax0, out ay0, out ax1, out ay1);
                for (int b = 0; b < n; b++)
                {
                    if (seen[b])
                        continue;
                    int bx0, by0, bx1, by1;
                    BoxOf(rooms, b, out bx0, out by0, out bx1, out by1);
                    if (SharedRun(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1) >= MinDoorRun)
                    {
                        seen[b] = true;
                        queue.Enqueue(b);
                    }
                }
            }
            return seen;
        }

        /// How many floating rooms nobody has connected yet.
        public static int SealedCount(IList<ExpansionRoom> rooms)
        {
            var linked = ConnectedAreas(rooms);
            int n = 0;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Floating && !linked[i + 1])
                    n++;
            return n;
        }

        public const int MaxSealed = 6;  // floating rooms waiting to be connected at once

        /// A floating room for this boss cycle (Docs/FloatingRooms.md): a shop, vendor or Gatekeeper room placed 4 to 8 tiles off a room on the
        /// connected map, touching nothing and at least 2 tiles from every room, inside the size cap. The same on every machine for the same
        /// seed, cycle and map. Null if nothing fits.
        public static ExpansionRoom? FloatingFor(int seed, int cycle, IList<ExpansionRoom> built, RoomKind kind)
        {
            int w = kind == RoomKind.Gatekeeper ? 800 : 600, h = kind == RoomKind.Gatekeeper ? 600 : 400;
            var rng = new DetRandom(seed * 40503 + cycle * 9176 + (int)kind * 131);
            var linked = ConnectedAreas(built);
            for (int attempt = 0; attempt < 120; attempt++)
            {
                int a = rng.Int(built.Count + 1);
                if (!linked[a])
                    continue;
                int bx0, by0, bx1, by1;
                BoxOf(built, a, out bx0, out by0, out bx1, out by1);
                int side = rng.Int(4), gap = 400 + rng.Int(5) * 100, x, y;
                if (side == 0 || side == 2)
                {
                    int lo = bx0 - w / 2, hi = bx1 - w / 2;
                    x = lo + rng.Int((hi - lo) / Step + 1) * Step;
                    y = side == 0 ? by0 - gap - h : by1 + gap;
                }
                else
                {
                    int lo = by0 - h / 2, hi = by1 - h / 2;
                    y = lo + rng.Int((hi - lo) / Step + 1) * Step;
                    x = side == 1 ? bx1 + gap : bx0 - gap - w;
                }
                if (x % Step != 0 || y % Step != 0)
                    continue;
                int x1 = x + w, y1 = y + h;
                bool ok = true;
                int minX = Math.Min(0, x), minY = Math.Min(0, y), maxX = Math.Max((int)Game.Width, x1), maxY = Math.Max((int)Game.Height, y1);
                for (int i = 0; i <= built.Count && ok; i++)
                {
                    int cx0, cy0, cx1, cy1;
                    BoxOf(built, i, out cx0, out cy0, out cx1, out cy1);
                    ok = !Overlaps(x - 200, y - 200, x1 + 200, y1 + 200, cx0, cy0, cx1, cy1);  // (two tiles clear of everything)
                    minX = Math.Min(minX, cx0); minY = Math.Min(minY, cy0); maxX = Math.Max(maxX, cx1); maxY = Math.Max(maxY, cy1);
                }
                if (!ok || maxX - minX > MaxWidth || maxY - minY > MaxHeight)
                    continue;
                var found = new ExpansionRoom { Parent = 0, X = x, Y = y, W = w, H = h, Kind = kind, Floating = true };
                var with = new List<ExpansionRoom>(built) { found };
                if (!ConnectorFor(with, with.Count - 1).HasValue)
                    continue;  // (a floating room nobody could ever build to isn't worth placing)
                return found;
            }
            return null;
        }

        /// The cheapest box that would connect a sealed floating room (its index in `built`) to the map: touching it and a connected room.
        /// Null if none fits. Bots use it to build to shops; the balance report reads its price.
        public static ExpansionRoom? ConnectorFor(IList<ExpansionRoom> built, int floatIndex)
        {
            var f = built[floatIndex];
            ExpansionRoom? best = null;
            string why;
            for (int side = 0; side < 4; side++)
                for (int along = 300; along <= 600; along += Step)      // the box's width along the floating room's wall
                    for (int reach = 300; reach <= 900; reach += Step)       // ...and how far it reaches out from it
                    {
                        bool horizontalWall = side < 2;                  // 0 north, 1 south: the box sits above or below
                        int w = horizontalWall ? along : reach, h = horizontalWall ? reach : along;
                        int lo = (horizontalWall ? f.X : f.Y) - along + MinDoorRun, hi = (horizontalWall ? f.X1 : f.Y1) - MinDoorRun;
                        for (int p = lo; p <= hi; p += Step)
                        {
                            int x = horizontalWall ? p : side == 2 ? f.X - w : f.X1;
                            int y = horizontalWall ? (side == 0 ? f.Y - h : f.Y1) : p;
                            var c = Custom(built, x, y, w, h, out why);
                            if (c.HasValue && (!best.HasValue || c.Value.Price < best.Value.Price))
                                best = c;
                        }
                    }
            return best;
        }

        /// How many loops a room would close (other rooms it gets a door to besides its parent).
        public static int LoopsOf(IList<ExpansionRoom> built, ExpansionRoom r)
        {
            int p, loops; string why;
            return Placement(built, r.X, r.Y, r.W, r.H, out p, out loops, out why) ? loops : 0;
        }

        // --- Opened walls: a stretch of wall between two rooms the team opens for vectors (1 per tile) ---

        public const int MinCut = 200, MaxCuts = 40;  // the shortest opening (2 tiles), and how many a map can hold

        /// A proposal's ExpansionRoom for a wall opening: the stretch as a thin box (width or height 0), priced at minus the refund.
        public static ExpansionRoom CutProposal(WallCut c, int refund) => new ExpansionRoom
        {
            Kind = RoomKind.Opening,
            X = c.Vertical ? c.At : c.From, Y = c.Vertical ? c.From : c.At,
            W = c.Vertical ? 0 : c.Till - c.From, H = c.Vertical ? c.Till - c.From : 0,
            Price = -refund,
        };

        public static WallCut CutOf(ExpansionRoom r) => r.W == 0
            ? new WallCut { Vertical = true, At = (short)r.X, From = (short)r.Y, Till = (short)(r.Y + r.H) }
            : new WallCut { Vertical = false, At = (short)r.Y, From = (short)r.X, Till = (short)(r.X + r.W) };

        /// Every stretch of wall two rooms share on this map (vertical?, its line, from, till): where an opening can go.
        public static List<int[]> SharedWalls(World w)
        {
            var list = new List<int[]>();
            for (int a = 0; a < w.Areas.Length; a++)
                for (int b = a + 1; b < w.Areas.Length; b++)
                {
                    var p = w.Areas[a]; var q = w.Areas[b];
                    int px0 = (int)p.Min.X, py0 = (int)p.Min.Y, px1 = (int)p.Max.X, py1 = (int)p.Max.Y, qx0 = (int)q.Min.X, qy0 = (int)q.Min.Y, qx1 = (int)q.Max.X, qy1 = (int)q.Max.Y;
                    int lo = Math.Max(py0, qy0), hi = Math.Min(py1, qy1);
                    if (hi > lo && (px1 == qx0 || qx1 == px0))
                        list.Add(new[] { 1, px1 == qx0 ? px1 : px0, lo, hi, a, b });
                    lo = Math.Max(px0, qx0); hi = Math.Min(px1, qx1);
                    if (hi > lo && (py1 == qy0 || qy1 == py0))
                        list.Add(new[] { 0, py1 == qy0 ? py1 : py0, lo, hi, a, b });
                }
            return list;
        }

        /// What opening this stretch gives back: 1 vector per tile of it that is still wall. 0 with `why` set if it can't be opened
        /// (not on the grid, too short, not a wall two rooms share, a sealed room behind it, or already open).
        public static int CutRefund(IList<ExpansionRoom> rooms, IList<WallCut> cuts, WallCut c, out string why)
        {
            why = null;
            if (c.At % Step != 0 || c.From % Step != 0 || c.Till % Step != 0) { why = "Not on the grid"; return 0; }
            if (c.Length < MinCut) { why = "Too short (at least " + MinCut / Step + " tiles)"; return 0; }
            if (cuts != null && cuts.Count >= MaxCuts) { why = "Too many openings"; return 0; }
            var w = Build(rooms, cuts);
            foreach (var s in SharedWalls(w))
            {
                if ((s[0] == 1) != c.Vertical || s[1] != c.At || c.From < s[2] || c.Till > s[3])
                    continue;
                if (w.Areas[s[4]].Sealed || w.Areas[s[5]].Sealed) { why = "A sealed room: build to it first"; return 0; }
                int open = 0;
                for (int u = c.From; u < c.Till; u += Step)
                {
                    float mid = u + Step / 2f;
                    foreach (var d in w.Doors)
                        if (d.Vertical == c.Vertical && Math.Abs((c.Vertical ? d.Pos.X : d.Pos.Y) - c.At) < 0.5f && Math.Abs((c.Vertical ? d.Pos.Y : d.Pos.X) - mid) < d.W / 2f)
                        {
                            open++;
                            break;
                        }
                }
                int refund = c.Length / Step - open;
                if (refund <= 0) { why = "Already open"; return 0; }
                return refund;
            }
            why = "Not a wall between two rooms";
            return 0;
        }

        // --- Portal links (Docs/RoomBuilding2.md step 2): a portal pair between two rooms, bought with vectors ---

        public const int MaxLinks = 3;            // portal pairs a map can hold (not counted as rooms)
        public const int LinkBase = 8, LinkMax = 30;  // a link costs 8, +1 per 2 tiles of walking it saves, at most 30
        public const float LinkMinSave = 600f;    // it must save at least 6 tiles of walking
        public const float LinkEdge = 90f, LinkClear = 250f, LinkDoor = 150f;  // kept off walls, other portals and doors

        public static ExpansionRoom LinkProposal(PortalLink l, int price) => new ExpansionRoom
        {
            Kind = RoomKind.Link, X = l.AX, Y = l.AY, W = l.BX - l.AX, H = l.BY - l.AY, Price = price,
        };

        public static PortalLink LinkOf(ExpansionRoom r) => new PortalLink { AX = (short)r.X, AY = (short)r.Y, BX = (short)(r.X + r.W), BY = (short)(r.Y + r.H) };

        static int OpenArea(World w, V2 p, float pad)
        {
            for (int i = 0; i < w.Areas.Length; i++)
                if (!w.Areas[i].Sealed && w.Areas[i].Contains(p, -pad))
                    return i;
            return -1;
        }

        /// How far you walk from a to b on this map: through the doors and any portals already there (float.MaxValue: no way).
        public static float WalkDistance(World w, V2 a, V2 b)
        {
            var pos = new List<V2> { a, b };
            var areas = new List<int[]> { new[] { OpenArea(w, a, 0f) }, new[] { OpenArea(w, b, 0f) } };
            foreach (var d in w.Doors) { pos.Add(d.Pos); areas.Add(new[] { d.A, d.B }); }
            int firstPortal = pos.Count;
            foreach (var pt in w.Portals) { pos.Add(pt.A); areas.Add(new[] { w.AreaAt(pt.A) }); pos.Add(pt.B); areas.Add(new[] { w.AreaAt(pt.B) }); }
            int n = pos.Count;
            var dist = new float[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++) dist[i] = float.MaxValue;
            dist[0] = 0f;
            for (int step = 0; step < n; step++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && dist[i] < float.MaxValue && (u < 0 || dist[i] < dist[u]))
                        u = i;
                if (u < 0) break;
                if (u == 1) return dist[1];
                done[u] = true;
                for (int v = 0; v < n; v++)
                {
                    if (done[v]) continue;
                    float c = float.MaxValue;
                    if (u >= firstPortal && v >= firstPortal && (u - firstPortal) / 2 == (v - firstPortal) / 2)
                        c = 0f;  // the two ends of one portal
                    else
                        foreach (int x in areas[u]) foreach (int y in areas[v]) if (x >= 0 && x == y) c = pos[u].DistanceTo(pos[v]);
                    if (c < float.MaxValue && dist[u] + c < dist[v])
                        dist[v] = dist[u] + c;
                }
            }
            return dist[1];
        }

        /// What a link costs on this map; 0 with `why` set if it can't go there (off the grid, in a wall, a sealed room, cover, too near a
        /// door or another portal, the same room, or saving too little walking).
        public static int LinkPrice(World w, int links, PortalLink l, out string why)
        {
            why = null;
            if (links >= MaxLinks) { why = "The map holds " + MaxLinks + " portal pairs at most"; return 0; }
            if (l.AX % Step != 0 || l.AY % Step != 0 || l.BX % Step != 0 || l.BY % Step != 0) { why = "Not on the grid"; return 0; }
            V2 a = l.A, b = l.B;
            int aa = OpenArea(w, a, LinkEdge), ab = OpenArea(w, b, LinkEdge);
            if (aa < 0 || ab < 0) { why = "Each end must be inside a connected room, off its walls"; return 0; }
            if (aa == ab) { why = "The two ends must be in different rooms"; return 0; }
            foreach (var c in w.Cover)
                if (Layouts.Inside(c, a, 50f) || Layouts.Inside(c, b, 50f)) { why = "An obstacle is in the way"; return 0; }
            foreach (var d in w.Doors)
                if (d.Pos.DistanceTo(a) < LinkDoor || d.Pos.DistanceTo(b) < LinkDoor) { why = "Too near a door"; return 0; }
            foreach (var pt in w.Portals)
                foreach (var e in new[] { pt.A, pt.B })
                    if (e.DistanceTo(a) < LinkClear || e.DistanceTo(b) < LinkClear) { why = "Too near another portal"; return 0; }
            float walk = WalkDistance(w, a, b);
            if (walk == float.MaxValue) { why = "No way between them"; return 0; }
            if (walk < LinkMinSave) { why = "Saves too little walking (at least " + (int)(LinkMinSave / Step) + " tiles)"; return 0; }
            return Math.Min(LinkMax, LinkBase + (int)Math.Round(walk / (2f * Step)));
        }

        /// The link that saves the most walking on this map (one clear spot per room, nearest its middle), or null. Bots propose it.
        public static PortalLink? BestLink(World w, int links)
        {
            var spots = new List<V2>();
            foreach (var a in w.Areas)
            {
                if (a.Sealed || a.Hall)
                    continue;
                V2? best = null;
                for (float y = (float)Math.Ceiling((a.Min.Y + LinkEdge) / Step) * Step; y <= a.Max.Y - LinkEdge; y += Step)
                    for (float x = (float)Math.Ceiling((a.Min.X + LinkEdge) / Step) * Step; x <= a.Max.X - LinkEdge; x += Step)
                    {
                        var p = new V2(x, y);
                        bool ok = true;
                        foreach (var c in w.Cover) ok &= !Layouts.Inside(c, p, 50f);
                        foreach (var d in w.Doors) ok &= d.Pos.DistanceTo(p) >= LinkDoor;
                        if (ok && (!best.HasValue || p.DistanceTo(a.Mid) < best.Value.DistanceTo(a.Mid)))
                            best = p;
                    }
                if (best.HasValue)
                    spots.Add(best.Value);
            }
            PortalLink? top = null;
            int topPrice = 0;
            string why;
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    var l = new PortalLink { AX = (short)spots[i].X, AY = (short)spots[i].Y, BX = (short)spots[j].X, BY = (short)spots[j].Y };
                    int price = LinkPrice(w, links, l, out why);
                    if (price > topPrice) { topPrice = price; top = l; }
                }
            return top;
        }

        /// A built link as a World portal: each end faces into its room (the way you come out).
        static WorldPortal PortalOf(RoomArea[] areas, PortalLink l)
        {
            Func<V2, V2> inward = p =>
            {
                foreach (var a in areas)
                    if (a.Contains(p))
                    {
                        V2 d = a.Mid - p;
                        return d.Length > 1f ? d.Normalized() : new V2(0f, 1f);
                    }
                return new V2(0f, 1f);
            };
            return new WorldPortal { A = l.A, B = l.B, InA = inward(l.A), InB = inward(l.B) };
        }

        // --- Themed rooms (Docs/FloatingRooms.md step 4): a path as a room's theme, drawn for extra vectors, unlocked by bosses ---

        /// Extra vectors a theme costs on top of the room's walls: its path's danger (1: 3, 2: 6, 3: 10).
        public static int ThemeCost(PathId floor)
        {
            if (floor == PathId.None) return 0;
            int d = Paths.Get(floor).Danger;
            return d <= 1 ? 3 : d == 2 ? 6 : 10;
        }

        /// Bosses beaten before the theme can be drawn (danger 1: none, 2: one, 3: two).
        public static int ThemeBoss(PathId floor) => floor == PathId.None ? 0 : Math.Max(0, Paths.Get(floor).Danger - 1);

        /// Bosses beaten by this wave (a boss closes every fifth wave).
        public static int BossesBeaten(int wave) => Math.Max(0, (wave - 1) / Milestone);

        public static bool ThemeUnlocked(PathId floor, int wave) => BossesBeaten(wave) >= ThemeBoss(floor);

        /// A room the team drew with a theme: the same room, a Floor room of that path, priced with the theme's vectors on top.
        public static ExpansionRoom? Custom(IList<ExpansionRoom> built, int x, int y, int w, int h, PathId floor, out string why)
        {
            if (floor != PathId.None && Array.IndexOf(Floors, floor) < 0) { why = "Not a theme"; return null; }
            var room = Custom(built, x, y, w, h, out why);
            if (!room.HasValue || floor == PathId.None)
                return room;
            var r = room.Value;
            r.Kind = RoomKind.Floor; r.Floor = floor; r.Price += ThemeCost(floor);
            return r;
        }

        /// A room the team drew (a box in world px, any 100 px grid position): checks it and prices it. Null with `why` set if it can't go there.
        public static ExpansionRoom? Custom(IList<ExpansionRoom> built, int x, int y, int w, int h, out string why)
        {
            why = null;
            if (x % Step != 0 || y % Step != 0 || w % Step != 0 || h % Step != 0) { why = "Not on the grid"; return null; }
            if (w < MinSide || h < MinSide) { why = "Too thin (at least " + MinSide + " px)"; return null; }
            int longest = Math.Min(w, h) <= MinSide ? HallMax : MaxSide;  // (a hallway may run longer)
            if (w > longest || h > longest || w * h > MaxArea) { why = "Too big (at most " + longest / Step + " tiles a side" + (longest == MaxSide ? ", " + HallMax / Step + " for a hallway" : "") + ")"; return null; }
            int parent, loops;
            if (!Placement(built, x, y, w, h, out parent, out loops, out why))
                return null;
            return new ExpansionRoom { Parent = parent, X = x, Y = y, W = w, H = h, Kind = RoomKind.Combat, Floor = PathId.None, Price = Cost(built, x, y, w, h) };
        }

        /// Does this box meet the leg before it end to end: exactly MinSide of shared wall, at one end of that leg (a corner or straight on, not a T)?
        static bool EndToEnd(ExpansionRoom prev, int x, int y, int w, int h)
        {
            if (SharedRun(x, y, x + w, y + h, prev.X, prev.Y, prev.X1, prev.Y1) != MinSide)
                return false;
            bool vertical = x + w == prev.X || x == prev.X1;  // the shared wall runs up and down
            int lo = vertical ? Math.Max(y, prev.Y) : Math.Max(x, prev.X), hi = vertical ? Math.Min(y + h, prev.Y1) : Math.Min(x + w, prev.X1);
            return vertical ? lo == prev.Y || hi == prev.Y1 : lo == prev.X || hi == prev.X1;
        }

        /// A bent hallway (up to MaxLegs boxes, each {x, y, w, h}, MinSide wide): the first leg must fit like any drawn room, each next
        /// one meets the leg before it end to end (exactly MinSide of shared wall) and opens full width onto it. The legs come back in
        /// order, built together; the first carries the whole price (each leg's new wall, halved like any hallway). Null with `why` set if it can't go.
        public static List<ExpansionRoom> Hall(IList<ExpansionRoom> built, IList<int[]> legs, out string why)
        {
            why = null;
            if (legs == null || legs.Count == 0) { why = "Draw a leg"; return null; }
            if (legs.Count > MaxLegs) { why = "At most " + MaxLegs + " legs"; return null; }
            var all = new List<ExpansionRoom>(built);
            var made = new List<ExpansionRoom>();
            int total = 0;
            for (int i = 0; i < legs.Count; i++)
            {
                int x = legs[i][0], y = legs[i][1], w = legs[i][2], h = legs[i][3];
                if (Math.Min(w, h) != MinSide) { why = "A hallway leg is " + MinSide / Step + " tiles wide"; return null; }
                var leg = Custom(all, x, y, w, h, out why);
                if (!leg.HasValue)
                    return null;
                var r = leg.Value;
                if (i > 0)
                {
                    var prev = all[all.Count - 1];
                    if (!EndToEnd(prev, x, y, w, h)) { why = "Each leg starts at the end of the last one"; return null; }
                    r.Leg = (byte)i;
                    r.Parent = all.Count;  // (the leg before it: area index = list index + 1)
                }
                total += r.Price;
                r.Price = 0;
                all.Add(r);
                made.Add(r);
            }
            var head = made[0];
            head.Price = total;
            made[0] = head;
            return made;
        }

        /// Doors an area has (to its parent, children and loops).
        static int Doors(IList<ExpansionRoom> rooms, int area)
        {
            int n = 0, ax0, ay0, ax1, ay1;
            BoxOf(rooms, area, out ax0, out ay0, out ax1, out ay1);
            for (int b = 0; b <= rooms.Count; b++)
            {
                if (b == area) continue;
                int bx0, by0, bx1, by1;
                BoxOf(rooms, b, out bx0, out by0, out bx1, out by1);
                if (SharedRun(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1) >= MinDoorRun)
                    n++;
            }
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
                int placedParent, loops; string why;
                if (!Placement(built, x, y, w, h, out placedParent, out loops, out why))
                    continue;
                parent = placedParent;
                if (loops == 0 && rng.Float() < 0.4f)
                    continue;  // a room that closes a loop is likelier to be offered: dead ends get long
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
                var offer = new ExpansionRoom { Parent = parent, X = x, Y = y, W = w, H = h, Kind = kind, Floor = floor, Price = Cost(built, x, y, w, h) };
                bool dup = false;
                foreach (var o in offers)
                    dup |= (o.W == w && o.H == h && o.Kind == kind) || (o.Parent == parent && Math.Abs(o.X - x) + Math.Abs(o.Y - y) < 400);
                if (!dup)
                    offers.Add(offer);
            }
            return offers;
        }

        // --- The world ---------------------------------------------------------------------------------------------

        /// Obstacles in a built room: pillars, bars of wall, an L of wall and blocks, laid out from the room's own box (so every
        /// machine builds the same ones, and a saved map needs nothing more). They keep CoverWall px off the room's walls, CoverDoor off
        /// every door, CoverMid off the middle and CoverGap off each other, so every door and every wall can still be walked along;
        /// a hallway (a side of MinSide or less) stays clear, and a bigger room gets more (one per ~220000 px2, up to 4).
        /// A room too small or too doored for that (a 400-800 px room with doors at both ends had no legal spot) gets fallback
        /// passes: first the middle kept clear by only CoverMidTight, then a lone pillar or block CoverTight off the walls, doors
        /// (plus CoverDoorTight) and the middle point.
        public const int CoverWall = 130, CoverDoor = 230, CoverMid = 130, CoverGap = 70;
        public const int CoverMidTight = 60, CoverTight = 100, CoverDoorTight = 180;
        /// Last pass, for a room only 400 px deep between two doors: one small pillar to a side, its centre CoverDoorMin from every door.
        public const int CoverDoorMin = 200;

        static void AddCover(List<Solid> to, ExpansionRoom r, List<V2> doors)
        {
            if (Math.Min(r.W, r.H) <= MinSide || r.Floating)
                return;  // (hallways and the floating rooms stay clear)
            var rng = new DetRandom(r.X * 73856093 ^ r.Y * 19349663 ^ r.W * 83492791 ^ r.H * 2654435);
            int want = Math.Max(1, Math.Min(4, r.W * r.H / 220000));
            var placed = new List<int[]>();  // each obstacle's bounding box: x0, y0, x1, y1
            V2 mid = new V2(r.X + r.W / 2f, r.Y + r.H / 2f);
            for (int n = 0, tries = 0; n < want && tries < 400; tries++)
            {
                int stage = tries < 60 ? 0 : tries < 120 ? 1 : tries < 200 ? 2 : 3;  // 0 the full rules, 1 a smaller clear middle, 2 closer to doors too, 3 a lone pillar by distance
                if (stage == 3 && n > 0)
                    break;  // (the last pass only makes sure a room has one)
                int kind = rng.Int(4);  // 0 pillar, 1 bar, 2 an L, 3 block
                if (stage >= 2)
                    kind = kind < 2 || stage == 3 ? 0 : 3;  // (only a pillar or block that tight)
                bool horizontal = rng.Int(2) == 0;
                int hw, hh;
                switch (kind)
                {
                    case 0: hw = hh = stage == 3 ? 30 : 30 + rng.Int(3) * 10; break;
                    case 1: hw = horizontal ? 70 + rng.Int(4) * 25 : 20; hh = horizontal ? 20 : 70 + rng.Int(4) * 25; break;
                    case 2: hw = hh = 90; break;
                    default: hw = hh = 45; break;
                }
                int wall = stage >= 2 ? CoverTight : CoverWall;
                int ix0 = r.X + wall + hw, ix1 = r.X + r.W - wall - hw, iy0 = r.Y + wall + hh, iy1 = r.Y + r.H - wall - hh;
                if (ix1 <= ix0 || iy1 <= iy0)
                    continue;
                int cx = ix0 + rng.Int((ix1 - ix0) / 10 + 1) * 10, cy = iy0 + rng.Int((iy1 - iy0) / 10 + 1) * 10;
                int bx0 = cx - hw, by0 = cy - hh, bx1 = cx + hw, by1 = cy + hh;
                int keepMid = stage == 0 ? CoverMid : stage == 1 ? CoverMidTight : CoverTight / 3, keepDoor = stage == 2 ? CoverDoorTight : CoverDoor;
                bool ok = Math.Abs(cx - mid.X) > hw + keepMid || Math.Abs(cy - mid.Y) > hh + keepMid;
                foreach (var d in doors)
                    ok &= stage == 3 ? new V2(cx, cy).DistanceTo(d) >= CoverDoorMin : Math.Abs(cx - d.X) > hw + keepDoor || Math.Abs(cy - d.Y) > hh + keepDoor;
                foreach (var o in placed)
                    ok &= bx0 > o[2] + CoverGap || bx1 < o[0] - CoverGap || by0 > o[3] + CoverGap || by1 < o[1] - CoverGap;
                if (!ok)
                    continue;
                placed.Add(new[] { bx0, by0, bx1, by1 });
                n++;
                switch (kind)
                {
                    case 0: to.Add(World.Pillar(cx, cy, hw)); break;
                    case 2:  // an L: one bar along the top and one down the left side
                        to.Add(World.Box(bx0, by0, bx1, by0 + 36));
                        to.Add(World.Box(bx0, by0, bx0 + 36, by1));
                        break;
                    default: to.Add(World.Box(bx0, by0, bx1, by1)); break;
                }
            }
        }

        /// The World for the hub plus these rooms. With no rooms it is the hub alone, walled in (the Single world has no walls).
        /// Rooms sharing a stretch of wall of at least MinDoorRun have a door there (the parent's, and loops).
        public static World Build(IList<ExpansionRoom> rooms, IList<WallCut> cuts = null, IList<PortalLink> links = null)
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
                    Name = a == 0 ? "Hub" : rooms[a - 1].Floating ? (kind == RoomKind.Shop ? "Shop" : kind == RoomKind.Vendor ? "Vendor" : "Gatekeeper") : rooms[a - 1].Shape + (kind == RoomKind.Shop ? " (shop)" : kind == RoomKind.Floor ? " (" + rooms[a - 1].Floor + ")" : ""),
                    Min = new V2(x0, y0), Max = new V2(x1, y1), Hub = a == 0, Hall = a > 0 && Math.Min(x1 - x0, y1 - y0) <= 300,
                    Kind = kind, Floor = a == 0 ? PathId.None : rooms[a - 1].Floor, Owner = a == 0 ? 0 : rooms[a - 1].Owner, Floating = a > 0 && rooms[a - 1].Floating,
                };
                // Doors to every earlier room it shares enough wall with (its parent is always one of them).
                for (int b = 0; b < a; b++)
                {
                    int bx0, by0, bx1, by1;
                    BoxOf(rooms, b, out bx0, out by0, out bx1, out by1);
                    if (SharedRun(x0, y0, x1, y1, bx0, by0, bx1, by1) < MinDoorRun)
                        continue;
                    int ox0 = Math.Max(x0, bx0), ox1 = Math.Min(x1, bx1), oy0 = Math.Max(y0, by0), oy1 = Math.Min(y1, by1);
                    float open = b == a - 1 && rooms[a - 1].Leg > 0 ? Math.Max(ox1 - ox0, oy1 - oy0) : 0f;  // a bent hallway's corner: open full width
                    if (x0 == bx1) doors.Add(new RoomDoor { Pos = new V2(x0, (oy0 + oy1) / 2f), Vertical = true, Width = open, A = b, B = a });
                    else if (x1 == bx0) doors.Add(new RoomDoor { Pos = new V2(x1, (oy0 + oy1) / 2f), Vertical = true, Width = open, A = b, B = a });
                    else if (y0 == by1) doors.Add(new RoomDoor { Pos = new V2((ox0 + ox1) / 2f, y0), Width = open, A = b, B = a });
                    else doors.Add(new RoomDoor { Pos = new V2((ox0 + ox1) / 2f, y1), Width = open, A = b, B = a });
                }
                // The four walls. Where an earlier room already has the wall (they share it), this room leaves that stretch out.
                for (int edge = 0; edge < 4; edge++)  // 0 west, 1 east, 2 north, 3 south
                {
                    bool vertical = edge < 2;
                    int at = edge == 0 ? x0 : edge == 1 ? x1 : edge == 2 ? y0 : y1;
                    int from = vertical ? y0 : x0, till = vertical ? y1 : x1;
                    var cut = new List<int[]>();
                    for (int b = 0; b < a; b++)
                    {
                        int bx0, by0, bx1, by1;
                        BoxOf(rooms, b, out bx0, out by0, out bx1, out by1);
                        int bat = edge == 0 ? bx1 : edge == 1 ? bx0 : edge == 2 ? by1 : by0;  // their wall on the same line, facing us
                        if (bat != at)
                            continue;
                        int lo = Math.Max(from, vertical ? by0 : bx0), hi = Math.Min(till, vertical ? by1 : bx1);
                        if (hi > lo)
                            cut.Add(new[] { lo, hi });
                    }
                    cut.Sort((p, q) => p[0].CompareTo(q[0]));
                    int pos = from;
                    foreach (var c in cut)
                    {
                        if (c[0] > pos) spans.Add(new WallSpan(vertical, at, pos, c[0]));
                        pos = Math.Max(pos, c[1]);
                    }
                    if (till > pos) spans.Add(new WallSpan(vertical, at, pos, till));
                }
            }
            if (cuts != null)  // opened walls: a wide gap between the two rooms, replacing any door it covers
                foreach (var c in cuts)
                    for (int a = 0; a < n; a++)
                        for (int b = a + 1; b < n; b++)
                        {
                            BoxOf(rooms, a, out int ax0, out int ay0, out int ax1, out int ay1);
                            BoxOf(rooms, b, out int bx0, out int by0, out int bx1, out int by1);
                            bool match = c.Vertical
                                ? (ax1 == c.At && bx0 == c.At || bx1 == c.At && ax0 == c.At) && c.From >= Math.Max(ay0, by0) && c.Till <= Math.Min(ay1, by1)
                                : (ay1 == c.At && by0 == c.At || by1 == c.At && ay0 == c.At) && c.From >= Math.Max(ax0, bx0) && c.Till <= Math.Min(ax1, bx1);
                            if (!match)
                                continue;
                            doors.RemoveAll(d => d.A == a && d.B == b && d.Vertical == c.Vertical && (c.Vertical ? d.Pos.Y : d.Pos.X) > c.From && (c.Vertical ? d.Pos.Y : d.Pos.X) < c.Till);
                            doors.Add(new RoomDoor { Pos = c.Vertical ? new V2(c.At, (c.From + c.Till) / 2f) : new V2((c.From + c.Till) / 2f, c.At), Vertical = c.Vertical, Width = c.Till - c.From, A = a, B = b });
                        }
            for (int a = 1; a < n; a++)  // a floating room with no door is sealed
                if (areas[a].Floating)
                {
                    bool open = false;
                    foreach (var d in doors)
                        open |= d.Joins(a);
                    areas[a].Sealed = !open;
                }
            var cover = new List<Solid>();
            for (int a = 1; a < n; a++)
            {
                var doorSpots = new List<V2>();
                foreach (var d in doors)
                    if (d.Joins(a))
                        doorSpots.Add(d.Pos);
                AddCover(cover, rooms[a - 1], doorSpots);
            }
            var portals = new WorldPortal[links == null ? 0 : links.Count];  // bought portal pairs (Docs/RoomBuilding2.md step 2)
            for (int i = 0; i < portals.Length; i++)
                portals[i] = PortalOf(areas, links[i]);
            return new World
            {
                Name = "Expanding",
                Areas = areas, Doors = doors.ToArray(), Portals = portals, Spans = spans.ToArray(), Cover = cover.ToArray(),
                Min = new V2(minX, minY), VaultMin = new V2(minX, minY), Max = new V2(maxX, maxY),
                Hub = 0, Vault = -1,
            };
        }
    }
}
