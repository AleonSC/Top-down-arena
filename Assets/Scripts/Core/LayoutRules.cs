using System;
using System.Collections.Generic;

namespace Arena
{
    /// How the arena's layout plays (Layouts.cs has the floor plans): which one a wave has, what stops walking and
    /// shots, where enemies come in, and how they steer round the walls. The host picks; everyone gets it in the snapshot.
    public partial class Game
    {
        /// Layouts come with regular waves. The test runner turns them off by default (LayoutsByDefault) so the other
        /// tests keep their open arena; the layouts test turns them on.
        public bool LayoutsOn = LayoutsByDefault;
        public static bool LayoutsByDefault = true;

        public LayoutId Layout;     // this cycle's floor plan (Open on boss waves); synced
        public float LayoutAge;     // every game, for the look: seconds since it changed
        int layoutCycle = -1;
        LayoutId lastLayout = LayoutId.Open;
        bool layoutFresh;           // host: a new cycle's layout whose crates haven't been placed yet

        /// The floor plan in force right now: none in a challenge room or the Showcase.
        public LayoutDef ActiveLayout
        {
            get
            {
                if (DrillOn && Training.Drills[Drill].Key == "wallkick")
                    return Training.KickLayout(Width, Height);  // the Wall Kick drill's panels (DrillGame.cs)
                if (RoomsOn)
                    return CurrentWorld.With(Layout);  // the floor plan in the hub, the rooms' walls round it (Rooms.cs)
                if (Layout == LayoutId.Open || InRoom || !ModeRules.Extras || ShowcaseMode)
                    return null;
                return Layouts.Get(Layout);
            }
        }

        public bool LayoutOn { get { return ActiveLayout != null; } }

        /// The edge wall's gates only work while the arena wraps: Narrow Pass and Tiny Arena close it.
        public bool EdgesGated { get { var d = ActiveLayout; return d != null && d.Edge != EdgeGates.Open && !Walled; } }

        void ResetLayout()
        {
            Layout = LayoutId.Open;
            layoutCycle = -1;
            lastLayout = LayoutId.Open;
            layoutFresh = false;
            LayoutAge = 0f;
            RoomsUp = false;
        }

        /// Host, at the start of each wave: boss waves are the open arena; every other cycle (the waves between two
        /// bosses) is a random floor plan, never the same one twice running.
        void PickLayout()
        {
            LayoutId was = Layout;
            if (!LayoutsOn || D(DiffKnob.ArenaLayouts) < 0.5f || !ModeRules.Extras || ShowcaseMode || BossNow)
                Layout = LayoutId.Open;
            else
            {
                int cycle = Hunt != null ? 0 : (Wave - 1) / 5;
                if (cycle != layoutCycle || Layout == LayoutId.Open)
                {
                    layoutCycle = cycle;
                    var pool = new List<LayoutId>();
                    foreach (var d in Layouts.Playable())
                        if (d.Id != lastLayout)
                            pool.Add(d.Id);
                    Layout = pool[rng.Next(pool.Count)];
                    lastLayout = Layout;
                    layoutFresh = true;
                    DoLog("layout: " + Layouts.Get(Layout).Name);
                }
            }
            if (Layout != was)
                LayoutAge = 0f;
        }

        /// Dev and tests: put a floor plan down now (its crates come with the next wave's props).
        public void DevSetLayout(LayoutId id)
        {
            Layout = id;
            LayoutAge = 0f;
            layoutFresh = id != LayoutId.Open;
        }

        /// A cycle's first wave: the layout's own cover goes down, after the last cycle's is cleared away.
        void PlaceLayoutCrates()
        {
            if (!layoutFresh)
                return;
            layoutFresh = false;
            var d = ActiveLayout;
            if (d == null)
                return;
            ClearProps(false);
            foreach (var c in d.Crates)
                AddProp(c.Heavy ? PropKind.HeavyCrate : PropKind.Crate, c.Pos);
        }

        void SyncLayout(NetSync s)
        {
            byte l = (byte)((byte)Layout | (RoomsUp ? 0x80 : 0));  // the top bit: the Rooms world is up (Rooms.cs)
            s.Byte(ref l);
            if (s.Reading)
            {
                bool rooms = (l & 0x80) != 0;
                var lay = (LayoutId)(l & 0x7f);
                if (lay != Layout || rooms != RoomsUp)
                    LayoutAge = 0f;
                Layout = lay;
                RoomsUp = rooms;
            }
        }

