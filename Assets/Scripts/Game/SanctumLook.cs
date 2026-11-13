using UnityEngine;

namespace Arena
{
    /// Sanctum in look B, "Bubble" (Docs/Mockups/sanctum.html): a thin shell with a slow gold-to-green shimmer that opens from the
    /// middle in 0.6 s. Each healing pulse (Core/Sanctum.cs) pushes a soft disc of light and a spray of sparkles out to the rim,
    /// and the rim flares as the ring reaches it. Sparkles also drift inside the whole time. The last 0.7 s the shell flickers
    /// and fades, and a thin rim just outside drains with the time left. With Visual effects off it is the plain dome.
    /// Drawing only: the pulses are worked out from the zone's replicated timer. Left out of the mockup: ripples where bullets
    /// hit the shell (the screen doesn't know where a bullet was erased).
    public partial class ArenaApp
    {
        static readonly Rgba SanctumPale = new Rgba(1f, 0.97f, 0.82f), SanctumHeal = new Rgba(0.55f, 1f, 0.67f);

        static float SanctumHash(float n)
        {
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }

        /// A bright, soft hue (gold through green to cyan) for the shell's shimmer.
        static Rgba SanctumShimmer(float deg, float alpha)
        {
            float h = MathX.Fmod(deg, 360f) / 60f, l = 0.7f, s = 0.9f;
            float ch = (1f - Mathf.Abs(2f * l - 1f)) * s, x = ch * (1f - Mathf.Abs(MathX.Fmod(h, 2f) - 1f)), m = l - ch / 2f;
            float r, g, b;
            switch ((int)h)
            {
                case 0: r = ch; g = x; b = 0f; break;
                case 1: r = x; g = ch; b = 0f; break;
                case 2: r = 0f; g = ch; b = x; break;
                case 3: r = 0f; g = x; b = ch; break;
                case 4: r = x; g = 0f; b = ch; break;
                default: r = ch; g = 0f; b = x; break;
            }
            return new Rgba(r + m, g + m, b + m, alpha);
        }

        void SanctumSparkle(V2 at, float size, Rgba c)
        {
            Line(at - new V2(size * 2f, 0f), at + new V2(size * 2f, 0f), 1f, c);
            Line(at - new V2(0f, size * 2f), at + new V2(0f, size * 2f), 1f, c);
            Circle(at, size * 0.6f, c, 6);
        }

        void DrawSanctum(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float open = MathX.Clamp01(age / 0.6f);
            open = 1f - (1f - open) * (1f - open) * (1f - open);
            float R = z.Radius * open;
            if (R < 2f)
                return;
            float da = z.T >= 0.7f ? 1f : MathX.Clamp01(z.T / 0.7f) * (0.6f + 0.4f * Mathf.Sin(t * 38f));
            V2 c = z.Pos;
            if (age < 0.35f)
                Circle(c, 60f + age * 500f, SanctumPale.WithAlpha(0.55f * (1f - age / 0.35f)), 32);

            // the shell's body: clear in the middle, brighter toward the edge
            Circle(c, R, z.Def.Color.WithAlpha(0.02f * da), 48);
            Arc(c, R - 7f, 14f, new Rgba(0.75f, 1f, 0.84f, 0.1f * da), 56);
            Arc(c, R - 2f, 6f, z.Def.Color.WithAlpha(0.16f * da), 56);

            // pulses: a soft disc of light and a spray of sparkles
            float flare = 0f;
            for (int k = 0; k < Game.SanctumPulses; k++)
            {
                float a = age - Game.SanctumPulseAt(k);
                if (a < 0f || a > Game.SanctumPulseRing + 1.6f)
                    continue;
                bool big = k == Game.SanctumPulses - 1;
                float rp = Mathf.Min(a / Game.SanctumPulseRing, 1f) * R;
                float fd = (big ? 1.35f : 1f) * (1f - MathX.Clamp01((a - Game.SanctumPulseRing) / 0.5f));
                if (a <= Game.SanctumPulseRing + 0.5f)
                {
                    for (int s = 0; s < 4; s++)  // the disc: stacked rings thickening toward its leading edge
                        Circle(c, Mathf.Max(1f, rp * (0.4f + 0.2f * s)), SanctumHeal.WithAlpha(0.045f * fd * da), 32);
                    Arc(c, Mathf.Max(2f, rp), 3f, new Rgba(0.9f, 1f, 0.92f, 0.85f * fd * da), 56);
                    float d = (R - rp) / 18f;
                    flare += Mathf.Exp(-d * d) * fd;
                }
                if (fxReduced)
                    continue;
                for (int i = 0; i < 18; i++)
                {
                    float sa = SanctumHash(k * 18 + i) * MathX.Tau + a * 0.7f, sv = 0.55f + 0.5f * SanctumHash(k * 18 + i + 400f);
                    float sr = Mathf.Min(a / Game.SanctumPulseRing, 1f) * R * sv + Mathf.Max(0f, a - Game.SanctumPulseRing) * 12f;
                    sr = Mathf.Min(sr, R - 4f);
                    float al = 0.9f * (1f - MathX.Clamp01(a / 1.6f)) * da;
                    SanctumSparkle(c + V2.FromAngle(sa) * sr + new V2(0f, -Mathf.Max(0f, a - Game.SanctumPulseRing) * 10f), 2.2f, new Rgba(0.86f, 1f, 0.9f, al));
                }
            }
            flare = Mathf.Min(flare, 1.6f);

            // sparkles drifting inside all the time
            if (!fxReduced)
                for (int i = 0; i < 26; i++)
                {
                    float sa = SanctumHash(i) * MathX.Tau + t * (SanctumHash(i + 9) - 0.5f) * 0.8f;
                    float sr = R * (0.15f + 0.7f * SanctumHash(i + 50)) * (0.97f + 0.03f * Mathf.Sin(t * 2f + i));
                    float al = (0.35f + 0.25f * Mathf.Sin(t * 3f + SanctumHash(i + 30) * MathX.Tau)) * da;
                    SanctumSparkle(c + V2.FromAngle(sa) * sr + new V2(0f, Mathf.Sin(t * 1.5f + i) * 4f), 1.4f, SanctumPale.WithAlpha(al));
                }

            // the shell: a rim whose colour drifts round it, flaring white as a pulse arrives
            const int N = 72;
            for (int k = 0; k < N; k++)
            {
                float a0 = MathX.Tau * k / N;
                Rgba rim = SanctumShimmer(a0 * 57.3f * 1.4f + t * 50f, 0.55f + 0.25f * flare);
                rim = new Rgba(Mathf.Lerp(rim.R, 1f, 0.3f * flare), Mathf.Lerp(rim.G, 1f, 0.3f * flare), Mathf.Lerp(rim.B, 1f, 0.3f * flare), rim.A * da);
                Arc(c, R, 3f + 2f * flare, rim, 2, a0, MathX.Tau / N + 0.03f);
            }
            Arc(c, R - 5f, 1.5f, SanctumPale.WithAlpha(0.2f * da), 56);
            Arc(c, R - R * 0.07f, R * 0.07f, new Rgba(1f, 1f, 1f, 0.3f * da), 8, -2.5f, 1.2f);  // glints
            Arc(c, R - R * 0.05f, R * 0.04f, new Rgba(1f, 1f, 1f, 0.15f * da), 4, 0.7f, 0.5f);

            // time left
            Arc(c, R + 7f, 2f, new Rgba(1f, 1f, 1f, 0.12f), 48);
            Arc(c, R + 7f, 2f, z.Def.Color.WithAlpha(0.85f), 48, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)));
        }
    }
}
