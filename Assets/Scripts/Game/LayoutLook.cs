using UnityEngine;

namespace Arena
{
    /// The look of the arena's floor plans (Layouts.cs, LayoutRules.cs): steel walls and pillars, the edge wall with
    /// its teal player gates and enemy gates (drawn as plain wall), the Bunker's hold point, and the HUD chip.
    public partial class ArenaApp
    {
        // Phosphor vector look (Docs/Mockups/arena-layouts-digital.html, style A, cyan palette): glowing outlines with a
        // faint fill and diagonal hatching, dashed pillar cores, teal chevrons, enemy gates drawn as plain wall.
        static readonly Rgba Cyan = new Rgba(0.373f, 0.941f, 1f), GateTeal = new Rgba(0.247f, 0.839f, 0.769f);
        Rgba Ink = Cyan, WallFill = Cyan, NeonCore = Cyan;  // NeonCore: the white-hot line down the middle of each outline
        static readonly float[] EdgePasses = { 27.5f, 14.5f, 6.5f, 1.5f, 0.75f };  // this frame's outline and fill/hatch colours (WallInk)

        public static EnemyType? WallBossOverride;  // previews: the boss the walls are themed for

        /// Each boss's wall colour: picked to suit its faction rather than its body colour (several bosses are pink).
        static Rgba BossWallInk(EnemyType b)
        {
            switch (b)
            {
                case EnemyType.Boss: return new Rgba(1f, 0.55f, 0.12f);         // the Warden: sodium prison lamps
                case EnemyType.HiveQueen: return new Rgba(1f, 0.88f, 0.15f);    // honey
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                case EnemyType.DualReaper: return new Rgba(1f, 0.3f, 0.9f); // dusk
                case EnemyType.Singularity: return new Rgba(0.68f, 0.35f, 1f);   // void violet
                case EnemyType.Colossus: return new Rgba(1f, 0.72f, 0.38f);  // sandstone
                case EnemyType.Duelist: return new Rgba(0.9f, 0.92f, 1f);       // silver
                case EnemyType.Lich: return new Rgba(0.25f, 1f, 0.5f);          // grave green
                case EnemyType.StormHerald: return new Rgba(0.35f, 0.58f, 1f);   // storm blue
                case EnemyType.Devourer: return new Rgba(1f, 0.2f, 0.35f);     // raw red
                case EnemyType.Eclipse: return new Rgba(1f, 0.84f, 0.3f);      // corona gold
                default: return Cyan;
            }
        }

        Rgba wallInkNow = Cyan;  // eased toward the target so walls that stay up (the Rooms hub) shift colour gently

        /// This frame's Ink (outlines) and WallFill (fill and hatching), themed for the boss this cycle leads to.
        void WallInk()
        {
            var b = WallBossOverride ?? game.ApproachBossAt(Mathf.Max(1, game.Wave));
            var target = BossWallInk(b);
            float k = WallBossOverride != null ? 1f : Mathf.Clamp01(Time.deltaTime * 2f);
            wallInkNow = wallInkNow.Mix(target, game.LayoutAge < 0.9f ? 1f : k);
            Ink = WallFill = wallInkNow;
            NeonCore = Ink.Mix(new Rgba(1f, 1f, 1f), 0.7f);
            PropHoloCyan = Ink;
            PropHoloBlue = Ink.Mix(new Rgba(1f, 1f, 1f), 0.3f);
        }

        /// Under the props: walls and pillars (they rise out of the floor when a new cycle starts), then the edge gates.
        void DrawLayout(float t)
        {
            WallInk();  // also the crates' and pots' colour (PropsLook.cs), so before the open-arena return
            var d = game.ActiveLayout;
            if (d == null)
                return;
            float rise = Mathf.Clamp01(game.LayoutAge / 0.9f), a = Mathf.Clamp01(game.LayoutAge / 0.35f);
            float lift = (1f - rise * rise) * 22f;
            if (d.HasHold)
                DrawHoldPoint(d.HoldPos, d.HoldR, t, a);
            foreach (var s in d.Solids)  // discs one by one; boxes merged below so corners and crossings read as one solid
                if (s.Round && (!s.Edge || !game.Walled))
                    DrawSolid(s, s.Edge ? 0f : lift, s.Edge, a);
            if (!game.Walled)
                DrawSolidUnion(SolidUnion.For(d.Solids, true, ref edgeUnion), 0f, true, a);
            DrawSolidUnion(SolidUnion.For(d.Solids, false, ref wallUnion), lift, false, a);
            if (game.EdgesGated)
                DrawEdgeGates(d, t, a);
        }

