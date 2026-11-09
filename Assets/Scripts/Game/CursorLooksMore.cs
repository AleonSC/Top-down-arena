using UnityEngine;

namespace Arena
{
    /// Eight more crosshairs (ranks 80 to 87, looks 11 to 18): Radar Sweep, Sniper Scope, Chevron Snap, Sonar Ping, Pixel Plus,
    /// Petal Ring, Laser Dot and Target Lock. Called from DrawCursorLook (CursorLooks.cs); their corner pointers are the cases
    /// 11 to 18 of DrawLookPointer. Local only, nothing on the wire. Mockup: Docs/Mockups/cursors-more.html.
    public partial class ArenaApp
    {
        void CurRadar(V2 m, float t, float f)
        {
            var g = new Rgba(0.42f, 1f, 0.6f);
            float r = 12f + f * 5f, a = t * 4f;
            Arc(m, r, 1.6f, g, 20);
            Arc(m, r * 0.5f, 1.2f, g.WithAlpha(0.4f), 14);
            for (int k = 0; k < 10; k++)
                Arc(m, r * 0.5f, r, g.WithAlpha(0.35f * (1f - k / 10f)), 3, a - (k + 1) * 0.09f, 0.09f);
            Line(m, m + V2.FromAngle(a) * r, 1.6f, new Rgba(0.78f, 1f, 0.88f));
            for (int i = 0; i < 2; i++)
            {
                float an = TrailHash(i + 3) * MathX.Tau, rd = r * (0.4f + 0.5f * TrailHash(i + 8)), since = MathX.Fmod(a - an, MathX.Tau);
                Circle(m + V2.FromAngle(an) * rd, 2f, new Rgba(0.91f, 1f, 0.94f, Mathf.Max(0f, 1f - since / 4f)), 6);
            }
            Circle(m, 1.6f, new Rgba(0.78f, 1f, 0.88f), 8);
        }

        void CurSniper(V2 m, float f)
        {
            var w = new Rgba(0.91f, 0.94f, 0.97f, 0.95f);
            float r = 16f - f * 3f, lw = 1.2f + f * 0.8f;
            Arc(m, r, lw, w, 22);
            Line(m - new V2(r + 8f, 0), m - new V2(4f, 0), lw, w);
            Line(m + new V2(4f, 0), m + new V2(r + 8f, 0), lw, w);
            Line(m - new V2(0, r + 8f), m - new V2(0, 4f), lw, w);
            Line(m + new V2(0, 4f), m + new V2(0, r + 8f), lw, w);
            for (int i = 1; i <= 2; i++)
            {
                float o = 4f + i * 4f;
                Circle(m + new V2(-o, 0), 0.9f, w, 5); Circle(m + new V2(o, 0), 0.9f, w, 5);
                Circle(m + new V2(0, -o), 0.9f, w, 5); Circle(m + new V2(0, o), 0.9f, w, 5);
            }
            Circle(m, 5f, new Rgba(1f, 0.16f, 0.16f, 0.25f), 10);
            Circle(m, 1.8f, new Rgba(1f, 0.3f, 0.3f), 8);
        }

        void CurChevron(V2 m, float t, float f)
        {
            var c = new Rgba(1f, 0.42f, 0.84f);
            float d = 10f + 3f * Mathf.Sin(t * 3.33f) - f * 5f;
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathX.Pi / 2f;
                V2 p0 = m + Rot(new V2(d + 6f, -5f), a), p1 = m + Rot(new V2(d, 0f), a), p2 = m + Rot(new V2(d + 6f, 5f), a);
                Line(p0, p1, 2.2f, c);
                Line(p1, p2, 2.2f, c);
            }
            Circle(m, 1.8f, new Rgba(1f, 0.88f, 0.96f), 8);
        }

