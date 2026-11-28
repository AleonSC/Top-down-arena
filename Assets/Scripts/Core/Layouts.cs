using System;
using System.Collections.Generic;

namespace Arena
{
    /// Fixed floor plans for the arena (Docs/Mockups/arena-layouts.html). Each boss cycle (the waves between two
    /// bosses) gets one at random; boss waves, challenge rooms and the Showcase use the open arena.
    public enum LayoutId : byte { Open, PillarRing, TwinLanes, Cross, Islands, Funnels, Bunker }

    /// What the arena's edges do. Open: the arena wraps everywhere and enemies come in anywhere. The others wall the
    /// edges in: players can only wrap through the (teal) player gates, and enemies only come in through the
    /// (red, holographic) enemy gates, which are part of the wall.
    public enum EdgeGates : byte { Open, Centre, Twin, Sides }

    /// A wall (a box) or a pillar (a disc) that stops walking and every shot, yours and theirs. Crates are not solids:
    /// they're props, so they wear away and your shots fly over them.
    public struct Solid
    {
        public bool Round, Edge;  // Edge: part of the wall round the arena (Narrow Pass and the other walls drop it)
        public V2 Pos;
        public float HW, HH, R;   // half width and height of a box; radius of a disc
    }

    /// A stretch along one edge: Side 0 top, 1 bottom, 2 left, 3 right; From..To is x for top and bottom, y for left and right.
    public struct EdgeSpan
    {
        public int Side;
        public float From, To;
        public float Mid { get { return (From + To) * 0.5f; } }
        public float Len { get { return To - From; } }
    }

    /// A wall portal's steering spot (World.cs): waypoint Wp, just off the wall at At (facing In, Half long either side),
    /// joined to waypoint Partner on the far end.
    public struct PortalMouth
    {
        public int Wp, Partner;
        public V2 At, In;
        public float Half;
    }

    public struct LayoutCrate
    {
        public V2 Pos;
        public bool Heavy;
    }

    public class LayoutDef
    {
        public LayoutId Id;
        public string Name, Blurb;
        public EdgeGates Edge;
        public bool[] SpawnSide = { true, true, true, true };  // top, bottom, left, right: where enemies may come in
        public Solid[] Solids;           // walls and pillars, then the edge wall
        public LayoutCrate[] Crates;     // the cover it starts the cycle with
        public EdgeSpan[] PlayerGates;   // gaps in the edge wall you can wrap through
        public EdgeSpan[] EnemyGates;    // the holographic parts of the edge wall enemies come in through
        public V2[] Waypoints;           // corners enemies steer round (LayoutRules.cs)
        public int[] Links;              // pairs of waypoints joined by a portal (the Rooms map: Rooms.cs), or null
        public PortalMouth[] Mouths;     // the wall portals' waypoints (World.With): there, walk into the wall to go through
        public bool HasHold;             // the Bunker's hold point
        public V2 HoldPos;
        public float HoldR;
        public SolidGrid Grid;           // its walls by cell, for quick line tests (SolidGrid.cs; built on first use)

        /// This plan with more solids added (the same corners and portal links): for steering round a shut gate (Gates.cs).
        public LayoutDef WithSolids(Solid[] extra)
        {
            var d = (LayoutDef)MemberwiseClone();
            var all = new Solid[Solids.Length + extra.Length];
            Solids.CopyTo(all, 0);
            extra.CopyTo(all, Solids.Length);
            d.Solids = all;
            return d;
        }
    }

    public static class Layouts
    {
        public const float EdgeT = 25f, WallT = 50f, WayPad = 30f;  // (on the 50 px floor grid, RoomTraps.Tile: walls a square thick, the edge wall half one)
        const float W = Game.Width, H = Game.Height;

        static readonly LayoutDef[] defs = Build();

        public static LayoutDef Get(LayoutId id) { return defs[(int)id]; }

        /// Every layout the cycle can pick (not the open arena).
        public static IEnumerable<LayoutDef> Playable()
        {
            for (int i = 1; i < defs.Length; i++)
                yield return defs[i];
        }

        // --- Building --------------------------------------------------------------------------------

        static Solid Box(float cx, float cy, float w, float h, bool edge = false)
        {
            return new Solid { Pos = new V2(cx, cy), HW = w / 2f, HH = h / 2f, Edge = edge };
        }
        static Solid HWall(float x1, float x2, float y) { return Box((x1 + x2) / 2f, y, x2 - x1, WallT); }
        static Solid VWall(float x, float y1, float y2) { return Box(x, (y1 + y2) / 2f, WallT, y2 - y1); }
        static Solid Pillar(float cx, float cy, float r) { return new Solid { Round = true, Pos = new V2(cx, cy), R = r }; }
        static LayoutCrate Crate(float x, float y, bool heavy = false) { return new LayoutCrate { Pos = new V2(x, y), Heavy = heavy }; }