        /// One wall, pillar or stretch of edge wall: tinted fill, hatching, glowing outline.
        void DrawSolid(Solid s, float lift, bool edge, float a)
        {
            V2 c = s.Pos - new V2(0f, lift);
            float lw = edge ? 1.5f : 2.5f;
            if (s.Round)
            {
                Circle(c, s.R, WallFill.WithAlpha(0.13f * a), 24);
                HatchCircle(c, s.R, WallFill.WithAlpha(0.30f * a));
                Arc(c, s.R, lw + 26f, Ink.WithAlpha(0.07f * a), 28);
                Arc(c, s.R, lw + 13f, Ink.WithAlpha(0.16f * a), 28);
                Arc(c, s.R, lw + 5f, Ink.WithAlpha(0.40f * a), 28);
                Arc(c, s.R, lw, Ink.WithAlpha(a), 28);
                Arc(c, s.R, lw * 0.5f, NeonCore.WithAlpha(a), 28);
                int n = Mathf.Max(6, Mathf.RoundToInt(MathX.Tau * s.R * 0.55f / 10f));
                for (int i = 0; i < n; i++)
                    Arc(c, s.R * 0.55f, 1.5f, Ink.WithAlpha(0.7f * a), 3, MathX.Tau * i / n, MathX.Tau / n * 0.5f);
                Line(c - new V2(s.R * 0.3f, 0f), c + new V2(s.R * 0.3f, 0f), 1.5f, Ink.WithAlpha(0.7f * a));
                Line(c - new V2(0f, s.R * 0.3f), c + new V2(0f, s.R * 0.3f), 1.5f, Ink.WithAlpha(0.7f * a));
                return;
            }
            float x = c.X - s.HW, y = c.Y - s.HH, w = s.HW * 2f, h = s.HH * 2f;
            FillRect(x, y, w, h, WallFill.WithAlpha(0.13f * a));
            HatchRect(x, y, w, h, WallFill.WithAlpha(0.30f * a));
            OutlineRect(x, y, w, h, lw + 26f, Ink.WithAlpha(0.07f * a));
            OutlineRect(x, y, w, h, lw + 13f, Ink.WithAlpha(0.16f * a));
            OutlineRect(x, y, w, h, lw + 5f, Ink.WithAlpha(0.40f * a));
            OutlineRect(x, y, w, h, lw, Ink.WithAlpha(a));
            OutlineRect(x, y, w, h, lw * 0.5f, NeonCore.WithAlpha(a));
        }

        SolidUnion wallUnion, edgeUnion;  // cached: rebuilt only when the boxes change

        /// The boxes as one solid: each spot filled and hatched once (the hatching lined up across them), and the
        /// glowing outline only round the outside, so corners, T-joins and crossings have no seams.
        void DrawSolidUnion(SolidUnion u, float lift, bool edge, float a)
        {
            if (u == null || u.Fills.Length == 0)
                return;
            float lw = edge ? 1.5f : 2.5f;
            var f = u.Fills;
            for (int i = 0; i < f.Length; i += 4)
                FillRect(f[i], f[i + 1] - lift, f[i + 2], f[i + 3], WallFill.WithAlpha(0.13f * a));
            for (int i = 0; i < f.Length; i += 4)
                HatchWorld(f[i], f[i + 1], f[i + 2], f[i + 3], lift, WallFill.WithAlpha(0.30f * a));
            UnionOutline(u, lift, lw + 26f, Ink.WithAlpha(0.07f * a));
            UnionOutline(u, lift, lw + 13f, Ink.WithAlpha(0.16f * a));
            UnionOutline(u, lift, lw + 5f, Ink.WithAlpha(0.40f * a));
            UnionOutline(u, lift, lw, Ink.WithAlpha(a));
            UnionOutline(u, lift, lw * 0.5f, NeonCore.WithAlpha(a));
        }

