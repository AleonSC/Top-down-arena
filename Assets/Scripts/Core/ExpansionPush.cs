using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
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
    /// tile; any other stretch starts a new section. A push costs its width in vectors (1 per new floor tile).
    public static partial class Expansion
    {
        public const byte PushLeg = 15;                       // ExpansionRoom.Leg of a pushed section (below PieceBase: not a drawn outline's piece)
        public const int PushMinWidth = 2, PushMaxWidth = 10; // a section's width, in tiles
        public const int PushMaxDepth = 12;                   // how far out a section goes, in tiles
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
                if (a == box.Parent || GroupHead(rooms, a) == head)
                    continue;  // (its own room's boxes may touch it)
                if (Overlaps(box.X - Step, box.Y - Step, box.X1 + Step, box.Y1 + Step, bx0, by0, bx1, by1))
                {
                    why = "Too close to " + (a == 0 ? "the start room" : rooms[a - 1].Floating ? "a sealed room" : "another room");
                    return null;
                }
            }
            if (maxX - minX > MaxWidth || maxY - minY > MaxHeight) { why = "The map would get too big"; return null; }
            cost = p.Width;
            box.Price += cost;
            index = own > 0 ? own - 1 : -1;
            return box;
        }
    }

    public partial class Game
    {
        /// A wall can be pushed now: free building, between waves (a building round, or the doors-only break of a full map).
        public bool CanPush => FreeBuild && CanCut;

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
