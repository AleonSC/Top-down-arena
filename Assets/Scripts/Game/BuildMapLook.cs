using UnityEngine;

namespace Arena
{
    /// The build window's map as a "Holo floor" (mockup: claude.ai artifact "Detailed Build Map", style C): each room's floor
    /// with its tile grid, the world's walls and fins as glowing bars cut at the doors, and the cover (crates, barriers,
    /// pillars, the hub's floor plan) as lit blocks, so the map shows the arena you fight in. Called from DrawExpansionDraw.
    public partial class ArenaApp
    {
        static readonly Rgba HoloWallInk = new Rgba(0.32f, 0.89f, 0.95f), HoloCoverInk = new Rgba(0.48f, 0.85f, 1f);

        /// A room's tile grid: a line every tile (100 px) once they're 6 px apart, every half tile once those are 10 px apart.
        void DrawHoloGrid(RoomArea a, System.Func<V2, V2> M, float k, Rgba ink)
        {
            float step = 50f * k >= 10f ? 50f : 100f * k >= 6f ? 100f : 0f;
            if (step <= 0f)
                return;
            V2 p0 = M(a.Min), p1 = M(a.Max);
            var line = ink.WithAlpha(0.13f);
            for (float gx = Mathf.Ceil((a.Min.X + 1f) / step) * step; gx < a.Max.X; gx += step)
                RoomMapRect(M(new V2(gx, 0f)).X, p0.Y, 1f, p1.Y - p0.Y, line);
            for (float gy = Mathf.Ceil((a.Min.Y + 1f) / step) * step; gy < a.Max.Y; gy += step)
                RoomMapRect(p0.X, M(new V2(0f, gy)).Y, p1.X - p0.X, 1f, line);
        }

        /// The walls and fins (World.WallsFor: cut at the doors) and the cover, over the floors.
        void DrawHoloSolids(World wd, System.Func<V2, V2> M, float k, Rect map)
        {
            foreach (var s in wd.WallsFor(false))
                HoloBox(s, M, k, HoloWallInk, true);
            foreach (var s in wd.Cover)
                HoloSolid(s, M, k, map);
            if (game.Layout != LayoutId.Open)  // this cycle's floor plan in the hub (its edge wall is the world's)
                foreach (var s in Layouts.Get(game.Layout).Solids)
                    if (!s.Edge)
                        HoloSolid(s, M, k, map);
        }

        void HoloSolid(Solid s, System.Func<V2, V2> M, float k, Rect map)
        {
            if (!s.Round)
            {
                HoloBox(s, M, k, HoloCoverInk, false);
                return;
            }
            V2 c = M(s.Pos);
            float r = Mathf.Max(2.5f, s.R * k);
            if (c.X - r - 2f < map.x || c.X + r + 2f > map.xMax || c.Y - r - 2f < map.y || c.Y + r + 2f > map.yMax)
                return;  // (discs aren't clipped: one half off the map waits until it's in view)
            int segs = Mathf.Clamp((int)(r * 1.5f), 12, 40);
            Circle(c, r + 1.5f, HoloCoverInk.WithAlpha(0.18f), segs);
            Circle(c, r, HoloCoverInk.WithAlpha(0.95f), segs);
            if (r > 3.5f)
                Circle(c, r - 1f, new Rgba(0.1f, 0.24f, 0.32f, 0.95f), segs);
        }