        /// The outside edges; the horizontal ones run half a width past their ends to square off every corner.
        void UnionOutline(SolidUnion u, float lift, float width, Rgba c)
        {
            float e = width * 0.5f;
            var g = u.Edges;
            for (int i = 0; i < g.Length; i += 4)
            {
                bool flat = g[i + 1] == g[i + 3];
                float ext = flat ? e : 0f;
                Line(new V2(g[i] - ext, g[i + 1] - lift), new V2(g[i + 2] + ext, g[i + 3] - lift), width, c);
            }
        }

        /// 45 degree lines every 12 px on a world-fixed grid (x + y a multiple of 12), clipped to the box, so boxes
        /// side by side hatch as one.
        void HatchWorld(float x, float y, float w, float h, float lift, Rgba c)
        {
            float c0 = Mathf.Ceil((x + y) / 12f) * 12f, c1 = x + w + y + h;
            for (float k = c0; k < c1; k += 12f)
            {
                float x0 = Mathf.Max(x, k - (y + h)), x1 = Mathf.Min(x + w, k - y);
                if (x1 - x0 < 0.35f)
                    continue;
                Line(new V2(x0, k - x0 - lift), new V2(x1, k - x1 - lift), 1.5f, c);
            }
        }

        void OutlineRect(float x, float y, float w, float h, float width, Rgba c)
        {
            float e = width * 0.5f;
            Line(new V2(x - e, y), new V2(x + w + e, y), width, c);
            Line(new V2(x - e, y + h), new V2(x + w + e, y + h), width, c);
            Line(new V2(x, y), new V2(x, y + h), width, c);
            Line(new V2(x + w, y), new V2(x + w, y + h), width, c);
        }

        /// 45 degree lines every 12 px, clipped to the box.
        void HatchRect(float x, float y, float w, float h, Rgba c)
        {
            for (float k = -h; k < w; k += 12f)
            {
                float u0 = Mathf.Max(0f, -k), u1 = Mathf.Min(h, w - k);
                if (u1 - u0 < 0.5f)
                    continue;
                Line(new V2(x + k + u0, y + h - u0), new V2(x + k + u1, y + h - u1), 1.5f, c);
            }
        }

        void HatchCircle(V2 c, float r, Rgba col)
        {
            float bx = c.X - r, by = c.Y - r, bh = r * 2f;
            for (float k = -bh; k < bh; k += 12f)
            {
                float px = bx + k - c.X, py = by + bh - c.Y;
                float b = px - py, disc = b * b - 2f * (px * px + py * py - r * r);
                if (disc <= 0f)
                    continue;
                float sq = Mathf.Sqrt(disc), u0 = (-b - sq) / 2f, u1 = (-b + sq) / 2f;
                Line(new V2(bx + k + u0, by + bh - u0), new V2(bx + k + u1, by + bh - u1), 1.5f, col);
            }
        }

        /// A dashed segment; phase shifts the dashes along it.
        void PhosDash(V2 a, V2 b, float dash, float gap, float phase, float width, Rgba c)
        {
            float len = (b - a).Length;
            if (len < 0.01f)
                return;
            V2 dir = (b - a) * (1f / len);
            float period = dash + gap, at = -(((phase % period) + period) % period);
            for (; at < len; at += period)
            {
                float s0 = Mathf.Max(0f, at), s1 = Mathf.Min(len, at + dash);
                if (s1 > s0)
                    Line(a + dir * s0, a + dir * s1, width, c);
            }
        }