        void TickLayout(float dt)
        {
            LayoutAge += dt;
            SurpriseAge += dt;  // (ExpansionGame.cs)
            MapResetAge = Math.Min(99f, MapResetAge + dt);  // (MapReset.cs)
        }

        // --- Walking and shots ----------------------------------------------------------------------

        bool Counts(Solid s) { return !(s.Edge && Walled); }

        /// Moves something of radius r out of every wall and pillar.
        public V2 PushOutOfSolids(V2 pos, float r)
        {
            var d = ActiveLayout;
            if (d == null)
                return pos;
            pos = PushOutOfTurrets(pos, r);  // a room's turrets are solid posts (RoomTraps.cs)
            foreach (var s in d.Solids)
            {
                if (!Counts(s))
                    continue;
                if (s.Round)
                {
                    float dist = pos.DistanceTo(s.Pos);
                    if (dist < s.R + r)
                        pos = s.Pos + (dist > 0.5f ? (pos - s.Pos) / dist : new V2(1f, 0f)) * (s.R + r);
                    continue;
                }
                float hw = s.HW + r, hh = s.HH + r;
                if (Math.Abs(pos.X - s.Pos.X) >= hw || Math.Abs(pos.Y - s.Pos.Y) >= hh)
                    continue;
                if (s.Edge)
                {
                    // The wall round the arena only ever pushes inward (a player landing in it must not be thrown out and wrapped across).
                    if (s.HW >= s.HH)
                        pos = new V2(pos.X, s.Pos.Y < Height / 2f ? s.Pos.Y + hh : s.Pos.Y - hh);
                    else
                        pos = new V2(s.Pos.X < Width / 2f ? s.Pos.X + hw : s.Pos.X - hw, pos.Y);
                    continue;
                }
                float l = pos.X - (s.Pos.X - hw), rr = s.Pos.X + hw - pos.X, t = pos.Y - (s.Pos.Y - hh), b = s.Pos.Y + hh - pos.Y;
                float m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                pos = m == l ? new V2(pos.X - l, pos.Y) : m == rr ? new V2(pos.X + rr, pos.Y) : m == t ? new V2(pos.X, pos.Y - t) : new V2(pos.X, pos.Y + b);
            }
            if (GateFits.Count > 0)
                pos = PushOutOfGates(pos, r);  // a shut shutter gate (Gates.cs)
            var ow = OneWayWorld;
            return ow != null ? ow.OneWayHold(pos, r) : pos;  // a One-way piece's doors (OneWayDoors.cs)
        }

        /// The world whose one-way doors count now (a One-way piece's, OneWayDoors.cs), or null when there are none.
        World OneWayWorld
        {
            get
            {
                if (!ExpandingOn || !RoomsOn)
                    return null;
                var w = CurrentWorld;
                return w.OneWays.Length > 0 ? w : null;
            }
        }

        /// A walker's step from `from` to `to` (a player's move or dash, an enemy's step): not through a one-way door the way it doesn't open.
        V2 OneWayStep(V2 from, V2 to, float r)
        {
            var ow = OneWayWorld;
            return ow != null ? ow.OneWayStop(from, to, r) : to;
        }

        /// Walls and pillars stop every shot, yours and theirs (Combat.cs, in Collide).
        bool SolidStops(Bullet b)
        {
            var d = ActiveLayout;
            foreach (var s in d.Solids)
            {
                if (!Counts(s))
                    continue;
                if (Layouts.Inside(s, b.Pos, b.Radius))
                {
                    AddFx(FxKind.Ring, b.Pos, Defs.BulletColor(b.Kind), 7f, 0.18f);  // a spark on the wall
                    return true;
                }
            }
            return false;
        }

        /// Is something of radius pad at `at` touching a wall or pillar? (Props keep clear of them.)
        bool NearSolid(V2 at, float pad)
        {
            var d = ActiveLayout;
            if (d == null)
                return false;
            foreach (var s in d.Solids)
                if (Counts(s) && Layouts.Inside(s, at, pad))
                    return true;
            return false;
        }