        /// A box solid: a soft glow round it, then the bar (walls) or a lit block with a bright edge (cover).
        void HoloBox(Solid s, System.Func<V2, V2> M, float k, Rgba ink, bool wall)
        {
            V2 p0 = M(s.Pos - new V2(s.HW, s.HH)), p1 = M(s.Pos + new V2(s.HW, s.HH));
            float min = wall ? 2f : 4f;
            if (p1.X - p0.X < min) { float c = (p0.X + p1.X) / 2f; p0 = new V2(c - min / 2f, p0.Y); p1 = new V2(c + min / 2f, p1.Y); }
            if (p1.Y - p0.Y < min) { float c = (p0.Y + p1.Y) / 2f; p0 = new V2(p0.X, c - min / 2f); p1 = new V2(p1.X, c + min / 2f); }
            float w = p1.X - p0.X, h = p1.Y - p0.Y;
            RoomMapRect(p0.X - 2f, p0.Y - 2f, w + 4f, h + 4f, ink.WithAlpha(0.14f));
            if (wall)
            {
                RoomMapRect(p0.X, p0.Y, w, h, ink.WithAlpha(0.95f));
                return;
            }
            RoomMapRect(p0.X, p0.Y, w, h, ink.WithAlpha(0.35f));
            RoomMapFrame(p0.X, p0.Y, w, h, 1f, ink.WithAlpha(0.95f));
        }

        static readonly Rgba HoloBreachInk = new Rgba(0.92f, 0.27f, 0.31f), HoloGateInk = new Rgba(1f, 0.42f, 0.24f);

        /// This wave's breaches: a pulsing red dashed bar in the wall and three chevrons pointing in (where enemies come from).
        void DrawHoloBreaches(System.Func<V2, V2> M, float k, Rect map)
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var b in game.ActiveBreaches)
            {
                V2 c = M(b.Pos);
                if (!map.Contains(new Vector2(c.X, c.Y)))
                    continue;
                float half = Mathf.Max(8f, Breaches.Len / 2f * k), th = Mathf.Max(4f, 18f * k);
                V2 al = b.Along, inw = b.In;
                Line(c - al * half, c + al * half, th + 3f, new Rgba(0.02f, 0.03f, 0.08f, 1f));  // the wall broken open
                Line(c - al * half, c + al * half, th + 8f, HoloBreachInk.WithAlpha(0.15f * pulse));  // its glow
                for (float s = -half; s < half; s += 7f)  // dashes along the wall
                {
                    V2 d0 = c + al * s, d1 = c + al * Mathf.Min(s + 4f, half);
                    Line(d0, d1, th, HoloBreachInk.WithAlpha(0.6f + 0.4f * pulse));
                }
                float sz = Mathf.Clamp(14f * k * 3f, 4f, 7f), gap = Mathf.Max(8f, 55f * k);
                for (int i = -1; i <= 1; i++)  // chevrons just inside, pointing into the room
                {
                    V2 tip = c + al * (i * gap) + inw * Mathf.Max(7f, 40f * k);
                    Line(tip - inw * sz - al * sz, tip, 2f, HoloBreachInk.WithAlpha(pulse));
                    Line(tip - inw * sz + al * sz, tip, 2f, HoloBreachInk.WithAlpha(pulse));
                }
            }
        }

        /// The shutter gates fitted: a laser grid across the door between two posts, bright while shut, faint while open.
        void DrawHoloGates(World wd, System.Func<V2, V2> M, float k)
        {
            for (int i = 0; i < game.GateFits.Count; i++)
            {
                var gf = game.GateFits[i];
                float w = Rooms.DoorW;
                foreach (var d in wd.Doors)
                    if ((d.Pos - gf.Pos).Length < 1f) { w = d.W; break; }
                bool shut = game.GateShut(i) > 0f;
                V2 c = M(gf.Pos), al = gf.Vertical ? new V2(0f, 1f) : new V2(1f, 0f), ac = new V2(al.Y, al.X);
                float half = Mathf.Max(6f, w / 2f * k);
                var ink = HoloGateInk.WithAlpha(shut ? 0.95f : 0.4f);
                for (int l = -2; l <= 2; l++)
                    Line(c - al * half + ac * (l * 2.2f), c + al * half + ac * (l * 2.2f), 1f, ink);
                Line(c - al * half - ac * 6f, c - al * half + ac * 6f, 3f, HoloGateInk);  // the posts
                Line(c + al * half - ac * 6f, c + al * half + ac * 6f, 3f, HoloGateInk);
            }
        }
    }
}
