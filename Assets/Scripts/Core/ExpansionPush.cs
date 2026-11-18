using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// What grows inside a pushed section (Docs/WallPush.md step 3): one style a section, rolled from where it is and its width.
    public enum PushStyle : byte { None, Fins, Baffles, Pincers, TFins, Divider, Cover, Stubs }

    /// A wall push (Docs/WallPush.md): push this stretch of an area's wall out one tile. Side: 0 west, 1 east, 2 north, 3 south
    /// (the same order as Expansion.Build's edges); Start: where the stretch starts along the wall, in world px on the 100 px grid
    /// (y for a west or east wall, x for a north or south one); Width: its length in tiles.
    public struct WallPush
    {
        public short Area;
        public byte Side;
        public short Start;
        public byte Width;
        public int End => Start + Width * Expansion.Step;
    }

    /// Wall push sections (Docs/WallPush.md step 1): a section is one ExpansionRoom box (Leg = PushLeg) joined open, full width, onto the
    /// area it was pushed from (its Parent, 0 for the hub). Pushing the section's own far wall, over its full width, grows that box by a
    /// tile; any other stretch starts a new section. A push costs its new wall in vectors, like a drawn room: a new section its width + 2, each push after 2.
    public static partial class Expansion
    {
        public const byte PushLeg = 15;                       // ExpansionRoom.Leg of a pushed section (below PieceBase: not a drawn outline's piece)
        public const int PushMinWidth = 2, PushMaxWidth = 10; // a section's width, in tiles
        public const int PushMaxDepth = 16;                   // how far out a section goes, in tiles (owner Nov 14: was 12)
        public const int PushReach = 700;                     // how near (px) a player must be to the wall they push (the host allows PushSlack more)
        public const int PushSlack = 300;
        public const int MaxPushes = 40;                      // sections a map can hold (not counted as rooms)

        public static bool IsPush(ExpansionRoom r) => r.Leg == PushLeg;

        /// The map has grown past the plain hub: a room was built or a wall pushed (the floating rooms the game places don't count).
        public static bool Grown(IList<ExpansionRoom> rooms)
        {
            foreach (var r in rooms)
                if (!r.Floating)
                    return true;
            return false;
        }

        /// How many pushed sections the map holds.
        public static int PushCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            foreach (var r in rooms)
                if (IsPush(r))
                    n++;
            return n;
        }

        /// The side a pushed section grows towards (away from its parent): 0 west, 1 east, 2 north, 3 south; -1 if it isn't one.
        public static int PushAway(IList<ExpansionRoom> rooms, int index)
        {
            var r = rooms[index];
            if (!IsPush(r) || r.Parent < 0 || r.Parent > rooms.Count)
                return -1;
            int px0, py0, px1, py1;
            BoxOf(rooms, r.Parent, out px0, out py0, out px1, out py1);
            return r.X == px1 ? 1 : r.X1 == px0 ? 0 : r.Y == py1 ? 3 : r.Y1 == py0 ? 2 : -1;
        }

        /// A section's depth out from its parent, in tiles.
        public static int PushDepth(ExpansionRoom r, int away) => (away < 2 ? r.W : r.H) / Step;

        /// The middle of the stretch of wall a push moves (where the player must be near).
        public static V2 PushFace(IList<ExpansionRoom> rooms, WallPush p)
        {
            if (p.Area < 0 || p.Area > rooms.Count)
                return new V2(0f, 0f);
            int x0, y0, x1, y1;
            BoxOf(rooms, p.Area, out x0, out y0, out x1, out y1);
            float mid = (p.Start + p.End) / 2f;
            return p.Side == 0 ? new V2(x0, mid) : p.Side == 1 ? new V2(x1, mid) : p.Side == 2 ? new V2(mid, y0) : new V2(mid, y1);
        }

        /// The line a push's wall stands on (x for a west or east wall, y for a north or south one).
        public static int PushLine(IList<ExpansionRoom> rooms, WallPush p)
        {
            int x0, y0, x1, y1;
            BoxOf(rooms, p.Area, out x0, out y0, out x1, out y1);
            return p.Side == 0 ? x0 : p.Side == 1 ? x1 : p.Side == 2 ? y0 : y1;
        }

        /// Is the tile just outside this area's wall, at `u` along it (the tile starting there), empty (no room in it)?
        static bool OutsideFree(IList<ExpansionRoom> rooms, int side, int line, int u)
        {
            int tx = side == 0 ? line - Step : side == 1 ? line : u, ty = side == 2 ? line - Step : side == 3 ? line : u;
            for (int a = 0; a <= rooms.Count; a++)
            {
                int bx0, by0, bx1, by1;
                BoxOf(rooms, a, out bx0, out by0, out bx1, out by1);
                if (Overlaps(tx, ty, tx + Step, ty + Step, bx0, by0, bx1, by1))
                    return false;
            }
            return true;
        }

        /// The stretch of wall the mouse points at (Docs/WallPush.md step 2): the nearest outer wall (with nothing outside it) to `at`
        /// within `grab` px, whose spot is within PushReach of the player at `from`; `width` tiles centred on the mouse's tile, cut to
        /// the run of outer wall there (so it may come out narrower). Null when there's none. A connected room's wall only (not a floating room).
        public static WallPush? PushPick(IList<ExpansionRoom> rooms, V2 at, V2 from, int width, float grab = 250f)
        {
            var linked = ConnectedAreas(rooms);
            WallPush? best = null;
            float bestD = grab;
            for (int a = 0; a <= rooms.Count; a++)
            {
                if (!linked[a] || a > 0 && rooms[a - 1].Floating)
                    continue;
                int x0, y0, x1, y1;
                BoxOf(rooms, a, out x0, out y0, out x1, out y1);
                for (int side = 0; side < 4; side++)
                {
                    bool vertical = side < 2;
                    int line = side == 0 ? x0 : side == 1 ? x1 : side == 2 ? y0 : y1;
                    int lo = vertical ? y0 : x0, hi = vertical ? y1 : x1;
                    if (hi - lo < Step)
                        continue;
                    float along = vertical ? at.Y : at.X, across = vertical ? at.X : at.Y;
                    int u = (int)Math.Floor(along / Step) * Step;
                    u = Math.Max(lo, Math.Min(hi - Step, u));
                    V2 spot = vertical ? new V2(line, u + Step / 2f) : new V2(u + Step / 2f, line);
                    float d = Math.Abs(across - line) + Math.Max(0f, Math.Abs(along - (u + Step / 2f)) - Step / 2f);
                    if (d >= bestD || spot.FartherThan(from, PushReach) || !OutsideFree(rooms, side, line, u))
                        continue;
                    int r0 = u, r1 = u + Step;  // the run of outer wall round the spot
                    while (r0 - Step >= lo && OutsideFree(rooms, side, line, r0 - Step)) r0 -= Step;
                    while (r1 + Step <= hi && OutsideFree(rooms, side, line, r1)) r1 += Step;
                    int w = Math.Max(1, Math.Min(Math.Min(width, PushMaxWidth), (r1 - r0) / Step));
                    int start = u - (w / 2) * Step;
                    start = Math.Max(r0, Math.Min(r1 - w * Step, start));
                    bestD = d;
                    best = new WallPush { Area = (short)a, Side = (byte)side, Start = (short)start, Width = (byte)w };
                }
            }
            return best;
        }

        /// The area whose wall on this side stands on `line` and holds the stretch [start, start + width tiles): where a locked
        /// selection is after the map changes (a section grown by a push). -1 if none. A section's own far wall wins over others.
        public static int PushAreaAt(IList<ExpansionRoom> rooms, int side, int line, int start, int width)
        {
            int end = start + width * Step, found = -1;
            for (int a = 0; a <= rooms.Count; a++)
            {
                int x0, y0, x1, y1;
                BoxOf(rooms, a, out x0, out y0, out x1, out y1);
                int at = side == 0 ? x0 : side == 1 ? x1 : side == 2 ? y0 : y1;
                int lo = side < 2 ? y0 : x0, hi = side < 2 ? y1 : x1;
                if (at != line || start < lo || end > hi)
                    continue;
                if (a > 0 && IsPush(rooms[a - 1]) && PushAway(rooms, a - 1) == side)
                    return a;
                if (found < 0)
                    found = a;
            }
            return found;
        }

        // --- Features (step 3): what grows inside a section as it's pushed --------------------------------------------

        /// Preview and tests: every section gets this style (-1: rolled).
        public static int PushStyleOverride = -1;

        /// The styles a section this many tiles wide can roll: 4+ fins, T-fins, cover; 5+ baffles, divider; 6+ pincers.
        static PushStyle[] PushStyles(int width)
            => width >= 6 ? new[] { PushStyle.Fins, PushStyle.Baffles, PushStyle.Pincers, PushStyle.TFins, PushStyle.Divider, PushStyle.Cover }
             : width >= 5 ? new[] { PushStyle.Fins, PushStyle.Baffles, PushStyle.TFins, PushStyle.Divider, PushStyle.Cover }
             : width >= 4 ? new[] { PushStyle.Fins, PushStyle.TFins, PushStyle.Cover }
             : new[] { PushStyle.Stubs };  // (a 2-3 wide corridor: stubs from alternate sides, a 1.5-tile lane past each)

        /// A section's style, rolled from where it is and how wide (not its depth, so it never changes as it grows): the same on every machine.
        public static PushStyle PushStyleFor(int parent, int away, int start, int width)
        {
            var ok = PushStyles(width);
            if (ok.Length == 0)
                return PushStyle.None;
            if (PushStyleOverride >= 0)
                return Array.IndexOf(ok, (PushStyle)PushStyleOverride) >= 0 ? (PushStyle)PushStyleOverride : PushStyle.None;
            return ok[new DetRandom(parent * 7919 ^ away * 104729 ^ start * 31 ^ width * 977 ^ 0x5eed).Int(ok.Length)];
        }

        public static PushStyle PushStyleOf(IList<ExpansionRoom> rooms, int index)
        {
            int away = PushAway(rooms, index);
            if (away < 0)
                return PushStyle.None;
            var r = rooms[index];
            return PushStyleFor(r.Parent, away, away < 2 ? r.Y : r.X, (away < 2 ? r.H : r.W) / Step);
        }

        /// The style a push would give: the section's own if it grows one, else the one a new section there would roll.
        public static PushStyle PushStyleOfPush(IList<ExpansionRoom> rooms, WallPush p)
        {
            if (p.Area > 0 && p.Area <= rooms.Count && IsPush(rooms[p.Area - 1]) && PushAway(rooms, p.Area - 1) == p.Side)
            {
                int lo = p.Side < 2 ? rooms[p.Area - 1].Y : rooms[p.Area - 1].X, w = (p.Side < 2 ? rooms[p.Area - 1].H : rooms[p.Area - 1].W) / Step;
                if (p.Start == lo && p.Width == w)
                    return PushStyleOf(rooms, p.Area - 1);
            }
            return PushStyleFor(p.Area, p.Side, p.Start, p.Width);
        }

        public static string PushStyleName(PushStyle s)
            => s == PushStyle.Fins ? "Fins" : s == PushStyle.Baffles ? "Baffles" : s == PushStyle.Pincers ? "Pincers" : s == PushStyle.TFins ? "T-fins"
             : s == PushStyle.Divider ? "Divider" : s == PushStyle.Cover ? "Cover" : s == PushStyle.Stubs ? "Stubs" : "None";

        public const int PushCrate = 32, PushPillar = 38;  // cover: a crate's half size, a pillar's radius

        /// A section's features (Docs/WallPush.md step 3), from its style and depth: rows 1 to depth - 2 (the two rows by the face stay clear)
        /// get the style's pieces as wall spans and cover. Pieces by a door (a section pushed off this one) or on a node are left out.
        /// Row r's pieces: Fins every 4 rows from alternate sides; Baffles every 3, 2 tiles short of the far side; Pincers every 5, a pair
        /// leaving a 3-tile gap; T-fins every 4, a stub with a 2-tile crossbar; Divider down the middle, 5 rows of wall then a 2-row doorway;
        /// Cover every 3 rows, a crate or pillar (two when 7+ wide).
        public static void PushFeatures(IList<ExpansionRoom> rooms, int index, List<WallSpan> spans, List<Solid> cover, List<RoomDoor> doors = null, IList<MapNode> nodes = null)
        {
            int away = PushAway(rooms, index);
            if (away < 0)
                return;
            var r = rooms[index];
            int w = (away < 2 ? r.H : r.W) / Step, depth = PushDepth(r, away), start = away < 2 ? r.Y : r.X;
            var style = PushStyleFor(r.Parent, away, start, w);
            if (style == PushStyle.None)
                return;
            int s = away == 1 || away == 3 ? 1 : -1;
            float f0 = away == 1 ? r.X : away == 0 ? r.X1 : away == 3 ? r.Y : r.Y1;  // the parent's wall line
            Func<float, float, V2> P = (a, u) => away < 2 ? new V2(f0 + s * u * Step, start + a * Step) : new V2(start + a * Step, f0 + s * u * Step);
            int area = index + 1;
            Func<V2, V2, bool> clear = (p, q) =>  // a piece from p to q keeps off doors and nodes
            {
                if (doors != null)
                    foreach (var d in doors)
                    {
                        if (!d.Joins(area) || d.Joins(r.Parent))
                            continue;  // (its own open join to the room it was pushed from doesn't count)
                        float reach = d.W / 2f + 70f;
                        if (d.Pos.CloserThan(p, reach) || d.Pos.CloserThan(q, reach) || d.Pos.CloserThan((p + q) * 0.5f, reach))
                            return false;
                    }
                if (nodes != null)
                    foreach (var nd in nodes)
                        if (nd.X > Math.Min(p.X, q.X) - NodeClear - 40 && nd.X < Math.Max(p.X, q.X) + NodeClear + 40
                            && nd.Y > Math.Min(p.Y, q.Y) - NodeClear - 40 && nd.Y < Math.Max(p.Y, q.Y) + NodeClear + 40)
                            return false;
                return true;
            };
            Action<float, float, float> across = (u, a0, a1) =>  // a wall parallel to the face, at depth u, from a0 to a1 along it
            {
                V2 p = P(a0, u), q = P(a1, u);
                if (!clear(p, q))
                    return;
                spans.Add(away < 2 ? new WallSpan(true, p.X, Math.Min(p.Y, q.Y), Math.Max(p.Y, q.Y)) : new WallSpan(false, p.Y, Math.Min(p.X, q.X), Math.Max(p.X, q.X)));
            };
            Action<float, float, float> down = (a, u0, u1) =>  // a wall running out from the parent, at a along the face, from depth u0 to u1
            {
                V2 p = P(a, u0), q = P(a, u1);
                if (!clear(p, q))
                    return;
                spans.Add(away < 2 ? new WallSpan(false, p.Y, Math.Min(p.X, q.X), Math.Max(p.X, q.X)) : new WallSpan(true, p.X, Math.Min(p.Y, q.Y), Math.Max(p.Y, q.Y)));
            };
            Action<int, float, float> fin = (side, len, u) => { if (side == 0) across(u, 0f, len); else across(u, w - len, w); };
            int segFrom = -1;  // the divider's open run of rows
            for (int row = 1; row <= depth - 2; row++)
            {
                float mid = row - 0.5f;
                switch (style)
                {
                    case PushStyle.Fins:
                        if (row % 4 == 2) fin((row >> 2) & 1, w >= 6 ? 2f : 1.5f, mid);
                        break;
                    case PushStyle.Baffles:
                        if (row % 3 == 2) fin((row / 3) & 1, w - 2, mid);
                        break;
                    case PushStyle.Stubs:
                        if (row % 3 == 2) fin((row / 3) & 1, w - 1.5f, mid);
                        break;
                    case PushStyle.Pincers:
                        if (row % 5 == 3) { fin(0, (w - 3) / 2f, mid); fin(1, (w - 3) / 2f, mid); }
                        break;
                    case PushStyle.TFins:
                        if (row % 4 == 2 && row + 1 <= depth - 1)
                        {
                            int side = (row >> 2) & 1;
                            float len = w >= 6 ? 2f : 1.5f, c = side == 0 ? len : w - len;
                            int before = spans.Count;
                            fin(side, len, mid);
                            if (spans.Count > before)
                                down(c, mid - 1f, mid + 1f);  // (the crossbar, only with its stem)
                        }
                        break;
                    case PushStyle.Divider:
                    {
                        bool wall = row >= 2 && (row - 2) % 7 < 5;
                        if (wall && segFrom < 0)
                            segFrom = row;
                        bool last = row == depth - 2;
                        if (segFrom >= 0 && (!wall || last))
                        {
                            int till = wall ? row : row - 1;
                            down(w / 2f, segFrom - 1f, till);
                            segFrom = -1;
                        }
                        break;
                    }
                    case PushStyle.Cover:
                        if (row % 3 == 2)
                        {
                            var rng = new DetRandom(r.Parent * 31 + start * 17 + w * 7 + row * 1013 + away);
                            int n = w >= 7 ? 2 : 1, lastA = -1;
                            for (int i = 0; i < n; i++)
                            {
                                int a = 1 + rng.Int(Math.Max(1, w - 2));
                                bool pillar = rng.Float() < 0.45f;
                                if (a == lastA)
                                    continue;
                                lastA = a;
                                V2 c = P(a + 0.5f, mid);
                                if (!clear(c, c))
                                    continue;
                                cover.Add(pillar ? new Solid { Round = true, Pos = c, R = PushPillar } : new Solid { Pos = c, HW = PushCrate, HH = PushCrate });
                            }
                        }
                        break;
                }
            }
        }

        /// Does a section's face (its away side) meet this area's wall for at least a door's run? Then Build opens it full width (it broke through).
        public static bool PushMeets(IList<ExpansionRoom> rooms, int section, int other)
        {
            if (section <= 0 || section > rooms.Count || other == section || other < 0 || other > rooms.Count || !IsPush(rooms[section - 1]))
                return false;
            int away = PushAway(rooms, section - 1);
            if (away < 0 || other == rooms[section - 1].Parent)
                return false;
            return FaceMeets(rooms[section - 1], away, rooms, other);
        }

        static bool FaceMeets(ExpansionRoom r, int away, IList<ExpansionRoom> rooms, int other)
        {
            int bx0, by0, bx1, by1;
            BoxOf(rooms, other, out bx0, out by0, out bx1, out by1);
            bool lined = away == 0 ? r.X == bx1 : away == 1 ? r.X1 == bx0 : away == 2 ? r.Y == by1 : r.Y1 == by0;
            return lined && SharedRun(r.X, r.Y, r.X1, r.Y1, bx0, by0, bx1, by1) >= MinDoorRun;
        }

        /// The room this push's new face would open into (an area index), or -1: for the label (BREAKS THROUGH).
        public static int PushBreaksInto(IList<ExpansionRoom> rooms, WallPush p)
        {
            int idx, cost;
            string why;
            var box = Push(rooms, p, out idx, out cost, out why);
            if (!box.HasValue)
                return -1;
            for (int a = 0; a <= rooms.Count; a++)
                if (a != box.Value.Parent && (idx < 0 || a != idx + 1) && FaceMeets(box.Value, p.Side, rooms, a))
                    return a;
            return -1;
        }

        /// The area index (0 the hub) of the multi-box room this area belongs to: its head, the way Build groups them.
        static int GroupHead(IList<ExpansionRoom> rooms, int area)
        {
            for (int guard = 0; guard < 64 && area > 0 && rooms[area - 1].Leg > 0 && rooms[area - 1].Parent < area; guard++)
                area = rooms[area - 1].Parent;
            return area;
        }

        /// What this push would build: a new section (index -1) or the section at `index` grown a tile, and what it costs. Null with
        /// `why` set if it can't. Rules: a connected room's plain wall (not a floating room), 2-10 tiles on the grid, inside that wall,
        /// at most 12 tiles out, overlapping nothing and a tile off every other room (rooms of the same multi-box room may touch), the
        /// map inside its size cap.
        public static ExpansionRoom? Push(IList<ExpansionRoom> rooms, WallPush p, out int index, out int cost, out string why)
        {
            index = -1; cost = 0; why = null;
            if (p.Area < 0 || p.Area > rooms.Count || p.Side > 3) { why = "No such wall"; return null; }
            if (p.Area > 0 && rooms[p.Area - 1].Floating) { why = "Can't push this room's walls"; return null; }
            if (!ConnectedAreas(rooms)[p.Area]) { why = "Can't push this room's walls"; return null; }
            if (p.Width < PushMinWidth || p.Width > PushMaxWidth) { why = "A section is " + PushMinWidth + "-" + PushMaxWidth + " tiles wide"; return null; }
            if (p.Start % Step != 0) { why = "Off the grid"; return null; }
            int x0, y0, x1, y1;
            BoxOf(rooms, p.Area, out x0, out y0, out x1, out y1);
            bool vertical = p.Side < 2;
            int lo = vertical ? y0 : x0, hi = vertical ? y1 : x1;
            if (p.Start < lo || p.End > hi) { why = "Off the end of the wall"; return null; }

            ExpansionRoom box;
            int own = -1;  // the area this box already is (growing a section), or -1
            if (p.Area > 0 && IsPush(rooms[p.Area - 1]) && PushAway(rooms, p.Area - 1) == p.Side && p.Start == lo && p.End == hi)
            {
                own = p.Area;
                box = rooms[p.Area - 1];
                if (PushDepth(box, p.Side) >= PushMaxDepth) { why = PushMaxDepth + " tiles out is the limit"; return null; }
                if (p.Side == 0) { box.X -= Step; box.W += Step; }
                else if (p.Side == 1) box.W += Step;
                else if (p.Side == 2) { box.Y -= Step; box.H += Step; }
                else box.H += Step;
            }
            else
            {
                if (PushCount(rooms) >= MaxPushes) { why = "The map has too many pushed walls"; return null; }
                int w = p.Width * Step;
                var parent = p.Area == 0 ? default(ExpansionRoom) : rooms[p.Area - 1];
                box = new ExpansionRoom
                {
                    Parent = p.Area, Leg = PushLeg,
                    Kind = p.Area == 0 ? RoomKind.Combat : parent.Kind, Floor = p.Area == 0 ? PathId.None : parent.Floor,
                    X = p.Side == 0 ? x0 - Step : p.Side == 1 ? x1 : p.Start,
                    Y = p.Side == 2 ? y0 - Step : p.Side == 3 ? y1 : p.Start,
                    W = vertical ? Step : w, H = vertical ? w : Step,
                };
            }

            int head = own >= 0 ? GroupHead(rooms, own) : GroupHead(rooms, p.Area);
            int minX = Math.Min(0, box.X), minY = Math.Min(0, box.Y), maxX = Math.Max((int)Game.Width, box.X1), maxY = Math.Max((int)Game.Height, box.Y1);
            for (int a = 0; a <= rooms.Count; a++)
            {
                if (a == own)
                    continue;
                int bx0, by0, bx1, by1;
                BoxOf(rooms, a, out bx0, out by0, out bx1, out by1);
                minX = Math.Min(minX, bx0); minY = Math.Min(minY, by0); maxX = Math.Max(maxX, bx1); maxY = Math.Max(maxY, by1);
                if (Overlaps(box.X, box.Y, box.X1, box.Y1, bx0, by0, bx1, by1)) { why = "Overlaps a room"; return null; }
            }  // (touching another room is fine: its face opens into it, Build; owner Nov 14, was "a tile off other rooms")
            if (maxX - minX > MaxWidth || maxY - minY > MaxHeight) { why = "The map would get too big"; return null; }
            cost = own >= 0 ? 2 : p.Width + 2;  // the new wall, like a drawn room (owner Nov 14): a new section its face and two side tiles, then 2 a push
            box.Price += cost;
            index = own > 0 ? own - 1 : -1;
            return box;
        }
    }

    public partial class Game
    {
        /// A wall can be pushed now: free building, between waves (a building round, or the doors-only break of a full map).
        /// Shelved (owner, Nov 15): wall push is off in the game. The code stays; set this false to bring it back (the tests do).
        public static bool PushShelved = true;

        public bool CanPush => !PushShelved && FreeBuild && CanCut && !GridRoomsOn;  // (grid rooms: no free-form sections)

        float pushClock;                                                   // host: seconds, for the per-player push rate
        readonly Dictionary<int, float> lastPush = new Dictionary<int, float>();
        const float PushGap = 0.15f;                                       // host: a player's pushes at most this often (holding repeats every 0.2 s)

        /// This game's player pushes a stretch of wall out a tile (Docs/WallPush.md).
        public void PushWall(WallPush p)
        {
            if (!CanPush || Me == null)
                return;
            if (IsHost)
            {
                HostWallPush(MyId, p);
                return;
            }
            var w = Proto.Begin(Msg.WallPush);
            w.Write(p.Area); w.Write(p.Side); w.Write(p.Start); w.Write(p.Width);
            session.SendToHost(Proto.End(w));
        }

        public void OnWallPush(int peer, BinaryReader r) => HostWallPush(peer, new WallPush { Area = r.ReadInt16(), Side = r.ReadByte(), Start = r.ReadInt16(), Width = r.ReadByte() });

        /// Host: build a push if it fits, the player is standing near that wall and can pay; they pay its width in vectors.
        void HostWallPush(int id, WallPush wp)
        {
            Player p;
            if (!IsHost || !CanPush || !Players.TryGetValue(id, out p) || p.Downed)
                return;
            float last;
            if (lastPush.TryGetValue(id, out last) && pushClock - last < PushGap)
                return;
            if (wp.Area >= 0 && wp.Area <= Expanded.Count && Expansion.PushFace(Expanded, wp).DistanceTo(p.Pos) > Expansion.PushReach + Expansion.PushSlack)
            {
                DoLog("wall push refused (" + p.Name + " is too far from that wall)");
                return;
            }
            int index, cost;
            string why;
            var made = Expansion.Push(Expanded, wp, out index, out cost, out why);
            if (!made.HasValue || cost > SpendableVectors(p))
            {
                DoLog("wall push refused (" + (why ?? "needs " + cost + " vectors, has " + p.Vectors) + ")");
                return;
            }
            lastPush[id] = pushClock;
            PayVectors(p, cost);
            var box = made.Value;
            if (index < 0)
            {
                box.Owner = id;
                Expanded.Add(box);
            }
            else
                Expanded[index] = box;
            roundBuilt++;
            expWorld = null;
            RaiseRoomsNow();  // (a push off the plain hub: the world goes up now, Rooms.cs)
            TickNodes();      // a node the new floor covers is claimed now
            BroadcastExpansion();
        }

        /// Host: the clock the push rate is measured on (called from TickExpansion every frame).
        void TickPushes(float dt) => pushClock += dt;
    }
}
