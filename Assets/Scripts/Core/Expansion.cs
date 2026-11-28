using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a room of the Expanding arena does (Docs/ExpandingArena.md). rule rooms carry a path's ground and hazards.
    public enum RoomKind : byte { Combat, Shop, Floor, BlackMarket, Gatekeeper, Opening, Link, Gate, Builder, Arms, Medic, Hospital }  // (Hospital: a Field Hospital's cell, there for a minute, FieldHospital.cs; Medic: the Medic, heals and revive tokens, Medic.cs; Builder: a Builder Shop card's room, BuilderShop.cs; Arms: the Arms Dealer, Docs/ArmsDealer.md; Vendor and Gatekeeper: the floating rooms, Docs/FloatingRooms.md; Opening, Link and Gate: only in a round's proposals, a stretch of wall to open, a portal pair or a shutter gate on a door)

    /// A stretch of interior wall the team opened (the Expanding arena): a vertical wall at x = At from y = From to Till, or a horizontal one at y = At.
    public struct WallCut : IEquatable<WallCut>
    {
        public bool Vertical;
        public short At, From, Till;
        public bool Equals(WallCut o) => Vertical == o.Vertical && At == o.At && From == o.From && Till == o.Till;
        public int Length => Till - From;
    }

    /// A portal pair the team bought (the Expanding arena, Docs/RoomBuilding2.md step 2): two spots on the 100 px grid, each on a
    /// room's outer wall (the wall round them becomes the portal). Older saves' pairs sit inside rooms: floor portals.
    public struct PortalLink : IEquatable<PortalLink>
    {
        public short AX, AY, BX, BY;
        public V2 A => new V2(AX, AY);
        public V2 B => new V2(BX, BY);
        public bool Equals(PortalLink o) => AX == o.AX && AY == o.AY && BX == o.BX && BY == o.BY;
    }

    /// What a map node is (Docs/Nodes.md). A node is a point in empty space; a built room that encloses it takes on its job.
    public enum NodeKind : byte { Chest, Relic, BreachLoot, Shrine }  // (a shrine node: a health shrine, one use per player, Shrines.cs; the only node placed since Nov 20; a chest node holds a treasure chest, Props.cs SpawnChest, Docs/Chests.md; a chest a room rolled keeps its kind in Rule (ChestKind + 1); a relic fills the relic slot of the player who walks up to it and takes their old relic's place, Relics.cs; a scout tower and a gold vein: NodeScouting.cs)

    /// A point on the Expanding map, at the centre of a 100 px tile: locked until a built room encloses it, then a fixture of that room.
    public struct MapNode : IEquatable<MapNode>
    {
        public short X, Y;
        public NodeKind Kind;
        public bool Spent;   // a chest that has been opened
        public byte Rule;    // a Breach loot node: its kind (Engineer.cs)
        public bool Hidden;  // fogged: its kind shows as "?" (off since Nov 18; NodeScouting.cs clears an old save's)
        public byte Drop;    // a relic node: the relic a player left in it, as RelicId + 1 (0: it still holds the relic rolled from its spot; Relics.cs)
        public bool Equals(MapNode o) => X == o.X && Y == o.Y && Kind == o.Kind && Spent == o.Spent && Rule == o.Rule && Hidden == o.Hidden && Drop == o.Drop;
        public V2 Pos => new V2(X, Y);
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
        public bool Floating;   // placed by the game off the map (a shop, Black Market or Gatekeeper), not drawn: sealed until a built room touches it
        public byte Surprise;   // a surprise room (Docs/SurpriseRooms.md): 1 + its SurpriseShape, on each of its boxes; 0: none
        public bool Claimed;    // a breach Stash's vector cache has been taken (Docs/BreachRooms.md)
        public bool Unseen;     // room fog (RoomFog.cs): nobody has walked in yet, so it is dark on the maps and what's inside is hidden
        public byte Mini;       // a mini-boss room's foe (an EnemyType, FactionMinis.cs): its boss cycle's faction's; 0: unset (an old save: the current one's)
        public bool OneWay;     // built from a One-way piece card (PieceMods.cs): all its doors but one let you through one way only (OneWayDoors.cs)
        public byte Piece;        // a drawn outline's piece (Expansion.PieceBase + n; an old save's 1 or 2: a retired bent hallway's leg): it opens full width onto its Parent (Expansion.Outline)
        public int X1 => X + W;
        public int Y1 => Y + H;
        public string Shape => Expansion.ShapeName(W, H);
    }

    /// A room modifier the team holds (Docs/RoomModifiers.md): spent once on a Combat room to make it a Shop or a rule room.
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
    public static partial class Expansion
    {
        public const int MaxTokens = 6;          // room kits the team can hold
        public const int MaxRooms = 24;          // bought rooms (the hub is extra)
        public const int MaxWidth = 8000, MaxHeight = 5000;  // the whole map's bounding box (owner Nov 13: was 6400 x 4000, raised so far-out rooms and nodes still fit)
        public const int Step = 100;             // rooms slide along a wall in steps of this
        public const int MinDoorRun = 200;       // a room must share at least this much wall with its parent (a door, plus a margin): 2 tiles, so a 2-wide room can join end on
        public const int Milestone = 5;          // an offer every this many waves
        public const int MinSide = 200;          // a drawn room's shortest side (any shape between these: no hallways, just rooms)
        public const int MaxArea = 1440000;  // a room's floor at most: 144 tiles

        public static readonly int[][] Shapes =
        {
            new[] { 600, 400 }, new[] { 800, 600 }, new[] { 1000, 700 }, new[] { 900, 900 },
            new[] { 1200, 300 }, new[] { 300, 900 },
        };
        static readonly string[] shapeNames = { "Closet", "Room", "Chamber", "Plaza", "Gallery", "Annex" };

        /// The floors a rule room can carry (paths whose ground and hazards read well as a room).
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

        /// The map has grown past the plain hub: a room was built (the floating rooms the game places don't count).
        public static bool Grown(IList<ExpansionRoom> rooms)
        {
            foreach (var r in rooms)
                if (!r.Floating)
                    return true;
            return false;
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
            return "Room";  // (a drawn one)
        }

        /// Is this entry a later part of a multi-box room (an outline's piece), joined open onto its Parent?
        public static bool Joined(IList<ExpansionRoom> rooms, int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a > rooms.Count || b > rooms.Count)
                return false;
            return a > 0 && rooms[a - 1].Piece > 0 && rooms[a - 1].Parent == b || b > 0 && rooms[b - 1].Piece > 0 && rooms[b - 1].Parent == a;
        }

        /// The first entry (list index) of the multi-box room this entry belongs to (itself for a plain box).
        public static int HeadOf(IList<ExpansionRoom> rooms, int index)
        {
            for (int guard = 0; guard < 64 && index >= 0 && index < rooms.Count && rooms[index].Piece > 0; guard++)
                index = rooms[index].Parent - 1;
            return index;
        }

        /// Every entry (list index) of the multi-box room this entry belongs to: its head and all its legs or pieces.
        public static List<int> GroupOf(IList<ExpansionRoom> rooms, int index)
        {
            int head = HeadOf(rooms, index);
            var all = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
                if (HeadOf(rooms, i) == head)
                    all.Add(i);
            return all;
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

        /// Rooms the team has drawn (floating rooms, placed by the game, don't count towards the cap).
        public static int BoughtCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            foreach (var r in rooms)
                if (!r.Floating && r.Piece == 0 && !IsGridCore(r) && !IsHospital(r))  // (a drawn outline is one room, however many boxes; a Grid run's core is free; a Field Hospital isn't built)
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
                    if (SharedRun(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1) >= MinDoorRun && !ShopWallBetween(rooms, a, b) || Joined(rooms, a, b))
                    {
                        seen[b] = true;
                        queue.Enqueue(b);
                    }
                }
            }
            return seen;
        }

        // --- Map nodes (Docs/Nodes.md): shop and Black Market points out in empty space, claimed by building a room around them ---

        public const int NodeClear = 90;                  // cover keeps this far off a node
        public const int NodeApart = 800;                 // and at least this far from every other node
        public const int MaxNodesWaiting = 8;             // unclaimed nodes on the map at once

        /// A chest node's kind (Props.cs ChestKind), by its tier (NodeTier): inner Gold or Vector (a coin seeded from the spot), middle Piece chest or Buildables
        /// (Buildables with room cards off), outer Upgrade.
        public static ChestKind NodeChestKind(int seed, MapNode n, bool cards)
            => n.Kind == NodeKind.Chest && n.Rule > 0 && n.Rule <= RoomChestWeights.Length ? (ChestKind)(n.Rule - 1) : NodeChestKind(seed, n.X, n.Y, cards);

        public static ChestKind NodeChestKind(int seed, int x, int y, bool cards)
        {
            int tier = NodeTier(x, y);
            if (tier != 1)
                return tier == 0 ? (InnerCoin(seed, x, y) ? ChestKind.Gold : ChestKind.Vector) : ChestKind.Upgrade;
            if (new DetRandom(seed * 31 + x * 7919 + y * 104729 + 9).Int(4) == 0)
                return ChestKind.Gadget;  // (1 in 4 middle-tier chests; its own roll, so the rest of the map stays as it was)
            var r = new DetRandom(seed * 31 + x * 7919 + y * 104729 + 5);
            return cards && r.Int(2) == 0 ? ChestKind.Piece : ChestKind.Buildable;
        }

        // --- Node tiers (owner Nov 18): the farther a prize node sits from the hub, the better what it holds ---

        public const int TierMid = 1500, TierOuter = 3000;  // a node this far (15 / 30 tiles) or more off the hub's wall is middle / outer tier

        /// A point's tier from its distance to the hub's box (square zones: the farther axis): 0 inner (gold and vector chests), 1 middle (gadget, piece and buildables chests),
        /// 2 outer (upgrade chests and relics). A pure function of the point, so it needs nothing on the wire or in saves.
        public static int NodeTier(int x, int y)
        {
            float dx = Math.Max(0f, Math.Max(-x, x - Game.Width)), dy = Math.Max(0f, Math.Max(-y, y - Game.Height));
            float d = Math.Max(dx, dy);  // square zones (owner Nov 19): the farther of the two axes
            return d >= TierOuter ? 2 : d >= TierMid ? 1 : 0;
        }

        public static int NodeTier(MapNode n) => NodeTier(n.X, n.Y);

        /// The tier the first cycle's ring node `i` is placed at: inner, middle, outer, twice round.
        public static int RingTier(int i) => i % 3;

        /// The tier a later cycle's node is placed at: a relic outer (it only holds one there), a chest by turns middle, outer, inner; a vein anywhere.
        public static int CycleTier(NodeKind kind, int cycle) => kind == NodeKind.Relic ? 2 : IsChest(kind) ? (cycle + 1) % 3 : 0;

        /// A tiered prize node (a chest or relic) takes the kind its tier holds: inner a chest (Gold or Vector),
        /// middle a chest (piece or buildables), outer an upgrade chest or a relic; seeded from the spot. Other nodes are unchanged.
        public static MapNode Tiered(int seed, MapNode n)
        {
            if (!IsChest(n.Kind) && n.Kind != NodeKind.Relic)
                return n;
            int tier = NodeTier(n);
            bool coin = InnerCoin(seed, n.X, n.Y);
            n.Kind = tier != 2 ? NodeKind.Chest : (coin ? NodeKind.Chest : NodeKind.Relic);
            return n;
        }

        /// The coin a spot is seeded with: inner tier Gold (heads) or Vector chest, outer tier chest or relic.
        static bool InnerCoin(int seed, int x, int y) => new DetRandom(seed * 53 + x * 6007 + y * 7151 + 11).Int(2) == 0;

        public static bool IsChest(NodeKind k) => k == NodeKind.Chest;

        public static bool NodeInside(MapNode n, int x0, int y0, int x1, int y1)
        {
            return n.X > x0 && n.X < x1 && n.Y > y0 && n.Y < y1;
        }

        /// Can this built room claim a node round it (any built room that encloses it)?
        public static bool NodeEncloses(ExpansionRoom r, MapNode n)
            => !r.Floating && NodeInside(n, r.X, r.Y, r.X1, r.Y1);

        /// The whole room this entry belongs to (all of a drawn outline's boxes, whose inner seams are open) encloses this node.
        public static bool NodeEnclosedBy(IList<ExpansionRoom> rooms, int index, MapNode n)
        {
            var r = rooms[index];
            if (r.Floating)
                return false;
            return NodeEncloses(r, n);
        }

        /// The nodes whose fixture stands in the middle of the room that claims them (owner Nov 18): chests, vector chests and relics.
        public static bool CentresInRoom(NodeKind k) => k == NodeKind.Chest || k == NodeKind.Relic || k == NodeKind.Shrine;

        /// Where a chest or relic node stands: in a grid room, the middle of the cell holding it; else the middle of the room that encloses it (a multi-box room: the middle of its whole outline if that
        /// is floor, else of the box holding the node; the random cover round it is cleared). A node nobody has claimed yet, or a room with no clear spot, keeps the node's own point.
        public const int HallMaxWidth = 300;  // a room this narrow or less is a hallway

        public static V2 NodeSpotIn(IList<ExpansionRoom> rooms, MapNode nd, IList<Solid> cover)
        {
            int at = -1;
            for (int i = 0; i < rooms.Count && at < 0; i++)
                if (NodeEnclosedBy(rooms, i, nd))
                    at = i;
            if (at < 0)
                return nd.Pos;
            var group = GroupOf(rooms, at);
            if (group.Count == 1 && Math.Min(rooms[at].W, rooms[at].H) <= HallMaxWidth)
                return nd.Pos;  // a hallway keeps its chest where it was put (a mystery door's chest sits at the far end)
            if (OnLattice(rooms, group))  // a grid room (owner Nov 19): the node stands in the middle of the cell it was built in, not the room's
            {
                double cx = GridX0 + (Math.Floor((nd.Pos.X - GridX0) / (double)GridCell) + 0.5) * GridCell;
                double cy = GridY0 + (Math.Floor((nd.Pos.Y - GridY0) / (double)GridCell) + 0.5) * GridCell;
                return new V2((float)cx, (float)cy);
            }
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue, held = at;
            foreach (int i in group)
            {
                var b = rooms[i];
                x0 = Math.Min(x0, b.X); y0 = Math.Min(y0, b.Y); x1 = Math.Max(x1, b.X1); y1 = Math.Max(y1, b.Y1);
                if (NodeEncloses(b, nd))
                    held = i;
            }
            double tx = (x0 + x1) / 2.0, ty = (y0 + y1) / 2.0;
            bool onFloor = false;
            foreach (int i in group)
                onFloor |= tx > rooms[i].X && tx < rooms[i].X1 && ty > rooms[i].Y && ty < rooms[i].Y1;
            if (!onFloor)
            {
                var h = rooms[held];
                tx = (h.X + h.X1) / 2.0; ty = (h.Y + h.Y1) / 2.0;
            }
            return new V2((float)tx, (float)ty);  // (Build clears the random cover round it; walls keep clear of a room's middle)
        }

        /// Every box of this room sits on the grid lattice (GridX0/GridY0, GridCell apart).
        static bool OnLattice(IList<ExpansionRoom> rooms, List<int> group)
        {
            foreach (int i in group)
            {
                var b = rooms[i];
                if (GridMod(b.X, GridX0) != 0 || GridMod(b.Y, GridY0) != 0 || b.W % GridCell != 0 || b.H % GridCell != 0)
                    return false;
            }
            return true;
        }

        /// A built room (a drawn outline as a whole) encloses this node.
        public static bool NodeClaimed(IList<ExpansionRoom> built, MapNode n)
        {
            for (int i = 0; i < built.Count; i++)
                if (NodeEnclosedBy(built, i, n))
                    return true;
            return false;
        }

        public static int UnclaimedCount(IList<ExpansionRoom> built, IList<MapNode> nodes)
        {
            int n = 0;
            foreach (var nd in nodes)
                if (!NodeClaimed(built, nd))
                    n++;
            return n;
        }

        public const int ShopRoomSide = 400;  // a floating shop or Black Market room is this square (4 x 4 tiles, owner Nov 11)  // floating rooms waiting to be connected at once

        // --- Opened walls: a stretch of wall between two rooms the team opens for vectors (1 per tile) ---

        // --- Portal links (Docs/RoomBuilding2.md step 2): a portal pair between two rooms, bought with vectors ---

        public const int MaxLinks = 3;            // portal pairs a map can hold (not counted as rooms)
        public const float LinkMinSave = 600f;    // it must save at least 6 tiles of walking
        public const float LinkEdge = 90f, LinkClear = 250f, LinkDoor = 150f;  // kept off walls, other portals and doors
        public const float LinkHalf = 100f;       // a wall portal turns 200 px of wall into the portal (100 either side of its spot)

        public static ExpansionRoom LinkProposal(PortalLink l) => new ExpansionRoom
        {
            Kind = RoomKind.Link, X = l.AX, Y = l.AY, W = l.BX - l.AX, H = l.BY - l.AY,
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
            foreach (var pt in w.Portals) { pos.Add(pt.MouthA); areas.Add(new[] { w.AreaAt(pt.MouthA) }); pos.Add(pt.MouthB); areas.Add(new[] { w.AreaAt(pt.MouthB) }); }
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

        /// Which way is into the room from a wall spot (null: p isn't on the 100 px grid on a room's outer wall). The outer wall:
        /// a connected room on one side, nothing on the other, LinkHalf either side of p (so no corner, door or seam is in it).
        public static V2? OuterWall(World w, V2 p)
        {
            foreach (var inn in Sides)
            {
                V2 along = inn.Orthogonal();
                bool ok = true;
                for (int k = -1; k <= 1 && ok; k++)
                {
                    V2 q = p + along * (k * (LinkHalf - 10f));
                    if (OpenArea(w, q + inn * 30f, 0f) < 0)
                        ok = false;
                    foreach (var a in w.Areas)
                        if (a.Contains(q - inn * 30f))
                            ok = false;
                }
                if (ok)
                    return inn;
            }
            return null;
        }

        static readonly V2[] Sides = { new V2(0f, 1f), new V2(0f, -1f), new V2(1f, 0f), new V2(-1f, 0f) };

        /// The outer-wall grid spot nearest p within reach (null: none), so the teleporter tool only ever lands on a wall.
        public static V2? NearestWallSpot(World w, V2 p, float reach)
        {
            V2? best = null;
            int r = (int)Math.Ceiling(reach / Step);
            float cx = (float)Math.Round(p.X / Step) * Step, cy = (float)Math.Round(p.Y / Step) * Step;
            for (int i = -r; i <= r; i++)
                for (int j = -r; j <= r; j++)
                {
                    V2 q = new V2(cx + i * Step, cy + j * Step);
                    if (q.FartherThan(p, reach) || (best.HasValue && !q.CloserThan(p, best.Value.DistanceTo(p))))
                        continue;
                    if (OuterWall(w, q).HasValue)
                        best = q;
                }
            return best;
        }

        /// Whether one end of a portal pair can go on the wall at p; `inn` is the way into its room, `why` what is wrong.
        static bool WallSpot(World w, V2 p, out V2 inn, out string why)
        {
            why = null;
            inn = V2.Zero;
            if ((int)p.X % Step != 0 || (int)p.Y % Step != 0) { why = "Not on the grid"; return false; }
            var side = OuterWall(w, p);
            if (!side.HasValue) { why = "Each end goes on a room's outer wall, with 2 tiles of plain wall"; return false; }
            inn = side.Value;
            V2 mouth = p + inn * WorldPortal.MouthIn, along = inn.Orthogonal();
            foreach (var c in w.Cover)
                if (Layouts.Inside(c, mouth, 50f)) { why = "An obstacle is in the way"; return false; }
            foreach (var c in w.Walls())  // (a room's side walls, standing out from the portal's wall)
                for (int k = -1; k <= 1; k++)
                    if (Layouts.Inside(c, p + inn * 36f + along * (k * (LinkHalf - 20f)), 4f)) { why = "A wall is in the way"; return false; }
            foreach (var d in w.Doors)
                if (d.Pos.CloserThan(p, LinkDoor)) { why = "Too near a door"; return false; }
            return true;
        }

        /// Where a link's end starts walking from: just off its wall (a wall end), or the spot itself (an older floor end).
        public static V2 LinkEnd(World w, V2 p)
        {
            var side = OuterWall(w, p);
            return side.HasValue ? p + side.Value * WorldPortal.MouthIn : p;
        }

        /// How far a link's two ends are apart on foot today.
        public static float LinkWalk(World w, PortalLink l) => WalkDistance(w, LinkEnd(w, l.A), LinkEnd(w, l.B));

        /// Whether a link can go on this map; if not, `why` says what is wrong (off the grid, not on a room's outer wall, a sealed
        /// room, cover or a side wall in front of it, too near a door or another portal, the same room, or saving too little walking).
        public static bool LinkFits(World w, int links, PortalLink l, out string why)
        {
            why = null;
            if (links >= MaxLinks) { why = "The map holds " + MaxLinks + " portal pairs at most"; return false; }
            V2 a = l.A, b = l.B, ia, ib;
            if (!WallSpot(w, a, out ia, out why) || !WallSpot(w, b, out ib, out why))
                return false;
            V2 ma = a + ia * WorldPortal.MouthIn, mb = b + ib * WorldPortal.MouthIn;
            if (OpenArea(w, ma, 0f) == OpenArea(w, mb, 0f)) { why = "The two ends must be in different rooms"; return false; }
            foreach (var pt in w.Portals)
                foreach (var e in new[] { pt.A, pt.B })
                    if (e.CloserThan(a, LinkClear) || e.CloserThan(b, LinkClear)) { why = "Too near another portal"; return false; }
            float walk = WalkDistance(w, ma, mb);
            if (walk == float.MaxValue) { why = "No way between them"; return false; }
            if (walk < LinkMinSave) { why = "Saves too little walking (at least " + (int)(LinkMinSave / Step) + " tiles)"; return false; }
            return true;
        }

        /// The link that saves the most walking on this map (one clear outer-wall spot per room, nearest its middle), or null. Bots propose it.
        public static PortalLink? BestLink(World w, int links)
        {
            var spots = new List<V2>();
            foreach (var a in w.Areas)
            {
                if (a.Sealed || a.Floating)
                    continue;
                V2? best = null;
                var edge = new List<V2>();
                for (float x = (float)Math.Ceiling(a.Min.X / Step) * Step; x <= a.Max.X; x += Step) { edge.Add(new V2(x, a.Min.Y)); edge.Add(new V2(x, a.Max.Y)); }
                for (float y = (float)Math.Ceiling(a.Min.Y / Step) * Step; y <= a.Max.Y; y += Step) { edge.Add(new V2(a.Min.X, y)); edge.Add(new V2(a.Max.X, y)); }
                foreach (var p in edge)
                {
                    V2 inn;
                    string why;
                    if (WallSpot(w, p, out inn, out why) && a.Contains(p + inn * 30f) && (!best.HasValue || p.CloserThan(a.Mid, best.Value.DistanceTo(a.Mid))))
                        best = p;
                }
                if (best.HasValue)
                    spots.Add(best.Value);
            }
            PortalLink? top = null;
            float topWalk = 0f;
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                {
                    string why;
                    var l = new PortalLink { AX = (short)spots[i].X, AY = (short)spots[i].Y, BX = (short)spots[j].X, BY = (short)spots[j].Y };
                    float walk = LinkFits(w, links, l, out why) ? LinkWalk(w, l) : 0f;
                    if (walk > topWalk) { topWalk = walk; top = l; }
                }
            return top;
        }

        /// Whether a Black Market trap can stand at p (Market.cs): on the grid, in a connected room off its walls, clear of cover, doors,
        /// portals and other traps; `why` says what is wrong.
        public static bool PlaceSpot(World w, IList<PlacedBuild> placed, V2 p, out string why)
        {
            why = null;
            if (placed.Count >= Market.MaxPlaced) { why = "The map holds " + Market.MaxPlaced + " traps and turrets at most"; return false; }
            if ((int)p.X % Step != 0 || (int)p.Y % Step != 0) { why = "Not on the grid"; return false; }
            if (OpenArea(w, p, LinkEdge) < 0) { why = "Must be inside a connected room, off its walls"; return false; }
            foreach (var c in w.Cover)
                if (Layouts.Inside(c, p, 50f)) { why = "An obstacle is in the way"; return false; }
            foreach (var c in w.Walls())
                if (Layouts.Inside(c, p, 50f)) { why = "A wall is in the way"; return false; }
            foreach (var d in w.Doors)
                if (d.Pos.CloserThan(p, LinkDoor)) { why = "Too near a door"; return false; }
            foreach (var pt in w.Portals)
                if (pt.MouthA.CloserThan(p, Market.PlaceClear) || pt.MouthB.CloserThan(p, Market.PlaceClear)) { why = "Too near a portal"; return false; }
            foreach (var o in placed)
                if (o.Pos.CloserThan(p, Market.PlaceClear)) { why = "Too near another trap"; return false; }
            return true;
        }

        /// A built link as a World portal. An end on a room's outer wall (the teleporter tool's rule, OuterWall: so a wall across the
        /// seam of a room of several boxes counts too) makes that wall the portal, facing into the room; an end inside a room (an
        /// older save) is a floor portal facing the room's middle.
        static WorldPortal PortalOf(World w, PortalLink l)
        {
            Func<V2, V2> inward = p =>
            {
                foreach (var a in w.Areas)
                    if (a.Contains(p))
                    {
                        V2 d = a.Mid - p;
                        return d.Length > 1f ? d.Normalized() : new V2(0f, 1f);
                    }
                return new V2(0f, 1f);
            };
            V2? wa = OuterWall(w, l.A), wb = OuterWall(w, l.B);
            if (wa.HasValue && wb.HasValue)
                return new WorldPortal { A = l.A, B = l.B, InA = wa.Value, InB = wb.Value, Half = LinkHalf };
            return new WorldPortal { A = l.A, B = l.B, InA = inward(l.A), InB = inward(l.B) };
        }

        // --- Themed rooms (Docs/FloatingRooms.md step 4): a path as a room's theme, drawn for extra vectors, unlocked by bosses ---

        /// Bosses beaten before the theme can be drawn (danger 1: none, 2: one, 3: two).
        public static int ThemeBoss(PathId floor) => floor == PathId.None ? 0 : Math.Max(0, Paths.Get(floor).Danger - 1);

        /// Bosses beaten by this wave (a boss closes every fifth wave).
        public static int BossesBeaten(int wave) => Math.Max(0, (wave - 1) / Milestone);

        public static bool ThemeUnlocked(PathId floor, int wave) => BossesBeaten(wave) >= ThemeBoss(floor);

        // --- Drawn outlines -----------------------------------------------------------------------------------------

        public const byte PieceBase = 16;        // ExpansionRoom.Piece from here up: a piece of a drawn outline (old saves may hold Piece 1 or 2: a retired bent hallway's legs, built the same way)
        public const int OutlineMaxSide = 2400;  // an outline's bounding box, each way
        public const int OutlineCoverMin = 500;  // an outline's piece narrower than this gets no cover

        static bool Block(bool[,] inside, int col, int row, int size)
        {
            if (col < 0 || row < 0 || col + size > inside.GetLength(0) || row + size > inside.GetLength(1))
                return false;
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    if (!inside[col + i, row + j])
                        return false;
            return true;
        }

        /// The tiles cut into boxes: runs of each row (or column) carried on while the next row has the very same run. Each {col, row, w, h} in tiles.
        static List<int[]> Slabs(bool[,] inside, bool byRows)
        {
            int cols = inside.GetLength(0), rows = inside.GetLength(1);
            int lines = byRows ? rows : cols, len = byRows ? cols : rows;
            var done = new List<int[]>();
            var open = new List<int[]>();  // {from, till, start line}
            for (int l = 0; l <= lines; l++)
            {
                var runs = new List<int[]>();
                if (l < lines)
                    for (int u = 0; u < len; )
                    {
                        if (!(byRows ? inside[u, l] : inside[l, u])) { u++; continue; }
                        int s = u;
                        while (u < len && (byRows ? inside[u, l] : inside[l, u])) u++;
                        runs.Add(new[] { s, u });
                    }
                var next = new List<int[]>();
                foreach (var o in open)
                {
                    var same = runs.Find(r => r[0] == o[0] && r[1] == o[1]);
                    if (same != null) { next.Add(o); runs.Remove(same); continue; }
                    done.Add(byRows ? new[] { o[0], o[2], o[1] - o[0], l - o[2] } : new[] { o[2], o[0], l - o[2], o[1] - o[0] });
                }
                foreach (var r in runs)
                    next.Add(new[] { r[0], r[1], l });
                open = next;
            }
            return done;
        }

        /// The room for these tiles (inside[col, row] from (x0, y0)): checked against the outline rules, cut into boxes and priced.
        static List<ExpansionRoom> FromTiles(IList<ExpansionRoom> built, bool[,] inside, int x0, int y0, PathId floor, out string why)
        {
            why = null;
            int cols = inside.GetLength(0), rows = inside.GetLength(1), count = 0;
            if (cols * Step > OutlineMaxSide || rows * Step > OutlineMaxSide) { why = "Too big (at most " + OutlineMaxSide / Step + " tiles each way)"; return null; }
            var wide = new bool[cols, rows];  // tiles inside some MinSide x MinSide block
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    if (inside[i, j]) count++;
                    if (Block(inside, i, j, MinSide / Step))
                        for (int a = 0; a < MinSide / Step; a++)
                            for (int b = 0; b < MinSide / Step; b++)
                                wide[i + a, j + b] = true;
                }
            if (count * Step * Step > MaxArea) { why = "Too big (" + count + " tiles of floor, at most " + MaxArea / (Step * Step) + ")"; return null; }
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                    if (inside[i, j] && !wide[i, j]) { why = "Too thin in places (at least " + MinSide / Step + " tiles wide)"; return null; }
            // Boxes: rows or columns, whichever makes fewer.
            var byRows = Slabs(inside, true);
            var byCols = Slabs(inside, false);
            var boxes = byCols.Count < byRows.Count ? byCols : byRows;
            var pieces = new List<int[]>();  // world px {x, y, w, h}
            foreach (var t in boxes)
                pieces.Add(new[] { x0 + t[0] * Step, y0 + t[1] * Step, t[2] * Step, t[3] * Step });
            // Nothing in the way, and the map stays inside its size cap.
            int minX = Math.Min(0, x0), minY = Math.Min(0, y0), maxX = Math.Max((int)Game.Width, x0 + cols * Step), maxY = Math.Max((int)Game.Height, y0 + rows * Step);
            foreach (var r in built)
            {
                minX = Math.Min(minX, r.X); minY = Math.Min(minY, r.Y); maxX = Math.Max(maxX, r.X1); maxY = Math.Max(maxY, r.Y1);
            }
            if (maxX - minX > MaxWidth || maxY - minY > MaxHeight) { why = "The map would get too big"; return null; }
            var linked = ConnectedAreas(built);
            int root = -1, rootParent = -1, best = 0, shared = 0, perimeter = 0;
            for (int p = 0; p < pieces.Count; p++)
            {
                int[] q = pieces[p];
                perimeter += 2 * (q[2] + q[3]);
                for (int a = 0; a <= built.Count; a++)
                {
                    BoxOf(built, a, out int bx0, out int by0, out int bx1, out int by1);
                    if (Overlaps(q[0], q[1], q[0] + q[2], q[1] + q[3], bx0, by0, bx1, by1)) { why = "Overlaps a room"; return null; }
                    int run = SharedRun(q[0], q[1], q[0] + q[2], q[1] + q[3], bx0, by0, bx1, by1);
                    shared += run;
                    if (run >= MinDoorRun && linked[a] && run > best && !(a > 0 && IsHospital(built[a - 1])) && !OnShopPedestalWall(built, a, q[0], q[1], q[0] + q[2], q[1] + q[3])) { best = run; root = p; rootParent = a; }  // (never off a Field Hospital: it comes off the map)
                }
                for (int o = 0; o < pieces.Count; o++)
                    if (o != p)
                        shared += SharedRun(q[0], q[1], q[0] + q[2], q[1] + q[3], pieces[o][0], pieces[o][1], pieces[o][0] + pieces[o][2], pieces[o][1] + pieces[o][3]);
            }
            if (root < 0) { why = "Must share at least " + MinDoorRun / Step + " tiles of one wall with the connected map"; return null; }
            // Order the pieces out from the one on the map, each joined to the piece it was reached from.
            var order = new List<int> { root };
            var from = new Dictionary<int, int> { { root, -1 } };
            for (int k = 0; k < order.Count; k++)
            {
                int[] q = pieces[order[k]];
                for (int o = 0; o < pieces.Count; o++)
                {
                    if (from.ContainsKey(o))
                        continue;
                    int[] r = pieces[o];
                    if (SharedRun(q[0], q[1], q[0] + q[2], q[1] + q[3], r[0], r[1], r[0] + r[2], r[1] + r[3]) > 0)
                    {
                        from[o] = order[k];
                        order.Add(o);
                    }
                }
            }
            if (order.Count != pieces.Count) { why = "The shape must be in one piece"; return null; }
            var made = new List<ExpansionRoom>();
            for (int k = 0; k < order.Count; k++)
            {
                int[] q = pieces[order[k]];
                made.Add(new ExpansionRoom
                {
                    Parent = k == 0 ? rootParent : built.Count + 1 + order.IndexOf(from[order[k]]),
                    X = q[0], Y = q[1], W = q[2], H = q[3],
                    Kind = floor == PathId.None ? RoomKind.Combat : RoomKind.Floor, Floor = floor,
                    Price = 0,
                    Piece = k == 0 ? (byte)0 : (byte)Math.Min(255, PieceBase + k),
                });
            }
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
                if (SharedRun(ax0, ay0, ax1, ay1, bx0, by0, bx1, by1) >= MinDoorRun || Joined(rooms, area, b))
                    n++;
            }
            return n;
        }

        // --- Offers ------------------------------------------------------------------------------------------------

        // --- The world ---------------------------------------------------------------------------------------------

        /// Obstacles in a built room: pillars, bars of wall, an L of wall and blocks, laid out from the room's own box (so every
        /// machine builds the same ones, and a saved map needs nothing more). They keep CoverWall px off the room's walls, CoverDoor off
        /// every door, CoverMid off the middle and CoverGap off each other, so every door and every wall can still be walked along;
        /// a strip (a side of StripMax or less) gets AddStripCover instead, and a bigger room gets more (one per ~220000 px2, up to 4).
        /// A room too small or too doored for that (a 400-800 px room with doors at both ends had no legal spot) gets fallback
        /// passes: first the middle kept clear by only CoverMidTight, then a lone pillar or block CoverTight off the walls, doors
        /// (plus CoverDoorTight) and the middle point.
        public const int CoverWall = 130, CoverDoor = 230, CoverMid = 130, CoverGap = 70;
        public const int CoverMidTight = 60, CoverTight = 100, CoverDoorTight = 180;
        /// Last pass, for a room only 400 px deep between two doors: one small pillar to a side, its centre CoverDoorMin from every door.
        public const int CoverDoorMin = 200;

        /// A narrow room's obstacles: a pillar or block every ~StripCoverEvery px along it, alternating sides, StripCoverWall px off the
        /// side wall (so the far side keeps a wide lane), each centre CoverDoorMin from every door and bend.
        public const int StripMax = 300;
        public const int StripCoverEvery = 500, StripCoverWall = 60, StripCoverR = 25;  // (a square: cover sits on the 50 px floor grid)

        /// A piece's middle on one axis so its edges (half `half` either side) lie on the 50 px floor grid (RoomTraps.SnapCentre).
        static int OnGrid(float v, int half) => (int)RoomTraps.SnapCentre(v, half);

        static void AddStripCover(List<Solid> to, ExpansionRoom r, List<V2> doors)
        {
            var rng = new DetRandom(r.X * 19349663 ^ r.Y * 73856093 ^ r.W * 2654435 ^ r.H * 83492791);
            bool alongX = r.W >= r.H;
            int len = alongX ? r.W : r.H, wid = alongX ? r.H : r.W;
            int count = Math.Max(1, len / StripCoverEvery);
            int side = rng.Int(2) == 0 ? -1 : 1;
            float off = wid / 2f - StripCoverWall - StripCoverR;  // from the strip's middle line
            for (int i = 0; i < count; i++, side = -side)
            {
                bool pillar = rng.Int(2) == 0;
                float t0 = (i + 0.5f) * len / count + (rng.Int(9) - 4) * 10;
                for (int shift = 0; shift < 7; shift++)  // 0, +50, -50, +100, ... until it is clear of the doors
                {
                    float t = t0 + (shift + 1) / 2 * 50 * (shift % 2 == 0 ? -1 : 1);
                    if (t < StripCoverR + 40 || t > len - StripCoverR - 40)
                        continue;
                    V2 c = alongX ? new V2(r.X + t, r.Y + wid / 2f + side * off) : new V2(r.X + wid / 2f + side * off, r.Y + t);
                    bool ok = true;
                    foreach (var d in doors)
                        ok &= c.FartherOrAt(d, CoverDoorMin);
                    if (!ok)
                        continue;
                    int cx = OnGrid(c.X, StripCoverR), cy = OnGrid(c.Y, StripCoverR);
                    to.Add(pillar ? World.Pillar(cx, cy, StripCoverR) : World.Box(cx - StripCoverR, cy - StripCoverR, cx + StripCoverR, cy + StripCoverR));
                    break;
                }
            }
        }

        /// A breach Stash's cover (Docs/BreachRooms.md): a block and a pillar either side of the middle (where the vector cache sits), on a diagonal so a door anywhere on its walls stays clear.
        static void AddStashCover(List<Solid> to, ExpansionRoom r, List<V2> doors)
        {
            bool wide = r.W >= r.H;
            float cx = r.X + r.W / 2f, cy = r.Y + r.H / 2f, len = Math.Max(r.W, r.H);
            for (int k = -1; k <= 1; k += 2)
            {
                float along = k * len * 0.28f, side = k * 55f;
                var at = wide ? new V2(cx + along, cy + side) : new V2(cx + side, cy + along);
                int x = OnGrid(at.X, 25), y = OnGrid(at.Y, 25);
                to.Add(k < 0 ? World.Box(x - 25, y - 25, x + 25, y + 25) : World.Pillar(x, y, 25));
            }
        }

        static void AddCover(List<Solid> to, ExpansionRoom r, List<V2> doors, Dictionary<long, CellLayouts.CellPlan> plans = null)
        {
            if (r.Floating || IsHospital(r))
                return;  // (the floating rooms and a Field Hospital's cell stay clear)
            if (r.Surprise == 1 + (int)SurpriseShape.Stash)
            {
                AddStashCover(to, r, doors);
                return;
            }
            if (RelicGauntlet.Is(r))
                return;  // (the Relic Gauntlet: bare floor, its hazards are pits, turrets and lasers, Gauntlet.cs)
            if (plans != null && CellLayouts.OnLattice(r))
            {
                CellLayouts.AddWalls(to, r, plans);  // (a grid room: its cells' hand-made layouts, not random obstacles; owner Nov 20)
                return;
            }
            if (Math.Min(r.W, r.H) <= StripMax)
            {
                AddStripCover(to, r, doors);
                return;
            }
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
                    // (every size whole squares of the 50 px floor grid: a half side a multiple of 25)
                    case 0: hw = hh = stage == 3 ? 25 : 25 + rng.Int(2) * 25; break;
                    case 1: hw = horizontal ? 75 + rng.Int(4) * 25 : 25; hh = horizontal ? 25 : 75 + rng.Int(4) * 25; break;
                    case 2: hw = hh = 100; break;
                    default: hw = hh = 50; break;
                }
                int wall = stage >= 2 ? CoverTight : CoverWall;
                int ix0 = r.X + wall + hw, ix1 = r.X + r.W - wall - hw, iy0 = r.Y + wall + hh, iy1 = r.Y + r.H - wall - hh;
                if (ix1 <= ix0 || iy1 <= iy0)
                    continue;
                int cx = OnGrid(ix0 + rng.Int((ix1 - ix0) / 10 + 1) * 10, hw), cy = OnGrid(iy0 + rng.Int((iy1 - iy0) / 10 + 1) * 10, hh);  // (onto the grid)
                if (cx < ix0 - 25 || cx > ix1 + 25 || cy < iy0 - 25 || cy > iy1 + 25)
                    continue;
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
                        to.Add(World.Box(bx0, by0, bx1, by0 + 50));
                        to.Add(World.Box(bx0, by0, bx0 + 50, by1));
                        break;
                    default: to.Add(World.Box(bx0, by0, bx1, by1)); break;
                }
            }
        }

        /// A built room's side walls: 0 a design rolled per room (seeded by the box, the same on every machine), 1 fins, 2 baffles,
        /// 3 pincers, 4 T-fins (the preview's FINSTYLE forces one), -1 none.
        public static int FinStyle = 0;
        public const int FinT = 24, FinEnd = 160, FinDoor = 120, FinLane = 260, FinPass = 150, FinMin = 50;  // FinPass: the narrowest gap a wall leaves

        /// Where a side wall off this edge at u, this deep, ends.
        static V2 Tip(int edge, int u, int depth, int x0, int y0, int x1, int y1)
            => edge == 0 ? new V2(x0 + depth, u) : edge == 1 ? new V2(x1 - depth, u) : edge == 2 ? new V2(u, y0 + depth) : new V2(u, y1 - depth);

        /// Is this point within a door's half width plus 70 px of one of this area's doors?
        static bool NearDoor(int area, List<RoomDoor> doors, V2 p)
        {
            foreach (var d in doors)
                if (d.Joins(area) && d.Pos.CloserThan(p, d.W / 2f + 70f))
                    return true;
            return false;
        }

        /// Walls that come out from a drawn room's outer walls into the room, as wall spans (drawn and solid like any wall).
        /// Only from real wall (not a seam to the room's other boxes, not near a door or a corner); cover in the way goes.
        static void AddFins(List<WallSpan> spans, List<Solid> cover, IList<ExpansionRoom> rooms, int[] group, int a, List<RoomDoor> doors, IList<MapNode> nodes)
        {
            var r = rooms[a - 1];
            int x0 = r.X, y0 = r.Y, x1 = r.X1, y1 = r.Y1;
            // Is the wall at this spot on this edge real wall a fin can stand on? (0 west, 1 east, 2 north, 3 south)
            Func<int, int, bool> real = (edge, u) =>
            {
                bool vertical = edge < 2;
                int at = edge == 0 ? x0 : edge == 1 ? x1 : edge == 2 ? y0 : y1;
                int lo = vertical ? y0 : x0, hi = vertical ? y1 : x1;
                int end = Math.Max(60, Math.Min(FinEnd, (hi - lo) / 4));  // (off the corners; less in a small room)
                if (u - lo < end || hi - u < end)
                    return false;
                for (int k = -1; k <= 1; k++)  // (the spot and a little either side: no seam behind it)
                {
                    int uu = u + k * 60, ox = vertical ? at + (edge == 0 ? -20 : 20) : uu, oy = vertical ? uu : at + (edge == 2 ? -20 : 20);
                    for (int b = 1; b < group.Length; b++)
                        if (b != a && group[b] == group[a])
                        {
                            var o = rooms[b - 1];
                            if (ox > o.X && ox < o.X1 && oy > o.Y && oy < o.Y1)
                                return false;
                        }
                }
                V2 foot = vertical ? new V2(at, u) : new V2(u, at);
                foreach (var d in doors)
                    if (d.Joins(a) && (d.Vertical == vertical && Math.Abs((vertical ? d.Pos.X : d.Pos.Y) - at) < 1f
                        && Math.Abs((vertical ? d.Pos.Y : d.Pos.X) - u) < d.W / 2f + FinDoor || d.Pos.CloserThan(foot, d.W / 2f + FinDoor)))
                        return false;  // (off its own wall's doors, and off a door round the corner in a small room)
                return true;
            };
            var made = new List<int[]>();  // each fin: edge, spot, depth
            Action<int, int, int> fin = (edge, u, depth) =>
            {
                if (depth < FinMin || !real(edge, u))
                    return;
                if (NearDoor(a, doors, Tip(edge, u, depth, x0, y0, x1, y1)))
                    return;  // (its tip, too, keeps off the doors)
                made.Add(new[] { edge, u, depth });
            };
            Action stubs = () =>  // fins (and T-fins): short stubs off every wall, one per ~550 px (at least one)
            {
                for (int edge = 0; edge < 4; edge++)
                {
                    int el = edge < 2 ? r.H : r.W, ed = edge < 2 ? r.W : r.H, es = edge < 2 ? y0 : x0;
                    int count = Math.Max(1, el / 550);
                    for (int i = 0; i < count; i++)
                        fin(edge, es + el * (i + 1) / (count + 1) / 10 * 10, Math.Min(220, ed * 3 / 10));
                }
            };
            int style = FinStyle > 0 ? FinStyle : 1 + new DetRandom(r.X * 19349663 ^ r.Y * 83492791 ^ r.W * 73856093 ^ r.H * 5).Int(4);
            bool wide = r.W >= r.H;
            int len = wide ? r.W : r.H, deep = wide ? r.H : r.W;
            int sideA = wide ? 2 : 0, sideB = wide ? 3 : 1, start = wide ? x0 : y0;  // the two long walls
            if (deep <= StripMax)  // a narrow room: stubs from alternate sides, a FinPass lane past each (an even count: the middle stays open)
            {
                int count = Math.Max(2, len / 800 * 2);
                for (int i = 0; i < count; i++)
                    fin(i % 2 == 0 ? sideA : sideB, start + len * (i + 1) / (count + 1) / 10 * 10, deep - FinPass);
                style = 1;  // (no crossbars in a strip)
            }
            else switch (style)
            {
                case 2:  // baffles: long walls from alternate sides, a lane left at the far end of each
                {
                    int count = Math.Max(2, len / 1000 * 2);  // (an even count: an odd one puts a baffle across the middle)
                    for (int i = 0; i < count; i++)
                        fin(i % 2 == 0 ? sideA : sideB, start + len * (i + 1) / (count + 1), Math.Min(deep - Math.Min(FinLane, Math.Max(FinPass, deep / 3)), deep * 2 / 3));
                    break;
                }
                case 3:  // pincers: a pair from both long walls facing each other, a choke in the middle
                {
                    int count = len >= 1300 ? 2 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        int u = start + len * (i + 1) / (count + 1), depth = (deep - Math.Min(300, Math.Max(FinPass, deep / 2))) / 2;
                        if (real(sideA, u) && real(sideB, u)) { fin(sideA, u, depth); fin(sideB, u, depth); }
                    }
                    break;
                }
                default:
                    stubs();
                    break;
            }
            if (made.Count == 0 && style != 1)  // (no room for this design, a door in the way: plain fins instead)
            {
                style = 1;
                stubs();
            }
            foreach (var f in made)
            {
                int edge = f[0], u = f[1], depth = f[2];
                bool onNode = false;  // (a shop or Black Market node is its room's fixture: no wall through it)
                if (nodes != null)
                    foreach (var nd in nodes)
                    {
                        int fx0 = edge == 0 ? x0 : edge == 1 ? x1 - depth : u - 80, fx1 = edge == 0 ? x0 + depth : edge == 1 ? x1 : u + 80;
                        int fy0 = edge >= 2 ? (edge == 2 ? y0 : y1 - depth) : u - 80, fy1 = edge >= 2 ? (edge == 2 ? y0 + depth : y1) : u + 80;
                        onNode |= nd.X > fx0 - NodeClear && nd.X < fx1 + NodeClear && nd.Y > fy0 - NodeClear && nd.Y < fy1 + NodeClear;
                    }
                if (onNode)
                    continue;
                float t = FinT / 2f;
                int ch = Math.Min(80, Math.Min(u - (edge < 2 ? y0 : x0), (edge < 2 ? y1 : x1) - u) - 40);  // a T-fin's crossbar, half: shorter near a corner
                bool cap = style == 4 && ch >= 30;
                if (cap)
                {
                    V2 tip = Tip(edge, u, depth, x0, y0, x1, y1), along = edge < 2 ? new V2(0f, ch) : new V2(ch, 0f);
                    cap = !NearDoor(a, doors, tip + along) && !NearDoor(a, doors, tip - along);
                }
                WallSpan s; float bx0, by0, bx1, by1;
                if (edge < 2)  // off a west or east wall: a horizontal fin
                {
                    int from = edge == 0 ? x0 : x1 - depth, till = edge == 0 ? x0 + depth : x1;
                    s = new WallSpan(false, u, from, till);
                    bx0 = from; bx1 = till; by0 = u - t; by1 = u + t;
                    if (cap) { int e = edge == 0 ? till : from; spans.Add(new WallSpan(true, e, u - ch, u + ch)); by0 = u - ch; by1 = u + ch; }
                }
                else
                {
                    int from = edge == 2 ? y0 : y1 - depth, till = edge == 2 ? y0 + depth : y1;
                    s = new WallSpan(true, u, from, till);
                    by0 = from; by1 = till; bx0 = u - t; bx1 = u + t;
                    if (cap) { int e = edge == 2 ? till : from; spans.Add(new WallSpan(false, e, u - ch, u + ch)); bx0 = u - ch; bx1 = u + ch; }
                }
                spans.Add(s);
                cover.RemoveAll(c => c.Pos.X + c.HW + c.R > bx0 - CoverGap && c.Pos.X - c.HW - c.R < bx1 + CoverGap
                    && c.Pos.Y + c.HH + c.R > by0 - CoverGap && c.Pos.Y - c.HH - c.R < by1 + CoverGap);
            }
        }

        /// The World for the hub plus these rooms. With no rooms it is the hub alone, walled in (the Single world has no walls).
        /// Rooms sharing a stretch of wall of at least MinDoorRun have a door there (the parent's, and loops).
        public static World Build(IList<ExpansionRoom> rooms, IList<WallCut> cuts = null, IList<PortalLink> links = null, IList<MapNode> nodes = null, CoreLayoutId core = CoreLayoutId.None)
        {
            int n = rooms.Count + 1;
            var areas = new RoomArea[n];
            var group = new int[n];       // each area's multi-box room: the area index of its head (itself for a plain box)
            var outline = new bool[n];    // (by head) a drawn outline
            var doors = new List<RoomDoor>();
            var spans = new List<WallSpan>();
            int minX = 0, minY = 0, maxX = (int)Game.Width, maxY = (int)Game.Height;
            for (int a = 0; a < n; a++)
            {
                int x0, y0, x1, y1;
                BoxOf(rooms, a, out x0, out y0, out x1, out y1);
                minX = Math.Min(minX, x0); minY = Math.Min(minY, y0); maxX = Math.Max(maxX, x1); maxY = Math.Max(maxY, y1);
                var kind = a == 0 ? RoomKind.Combat : rooms[a - 1].Kind;
                group[a] = a > 0 && rooms[a - 1].Piece > 0 && rooms[a - 1].Parent < a ? group[rooms[a - 1].Parent] : a;
                if (a > 0 && rooms[a - 1].Piece >= PieceBase)
                    outline[group[a]] = true;
                areas[a] = new RoomArea
                {
                    Name = a == 0 ? "Hub" : rooms[a - 1].Floating ? (kind == RoomKind.Shop ? "Shop" : kind == RoomKind.BlackMarket ? "Black Market" : FactionMinis.Name((EnemyType)rooms[a - 1].Mini)) : rooms[a - 1].Shape + (kind == RoomKind.Shop ? " (shop)" : kind == RoomKind.BlackMarket ? " (Black Market)" : kind == RoomKind.Arms ? " (Arms Dealer)" : kind == RoomKind.Medic ? " (Medic)" : kind == RoomKind.Hospital ? " (Field Hospital)" : kind == RoomKind.Builder ? " (builder shop)" : kind == RoomKind.Floor ? " (" + rooms[a - 1].Floor + ")" : ""),
                    Min = new V2(x0, y0), Max = new V2(x1, y1), Hub = a == 0,
                    Kind = kind, Floor = a == 0 ? PathId.None : rooms[a - 1].Floor, Owner = a == 0 ? 0 : rooms[a - 1].Owner, Floating = a > 0 && rooms[a - 1].Floating, Surprise = a == 0 ? (byte)0 : rooms[a - 1].Surprise,
                };
                if (a > 0)
                    areas[a].PedestalWalls = ShopPedestalWalls(rooms, a);  // (a vendor room's pedestal walls: ShopPedestals.cs)
                // Doors to every earlier room it shares enough wall with (its parent is always one of them).
                for (int b = 0; b < a; b++)
                {
                    int bx0, by0, bx1, by1;
                    BoxOf(rooms, b, out bx0, out by0, out bx1, out by1);
                    bool joined = rooms[a - 1].Piece > 0 && rooms[a - 1].Parent == b;  // an outline's inner seam (or an old save's bent hallway corner): open full width
                    int run = SharedRun(x0, y0, x1, y1, bx0, by0, bx1, by1);
                    if (run < MinDoorRun && !(joined && run > 0))
                        continue;
                    if (!joined && ShopWallBetween(rooms, a, b))
                        continue;  // (a pedestal shop's pedestal walls take no door: ShopPedestals.cs)
                    int ox0 = Math.Max(x0, bx0), ox1 = Math.Min(x1, bx1), oy0 = Math.Max(y0, by0), oy1 = Math.Min(y1, by1);
                    float open = joined ? Math.Max(ox1 - ox0, oy1 - oy0) : 0f;
                    float dy = joined ? (oy0 + oy1) / 2f : GridDoorAlong(oy0, oy1, x0 == bx1 ? x0 : x1, GridX0, GridY0, (y0 + y1) / 2f);
                    float dx = joined ? (ox0 + ox1) / 2f : GridDoorAlong(ox0, ox1, y0 == by1 ? y0 : y1, GridY0, GridX0, (x0 + x1) / 2f);
                    if (x0 == bx1) doors.Add(new RoomDoor { Pos = new V2(x0, dy), Vertical = true, Width = open, A = b, B = a });
                    else if (x1 == bx0) doors.Add(new RoomDoor { Pos = new V2(x1, dy), Vertical = true, Width = open, A = b, B = a });
                    else if (y0 == by1) doors.Add(new RoomDoor { Pos = new V2(dx, y0), Width = open, A = b, B = a });
                    else doors.Add(new RoomDoor { Pos = new V2(dx, y1), Width = open, A = b, B = a });
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
            for (int a = 1; a < n; a++)  // a multi-box room's areas know each other (their rims, Game/RoomsLook.cs)
            {
                bool multi = false;
                for (int j = 1; j < n; j++)
                    multi |= j != a && group[j] == group[a];
                areas[a].Group = multi ? group[a] + 1 : 0;
                if (outline[group[a]])
                {
                    var k = rooms[a - 1].Kind;
                    areas[a].Name = "Drawn room" + (k == RoomKind.Shop ? " (shop)" : k == RoomKind.Floor ? " (" + rooms[a - 1].Floor + ")" : "");
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
            DropSecondGridDoors(doors);
            MarkOneWayDoors(rooms, group, doors);  // a One-way piece's doors (OneWayDoors.cs)
            for (int a = 1; a < n; a++)  // a floating room with no door is sealed
                if (areas[a].Floating)
                {
                    bool open = false;
                    foreach (var d in doors)
                        open |= d.Joins(a);
                    areas[a].Sealed = !open;
                }
            var cover = new List<Solid>();
            var pits = new List<Solid>();
            bool gridRun = false;  // (a Grid run has its core: only there do cells get layouts)
            foreach (var q in rooms)
                gridRun |= IsGridCore(q);
            var plans = gridRun ? CellLayouts.Plans(rooms) : null;
            for (int a = 1; a < n; a++)
            {
                var doorSpots = new List<V2>();
                foreach (var d in doors)
                    if (d.Joins(a))
                        doorSpots.Add(d.Pos);
                var cr = rooms[a - 1];
                if (outline[group[a]] && Math.Min(cr.W, cr.H) < OutlineCoverMin && cr.Surprise != 1 + (int)SurpriseShape.Stash)
                    continue;  // (a sliver of a drawn outline: no cover in it)
                if (gridRun && core != CoreLayoutId.None && IsGridCore(cr))
                    continue;  // (the core's floor plan covers it: CoreLayouts.cs)
                if (IsVendor(cr.Kind))
                    continue;  // (a vendor room is bare: its wares stand on pedestals along its walls, ShopPedestals.cs)
                AddCover(cover, cr, doorSpots, gridRun ? plans : null);
                if (gridRun && RelicGauntlet.Is(cr))
                    RelicGauntlet.AddPits(pits, cr);  // (the Relic Gauntlet's voids)
                else if (gridRun && !cr.Floating && !IsHospital(cr) && CellLayouts.OnLattice(cr))
                    CellLayouts.AddPits(pits, cr, plans);  // a pit variant's voids (CellVariants.cs)
            }
            if (gridRun && core != CoreLayoutId.None)
                CoreLayouts.AddSolids(cover, core);  // the core's floor plan (Docs/Mockups/core-layouts.html)
            else
                core = CoreLayoutId.None;
            for (int a = 1; a < n; a++)  // a built room's side walls: out from its outer walls, a design rolled per room
                if (FinStyle >= 0 && !rooms[a - 1].Floating && !(gridRun && CellLayouts.OnLattice(rooms[a - 1])) && rooms[a - 1].Surprise != 1 + (int)SurpriseShape.Stash)  // (a Stash is too small for fins: its cover stays)
                    AddFins(spans, cover, rooms, group, a, doors, nodes);
            V2[] spots = null;
            if (nodes != null && nodes.Count > 0)  // no cover on a node: it is a fixture of its room (a claimed chest or relic stands in its room's middle)
            {
                spots = new V2[nodes.Count];
                for (int i = 0; i < spots.Length; i++)
                    spots[i] = CentresInRoom(nodes[i].Kind) ? NodeSpotIn(rooms, nodes[i], cover) : nodes[i].Pos;
                cover.RemoveAll(c =>
                {
                    foreach (var sp in spots)
                        if (Math.Abs(c.Pos.X - sp.X) < c.HW + c.R + NodeClear && Math.Abs(c.Pos.Y - sp.Y) < c.HH + c.R + NodeClear)
                            return true;
                    return false;
                });
            }
            var built = new World
            {
                Name = "Expanding",
                Areas = areas, Doors = doors.ToArray(), Portals = new WorldPortal[0], Spans = spans.ToArray(), Cover = cover.ToArray(), Pits = pits.ToArray(), NodeSpots = spots,
                Min = new V2(minX, minY), Max = new V2(maxX, maxY),
                Hub = 0, Core = core,
            };
            var portals = new WorldPortal[links == null ? 0 : links.Count];  // bought portal pairs (Docs/RoomBuilding2.md step 2)
            for (int i = 0; i < portals.Length; i++)
                portals[i] = PortalOf(built, links[i]);
            built.Portals = portals;
            return built;
        }
    }
}