        /// The gaps (teal chevrons pointing into the arena, bracketed) and the enemy gates (drawn as plain wall).
        void DrawEdgeGates(LayoutDef d, float t, float a)
        {
            foreach (var g in d.PlayerGates)
            {
                bool alongX = g.Side < 2;
                V2 mid = EdgeAt(g.Side, g.Mid, Layouts.EdgeT / 2f), dir = EdgeIn(g.Side);
                V2 across = alongX ? new V2(1f, 0f) : new V2(0f, 1f);
                int n = Mathf.FloorToInt(g.Len / 44f);
                for (int i = 0; i < n; i++)
                {
                    V2 cp = mid + across * ((i - (n - 1) * 0.5f) * 44f);
                    V2 tip = cp + dir * 5f;
                    for (int pass = 0; pass < 2; pass++)  // glow, then the line
                    {
                        var col = GateTeal.WithAlpha((pass == 0 ? 0.2f : 1f) * a);
                        float wd = pass == 0 ? 8f : 3f;
                        Line(cp - across * 9f - dir * 5f, tip, wd, col);
                        Line(cp + across * 9f - dir * 5f, tip, wd, col);
                    }
                }
                V2 e0 = alongX ? new V2(g.From, mid.Y) : new V2(mid.X, g.From), e1 = alongX ? new V2(g.To, mid.Y) : new V2(mid.X, g.To);
                V2 thick = dir * (Layouts.EdgeT / 2f);
                Line(e0 - thick, e0 + thick, 2f, GateTeal.WithAlpha(a));
                Line(e1 - thick, e1 + thick, 2f, GateTeal.WithAlpha(a));
            }
            for (int gi = 0; gi < d.EnemyGates.Length; gi++)
            {
                var g = d.EnemyGates[gi];
                if (!d.SpawnSide[g.Side])
                    continue;
                bool alongX = g.Side < 2;
                float thick = Layouts.EdgeT;
                float cx = g.Side == 2 ? thick / 2f : g.Side == 3 ? Game.Width - thick / 2f : g.Mid, cy = g.Side == 0 ? thick / 2f : g.Side == 1 ? Game.Height - thick / 2f : g.Mid;
                float w = alongX ? g.Len : thick, h = alongX ? thick : g.Len;
                float x = cx - w / 2f, y = cy - h / 2f;
                // Drawn as plain edge wall (red is the breaches' colour; enemies show where they come in with portals):
                // fill and world-aligned hatching like its neighbours, the long sides outlined, no end caps, so it joins them.
                FillRect(x, y, w, h, WallFill.WithAlpha(0.13f * a));
                HatchWorld(x, y, w, h, 0f, WallFill.WithAlpha(0.30f * a));
                V2 s0 = new V2(x, y), s1 = alongX ? new V2(x + w, y) : new V2(x, y + h);
                V2 q0 = alongX ? new V2(x, y + h) : new V2(x + w, y), q1 = new V2(x + w, y + h);
                foreach (var pass in EdgePasses)
                {
                    var col = (pass < 1f ? NeonCore : Ink).WithAlpha((pass > 20f ? 0.07f : pass > 10f ? 0.16f : pass > 5f ? 0.40f : 1f) * a);
                    Line(s0, s1, pass, col);
                    Line(q0, q1, pass, col);
                }
            }
        }

        /// The point on an edge (along = x for top and bottom, y for left and right), in from it by inset.
        static V2 EdgeAt(int side, float along, float inset)
        {
            return side == 0 ? new V2(along, inset) : side == 1 ? new V2(along, Game.Height - inset) : side == 2 ? new V2(inset, along) : new V2(Game.Width - inset, along);
        }

        /// Which way is into the arena from an edge.
        static V2 EdgeIn(int side)
        {
            return side == 0 ? new V2(0f, 1f) : side == 1 ? new V2(0f, -1f) : side == 2 ? new V2(1f, 0f) : new V2(-1f, 0f);
        }

        void DrawHoldPoint(V2 at, float r, float t, float a)
        {
            int n = 24;
            for (int i = 0; i < n; i++)
            {
                float st = MathX.Tau * i / n;
                Arc(at, r, 6f, Ink.WithAlpha(0.15f * a), 3, st, MathX.Tau / n * 0.55f);
                Arc(at, r, 2f, Ink.WithAlpha(a), 3, st, MathX.Tau / n * 0.55f);
            }
            Line(at - new V2(14f, 0f), at + new V2(14f, 0f), 2f, Ink.WithAlpha(a));
            Line(at - new V2(0f, 14f), at + new V2(0f, 14f), 2f, Ink.WithAlpha(a));
            float s3 = Mathf.Sin(t * 3f);
            Arc(at, r + 10f + 4f * s3, 2f, Ink.WithAlpha((0.5f + 0.3f * s3) * a), 40);
        }

    }
}
