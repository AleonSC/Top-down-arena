using System;
using System.Collections.Generic;

namespace Arena
{
    /// Enemies path round crates and barrels (host). When one is in a walker's way (`PropAhead`), a breadth-first search
    /// on a small grid of 16 px squares round it (480 px across, walls and the arena's edge counted too) finds the way to
    /// a point PropPlanAhead px along its heading, and it walks at the farthest point of that path it can see straight.
    /// The route is kept while the straight line to that point stays blocked, so a walker backing out of a pocket of
    /// crates doesn't turn straight back in. No way round (boxed in): it keeps on and shoves (`PropDir`).
    public partial class Game
    {
        const float RouteCell = 16f;             // the search grid's square
        const int PropHalfCells = 15;           // squares each side of the walker (31 x 31)
        const int PropGridN = PropHalfCells * 2 + 1;
        const float PropPlanAhead = 220f;       // how far along its heading the walker aims its route
        const float PropRouteHold = 0.4f;       // seconds a route holds before it's planned again
        const int PropPlansPerTick = 12;        // searches a tick may run; the rest turn the old way (PropTurn)

        sealed class PropRouteMem
        {
            public float Until, Planned;
            public V2 Wp, Goal;
            public bool Active;
        }

        readonly Dictionary<int, PropRouteMem> propRoutes = new Dictionary<int, PropRouteMem>();
        readonly List<int> propRouteGone = new List<int>();
        readonly byte[] prState = new byte[PropGridN * PropGridN];  // 0 unknown, 1 open, 2 blocked
        readonly int[] prFrom = new int[PropGridN * PropGridN];     // -1 unseen; else the square it was reached from
        readonly int[] prQueue = new int[PropGridN * PropGridN];
        readonly List<Prop> routeNear = new List<Prop>();
        readonly List<V2> prPath = new List<V2>();
        int propPlansLeft = PropPlansPerTick;  // topped up each enemy tick (TickEnemies)
        static readonly int[] PrDx = { 1, -1, 0, 0, 1, 1, -1, -1 }, PrDy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// Does the straight line a→b, for something of radius r, run into a crate or barrel (near: the props round it)?
        static bool PropLineBlocked(List<Prop> near, V2 a, V2 b, float r)
        {
            float len = a.DistanceTo(b);
            int steps = Math.Max(1, (int)Math.Ceiling(len / 8f));
            for (int i = 1; i <= steps; i++)
            {
                V2 pt = a + (b - a) * (i / (float)steps);
                foreach (var p in near)
                    if (Pushable(p) && PropCovers(p, pt, r))
                        return true;
            }
            return false;
        }

        bool PropClear(List<Prop> near, LayoutDef layout, V2 a, V2 b, float r)
            => !PropLineBlocked(near, a, b, r) && (layout == null || !SegBlocked(layout, a, b, r));

        /// The way round the crates and barrels for enemy e heading along dir (r: its padded radius). Returns false
        /// when it has no route to give (nothing in the way, or no way round, or no searches left this tick).
        bool PropRouteDir(Enemy e, V2 dir, float r, bool blockedAhead, out V2 outDir)
        {
            outDir = dir;
            PropRouteMem m;
            propRoutes.TryGetValue(e.Id, out m);
            var layout = LayoutOn ? SteerLayout : null;
            V2 goal = e.Pos + dir * PropPlanAhead;
            var near = PropsNear(e.Pos, PropHalfCells * RouteCell + r, routeNear);
            bool following = m != null && m.Active && e.T < m.Until + 2f && m.Goal.DistanceTo(goal) < 90f;
            if (!blockedAhead && !(following && !PropClear(near, layout, e.Pos, goal, r)))
            {
                if (m != null)
                    m.Active = false;
                return false;
            }
            // A route still good: the same goal, its waypoint not reached and still in sight.
            if (following && e.T < m.Until && e.T >= m.Planned && e.Pos.FartherThan(m.Wp, 10f) && PropClear(near, layout, e.Pos, m.Wp, r))
            {
                outDir = (m.Wp - e.Pos).Normalized();
                return true;
            }
            if (propPlansLeft <= 0)
                return false;
            propPlansLeft--;
            V2 wp;
            if (!PlanPropRoute(near, layout, e.Pos, goal, r, out wp))
            {
                if (m != null)
                    m.Active = false;
                return false;
            }
            if (m == null)
            {
                if (propRoutes.Count > 200)
                    PrunePropRoutes();
                propRoutes[e.Id] = m = new PropRouteMem();
            }
            m.Active = true;
            m.Wp = wp;
            m.Goal = goal;
            m.Planned = e.T;
            m.Until = e.T + PropRouteHold;
            outDir = (wp - e.Pos).Normalized();
            return true;
        }

