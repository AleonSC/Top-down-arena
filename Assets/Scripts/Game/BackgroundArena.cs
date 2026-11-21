using UnityEngine;

namespace Arena
{
    /// Three quiet reactive menu backgrounds: Deep Field, Nebula Drift and Aurora Veil. No lines or grids, so behind a run they
    /// never fight the arena floor's grid (FloorGridLook.cs): only soft light, stars and haze in the arena's dark navy and blue.
    /// In the menus they answer the pointer and clicks gently (BackgroundReact.cs); behind a run they only drift on their own.
    public partial class ArenaApp
    {
        void ArenaBackdrop()
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.016f, 0.024f, 0.06f), new Rgba(0.04f, 0.05f, 0.12f));
        }

        /// The pointer and how much to answer it (0 when it isn't over this view, or behind a run).
        float CalmPointer(out V2 p)
        {
            if (ReactPointer(out p))
                return 1f;
            p = new V2(W / 2f, H / 2f);
            return 0f;
        }

        /// How strongly a click ripple reaches `dist` from where it was made: a soft band travelling out, fading as it ages.
        float CalmWave(int i, float age, float dist, float speed, float width)
        {
            float life = 1f - age / BgRippleLife;
            float d = dist - age * speed;
            return Mathf.Exp(-d * d / (2f * width * width)) * life * life * bgRipples[i].Power;
        }

        // --- Deep Field ----------------------------------------------------------------------------------------------------

        /// Three layers of stars drifting slowly sideways. The pointer tilts the view a little (the near stars move the most) and
        /// brightens the stars round it; a click makes the stars it passes flare. Now and then a star falls across the top.
        void BgDeepField(float t)
        {
            ArenaBackdrop();
            float k = CalmPointer(out var p);
            var tilt = (p - new V2(W / 2f, H / 2f)) * (-0.035f * k);
            for (int i = 0; i < 170; i++)
            {
                float depth = 0.25f + 0.75f * Hash(i * 5 + 3);  // 1 is the nearest
                var at = new V2(MathX.Fmod(Hash(i * 5) * (W + 80f) + t * (2f + 9f * depth), W + 80f) - 40f, Hash(i * 5 + 1) * H) + tilt * depth;
                float tw = 0.55f + 0.45f * Mathf.Sin(t * (0.7f + 1.6f * Hash(i * 5 + 2)) + i);
                float near = k * Mathf.Clamp01(1f - (at - p).Length / 160f);
                float flare = 0f;
                for (int r = 0; r < BgRippleSlots; r++)
                    if (ReactRipple(r, t, out var c, out float age))
                        flare += CalmWave(r, age, (at - c).Length, 420f, 50f);
                float a = Mathf.Min(1f, (0.18f + 0.4f * depth) * tw + 0.35f * near + 0.6f * flare);
                float size = 0.6f + 1.2f * depth + 1.2f * flare;
                var col = new Rgba(0.72f, 0.82f, 1f).Mix(Rgba.White, near + flare);
                if (flare > 0.15f || near > 0.3f)
                    SoftGlow(at, 6f + 10f * (flare + near), col.WithAlpha(0.25f * Mathf.Min(1f, flare + near)));
                Circle(at, size, col.WithAlpha(a), 6);
            }
            // A falling star every 11 s, across the upper part of the sky.
            float fs = MathX.Fmod(t, 11f);
            if (fs < 0.9f)
            {
                int n = Mathf.FloorToInt(t / 11f);
                var from = new V2(W * (0.2f + 0.6f * Hash(n * 7 + 1)), H * (0.08f + 0.2f * Hash(n * 7 + 2)));
                var dir = new V2(1f, 0.32f).Normalized() * (Hash(n * 7 + 3) < 0.5f ? 1f : -1f);
                float f = fs / 0.9f, fade = Mathf.Sin(f * MathX.Pi);
                var head = from + dir * (f * 260f);
                for (int s = 0; s < 8; s++)
                    Line(head - dir * (s * 8f), head - dir * (s * 8f + 8f), 1.4f - s * 0.12f, new Rgba(0.8f, 0.9f, 1f, 0.5f * fade * (1f - s / 8f)));
            }
        }

        // --- Nebula Drift --------------------------------------------------------------------------------------------------

        static readonly Rgba[] NebulaCols =
        {
            new Rgba(0.2f, 0.35f, 0.95f), new Rgba(0.15f, 0.6f, 0.85f), new Rgba(0.45f, 0.3f, 0.9f), new Rgba(0.2f, 0.45f, 1f),
            new Rgba(0.3f, 0.25f, 0.8f), new Rgba(0.12f, 0.55f, 0.75f), new Rgba(0.35f, 0.4f, 1f),
        };

        /// Slow clouds of blue and violet light drifting past a few faint stars. The clouds part a little round the pointer and a
        /// soft light follows it; a click sends a slow swell of light outward.
        void BgNebulaDrift(float t)
        {
            ArenaBackdrop();
            float k = CalmPointer(out var p);
            for (int i = 0; i < 60; i++)
            {
                var s = new V2(Hash(i * 3 + 900) * W, Hash(i * 3 + 901) * H);
                Circle(s, 0.7f + Hash(i * 3 + 902), new Rgba(0.7f, 0.8f, 1f, 0.12f + 0.15f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f + i))), 6);
            }
            for (int i = 0; i < NebulaCols.Length; i++)
            {
                var c = new V2(W * (0.5f + 0.42f * Mathf.Sin(t * (0.035f + 0.012f * i) + i * 2.1f)), H * (0.5f + 0.4f * Mathf.Sin(t * (0.05f + 0.01f * i) + i * 1.3f)));
                var away = c - p;
                float dist = Mathf.Max(1f, away.Length);
                c += away / dist * (70f * k * Mathf.Exp(-dist * dist / (2f * 260f * 260f)));
                float breathe = 0.85f + 0.15f * Mathf.Sin(t * 0.3f + i);
                SoftGlow(c, (260f + 160f * Hash(i * 11 + 5)) * breathe, NebulaCols[i].WithAlpha(0.11f + 0.05f * Hash(i * 11 + 6)));
            }
            for (int r = 0; r < BgRippleSlots; r++)
                if (ReactRipple(r, t, out var at, out float age))
                {
                    float life = 1f - age / BgRippleLife;
                    SoftGlow(at, 60f + age * 260f, new Rgba(0.5f, 0.75f, 1f, 0.16f * life * life * bgRipples[r].Power));
                }
            if (k > 0f)
                SoftGlow(p, 170f + 90f * ReactEnergy, new Rgba(0.55f, 0.75f, 1f, 0.06f + 0.06f * ReactEnergy));
        }

        // --- Aurora Veil ---------------------------------------------------------------------------------------------------

        /// Soft curtains of light along the top and bottom edges, leaving the middle calm and dark. They brighten and lean toward
        /// the pointer when it comes near; a click sends a shimmer running along them both ways.
        void BgAuroraVeil(float t)
        {
            ArenaBackdrop();
            float k = CalmPointer(out var p);
            const float step = 16f;
            for (int band = 0; band < 2; band++)
            {
                bool top = band == 0;
                float prevX = 0f, prevEdge = 0f, prevGlow = 0f;
                Rgba prevCol = default;
                for (float x = 0f; x <= W + step; x += step)
                {
                    float wave = 70f + 26f * Mathf.Sin(x * 0.006f + t * 0.35f + band * 2f) + 14f * Mathf.Sin(x * 0.015f - t * 0.6f + band);
                    float nearX = k * Mathf.Exp(-(x - p.X) * (x - p.X) / (2f * 170f * 170f));
                    float nearY = top ? Mathf.Clamp01(1f - p.Y / (H * 0.5f)) : Mathf.Clamp01((p.Y - H * 0.5f) / (H * 0.5f));
                    float lean = nearX * nearY;
                    wave += 40f * lean;
                    float shimmer = 0f;
                    for (int r = 0; r < BgRippleSlots; r++)
                        if (ReactRipple(r, t, out var at, out float age))
                            shimmer += CalmWave(r, age, Mathf.Abs(x - at.X), 520f, 60f);
                    float glow = Mathf.Min(1f, 0.2f + 0.1f * Mathf.Sin(x * 0.02f + t * 1.1f + band * 3f) + 0.35f * lean + 0.45f * shimmer);
                    float u = x / W;
                    var col = new Rgba(0.15f, 0.75f, 0.85f).Mix(new Rgba(0.3f, 0.4f, 1f), u).Mix(new Rgba(0.5f, 0.35f, 0.95f), Mathf.Max(0f, u - 0.6f) * 1.5f);
                    float edge = top ? wave : H - wave;
                    if (x > 0f)
                    {
                        float y0 = top ? 0f : H;
                        var clear = col.WithAlpha(0f);
                        // From the screen edge (faint) to the curtain's lower rim (brightest), then a short fade past it.
                        VTri(new V2(prevX, y0), prevCol.WithAlpha(0.05f * prevGlow), new V2(x, y0), col.WithAlpha(0.05f * glow), new V2(x, edge), col.WithAlpha(0.5f * glow));
                        VTri(new V2(prevX, y0), prevCol.WithAlpha(0.05f * prevGlow), new V2(x, edge), col.WithAlpha(0.5f * glow), new V2(prevX, prevEdge), prevCol.WithAlpha(0.5f * prevGlow));
                        float d = top ? 34f : -34f;
                        VTri(new V2(prevX, prevEdge), prevCol.WithAlpha(0.5f * prevGlow), new V2(x, edge), col.WithAlpha(0.5f * glow), new V2(x, edge + d), clear);
                        VTri(new V2(prevX, prevEdge), prevCol.WithAlpha(0.5f * prevGlow), new V2(x, edge + d), clear, new V2(prevX, prevEdge + d), clear);
                    }
                    prevX = x; prevEdge = edge; prevGlow = glow; prevCol = col;
                }
            }
            Stars(50, t, 0.3f, 7300);
        }
    }
}
