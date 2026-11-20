using System;
using System.Collections.Generic;

namespace Arena
{
    /// Which arena the run plays in (Docs/ExpandingArena.md). Single is the Classic arena; Expanding starts as Single
    /// and grows by built rooms; Survival is Survival's big room.
    public enum ArenaLayout : byte { Single, Expanding, Survival }

    /// A wraparound portal pair (Rooms.cs): centres, and the way into the map from each. Half > 0: a wall portal (the Expanding
    /// arena's bought pairs, like the classic arena's edge gates): A and B sit on a room's outer wall line, and the wall from
    /// Half either side of them is the portal; you walk into the wall and come out of the partner wall. Half 0: a floor portal.
    public struct WorldPortal
    {
        public V2 A, B; public V2 InA, InB;
        public float Half;
        public bool Wall => Half > 0f;
        public const float MouthIn = 50f;  // a wall portal's steering spot: this far in from its wall line
        /// Where an end's steering spot is (and where walking distances start): the floor portal itself, or just off the wall.
        public V2 MouthA => Wall ? A + InA * MouthIn : A;
        public V2 MouthB => Wall ? B + InB * MouthIn : B;
    }

    /// A straight stretch of wall, cut at the doors on it. Vertical: runs up and down at x = At, from y = From to Till;
    /// else runs along y = At from x = From to Till.
    public struct WallSpan
    {
        public bool Vertical;
        public float At, From, Till;
        public WallSpan(bool vertical, float at, float from, float till)
        {
            Vertical = vertical; At = at; From = from; Till = till;
        }
    }

    /// The geometry of one arena as data: its areas (rooms), the doors between them, the portals, the wall spans and the
    /// cover. Everything that asks "how big is the playable space, where are the walls, which room is this point in"
    /// reads a World, so a new map is a new World and nothing else changes. Expansion.cs and SurvivalWorld.cs build theirs; Single is
    /// <see cref="Single"/>. The walls and the steering floor plan are built on first use and kept.
    public sealed partial class World
    {
        public string Name;
        public RoomArea[] Areas;
        public RoomDoor[] Doors;
        public WorldPortal[] Portals;
        public WallSpan[] Spans;
        public Solid[] Cover;
        public V2[] NodeSpots;             // the Expanding map's nodes (same order): where each stands now. A claimed chest or relic stands at the middle of its room (Expansion.NodeSpotIn); null if none
        public V2 Min, Max;                // the world's box
        public int Hub;                    // the hub's area index
        public float PortalR = 42f, PortalExit = 70f;

        Solid[] walls;
        readonly Dictionary<int, LayoutDef> plans = new Dictionary<int, LayoutDef>();

        /// A plain box the size of the classic arena: one area, no doors, no walls of its own.
        public static readonly World Single = new World
        {
            Name = "Single",
            Areas = new[] { new RoomArea { Name = "Arena", Min = V2.Zero, Max = new V2(Game.Width, Game.Height), Hub = true } },
            Doors = new RoomDoor[0],
            Portals = new WorldPortal[0],
            Spans = new WallSpan[0],
            Cover = new Solid[0],
            Min = V2.Zero,
            Max = new V2(Game.Width, Game.Height),
        };

        /// Is this door a seam inside one multi-box room (a drawn outline's or bent hallway's boxes, RoomArea.Group)? Open, and not drawn as a door.
        public bool Seam(int i) => Areas[Doors[i].A].Group != 0 && Areas[Doors[i].A].Group == Areas[Doors[i].B].Group;

        /// The area a point is in (the nearest one for a point in a wall or just outside the world).
        public int AreaAt(V2 p)
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

        /// How near its wall line a body's edge must come to go through a wall portal (the wall's half thickness and a little).
        public const float WallReach = Rooms.WallT / 2f + 8f;

        /// Where something stepping into a portal at `at` (a body `r` wide) comes out (null: not in a portal). Keeps its offset along the wall.
        public V2? PortalExitFor(V2 at, float r = 0f)
        {
            foreach (var p in Portals)
            {
                if (p.Wall)
                {
                    V2? x = WallHop(at, r, p.A, p.InA, p.B, p.InB, p.Half);
                    if (!x.HasValue)
                        x = WallHop(at, r, p.B, p.InB, p.A, p.InA, p.Half);
                    if (x.HasValue)
                        return x;
                    continue;
                }
                if (at.CloserThan(p.A, PortalR))
                    return p.B + p.InB * PortalExit + new V2(0f, at.Y - p.A.Y);
                if (at.CloserThan(p.B, PortalR))
                    return p.A + p.InA * PortalExit + new V2(0f, at.Y - p.B.Y);
            }
            return null;
        }

