using UnityEngine;

namespace Arena
{
    /// The aim lines a rank unlocks (Rank.cs, CosmeticKind.AimLine; mockup Docs/Mockups/aim-lines.html). Only your own line is drawn,
    /// so nothing here goes on the wire. 0 is the Classic line (everyone has it), then 1.. in Rank.Cosmetics order: Dotted Tracer,
    /// Chevron Flow, Ruler, Ember Wisp, Frost Shard, Storm Arc, Pixel Beam, Helix, Prism. Each draws from `from` along `dir` for
    /// `reach` px, tinted by your class colour `c`, and ends in a cap when `capped` (the crosshair is beyond your weapon's reach).
    /// The arena (DrawAimLine) and the cosmetics window previews call the same code.
    public partial class ArenaApp
    {
        /// The look you have equipped and your rank has reached (0 Classic).
        int AimLook()
        {
            var p = Profile.Current;
            return Rank.Owns(p.RankNow, p.AimLine) ? Rank.AimLineIndex(p.AimLine) : 0;
        }

        /// The colour the previews tint with (the real line uses your class colour).
        static readonly Rgba AimSampleColor = new Rgba(0.43f, 0.76f, 1f);

        void DrawAimLook(int look, V2 from, V2 dir, float reach, float t, Rgba c, bool capped)
        {
            if (reach < 1f)
                return;
            V2 side = dir.Orthogonal();
            switch (look)
            {
                case 1: AimDotted(from, dir, reach, t, c, capped); break;
                case 2: AimChevron(from, dir, side, reach, t, c, capped); break;
                case 3: AimRuler(from, dir, side, reach, t, c, capped); break;
                case 4: AimEmber(from, dir, side, reach, t, c, capped); break;
                case 5: AimFrost(from, dir, side, reach, t, c, capped); break;
                case 6: AimStorm(from, dir, side, reach, t, c, capped); break;
                case 7: AimPixel(from, dir, reach, t, c, capped); break;
                case 8: AimHelix(from, dir, side, reach, t, c, capped); break;
                case 9: AimPrism(from, dir, side, reach, t, c, capped); break;
                default: AimClassic(from, dir, side, reach, t, c, capped); break;
            }
        }

        static float AimFade(float d, float reach, float k = 0.8f) => 1f - k * d / reach;

        void AimClassic(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var c = col.Mix(Rgba.White, 0.35f);
            const int Steps = 24;
            for (int i = 0; i < Steps; i++)
            {
                float a = (float)i / Steps, b = (float)(i + 1) / Steps;
                float fade = 1f - 0.8f * a;  // strongest at the gun, still visible at the crosshair
                V2 p0 = from + dir * (reach * a), p1 = from + dir * (reach * b);
                Line(p0, p1, 8f, c.WithAlpha(0.07f * fade));   // soft glow
                Line(p0, p1, 1.5f, c.WithAlpha(0.32f * fade));  // thin core
            }
            if (capped)
            {
                V2 end = from + dir * reach;
                Line(end - side * 6f, end + side * 6f, 2f, c.WithAlpha(0.45f));
            }
            const float Spacing = 34f;  // motes drifting outward
            float offset = MathX.Fmod(t * 140f, Spacing);
            for (float d = offset; d < reach; d += Spacing)
            {
                float f = AimFade(d, reach, 0.75f);
                Circle(from + dir * d, 1.3f + 1.2f * f, c.WithAlpha(0.5f * f), 8);
            }
        }

        void AimDotted(V2 from, V2 dir, float reach, float t, Rgba col, bool capped)
        {
            var c = col.Mix(Rgba.White, 0.25f);
            for (float d = MathX.Fmod(t * 50f, 14f); d < reach; d += 14f)
            {
                float f = AimFade(d, reach, 0.85f);
                Circle(from + dir * d, 0.8f + 2.4f * f, c.WithAlpha(0.9f * f + 0.1f), 8);
            }
            if (capped)
                Arc(from + dir * reach, 5f, 1.6f, c.WithAlpha(0.7f), 14);
        }

        void AimChevron(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var c = col.Mix(Rgba.White, 0.3f);
            Line(from, from + dir * reach, 1f, c.WithAlpha(0.12f));
            for (float d = MathX.Fmod(t * 100f, 28f); d < reach; d += 28f)
            {
                var a = c.WithAlpha(0.85f * AimFade(d, reach));
                V2 tip = from + dir * d;
                Line(tip - dir * 5f - side * 5f, tip, 2f, a);
                Line(tip, tip - dir * 5f + side * 5f, 2f, a);
            }
            if (capped)
            {
                V2 tip = from + dir * (reach + 1f);
                Line(tip - dir * 7f - side * 7f, tip, 2.5f, c.WithAlpha(0.9f));
                Line(tip, tip - dir * 7f + side * 7f, 2.5f, c.WithAlpha(0.9f));
            }
        }

