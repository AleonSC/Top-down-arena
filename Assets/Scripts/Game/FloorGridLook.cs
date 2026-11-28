using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The arena floor's grid, the "Pressure grid" (mockup https://claude.ai/artifact/4N3zygv8yPS7mpCG8X4cQM): the 50 px (RoomTraps.Tile, world-anchored: traps snap to it)
    /// lines bow away from each player like a stretched sheet and trail a little behind them while they move, and
    /// light up in the player's class colour near them; a kill's ripple runs through it (Juice.cs `Warp`). Your Dodge sends a
    /// small burst ripple from where it started (`FloorDashBurst`).
    /// Settings > Visual effects > Arena floor: Bending (the default) or Static (the lines stay straight, only the colour shows),
    /// and the pattern: this grid or one of the others in FloorStylesLook.cs.
    /// With Visual effects off it draws the plain grid, as before.
    public partial class ArenaApp
    {
        bool floorBend = true;  // Settings > Visual effects > Arena floor (pref "floor_bend"; previews: FLOORBEND=0)
        public static bool PreviewFloorStatic;  // the preview's FLOORBEND=0
        public static float PreviewDashBurst = -1f;  // the preview's DASHBURST=<s>: a dash burst that old

        const float FloorViewPad = 96f;  // DrawFloorGrid's cut round the view: more than a line ever bends
        const float FloorStep = RoomTraps.Tile, FloorSeg = 16f, FloorR = 120f, FloorPush = 9f, FloorDrag = 0.035f;

        struct FloorPull { public V2 Pos, Vel; public Rgba Col; }
        readonly List<FloorPull> floorPulls = new List<FloorPull>();
        readonly Dictionary<int, V2> floorLast = new Dictionary<int, V2>(), floorVel = new Dictionary<int, V2>();
        Game floorDodgeGame;
        int floorDodges;  // the dodges your game had counted when the floor last looked (Game.Dodges)
        const float DashBurstPower = 0.7f, DashBurstLife = 0.4f;
        int floorFrame = -1;

        bool FloorBends => Juicy && floorBend && !PreviewFloorStatic;

        /// Who's on the floor this frame, with a smoothed velocity from how far each moved since the last frame.
        void GatherFloorPulls()
        {
            if (floorFrame == Time.frameCount)
                return;
            floorFrame = Time.frameCount;
            floorPulls.Clear();
            if (!Juicy)
                return;
            if (PreviewDashBurst >= 0f)  // (by the player highest up the screen, clear of the HUD)
            {
                Player top = null;
                foreach (var p in game.Players.Values)
                    if (top == null || p.Disp.Y < top.Disp.Y)
                        top = p;
                if (top != null)
                    ripples.Add(new Ripple { Pos = top.Disp, T = PreviewDashBurst, Power = DashBurstPower, Life = DashBurstLife, Col = top.Def.Color });
                PreviewDashBurst = -1f;
            }
            FloorDashBurst();
            float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            foreach (var p in game.Players.Values)
            {
                V2 v = V2.Zero, last;
                if (floorLast.TryGetValue(p.Id, out last))
                {
                    V2 raw = (p.Disp - last) / dt;
                    if (raw.Length > 2000f)
                        raw = V2.Zero;  // a teleport or respawn, not a move
                    floorVel.TryGetValue(p.Id, out v);
                    v += (raw - v) * Mathf.Min(1f, dt * 12f);
                }
                floorLast[p.Id] = p.Disp;
                floorVel[p.Id] = v;
                floorPulls.Add(new FloorPull { Pos = p.Disp, Vel = game.Paused ? V2.Zero : v, Col = p.Def.Color });
            }
        }

        /// Your Dodge sends a small, short ripple through the grid from where it started (your own dodges only: the
        /// Dodge isn't sent to other players).
        void FloorDashBurst()
        {
            bool fresh = floorDodgeGame != game;  // (a new game counts from 0 again: no burst for that)
            if (!fresh && game.Dodges == floorDodges)
                return;
            floorDodgeGame = game;
            floorDodges = game.Dodges;
            if (fresh)
                return;
            var me = game.Me;
            if (me != null)
                ripples.Add(new Ripple { Pos = game.DodgeFrom, Power = DashBurstPower, Life = DashBurstLife, Col = me.Def.Color });  // Juice.cs
        }

        /// A floor point moved by the players (when bending) and by kill ripples.
        V2 FloorWarp(float x, float y)
        {
            V2 p = Juicy ? Warp(x, y) : new V2(x, y);
            if (!FloorBends)
                return p;
            V2 off = V2.Zero;
            foreach (var f in floorPulls)
            {
                float dx = x - f.Pos.X, dy = y - f.Pos.Y, d2 = dx * dx + dy * dy;
                if (d2 > 9f * FloorR * FloorR)
                    continue;
                float k = Mathf.Exp(-d2 / (FloorR * FloorR)), d = Mathf.Sqrt(d2);
                V2 dir = d > 0.5f ? new V2(dx / d, dy / d) : V2.Zero;
                off += (dir * FloorPush - f.Vel * FloorDrag) * k;
            }
            return p + off;
        }

        /// The grid inside lo..hi, lines starting at first (then every 64 px).
        void DrawFloorGrid(V2 lo, V2 hi, V2 first, Rgba grid)
        {
            GatherFloorPulls();
            V2 areaLo = lo, areaHi = hi;  // (the whole area, before the cut to the view below)
            bool warpAll = Juicy && ripples.Count > 0, bend = FloorBends;
            float reach = FloorR * 2.4f;
            if (CamZoomed)
            {
                // Following you round a big room (Rooms, Survival): only the lines in view, cut on the same grid and
                // segment marks, so what shows is drawn exactly as before (a ripple bent every line of the whole room).
                V2 vh = ViewHalf();  // (the turned screen's reach, CameraRotate.cs)
                float vx = vh.X + FloorViewPad, vy = vh.Y + FloorViewPad;
                float x0 = camFocus.X - vx, x1 = camFocus.X + vx, y0 = camFocus.Y - vy, y1 = camFocus.Y + vy;
                if (x0 > first.X)
                    first.X += Mathf.Floor((x0 - first.X) / FloorStep) * FloorStep;
                if (y0 > first.Y)
                    first.Y += Mathf.Floor((y0 - first.Y) / FloorStep) * FloorStep;
                if (x0 > lo.X)
                    lo.X += Mathf.Floor((x0 - lo.X) / FloorSeg) * FloorSeg;
                if (y0 > lo.Y)
                    lo.Y += Mathf.Floor((y0 - lo.Y) / FloorSeg) * FloorSeg;
                hi = new V2(Mathf.Min(hi.X, x1), Mathf.Min(hi.Y, y1));
                if (lo.X >= hi.X || lo.Y >= hi.Y)
                    return;
            }
            if (Juicy && FloorStyle != 0)  // Flux, Seismo, Plates or Circuit (FloorStylesLook.cs)
            {
                DrawFloorPattern(areaLo, areaHi, lo, hi, grid);
                return;
            }
            for (float x = first.X; x <= hi.X + 0.5f; x += FloorStep)
            {
                bool near = warpAll || bend && NearFloorPull(x, true, reach);
                if (!near)
                    Line(new V2(x, lo.Y), new V2(x, hi.Y), 1f, grid);
                else
                    for (float y = lo.Y; y < hi.Y; y += FloorSeg)
                        Line(FloorWarp(x, y), FloorWarp(x, Mathf.Min(y + FloorSeg, hi.Y)), 1f, grid);
            }
            for (float y = first.Y; y <= hi.Y + 0.5f; y += FloorStep)
            {
                bool near = warpAll || bend && NearFloorPull(y, false, reach);
                if (!near)
                    Line(new V2(lo.X, y), new V2(hi.X, y), 1f, grid);
                else
                    for (float x = lo.X; x < hi.X; x += FloorSeg)
                        Line(FloorWarp(x, y), FloorWarp(Mathf.Min(x + FloorSeg, hi.X), y), 1f, grid);
            }
            DrawFloorGlow(lo, hi, first, grid);
        }

        bool NearFloorPull(float at, bool vertical, float reach)
        {
            foreach (var f in floorPulls)
                if (Mathf.Abs((vertical ? f.Pos.X : f.Pos.Y) - at) < reach)
                    return true;
            return false;
        }

        /// Near each player the lines and crossings take their colour, strongest right by them.
        void DrawFloorGlow(V2 lo, V2 hi, V2 first, Rgba grid)
        {
            if (!Juicy)
                return;
            DrawDashGlow(lo, hi, first);
            float reach = FloorR * 1.6f;
            var ink = new Rgba(grid.R, grid.G, grid.B, 1f);
            foreach (var f in floorPulls)
            {
                float x0 = Mathf.Max(lo.X, f.Pos.X - reach), x1 = Mathf.Min(hi.X, f.Pos.X + reach);
                float y0 = Mathf.Max(lo.Y, f.Pos.Y - reach), y1 = Mathf.Min(hi.Y, f.Pos.Y + reach);
                if (x0 >= x1 || y0 >= y1)
                    continue;
                var col = ink.Mix(f.Col, 0.55f);
                float gx0 = first.X + Mathf.Ceil((x0 - first.X) / FloorStep) * FloorStep;
                float gy0 = first.Y + Mathf.Ceil((y0 - first.Y) / FloorStep) * FloorStep;
                float sx0 = lo.X + Mathf.Floor((x0 - lo.X) / FloorSeg) * FloorSeg;
                float sy0 = lo.Y + Mathf.Floor((y0 - lo.Y) / FloorSeg) * FloorSeg;
                for (float x = gx0; x <= x1 + 0.5f; x += FloorStep)
                    for (float y = sy0; y < y1; y += FloorSeg)
                        FloorGlowSeg(new V2(x, y), new V2(x, Mathf.Min(y + FloorSeg, hi.Y)), f.Pos, col);
                for (float y = gy0; y <= y1 + 0.5f; y += FloorStep)
                    for (float x = sx0; x < x1; x += FloorSeg)
                        FloorGlowSeg(new V2(x, y), new V2(Mathf.Min(x + FloorSeg, hi.X), y), f.Pos, col);
                for (float x = gx0; x <= x1 + 0.5f; x += FloorStep)  // the crossings: small lit squares
                    for (float y = gy0; y <= y1 + 0.5f; y += FloorStep)
                    {
                        float k = FloorFalloff(new V2(x, y), f.Pos);
                        if (k < 0.04f)
                            continue;
                        V2 c = FloorWarp(x, y);
                        FillRect(c.X - 1.5f, c.Y - 1.5f, 3f, 3f, col.WithAlpha(0.7f * k));
                    }
            }
        }

        /// A dash burst's front lights the lines it crosses in the dasher's colour, fading as it spreads.
        void DrawDashGlow(V2 lo, V2 hi, V2 first)
        {
            foreach (var r in ripples)
            {
                if (r.Life <= 0f || r.Col.A <= 0f)
                    continue;
                float front = r.T * 700f, fade = 1f - r.T / r.Life, reach = front + 50f;
                if (fade <= 0f)
                    continue;
                float x0 = Mathf.Max(lo.X, r.Pos.X - reach), x1 = Mathf.Min(hi.X, r.Pos.X + reach);
                float y0 = Mathf.Max(lo.Y, r.Pos.Y - reach), y1 = Mathf.Min(hi.Y, r.Pos.Y + reach);
                if (x0 >= x1 || y0 >= y1)
                    continue;
                float gx0 = first.X + Mathf.Ceil((x0 - first.X) / FloorStep) * FloorStep;
                float gy0 = first.Y + Mathf.Ceil((y0 - first.Y) / FloorStep) * FloorStep;
                float sx0 = lo.X + Mathf.Floor((x0 - lo.X) / FloorSeg) * FloorSeg;
                float sy0 = lo.Y + Mathf.Floor((y0 - lo.Y) / FloorSeg) * FloorSeg;
                for (float x = gx0; x <= x1 + 0.5f; x += FloorStep)
                    for (float y = sy0; y < y1; y += FloorSeg)
                        DashGlowSeg(new V2(x, y), new V2(x, Mathf.Min(y + FloorSeg, hi.Y)), r, front, fade);
                for (float y = gy0; y <= y1 + 0.5f; y += FloorStep)
                    for (float x = sx0; x < x1; x += FloorSeg)
                        DashGlowSeg(new V2(x, y), new V2(Mathf.Min(x + FloorSeg, hi.X), y), r, front, fade);
            }
        }

        void DashGlowSeg(V2 a, V2 b, Ripple r, float front, float fade)
        {
            float band = 1f - Mathf.Abs(((a + b) * 0.5f - r.Pos).Length - front) / 50f;
            if (band <= 0f)
                return;
            Line(FloorWarp(a.X, a.Y), FloorWarp(b.X, b.Y), 1.5f, r.Col.WithAlpha(0.55f * band * fade));
        }

        void FloorGlowSeg(V2 a, V2 b, V2 who, Rgba col)
        {
            float k = FloorFalloff((a + b) * 0.5f, who);
            if (k < 0.04f)
                return;
            Line(FloorWarp(a.X, a.Y), FloorWarp(b.X, b.Y), 1f, col.WithAlpha(0.32f * k));
        }

        static float FloorFalloff(V2 at, V2 who)
        {
            float d = (at - who).Length / (FloorR * 1.6f);
            return d >= 1f ? 0f : (1f - d) * (1f - d);
        }
    }
}