        void CurSonar(V2 m, float t, float f)
        {
            var c = new Rgba(0.5f, 0.91f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float ph = MathX.Fmod(t / 1.4f + i / 3f, 1f);
                Arc(m, 3f + ph * (20f + f * 6f), 1.6f, c.WithAlpha((1f - ph) * 0.8f), 26);
            }
            Circle(m, 5f, c.WithAlpha(0.3f), 10);
            Circle(m, 2.4f, new Rgba(0.91f, 0.98f, 1f), 8);
        }

        void CurPixel(V2 m, float t, float f)
        {
            var pal = new[] { new Rgba(1f, 0.3f, 0.43f), new Rgba(1f, 0.82f, 0.4f), new Rgba(0.02f, 0.84f, 0.63f), new Rgba(0.3f, 0.79f, 0.94f) };
            const float u = 3f;
            int n2 = 4 + Mathf.RoundToInt(f * 2f), st = Mathf.FloorToInt(t / 0.15f);
            var ink = new Rgba(0.02f, 0.03f, 0.04f);
            for (int n = 2; n <= n2; n++)
            {
                float o = n * u;
                V2[] ps = { m + new V2(-o, 0), m + new V2(o, 0), m + new V2(0, -o), m + new V2(0, o) };
                for (int i = 0; i < 4; i++)
                {
                    FillRect(ps[i].X - u / 2f - 1f, ps[i].Y - u / 2f - 1f, u + 2f, u + 2f, ink);
                    FillRect(ps[i].X - u / 2f, ps[i].Y - u / 2f, u, u, pal[(st + n + i) % 4]);
                }
            }
            FillRect(m.X - u / 2f - 1f, m.Y - u / 2f - 1f, u + 2f, u + 2f, ink);
            FillRect(m.X - u / 2f, m.Y - u / 2f, u, u, Rgba.White);
        }

        void CurPetal(V2 m, float t, float f)
        {
            float R = 9f + f * 5f, a0 = t * 1.5f;
            for (int k = 0; k < 5; k++)
            {
                float a = a0 + k * MathX.Tau / 5f;
                V2 d = V2.FromAngle(a), p = m + d * R;
                Line(p - d * 4.2f, p + d * 4.2f, 6.8f, new Rgba(1f, 0.94f, 0.96f, 0.9f));
                Line(p - d * 4f, p + d * 4f, 5f, new Rgba(1f, 0.62f, 0.77f, 0.95f));
            }
            Circle(m, 2f, new Rgba(1f, 0.94f, 0.96f), 8);
        }

        void CurLaser(V2 m, float t, float f)
        {
            float w = Mathf.Sin(t * 11f) * 0.35f;
            V2 p = m + new V2(w, -w);
            Circle(m, 9f + f * 6f, new Rgba(1f, 0.24f, 0.24f, 0.15f), 14);
            Circle(m, 5f + f * 3f, new Rgba(1f, 0.24f, 0.24f, 0.25f), 12);
            Circle(p, 2.3f + f * 1.2f, new Rgba(1f, 0.35f, 0.35f), 10);
            Circle(p, 0.9f, new Rgba(1f, 0.88f, 0.88f), 6);
        }

        void CurLock(V2 m, float t, float f)
        {
            var red = new Rgba(1f, 0.36f, 0.36f);
            for (int k = 0; k < 9; k++)
                Arc(m, 9f, 1.3f, red, 4, t * 1.2f + k * MathX.Tau / 9f, MathX.Tau / 18f);
            float R = 15f - f * 4f;
            var orange = new Rgba(1f, 0.54f, 0.24f);
            for (int k = 0; k < 3; k++)
            {
                float a = t * 2f + k * 2.094f;
                V2 d = V2.FromAngle(a), p = m + d * R, tip = p - d * 5f, perp = new V2(-d.Y, d.X);
                V2 b1 = p + d * 4f + perp * 4.2f, b2 = p + d * 4f - perp * 4.2f;
                for (int i = 0; i <= 6; i++)
                {
                    float u = i / 6f;
                    Line(tip + (b1 - tip) * u, tip + (b2 - tip) * u, 1.8f, orange);
                }
            }
            Circle(m, 1.8f, new Rgba(1f, 0.85f, 0.63f), 8);
        }
    }
}