        void AimRuler(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var c = col.Mix(Rgba.White, 0.4f);
            Line(from, from + dir * reach, 1f, c.WithAlpha(0.35f));
            for (float d = 20f; d <= reach; d += 20f)
            {
                bool major = Mathf.Abs(MathX.Fmod(d, 100f)) < 0.5f, mid = Mathf.Abs(MathX.Fmod(d, 50f)) < 0.5f;
                float len = major ? 9f : mid ? 6f : 3.5f;
                V2 at = from + dir * d;
                Line(at - side * (len / 2f), at + side * (len / 2f), major ? 1.6f : 1f, c.WithAlpha(major ? 0.8f : 0.45f));
            }
            float s = MathX.Fmod(t * 260f, reach + 60f) - 30f;  // a bright bead sweeping out along the rule
            if (s > 0f && s < reach)
                Circle(from + dir * s, 2.6f, c.WithAlpha(0.9f), 8);
            if (capped)
            {
                V2 end = from + dir * reach;
                Line(end - side * 7f, end + side * 7f, 2.2f, c.WithAlpha(0.9f));
            }
        }

        void AimEmber(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var ember = new Rgba(1f, 0.59f, 0.24f);
            Line(from, from + dir * reach, 6f, col.Mix(ember, 0.7f).WithAlpha(0.07f));
            Line(from, from + dir * reach, 1.4f, ember.WithAlpha(0.22f));
            for (int i = 0; i < 28; i++)
            {
                float ph = MathX.Fmod(t * 0.7f + i * 0.618f, 1f), r = TrailHash(i), d = ph * reach;
                float o = (r - 0.5f) * 10f + Mathf.Sin(t * 5f + i * 2f) * 4f * ph;
                var spark = new Rgba(1f, 0.86f, 0.43f).Mix(new Rgba(0.86f, 0.2f, 0.08f), ph);
                Circle(from + dir * d + side * o, 0.7f + 2.2f * (1f - ph), spark.WithAlpha(0.9f * (1f - ph)), 6);
            }
            if (capped)
            {
                V2 e = from + dir * reach;
                var a = ember.WithAlpha(0.85f);
                Line(e - dir * 7f, e + side * 5f, 1.6f, a);
                Line(e + side * 5f, e + dir * 7f, 1.6f, a);
                Line(e + dir * 7f, e - side * 5f, 1.6f, a);
                Line(e - side * 5f, e - dir * 7f, 1.6f, a);
            }
        }

        void AimFrost(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var ice = col.Mix(new Rgba(0.67f, 0.92f, 1f), 0.7f);
            Line(from, from + dir * reach, 6f, ice.WithAlpha(0.06f));
            Line(from, from + dir * reach, 1.2f, ice.WithAlpha(0.28f));
            int i = 0;
            for (float d = MathX.Fmod(t * 40f, 44f); d < reach; d += 44f, i++)
            {
                float f = AimFade(d, reach, 0.6f), s = 1.5f + 4f * f, sgn = i % 2 == 0 ? 1f : -1f;
                Line(from + dir * (d - 10f), from + dir * (d - 18f) + side * (sgn * 6f), 1.2f, ice.WithAlpha(0.45f * f));
                V2 at = from + dir * d;
                SetColor(ice.WithAlpha(0.85f * f));
                Quad(at - dir * (s * 1.6f), at - side * s, at + dir * (s * 1.6f), at + side * s);
            }
            if (capped)
            {
                V2 e = from + dir * reach;
                for (int k = 0; k < 3; k++)
                {
                    V2 spoke = V2.FromAngle(k * MathX.Pi / 3f) * 7f;
                    Line(e - spoke, e + spoke, 1.5f, ice.WithAlpha(0.85f));
                }
            }
        }

