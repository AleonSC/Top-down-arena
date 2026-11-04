using UnityEngine;

namespace Arena
{
    /// Flame Wall in style B, "Magma ring" (Docs/Mockups/flame-wall-rework.html), with Visual effects on: the ground splits into
    /// a scorched stone band with glowing lava veins that flash outward from the side nearest the caster, low flames along its
    /// edge, smoke and embers rising. A rim round the outside drains with the wall's time and an inner arc with what it can
    /// still block; the rock flares where an enemy shot is about to hit it. Once the guard is spent the veins dim and the
    /// flames sink to 45% (as before). Drawing only: everything comes from the zone, its owner and the bullets this screen has.
    public partial class ArenaApp
    {
        static readonly Rgba MagmaHot = new Rgba(1f, 0.89f, 0.47f);

        void DrawMagmaWall(Zone z, float t)
        {
            float R = z.Radius, age = z.Duration - z.T;
            float A = Mathf.Clamp01(z.T / 0.6f);
            float lowK = z.GuardFrac > 0f ? 1f : 0.45f;
            V2 c = z.Pos;
            float face = MathX.Pi;  // the side nearest the caster lights first
            if (game.Players.TryGetValue(z.Owner, out var owner))
            {
                V2 d = owner.Disp - c;
                face = Mathf.Atan2(d.Y, d.X);
            }
            float band = EaseOut3(Mathf.Clamp01(age / 0.3f)) * A;

            // scorched ground and the stone band
            Circle(c, R + 6f, new Rgba(0.1f, 0.05f, 0.04f, 0.6f * A), 48);
            Circle(c, R * 0.8f, new Rgba(0.35f, 0.12f, 0.05f, 0.14f * A), 40);
            Arc(c, R, 22f, new Rgba(0.1f, 0.05f, 0.04f, band), 64);
            Arc(c, R + 10f, 2f, new Rgba(0.27f, 0.16f, 0.11f, 0.6f * band), 64);
            Arc(c, R - 10f, 2f, new Rgba(0.27f, 0.16f, 0.11f, 0.6f * band), 64);
            for (int i = 0; i < 54; i++)
            {
                float a = i / 54f * MathX.Tau, rr = R + (Hash01(i) - 0.5f) * 14f;
                Circle(c + V2.FromAngle(a) * rr, 1.6f + Hash01(i + 1f) * 1.4f, new Rgba(0.24f, 0.15f, 0.12f, band), 5);
            }

            // lava veins, flashing out from the caster's side
            for (int i = 0; i < 52; i++)
            {
                float a = i / 52f * MathX.Tau + Hash01(i) * 0.1f;
                float dd = Mathf.Abs(Mathf.Atan2(Mathf.Sin(a - face), Mathf.Cos(a - face)));
                float reveal = Mathf.Clamp01((age - 0.05f - dd * 0.1f) / 0.2f);
                if (reveal <= 0f)
                    continue;
                float len = 0.07f + Hash01(i + 2f) * 0.1f, pul = (0.55f + 0.45f * Mathf.Sin(t * 3f + i)) * lowK;
                Arc(c, R + (Hash01(i + 1f) - 0.5f) * 10f, 2f + Hash01(i + 3f) * 2.2f, new Rgba(1f, 0.39f + Hash01(i) * 0.35f, 0.08f, pul * A * reveal), 4, a, len);
            }

            // low flames along the edge
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * MathX.Tau + 0.05f;
                float dd = Mathf.Abs(Mathf.Atan2(Mathf.Sin(a - face), Mathf.Cos(a - face)));
                float grow = EaseOut3(Mathf.Clamp01((age - 0.3f * dd / MathX.Pi) / 0.18f)) * lowK;
                if (grow <= 0f)
                    continue;
                float h = (9f + 6f * Mathf.Sin(t * 10f + i)) * grow * A, sw = Mathf.Sin(t * 9f + i * 1.7f) * 2.2f;
                V2 dir = V2.FromAngle(a), b0 = c + dir * R, n = dir.Orthogonal();
                SetColor(new Rgba(1f, 0.47f + Hash01(i) * 0.31f, 0.12f, 0.9f * A));
                Tri(b0 + n * 5f, b0 - n * 5f, b0 + dir * h + n * sw);
            }

            // smoke and embers rising
            for (int k = 0; k < 10; k++)
            {
                float p = MathX.Fmod(t * 0.35f + Hash01(k), 1f), a = Hash01(k + 4f) * MathX.Tau;
                GlowDisc(c + V2.FromAngle(a) * R * 0.9f + new V2(0f, -p * 70f), 14f + p * 10f, new Rgba(0.27f, 0.24f, 0.24f), 0.28f * (1f - p) * A);
            }
            for (int k = 0; k < 14; k++)
            {
                float p = MathX.Fmod(t * 0.7f + Hash01(k + 20f), 1f), a = Hash01(k + 24f) * MathX.Tau;
                V2 at = c + V2.FromAngle(a) * R * (0.9f + 0.12f * Hash01(k + 2f)) + new V2(Mathf.Sin(t * 3f + k) * 6f, -p * 50f);
                Circle(at, 2.4f * (1f - p) + 0.6f, new Rgba(1f, 0.7f + 0.2f * p, 0.3f, (1f - p) * A * lowK), 6);
            }

            // where an enemy shot is about to hit the rock
            if (z.GuardFrac > 0f)
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && Mathf.Abs(b.Pos.DistanceTo(c) - R) < 34f)
                    {
                        V2 off = b.Pos - c;
                        Arc(c, R, 7f, MagmaHot.WithAlpha(0.9f * A), 8, Mathf.Atan2(off.Y, off.X) - 0.55f, 1.1f);
                    }

            // time left (outside) and what it can still block (inside)
            Arc(c, R + 16f, 2f, new Rgba(1f, 0.54f, 0.16f, 0.12f * A), 64);
            Arc(c, R + 16f, 2.5f, MagmaHot.WithAlpha(0.85f * A), 64, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)));
            Arc(c, R - 16f, 3.5f, new Rgba(0.35f, 0.27f, 0.24f, 0.35f * A), 48);
            if (z.GuardFrac > 0f)
                Arc(c, R - 16f, 3.5f, new Rgba(1f, 0.93f, 0.67f, 0.9f * A), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(z.GuardFrac));
        }
    }
}
