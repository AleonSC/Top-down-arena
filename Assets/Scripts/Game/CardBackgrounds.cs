using UnityEngine;

namespace Arena
{
    /// The card backgrounds a rank unlocks (Rank.cs, CosmeticKind.CardBackground): the fill of a lobby party card, behind its
    /// portrait and text. Mockup: Docs/Mockups/card-backgrounds.html. Drawing only: the lobby entry carries the number.
    /// Every one is drawn inside a clipped group (so nothing spills past the card), then a left-to-right scrim goes over it so the
    /// name and badges read the same on all of them. With Visual effects off each one is its still self (the clock held at 0).
    /// Built from IMGUI fills only (rects, discs, rotated rects as lines), like FrameLooks.cs.
    public partial class ArenaApp
    {
        const float CardBgInset = 3f;  // keeps the card's own rim visible round the fill

        /// Draws background `index` (Rank.CardBgIndex; 0 draws nothing) into card rect r. Call after the card's panel, before its content.
        void DrawCardBackground(Rect r, int index, float t)
        {
            if (index <= 0)
                return;
            string id = Rank.CardBgId(index);
            if (id == "")
                return;
            if (!fxOn)
                t = 0f;
            var inner = new Rect(r.x + CardBgInset, r.y + CardBgInset, r.width - 2f * CardBgInset, r.height - 2f * CardBgInset);
            float w = inner.width, h = inner.height;
            GUI.BeginGroup(inner);
            switch (id)
            {
                case "card-dusk": CardDusk(w, h, t); break;
                case "card-stars": CardStars(w, h, t); break;
                case "card-blueprint": CardBlueprint(w, h, t); break;
                case "card-embers": CardEmbers(w, h, t); break;
                case "card-aurora": CardAurora(w, h, t); break;
                case "card-circuit": CardCircuit(w, h, t); break;
                case "card-rain": CardRain(w, h, t); break;
                case "card-void": CardVoid(w, h, t); break;
                case "card-foil": CardFoil(w, h, t); break;
                case "card-prism": CardPrism(w, h, t); break;
            }
            // The scrim: dark behind the portrait and name, lighter on the right.
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float f = i / (float)(steps - 1), a = f < 0.55f ? Mathf.Lerp(0.88f, 0.55f, f / 0.55f) : Mathf.Lerp(0.55f, 0.15f, (f - 0.55f) / 0.45f);
                Ui.Fill(new Rect(w * i / steps, 0f, w / steps + 1f, h), new Color(0.024f, 0.035f, 0.063f, a));
            }
            GUI.EndGroup();
        }

        // --- Drawing helpers (local to the clipped group) ---------------------------------------------------------

