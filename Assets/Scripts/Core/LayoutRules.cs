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

        /// The floor plan in force right now: none in a challenge room, the Practice Range or the Showcase.
        public LayoutDef ActiveLayout
        {
            get
            {
                if (Layout == LayoutId.Open || InRoom || Practice != null || ShowcaseMode)
                    return null;
                return Layouts.Get(Layout);
            }
        }

        public bool LayoutOn { get { return ActiveLayout != null; } }

        /// The edge wall's gates only work while the arena wraps: Narrow Pass, Tiny Arena and Solid Walls close it.
        public bool EdgesGated { get { var d = ActiveLayout; return d != null && d.Edge != EdgeGates.Open && !Walled; } }

        void ResetLayout()
        {
            Layout = LayoutId.Open;
            layoutCycle = -1;
            lastLayout = LayoutId.Open;
            layoutFresh = false;
            LayoutAge = 0f;
        }

        /// Host, at the start of each wave: boss waves are the open arena; every other cycle (the waves between two
        /// bosses) is a random floor plan, never the same one twice running.
        void PickLayout()
        {
            LayoutId was = Layout;
            if (!LayoutsOn || Practice != null || ShowcaseMode || IsBossWave(Wave))
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
            byte l = (byte)Layout;
            s.Byte(ref l);
            if (s.Reading && (LayoutId)l != Layout)
            {
                Layout = (LayoutId)l;
                LayoutAge = 0f;
            }
        }

        void TickLayout(float dt)
        {
            LayoutAge += dt;
        }

        // --- Walking and shots ----------------------------------------------------------------------

        bool Counts(Solid s) { return !(s.Edge && Walled); }

        /// Moves something of radius r out of every wall and pillar.
        public V2 PushOutOfSolids(V2 pos, float r)
        {
            var d = ActiveLayout;
            if (d == null)
                return pos;
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
            return pos;
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

        /// A Pincer squad's Hexers come in from the left and right edges: their y on that side.
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
        }

        readonly Dictionary<int, SteerMem> steerMem = new Dictionary<int, SteerMem>();

        /// Does the straight line from a to b, for something of radius r, run into a wall or pillar (the edge wall aside)?
        bool SegBlocked(LayoutDef d, V2 a, V2 b, float r)
        {
            foreach (var s in d.Solids)
            {
                if (s.Edge)
                    continue;
                if (s.Round)
                {
                    V2 ab = b - a;
                    float len2 = ab.X * ab.X + ab.Y * ab.Y, t = len2 > 0.0001f ? Math.Max(0f, Math.Min(1f, ((s.Pos.X - a.X) * ab.X + (s.Pos.Y - a.Y) * ab.Y) / len2)) : 0f;
                    if ((a + ab * t).DistanceTo(s.Pos) < s.R + r)
                        return true;
                    continue;
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
                if (hit)
                    return true;
            }
            return false;
        }

        /// Which way a walking enemy heads for its target: straight at it, or, with a wall in the way, at the corner
        /// that gets it round soonest. It sticks to the corner it chose for half a second so it doesn't dither.
        V2 LayoutDir(Enemy e, V2 tpos, V2 dir)
        {
            var d = ActiveLayout;
            if (d == null || d.Waypoints.Length == 0)
                return dir;
            float r = e.Radius * 0.8f + 3f;
            if (!SegBlocked(d, e.Pos, tpos, r))
                return dir;
            SteerMem m;
            if (!steerMem.TryGetValue(e.Id, out m))
            {
                if (steerMem.Count > 300)
                    steerMem.Clear();
                steerMem[e.Id] = m = new SteerMem();
            }
            var wps = d.Waypoints;
            if (m.Wp >= 0 && e.T < m.Until && e.Pos.DistanceTo(wps[m.Wp]) > 14f && !SegBlocked(d, e.Pos, wps[m.Wp], r))
                return (wps[m.Wp] - e.Pos).Normalized();
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < wps.Length; i++)
            {
                float toW = e.Pos.DistanceTo(wps[i]);
                if (toW < 14f || SegBlocked(d, e.Pos, wps[i], r))
                    continue;
                float score = toW + wps[i].DistanceTo(tpos) + (SegBlocked(d, wps[i], tpos, r) ? 500f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0)
                return dir;
            m.Wp = best;
            m.Until = e.T + 0.5f;
            return (wps[best] - e.Pos).Normalized();
        }
    }
}
