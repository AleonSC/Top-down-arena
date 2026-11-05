using UnityEngine;

namespace Arena
{
    /// Hallowed Blade in look B, "Pillar of Light" (Docs/Mockups/hallowed-blade-rework.html): a sword of light drops down a thin
    /// beam, lands, and stays planted with the beam rising to the sky. A faint floor ring turns around it and motes spiral in
    /// toward the blade. Each pulse (Core/HallowedBlade.cs) is three ripples (gold, white, mint) with diamond sparks on the
    /// front that cross the whole arena; the beam swells as one leaves. Drawing only: pulses are worked out from the zone's
    /// replicated timer. With Visual effects off it is a plain ring, the sword and plain ripples; Reduced effects drops the
    /// motes and sparks.
    public partial class ArenaApp
    {
        static readonly Rgba HbGold = new Rgba(1f, 0.84f, 0.43f), HbWhite = new Rgba(1f, 0.97f, 0.88f), HbMint = new Rgba(0.47f, 1f, 0.69f);

        /// The sword of light, point down at `tip`, `s` times the mockup's size, `a` visible, `glow` 0..1.
        void DrawHbSword(V2 tip, float s, float a, float glow, float t)
        {
            System.Func<float, float, V2> P = (x, y) => tip + new V2(x * s, -y * s);
            if (fxOn)
                GlowDisc(P(0f, 95f), 150f * s, HbGold, 0.32f * glow * a);
            Quad(P(-18f, 128f), P(18f, 128f), P(10f, 10f), P(-10f, 10f), HbGold.WithAlpha(0.3f * a));
            Rgba body = new Rgba(1f, 0.93f, 0.7f, a);
            Quad(P(0f, 0f), P(-5f, 14f), P(5f, 14f), P(0f, 0f), body);
            Quad(P(-5f, 14f), P(5f, 14f), P(14f, 46f), P(-14f, 46f), body);
            Quad(P(-14f, 46f), P(14f, 46f), P(14f, 128f), P(-14f, 128f), body);
            Line(P(0f, 12f), P(0f, 126f), 8f * s, HbWhite.WithAlpha(a));
            float gy = MathX.Fmod(t * 0.35f, 1f) * 118f;
            Line(P(-10f, gy), P(10f, gy + 10f), 3f * s, Rgba.White.WithAlpha(0.6f * a));
            Quad(P(-38f, 141f), P(38f, 141f), P(38f, 128f), P(-38f, 128f), new Rgba(1f, 0.92f, 0.6f, a));
            Quad(P(-38f, 141f), P(-48f, 155f), P(-30f, 141f), P(-38f, 141f), new Rgba(1f, 0.92f, 0.6f, a));
            Quad(P(38f, 141f), P(48f, 155f), P(30f, 141f), P(38f, 141f), new Rgba(1f, 0.92f, 0.6f, a));
            Line(P(0f, 142f), P(0f, 170f), 9f * s, new Rgba(0.35f, 0.23f, 0.1f, a));
            Circle(P(0f, 178f), 9f * s, new Rgba(1f, 0.92f, 0.6f, a), 14);
            Circle(P(0f, 178f), 4.2f * s, HbMint.WithAlpha(a), 10);
            if (fxOn)
                GlowDisc(P(0f, 178f), 22f * s, HbMint, 0.35f * a);
        }

        /// The sword while it falls (the 0.6 s `HolySword` zone): a filling gold circle, a beam above the spot, the sword coming down it.
        void DrawHolySwordFall(Zone z, float t)
        {
            float p = 1f - MathX.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            V2 c = z.Pos;
            float R = z.Radius * 0.85f;
            Circle(c, R * p, HbGold.WithAlpha(0.16f * p), 40);
            Arc(c, R, 3f, HbGold.WithAlpha(MathX.Fmod(t, 0.2f) < 0.1f ? 0.9f : 0.5f), 48);
            Circle(c, 10f + 34f * p, new Rgba(0f, 0f, 0f, 0.3f * p), 16);
            Line(c, c + new V2(0f, -700f), 6f + 14f * p, HbWhite.WithAlpha(0.18f * p));
            float off = -(1f - p * p * p) * 560f;
            if (fxOn)
                for (int i = 0; i < 6; i++)  // speed lines above the blade
                {
                    float len = (60f + SanctumHash(i) * 90f) * (1f - p) + 10f;
                    V2 top = c + new V2(-20f + i * 8f, off - 220f);
                    Line(top, top + new V2(0f, -len), 1.5f, HbWhite.WithAlpha(0.35f));
                }
            DrawHbSword(c + new V2(0f, off), 1.18f, 1f, 0.6f, t);
        }