        /// A random spot in the arena (m in from the edge) clear of walls and pillars by `pad`, for a wandering enemy.
        V2 WanderSpot(float m, float pad)
        {
            V2 at = RandomInArena(m);
            for (int i = 0; i < 12 && NearSolid(at, pad); i++)
                at = RandomInArena(m);
            return at;
        }

        // --- Where enemies come in ------------------------------------------------------------------

        /// A regular enemy's spawn spot (m: how far in from the edge). With the edge gated it's a spot along one of
        /// the enemy gates; otherwise a random spot along an edge enemies may come in on.
        V2 LayoutSpawn(float m)
        {
            var d = ActiveLayout;
            if (EdgesGated && d.EnemyGates.Length > 0)
            {
                float total = 0f;
                foreach (var g in d.EnemyGates)
                    if (d.SpawnSide[g.Side])
                        total += g.Len;
                if (total > 0f)
                {
                    float pick = Rand(0f, total);
                    foreach (var g in d.EnemyGates)
                    {
                        if (!d.SpawnSide[g.Side])
                            continue;
                        pick -= g.Len;
                        if (pick <= 0f)
                            return GatePoint(g, m, Math.Min(m, g.Len / 2f - 8f));
                    }
                }
            }
            int side;
            do
                side = rng.Next(4);
            while (!d.SpawnSide[side]);
            V2 lo = ArenaMin + new V2(m, m), hi = ArenaMax - new V2(m, m);
            return side == 0 ? new V2(Rand(lo.X, hi.X), lo.Y) : side == 1 ? new V2(Rand(lo.X, hi.X), hi.Y)
                : side == 2 ? new V2(lo.X, Rand(lo.Y, hi.Y)) : new V2(hi.X, Rand(lo.Y, hi.Y));
        }

        /// A spot m in from the edge, somewhere along the gate (along: how far from its ends to stay).
        V2 GatePoint(EdgeSpan g, float m, float along)
        {
            float u = Rand(g.From + along, g.To - along);
            return g.Side == 0 ? new V2(u, m) : g.Side == 1 ? new V2(u, Height - m) : g.Side == 2 ? new V2(m, u) : new V2(Width - m, u);
        }

        /// Where a squad forms up: in front of one of the enemy gates, or along an allowed edge.
        V2 LayoutSquadBase()
        {
            var d = ActiveLayout;
            if (EdgesGated && d.EnemyGates.Length > 0)
            {
                var ok = new List<EdgeSpan>();
                foreach (var g in d.EnemyGates)
                    if (d.SpawnSide[g.Side])
                        ok.Add(g);
                if (ok.Count > 0)
                {
                    var g = ok[rng.Next(ok.Count)];
                    return GatePoint(g, 50f, Math.Min(60f, g.Len / 2f - 8f));
                }
            }
            return LayoutSpawn(50f);
        }

        /// A Pincer squad's first two come in from the left and right edges: their y on that side.
        float LayoutSideY(int side, float margin)
        {
            var d = ActiveLayout;
            if (EdgesGated)
            {
                var ok = new List<EdgeSpan>();
                foreach (var g in d.EnemyGates)
                    if (g.Side == side)
                        ok.Add(g);
                if (ok.Count > 0)
                {
                    var g = ok[rng.Next(ok.Count)];
                    return Rand(g.From + 10f, g.To - 10f);
                }
            }
            return Rand(ArenaMin.Y + margin, ArenaMax.Y - margin);
        }

        // --- Steering round walls -------------------------------------------------------------------

        sealed class SteerMem
        {
            public int Wp = -1;
            public float Until;
            public LayoutDef Def;  // the floor plan Wp indexes into: a new layout means a fresh start
            public V2 Goal;        // where it was heading when it chose Wp: a goal far from that means a fresh choice
        }

        readonly Dictionary<int, SteerMem> steerMem = new Dictionary<int, SteerMem>();
        readonly List<int> steerGone = new List<int>();

        /// Drops the routes of enemies that are gone (keys are enemy ids; a bot's is minus its player id), so the
        /// living keep theirs instead of every walker re-planning at once.
        void PruneSteerMem()
        {
            steerGone.Clear();
            foreach (var k in steerMem.Keys)
                if (k > 0 && !Enemies.ContainsKey(k))
                    steerGone.Add(k);
            foreach (var k in steerGone)
                steerMem.Remove(k);
            if (steerMem.Count > 300)
                steerMem.Clear();
        }

