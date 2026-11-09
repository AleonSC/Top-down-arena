using System;
using System.Collections.Generic;

namespace Arena
{
    /// One part of the Rooms world: the hub (today's arena), a side room or a hallway. A box in world coordinates.
    public struct RoomArea
    {
        public string Name;
        public V2 Min, Max;
        public bool Hub, Hall;
        public V2 Mid => (Min + Max) * 0.5f;
        public V2 Size => Max - Min;
        public bool Contains(V2 p, float pad = 0f) => p.X >= Min.X - pad && p.X <= Max.X + pad && p.Y >= Min.Y - pad && p.Y <= Max.Y + pad;
    }

    /// A doorway between two areas: a gap in the wall, Rooms.DoorW long. Vertical: the wall runs up and down (x = Pos.X).
    public struct RoomDoor
    {
        public V2 Pos;
        public bool Vertical;
        public int A, B;  // the areas it joins (indexes into Rooms.Areas)
        public V2 Across => Vertical ? new V2(1f, 0f) : new V2(0f, 1f);  // through the door
        public V2 Along => Vertical ? new V2(0f, 1f) : new V2(1f, 0f);   // along the wall
    }

    /// The Rooms arena (Docs/Rooms.md, the Arena setting): on regular waves the arena grows past today's 1600x900 hub
    /// into side rooms joined by hallways. This is the first map, Wings: a big room left and right of the hub, each with
    /// a door into it, and a hallway above and below the hub joining the two rooms into a loop round it (a door from
    /// each room into each hallway). The hub's own edges are wall except the two room doors, so nothing wraps.
    /// The whole world is one box, -900..2500 by -260..1160, the same margin on both sides of the hub, so Game.ArenaMin
    /// and ArenaMax simply grow (Paths.cs). The walls are ordinary layout solids: Rooms.With adds them to the boss
    /// cycle's floor plan, so walking, shots and the enemies' steering (LayoutRules.cs) work through the doors as is.
    public static class Rooms
    {
        public const float RoomW = 900f, HallH = 260f, DoorW = 160f, WallT = 24f, DoorPad = 60f;
        const float W = Game.Width, H = Game.Height;

        /// How far the world reaches past the hub on each side (ArenaMin = -Reach, ArenaMax = hub size + Reach).
        public static readonly V2 Reach = new V2(RoomW, HallH);
        public static V2 WorldMin => new V2(-RoomW, -HallH);
        public static V2 WorldMax => new V2(W + RoomW, H + HallH);

        public const int Hub = 0, West = 1, East = 2, TopHall = 3, BottomHall = 4;

        public static readonly RoomArea[] Areas =
        {
            new RoomArea { Name = "Hub", Min = new V2(0f, 0f), Max = new V2(W, H), Hub = true },
            new RoomArea { Name = "West room", Min = new V2(-RoomW, 0f), Max = new V2(0f, H) },
            new RoomArea { Name = "East room", Min = new V2(W, 0f), Max = new V2(W + RoomW, H) },
            new RoomArea { Name = "Top hall", Min = new V2(-RoomW, -HallH), Max = new V2(W + RoomW, 0f), Hall = true },
            new RoomArea { Name = "Bottom hall", Min = new V2(-RoomW, H), Max = new V2(W + RoomW, H + HallH), Hall = true },
        };

        public static readonly RoomDoor[] Doors =
        {
            new RoomDoor { Pos = new V2(0f, H / 2f), Vertical = true, A = Hub, B = West },
            new RoomDoor { Pos = new V2(W, H / 2f), Vertical = true, A = Hub, B = East },
            new RoomDoor { Pos = new V2(-RoomW / 2f, 0f), A = West, B = TopHall },
            new RoomDoor { Pos = new V2(W + RoomW / 2f, 0f), A = East, B = TopHall },
            new RoomDoor { Pos = new V2(-RoomW / 2f, H), A = West, B = BottomHall },
            new RoomDoor { Pos = new V2(W + RoomW / 2f, H), A = East, B = BottomHall },
        };

