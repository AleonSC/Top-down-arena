using UnityEngine;

namespace Arena
{
    /// The Frost Lance as a crystal spear (Docs/Mockups/frost-lance.html, look A), with Visual effects on: the icicle is a
    /// faceted crystal that shoots out, cracks white and breaks, and its splinters are tumbling kite-shaped shards that
    /// sparkle as they land. Drawing only; reach, hits and timing are in Knight.cs (the splinters come SplinterDelay later).
    public partial class ArenaApp
    {
        const float CrystalOut = 0.09f, CrystalHold = 0.2f, CrystalBreak = 0.16f, CrystalCrack = 0.13f;  // seconds into the cast

        static readonly Rgba CrystalLight = new Rgba(0.79f, 0.96f, 1f), CrystalDark = new Rgba(0.35f, 0.69f, 0.9f);

        static float CrystalBackOut(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            return 1f + 2.9f * x * x * x + 1.9f * x * x;
        }

        /// A point `x` along the lance and `y` to its side.
        static V2 Lance(V2 o, V2 dir, V2 side, float x, float y) => o + dir * x + side * y;

        /// FxKind.Icicle, the crystal spear. f is how far through the effect's life it is.
        void DrawIcicleCrystal(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return;
            V2 o = fx.Pos, dir = to / reach, side = dir.Orthogonal();
            float t = f * Game.IcicleFxTime;  // seconds since the cast
            float L = reach * Mathf.Min(1.05f, CrystalBackOut(t / CrystalOut));
            float fade = t < CrystalHold ? 1f : 1f - Mathf.Clamp01((t - CrystalHold) / CrystalBreak);
            if (fade <= 0f)
                return;
            // A cold haze round it.
            SetColor(fx.Color.WithAlpha(0.12f * fade));
            Tri(Lance(o, dir, side, 0f, -w * 2.2f), Lance(o, dir, side, L, 0f), Lance(o, dir, side, 0f, w * 2.2f));
            SetColor(fx.Color.WithAlpha(0.14f * fade));
            Tri(Lance(o, dir, side, 0f, -w * 1.4f), Lance(o, dir, side, L * 0.95f, 0f), Lance(o, dir, side, 0f, w * 1.4f));
            // The body: a light half and a dark half, each a fan from the centre line.
            V2 b0 = Lance(o, dir, side, 0f, 0f), tip = Lance(o, dir, side, L, 0f);
            V2 u1 = Lance(o, dir, side, 0f, -w * 0.55f), u2 = Lance(o, dir, side, L * 0.28f, -w), u3 = Lance(o, dir, side, L * 0.88f, -w * 0.28f);
            V2 d1 = Lance(o, dir, side, 0f, w * 0.55f), d2 = Lance(o, dir, side, L * 0.28f, w), d3 = Lance(o, dir, side, L * 0.88f, w * 0.28f);
            SetColor(CrystalLight.WithAlpha(fade));
            Tri(b0, u1, u2); Tri(b0, u2, u3); Tri(b0, u3, tip);
            SetColor(CrystalDark.WithAlpha(fade));
            Tri(b0, d1, d2); Tri(b0, d2, d3); Tri(b0, d3, tip);
            // Inner facets, and the lines between them.
            SetColor(Rgba.White.WithAlpha(0.35f * fade));
            Quad(u2, u3, Lance(o, dir, side, L * 0.5f, 0f), Lance(o, dir, side, L * 0.1f, -w * 0.3f));
            SetColor(new Rgba(0.08f, 0.27f, 0.55f, 0.35f * fade));
            Quad(d2, d3, Lance(o, dir, side, L * 0.6f, 0f), Lance(o, dir, side, L * 0.2f, w * 0.3f));
            Line(u2, tip, 1.3f, Rgba.White.WithAlpha(0.9f * fade));
            Line(d2, tip, 1f, new Rgba(0.86f, 0.96f, 1f, 0.5f * fade));
            Line(Lance(o, dir, side, L * 0.28f, -w), Lance(o, dir, side, L * 0.28f, w), 1f, Rgba.White.WithAlpha(0.35f * fade));
            Line(b0, tip, 1.6f, Rgba.White.WithAlpha(0.95f * fade));
            // Glints sliding along it.
            for (int i = 0; i < 3; i++)
            {
                float gx = MathX.Fmod(t * 700f + i * 90f, L + 1f);
                Star4(Lance(o, dir, side, gx, (i - 1) * w * 0.25f * (1f - gx / reach)), 3f + (i % 2) * 2f, Rgba.White.WithAlpha(0.95f * fade));
            }
            // The crack: a white flash along it just before it breaks.
            float flash = Mathf.Clamp01(1f - Mathf.Abs(t - CrystalCrack) / 0.05f);
            if (flash > 0f)
            {
                SetColor(Rgba.White.WithAlpha(0.8f * flash * fade));
                Tri(b0, u1, u2); Tri(b0, u2, u3); Tri(b0, u3, tip);
                Tri(b0, d1, d2); Tri(b0, d2, d3); Tri(b0, d3, tip);
            }
            if (t > 0.08f)  // cracks run across it
                for (int i = 0; i < 4; i++)
                {
                    float at = L * (0.2f + 0.2f * i), h = w * (1f - at / reach) * 0.9f;
                    Line(Lance(o, dir, side, at + 5f, -h), Lance(o, dir, side, at - 3f, h * 0.2f), 1.2f, new Rgba(0.24f, 0.47f, 0.75f, 0.8f * fade));
                }
        }

