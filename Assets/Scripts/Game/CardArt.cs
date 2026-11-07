using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The picker cards' bodies: a rounded card shaded top to bottom in the rarity's colour (rich at the top, dark at the
    /// bottom) with a bright bloom behind the icon tile, drawn as one texture per colour. Ui.Card's own fill is a flat dark.
    public static class CardArt
    {
        static readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();

        static readonly Dictionary<int, Texture2D> holoCache = new Dictionary<int, Texture2D>();
        static Texture2D bandTex;

        static float Phi(float z)  // the normal curve's running total (Abramowitz & Stegun 26.2.17)
        {
            float a = Mathf.Abs(z), t = 1f / (1f + 0.2316419f * a);
            float d = 0.3989423f * Mathf.Exp(-a * a / 2f);
            float p = d * t * (0.3193815f + t * (-0.3565638f + t * (1.781478f + t * (-1.821256f + t * 1.330274f))));
            return z > 0f ? 1f - p : p;
        }

        /// The holographic card body (Docs/Mockups/holo-level-up-cards.html, "3 Projection"): the rarity colour as see-through
        /// glass at the top, fading to the dark screen colour by 70% down, with the colour glowing in from the edges. Baked at
        /// half the level-up card's size (135 x 192) and stretched; the scanlines, the drifting band and the colour fringe
        /// are drawn on top each frame.
        public static Texture2D Holo(Color rc)
        {
            int key = (Mathf.RoundToInt(rc.r * 255f) << 16) | (Mathf.RoundToInt(rc.g * 255f) << 8) | Mathf.RoundToInt(rc.b * 255f);
            Texture2D tex;
            if (holoCache.TryGetValue(key, out tex) && tex != null)
                return tex;
            const int w = 135, h = 192;
            const float sigma = 9.5f;  // the mockup's 3cqw inset blur (38 px on a 270 px card) is 19 px of spread, 9.5 texels here
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            var back = new Color(0.006f, 0.016f, 0.04f);       // the dimmed arena the card sits on
            var screen = new Color(3f / 255f, 8f / 255f, 20f / 255f);  // #030814
            var mid = Color.Lerp(screen, rc, 0.1f);            // 10% rarity colour at the 70% stop
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;  // 0 at the top (y counts down from the top; texture rows start at the bottom, so the write flips it)
                float py = Phi((y + 0.5f) / sigma) - Phi((y + 0.5f - h) / sigma);
                Color row;
                if (v < 0.7f)
                {
                    float t = v / 0.7f, a = Mathf.Lerp(0.34f, 1f, t);
                    var pre = new Color(Mathf.Lerp(rc.r * 0.34f, mid.r, t), Mathf.Lerp(rc.g * 0.34f, mid.g, t), Mathf.Lerp(rc.b * 0.34f, mid.b, t));
                    row = new Color(pre.r + back.r * (1f - a), pre.g + back.g * (1f - a), pre.b + back.b * (1f - a));
                }
                else
                    row = Color.Lerp(mid, screen, (v - 0.7f) / 0.3f);
                for (int x = 0; x < w; x++)
                {
                    float px_ = Phi((x + 0.5f) / sigma) - Phi((x + 0.5f - w) / sigma);
                    float sh = (1f - px_ * py) * 0.4f;  // rarity colour at 40%, blurred in from every edge
                    px[(h - 1 - y) * w + x] = new Color(row.r * (1f - sh) + rc.r * sh, row.g * (1f - sh) + rc.g * sh, row.b * (1f - sh) + rc.b * sh, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            holoCache[key] = tex;
            return tex;
        }

        /// A white strip that fades in to the middle and out again: the drifting scan band, tinted when drawn.
        public static Texture2D Band()
        {
            if (bandTex != null)
                return bandTex;
            const int h = 64;
            bandTex = new Texture2D(1, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[h];
            for (int i = 0; i < h; i++)
                px[i] = new Color(1f, 1f, 1f, 1f - Mathf.Abs((i + 0.5f) / h * 2f - 1f));
            bandTex.SetPixels(px);
            bandTex.Apply();
            return bandTex;
        }

        /// tall: the 270 x 384 level-up cards; otherwise the weapon mod grid's wide, short ones.
        public static Texture2D Body(Color rc, bool tall)
        {
            long key = ((long)Mathf.RoundToInt(rc.r * 255f) << 17) | ((long)Mathf.RoundToInt(rc.g * 255f) << 9) | ((long)Mathf.RoundToInt(rc.b * 255f) << 1) | (tall ? 1L : 0L);
            Texture2D tex;
            if (cache.TryGetValue(key, out tex) && tex != null)
                return tex;
            int w = tall ? 108 : 100, h = tall ? 154 : 74;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[w * h];
            var baseCol = new Color(0.045f, 0.055f, 0.095f);
            var topCol = new Color(0.085f + rc.r * 0.34f, 0.095f + rc.g * 0.34f, 0.14f + rc.b * 0.34f);
            const float radius = 4f;
            float bloomY = tall ? 0.27f : 0.38f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = 1f - (y + 0.5f) / h;  // v: 0 at the top, 1 at the bottom (texture rows start at the bottom)
                    float t = Mathf.Clamp01(v / 0.9f);
                    t = t * t * (3f - 2f * t);
                    var c = Color.Lerp(topCol, baseCol, t);
                    float dx = (u - 0.5f) * (tall ? 1.1f : 0.8f), dy = (v - bloomY) * (tall ? 1.3f : 1.1f);
                    float bloom = Mathf.Exp(-(dx * dx + dy * dy) * 11f) * 0.32f;
                    c.r += rc.r * bloom; c.g += rc.g * bloom; c.b += rc.b * bloom;
                    float sheen = 0.06f * Mathf.Pow(1f - v, 3f);
                    c.r += sheen; c.g += sheen; c.b += sheen;
                    float qx = Mathf.Abs(x + 0.5f - w / 2f) - (w / 2f - radius), qy = Mathf.Abs(y + 0.5f - h / 2f) - (h / 2f - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    c.a = Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            cache[key] = tex;
            return tex;
        }
    }
}