        /// Does the straight line from a to b, for something of radius r, run into a wall or pillar (the edge wall aside)?
        bool SegBlocked(LayoutDef d, V2 a, V2 b, float r)
        {
            var grid = r <= SolidGrid.Margin ? SolidGrid.For(d) : null;  // (a big map: only the walls along the line, SolidGrid.cs)
            if (grid != null)
                return grid.Blocks(a, b, r);
            foreach (var s in d.Solids)
                if (!s.Edge && SolidBlocks(s, a, b, r))
                    return true;
            return false;
        }

        /// Does the straight line from a to b, for something of radius r, run into this wall or pillar?
        internal static bool SolidBlocks(Solid s, V2 a, V2 b, float r)
        {
            if (s.Round)
            {
                V2 ab = b - a;
                float len2 = ab.X * ab.X + ab.Y * ab.Y, t = len2 > 0.0001f ? Math.Max(0f, Math.Min(1f, ((s.Pos.X - a.X) * ab.X + (s.Pos.Y - a.Y) * ab.Y) / len2)) : 0f;
                return (a + ab * t).DistanceTo(s.Pos) < s.R + r;
            }
            float x0 = s.Pos.X - s.HW - r, x1 = s.Pos.X + s.HW + r, y0 = s.Pos.Y - s.HH - r, y1 = s.Pos.Y + s.HH + r;
            float t0 = 0f, t1 = 1f, dx = b.X - a.X, dy = b.Y - a.Y;
            bool hit = true;
            for (int axis = 0; axis < 2 && hit; axis++)
            {
                float o = axis == 0 ? a.X : a.Y, dd = axis == 0 ? dx : dy, lo = axis == 0 ? x0 : y0, hi = axis == 0 ? x1 : y1;
                if (Math.Abs(dd) < 1e-6f)
                {
                    if (o < lo || o > hi)
                        hit = false;
                    continue;
                }
                float ta = (lo - o) / dd, tb = (hi - o) / dd;
                if (ta > tb) { float sw = ta; ta = tb; tb = sw; }
                t0 = Math.Max(t0, ta);
                t1 = Math.Min(t1, tb);
                if (t0 > t1)
                    hit = false;
            }
            return hit;
        }

        /// The nearest spot to p that a body of radius r can stand on: out of every wall and pillar that SegBlocked
        /// checks (twice round, so an inside corner pushes it clear of both walls).
        static V2 ClearOfWalls(LayoutDef d, V2 p, float r)
        {
            r += 0.5f;
            var grid = r <= SolidGrid.Margin ? SolidGrid.For(d) : null;
            for (int pass = 0; pass < 2; pass++)
            {
                if (grid != null)
                {
                    // A big map: only the walls filed in p's cell can touch it, taken in the same order as below; a push
                    // that moves p on to another cell carries on through that cell's walls (SolidGrid.cs).
                    var list = grid.CellAt(p);
                    int last = -1;
                    for (int k = 0; k < list.Length; k++)
                    {
                        int i = list[k];
                        if (i <= last)
                            continue;
                        last = i;
                        V2 was = p;
                        p = PushClear(d.Solids[i], p, r);
                        if (p.X != was.X || p.Y != was.Y)
                        {
                            list = grid.CellAt(p);
                            k = -1;
                        }
                    }
                    continue;
                }
                foreach (var s in d.Solids)
                    if (!s.Edge)
                        p = PushClear(s, p, r);
            }
            return p;
        }

        /// p moved just clear of one wall or pillar (padded by r), if it was inside it.
        static V2 PushClear(Solid s, V2 p, float r)
        {
            if (s.Round)
            {
                float dist = p.DistanceTo(s.Pos);
                if (dist < s.R + r)
                    p = s.Pos + (dist > 0.5f ? (p - s.Pos) / dist : new V2(1f, 0f)) * (s.R + r);
                return p;
            }
            float hw = s.HW + r, hh = s.HH + r, dx = p.X - s.Pos.X, dy = p.Y - s.Pos.Y;
            if (Math.Abs(dx) >= hw || Math.Abs(dy) >= hh)
                return p;
            if (hw - Math.Abs(dx) < hh - Math.Abs(dy))
                return new V2(s.Pos.X + (dx < 0f ? -hw : hw), p.Y);
            return new V2(p.X, s.Pos.Y + (dy < 0f ? -hh : hh));
        }

