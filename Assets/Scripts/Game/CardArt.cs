using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The picker cards' bodies: a rounded card shaded top to bottom in the rarity's colour (rich at the top, dark at the
    /// bottom) with a bright bloom behind the icon tile, drawn as one texture per colour. Ui.Card's own fill is a flat dark.
    public static class CardArt
    {
        static readonly Dictionary<long, Texture2D> cache = new Dictionary<long, Texture2D>();

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