        /// The area a point is in (the nearest one for a point in a wall or just outside the world).
        public static int AreaAt(V2 p)
        {
            for (int i = 0; i < Areas.Length; i++)
                if (Areas[i].Contains(p))
                    return i;
            int best = Hub;
            float bestD = float.MaxValue;
            for (int i = 0; i < Areas.Length; i++)
            {
                var a = Areas[i];
                V2 c = p.Clamp(a.Min, a.Max);
                float d = c.DistanceTo(p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// Where the rooms' camera centres a view hx by hy half-size on someone at `at`: held inside the area they're in
        /// (centred on it when it's smaller than the view), then inside the world (CameraZoom.cs; the run log's samples).
        public static V2 ViewCentre(V2 at, float hx, float hy)
        {
            var a = Areas[AreaAt(at)];
            float x = a.Size.X >= 2f * hx ? Math.Max(a.Min.X + hx, Math.Min(a.Max.X - hx, at.X)) : a.Mid.X;
            float y = a.Size.Y >= 2f * hy ? Math.Max(a.Min.Y + hy, Math.Min(a.Max.Y - hy, at.Y)) : a.Mid.Y;
            V2 lo = WorldMin, hi = WorldMax;
            return new V2(Math.Max(lo.X + hx, Math.Min(hi.X - hx, x)), Math.Max(lo.Y + hy, Math.Min(hi.Y - hy, y)));
        }

        // --- The walls ---------------------------------------------------------------------------------------------

        static Solid Box(float x0, float y0, float x1, float y1) => new Solid { Pos = new V2((x0 + x1) / 2f, (y0 + y1) / 2f), HW = (x1 - x0) / 2f, HH = (y1 - y0) / 2f };
        static Solid Pillar(float x, float y, float r) => new Solid { Round = true, Pos = new V2(x, y), R = r };

        /// A wall along a line (horizontal at y from x0 to x1, or vertical at x from y0 to y1), cut at its doors.
        static void WallLine(List<Solid> to, bool vertical, float at, float from, float till)
        {
            var gaps = new List<float>();
            foreach (var d in Doors)
                if (d.Vertical == vertical && Math.Abs((vertical ? d.Pos.X : d.Pos.Y) - at) < 0.5f)
                {
                    float u = vertical ? d.Pos.Y : d.Pos.X;
                    if (u > from && u < till)
                        gaps.Add(u);
                }
            gaps.Sort();
            float a = from, t = WallT / 2f;
            gaps.Add(float.NaN);
            foreach (var g in gaps)
            {
                float b = float.IsNaN(g) ? till : g - DoorW / 2f;
                if (b - a > 1f)
                    to.Add(vertical ? Box(at - t, a, at + t, b) : Box(a, at - t, b, at + t));
                if (!float.IsNaN(g))
                    a = g + DoorW / 2f;
            }
        }

        static Solid[] walls, cover;

        /// The walls between the areas and round the world (cut at the doors), then the small obstacles in the rooms
        /// and hallways (Docs/Rooms.md decision 11).
        public static Solid[] Walls
        {
            get
            {
                if (walls != null)
                    return walls;
                var l = new List<Solid>();
                V2 lo = WorldMin, hi = WorldMax;
                WallLine(l, true, 0f, 0f, H);                 // hub | west room
                WallLine(l, true, W, 0f, H);                  // hub | east room
                WallLine(l, false, 0f, lo.X, hi.X);           // top hall | west room, hub, east room
                WallLine(l, false, H, lo.X, hi.X);            // bottom hall | west room, hub, east room
                WallLine(l, true, lo.X, lo.Y, hi.Y);          // the world's own edges
                WallLine(l, true, hi.X, lo.Y, hi.Y);
                WallLine(l, false, lo.Y, lo.X, hi.X);
                WallLine(l, false, hi.Y, lo.X, hi.X);
                return walls = l.ToArray();
            }
        }

        public static Solid[] Cover
        {
            get
            {
                if (cover != null)
                    return cover;
                float wx = -RoomW / 2f, ex = W + RoomW / 2f;
                return cover = new[]
                {
                    Pillar(wx, 250f, 40f), Pillar(wx, H - 250f, 40f), Box(-RoomW + 165f, H / 2f - 120f, -RoomW + 195f, H / 2f + 120f),  // west room
                    Pillar(ex, 250f, 40f), Pillar(ex, H - 250f, 40f), Box(W + RoomW - 195f, H / 2f - 120f, W + RoomW - 165f, H / 2f + 120f),  // east room
                    Box(170f, -HallH / 2f - 30f, 230f, -HallH / 2f + 30f), Box(W - 230f, -HallH / 2f - 30f, W - 170f, -HallH / 2f + 30f),  // top hall
                    Pillar(200f, H + HallH / 2f, 26f), Pillar(W - 200f, H + HallH / 2f, 26f),  // bottom hall
                };
            }
        }

        /// Either side of each door, for the steering graph: routes from room to room go through these.
        static IEnumerable<V2> DoorSpots()
        {
            foreach (var d in Doors)
            {
                yield return d.Pos + d.Across * DoorPad;
                yield return d.Pos - d.Across * DoorPad;
            }
        }

        static readonly Dictionary<LayoutId, LayoutDef> withRooms = new Dictionary<LayoutId, LayoutDef>();

        /// The boss cycle's floor plan with the rooms round it: the plan's own walls, pillars and crates stay in the hub
        /// (decision 11), its edge wall and gates go (the rooms' walls and doors take their place).
        public static LayoutDef With(LayoutId hub)
        {
            LayoutDef d;
            if (withRooms.TryGetValue(hub, out d))
                return d;
            var plan = Layouts.Get(hub);
            var solids = new List<Solid>();
            foreach (var s in plan.Solids)
                if (!s.Edge)
                    solids.Add(s);
            solids.AddRange(Walls);
            solids.AddRange(Cover);
            d = new LayoutDef
            {
                Id = hub,
                Name = hub == LayoutId.Open ? "Wings" : plan.Name + " · Wings",
                Blurb = plan.Blurb,
                Edge = EdgeGates.Open,
                Solids = solids.ToArray(),
                Crates = plan.Crates,
                PlayerGates = new EdgeSpan[0],
                EnemyGates = new EdgeSpan[0],
                HasHold = plan.HasHold,
                HoldPos = plan.HoldPos,
                HoldR = plan.HoldR,
            };
            d.Waypoints = Layouts.MakeWaypoints(d, WorldMin, WorldMax, DoorSpots());
            withRooms[hub] = d;
            return d;
        }
    }

    public partial class Game
    {
        /// Host, at each wave's start: the Rooms world is up for this wave (synced in the snapshot with the layout).
        public bool RoomsUp;

        /// The Rooms world is what you're playing in right now (not in a Data Gate's challenge, the Practice Range or
        /// the Showcase).
        public bool RoomsOn => RoomsUp && !InRoom && Practice == null && !ShowcaseMode;

        /// The run's Arena setting is Rooms (the lobby's Difficulty tab; Classic, today's arena, by default).
        public bool RoomsArena => D(DiffKnob.ArenaRooms) >= 0.5f;

        /// Host, at the start of each wave (with PickLayout): regular waves get the rooms, boss waves the hub alone
        /// (bosses are tuned for 1600x900). Not with Tiny Arena or on Narrow Pass, which wall the hub in themselves,
        /// and not in a Boss Hunt, the Practice Range or the Showcase.
        void PickRooms()
        {
            bool was = RoomsUp;
            RoomsUp = RoomsArena && Practice == null && !ShowcaseMode && Hunt == null && !IsBossWave(Wave)
                && !Has(RunMod.TinyArena) && !OnPathOf(PathId.NarrowPass);
            if (RoomsUp != was)
            {
                LayoutAge = 0f;  // the walls rise (or the old ones go) with the change
                DoLog(RoomsUp ? "rooms: up (Wings)" : "rooms: down (the hub alone)");
            }
            if (was && !RoomsUp)
                PullIntoHub();
        }

        /// Host, when the rooms go (a boss wave): everyone outside the hub is brought in, and the props left in the
        /// rooms and hallways are cleared away.
        void PullIntoHub()
        {
            var hub = Rooms.Areas[Rooms.Hub];
            foreach (var p in Players.Values)
                if (!hub.Contains(p.Pos, -1f))
                {
                    V2 at = p.Pos.Clamp(hub.Min + new V2(90f, 90f), hub.Max - new V2(90f, 90f));
                    MovePlayer(p, PushOutOfProps(at, p.Def.BodyR));
                }
            scratchIds.Clear();
            foreach (var pr in Props.Values)
                if (!hub.Contains(pr.Pos))
                    scratchIds.Add(pr.Id);
            foreach (int id in scratchIds)
                Props.Remove(id);
        }

        /// The hub's bounds while the rooms are up (else the playable arena): things that belong in the middle of
        /// the fight, like a Data Gate, go here.
        public V2 HubMin => RoomsOn ? V2.Zero : ArenaMin;
        public V2 HubMax => RoomsOn ? new V2(Width, Height) : ArenaMax;

        /// Which part of the Rooms world a point is in (the hub when the rooms aren't up).
        public int AreaAt(V2 p) => RoomsOn ? Rooms.AreaAt(p) : Rooms.Hub;
    }
}
