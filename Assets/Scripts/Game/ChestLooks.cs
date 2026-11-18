using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// MOCKUP: six chest kinds, each a holo chest in its own ink with its own emblem, trim and spill
    /// (Gold, Vector, Upgrade, Relic, Room card, Buildables). Not wired to the game yet: only the preview
    /// (CHESTLOOKS=1, Tools/Preview/ShotChests.cs) fills MockChestLooks.
    public partial class ArenaApp
    {
        public enum ChestLook : byte { Gold, Vector, Upgrade, Relic, RoomCard, Buildable }
        internal static readonly Dictionary<int, ChestLook> MockChestLooks = new Dictionary<int, ChestLook>();

        static readonly Rgba[] ChestLookInk =
        {
            new Rgba(1f, 0.81f, 0.35f),   // Gold
            new Rgba(0.3f, 0.88f, 1f),    // Vector (VecCyan)
            new Rgba(0.4f, 1f, 0.5f),     // Upgrade
            new Rgba(0.78f, 0.5f, 1f),    // Relic
            new Rgba(1f, 0.55f, 0.3f),    // Room card
            new Rgba(1f, 0.4f, 0.72f),    // Buildables (Black Market)
        };

        void DrawChestLook(Prop p, V2 c, float a, float t, ChestLook look)
        {
            float hw = Game.ChestW / 2f, hh = Game.ChestH / 2f;
            var ink = ChestLookInk[(int)look];
            var col = ink.WithAlpha(a);
            var hot = ink.Mix(new Rgba(1f, 1f, 1f), 0.6f).WithAlpha(a);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f + (int)look);
            float open = p.Open ? Mathf.Clamp01(p.OpenT / 0.25f) : 0f;
            if (!p.Open)
                Circle(c, hw + 14f + 4f * pulse, ink.WithAlpha((0.08f + 0.06f * pulse) * a), 28);
            else
                for (int i = 0; i < 4; i++)  // light pours up in its ink
                {
                    float w = hw * (1.1f - i * 0.22f), up = 70f + i * 20f;
                    SetColor(ink.Mix(new Rgba(1f, 1f, 1f), 0.3f).WithAlpha(0.1f * a));
                    Quad(new V2(c.X - hw * 0.8f, c.Y - hh + 4f), new V2(c.X + hw * 0.8f, c.Y - hh + 4f), new V2(c.X + w, c.Y - hh - up), new V2(c.X - w, c.Y - hh - up));
                }

            // Body: faint fill and scanlines.
            var fill = ink.WithAlpha(0.09f * a);
            FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, fill);
            if (!fxReduced)
                for (float y = -hh + 1f; y < hh; y += 3f)
                    FillRect(c.X - hw, c.Y + y, hw * 2f, 1f, ink.WithAlpha(0.06f * a));
            if (look == ChestLook.Buildable)  // blueprint grid
                for (float gx = -hw + 8f; gx < hw; gx += 8f)
                    FillRect(c.X + gx - 0.5f, c.Y - hh, 1f, hh * 2f, ink.WithAlpha(0.22f * a));
            if (p.Open)
                FillRect(c.X - hw + 5f, c.Y - hh + 4f, hw * 2f - 10f, hh - 2f, hot.WithAlpha(0.55f * a));

            // Outline: Relic has cut corners, the rest a box.
            float ch = look == ChestLook.Relic ? 7f : 0f;
            V2[] o =
            {
                new V2(c.X - hw + ch, c.Y - hh), new V2(c.X + hw - ch, c.Y - hh), new V2(c.X + hw, c.Y - hh + ch), new V2(c.X + hw, c.Y + hh - ch),
                new V2(c.X + hw - ch, c.Y + hh), new V2(c.X - hw + ch, c.Y + hh), new V2(c.X - hw, c.Y + hh - ch), new V2(c.X - hw, c.Y - hh + ch),
            };
            for (int i = 0; i < 8; i++)
                if (ch > 0f || i % 2 == 0)
                    GlowLine(o[i], o[(i + 1) % 8], 1.5f, col);
            if (ch == 0f)
                for (int i = 1; i < 8; i += 2)
                    GlowLine(o[i], o[(i + 1) % 8], 1.5f, col);

            // Lid: the seam, or lifted away as an outline.
            if (p.Open)
            {
                float ly = c.Y - hh - 18f * open, lh = hh * 0.9f;
                var lid = col.WithAlpha(0.7f * a);
                GlowLine(new V2(c.X - hw, ly), new V2(c.X + hw, ly), 1.2f, lid);
                GlowLine(new V2(c.X + hw, ly), new V2(c.X + hw, ly + lh), 1.2f, lid);
                GlowLine(new V2(c.X + hw, ly + lh), new V2(c.X - hw, ly + lh), 1.2f, lid);
                GlowLine(new V2(c.X - hw, ly + lh), new V2(c.X - hw, ly), 1.2f, lid);
            }
            else
                GlowLine(new V2(c.X - hw, c.Y - hh * 0.15f), new V2(c.X + hw, c.Y - hh * 0.15f), 1.5f, col);

            // Trim: each kind its own.
            switch (look)
            {
                case ChestLook.Gold:  // two straps
                    GlowLine(new V2(c.X - hw * 0.55f, c.Y - hh), new V2(c.X - hw * 0.55f, c.Y + hh), 1f, col);
                    GlowLine(new V2(c.X + hw * 0.55f, c.Y - hh), new V2(c.X + hw * 0.55f, c.Y + hh), 1f, col);
                    break;
                case ChestLook.Vector:  // chevrons along the base
                    for (int s = -1; s <= 1; s += 2)
                        for (int k = 0; k < 2; k++)
                        {
                            float x = c.X + s * (hw - 6f - k * 5f), y = c.Y + hh - 6f;
                            GlowLine(new V2(x - s * 2.5f, y - 3f), new V2(x + s * 0.5f, y), 1f, col);
                            GlowLine(new V2(x + s * 0.5f, y), new V2(x - s * 2.5f, y + 3f), 1f, col);
                        }
                    break;
                case ChestLook.Upgrade:  // up ticks on both sides
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float x = c.X + s * hw * 0.7f;
                        GlowLine(new V2(x, c.Y + hh - 3f), new V2(x, c.Y - hh * 0.15f + 4f), 1f, col);
                        GlowLine(new V2(x - 3f, c.Y - hh * 0.15f + 7f), new V2(x, c.Y - hh * 0.15f + 4f), 1f, col);
                        GlowLine(new V2(x + 3f, c.Y - hh * 0.15f + 7f), new V2(x, c.Y - hh * 0.15f + 4f), 1f, col);
                    }
                    break;
                case ChestLook.Relic:  // rune dots in the cut corners
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i & 1) != 0 ? 1f : -1f, sy = (i & 2) != 0 ? 1f : -1f;
                        Circle(c + new V2(sx * (hw - 7f), sy * (hh - 6f)), 1.4f, hot, 6);
                    }
                    break;
                case ChestLook.RoomCard:  // card edges stacked on the lid
                    for (int k = 0; k < 3; k++)
                        GlowLine(new V2(c.X - hw + 6f + k * 2f, c.Y - hh - 3f - k * 2.5f), new V2(c.X + hw - 6f - k * 2f, c.Y - hh - 3f - k * 2.5f), 0.9f, col.WithAlpha((0.75f - 0.2f * k) * a));
                    break;
                case ChestLook.Buildable:  // hazard ticks on the base
                    for (float x = -hw + 4f; x < hw - 4f; x += 6f)
                        GlowLine(new V2(c.X + x, c.Y + hh - 1.5f), new V2(c.X + x + 3f, c.Y + hh - 5f), 1f, col.WithAlpha(0.8f * a));
                    break;
            }

            // Corner brackets (Relic's are its cut corners).
            if (look != ChestLook.Relic)
            {
                const float L = 9f, e = 1.4f;
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i & 1) != 0 ? 1f : -1f, sy = (i & 2) != 0 ? 1f : -1f;
                    V2 corner = c + new V2(sx * hw, sy * hh);
                    GlowLine(corner + new V2(sx * e, 0f), corner + new V2(-sx * L, 0f), 2.8f, col);
                    GlowLine(corner + new V2(0f, sy * e), corner + new V2(0f, -sy * L), 2.8f, col);
                }
            }

            if (!p.Open)
            {
                // The emblem plate on the seam.
                V2 m = c + new V2(0f, 1f);
                SetColor(ink.WithAlpha(0.22f * a));
                Quad(m + new V2(-8f, -8f), m + new V2(8f, -8f), m + new V2(8f, 8f), m + new V2(-8f, 8f));
                ChestEmblem(look, m, 1f, col, hot, a);
            }
            else
            {
                // What's inside rises out of the light.
                float k = Mathf.Clamp01((p.OpenT - 0.1f) / 0.6f);
                for (int i = 0; i < 3; i++)
                {
                    float kk = Mathf.Clamp01(k * 1.4f - i * 0.2f);
                    if (kk <= 0f) continue;
                    V2 at = c + new V2((i - 1) * 13f, -hh - 14f - 34f * kk - (i == 1 ? 10f : 0f));
                    ChestEmblem(look, at, i == 1 ? 1.1f : 0.75f, col.WithAlpha(a * Mathf.Min(1f, kk * 2f)), hot.WithAlpha(a * Mathf.Min(1f, kk * 2f)), a);
                }
            }
        }

        /// Each chest kind's mark, centred on m, scaled by s.
        void ChestEmblem(ChestLook look, V2 m, float s, Rgba col, Rgba hot, float a)
        {
            switch (look)
            {
                case ChestLook.Gold:  // a coin
                    GlowArc(m, 5.5f * s, 1.3f, col, 16);
                    Circle(m, 3.6f * s, col.WithAlpha(0.35f * col.A), 12);
                    GlowLine(m + new V2(0f, -2.6f) * s, m + new V2(0f, 2.6f) * s, 1.3f, hot);
                    break;
                case ChestLook.Vector:  // the vector arrow
                {
                    V2 f = m + new V2(-5f, 5f) * s, b = m + new V2(5f, -5f) * s;
                    GlowLine(f, b, 1.5f, hot);
                    GlowLine(b, b + new V2(-5f, 0f) * s, 1.5f, hot);
                    GlowLine(b, b + new V2(0f, 5f) * s, 1.5f, hot);
                    break;
                }
                case ChestLook.Upgrade:  // two stacked up chevrons
                    for (int k = 0; k < 2; k++)
                    {
                        V2 tip = m + new V2(0f, (-4f + k * 5f) * s);
                        GlowLine(tip + new V2(-5f, 4f) * s, tip, 1.5f, k == 0 ? hot : col);
                        GlowLine(tip, tip + new V2(5f, 4f) * s, 1.5f, k == 0 ? hot : col);
                    }
                    break;
                case ChestLook.Relic:  // a faceted gem
                {
                    V2 tp = m + new V2(0f, -6f) * s, l = m + new V2(-5.5f, -1.5f) * s, r = m + new V2(5.5f, -1.5f) * s, bt = m + new V2(0f, 6.5f) * s;
                    SetColor(col.WithAlpha(0.35f * col.A));
                    Quad(tp, r, bt, l);
                    GlowLine(tp, r, 1.2f, hot); GlowLine(r, bt, 1.2f, hot); GlowLine(bt, l, 1.2f, hot); GlowLine(l, tp, 1.2f, hot);
                    GlowLine(l, r, 0.9f, col);
                    GlowLine(m + new V2(-2f, -1.5f) * s, bt, 0.8f, col);
                    GlowLine(m + new V2(2f, -1.5f) * s, bt, 0.8f, col);
                    break;
                }
                case ChestLook.RoomCard:  // a card with a two-room plan
                {
                    float w = 5f * s, h = 7f * s;
                    GlowLine(m + new V2(-w, -h), m + new V2(w, -h), 1.2f, col);
                    GlowLine(m + new V2(w, -h), m + new V2(w, h), 1.2f, col);
                    GlowLine(m + new V2(w, h), m + new V2(-w, h), 1.2f, col);
                    GlowLine(m + new V2(-w, h), m + new V2(-w, -h), 1.2f, col);
                    var room = hot.WithAlpha(0.85f * hot.A);  // a 2 x 2 floor plan
                    FillRect(m.X - 3.2f * s, m.Y - 4.6f * s, 2.8f * s, 4f * s, room);
                    FillRect(m.X + 0.4f * s, m.Y - 4.6f * s, 2.8f * s, 4f * s, room);
                    FillRect(m.X - 3.2f * s, m.Y + 0.6f * s, 6.4f * s, 4f * s, room);
                    break;
                }
                case ChestLook.Buildable:  // a wrench
                {
                    V2 head = m + new V2(3f, -3f) * s, tail = m + new V2(-5f, 5f) * s;
                    GlowLine(tail, head, 2f, hot);
                    Arc(head, 3.2f * s, 1.6f, hot, 12, -0.4f, 4.4f);
                    Circle(tail, 1.3f * s, hot, 6);
                    break;
                }
            }
        }
    }
}
