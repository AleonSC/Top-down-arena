using UnityEngine;

namespace Arena
{
    /// Eight more party-card fills (ranks 57 to 71, the odd ones): Radar Sweep, Bullet Spiral, Phosphor Rain, Lava Veins,
    /// Synthwave Sun, Snowfall, Contour Map and Honeycomb Pulse. Drawn by DrawCardBackground (CardBackgrounds.cs) with its helpers
    /// (CardFill, CardDot, CardLine, ...), so they are clipped to the card and sit under the same scrim. With Visual effects off the
    /// clock is held at 0. Mockup: Docs/Mockups/card-backgrounds-more.html.
    public partial class ArenaApp
    {
        static void CardRing(float cx, float cy, float rad, float dotRad, Color c)
        {
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.9f / Mathf.Max(8f, rad) * 3f)
                CardDot(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad, dotRad, c);
        }

        /// Rank 57: a green radar scope; the beam lights the contacts it passes.
        static void CardRadar(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.016f, 0.078f, 0.059f));
            float cx = w * 0.8f, cy = h * 0.5f, a = t > 0f ? t * 1.3f : 0.6f;
            var grid = new Color(0.27f, 0.9f, 0.59f, 0.2f);
            foreach (float rad in new[] { 16f, 32f, 50f })
                CardRing(cx, cy, rad, 0.6f, grid);
            CardFill(new Rect(cx - 60f, cy - 0.5f, 120f, 1f), grid);
            CardFill(new Rect(cx - 0.5f, cy - 60f, 1f, 120f), grid);
            for (int k = 0; k < 10; k++)
            {
                float an = a - k * 0.08f;
                CardLine(new Vector2(cx, cy), new Vector2(cx + Mathf.Cos(an) * 90f, cy + Mathf.Sin(an) * 90f), 2.6f, new Color(0.27f, 0.9f, 0.59f, 0.12f * (1f - k / 10f)));
            }
            CardLine(new Vector2(cx, cy), new Vector2(cx + Mathf.Cos(a) * 90f, cy + Mathf.Sin(a) * 90f), 1.3f, new Color(0.67f, 1f, 0.82f, 0.85f));
            for (int i = 0; i < 6; i++)
            {
                float ang = FrameHash(i) * Mathf.PI * 2f, rad = 10f + FrameHash(i + 4) * 70f, since = Wrap(a - ang, Mathf.PI * 2f), al = Mathf.Max(0f, 1f - since / 5f);
                CardDot(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad, 2.2f, new Color(0.75f, 1f, 0.86f, al));
            }
        }

        /// Rank 59: a five-armed spiral of slow pink and cyan bullets fanning out from the right edge.
        static void CardSpiral(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.07f, 0.04f, 0.08f));
            float cx = w * 0.86f, cy = h * 0.5f;
            for (int k = 0; k < 5; k++)
                for (int j = 0; j < 18; j++)
                {
                    float age = Wrap(t * 0.3f + j / 18f, 1f), ang = k * Mathf.PI * 2f / 5f + age * 1.5f + t * 0.25f, r = age * w * 0.95f;
                    float x = cx + Mathf.Cos(ang) * r, y = cy + Mathf.Sin(ang) * r * 0.9f;
                    var col = k % 2 == 1 ? new Color(1f, 0.35f, 0.67f) : new Color(0.35f, 0.86f, 1f);
                    CardDot(x, y, 5f + age * 4f, new Color(col.r, col.g, col.b, 0.12f * (1f - age)));
                    CardDot(x, y, 1.6f + age * 1.8f, new Color(col.r, col.g, col.b, 0.85f - age * 0.55f));
                }
        }

        /// Rank 61: columns of green characters falling (each character a couple of small blocks), bright head and fading tail.
        static void CardMatrix(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.008f, 0.04f, 0.024f));
            for (int i = 0; i < (int)(w / 11f); i++)
            {
                float sp = 22f + FrameHash(i) * 30f, head = Wrap(t * sp + FrameHash(i + 5) * 200f, h + 90f) - 12f;
                for (int j = 0; j < 7; j++)
                {
                    float y = head - j * 11f;
                    if (y < -10f || y > h + 10f)
                        continue;
                    int g = Mathf.FloorToInt(FrameHash(i * 7 + j + Mathf.FloorToInt(t * 3f + j * 0.3f)) * 1000f);
                    var col = j == 0 ? new Color(0.82f, 1f, 0.88f, 0.95f) : new Color(0.24f, 1f, 0.55f, 0.55f * (1f - j / 7f));
                    float x = i * 11f + 1f;
                    CardFill(new Rect(x + (g % 3 == 0 ? 0f : 2f), y - 6f, 2f, 7f), col);
                    CardFill(new Rect(x + (g % 2 == 0 ? 0f : 4f), y - 6f + (g % 5), 2f, 2f), col);
                    CardFill(new Rect(x, y - 1f - (g % 4 == 0 ? 3f : 0f), 6f, 1.5f), col);
                }
            }
        }

        /// Rank 63: near-black rock with glowing orange veins that slide and pulse.
        static void CardLava(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.075f, 0.024f, 0.027f));
            for (int i = 0; i < 3; i++)
            {
                float x = w * (0.2f + 0.3f * i) + Mathf.Sin(t * 0.4f + i) * 20f, y = h * 0.5f + Mathf.Cos(t * 0.5f + i * 2f) * 18f;
                for (int k = 3; k >= 1; k--)
                    CardFill(new Rect(x - 16f * k, y - 12f * k, 32f * k, 24f * k), new Color(1f, 0.47f, 0.12f, 0.07f));
            }
            for (int k = 0; k < 5; k++)
                for (float x = -4f; x <= w + 4f; x += 5f)
                {
                    float y = h * (0.12f + 0.19f * k) + Mathf.Sin(x * 0.03f + t * 0.5f + k * 2f) * 11f + Mathf.Sin(x * 0.07f - t * 0.8f + k) * 5f;
                    CardFill(new Rect(x, y - 4.5f, 5f, 9f), new Color(1f, 0.35f, 0.08f, 0.1f));
                    CardFill(new Rect(x, y - 1f, 5f, 2f), new Color(1f, 0.51f + 0.2f * Mathf.Sin(t + k), 0.16f, 0.6f));
                }
        }

        /// Rank 65: a striped sun sinking into a pink horizon over a dark floor with a grid.
        static void CardSun(float w, float h, float t)
        {
            float hz = h * 0.64f, cx = w * 0.72f;
            for (int i = 0; i < 12; i++)
                CardFill(new Rect(0f, hz * i / 12f, w, hz / 12f + 1f), Color.Lerp(i < 7 ? new Color(0.08f, 0.04f, 0.2f) : new Color(0.29f, 0.09f, 0.41f), i < 7 ? new Color(0.29f, 0.09f, 0.41f) : new Color(0.84f, 0.25f, 0.49f), (i < 7 ? i / 6f : (i - 7) / 4f)));
            for (float dy = -34f; dy < 0f; dy += 2f)
            {
                float half = Mathf.Sqrt(34f * 34f - dy * dy), f = (dy + 34f) / 34f;
                CardFill(new Rect(cx - half, hz - 4f + dy, half * 2f, 2f), Color.Lerp(new Color(1f, 0.89f, 0.48f), new Color(1f, 0.31f, 0.55f), f));
            }
            for (int k = 0; k < 7; k++)
            {
                float y = hz - 38f + Wrap(k * 8f + t * 3f, 56f), th = 0.8f + (y - (hz - 38f)) / 56f * 4.5f;
                CardFill(new Rect(cx - 40f, y, 80f, th), new Color(0.29f, 0.09f, 0.41f));
            }
            CardFill(new Rect(0f, hz, w, h - hz), new Color(0.05f, 0.03f, 0.13f));
            var line = new Color(1f, 0.35f, 0.63f, 0.4f);
            for (int i = -10; i <= 10; i++)
                CardLine(new Vector2(cx + i * 6f, hz), new Vector2(cx + i * 26f, h), 1f, line);
            for (int k = 0; k < 4; k++)
            {
                float f = Wrap(k + t * 0.4f, 4f) / 4f;
                CardFill(new Rect(0f, hz + (h - hz) * f * f, w, 1f), new Color(1f, 0.35f, 0.63f, 0.2f + 0.5f * f));
            }
        }

        /// Rank 67: a cold night blue with two depths of snow drifting down, and a pale haze along the bottom.
        static void CardSnow(float w, float h, float t)
        {
            CardGradient(w, h, new Color(0.043f, 0.086f, 0.157f), new Color(0.11f, 0.196f, 0.314f));
            for (int i = 0; i < 70; i++)
            {
                bool far = i % 2 == 1;
                float sp = far ? 7f + FrameHash(i) * 6f : 15f + FrameHash(i) * 14f;
                float x = Wrap(FrameHash(i + 1) * w + Mathf.Sin(t * 0.7f + i) * 9f + t * (far ? 2f : 5f), w), y = Wrap(FrameHash(i + 9) * h + t * sp, h);
                CardDot(x, y, far ? 0.9f : 1.7f, new Color(0.92f, 0.96f, 1f, far ? 0.35f : 0.8f));
            }
            for (int i = 0; i < 6; i++)
                CardFill(new Rect(0f, h - 26f + i * 26f / 6f, w, 26f / 6f + 1f), new Color(0.86f, 0.92f, 1f, 0.14f * (i + 1) / 6f));
        }

        /// Rank 69: topographic lines as wobbling rings round two drifting peaks, every third one brighter.
        static void CardTopo(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.047f, 0.086f, 0.067f));
            for (int p = 0; p < 2; p++)
            {
                float px = p == 0 ? w * 0.78f + Mathf.Sin(t * 0.2f) * 12f : w * 0.32f + Mathf.Cos(t * 0.17f) * 14f, py = p == 0 ? h * 0.45f : h * 0.85f, sc = p == 0 ? 1f : 0.8f;
                for (int lv = 1; lv <= 10; lv++)
                {
                    var col = lv % 3 != 0 ? new Color(0.55f, 0.82f, 0.59f, 0.16f) : new Color(0.67f, 0.92f, 0.67f, 0.34f);
                    for (float a = 0f; a < Mathf.PI * 2f; a += 0.16f)
                    {
                        float rad = lv * 8.5f * sc * (1f + 0.12f * Mathf.Sin(a * 3f + t * 0.4f + lv * 0.3f) + 0.07f * Mathf.Sin(a * 5f - t * 0.3f));
                        CardDot(px + Mathf.Cos(a) * rad * 1.5f, py + Mathf.Sin(a) * rad * 0.8f, 0.65f, col);
                    }
                }
            }
        }

        /// Rank 71: a hex grid where a wave of amber light spreads out from a point on the right.
        static void CardHex(float w, float h, float t)
        {
            CardFill(new Rect(0f, 0f, w, h), new Color(0.05f, 0.055f, 0.1f));
            const float r = 11f;
            float dx = r * 1.5f, dy = Mathf.Sqrt(3f) * r;
            for (int i = -1; i < w / dx + 1f; i++)
                for (int j = -1; j < h / dy + 1f; j++)
                {
                    float x = i * dx, y = j * dy + (i % 2 != 0 ? dy / 2f : 0f);
                    float wave = 0.5f + 0.5f * Mathf.Sin(Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.8f, h * 0.5f)) * 0.06f - t * 2.2f);
                    var c = new Color(1f, 0.73f, 0.24f, 0.05f + wave * wave * 0.2f);
                    float hh = dy / 2f - 1f;
                    CardFill(new Rect(x - r + 2f, y - hh * 0.35f, (r - 2f) * 2f, hh * 0.7f), c);
                    CardFill(new Rect(x - r * 0.5f + 1f, y - hh, r - 2f, hh * 2f), c);
                }
        }
    }
}
