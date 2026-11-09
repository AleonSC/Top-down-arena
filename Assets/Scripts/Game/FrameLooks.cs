using UnityEngine;

namespace Arena
{
    /// The frames a rank unlocks (Rank.cs), drawn round a card: Steel and Gold are a plain lit rim, and the rest add something
    /// that moves (Circuit, Ember Edge, Void Pulse, Legend Foil, Prism; and the eight of FrameLooksMore.cs). Drawing only: the lobby entry carries the frame number
    /// and every screen animates it from the clock. With Visual effects off they fall back to the static rim in their base colour.
    /// Mockup: Docs/Mockups/rank-frames.html.
    public partial class ArenaApp
    {
        static float FrameHash(int n)
        {
            float x = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }

        /// The rim colour now: the frame's base colour, flickering (Ember Edge) or turning through the spectrum (Prism).
        Color FrameTint(int index, float t)
        {
            string id = Rank.FrameId(index);
            var c = RankLook.Tint(Rank.FrameColor(index));
            if (!fxOn)
                return c;
            if (id == "frame-prism")
                return RankLook.Tint(Spectrum(t * 0.11f));
            if (id == "frame-ember")
                c.a = 0.8f + 0.2f * Mathf.Sin(t * 17f);
            if (id == "frame-neon" && Mathf.Sin(t * 41f) * Mathf.Sin(t * 7.3f) > 0.93f)
                c.a = 0.3f;  // the tube drops out for a few frames
            return c;
        }