        static void Block(List<LayoutCrate> to, float cx, float cy)
        {
            Pair(to, cx, cy - 25f, false); Pair(to, cx, cy + 25f, false);
        }

        static void Pair(List<LayoutCrate> to, float cx, float cy, bool vertical)
        {
            if (vertical) { to.Add(Crate(cx, cy - 25f)); to.Add(Crate(cx, cy + 25f)); }  // (a square apart: each on a square's middle)
            else { to.Add(Crate(cx - 25f, cy)); to.Add(Crate(cx + 25f, cy)); }
        }

        static EdgeSpan Span(int side, float from, float to) { return new EdgeSpan { Side = side, From = from, To = to }; }

        /// The gaps and holograms of an edge preset. Left and right use y, top and bottom use x. Each gap pairs with
        /// the same spot on the opposite edge.
        static void EdgeSpans(EdgeGates g, List<EdgeSpan> gates, List<EdgeSpan> foes)
        {
            float[][] lrGate, tbGate, lrFoe, tbFoe;
            switch (g)
            {
                case EdgeGates.Centre:
                    lrGate = new[] { new[] { 350f, 550f } }; tbGate = new[] { new[] { 700f, 900f } };
                    lrFoe = new[] { new[] { 100f, 250f }, new[] { 650f, 800f } }; tbFoe = new[] { new[] { 250f, 450f }, new[] { 1150f, 1350f } };
                    break;
                case EdgeGates.Twin:
                    lrGate = new[] { new[] { 150f, 300f }, new[] { 600f, 750f } }; tbGate = new[] { new[] { 300f, 500f }, new[] { 1100f, 1300f } };
                    lrFoe = new[] { new[] { 400f, 500f } }; tbFoe = new[] { new[] { 700f, 900f } };
                    break;
                case EdgeGates.Sides:
                    lrGate = new[] { new[] { 300f, 600f } }; tbGate = new float[0][];
                    lrFoe = new[] { new[] { 100f, 200f }, new[] { 700f, 800f } }; tbFoe = new[] { new[] { 500f, 700f }, new[] { 900f, 1100f } };
                    break;
                default:
                    return;
            }
            foreach (var s in lrGate) { gates.Add(Span(2, s[0], s[1])); gates.Add(Span(3, s[0], s[1])); }
            foreach (var s in tbGate) { gates.Add(Span(0, s[0], s[1])); gates.Add(Span(1, s[0], s[1])); }
            foreach (var s in lrFoe) { foes.Add(Span(2, s[0], s[1])); foes.Add(Span(3, s[0], s[1])); }
            foreach (var s in tbFoe) { foes.Add(Span(0, s[0], s[1])); foes.Add(Span(1, s[0], s[1])); }
        }

        /// The edge wall: everything along each edge except the player gates (the enemy gates are part of it).
        static void EdgeWall(List<Solid> to, List<EdgeSpan> gates)
        {
            for (int side = 0; side < 4; side++)
            {
                float len = side < 2 ? W : H, at = 0f;
                var holes = new List<EdgeSpan>();
                foreach (var s in gates)
                    if (s.Side == side)
                        holes.Add(s);
                holes.Sort((a, b) => a.From.CompareTo(b.From));
                for (int i = 0; i <= holes.Count; i++)
                {
                    float to2 = i < holes.Count ? holes[i].From : len;
                    if (to2 - at > 1f)
                        to.Add(side == 0 ? Box((at + to2) / 2f, EdgeT / 2f, to2 - at, EdgeT, true)
                            : side == 1 ? Box((at + to2) / 2f, H - EdgeT / 2f, to2 - at, EdgeT, true)
                            : side == 2 ? Box(EdgeT / 2f, (at + to2) / 2f, EdgeT, to2 - at, true)
                            : Box(W - EdgeT / 2f, (at + to2) / 2f, EdgeT, to2 - at, true));
                    if (i < holes.Count)
                        at = holes[i].To;
                }
            }
        }

        static LayoutDef Make(LayoutId id, string name, string blurb, EdgeGates edge, List<Solid> solids, List<LayoutCrate> crates, bool[] spawn = null)
        {
            HubLayoutEdits.Committed(id, ref solids, ref crates);  // (edits folded into the code: HubLayouts.Generated.cs)
            return MakeRaw(id, name, blurb, edge, solids, crates, spawn);
        }

        /// The walls and crates `d` was built from (not the edge wall).
        public static void PartsOf(LayoutDef d, out List<Solid> solids, out List<LayoutCrate> crates)
        {
            solids = new List<Solid>();
            foreach (var s in d.Solids)
                if (!s.Edge)
                    solids.Add(s);
            crates = new List<LayoutCrate>(d.Crates);
        }

