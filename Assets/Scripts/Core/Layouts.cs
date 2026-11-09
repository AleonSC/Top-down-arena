using System;
using System.Collections.Generic;

namespace Arena
{
    /// Fixed floor plans for the arena (Docs/Mockups/arena-layouts.html). Each boss cycle (the waves between two
    /// bosses) gets one at random; boss waves, the Practice Range, challenge rooms and the Showcase use the open arena.
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
        public bool HasHold;             // the Bunker's hold point
        public V2 HoldPos;
        public float HoldR;
    }

    public static class Layouts
    {
        public const float EdgeT = 24f, WallT = 24f, WayPad = 30f;
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

        static void Pair(List<LayoutCrate> to, float cx, float cy, bool vertical)
        {
            if (vertical) { to.Add(Crate(cx, cy - 22f)); to.Add(Crate(cx, cy + 22f)); }
            else { to.Add(Crate(cx - 22f, cy)); to.Add(Crate(cx + 22f, cy)); }
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
                    lrGate = new[] { new[] { 360f, 540f } }; tbGate = new[] { new[] { 700f, 900f } };
                    lrFoe = new[] { new[] { 110f, 250f }, new[] { 650f, 790f } }; tbFoe = new[] { new[] { 250f, 450f }, new[] { 1150f, 1350f } };
                    break;
                case EdgeGates.Twin:
                    lrGate = new[] { new[] { 140f, 280f }, new[] { 620f, 760f } }; tbGate = new[] { new[] { 300f, 500f }, new[] { 1100f, 1300f } };
                    lrFoe = new[] { new[] { 380f, 520f } }; tbFoe = new[] { new[] { 700f, 900f } };
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
        internal static V2[] MakeWaypoints(LayoutDef d, V2 lo, V2 hi, IEnumerable<V2> extra)
        {
            var pts = new List<V2>();
            if (extra != null)
                pts.AddRange(extra);
            foreach (var s in d.Solids)
            {
                if (s.Edge)
                    continue;
                if (s.Round)
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
                    if (Inside(s, p, 18f))
                    {
                        inside = true;
                        break;
                    }
                if (!inside)
                    keep.Add(p);
            }
            return keep.ToArray();
        }

        /// Is p within pad of the solid (inside it, or touching its padded outline)?
        public static bool Inside(Solid s, V2 p, float pad)
        {
            if (s.Round)
                return p.DistanceTo(s.Pos) < s.R + pad;
            return Math.Abs(p.X - s.Pos.X) < s.HW + pad && Math.Abs(p.Y - s.Pos.Y) < s.HH + pad;
        }

        static LayoutDef[] Build()
        {
            var list = new LayoutDef[7];
            var open = new LayoutDef { Id = LayoutId.Open, Name = "Open arena", Blurb = "The plain arena.", Solids = new Solid[0], Crates = new LayoutCrate[0], PlayerGates = new EdgeSpan[0], EnemyGates = new EdgeSpan[0], Waypoints = new V2[0] };
            list[0] = open;

            // Pillar Ring: eight pillars round an open middle, crate pairs in the corners.
            var s = new List<Solid>();
            var c = new List<LayoutCrate>();
            for (int k = 0; k < 8; k++)
            {
                float a = MathX.Pi / 8f + k * MathX.Pi / 4f;
                s.Add(Pillar(800f + 260f * (float)Math.Cos(a), 450f + 260f * (float)Math.Sin(a), 28f));
            }
            Pair(c, 330f, 200f, false); Pair(c, 1270f, 200f, false); Pair(c, 330f, 700f, false); Pair(c, 1270f, 700f, false);
            list[1] = Make(LayoutId.PillarRing, "Pillar Ring", "Eight pillars round an open middle. Loop through the gaps.", EdgeGates.Centre, s, c);

            // Twin Lanes: two long walls with a door in the middle, a crate bar between them.
            s = new List<Solid> { HWall(300f, 740f, 300f), HWall(860f, 1300f, 300f), HWall(300f, 740f, 600f), HWall(860f, 1300f, 600f) };
            c = new List<LayoutCrate> { Crate(800f, 406f), Crate(800f, 450f), Crate(800f, 494f) };
            Pair(c, 800f, 150f, false); Pair(c, 800f, 750f, false);
            list[2] = Make(LayoutId.TwinLanes, "Twin Lanes", "Two long walls make a corridor and two outer strips.", EdgeGates.Sides, s, c);

            // Cross: a plus-shaped wall, four rooms, a 160 px hub.
            s = new List<Solid> { HWall(160f, 720f, 450f), HWall(880f, 1440f, 450f), VWall(800f, 110f, 390f), VWall(800f, 510f, 790f) };
            c = new List<LayoutCrate>();
            Pair(c, 430f, 230f, true); Pair(c, 1170f, 230f, true); Pair(c, 430f, 670f, true); Pair(c, 1170f, 670f, true);
            list[3] = Make(LayoutId.Cross, "Cross", "Four rooms round a small hub.", EdgeGates.Twin, s, c);

            // Islands: crate clusters, nothing else but two small pillars.
            s = new List<Solid> { Pillar(800f, 130f, 24f), Pillar(800f, 770f, 24f) };
            c = new List<LayoutCrate> { Crate(773f, 423f, true), Crate(827f, 423f, true), Crate(773f, 477f, true), Crate(827f, 477f, true),
                Crate(340f, 190f), Crate(384f, 190f), Crate(340f, 234f), Crate(1260f, 190f), Crate(1216f, 190f), Crate(1260f, 234f),
                Crate(340f, 710f), Crate(384f, 710f), Crate(340f, 666f), Crate(1260f, 710f), Crate(1216f, 710f), Crate(1260f, 666f) };
            list[4] = Make(LayoutId.Islands, "Islands", "Crate clusters that wear away as the wave goes on.", EdgeGates.Open, s, c);

            // Funnels: two gated walls near the sides; enemies only come in from the left and right.
            s = new List<Solid> { VWall(260f, 0f, 240f), VWall(260f, 360f, 540f), VWall(260f, 660f, 900f),
                VWall(1340f, 0f, 240f), VWall(1340f, 360f, 540f), VWall(1340f, 660f, 900f), Pillar(800f, 450f, 36f) };
            c = new List<LayoutCrate>();
            Pair(c, 560f, 190f, false); Pair(c, 560f, 710f, false); Pair(c, 1040f, 190f, false); Pair(c, 1040f, 710f, false);
            list[5] = Make(LayoutId.Funnels, "Funnels", "Two gated walls. Enemies stream in from the sides.", EdgeGates.Open, s, c, new[] { false, false, true, true });

            // Bunker: a walled fort with a door on each side and a hold point inside.
            s = new List<Solid> { HWall(640f, 755f, 330f), HWall(845f, 960f, 330f), HWall(640f, 755f, 570f), HWall(845f, 960f, 570f),
                VWall(640f, 330f, 405f), VWall(640f, 495f, 570f), VWall(960f, 330f, 405f), VWall(960f, 495f, 570f),
                Pillar(330f, 200f, 28f), Pillar(1270f, 200f, 28f), Pillar(330f, 700f, 28f), Pillar(1270f, 700f, 28f) };
            c = new List<LayoutCrate>();
            Pair(c, 480f, 450f, true); Pair(c, 1120f, 450f, true);
            list[6] = Make(LayoutId.Bunker, "Bunker", "A walled fort with four doors and a hold point.", EdgeGates.Centre, s, c);
            list[6].HasHold = true;
            list[6].HoldPos = new V2(800f, 450f);
            list[6].HoldR = 46f;
            return list;
        }
    }
}