        // Waypoint graph: which corners see each other (for a fat enemy), and the shortest walk between any two.
        sealed class WayGraph
        {
            public float[,] Direct;  // the straight line between corners that see each other (else MaxValue)
            public int[][] Out, In;  // each corner's direct links out (Direct[i, k] set) and in (Direct[k, i] set)
            public float[][] OutW, InW;  // their lengths: OutW[i][j] = Direct[i, Out[i][j]], InW[k][j] = Direct[In[k][j], k]
        }

        sealed class GoalInfo
        {
            public float[] Cost;  // from each corner to the target
            public int[] Next;    // the corner to head for from there (-1: straight at the target)
        }

        const float GraphR = 22f, PortalHop = 40f;
        readonly Dictionary<LayoutDef, WayGraph> wayGraphs = new Dictionary<LayoutDef, WayGraph>();
        readonly Dictionary<LayoutDef, WayGraph> plainGraphs = new Dictionary<LayoutDef, WayGraph>();  // the same without the portals: enemies don't use them
        readonly Dictionary<long, GoalInfo> goalCost = new Dictionary<long, GoalInfo>();
        int goalStamp = -1;
        LayoutDef goalDef;

        static void HeapPush(List<KeyValuePair<float, int>> h, float key, int val)
        {
            h.Add(new KeyValuePair<float, int>(key, val));
            for (int i = h.Count - 1; i > 0;)
            {
                int p = (i - 1) / 2;
                if (h[p].Key <= h[i].Key)
                    break;
                var t = h[p]; h[p] = h[i]; h[i] = t;
                i = p;
            }
        }

