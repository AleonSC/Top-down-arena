using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        static float OrbHash(float n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - (float)System.Math.Floor(s); }

        /// A thin ellipse outline (rx, ry), drawn as short lines.
        void EllipseLine(V2 c, float rx, float ry, float w, Rgba col, int segs = 16)
        {
            V2 prev = c + new V2(rx, 0f);
            for (int i = 1; i <= segs; i++)
            {
                float a = MathX.Tau * i / segs;
                V2 next = c + new V2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Line(prev, next, w, col);
                prev = next;
            }
        }

        /// A health orb as a projected hologram (Mockups/digital-health-orbs.html, look C): a see-through
        /// wireframe sphere with scrolling latitude lines, a turning meridian, a flat white plus that
        /// glitches now and then, and a ring of ticks. Big orbs keep the gold ring; tiny ones are paler.
        void DrawHoloOrb(Orb o, float t)
        {
            var col = o.Tiny ? new Rgba(0.55f, 1f, 0.75f) : new Rgba(0.35f, 1f, 0.5f);
            float r = o.Radius * 1.1f * (1f + 0.05f * Mathf.Sin(t * 8f + o.Id));
            float lw = o.Big ? 1.6f : 1.2f;
            V2 c = o.Disp;
            Circle(c, r * 2f, col.WithAlpha(0.08f), 20);
            Circle(c, r, col.WithAlpha(0.18f), 16);
            // scrolling latitude rings, squashed so the sphere reads as seen from above
            float ph = MathX.Fmod(t * 0.6f, 1f);
            for (int i = -3; i <= 3; i++)
            {
                float f = (i + ph) / 3.5f;
                float w = Mathf.Sqrt(Mathf.Max(0f, 1f - f * f)) * r;
                if (w > 0.5f)
                    EllipseLine(c + new V2(0f, f * r), w, w * 0.28f, lw, col.WithAlpha(0.75f), 10);
            }
            EllipseLine(c, r * Mathf.Abs(Mathf.Cos(t * 1.4f)), r, lw, col.WithAlpha(0.55f), 12);
            Arc(c, r, lw * 1.2f, col, 16);
            // the plus; a rare sideways glitch
            float sh = OrbHash((float)System.Math.Floor(t * 6f) + o.Id) > 0.9f ? (OrbHash(o.Id + t) * 4f - 2f) : 0f;
            float u = r * 0.2f;
            FillRect(c.X - u * 0.6f + sh, c.Y - u * 2f, u * 1.2f, u * 4f, Rgba.White);
            FillRect(c.X - u * 2f + sh, c.Y - u * 0.6f, u * 4f, u * 1.2f, Rgba.White);
            if (!o.Tiny)
            {
                float rr = r * 1.6f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * MathX.Tau + t * 0.8f, l = i % 3 == 0 ? 0.18f : 0.1f;
                    Line(c + V2.FromAngle(a) * rr, c + V2.FromAngle(a) * rr * (1f + l), lw, col.WithAlpha(0.8f));
                }
            }
            if (o.Big)
                Arc(c, r + 5f, 2f, new Rgba(1f, 0.95f, 0.5f, 0.85f), 24);
        }
    }
}
