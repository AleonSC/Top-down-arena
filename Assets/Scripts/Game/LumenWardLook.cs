using UnityEngine;

namespace Arena
{
    /// The Healer Elementalist's Lumen Ward (Core/LumenWard.cs) with Visual effects on: look A "Cathedral" of
    /// Docs/Mockups/sanctify-cone.html. Drawing only. Warm gold shafts of light fan out of the caster, long god rays shimmer
    /// through them and dust drifts toward the far end; enemy bullets the light has stopped wear a pale gold halo. With Visual
    /// effects off it is a plain pale wedge with two edges; with Reduced effects the rays and dust drop out.
    public partial class ArenaApp
    {
        static readonly Rgba LumenGold = new Rgba(1f, 0.86f, 0.5f), LumenPale = new Rgba(1f, 0.97f, 0.82f);

        /// A filled ring-slice of a cone: from radius r0 to r1, `half` either side of `ang`.
        void ConeBand(V2 c, float r0, float r1, float ang, float half, Rgba col, int segs)
        {
            SetColor(col);
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(ang - half + 2f * half * i / segs), d1 = V2.FromAngle(ang - half + 2f * half * (i + 1) / segs);
                Quad(c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0);
            }
        }

        /// A thin ray from the apex along `ang`, fading out in three steps.
        void LumenRay(V2 c, float ang, float w, float len, float a, Rgba col)
        {
            for (int s = 0; s < 3; s++)
            {
                float r0 = len * s / 3f, r1 = len * (s + 1) / 3f, wa = w * (1f - s * 0.1f);
                V2 n0 = V2.FromAngle(ang - MathX.Pi / 2f), d = V2.FromAngle(ang);
                SetColor(col.WithAlpha(a * (1f - s / 3f)));
                Quad(c + d * r0 - n0 * (wa * r0), c + d * r0 + n0 * (wa * r0), c + d * r1 + n0 * (wa * r1), c + d * r1 - n0 * (wa * r1));
            }
        }

        void DrawLumenWard(Zone z, float t)
        {
            float len, half;
            bool on;
            Game.LumenShape(z, out len, out half, out on);
            if (len < 2f)
                return;
            float k = len / z.Radius;
            V2 c = z.Pos;
            if (!fxOn)
            {
                Wedge(c, len, z.Aim, half, LumenGold.WithAlpha(0.14f), 20);
                Arc(c, len, 2f, LumenGold.WithAlpha(0.6f), 24, z.Aim - half, half * 2f);
                Line(c, c + V2.FromAngle(z.Aim - half) * len, 2f, LumenGold.WithAlpha(0.6f));
                Line(c, c + V2.FromAngle(z.Aim + half) * len, 2f, LumenGold.WithAlpha(0.6f));
                return;
            }
            const int bands = 12;
            for (int i = 0; i < bands; i++)  // outer glow, falling off with distance
            {
                float f = (float)i / bands, f1 = (i + 1f) / bands;
                float a = 0.3f * Mathf.Pow(1f - f, 1.4f) * k;
                ConeBand(c, len * f, len * f1, z.Aim, half * 1.08f, LumenGold.WithAlpha(a), 12);
            }
            for (int i = 0; i < bands; i++)  // bright narrow core
            {
                float f = (float)i / bands * 0.85f, f1 = (i + 1f) / bands * 0.85f;
                float a = 0.34f * Mathf.Pow(1f - f, 1.6f) * k;
                ConeBand(c, len * f, len * f1, z.Aim, half * 0.55f, LumenPale.WithAlpha(a), 8);
            }
            if (!fxReduced)
            {
                for (int i = 0; i < 16; i++)  // god rays
                {
                    float o = MathX.Fmod(i * 0.618034f + z.Id * 0.07f, 1f) * 2f - 1f;
                    float w = 0.012f + MathX.Fmod(i * 0.37f, 1f) * 0.03f;
                    float l = len * (0.55f + MathX.Fmod(i * 0.73f, 1f) * 0.45f);
                    float a = (0.1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(t * (0.9f + (i % 5) * 0.25f) + i * 1.7f))) * k;
                    LumenRay(c, z.Aim + o * half * 0.95f, w, l, a, LumenPale);
                }
                for (int i = 0; i < 26; i++)  // dust drifting toward the far end
                {
                    float f = MathX.Fmod(t * 0.28f + i * 0.37f + z.Id * 0.11f, 1f);
                    float side = (MathX.Fmod(i * 0.618f + z.Id * 0.13f, 1f) * 2f - 1f) * half * 0.9f;
                    V2 at = c + V2.FromAngle(z.Aim + side) * (f * len) + new V2(Mathf.Sin(t * 1.6f + i) * 4f, -6f * f);
                    Circle(at, 1.7f, LumenPale.WithAlpha(0.8f * Mathf.Sin(f * MathX.Pi) * k), 6);
                }
            }
            V2 e0 = c + V2.FromAngle(z.Aim - half) * len, e1 = c + V2.FromAngle(z.Aim + half) * len;
            Line(c, e0, 2.4f, LumenPale.WithAlpha(0.55f * k));
            Line(c, e1, 2.4f, LumenPale.WithAlpha(0.55f * k));
            Arc(c, len, 2f, LumenGold.WithAlpha(0.25f * k), 28, z.Aim - half, half * 2f);
            Circle(c, 44f * k + 8f, LumenGold.WithAlpha(0.3f), 20);  // the sunburst at the caster
            Circle(c, 22f * k + 5f, LumenPale.WithAlpha(0.55f), 16);
        }

        /// Enemy bullets a Lumen Ward is holding still: a pale gold halo that grows as they stop.
        void DrawFrozenShots(float t)
        {
            if (!fxOn)
                return;
            foreach (var b in game.Bullets.Values)
            {
                if (b.Stasis < 0.05f)
                    continue;
                float s = Mathf.Min(1f, b.Stasis * 1.2f);
                Circle(b.Pos, b.Radius + 7f * s, LumenGold.WithAlpha(0.28f * s), 12);
                Circle(b.Pos, b.Radius * 0.55f, LumenPale.WithAlpha(0.9f * s), 8);
                if (b.Stasis > 0.9f && !fxReduced)
                    Arc(b.Pos, b.Radius + 5f + Mathf.Sin(t * 3f + b.Id) * 1.2f, 1f, LumenPale.WithAlpha(0.5f), 12);
            }
        }
    }
}
