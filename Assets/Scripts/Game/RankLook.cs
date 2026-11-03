using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The rank badge (Docs/Rank.md, Docs/Mockups/rank-screens.html, style B): a pixel-art shield in the tier's colour with the
    /// rank number over it and, from 18 px up, one chevron per tier step below it. One small texture per tier, made once.
    public static class RankLook
    {
        const int N = 32;
        static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();

        public static Color Tint(Rgba c) => new Color(c.R, c.G, c.B, c.A);
        public static Color TierColor(int rank) => Tint(Rank.TierColor(rank));

        /// Is this pixel (column x, row y from the top) inside the shield? Flat top, straight sides, a curved point.
        static bool Inside(int x, int y)
        {
            if (x < 0 || y < 0 || x >= N || y >= N)
                return false;
            float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
            if (v < 0.07f || v > 0.96f)
                return false;
            float half = 0.42f;
            if (v > 0.5f)
            {
                float t = (v - 0.5f) / 0.46f;
                half = 0.42f * (1f - Mathf.Pow(t, 1.7f));
            }
            return Mathf.Abs(u - 0.5f) <= half;
        }

        static Color32 Pack(Color c) => new Color32((byte)Mathf.RoundToInt(c.r * 255f), (byte)Mathf.RoundToInt(c.g * 255f), (byte)Mathf.RoundToInt(c.b * 255f), (byte)Mathf.RoundToInt(c.a * 255f));

        static Texture2D Shield(int tier, bool chevrons)
        {
            int key = tier * 2 + (chevrons ? 1 : 0);
            Texture2D tex;
            if (cache.TryGetValue(key, out tex))
                return tex;
            var col = Tint(Rank.TierColors[tier]);
            var px = new Color32[N * N];
            var dark = new Color(0.07f, 0.09f, 0.16f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color c = new Color(0f, 0f, 0f, 0f);
                    if (Inside(x, y))
                    {
                        bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                        float v = (y + 0.5f) / N;
                        c = edge ? col : Color.Lerp(dark, col, 0.38f * (1f - v));
                        if (!edge && y == 2 + 1)  // a faint highlight just under the top edge
                            c = Color.Lerp(c, Color.white, 0.12f);
                    }
                    px[(N - 1 - y) * N + x] = Pack(c);  // textures start at the bottom row
                }
            if (chevrons)
            {
                var lit = Color.Lerp(col, Color.white, 0.25f);
                for (int i = 0; i <= tier; i++)
                {
                    float apex = 0.60f + i * 0.065f;
                    for (int x = 0; x < N; x++)
                    {
                        float dx = Mathf.Abs((x + 0.5f) / N - 0.5f);
                        if (dx > 0.12f)
                            continue;
                        int y = Mathf.FloorToInt((apex - (0.12f - dx) * 0.85f) * N);
                        if (Inside(x, y) && Inside(x - 1, y) && Inside(x + 1, y))
                            px[(N - 1 - y) * N + x] = Pack(lit);
                    }
                }
            }
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            cache[key] = tex;
            return tex;
        }

        /// The badge in a square: the shield, then the number. Fits any size from about 16 px to 120 px (the chevrons appear from 36).
        public static void Draw(Rect r, int rank, float alpha = 1f)
        {
            bool chevrons = r.height >= 36f;
            Ui.Icon(r, Shield(Rank.Tier(rank), chevrons), alpha);
            string text = rank.ToString();
            int size = Mathf.Max(8, Mathf.RoundToInt(r.height * (text.Length >= 3 ? 0.30f : text.Length == 2 ? 0.38f : 0.46f)));
            var tr = new Rect(r.x, r.y + r.height * (chevrons ? 0.04f : 0.1f), r.width, r.height * (chevrons ? 0.5f : 0.6f));
            Ui.Label(new Rect(tr.x + 1f, tr.y + 1f, tr.width, tr.height), text, size, new Color(0f, 0f, 0f, 0.55f * alpha), TextAnchor.MiddleCenter, true);
            Ui.Label(tr, text, size, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter, true);
        }
    }
}