        void DrawHallowedBlade(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float da = MathX.Clamp01(age / 0.3f) * MathX.Clamp01((Game.HallowedRingTime + 0.5f - age) / 0.6f);
            float R = z.Radius, reach = Game.HallowedReach(z.Pos);
            V2 c = z.Pos;

            float surge = 0f;
            for (int k = 0; k < Game.HallowedPulses; k++)
            {
                float a = age - Game.HallowedPulseAt(k);
                if (a >= 0f)
                    surge = Mathf.Max(surge, Mathf.Exp(-a * 2.4f));
            }

            if (da > 0f)
            {
                // the floor: a soft pale light, the rim, and (with effects) turning rings and spokes
                GlowDisc(c, R, HbWhite, 0.22f * da);
                Circle(c, R, HbGold.WithAlpha(0.07f * da), 48);
                Arc(c, R - 3f, 2f, HbGold.WithAlpha(0.7f * da), 56);
                if (fxOn && !fxReduced)
                {
                    HbDashes(c, R * 0.9f, 2f, HbGold.WithAlpha(0.55f * da), 56, 0.25f, t * 0.4f);
                    HbDashes(c, R * 0.62f, 2.5f, HbWhite.WithAlpha(0.5f * da), 20, 0.57f, -t * 0.5f);
                    for (int i = 0; i < 12; i++)
                    {
                        V2 d = V2.FromAngle(MathX.Tau * i / 12f + t * 0.12f);
                        Line(c + d * (R * 0.18f), c + d * (R * 0.6f), 1.2f, HbGold.WithAlpha(0.22f * da));
                    }
                    for (int i = 0; i < 46; i++)  // motes drawn in toward the blade: the healing
                    {
                        float ph = MathX.Fmod(t * 0.3f + SanctumHash(i + 300f), 1f);
                        float ang = SanctumHash(i) * MathX.Tau + ph * 2.4f, r = R * 0.92f * (1f - ph);
                        V2 at = c + V2.FromAngle(ang) * r + new V2(0f, -ph * 26f);
                        Circle(at, 1.8f, (i % 3 == 0 ? HbWhite : HbMint).WithAlpha(Mathf.Sin(ph * MathX.Pi) * 0.85f * da), 6);
                    }
                }
                else
                    Arc(c, R * 0.62f, 2f, HbWhite.WithAlpha(0.4f * da), 40);

                // the beam: layered columns that swell as a pulse leaves
                float w = 9f + 16f * surge + Mathf.Sin(t * 5f);
                for (int i = 0; i < 4; i++)
                {
                    float wi = w * (2f - i * 0.45f), al = (i == 3 ? 0.55f + 0.35f * surge : 0.1f) * da;
                    Line(c + new V2(0f, -60f), c + new V2(0f, -900f), wi, (i == 3 ? HbWhite : HbGold).WithAlpha(al));
                }
                if (fxOn)
                    GlowDisc(c, 52f + 24f * surge, HbWhite, 0.5f * da);
                DrawHbSword(c, 1.18f, da, 0.4f + 0.6f * surge, t);
            }

            // the pulses
            for (int k = 0; k < Game.HallowedPulses; k++)
            {
                float a = age - Game.HallowedPulseAt(k);
                if (a < 0f || a > Game.HallowedPulseRing + 0.3f)
                    continue;
                bool big = k == Game.HallowedPulses - 1;
                float q = MathX.Clamp01(a / Game.HallowedPulseRing), r = reach * q;
                float fade = 1f - MathX.Clamp01((a - Game.HallowedPulseRing) / 0.3f);
                float al = (1f - 0.55f * q) * fade;
                Rgba[] cols = { HbGold, HbWhite, HbMint };
                for (int i = 0; i < 3; i++)
                {
                    float rr = r - i * 16f;
                    if (rr < 8f || (!fxOn && i > 0))
                        continue;
                    Arc(c, rr, ((big ? 8f : 5.5f) - i * 1.2f) * (1f - 0.4f * q) + 1.2f, cols[i].WithAlpha((i == 0 ? 1f : 0.9f - i * 0.1f) * al), (int)Mathf.Clamp(rr / 8f, 24f, 96f));
                }
                if (fxOn && !fxReduced)
                {
                    int n = (int)Mathf.Clamp(r / 7f, 16f, 90f);
                    for (int i = 0; i < n; i++)
                        Polygon(c + V2.FromAngle(MathX.Tau * i / n + k * 0.3f) * r, 3.2f, 4, MathX.Pi / 4f, HbWhite.WithAlpha(0.9f * al));
                }
            }
        }

        void HbDashes(V2 c, float r, float w, Rgba col, int n, float fill, float rot)
        {
            float step = MathX.Tau / n;
            for (int i = 0; i < n; i++)
                Arc(c, r, w, col, 2, rot + i * step, step * fill);
        }
    }
}