        /// Put new walls and crates in a floor plan (the Cell Editor's Hub tab): same name, edge gates and spawn sides, its corners made again.
        public static void Replace(LayoutId id, List<Solid> solids, List<LayoutCrate> crates)
        {
            var old = defs[(int)id];
            var d = MakeRaw(id, old.Name, old.Blurb, old.Edge, solids, crates, old.SpawnSide);
            d.HasHold = old.HasHold;
            d.HoldPos = old.HoldPos;
            d.HoldR = old.HoldR;
            defs[(int)id] = d;
        }

        static LayoutDef MakeRaw(LayoutId id, string name, string blurb, EdgeGates edge, List<Solid> solids, List<LayoutCrate> crates, bool[] spawn)
        {
            var d = new LayoutDef { Id = id, Name = name, Blurb = blurb, Edge = edge, Crates = crates.ToArray() };
            if (spawn != null)
                d.SpawnSide = spawn;
            var gates = new List<EdgeSpan>();
            var foes = new List<EdgeSpan>();
            EdgeSpans(edge, gates, foes);
            var all = new List<Solid>(solids);
            EdgeWall(all, gates);
            d.Solids = all.ToArray();
            d.PlayerGates = gates.ToArray();
            d.EnemyGates = foes.ToArray();
            d.Waypoints = MakeWaypoints(d);
            return d;
        }

        /// Corners to steer round: each box's four corners and eight spots round each pillar, pushed out from it, and
        /// only the ones that aren't inside something else.
        static V2[] MakeWaypoints(LayoutDef d) => MakeWaypoints(d, V2.Zero, new V2(W, H), null);

        /// The same for any bounds (lo..hi), plus extra spots of the caller's (Rooms.cs: either side of each door).
        /// squarePillars: a pillar gets the four corners of its square, as a box does, not eight spots round it (the walled
        /// worlds: their many pillars would make the route graph, all pairs of spots, slow to build).
        internal static V2[] MakeWaypoints(LayoutDef d, V2 lo, V2 hi, IEnumerable<V2> extra, float clear = 18f, bool squarePillars = false)
        {
            var pts = new List<V2>();
            if (extra != null)
                pts.AddRange(extra);
            foreach (var s in d.Solids)
            {
                if (s.Edge)
                    continue;
                if (s.Round && squarePillars)
                    for (int i = 0; i < 4; i++)
                        pts.Add(s.Pos + new V2((i % 2 == 0 ? -1f : 1f) * (s.R + WayPad), (i < 2 ? -1f : 1f) * (s.R + WayPad)));
                else if (s.Round)
                    for (int i = 0; i < 8; i++)
                        pts.Add(s.Pos + V2.FromAngle(MathX.Tau * i / 8f + 0.39f) * (s.R + WayPad));
                else
                    for (int i = 0; i < 4; i++)
                        pts.Add(s.Pos + new V2((i % 2 == 0 ? -1f : 1f) * (s.HW + WayPad), (i < 2 ? -1f : 1f) * (s.HH + WayPad)));
            }
            var keep = new List<V2>();
            foreach (var p in pts)
            {
                if (p.X < lo.X + 40f || p.Y < lo.Y + 40f || p.X > hi.X - 40f || p.Y > hi.Y - 40f)
                    continue;
                bool inside = false;
                foreach (var s in d.Solids)
                    if (Inside(s, p, clear))
                    {
                        inside = true;
                        break;
                    }
                if (inside)
                    continue;
                bool twin = false;  // two walls' padded corners can coincide: twin spots point at each other and trap an enemy standing on them
                foreach (var k in keep)
                    if (k.CloserThan(p, 1f))
                    {
                        twin = true;
                        break;
                    }
                if (!twin)
                    keep.Add(p);
            }
            return keep.ToArray();
        }

        /// Is p within pad of the solid (inside it, or touching its padded outline)?
        public static bool Inside(Solid s, V2 p, float pad)
        {
            if (s.Round)
                return p.CloserThan(s.Pos, s.R + pad);
            return Math.Abs(p.X - s.Pos.X) < s.HW + pad && Math.Abs(p.Y - s.Pos.Y) < s.HH + pad;
        }

