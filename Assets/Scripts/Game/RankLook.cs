using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The rank badge (Docs/Rank.md, option A "Engraved crest" of Docs/Mockups/rank-badges.html): a shield with a bevelled
    /// metal rim, a dark inset face, a highlight band and, from 40 px up, one chevron per tier step under the number. Champion
    /// and Legend grow wings and Legend a star. Drawn once per tier and size class into a small smooth texture (the shapes
    /// below are in the mockup's 100-unit space, supersampled), then the rank number goes over it.
    public static class RankLook
    {
        const float PxPerUnit = 1.5f, X0 = -10f, Y0 = -6f, VW = 120f, VH = 124f;  // the texture covers x -10..110, y -6..118
        const float ShieldTop = 8f, ShieldH = 104f;                                 // the shield itself: y 8..112
        static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();

        public static Color TierColor(int rank) => Rank.TierColor(rank).ToColor();

        // --- Colours, as the mockup derives them from the tier colour -------------------------------

        static readonly Color Ink = new Color(10f / 255f, 14f / 255f, 28f / 255f);
        static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
        static Color Lit(Color c) => Mix(c, Color.white, 0.35f);
        static Color Dim(Color c) => Mix(c, Ink, 0.62f);
        static Color Deep(Color c) => Mix(c, Ink, 0.82f);

        // --- Shapes ---------------------------------------------------------------------------------

        class Layer
        {
            public float[] P;                   // x0,y0,x1,y1,... a closed polygon
            public Func<float, float, Color> Fill;  // null: no fill
            public float StrokeW;               // 0: no outline
            public Color Stroke;
            public float MinX, MinY, MaxX, MaxY;
        }

        static void Line(List<float> p, float x, float y) { p.Add(x); p.Add(y); }

        static void Curve(List<float> p, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            for (int i = 1; i <= 14; i++)
            {
                float t = i / 14f, u = 1f - t;
                Line(p, u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3, u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3);
            }
        }

        /// The shield outline: a flat top with sloped shoulders, straight sides, a curved point. inset 0 is the rim, 1 the face.
        static float[] ShieldPath(bool face)
        {
            var p = new List<float>();
            if (!face)
            {
                Line(p, 50, 8); Line(p, 88, 20); Line(p, 88, 60);
                Curve(p, 88, 60, 88, 86, 66, 104, 50, 112);
                Curve(p, 50, 112, 34, 104, 12, 86, 12, 60);
                Line(p, 12, 20);
            }
            else
            {
                Line(p, 50, 17); Line(p, 79, 26); Line(p, 79, 59);
                Curve(p, 79, 59, 79, 79, 63, 93, 50, 100);
                Curve(p, 50, 100, 37, 93, 21, 79, 21, 59);
                Line(p, 21, 26);
            }
            return p.ToArray();
        }

        static Layer Make(float[] p, Func<float, float, Color> fill, float strokeW = 0f, Color stroke = default(Color))
        {
            var l = new Layer { P = p, Fill = fill, StrokeW = strokeW, Stroke = stroke, MinX = 1e9f, MinY = 1e9f, MaxX = -1e9f, MaxY = -1e9f };
            for (int i = 0; i < p.Length; i += 2)
            {
                l.MinX = Math.Min(l.MinX, p[i]); l.MaxX = Math.Max(l.MaxX, p[i]);
                l.MinY = Math.Min(l.MinY, p[i + 1]); l.MaxY = Math.Max(l.MaxY, p[i + 1]);
            }
            return l;
        }

        static float[] Mirror(float[] p) { var q = (float[])p.Clone(); for (int i = 0; i < q.Length; i += 2) q[i] = 100f - q[i]; return q; }

        static List<Layer> Layers(int tier, bool detail)
        {
            var c = Rank.TierColors[tier].ToColor();
            var layers = new List<Layer>();
            Color edge = Deep(c);
            if (tier >= 3 && detail)  // wings
            {
                var wing = new float[] { 12, 38, -6, 30, 0, 44, -8, 50, 2, 56, -2, 68, 14, 64 };
                Color wf = tier >= 4 ? Lit(c) : c;
                layers.Add(Make(wing, (x, y) => wf, 1.5f, edge));
                layers.Add(Make(Mirror(wing), (x, y) => wf, 1.5f, edge));
            }
            if (tier >= 5 && detail)  // the star over the top; from 150 two small ones beside it
            {
                layers.Add(Make(Star(50f, -2f, 1f), (x, y) => Lit(c), 1.5f, edge));
                if (tier >= 11)
                {
                    layers.Add(Make(Star(24f, 6f, 0.6f), (x, y) => Lit(c), 1.2f, edge));
                    layers.Add(Make(Star(76f, 6f, 0.6f), (x, y) => Lit(c), 1.2f, edge));
                }
            }
            Color lit = Lit(c), mid = c, low = Dim(c);
            layers.Add(Make(ShieldPath(false), (x, y) => { float t = Mathf.Clamp01((y - ShieldTop) / ShieldH); return t < 0.5f ? Mix(lit, mid, t * 2f) : Mix(mid, low, (t - 0.5f) * 2f); }, 2.5f, edge));
            Color fTop = Mix(c, Ink, 0.55f), fBot = Deep(c);
            var rim = lit.WithAlpha(0.55f);
            layers.Add(Make(ShieldPath(true), (x, y) => Mix(fTop, fBot, Mathf.Clamp01((y - 17f) / 83f)), 1.5f, rim));
            var band = new List<float>();
            Line(band, 50, 17); Line(band, 79, 26); Line(band, 79, 40);
            Curve(band, 79, 40, 66, 34, 58, 30, 50, 29);
            Curve(band, 50, 29, 42, 30, 34, 34, 21, 40);
            Line(band, 21, 26);
            layers.Add(Make(band.ToArray(), (x, y) => new Color(1f, 1f, 1f, 0.1f)));
            return layers;
        }

        static float[] Star(float cx, float cy, float k)
        {
            var s = new List<float>();
            float[][] d = { new[] { 4f, 9f }, new[] { 10f, 1f }, new[] { -8f, 7f }, new[] { 3f, 10f }, new[] { -9f, -6f }, new[] { -9f, 6f }, new[] { 3f, -10f }, new[] { -8f, -7f }, new[] { 10f, -1f } };
            Line(s, cx, cy);
            foreach (var v in d) { cx += v[0] * k; cy += v[1] * k; Line(s, cx, cy); }
            return s.ToArray();
        }

        static bool Inside(float[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 2; i < p.Length; j = i, i += 2)
                if ((p[i + 1] > y) != (p[j + 1] > y) && x < (p[j] - p[i]) * (y - p[i + 1]) / (p[j + 1] - p[i + 1]) + p[i])
                    inside = !inside;
            return inside;
        }

        static float Dist(float[] p, float x, float y)
        {
            float best = 1e9f;
            for (int i = 0, j = p.Length - 2; i < p.Length; j = i, i += 2)
            {
                float ax = p[j], ay = p[j + 1], dx = p[i] - ax, dy = p[i + 1] - ay;
                float len2 = dx * dx + dy * dy, t = len2 > 0f ? Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / len2) : 0f;
                float ex = ax + dx * t - x, ey = ay + dy * t - y;
                best = Math.Min(best, ex * ex + ey * ey);
            }
            return (float)Math.Sqrt(best);
        }

        /// Straight-alpha "over": paints src on dst (both premultiplied here: r,g,b already times a).
        static void Over(ref float r, ref float g, ref float b, ref float a, Color src)
        {
            float sa = src.a;
            r = src.r * sa + r * (1f - sa);
            g = src.g * sa + g * (1f - sa);
            b = src.b * sa + b * (1f - sa);
            a = sa + a * (1f - sa);
        }

        static Texture2D Build(int tier, bool detail)
        {
            int key = tier * 2 + (detail ? 1 : 0);
            Texture2D tex;
            if (cache.TryGetValue(key, out tex) && tex != null)  // (a texture Unity destroyed, e.g. on a scene reload, is made again)
                return tex;
            var layers = Layers(tier, detail);
            int w = (int)Math.Round(VW * PxPerUnit), h = (int)Math.Round(VH * PxPerUnit);
            var px = new Color32[w * h];
            float[] off = { 0.25f, 0.75f };
            for (int py = 0; py < h; py++)
                for (int pxl = 0; pxl < w; pxl++)
                {
                    float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                    foreach (float oy in off)
                        foreach (float ox in off)
                        {
                            float x = X0 + (pxl + ox) / PxPerUnit, y = Y0 + (py + oy) / PxPerUnit;
                            float r = 0f, g = 0f, b = 0f, a = 0f;
                            foreach (var l in layers)
                            {
                                float pad = l.StrokeW * 0.5f;
                                if (x < l.MinX - pad || x > l.MaxX + pad || y < l.MinY - pad || y > l.MaxY + pad)
                                    continue;
                                bool inside = Inside(l.P, x, y);
                                if (inside && l.Fill != null)
                                    Over(ref r, ref g, ref b, ref a, l.Fill(x, y));
                                if (l.StrokeW > 0f && Dist(l.P, x, y) <= pad)
                                    Over(ref r, ref g, ref b, ref a, l.Stroke);
                            }
                            sr += r; sg += g; sb += b; sa += a;
                        }
                    // average, back to straight alpha; textures start at the bottom row
                    float ia = sa > 0f ? 1f / sa : 0f, alpha = sa / 4f;
                    px[(h - 1 - py) * w + pxl] = new Color32((byte)Math.Round(Math.Min(1f, sr * ia) * 255f), (byte)Math.Round(Math.Min(1f, sg * ia) * 255f), (byte)Math.Round(Math.Min(1f, sb * ia) * 255f), (byte)Math.Round(Math.Min(1f, alpha) * 255f));
                }
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            cache[key] = tex;
            return tex;
        }

        /// The level marks: nine slots under the number (5 over 4), one lit per level climbed in the tier (Rank.Marks).
        static void DrawMarks(float cx, float cy, float s, Color c, int lit, float alpha)
        {
            float d = Math.Max(2f, 6f * s);
            for (int i = 0; i < Rank.MarkCount; i++)
            {
                bool top = i < 5;
                float ux = top ? 32f + i * 9f : 36.5f + (i - 5) * 9f, uy = top ? 78f : 87f;
                var rect = new Rect(cx + (ux - 50f) * s - d / 2f, cy + (uy - 60f) * s - d / 2f, d, d);
                if (i < lit)
                {
                    Ui.Dot(rect, Deep(c).WithAlpha(alpha));
                    Ui.Dot(new Rect(rect.x + d * 0.1f, rect.y + d * 0.1f, d * 0.8f, d * 0.8f), Lit(c).WithAlpha(alpha));
                }
                else
                    Ui.Dot(rect, new Color(1f, 1f, 1f, 0.12f * alpha));
            }
        }

        /// The badge in a square: the shield (its height is 92% of the square's), then the number. Fits any size from about
        /// 16 px to 120 px; the level marks, wings and stars appear from 40 px.
        public static void Draw(Rect r, int rank, float alpha = 1f)
        {
            int tier = Rank.Tier(rank);
            bool detail = r.height >= 40f;
            float s = 0.92f * r.height / ShieldH;  // pixels per unit
            float cx = r.center.x, cy = r.center.y;
            var dest = new Rect(cx - (50f - X0) * s, cy - (60f - Y0) * s, VW * s, VH * s);  // the shield's middle (50, 60) sits at the centre
            Ui.Icon(dest, Build(tier, detail), alpha);
            if (detail)
                DrawMarks(cx, cy, s, Rank.TierColor(rank).ToColor(), Rank.Marks(rank), alpha);
            string text = rank.ToString();
            float baseSize = detail ? 34f : 42f;
            float size = baseSize * (text.Length >= 4 ? 0.5f : text.Length >= 3 ? 0.62f : text.Length == 2 ? 0.8f : 1f) * s;
            int fs = Math.Max(8, (int)Math.Round(size));
            float ny = detail ? 46f : 55f;
            var tr = new Rect(cx - 50f * s, cy + (ny - 60f) * s - fs, 100f * s, fs * 2f);
            var dark = new Color(0.02f, 0.03f, 0.06f, 0.9f * alpha);
            float o = fs >= 22 ? Math.Max(1f, fs * 0.07f) : 1f;
            Ui.Label(new Rect(tr.x - o, tr.y, tr.width, tr.height), text, fs, dark, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(tr.x + o, tr.y, tr.width, tr.height), text, fs, dark, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(tr.x, tr.y - o, tr.width, tr.height), text, fs, dark, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(tr.x, tr.y + o, tr.width, tr.height), text, fs, dark, TextAnchor.MiddleCenter, true);
            Ui.Label(tr, text, fs, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter, true);
        }
    }
}
