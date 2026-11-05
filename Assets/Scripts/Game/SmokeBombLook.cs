using UnityEngine;

namespace Arena
{
    /// The Ninja's Smoke Bomb, with Visual effects on (Docs/Mockups/ninja-smoke-bomb.html): a crack (flash, amber sparks and a
    /// shockwave ring) as it lands, then a cloud that blooms out in layered puffs with a lit upper rim, outer puffs turning
    /// faster than inner ones, thin wisps circling. An amber seal ring drains with the time left and blinks in the last 1.2 s,
    /// when the edge frays and the puffs lift and thin. Drawing only: everything comes from the zone.
    public partial class ArenaApp
    {
        const int SmokePuffs = 40;
        static readonly Rgba SmokeBody = new Rgba(0.26f, 0.235f, 0.38f), SmokeLit = new Rgba(0.75f, 0.73f, 0.93f), SmokeAmber = new Rgba(1f, 0.72f, 0.29f);

        /// The capstone: a slow violet undertow turning against the cloud, and curls of smoke dragging at each enemy inside it.
        void SmokeCling(Zone z, V2 c, float R, float age, float k)
        {
            Arc(c, R * 0.55f, 3f, SmokeAmber.WithAlpha(0.22f * k), 40, -age * 0.35f, MathX.Tau * 0.7f);
            Arc(c, R * 0.8f, 2f, SmokeLit.WithAlpha(0.2f * k), 48, -age * 0.25f + 2f, MathX.Tau * 0.55f);
            foreach (var e in game.Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Disp.DistanceTo(c) > R + e.Radius)
                    continue;
                float r = e.Radius + 5f;
                for (int j = 0; j < 3; j++)  // three curls turning slowly round it
                {
                    float a = age * 1.1f + j * MathX.Tau / 3f + e.Id;
                    Arc(e.Disp, r + j * 2.5f, 2.5f, SmokeLit.WithAlpha((0.55f - 0.12f * j) * k), 10, a, 1.2f);
                }
                Circle(e.Disp + new V2(0f, e.Radius * 0.5f), e.Radius * 1.1f, SmokeBody.WithAlpha(0.35f * k), 14);  // weight pooled at its feet
                for (int j = 0; j < 3; j++)  // drag: motes that trail behind and sink
                {
                    float f = MathX.Fmod(age * 0.9f + j * 0.33f + e.Id * 0.17f, 1f);
                    Circle(e.Disp + new V2(Mathf.Sin(f * 6f + j) * 6f, e.Radius * 0.3f + f * 14f), 2.2f * (1f - f), SmokeLit.WithAlpha(0.6f * (1f - f) * k), 6);
                }
            }
        }