        static LayoutDef[] Build()
        {
            var list = new LayoutDef[7];
            var open = new LayoutDef { Id = LayoutId.Open, Name = "Open arena", Blurb = "The plain arena.", Solids = new Solid[0], Crates = new LayoutCrate[0], PlayerGates = new EdgeSpan[0], EnemyGates = new EdgeSpan[0], Waypoints = new V2[0] };
            list[0] = open;

            // Every plan sits on the 50 px floor grid (RoomTraps.Tile): wall edges on its lines, crates on square middles. The hub is 32 x 18 squares.
            // Pillar Ring: eight square wall pillars round an open middle, crate pairs in the corners.
            var s = new List<Solid>();
            var c = new List<LayoutCrate>();
            foreach (var p in new[] { new[] { 575f, 375f }, new[] { 1025f, 375f }, new[] { 575f, 525f }, new[] { 1025f, 525f }, new[] { 725f, 225f }, new[] { 875f, 225f }, new[] { 725f, 675f }, new[] { 875f, 675f } })
                s.Add(Box(p[0], p[1], 50f, 50f));
            Pair(c, 350f, 175f, false); Pair(c, 1250f, 175f, false); Pair(c, 350f, 725f, false); Pair(c, 1250f, 725f, false);
            list[1] = Make(LayoutId.PillarRing, "Pillar Ring", "Eight pillars round an open middle. Loop through the gaps.", EdgeGates.Centre, s, c);

            // Twin Lanes: two long walls with a door in the middle, a crate block between them.
            s = new List<Solid> { HWall(300f, 750f, 275f), HWall(850f, 1300f, 275f), HWall(300f, 750f, 625f), HWall(850f, 1300f, 625f) };
            c = new List<LayoutCrate>();
            Block(c, 800f, 450f);
            Pair(c, 800f, 125f, false); Pair(c, 800f, 775f, false);
            list[2] = Make(LayoutId.TwinLanes, "Twin Lanes", "Two long walls make a corridor and two outer strips.", EdgeGates.Sides, s, c);

            // Cross: a pinwheel of four walls (each arm half a square off the middle line, so it sits on the grid), four rooms, a hub between.
            s = new List<Solid> { HWall(150f, 700f, 425f), HWall(900f, 1450f, 475f), VWall(825f, 100f, 400f), VWall(775f, 500f, 800f) };
            c = new List<LayoutCrate>();
            Pair(c, 425f, 200f, true); Pair(c, 1175f, 200f, true); Pair(c, 425f, 700f, true); Pair(c, 1175f, 700f, true);
            list[3] = Make(LayoutId.Cross, "Cross", "Four rooms round a small hub.", EdgeGates.Twin, s, c);

            // Islands: crate clusters, nothing else.
            s = new List<Solid>();
            c = new List<LayoutCrate> { Crate(775f, 425f, true), Crate(825f, 425f, true), Crate(775f, 475f, true), Crate(825f, 475f, true) };
            Pair(c, 800f, 125f, false); Pair(c, 800f, 775f, false);
            Block(c, 350f, 200f); Block(c, 1250f, 200f); Block(c, 350f, 700f); Block(c, 1250f, 700f);
            list[4] = Make(LayoutId.Islands, "Islands", "Crate clusters that wear away as the wave goes on.", EdgeGates.Open, s, c);

            // Funnels: two gated walls near the sides; enemies only come in from the left and right.
            s = new List<Solid> { VWall(275f, 0f, 200f), VWall(275f, 350f, 550f), VWall(275f, 700f, 900f),
                VWall(1325f, 0f, 200f), VWall(1325f, 350f, 550f), VWall(1325f, 700f, 900f), Box(800f, 450f, 100f, 100f) };
            c = new List<LayoutCrate>();
            Pair(c, 550f, 175f, false); Pair(c, 550f, 725f, false); Pair(c, 1050f, 175f, false); Pair(c, 1050f, 725f, false);
            list[5] = Make(LayoutId.Funnels, "Funnels", "Two gated walls. Enemies stream in from the sides.", EdgeGates.Open, s, c, new[] { false, false, true, true });

            // Bunker: a walled fort with a door on each side and a hold point inside.
            s = new List<Solid> { HWall(600f, 750f, 325f), HWall(850f, 1000f, 325f), HWall(600f, 750f, 575f), HWall(850f, 1000f, 575f),
                VWall(625f, 350f, 400f), VWall(625f, 500f, 550f), VWall(975f, 350f, 400f), VWall(975f, 500f, 550f),
                Box(325f, 175f, 50f, 50f), Box(1275f, 175f, 50f, 50f), Box(325f, 725f, 50f, 50f), Box(1275f, 725f, 50f, 50f) };
            c = new List<LayoutCrate>();
            Pair(c, 475f, 450f, true); Pair(c, 1125f, 450f, true);
            list[6] = Make(LayoutId.Bunker, "Bunker", "A walled fort with four doors and a hold point.", EdgeGates.Centre, s, c);
            list[6].HasHold = true;
            list[6].HoldPos = new V2(800f, 450f);
            list[6].HoldR = 50f;
            return list;
        }
    }
}