        void PrunePropRoutes()
        {
            propRouteGone.Clear();
            foreach (var k in propRoutes.Keys)
                if (!Enemies.ContainsKey(k))
                    propRouteGone.Add(k);
            foreach (var k in propRouteGone)
                propRoutes.Remove(k);
        }

        /// Breadth-first search (8 ways) on the grid round from, for the way to goal (or, if goal can't be reached,
        /// to the square nearest it). wp: the farthest point of that path in straight sight. False when there's no
        /// square nearer the goal than from to go to.
        bool PlanPropRoute(List<Prop> near, LayoutDef layout, V2 from, V2 goal, float r, out V2 wp)
        {
            wp = from;
            V2 origin = from - new V2(PropHalfCells * RouteCell, PropHalfCells * RouteCell);
            Array.Clear(prState, 0, prState.Length);
            for (int i = 0; i < prFrom.Length; i++)
                prFrom[i] = -1;
            // The crates and barrels, padded by r, mark their squares blocked.
            foreach (var p in near)
            {
                if (!Pushable(p))
                    continue;
                float hw = HalfW(p) + r, hh = HalfH(p) + r;
                int x0 = Math.Max(0, (int)Math.Ceiling((p.Pos.X - hw - origin.X) / RouteCell - 0.5f));
                int x1 = Math.Min(PropGridN - 1, (int)Math.Floor((p.Pos.X + hw - origin.X) / RouteCell - 0.5f));
                int y0 = Math.Max(0, (int)Math.Ceiling((p.Pos.Y - hh - origin.Y) / RouteCell - 0.5f));
                int y1 = Math.Min(PropGridN - 1, (int)Math.Floor((p.Pos.Y + hh - origin.Y) / RouteCell - 0.5f));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (PropCovers(p, origin + new V2((x + 0.5f) * RouteCell, (y + 0.5f) * RouteCell), r))
                            prState[y * PropGridN + x] = 2;
            }
            int start = PropHalfCells * PropGridN + PropHalfCells;
            prState[start] = 1;  // (where it stands is open, even pressed against a crate)
            prFrom[start] = start;
            V2 lo = ArenaMin + new V2(r, r), hi = ArenaMax - new V2(r, r);
            int head = 0, tail = 0, best = start;
            float bestD = from.DistanceTo(goal);
            prQueue[tail++] = start;
            while (head < tail)
            {
                int c = prQueue[head++];
                int cx = c % PropGridN, cy = c / PropGridN;
                V2 cp = origin + new V2((cx + 0.5f) * RouteCell, (cy + 0.5f) * RouteCell);
                float d = cp.DistanceTo(goal);
                if (d < bestD - 0.01f)
                {
                    bestD = d;
                    best = c;
                    if (d < RouteCell)
                        break;
                }
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + PrDx[k], ny = cy + PrDy[k];
                    if (nx < 0 || ny < 0 || nx >= PropGridN || ny >= PropGridN)
                        continue;
                    int n = ny * PropGridN + nx;
                    if (prFrom[n] >= 0 || prState[n] == 2)
                        continue;
                    if (k >= 4 && (prState[cy * PropGridN + nx] == 2 || prState[ny * PropGridN + cx] == 2))
                        continue;  // no cutting a crate's corner
                    if (prState[n] == 0)
                    {
                        V2 np = origin + new V2((nx + 0.5f) * RouteCell, (ny + 0.5f) * RouteCell);
                        bool open = np.X >= lo.X && np.Y >= lo.Y && np.X <= hi.X && np.Y <= hi.Y
                            && (layout == null || !SegBlocked(layout, cp, np, r));
                        prState[n] = (byte)(open ? 1 : 2);
                        if (!open)
                            continue;
                    }
                    prFrom[n] = c;
                    prQueue[tail++] = n;
                }
            }
            if (best == start)
                return false;
            prPath.Clear();
            for (int c = best; c != start; c = prFrom[c])
                prPath.Add(origin + new V2((c % PropGridN + 0.5f) * RouteCell, (c / PropGridN + 0.5f) * RouteCell));
            // prPath runs goal → start: the first point in straight sight is the farthest along.
            for (int i = 0; i < prPath.Count; i++)
                if (PropClear(near, layout, from, prPath[i], r))
                {
                    wp = prPath[i];
                    return true;
                }
            wp = prPath[prPath.Count - 1];
            return true;
        }
    }
}