        void AimStorm(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var bolt = new Rgba(0.78f, 0.67f, 1f);
            int seed = (int)(t * 20f), n = Mathf.Max(2, (int)(reach / 14f));
            var pts = new V2[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float amp = k == 0 || k == n ? 0f : 5f;
                pts[k] = from + dir * (reach * k / n) + side * ((TrailHash(seed * 13 + k) - 0.5f) * 2f * amp);
            }
            for (int k = 0; k < n; k++)
                Line(pts[k], pts[k + 1], 8f, col.Mix(bolt, 0.5f).WithAlpha(0.14f));
            for (int k = 0; k < n; k++)
                Line(pts[k], pts[k + 1], 2.6f, bolt.WithAlpha(0.5f));
            for (int k = 0; k < n; k++)
                Line(pts[k], pts[k + 1], 1.2f, Rgba.White.WithAlpha(0.9f));
            if (n > 3 && TrailHash(seed) > 0.45f)  // the odd forked spur
            {
                int b = 1 + (int)(TrailHash(seed + 9) * (n - 2));
                float sg = TrailHash(seed + 4) > 0.5f ? 1f : -1f;
                V2 p = pts[b], m = p + dir * 10f + side * (sg * 8f), e = p + dir * 20f + side * (sg * 5f + (TrailHash(seed + 2) - 0.5f) * 6f);
                Line(p, m, 1.1f, Rgba.White.WithAlpha(0.7f));
                Line(m, e, 1.1f, Rgba.White.WithAlpha(0.7f));
            }
            if (capped)
            {
                V2 end = from + dir * reach;
                Line(end - side * 6f, end + side * 6f, 2f, bolt.WithAlpha(0.85f));
                Circle(end, 3f, Rgba.White.WithAlpha(0.9f), 8);
            }
        }

        void AimPixel(V2 from, V2 dir, float reach, float t, Rgba col, bool capped)
        {
            var c = col.Mix(Rgba.White, 0.2f);
            const float G = 4f;  // a 4 px grid: squares are snapped to it, so they never rotate with the line
            for (float d = 0f; d < reach; d += 8f)
            {
                V2 p = from + dir * d;
                bool on = ((int)System.Math.Floor((d - t * 60f) / 8f) % 3 + 3) % 3 != 0;
                FillRect((float)System.Math.Floor(p.X / G) * G, (float)System.Math.Floor(p.Y / G) * G, G, G, c.WithAlpha(on ? 0.9f * AimFade(d, reach, 0.5f) + 0.1f : 0.14f));
            }
            if (capped)
            {
                V2 q = from + dir * reach;
                float x = (float)System.Math.Floor(q.X / G) * G, y = (float)System.Math.Floor(q.Y / G) * G;
                var a = c.WithAlpha(0.95f);
                FillRect(x, y, G, G, a);
                FillRect(x - G, y, G, G, a);
                FillRect(x + G, y, G, G, a);
                FillRect(x, y - G, G, G, a);
                FillRect(x, y + G, G, G, a);
            }
        }

        static float HelixWave(float d, float t) => Mathf.Sin(d / 56f * MathX.Tau - t * 5f) * 6f * Mathf.Min(1f, d / 24f);

        void AimHelix(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            var c1 = col.Mix(Rgba.White, 0.3f);
            var c2 = col.Mix(new Rgba(1f, 0.78f, 0.35f), 0.75f);
            for (float d = 14f; d < reach; d += 14f)
                Line(from + dir * d + side * HelixWave(d, t), from + dir * d - side * HelixWave(d, t), 1f, c1.WithAlpha(0.14f * AimFade(d, reach)));
            float prev = 0f;
            for (float d = 3f; d < reach + 2.9f; d += 3f)
            {
                float dd = Mathf.Min(d, reach), o = HelixWave(dd, t), po = HelixWave(prev, t);
                Line(from + dir * prev + side * po, from + dir * dd + side * o, 1.8f, c1.WithAlpha(0.75f));
                Line(from + dir * prev - side * po, from + dir * dd - side * o, 1.8f, c2.WithAlpha(0.75f));
                prev = dd;
            }
            if (capped)
            {
                V2 e = from + dir * reach;
                Arc(e, 6f, 1.6f, c1.WithAlpha(0.8f), 14);
                Circle(e, 1.8f, c2.WithAlpha(0.9f), 8);
            }
        }

        void AimPrism(V2 from, V2 dir, V2 side, float reach, float t, Rgba col, bool capped)
        {
            const float Step = 5f;
            for (float d = 0f; d < reach; d += Step)
            {
                var h = Spectrum(d * 0.0025f - t * 0.4f);
                float f = AimFade(d, reach, 0.45f);
                V2 a = from + dir * d, b = from + dir * Mathf.Min(d + Step + 0.5f, reach);
                Line(a, b, 9f, h.WithAlpha(0.09f * f));
                Line(a, b, 2.4f, h.WithAlpha(0.8f * f));
            }
            for (float d = MathX.Fmod(t * 120f, 40f); d < reach; d += 40f)
                Circle(from + dir * d, 1.8f, Rgba.White.WithAlpha(0.7f * AimFade(d, reach)), 8);
            if (capped)
            {
                V2 e = from + dir * reach;
                Line(e - side * 7f, e + side * 7f, 2.5f, Spectrum(reach * 0.0025f - t * 0.4f).WithAlpha(0.95f));
            }
        }
    }
}
