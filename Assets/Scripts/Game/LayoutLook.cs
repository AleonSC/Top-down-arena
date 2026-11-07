using UnityEngine;

namespace Arena
{
    /// The look of the arena's floor plans (Layouts.cs, LayoutRules.cs): steel walls and pillars, the edge wall with
    /// its teal player gates and red holographic enemy gates, the Bunker's hold point, and the HUD chip.
    public partial class ArenaApp
    {
        // Phosphor vector look (Docs/Mockups/arena-layouts-digital.html, style A, cyan palette): glowing outlines with a
        // faint fill and diagonal hatching, dashed pillar cores, teal chevrons and flickering red enemy gates.
        static readonly Rgba Ink = new Rgba(0.373f, 0.941f, 1f), GateTeal = new Rgba(0.247f, 0.839f, 0.769f), HoloRed = new Rgba(1f, 0.353f, 0.29f);

        /// Under the props: walls and pillars (they rise out of the floor when a new cycle starts), then the edge gates.
        void DrawLayout(float t)
        {
            var d = game.ActiveLayout;
            if (d == null)
                return;
            float rise = Mathf.Clamp01(game.LayoutAge / 0.9f), a = Mathf.Clamp01(game.LayoutAge / 0.35f);
            float lift = (1f - rise * rise) * 22f;
            if (d.HasHold)
                DrawHoldPoint(d.HoldPos, d.HoldR, t, a);
            foreach (var s in d.Solids)
            {
                if (s.Edge)
                {
                    if (!game.Walled)
                        DrawSolid(s, 0f, true, a);
                    continue;
                }
                DrawSolid(s, lift, false, a);
            }
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
                Circle(c, s.R, Ink.WithAlpha(0.10f * a), 24);
                HatchCircle(c, s.R, Ink.WithAlpha(0.22f * a));
                Arc(c, s.R, lw + 8f, Ink.WithAlpha(0.10f * a), 28);
                Arc(c, s.R, lw + 4f, Ink.WithAlpha(0.18f * a), 28);
                Arc(c, s.R, lw, Ink.WithAlpha(a), 28);
                int n = Mathf.Max(6, Mathf.RoundToInt(MathX.Tau * s.R * 0.55f / 10f));
                for (int i = 0; i < n; i++)
                    Arc(c, s.R * 0.55f, 1.5f, Ink.WithAlpha(0.7f * a), 3, MathX.Tau * i / n, MathX.Tau / n * 0.5f);
                Line(c - new V2(s.R * 0.3f, 0f), c + new V2(s.R * 0.3f, 0f), 1.5f, Ink.WithAlpha(0.7f * a));
                Line(c - new V2(0f, s.R * 0.3f), c + new V2(0f, s.R * 0.3f), 1.5f, Ink.WithAlpha(0.7f * a));
                return;
            }
            float x = c.X - s.HW, y = c.Y - s.HH, w = s.HW * 2f, h = s.HH * 2f;
            FillRect(x, y, w, h, Ink.WithAlpha(0.10f * a));
            HatchRect(x, y, w, h, Ink.WithAlpha(0.22f * a));
            OutlineRect(x, y, w, h, lw + 8f, Ink.WithAlpha(0.10f * a));
            OutlineRect(x, y, w, h, lw + 4f, Ink.WithAlpha(0.18f * a));
            OutlineRect(x, y, w, h, lw, Ink.WithAlpha(a));
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

        /// The gaps (teal chevrons pointing into the arena, bracketed) and the holograms (red, flickering, dashed,
        /// brighter while something is stepping out of them).
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
                float burst = 0f;
                foreach (var e in game.Enemies.Values)  // something stepping out of it
                    if (e.SpawnT > 0f && Mathf.Abs(alongX ? e.Pos.X - g.Mid : e.Pos.Y - g.Mid) < g.Len / 2f + 20f && (g.Side < 2 ? Mathf.Abs(e.Pos.Y - cy) : Mathf.Abs(e.Pos.X - cx)) < 110f)
                        burst = Mathf.Max(burst, e.SpawnT);
                float f = 0.55f + 0.35f * Mathf.Sin(t * 9f + gi * 1.7f) - (Mathf.Sin(t * 37f + gi) > 0.93f ? 0.3f : 0f);
                f = Mathf.Clamp01(Mathf.Max(0.15f, f) + 0.4f * burst) * a;
                FillRect(x - 8f, y - 8f, w + 16f, h + 16f, HoloRed.WithAlpha(0.10f * f));
                FillRect(x, y, w, h, HoloRed.WithAlpha(0.35f * f));
                float ph = t * 30f;
                V2 p00 = new V2(x + 1f, y + 1f), p10 = new V2(x + w - 1f, y + 1f), p11 = new V2(x + w - 1f, y + h - 1f), p01 = new V2(x + 1f, y + h - 1f);
                PhosDash(p00, p10, 8f, 6f, ph, 2f, HoloRed.WithAlpha(f));
                PhosDash(p10, p11, 8f, 6f, ph + w, 2f, HoloRed.WithAlpha(f));
                PhosDash(p11, p01, 8f, 6f, ph + w + h, 2f, HoloRed.WithAlpha(f));
                PhosDash(p01, p00, 8f, 6f, ph + 2f * w + h, 2f, HoloRed.WithAlpha(f));
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

        /// Under the path chip: the floor plan's name.
        float DrawLayoutChip(float x, float y)
        {
            var d = game.ActiveLayout;
            if (d == null)
                return 0f;
            string text = d.Name.ToUpperInvariant();
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var accent = ToColor(GateTeal);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), accent);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, accent, TextAnchor.MiddleLeft, true);
            return 24f;
        }
    }
}