        static KeyValuePair<float, int> HeapPop(List<KeyValuePair<float, int>> h)
        {
            var top = h[0];
            h[0] = h[h.Count - 1];
            h.RemoveAt(h.Count - 1);
            for (int i = 0; ;)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < h.Count && h[l].Key < h[m].Key) m = l;
                if (r < h.Count && h[r].Key < h[m].Key) m = r;
                if (m == i)
                    break;
                var t = h[m]; h[m] = h[i]; h[i] = t;
                i = m;
            }
            return top;
        }

        WayGraph GraphFor(LayoutDef d, bool portals)
        {
            WayGraph g;
            var cache = portals ? wayGraphs : plainGraphs;
            if (cache.TryGetValue(d, out g))
                return g;
            var w = d.Waypoints;
            int n = w.Length;
            var oneWay = OneWayWorld;
            var dist = new float[n, n];
            WayGraph other;  // the same map's graph with (or without) the portals: its lines are these, the portals' hops aside
            if ((portals ? plainGraphs : wayGraphs).TryGetValue(d, out other) && other.Direct.GetLength(0) == n)
            {
                Array.Copy(other.Direct, dist, n * n);
                if (d.Links != null)
                    for (int l = 0; l + 1 < d.Links.Length; l += 2)
                    {
                        int la = d.Links[l], lb = d.Links[l + 1];
                        bool lblocked = SegBlocked(d, w[la], w[lb], GraphR);
                        dist[la, lb] = portals ? PortalHop : lblocked || oneWay != null && oneWay.OneWayBlocks(w[la], w[lb]) ? float.MaxValue : w[la].DistanceTo(w[lb]);
                        dist[lb, la] = portals ? PortalHop : lblocked || oneWay != null && oneWay.OneWayBlocks(w[lb], w[la]) ? float.MaxValue : w[la].DistanceTo(w[lb]);
                    }
                return cache[d] = g = Linked(dist, n);
            }
            var solids = d.Solids;
            int lastHit = -1;  // the solid that blocked the last line: the next one is often blocked by it too
            var box = new float[d.Solids.Length * 4];  // each solid's box padded by GraphR: x0, x1, y0, y1 (the edge wall's empty)
            for (int k = 0; k < d.Solids.Length; k++)
            {
                var so = d.Solids[k];
                float hw = (so.Round ? so.R : so.HW) + GraphR, hh = (so.Round ? so.R : so.HH) + GraphR;
                if (so.Edge)
                    hw = hh = -1e9f;
                box[4 * k] = so.Pos.X - hw; box[4 * k + 1] = so.Pos.X + hw; box[4 * k + 2] = so.Pos.Y - hh; box[4 * k + 3] = so.Pos.Y + hh;
            }
            for (int i = 0; i < n; i++)
                for (int j = i; j < n; j++)
                {
                    if (i == j)
                    {
                        dist[i, i] = 0f;
                        continue;
                    }
                    float sx0 = Math.Min(w[i].X, w[j].X), sx1 = Math.Max(w[i].X, w[j].X), sy0 = Math.Min(w[i].Y, w[j].Y), sy1 = Math.Max(w[i].Y, w[j].Y);
                    bool blocked = lastHit >= 0 && SolidBlocks(solids[lastHit], w[i], w[j], GraphR);  // (the same both ways: tested once a pair; SegBlocked's test)
                    for (int k = 0; k < solids.Length && !blocked; k++)
                        if (box[4 * k] <= sx1 && box[4 * k + 1] >= sx0 && box[4 * k + 2] <= sy1 && box[4 * k + 3] >= sy0 && SolidBlocks(solids[k], w[i], w[j], GraphR))
                        {
                            blocked = true;  // (only a solid whose padded box meets the line's box can block it)
                            lastHit = k;
                        }
                    dist[i, j] = blocked || oneWay != null && oneWay.OneWayBlocks(w[i], w[j]) ? float.MaxValue : w[i].DistanceTo(w[j]);  // (one way through a One-way piece's door)
                    dist[j, i] = blocked || oneWay != null && oneWay.OneWayBlocks(w[j], w[i]) ? float.MaxValue : w[i].DistanceTo(w[j]);
                }
            if (portals && d.Links != null)  // portals: their two spots count as next to each other (Rooms.cs)
                for (int l = 0; l + 1 < d.Links.Length; l += 2)
                    dist[d.Links[l], d.Links[l + 1]] = dist[d.Links[l + 1], d.Links[l]] = PortalHop;
            return cache[d] = g = Linked(dist, n);
        }

        /// A graph from its direct lines: each corner's links out and in.
        static WayGraph Linked(float[,] dist, int n)
        {
            var outs = new int[n][];
            var ins = new int[n][];
            var outW = new float[n][];
            var inW = new float[n][];
            var tmp = new List<int>();
            for (int i = 0; i < n; i++)
            {
                tmp.Clear();
                for (int k = 0; k < n; k++)
                    if (k != i && dist[i, k] != float.MaxValue)
                        tmp.Add(k);
                outs[i] = tmp.ToArray();
                outW[i] = new float[tmp.Count];
                for (int j = 0; j < tmp.Count; j++)
                    outW[i][j] = dist[i, tmp[j]];
                tmp.Clear();
                for (int k = 0; k < n; k++)
                    if (k != i && dist[k, i] != float.MaxValue)
                        tmp.Add(k);
                ins[i] = tmp.ToArray();
                inW[i] = new float[tmp.Count];
                for (int j = 0; j < tmp.Count; j++)
                    inW[i][j] = dist[tmp[j], i];
            }
            return new WayGraph { Direct = dist, Out = outs, In = ins, OutW = outW, InW = inW };
        }

        const float GoalCell = 24f;  // GoalCosts shares one answer for targets in the same square this wide (a bigger map's rebuild is dear)

        /// For each corner, the length of the best walk from it to tpos (through other corners if need be).
        GoalInfo GoalCosts(LayoutDef d, V2 tpos, float r, bool portals)
        {
            int stamp = (int)(LayoutAge * 5f);
            if (stamp != goalStamp || d != goalDef || goalCost.Count > 64)
            {
                goalCost.Clear();
                goalStamp = stamp;
                goalDef = d;
            }
            long key = ((long)(int)(tpos.X / GoalCell) << 32) ^ ((long)(int)(tpos.Y / GoalCell) << 8) ^ (long)(int)r ^ (portals ? 0L : 1L << 62);
            GoalInfo info;
            if (goalCost.TryGetValue(key, out info))
                return info;
            var w = d.Waypoints;
            var oneWay = OneWayWorld;
            var graph = GraphFor(d, portals);
            int n = w.Length;
            // The best walk to the target from every corner at once: a Dijkstra out from the target, backwards along the
            // links, starting from the corners that see it (their last leg).
            var cost = new float[n];
            var done = new bool[n];
            var heap = new List<KeyValuePair<float, int>>();
            for (int j = 0; j < n; j++)
            {
                cost[j] = SegBlocked(d, w[j], tpos, r) || oneWay != null && oneWay.OneWayBlocks(w[j], tpos) ? float.MaxValue : w[j].DistanceTo(tpos);
                if (cost[j] != float.MaxValue)
                    HeapPush(heap, cost[j], j);
            }
            var last = (float[])cost.Clone();  // corners that see the target: the walk's last leg
            while (heap.Count > 0)
            {
                var top = HeapPop(heap);
                int k = top.Value;
                if (done[k])
                    continue;
                done[k] = true;
                var into = graph.In[k];
                var intoW = graph.InW[k];
                for (int j = 0; j < into.Length; j++)
                {
                    int i = into[j];
                    float c = top.Key + intoW[j];
                    if (c < cost[i])
                    {
                        cost[i] = c;
                        HeapPush(heap, c, i);
                    }
                }
            }
            var next = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (cost[i] == float.MaxValue)
                    cost[i] = 500f + w[i].DistanceTo(tpos);  // no clean route: the nearest corner will do
                next[i] = -1;
            }
            for (int i = 0; i < n; i++)
            {
                if (last[i] != float.MaxValue && last[i] <= cost[i] + 0.01f)
                    continue;  // sees the target: go straight
                float nb = float.MaxValue;
                var outOf = graph.Out[i];
                var outW = graph.OutW[i];
                for (int j = 0; j < outOf.Length; j++)
                {
                    int k = outOf[j];
                    if (outW[j] + cost[k] < nb)
                    {
                        nb = outW[j] + cost[k];
                        next[i] = k;
                    }
                }
            }
            goalCost[key] = info = new GoalInfo { Cost = cost, Next = next };
            return info;
        }

        float[] steerKeys = new float[0];  // SteerDir's corners by walk length (reused)
        int[] steerOrder = new int[0];
        const float SteerGoalSlack = 60f;  // a remembered corner holds while the goal stays this close to where it was

        /// The way a walking enemy heads for a spot of its own choosing (a formation place, a herding spot, another
        /// enemy): round walls (`LayoutDir`) and crates (`PropDir`) like a chase. Bosses go straight. Zero when it's there.
        V2 RouteTo(Enemy e, V2 goal)
        {
            V2 to = goal - e.Pos;
            float len = to.Length;
            if (len < 0.01f)
                return V2.Zero;
            V2 dir = to / len;
            if (e.Def.IsBoss)
                return dir;
            if (LayoutOn)
                dir = LayoutDir(e, goal, dir);
            if (IsHost && Props.Count > 0)
                dir = PropDir(e, dir);
            return dir;
        }

        /// `RouteTo` keeping the length of a goal-seeking velocity (formations slow down as they near their place).
        V2 RouteVel(Enemy e, V2 goal, V2 v)
        {
            float len = v.Length;
            return len < 0.0001f ? v : RouteTo(e, goal) * len;
        }

        /// Which way a walking enemy heads for its target: straight at it, or, with a wall in the way, along the
        /// shortest route through the wall corners (as many as it takes). It sticks to the corner it chose for a
        /// moment so it doesn't dither.
        V2 LayoutDir(Enemy e, V2 tpos, V2 dir) => SteerDir(e.Id, e.Pos, e.T, e.Radius * 0.8f + 3f, tpos, dir);

        /// LayoutDir for anything that walks (an enemy, or a bot heading through the Rooms arena's doors: BotBrain.cs):
        /// key names its steering memory, now is its own clock, r how fat it is.
        V2 SteerDir(int key, V2 pos, float now, float r, V2 tpos, V2 dir)
        {
            var d = SteerLayout;  // (with any shut gate as a wall, so the route goes round it: Gates.cs)
            if (d == null || d.Waypoints.Length == 0)
                return dir;
            // Both ends pulled clear of the walls' padding first: a target standing against a wall (or an enemy sliding
            // along one) sits inside that padding, and every line to it would read as blocked, sending enemies off
            // round far corners instead of straight at you.
            V2 raw = pos;
            pos = ClearOfWalls(d, pos, r);
            tpos = ClearOfWalls(d, tpos, r);
            var oneWay = OneWayWorld;  // (a One-way piece's doors: routes cross them only the way they open, OneWayDoors.cs)
            if (!SegBlocked(d, pos, tpos, r) && (oneWay == null || !oneWay.OneWayBlocks(pos, tpos)))
                return dir;
            bool portals = key < 0;  // (bots, keyed by minus their id, may use portals; enemies, keyed by their id, never do)
            if (portals && d.Mouths != null && d.Mouths.Length > 0)  // at a wall portal its route goes through: walk into the wall (World.cs)
                foreach (var mo in d.Mouths)
                {
                    V2 o = raw - mo.At;
                    float off = o.Dot(mo.In), along = Math.Abs(o.Dot(mo.In.Orthogonal()));
                    if (off < WorldPortal.MouthIn + 16f && off > 0f && along < mo.Half - 8f && GoalCosts(d, tpos, r, true).Next[mo.Wp] == mo.Partner)
                        return -mo.In;
                }
            SteerMem m;
            if (!steerMem.TryGetValue(key, out m))
            {
                if (steerMem.Count > 300)
                    PruneSteerMem();
                steerMem[key] = m = new SteerMem();
            }
            var wps = d.Waypoints;
            if (m.Def != d || m.Wp >= wps.Length || m.Goal.FartherThan(tpos, SteerGoalSlack))
            {
                m.Def = d;
                m.Wp = -1;
            }
            m.Goal = tpos;
            if (m.Wp >= 0 && now < m.Until && pos.FartherThan(wps[m.Wp], 14f) && !SegBlocked(d, pos, wps[m.Wp], r) && (oneWay == null || !oneWay.OneWayBlocks(pos, wps[m.Wp])))
                return (wps[m.Wp] - pos).Normalized();
            var goal = GoalCosts(d, tpos, r, portals);
            var cost = goal.Cost;
            int at = -1;  // standing on a corner: carry on to the next one on the route
            for (int i = 0; i < wps.Length; i++)
                if (pos.CloserThan(wps[i], 14f) && (at < 0 || pos.CloserThan(wps[i], pos.DistanceTo(wps[at]))))
                    at = i;
            if (at >= 0)
            {
                if (goal.Next[at] >= 0)
                {
                    m.Wp = goal.Next[at];
                    m.Until = now + 0.5f;
                    return (wps[m.Wp] - pos).Normalized();
                }
                if (!RoomsOn)
                    return dir;
                // (the Rooms arena: a corner with no way on from it and the target still behind a wall: pick another
                // corner below rather than walk straight into the wall)
            }
            // The corner it can see with the shortest walk on (ties: the lowest corner). Tried cheapest first, so only a few
            // lines are tested, not one to every corner (a grown Expanding map has about a thousand).
            if (steerKeys.Length < wps.Length)
            {
                steerKeys = new float[wps.Length];
                steerOrder = new int[wps.Length];
            }
            int cnt = 0;
            for (int i = 0; i < wps.Length; i++)
            {
                float toW = pos.DistanceTo(wps[i]);
                if (toW < 14f)
                    continue;
                steerKeys[cnt] = toW + cost[i];
                steerOrder[cnt++] = i;
            }
            Array.Sort(steerKeys, steerOrder, 0, cnt);
            int best = -1;
            float bestScore = 0f;
            for (int k = 0; k < cnt; k++)
            {
                if (best >= 0 && steerKeys[k] > bestScore)
                    break;
                int i = steerOrder[k];
                if (best >= 0 && i > best || SegBlocked(d, pos, wps[i], r) || oneWay != null && oneWay.OneWayBlocks(pos, wps[i]))
                    continue;
                best = i;
                bestScore = steerKeys[k];
            }
            if (best < 0)
                return dir;
            m.Wp = best;
            m.Until = now + 0.5f;
            return (wps[best] - pos).Normalized();
        }
    }
}