        /// A card's frame: the soft glow, the lit rim, then the frame's own effect. Call in place of Ui.Card for a framed card.
        void DrawFrameCard(Rect r, int index, float t, bool glow = true, int cardBg = 0)
        {
            var fc = FrameTint(index, t);
            string id = Rank.FrameId(index);
            float pulse = fxOn && id == "frame-void" ? 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f)) : fxOn && id == "frame-ember" ? 0.7f + 0.3f * Mathf.Sin(t * 11f) : 1f;
            if (glow)
                for (int k = 3; k >= 1; k--)
                    Ui.Pill(new Rect(r.x - 3f * k, r.y - 3f * k, r.width + 6f * k, r.height + 6f * k), new Color(fc.r, fc.g, fc.b, 0.05f * (4 - k) / 3f * pulse));
            Ui.Card(r, fc, 1f);
            DrawCardBackground(r, cardBg, t);  // CardBackgrounds.cs: under the frame's own effect
            DrawFrameFx(r, index, t);
        }

        static Vector2 PerimeterAt(Rect r, float s)
        {
            float w = r.width - 16f, h = r.height - 16f, d = (s - (float)System.Math.Floor(s)) * 2f * (w + h);  // along the straight parts, corners skipped
            if (d < w) return new Vector2(r.x + 8f + d, r.y);
            d -= w;
            if (d < h) return new Vector2(r.xMax, r.y + 8f + d);
            d -= h;
            if (d < w) return new Vector2(r.xMax - 8f - d, r.yMax);
            d -= w;
            return new Vector2(r.x, r.yMax - 8f - d);
        }

        static void FrameDot(float x, float y, float rad, Color c) => Ui.Pill(new Rect(x - rad, y - rad, rad * 2f, rad * 2f), c);

        static void FrameSpark(float x, float y, float r, Color c)
        {
            Ui.Fill(new Rect(x - r, y - 0.7f, r * 2f, 1.4f), c);
            Ui.Fill(new Rect(x - 0.7f, y - r, 1.4f, r * 2f), c);
        }

        void DrawFrameFx(Rect r, int index, float t)
        {
            if (!fxOn)
                return;
            switch (Rank.FrameId(index))
            {
                case "frame-circuit":
                {
                    var teal = RankLook.Tint(Rank.FrameColor(index));
                    const float L = 18f, th = 3f;
                    foreach (var corner in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.xMax, r.yMax), new Vector2(r.x, r.yMax) })
                    {
                        float sx = corner.x < r.center.x ? 1f : -1f, sy = corner.y < r.center.y ? 1f : -1f;
                        Ui.Fill(new Rect(sx > 0 ? corner.x : corner.x - L, sy > 0 ? corner.y : corner.y - th, L, th), teal);
                        Ui.Fill(new Rect(sx > 0 ? corner.x : corner.x - th, sy > 0 ? corner.y : corner.y - L, th, L), teal);
                    }
                    float head = (t * 0.22f) % 1f;
                    for (int j = 0; j < 14; j++)
                    {
                        var p = PerimeterAt(r, head - j * 0.006f);
                        FrameDot(p.x, p.y, 3f - j * 0.15f, new Color(0.78f, 1f, 0.96f, (1f - j / 14f) * 0.95f));
                    }
                    break;
                }
                case "frame-ember":
                    for (int i = 0; i < 16; i++)
                    {
                        float life = (t * 0.7f + FrameHash(i * 7 + 3)) % 1f;
                        float x = r.x + 12f + FrameHash(i * 5 + 1) * (r.width - 24f) + Mathf.Sin(t * 3f + i) * 4f, y = r.y - life * 22f - 1f;
                        FrameDot(x, y, 1f + 1.6f * (1f - life), new Color(1f, 0.59f + 0.31f * (1f - life), 0.16f, 1f - life));
                    }
                    break;
                case "frame-void":
                {
                    var v = RankLook.Tint(Rank.FrameColor(index));
                    float b = 0.5f + 0.5f * Mathf.Sin(t * 2.4f), a = 0.2f + 0.35f * b;
                    var inner = new Color(v.r, v.g, v.b, a);
                    Ui.Fill(new Rect(r.x + 8f, r.y + 5f, r.width - 16f, 1f), inner);
                    Ui.Fill(new Rect(r.x + 8f, r.yMax - 6f, r.width - 16f, 1f), inner);
                    Ui.Fill(new Rect(r.x + 5f, r.y + 8f, 1f, r.height - 16f), inner);
                    Ui.Fill(new Rect(r.xMax - 6f, r.y + 8f, 1f, r.height - 16f), inner);
                    for (int k = 0; k < 2; k++)
                    {
                        var p = PerimeterAt(r, t * 0.06f + k * 0.5f);
                        FrameDot(p.x, p.y, 4.6f, new Color(v.r, v.g, v.b, 0.9f));
                        FrameDot(p.x, p.y, 3.2f, new Color(0.05f, 0.02f, 0.12f, 0.97f));
                    }
                    break;
                }
                case "frame-foil":
                {
                    float gx = r.x + ((t % 3.4f) / 3.4f) * (r.width + 140f) - 70f;
                    for (float x = r.x + 8f; x < r.xMax - 8f; x += 4f)
                    {
                        float top = 1f - Mathf.Abs(x - gx) / 34f, bot = 1f - Mathf.Abs(x - (gx - 30f)) / 34f;
                        if (top > 0f) Ui.Fill(new Rect(x, r.y, 4f, 2.6f), new Color(1f, 1f, 0.96f, top));
                        if (bot > 0f) Ui.Fill(new Rect(x, r.yMax - 2.6f, 4f, 2.6f), new Color(1f, 1f, 0.96f, bot));
                    }
                    float left = 1f - (gx - r.x) / 34f, right = 1f - (r.xMax - gx) / 34f;
                    if (left > 0f) Ui.Fill(new Rect(r.x, r.y + 8f, 2.6f, r.height - 16f), new Color(1f, 1f, 0.96f, left));
                    if (right > 0f) Ui.Fill(new Rect(r.xMax - 2.6f, r.y + 8f, 2.6f, r.height - 16f), new Color(1f, 1f, 0.96f, right));
                    var cs = new[] { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.xMax, r.yMax), new Vector2(r.x, r.yMax) };
                    for (int k = 0; k < 4; k++)
                    {
                        float tw = 0.5f + 0.5f * Mathf.Sin(t * 4f + k * 1.7f);
                        FrameSpark(cs[k].x, cs[k].y, 3f + 6f * tw, new Color(1f, 0.96f, 0.82f, 0.5f + 0.5f * tw));
                    }
                    break;
                }
                case "frame-prism":
                {
                    for (float s = 0f; s < 1f; s += 0.0046f)  // short bits of rim, each its own colour, turning round the card
                    {
                        var p = PerimeterAt(r, s);
                        var c = RankLook.Tint(Spectrum(s * 2f - t * 0.15f));
                        bool horizontal = Mathf.Abs(p.y - r.y) < 0.5f || Mathf.Abs(p.y - r.yMax) < 0.5f;
                        Ui.Fill(horizontal ? new Rect(p.x, p.y < r.center.y ? r.y : r.yMax - 2.6f, 4f, 2.6f) : new Rect(p.x < r.center.x ? r.x : r.xMax - 2.6f, p.y, 2.6f, 4f), c);
                    }
                    var g = PerimeterAt(r, t * 0.12f);
                    FrameSpark(g.x, g.y, 5f, new Color(1f, 1f, 1f, 0.95f));
                    break;
                }
                default:
                    DrawFrameFxMore(r, Rank.FrameId(index), t);
                    break;
            }
        }
    }
}