        static void CardGradient(float w, float h, Color top, Color bottom)
        {
            const int n = 14;
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(0f, h * i / n, w, h / n + 1f), Color.Lerp(top, bottom, i / (float)(n - 1)));
        }

        /// A straight line of the given thickness (a rotated fill).
        static void CardLine(Vector2 a, Vector2 b, float thick, Color c)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.01f)
                return;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Ui.Fill(new Rect(a.x, a.y - thick / 2f, len, thick), c);
            GUI.matrix = old;
        }

        /// A square turned by `deg` degrees round its centre (a shard).
        static void CardDiamond(float cx, float cy, float half, float deg, Color c)
        {
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(deg, new Vector2(cx, cy));
            Ui.Fill(new Rect(cx - half, cy - half, half * 2f, half * 2f), c);
            GUI.matrix = old;
        }

        static float Wrap(float v, float m) => ((v % m) + m) % m;

        // --- The ten ----------------------------------------------------------------------------------------------

        /// Rank 6: a perspective grid sliding toward you under a violet horizon.
        static void CardDusk(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.09f, 0.07f, 0.18f), new Color(0.05f, 0.04f, 0.11f));
            float hz = h * 0.42f;
            var line = new Color(0.75f, 0.55f, 1f, 0.35f);
            for (int i = -8; i <= 8; i++)
                CardLine(new Vector2(w / 2f + i * 8f, hz), new Vector2(w / 2f + i * w * 0.12f, h), 1f, line);
            for (int k = 0; k < 7; k++)
            {
                float f = Wrap(k + t * 0.35f, 7f) / 7f, y = hz + (h - hz) * f * f;
                Ui.Fill(new Rect(0f, y, w, 1f), new Color(0.75f, 0.55f, 1f, 0.15f + 0.5f * f));
            }
            Ui.Fill(new Rect(0f, hz - 1f, w, 2f), new Color(1f, 0.59f, 0.82f, 0.18f));
        }

        /// Rank 10: three layers of drifting stars, and now and then a shooting star.
        static void CardStars(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.03f, 0.04f, 0.1f), new Color(0.06f, 0.1f, 0.2f));
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < 26; i++)
                {
                    float x = Wrap(FrameHash(i + layer * 40) * w - t * (layer + 1) * 9f, w), y = FrameHash(i * 3 + layer * 17) * h;
                    float tw = 0.55f + 0.45f * Mathf.Sin(t * 2f + i), s = 0.8f + layer * 0.6f;
                    Ui.Fill(new Rect(x, y, s, s), new Color(0.86f, 0.9f, 1f, (0.25f + layer * 0.22f) * tw));
                }
            float p = Wrap(t * 0.22f, 1f);
            if (p < 0.18f && t > 0f)
            {
                float q = p / 0.18f, x = w * 0.9f - q * w * 0.5f, y = h * 0.1f + q * h * 0.45f;
                CardLine(new Vector2(x, y), new Vector2(x + 30f, y - 12f), 1.4f, new Color(1f, 1f, 1f, 1f - q));
            }
        }

        /// Rank 13: cyan drafting paper, a grid, a dimension line and a circle that traces itself.
        static void CardBlueprint(float w, float h, float t)
        {
            Ui.Fill(new Rect(0f, 0f, w, h), new Color(0.055f, 0.165f, 0.29f));
            var grid = new Color(0.47f, 0.75f, 1f, 0.14f);
            for (float x = 0f; x < w; x += 12f) Ui.Fill(new Rect(x, 0f, 1f, h), grid);
            for (float y = 0f; y < h; y += 12f) Ui.Fill(new Rect(0f, y, w, 1f), grid);
            var ink = new Color(0.59f, 0.82f, 1f, 0.45f);
            var c = new Vector2(w * 0.8f, h * 0.5f);
            float sweep = t > 0f ? Wrap(t * 0.25f, 1f) * Mathf.PI * 2f : Mathf.PI * 1.5f;
            for (float a = 0f; a < sweep; a += 0.2f)
                CardLine(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 26f, c + new Vector2(Mathf.Cos(a + 0.2f), Mathf.Sin(a + 0.2f)) * 26f, 1f, ink);
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.4f)
                CardLine(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f, c + new Vector2(Mathf.Cos(a + 0.4f), Mathf.Sin(a + 0.4f)) * 14f, 1f, ink);
            Ui.Fill(new Rect(w * 0.45f, h - 12f, w * 0.55f - 14f, 1f), ink);
            Ui.Fill(new Rect(w * 0.45f, h - 16f, 1f, 8f), ink);
            Ui.Fill(new Rect(w - 14f, h - 16f, 1f, 8f), ink);
        }

        /// Rank 19: a warm glow along the bottom edge, with embers lifting off it.
        static void CardEmbers(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.06f, 0.04f, 0.05f), new Color(0.29f, 0.1f, 0.03f));
            for (int i = 0; i < 22; i++)
            {
                float life = Wrap(t * 0.4f + FrameHash(i), 1f), x = FrameHash(i * 5 + 1) * w + Mathf.Sin(t * 2f + i) * 6f, y = h - life * h * 1.05f;
                FrameDot(x, y, 1f + 1.8f * (1f - life), new Color(1f, 0.59f + 0.35f * (1f - life), 0.24f, (1f - life) * 0.8f));
            }
        }

        /// Rank 23: slow green and violet ribbons.
        static void CardAurora(float w, float h, float t)
        {
            Ui.Fill(new Rect(0f, 0f, w, h), new Color(0.03f, 0.07f, 0.1f));
            var cols = new[] { new Color(0.24f, 1f, 0.67f, 0.10f), new Color(0.47f, 0.43f, 1f, 0.10f), new Color(0.24f, 0.86f, 1f, 0.10f) };
            for (int k = 0; k < 3; k++)
                for (float x = 0f; x < w; x += 6f)
                {
                    float y = h * (0.3f + 0.12f * k) + Mathf.Sin(x * 0.02f + t * 0.6f + k * 2f) * 14f + Mathf.Sin(x * 0.045f - t * 0.4f + k * 2f) * 7f;
                    Ui.Fill(new Rect(x, y, 6f, h - y), cols[k]);
                }
        }

        /// Rank 27: dark green traces with a pulse running along each.
        static void CardCircuit(float w, float h, float t)
        {
            Ui.Fill(new Rect(0f, 0f, w, h), new Color(0.03f, 0.08f, 0.06f));
            var trace = new Color(0.24f, 0.78f, 0.59f, 0.22f);
            for (int i = 0; i < 6; i++)
            {
                float y = 10f + i * (h - 20f) / 5f, x0 = FrameHash(i) * 40f, bend = x0 + 60f + FrameHash(i + 9) * 120f, dy = i % 2 == 1 ? 14f : -14f;
                Ui.Fill(new Rect(0f, y - 0.75f, bend, 1.5f), trace);
                CardLine(new Vector2(bend, y), new Vector2(bend + 14f, y + dy), 1.5f, trace);
                Ui.Fill(new Rect(bend + 14f, y + dy - 0.75f, w - bend - 14f, 1.5f), trace);
                Ui.Fill(new Rect(bend - 2f, y - 2f, 4f, 4f), new Color(0.24f, 0.78f, 0.59f, 0.5f));
                float px = Wrap(t * 60f + i * 70f, w + 40f) - 20f;
                float py = px < bend ? y : px < bend + 14f ? y + dy * (px - bend) / 14f : y + dy;
                FrameDot(px, py, 2f, new Color(0.59f, 1f, 0.86f, 0.9f));
            }
        }

        /// Rank 32: cool grey-blue with thin streaks and the odd bead running down.
        static void CardRain(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.08f, 0.11f, 0.15f), new Color(0.04f, 0.06f, 0.09f));
            var streak = new Color(0.67f, 0.78f, 0.9f, 0.22f);
            for (int i = 0; i < 34; i++)
            {
                float x = FrameHash(i) * w, sp = 0.8f + FrameHash(i + 7) * 0.9f, y = Wrap(t * sp * 140f + FrameHash(i + 3) * h, h + 30f) - 15f;
                CardLine(new Vector2(x, y), new Vector2(x - 3f, y + 14f), 1f, streak);
            }
            for (int i = 0; i < 5; i++)
                FrameDot(FrameHash(i + 30) * w, Wrap(t * 18f + FrameHash(i + 40) * h, h), 2.2f, new Color(0.78f, 0.88f, 1f, 0.35f));
        }

        /// Rank 37: a slow violet vortex behind the portrait.
        static void CardVoid(float w, float h, float t)
        {
            Ui.Fill(new Rect(0f, 0f, w, h), new Color(0.04f, 0.024f, 0.08f));
            var c = new Vector2(w * 0.2f, h * 0.5f);
            for (int ring = 0; ring < 5; ring++)
            {
                var col = new Color(0.59f, 0.35f, 1f, 0.28f - ring * 0.04f);
                Vector2 prev = Vector2.zero;
                bool first = true;
                for (float a = 0f; a < Mathf.PI * 3.2f; a += 0.25f)
                {
                    float rad = 10f + ring * 14f + a * 7f, an = a - t * (0.6f + ring * 0.1f) + ring;
                    var p = c + new Vector2(Mathf.Cos(an) * rad * 1.6f, Mathf.Sin(an) * rad * 0.7f);
                    if (!first)
                        CardLine(prev, p, 1.5f, col);
                    prev = p;
                    first = false;
                }
            }
            for (int k = 3; k >= 1; k--)
                Ui.Pill(new Rect(c.x - 18f * k, c.y - 12f * k, 36f * k, 24f * k), new Color(0.75f, 0.55f, 1f, 0.12f));
        }

        /// Rank 42: brushed gold with a diagonal sheen that sweeps across.
        static void CardFoil(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.23f, 0.16f, 0.03f), new Color(0.18f, 0.125f, 0.024f));
            for (int i = 0; i < 6; i++)
                Ui.Fill(new Rect(w * i / 6f, 0f, w / 6f + 1f, h), new Color(0.42f, 0.31f, 0.07f, Mathf.Sin(i / 5f * Mathf.PI) * 0.7f));
            for (float y = 0f; y < h; y += 3f)
                Ui.Fill(new Rect(0f, y, w, 1f), new Color(1f, 0.88f, 0.55f, 0.05f));
            float sheen = (t > 0f ? Wrap(t * 0.28f, 1.4f) - 0.2f : 0.55f) * (w + 120f) - 60f;
            for (float y = 0f; y < h; y += 3f)
            {
                float cx = sheen + (h - y) * 0.4f;
                Ui.Fill(new Rect(cx - 36f, y, 72f, 3f), new Color(1f, 0.94f, 0.7f, 0.08f));
                Ui.Fill(new Rect(cx - 20f, y, 40f, 3f), new Color(1f, 0.94f, 0.7f, 0.12f));
                Ui.Fill(new Rect(cx - 8f, y, 16f, 3f), new Color(1f, 0.94f, 0.7f, 0.16f));
            }
        }

        /// Rank 55: spectrum shards turning slowly and overlapping.
        static void CardPrism(float w, float h, float t)
        {
            Ui.Fill(new Rect(0f, 0f, w, h), new Color(0.05f, 0.05f, 0.09f));
            for (int i = 0; i < 9; i++)
            {
                float cx = FrameHash(i) * w, cy = FrameHash(i + 5) * h, half = 9f + FrameHash(i + 11) * 14f;
                float deg = (t * (17f + FrameHash(i + 2) * 23f) * (i % 2 == 1 ? 1f : -1f) + i * 40f);
                var col = Color.HSVToRGB(Wrap(i * 0.11f + t * 0.08f, 1f), 0.6f, 1f);
                CardDiamond(cx, cy, half, deg, new Color(col.r, col.g, col.b, 0.14f));
            }
        }
    }
}