        /// Through a wall portal's end `a` (facing `ina`) and out of `b`: null unless the body touches that stretch of wall. The
        /// offset along the wall turns with the walls, as if the two walls were one doorway: off the left wall onto the right
        /// wall keeps your height, like the classic arena's edge gates.
        static V2? WallHop(V2 at, float r, V2 a, V2 ina, V2 b, V2 inb, float half)
        {
            V2 o = at - a, along = ina.Orthogonal();
            float d = o.Dot(ina), t = o.Dot(along);
            if (d > r + WallReach || d < -WallReach || Math.Abs(t) > half)
                return null;
            t = Math.Max(-(half - Math.Min(r, half * 0.5f)), Math.Min(half - Math.Min(r, half * 0.5f), t));
            V2 e = -ina;  // the way in; the turn takes it to the way out
            V2 turn = new V2(inb.X * e.X + inb.Y * e.Y, inb.Y * e.X - inb.X * e.Y);  // inb times e's conjugate, as complex numbers
            V2 off = along * t;
            V2 offB = new V2(off.X * turn.X - off.Y * turn.Y, off.X * turn.Y + off.Y * turn.X);
            return b + inb * (r + WallReach + 30f) + offB;
        }

        /// Where the camera centres a view hx by hy half-size on someone at `at`: on them, held inside the world (the open
        /// map).
        public V2 ViewCentre(V2 at, float hx, float hy) =>
            new V2(Math.Max(Min.X + hx, Math.Min(Max.X - hx, at.X)), Math.Max(Min.Y + hy, Math.Min(Max.Y - hy, at.Y)));

        /// An area's outline as drawn: its four sides less the stretches open onto another box of the same room (RoomArea.Group: a
        /// drawn outline's pieces, a bent hallway's legs), each pulled `ins` px into the room (longer at an inside corner, shorter at an
        /// outside one). A plain box gives its four sides. Each {from, to}.
        public List<V2[]> Rim(int area, float ins)
        {
            var rim = new List<V2[]>();
            var a = Areas[area];
            for (int side = 0; side < 4; side++)  // 0 west, 1 east, 2 north, 3 south
            {
                bool vertical = side < 2;
                float at = side == 0 ? a.Min.X : side == 1 ? a.Max.X : side == 2 ? a.Min.Y : a.Max.Y;
                float lo = vertical ? a.Min.Y : a.Min.X, hi = vertical ? a.Max.Y : a.Max.X;
                V2 n = side == 0 ? new V2(1f, 0f) : side == 1 ? new V2(-1f, 0f) : side == 2 ? new V2(0f, 1f) : new V2(0f, -1f);  // into the room
                var open = new List<float[]>();
                if (a.Group != 0)
                    for (int j = 0; j < Areas.Length; j++)
                    {
                        var b = Areas[j];
                        if (j == area || b.Group != a.Group)
                            continue;
                        float bat = side == 0 ? b.Max.X : side == 1 ? b.Min.X : side == 2 ? b.Max.Y : b.Min.Y;
                        if (Math.Abs(bat - at) > 0.5f)
                            continue;
                        float o0 = Math.Max(lo, vertical ? b.Min.Y : b.Min.X), o1 = Math.Min(hi, vertical ? b.Max.Y : b.Max.X);
                        if (o1 > o0)
                            open.Add(new[] { o0, o1 });
                    }
                open.Sort((p, q) => p[0].CompareTo(q[0]));
                float pos = lo;
                open.Add(new[] { hi, hi });
                foreach (var o in open)
                {
                    if (o[0] > pos + 0.5f)
                    {
                        V2 p0 = vertical ? new V2(at, pos) : new V2(pos, at), p1 = vertical ? new V2(at, o[0]) : new V2(o[0], at);
                        V2 t = (p1 - p0) * (1f / (o[0] - pos));
                        float e0 = RimEnd(a.Group, area, p0 - t, n, ins), e1 = RimEnd(a.Group, area, p1 + t, n, ins);
                        rim.Add(new[] { p0 + n * ins + t * e0, p1 + n * ins - t * e1 });
                    }
                    pos = Math.Max(pos, o[1]);
                }
            }
            return rim;
        }

        /// How far a rim line stops short of its end (`past`: just beyond that end, on the wall's line): an outside corner stops short by
        /// ins, an inside corner goes on by ins (negative), and a wall the next box carries straight on meets it (0).
        float RimEnd(int group, int area, V2 past, V2 n, float ins)
        {
            if (!InGroup(group, area, past + n))
                return ins;
            return InGroup(group, area, past - n) ? -ins : 0f;
        }

        bool InGroup(int group, int area, V2 p)
        {
            for (int j = 0; j < Areas.Length; j++)
                if ((j == area || group != 0 && Areas[j].Group == group) && Areas[j].Contains(p) && p.X > Areas[j].Min.X && p.X < Areas[j].Max.X && p.Y > Areas[j].Min.Y && p.Y < Areas[j].Max.Y)
                    return true;
            return false;
        }