        void DrawSmokeCloud(Zone z, float t)
        {
            float R0 = z.Radius, age = z.Duration - z.T;
            float fade = Mathf.Clamp01(z.T / 1.2f);                                  // the last 1.2 s
            float grow = EaseOut3(Mathf.Clamp01(age / 0.65f));
            float R = R0 * grow;
            V2 c = z.Pos;
            float seed = z.Id * 1.7f;

            // ground shade
            Circle(c, R, new Rgba(0.13f, 0.12f, 0.22f, 0.3f * fade), 40);
            Circle(c, R * 0.7f, new Rgba(0.13f, 0.12f, 0.22f, 0.22f * fade), 32);

            // puffs: a dark body with a lit rim offset toward the upper left
            for (int k = 0; k < SmokePuffs; k++)
            {
                float del = Hash01(seed + k * 3.1f) * 0.18f;
                float e = EaseOut3(Mathf.Clamp01((age - del) / 0.65f));
                if (e <= 0f)
                    continue;
                float rad = Mathf.Sqrt(Hash01(seed + k * 1.3f)) * 0.88f;
                float spin = (Hash01(seed + k * 7.7f) - 0.5f) * 0.55f * (1.25f - rad);
                float size = R0 * (0.2f + Hash01(seed + k * 5.1f) * 0.2f) * (0.4f + 0.6f * e) * (1f + (1f - fade) * 0.12f);
                float ang = Hash01(seed + k * 2.3f) * MathX.Tau + spin * age;
                float ph = Hash01(seed + k * 9.9f) * MathX.Tau;
                V2 at = c + V2.FromAngle(ang) * (rad * R0 * e * (1f + (1f - fade) * 0.18f))
                    + new V2(Mathf.Sin(age * 0.7f + ph) * 3f, Mathf.Cos(age * 0.6f + ph) * 3f - (1f - fade) * 14f);
                float a = Mathf.Clamp01(e * 1.6f) * fade;
                Circle(at, size, SmokeBody.WithAlpha(0.2f * a), 16);
                Circle(at, size * 0.72f, SmokeBody.WithAlpha(0.28f * a), 16);
                Circle(at, size * 0.4f, SmokeBody.WithAlpha(0.3f * a), 12);
                V2 lit = at + new V2(-size * 0.22f, -size * 0.3f);
                Circle(lit, size * 0.55f, SmokeLit.WithAlpha(0.16f * a), 14);
                Circle(lit, size * 0.3f, SmokeLit.WithAlpha(0.14f * a), 10);
            }

            // thin wisps circling
            for (int k = 0; k < 9; k++)
            {
                float a0 = Hash01(seed + k * 4.4f) * MathX.Tau + age * (0.2f + Hash01(seed + k) * 0.35f);
                float r = (0.25f + Hash01(seed + k * 6.6f) * 0.65f) * R;
                Arc(c, r, 2f, SmokeLit.WithAlpha(0.18f * fade * grow), 20, a0, 0.5f + Hash01(seed + k * 8.8f) * 0.9f);
            }

            // seal ring: a dashed base and an amber arc that drains with the time left
            float frac = Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            float blink = z.T < 1.2f ? 0.55f + 0.45f * Mathf.Sin(age * 18f) : 1f;
            for (int k = 0; k < 24; k++)
                Arc(c, R - 2f, 1.5f, SmokeLit.WithAlpha(0.35f * grow), 4, MathX.Tau * k / 24f, MathX.Tau / 48f);
            Arc(c, R - 2f, 3f, SmokeAmber.WithAlpha(0.9f * blink * grow), 64, -MathX.Pi / 2f, MathX.Tau * frac);
            for (int k = 0; k < 8; k++)
            {
                V2 d = V2.FromAngle(MathX.Tau * k / 8f + age * 0.15f);
                Line(c + d * (R - 9f), c + d * (R - 15f), 2f, SmokeAmber.WithAlpha(0.5f * grow));
            }

            // mastered: a heavier, slower cloud, and smoke clings to every enemy inside it (its 40% slow)
            if (z.GuardFrac > 0f)
                SmokeCling(z, c, R, age, fade * grow);

            // the crack: flash, sparks, shockwave
            if (age < 0.55f)
            {
                float f = 1f - age / 0.55f;
                if (age < 0.3f)
                    Circle(c, R0 * 0.5f * (0.5f + age * 3f), new Rgba(0.94f, 0.93f, 1f, 0.55f * (1f - age / 0.3f)), 32);
                for (int k = 0; k < 14; k++)
                {
                    V2 d = V2.FromAngle(Hash01(seed + k * 2.9f) * MathX.Tau);
                    float v = 120f + Hash01(seed + k * 3.7f) * 190f, d1 = v * age;
                    Line(c + d * Mathf.Max(0f, d1 - 12f), c + d * d1, 2f, SmokeAmber.WithAlpha(f));
                }
                Arc(c, R0 * 1.08f * EaseOut3(Mathf.Clamp01(age / 0.45f)), 3f * f + 1f, new Rgba(0.86f, 0.84f, 1f, 0.8f * f), 64);
            }
        }
    }
}