        /// FxKind.Splinter, the shards. Seeded by where it was cast, so everyone sees the same burst.
        void DrawSplinterCrystal(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, spread = fx.Radius;
            if (reach < 1f)
                return;
            V2 o = fx.Pos, dir = to / reach, side = dir.Orthogonal();
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)fx.Pos2.X * 83492791) | 1u;
            System.Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(f * 1.15f), 3f);
            float alpha = Mathf.Clamp01(1.6f - f * 1.6f), hw = Game.IcicleHalfWidth * 0.7f;
            int n = Mathf.Max(5, Mathf.FloorToInt(reach / 17f));
            for (int i = 0; i < n; i++)
                for (int sg = -1; sg <= 1; sg += 2)
                {
                    float along = reach * (i + 0.2f + 0.6f * rnd()) / n, far = spread * (0.55f + 0.45f * rnd());
                    float len = 7f + 6f * rnd(), spin = (rnd() - 0.5f) * 9f, tilt = (rnd() - 0.5f) * 0.7f;
                    V2 at = Lance(o, dir, side, along + tilt * far * e * 0.6f, sg * (hw + (far - hw) * e));
                    float ang = (float)System.Math.Atan2(sg, tilt) + spin * f;
                    V2 h = side * Mathf.Sin(ang) + dir * Mathf.Cos(ang);  // heading in the lance's frame
                    V2 p = h.Orthogonal() * (len * 0.38f);
                    SetColor(fx.Color.WithAlpha(0.18f * alpha));  // its glow
                    Quad(at + h * len * 1.3f, at + p * 2f, at - h * len * 0.9f, at - p * 2f);
                    SetColor(CrystalLight.WithAlpha(alpha));
                    Tri(at + h * len, at - p, at - h * (len * 0.45f));
                    SetColor(CrystalDark.WithAlpha(alpha));
                    Tri(at + h * len, at + p, at - h * (len * 0.45f));
                    if (f > 0.55f)  // a sparkle as it lands
                        Star4(at, 11f * (1f - f), Rgba.White.WithAlpha(0.9f * alpha));
                }
        }

        /// A four-pointed sparkle (two thin diamonds).
        void Star4(V2 c, float r, Rgba col)
        {
            if (r < 0.3f)
                return;
            SetColor(col);
            float k = r * 0.22f;
            Quad(c + new V2(-r, 0f), c + new V2(0f, -k), c + new V2(r, 0f), c + new V2(0f, k));
            Quad(c + new V2(0f, -r), c + new V2(k, 0f), c + new V2(0f, r), c + new V2(-k, 0f));
        }
    }
}