        // --- The walls ---------------------------------------------------------------------------------------------

        public static Solid Box(float x0, float y0, float x1, float y1) => new Solid { Pos = new V2((x0 + x1) / 2f, (y0 + y1) / 2f), HW = (x1 - x0) / 2f, HH = (y1 - y0) / 2f };
        public static Solid Pillar(float x, float y, float r) => new Solid { Round = true, Pos = new V2(x, y), R = r };

        /// A wall along a line (horizontal at y from x0 to x1, or vertical at x from y0 to y1), cut at its doors.
        void WallLine(List<Solid> to, WallSpan s)
        {
            var gaps = new List<float[]>();  // each gap: its centre and half length
            for (int i = 0; i < Doors.Length; i++)
            {
                var d = Doors[i];
                if (d.Vertical == s.Vertical && Math.Abs((s.Vertical ? d.Pos.X : d.Pos.Y) - s.At) < 0.5f)
                {
                    float u = s.Vertical ? d.Pos.Y : d.Pos.X;
                    if (u > s.From && u < s.Till)
                        gaps.Add(new[] { u, d.W / 2f });
                }
            }
            gaps.Sort((p, q) => p[0].CompareTo(q[0]));
            float a = s.From, t = Rooms.WallT / 2f;
            gaps.Add(null);
            foreach (var g in gaps)
            {
                float b = g == null ? s.Till : g[0] - g[1];
                if (b - a > 1f)
                    to.Add(s.Vertical ? Box(s.At - t, a, s.At + t, b) : Box(a, s.At - t, b, s.At + t));
                if (g != null)
                    a = g[0] + g[1];
            }
        }

        /// The walls between the areas and round the world (cut at the doors).
        public Solid[] Walls()
        {
            if (walls != null)
                return walls;
            var l = new List<Solid>();
            foreach (var s in Spans)
                WallLine(l, s);
            return walls = l.ToArray();
        }

        /// Either side of each door, for the steering graph: routes from room to room go through these.
        IEnumerable<V2> DoorSpots()
        {
            foreach (var d in Doors)
            {
                yield return d.Pos + d.Across * Rooms.DoorPad;
                yield return d.Pos - d.Across * Rooms.DoorPad;
            }
        }

        /// The boss cycle's floor plan with this world round it: the plan's own walls, pillars and crates stay in the hub
        /// (Docs/Rooms.md decision 11), its edge wall and gates go (the world's walls and doors take their place).
        public LayoutDef With(LayoutId hub)
        {
            int key = (int)hub;
            LayoutDef d;
            if (plans.TryGetValue(key, out d))
                return d;
            var plan = Layouts.Get(hub);
            var solids = new List<Solid>();
            foreach (var s in plan.Solids)
                if (!s.Edge)
                    solids.Add(s);
            solids.AddRange(Walls());
            solids.AddRange(Cover);
            d = new LayoutDef
            {
                Id = hub,
                Name = hub == LayoutId.Open ? Name : plan.Name + " · " + Name,
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
            // Steering spots: the usual ones (18 px clear of every wall), minus any within 24 px of the world's own walls (the
            // stubs where two room walls meet leave corners the steering graph's body can't get away from); the hub's floor
            // plan keeps its own, so its narrow doors still have corners to route through.
            var wps = new List<V2>();
            foreach (var p in Layouts.MakeWaypoints(d, Min, Max, DoorSpots()))
            {
                bool tight = false;
                foreach (var w in Walls())
                    tight |= Layouts.Inside(w, p, 24f);
                if (!tight)
                    wps.Add(p);
            }
            // The portals: a steering spot in each, linked to its partner (LayoutRules.cs GraphFor), so enemies route through them.
            var links = new List<int>();
            var mouths = new List<PortalMouth>();
            foreach (var pt in Portals)
            {
                int ia = wps.Count, ib = ia + 1;
                links.Add(ia); wps.Add(pt.MouthA);
                links.Add(ib); wps.Add(pt.MouthB);
                if (pt.Wall)  // a wall portal: from its spot, walk into the wall (LayoutRules.cs SteerDir)
                {
                    mouths.Add(new PortalMouth { Wp = ia, Partner = ib, At = pt.A, In = pt.InA, Half = pt.Half });
                    mouths.Add(new PortalMouth { Wp = ib, Partner = ia, At = pt.B, In = pt.InB, Half = pt.Half });
                }
            }
            d.Links = links.ToArray();
            d.Mouths = mouths.ToArray();
            d.Waypoints = wps.ToArray();
            plans[key] = d;
            return d;
        }
    }
}
