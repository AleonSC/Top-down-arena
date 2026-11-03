using System;
using UnityEngine;

namespace Arena
{
    /// The Repulsion Cannon as a cannon blast (Docs/Mockups/repulsion-cannon.html, look D), with Visual effects on: a white
    /// muzzle star, smoke billowing through the cone, a crisp shock ring, sparks, and a very small screen kick (Juice.cs).
    /// Drawing only; the cone, shove and orbs are in Repulsion.cs.
    public partial class ArenaApp
    {
        /// FxKind.Repulse. f is how far through the effect's life it is (Game.RepulseFxTime).
        void DrawRepulseBlast(Effect fx, float f)
        {
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, reach = mid.Length, half = fx.Radius;
            float t = f * Game.RepulseFxTime;  // seconds since the shot
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)(aim * 1000f) * 83492791) | 1u;
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            var smoke = new Rgba(0.8f, 0.92f, 0.98f);
            // Smoke: soft puffs thrown out through the cone, growing as they thin.
            for (int i = 0; i < 26; i++)
            {
                float a = aim + (rnd() * 2f - 1f) * half * 0.92f, sp = 0.6f + rnd() * 0.55f, born = rnd() * 0.06f, life = 0.4f + rnd() * 0.2f, size = rnd();
                float u = (t - born) / life;
                if (u <= 0f || u >= 1f)
                    continue;
                float d = 14f + (reach - 40f) * (1f - Mathf.Pow(1f - u, 3f)) * sp, rad = 12f + 22f * u + 8f * size, al = 0.3f * (1f - u);
                V2 at = c + V2.FromAngle(a) * d;
                Circle(at, rad, smoke.WithAlpha(al * 0.35f), 14);  // soft: three stacked discs
                Circle(at, rad * 0.7f, smoke.WithAlpha(al * 0.4f), 14);
                Circle(at, rad * 0.4f, smoke.WithAlpha(al * 0.5f), 12);
            }
            // The shock ring: thin and crisp.
            float g = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.16f), 3f), ringFade = 1f - Mathf.Clamp01((t - 0.1f) / 0.2f);
            if (ringFade > 0f)
            {
                float rr = 24f + (reach - 24f) * g;
                Arc(c, rr, 9f, fx.Color.WithAlpha(0.22f * ringFade), 16, aim - half, 2f * half);
                Arc(c, rr, 3f, new Rgba(0.92f, 0.99f, 1f, 0.9f * ringFade), 16, aim - half, 2f * half);
            }
            // The muzzle star.
            if (t < 0.11f)
            {
                float k = 1f - t / 0.11f;
                Circle(c, 56f, fx.Color.WithAlpha(0.12f * k), 18);
                Circle(c, 38f, new Rgba(0.67f, 0.94f, 1f, 0.25f * k), 18);
                Circle(c, 20f, Rgba.White.WithAlpha(0.7f * k), 16);
                SetColor(Rgba.White.WithAlpha(0.9f * k));
                for (int i = 0; i < 7; i++)
                {
                    float a = aim + (i / 6f - 0.5f) * half * 2.1f;
                    Quad(c, c + V2.FromAngle(a - 0.07f) * (70f * k), c + V2.FromAngle(a) * (96f * k), c + V2.FromAngle(a + 0.07f) * (70f * k));
                }
            }
            // Sparks, with trails.
            for (int i = 0; i < 20; i++)
            {
                float a = aim + (rnd() * 2f - 1f) * half, sp = 0.5f + rnd() * 0.5f, u = t / (0.2f + rnd() * 0.15f), tail = 16f + 16f * rnd();
                if (u >= 1f)
                    continue;
                float d = 20f + (reach + 10f) * (1f - Mathf.Pow(1f - u, 3f)) * sp;
                Line(c + V2.FromAngle(a) * Mathf.Max(0f, d - tail), c + V2.FromAngle(a) * d, 1.6f, Rgba.White.WithAlpha(1f - u));
            }
        }
    }
}
