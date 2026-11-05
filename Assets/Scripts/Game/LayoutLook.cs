using UnityEngine;

namespace Arena
{
    /// The look of the arena's floor plans (Layouts.cs, LayoutRules.cs): steel walls and pillars, the edge wall with
    /// its teal player gates and red holographic enemy gates, the Bunker's hold point, and the HUD chip.
    public partial class ArenaApp
    {
        static readonly Rgba WallBody = new Rgba(0.33f, 0.4f, 0.45f), WallTop = new Rgba(0.6f, 0.72f, 0.79f), WallDark = new Rgba(0.14f, 0.18f, 0.21f);
        static readonly Rgba GateTeal = new Rgba(0.25f, 0.84f, 0.77f), HoloRed = new Rgba(1f, 0.35f, 0.29f), HoldGold = new Rgba(0.9f, 0.83f, 0.42f);

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
                        DrawEdgeWall(s, a);
                    continue;
                }
                if (s.Round)
                {
                    Circle(s.Pos + new V2(4f, 6f), s.R, new Rgba(0f, 0f, 0f, 0.32f * a), 20);
                    Circle(s.Pos - new V2(0f, lift), s.R, WallBody.WithAlpha(a), 22);
                    Arc(s.Pos - new V2(0f, lift), s.R - 1.5f, 3f, WallTop.WithAlpha(0.9f * a), 22);
                    Circle(s.Pos - new V2(0f, lift), s.R * 0.42f, WallDark.WithAlpha(0.6f * a), 14);
                    continue;
                }
                float x = s.Pos.X - s.HW, y = s.Pos.Y - s.HH - lift, w = s.HW * 2f, h = s.HH * 2f;
                FillRect(x + 4f, s.Pos.Y - s.HH + 6f, w, h, new Rgba(0f, 0f, 0f, 0.32f * a));
                FillRect(x, y, w, h, WallBody.WithAlpha(a));
                FillRect(x, y, w, Mathf.Min(4f, h), WallTop.WithAlpha(0.9f * a));          // lit top edge
                FillRect(x, y + h - Mathf.Min(4f, h), w, Mathf.Min(4f, h), WallDark.WithAlpha(0.75f * a));  // shaded foot
                // Rivets along the long side.
                bool horizontal = w >= h;
                float len = horizontal ? w : h;
                for (float u = 14f; u < len - 8f; u += 30f)
                    Circle(horizontal ? new V2(x + u, y + h / 2f) : new V2(x + w / 2f, y + u), 1.8f, WallDark.WithAlpha(0.7f * a), 6);
            }
            if (game.EdgesGated)
                DrawEdgeGates(d, t, a);
        }

        /// A stretch of the wall round the arena: dark steel with a bright inner edge.
        void DrawEdgeWall(Solid s, float a)
        {
            float x = s.Pos.X - s.HW, y = s.Pos.Y - s.HH, w = s.HW * 2f, h = s.HH * 2f;
            FillRect(x, y, w, h, WallDark.WithAlpha(0.96f * a));
            bool horizontal = w >= h;
            if (horizontal)
                FillRect(x, s.Pos.Y < Game.Height / 2f ? y + h - 3f : y, w, 3f, WallTop.WithAlpha(0.55f * a));
            else
                FillRect(s.Pos.X < Game.Width / 2f ? x + w - 3f : x, y, 3f, h, WallTop.WithAlpha(0.55f * a));
        }

        /// The gaps (teal chevrons pointing through: walk out and come back in opposite) and the holograms (red,
        /// scanning, brighter while something is stepping out of them).
        void DrawEdgeGates(LayoutDef d, float t, float a)
        {
            foreach (var g in d.PlayerGates)
            {
                V2 mid = EdgeAt(g.Side, g.Mid, 12f), dir = EdgeIn(g.Side);
                bool alongX = g.Side < 2;
                V2 p0 = alongX ? new V2(g.From, mid.Y) : new V2(mid.X, g.From), p1 = alongX ? new V2(g.To, mid.Y) : new V2(mid.X, g.To);
                float pulse = 0.55f + 0.25f * Mathf.Sin(t * 3f + g.Side);
                Line(p0, p1, 2.5f, GateTeal.WithAlpha(0.7f * a));
                // Two chevrons pointing out through the gap.
                V2 across = alongX ? new V2(1f, 0f) : new V2(0f, 1f);
                for (int k = 0; k < 2; k++)
                {
                    V2 tip = mid - dir * (k * 14f - 8f);
                    Line(tip - across * 12f + dir * 9f, tip, 2.5f, GateTeal.WithAlpha(pulse * a * (1f - 0.4f * k)));
                    Line(tip + across * 12f + dir * 9f, tip, 2.5f, GateTeal.WithAlpha(pulse * a * (1f - 0.4f * k)));
                }
            }
            foreach (var g in d.EnemyGates)
            {
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
                float flick = 0.8f + 0.2f * Mathf.Sin(t * 23f + g.From) * Mathf.Sin(t * 7.1f);
                FillRect(x, y, w, h, HoloRed.WithAlpha((0.22f + 0.4f * burst) * flick * a));
                // Scan lines drifting along the gate.
                float len = alongX ? w : h;
                for (float u = (t * 40f) % 14f; u < len; u += 14f)
                    Line(alongX ? new V2(x + u, y) : new V2(x, y + u), alongX ? new V2(x + u, y + h) : new V2(x + w, y + u), 1.2f, HoloRed.WithAlpha(0.3f * a));
                // Bright rim on the arena side and chevrons pointing in.
                V2 dirIn = EdgeIn(g.Side);
                V2 rimA = alongX ? new V2(x, g.Side == 0 ? y + h : y) : new V2(g.Side == 2 ? x + w : x, y);
                V2 rimB = alongX ? new V2(x + w, rimA.Y) : new V2(rimA.X, y + h);
                Line(rimA, rimB, 2.5f, HoloRed.WithAlpha((0.8f + 0.2f * burst) * a));
                V2 across = alongX ? new V2(1f, 0f) : new V2(0f, 1f);
                for (float u = 24f; u < len - 12f; u += 46f)
                {
                    V2 at = (alongX ? new V2(x + u, cy) : new V2(cx, y + u)) + dirIn * (3f * Mathf.Sin(t * 4f - u * 0.05f));
                    Line(at - across * 7f - dirIn * 4f, at + dirIn * 4f, 2f, HoloRed.WithAlpha(0.75f * a));
                    Line(at + across * 7f - dirIn * 4f, at + dirIn * 4f, 2f, HoloRed.WithAlpha(0.75f * a));
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
            for (int i = 0; i < 16; i++)
                Arc(at, r, 2.5f, HoldGold.WithAlpha(0.7f * a), 3, MathX.Tau * i / 16f + t * 0.15f, MathX.Tau / 32f);
            Circle(at, r - 3f, HoldGold.WithAlpha(0.05f * a), 28);
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
