using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The crosshairs a rank unlocks (Rank.cs, CosmeticKind.Cursor; mockup Docs/Mockups/cursors.html). Only your own crosshair is
    /// drawn, so nothing here goes on the wire. 0 is the Classic crosshair (everyone has it), then 1.. in Rank.Cosmetics order: Fine Cross,
    /// Corner Sight, Ember Ring, Frost Snowflake, Circuit Sight, Storm Arc, Void Eye, Gold Diamond, Prism Ring, Light Cycle.
    /// Each draws at `m` for time `t` (seconds); `fire` is 1 as you shoot and falls to 0 in 0.24 s (the look swells); `hist` is where
    /// the cursor has been (only Light Cycle reads it). The arena (DrawCrosshair) and the cosmetics previews call the same code.
    public partial class ArenaApp
    {
        struct CursorPt { public V2 At; public float T; }

        const float CursorFireLen = 0.24f;
        const float CycleCursorLife = 2.4f;
        readonly List<CursorPt> cursorHist = new List<CursorPt>();
        readonly List<CursorPt> cursorSnap = new List<CursorPt>();
        float cursorFire;

        /// The look you have equipped and your rank has reached (0 Classic).
        int CursorLook()
        {
            var p = Profile.Current;
            return Rank.Owns(p.RankNow, p.Cursor) ? Rank.CursorIndex(p.Cursor) : 0;
        }

        /// Called once a frame from the arena: your fire pulse and the Light Cycle history.
        void StepCursor(V2 m, float t, bool firing)
        {
            cursorFire = firing ? 1f : Mathf.Max(0f, cursorFire - Time.unscaledDeltaTime / CursorFireLen);
            cursorHist.Add(new CursorPt { At = m, T = t });
            while (cursorHist.Count > 0 && t - cursorHist[0].T > CycleCursorLife)
                cursorHist.RemoveAt(0);
        }

        /// A preview (cosmetics window, ladder): the cursor at a UI canvas point, drifting and firing every 2.2 s.
        void DrawCursorSample(int look, V2 at, float t)
        {
            UiModelAt(at, 1f);
            cursorSnap.Clear();
            for (int i = 40; i >= 1; i--)
                cursorSnap.Add(new CursorPt { At = CursorSampleAt(t - i * 0.045f), T = t - i * 0.045f });
            float f = Mathf.Max(0f, 1f - MathX.Fmod(t, 2.2f) / CursorFireLen);
            DrawCursorLook(look, CursorSampleAt(t), t, f, cursorSnap);
        }

        static V2 CursorSampleAt(float t) => new V2(Mathf.Sin(t / 0.7f) * 16f, Mathf.Sin(t / 0.45f + 1f) * 9f);

        static V2 Rot(V2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new V2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        void DrawCursorLook(int look, V2 m, float t, float f, List<CursorPt> hist)
        {
            switch (look)
            {
                case 1: CurFine(m, f); break;
                case 2: CurCorner(m, f); break;
                case 3: CurEmber(m, t, f); break;
                case 4: CurFrost(m, t, f); break;
                case 5: CurCircuit(m, t, f); break;
                case 6: CurStorm(m, t, f); break;
                case 7: CurVoid(m, t, f); break;
                case 8: CurGold(m, t, f); break;
                case 9: CurPrism(m, t, f); break;
                case 10: CurCycle(m, t, f, hist); break;
                default: CurClassic(m, f); break;
            }
        }

        void CurClassic(V2 m, float f)
        {
            var ch = new Rgba(1f, 1f, 1f, 0.7f);
            Arc(m, 8f + f * 3f, 1.5f, ch, 16);
            float g = 5f + f * 3f;
            Line(m - new V2(g + 8f, 0), m - new V2(g, 0), 1.5f, ch);
            Line(m + new V2(g, 0), m + new V2(g + 8f, 0), 1.5f, ch);
            Line(m - new V2(0, g + 8f), m - new V2(0, g), 1.5f, ch);
            Line(m + new V2(0, g), m + new V2(0, g + 8f), 1.5f, ch);
        }

        void CurFine(V2 m, float f)
        {
            var c = new Rgba(0.91f, 0.94f, 0.97f, 0.95f);
            float g = 5f + f * 6f;
            Line(m - new V2(g + 9f, 0), m - new V2(g, 0), 1.5f, c);
            Line(m + new V2(g, 0), m + new V2(g + 9f, 0), 1.5f, c);
            Line(m - new V2(0, g + 9f), m - new V2(0, g), 1.5f, c);
            Line(m + new V2(0, g), m + new V2(0, g + 9f), 1.5f, c);
            Circle(m, 1.4f, c, 8);
        }

        void CurCorner(V2 m, float f)
        {
            var c = new Rgba(1f, 0.71f, 0.28f);
            float d = 12f + f * 5f, L = 6f, rot = f * 0.45f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i == 1 || i == 2 ? 1f : -1f, sy = i >= 2 ? 1f : -1f;
                V2 a = m + Rot(new V2(sx * d, sy * (d - L)), rot), b = m + Rot(new V2(sx * d, sy * d), rot), e = m + Rot(new V2(sx * (d - L), sy * d), rot);
                Line(a, b, 2f, c);
                Line(b, e, 2f, c);
            }
            Circle(m, 2f, c, 8);
        }

        void CurEmber(V2 m, float t, float f)
        {
            var fire = new Rgba(1f, 0.54f, 0.24f);
            float r = 9f + f * 5f;
            Arc(m, r, 6f, fire.WithAlpha(0.18f), 20);
            Arc(m, r, 2f, fire.WithAlpha(0.95f), 20);
            for (int i = 0; i < 8; i++)
            {
                float q = t * 40f + i * 53f, ph = MathX.Fmod(q, 30f) / 30f;
                int seed = Mathf.FloorToInt(q / 30f);
                V2 p = m + new V2((TrailHash(i * 7 + seed) - 0.5f) * 18f, -8f - ph * 22f);
                Circle(p, 2.2f * (1f - ph) + 0.4f, (i % 2 == 0 ? new Rgba(1f, 0.71f, 0.28f) : new Rgba(1f, 0.42f, 0.1f)).WithAlpha(1f - ph), 6);
            }
            Circle(m, 2f, new Rgba(1f, 0.85f, 0.63f), 8);
        }

        void CurFrost(V2 m, float t, float f)
        {
            var c = new Rgba(0.75f, 0.91f, 1f, 0.95f);
            float r0 = 5f + f * 3f, r1 = 15f + f * 4f;
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathX.Tau / 6f + t * 0.4f;
                V2 d = V2.FromAngle(a), tip = m + d * r1, br = m + d * (r1 - 4f);
                Line(m + d * r0, tip, 1.6f, c);
                Line(br, br + V2.FromAngle(a + 0.7f) * 4f, 1.4f, c);
                Line(br, br + V2.FromAngle(a - 0.7f) * 4f, 1.4f, c);
            }
            float tw = 0.5f + 0.5f * Mathf.Sin(t * 3.8f);
            Circle(m + new V2(11f, -11f), 1.5f + 2f * tw, Rgba.White, 6);
            Circle(m, 1.8f, new Rgba(0.91f, 0.97f, 1f), 8);
        }

        void CurCircuit(V2 m, float t, float f)
        {
            var c = new Rgba(0.3f, 0.88f, 0.74f);
            float h = 13f + f * 4f, L = 7f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i == 1 || i == 2 ? 1f : -1f, sy = i >= 2 ? 1f : -1f;
                V2 a = m + new V2(sx * h, sy * (h - L)), b = m + new V2(sx * h, sy * h), e = m + new V2(sx * (h - L), sy * h);
                Line(a, b, 1.8f, c);
                Line(b, e, 1.8f, c);
                FillRect(a.X - 2f, a.Y - 2f, 4f, 4f, c);
                FillRect(e.X - 2f, e.Y - 2f, 4f, 4f, c);
            }
            float side = h * 2f, p = MathX.Fmod(t / 1.5f, 1f) * 4f * side;
            int s = Mathf.FloorToInt(p / side);
            float u = p - s * side;
            V2 pt = s == 0 ? new V2(m.X - h + u, m.Y - h) : s == 1 ? new V2(m.X + h, m.Y - h + u) : s == 2 ? new V2(m.X + h - u, m.Y + h) : new V2(m.X - h, m.Y + h - u);
            Circle(pt, 5f, c.WithAlpha(0.3f), 10);
            Circle(pt, 2.6f, new Rgba(0.91f, 1f, 0.98f), 8);
            Circle(m, 1.8f, c, 8);
        }

        void CurStorm(V2 m, float t, float f)
        {
            var c = new Rgba(1f, 0.89f, 0.37f);
            int s = Mathf.FloorToInt(t / 0.08f);
            float r = 9f + f * 5f;
            const int N = 16;
            V2 prev = V2.Zero;
            for (int i = 0; i <= N; i++)
            {
                float rr = r + (TrailHash(s * 31 + i % N) - 0.5f) * 5f;
                V2 p = m + V2.FromAngle(i * MathX.Tau / N) * rr;
                if (i > 0)
                {
                    Line(prev, p, 5f, c.WithAlpha(0.15f));
                    Line(prev, p, 1.8f, c);
                }
                prev = p;
            }
            for (int b = 0; b < 3; b++)
            {
                float a0 = TrailHash(s * 7 + b) * MathX.Tau;
                V2 d = V2.FromAngle(a0), q = m + d * r;
                for (int k = 1; k <= 3; k++)
                {
                    V2 n = q + d * 4f + new V2(TrailHash(s * 13 + b * 5 + k) - 0.5f, TrailHash(s * 17 + b * 3 + k) - 0.5f) * 5f;
                    Line(q, n, 1.6f, c);
                    q = n;
                }
            }
            Circle(m, 2f, new Rgba(1f, 0.98f, 0.88f), 8);
        }

        void CurVoid(V2 m, float t, float f)
        {
            var edge = new Rgba(0.61f, 0.42f, 1f);
            float r = 9f + f * 5f;
            Circle(m, r, new Rgba(0.04f, 0.02f, 0.08f, 0.88f), 20);
            Arc(m, r, 6f, edge.WithAlpha(0.2f), 20);
            Arc(m, r, 2f, edge, 20);
            Arc(m, r * 0.55f, 1.2f, edge.WithAlpha(0.35f + 0.3f * Mathf.Sin(t * 2.5f)), 14);
            for (int mote = 0; mote < 2; mote++)
                for (int k = 0; k < 6; k++)
                {
                    float a = t * 4f + mote * MathX.Pi - k * 0.16f;
                    Circle(m + V2.FromAngle(a) * (16f + f * 3f), 2.4f * (1f - k / 8f), new Rgba(0.72f, 0.58f, 1f, (1f - k / 6f) * 0.9f), 6);
                }
            Circle(m, 1.6f, new Rgba(0.85f, 0.78f, 1f), 8);
        }

        void CurGold(V2 m, float t, float f)
        {
            var c = new Rgba(1f, 0.82f, 0.3f);
            float sc = 1f + f * 0.3f, o = 13f + f * 4f;
            V2[] k = new V2[4];
            for (int i = 0; i < 4; i++)
                k[i] = m + V2.FromAngle(MathX.Pi / 4f + i * MathX.Pi / 2f) * (9.9f * sc);
            for (int i = 0; i < 4; i++)
                Line(k[i], k[(i + 1) % 4], 2f, c);
            Line(m - new V2(o + 5f, 0), m - new V2(o, 0), 1.6f, c);
            Line(m + new V2(o, 0), m + new V2(o + 5f, 0), 1.6f, c);
            Line(m - new V2(0, o + 5f), m - new V2(0, o), 1.6f, c);
            Line(m + new V2(0, o), m + new V2(0, o + 5f), 1.6f, c);
            Circle(m, 2f, new Rgba(1f, 0.95f, 0.72f), 8);
            float tw = 0.5f + 0.5f * Mathf.Sin(t * 3.3f), sr = 5f * tw + 2f;
            V2 sp = m + new V2(13f, -13f);
            var w = new Rgba(1f, 0.97f, 0.82f);
            Line(sp - new V2(sr, 0), sp + new V2(sr, 0), 1.4f, w);
            Line(sp - new V2(0, sr), sp + new V2(0, sr), 1.4f, w);
        }

        void CurPrism(V2 m, float t, float f)
        {
            float r = 10f + f * 5f;
            for (int i = 0; i < 3; i++)
            {
                float a0 = t * 3f + i * 2.094f;
                Arc(m, r, 3f, Spectrum(t * 0.12f * 1.0f + i / 3f), 10, a0, 1.6f);
            }
            float g = t * 6f;
            Circle(m + V2.FromAngle(g) * (r + 6f), 1.8f, Rgba.White, 6);
            Circle(m, 2f, Rgba.White, 8);
        }

        void CurCycle(V2 m, float t, float f, List<CursorPt> hist)
        {
            var c = new Rgba(70f / 255f, 230f / 255f, 1f);
            if (hist != null && hist.Count > 1)
            {
                // The wall snaps to straight runs: each step goes along the bigger axis only.
                V2 s = hist[0].At;
                for (int i = 1; i <= hist.Count; i++)
                {
                    V2 p = i < hist.Count ? hist[i].At : m;
                    float pt = i < hist.Count ? hist[i].T : t;
                    if (Mathf.Abs(p.X - s.X) < 2f && Mathf.Abs(p.Y - s.Y) < 2f)
                        continue;
                    V2 n = Mathf.Abs(p.X - s.X) >= Mathf.Abs(p.Y - s.Y) ? new V2(p.X, s.Y) : new V2(s.X, p.Y);
                    float age = t - pt, al = 1f - age / CycleCursorLife;
                    if (al > 0f)
                    {
                        if (age > CycleCursorLife * 0.5f)  // old wall breaks into squares
                        {
                            V2 d = n - s;
                            float len = d.Length;
                            for (float u = 0f; u < len; u += 8f)
                                FillRect((s + d * (u / Mathf.Max(len, 0.01f))).X - 1.5f, (s + d * (u / Mathf.Max(len, 0.01f))).Y - 1.5f, 3f, 3f, c.WithAlpha(al));
                        }
                        else
                        {
                            Line(s, n, 6f, c.WithAlpha(0.15f * al));
                            Line(s, n, 3f, c.WithAlpha(al));
                        }
                    }
                    s = n;
                }
                Line(s, new V2(m.X, s.Y), 3f, c);  // the last run reaches the head
                Line(new V2(m.X, s.Y), m, 3f, c);
            }
            float sz = 8f + f * 4f;
            FillRect(m.X - sz / 2f - 2f, m.Y - sz / 2f - 2f, sz + 4f, sz + 4f, c.WithAlpha(0.22f));
            FillRect(m.X - sz / 2f, m.Y - sz / 2f, sz, sz, c);
            FillRect(m.X - 1.5f, m.Y - 1.5f, 3f, 3f, new Rgba(0.04f, 0.05f, 0.06f));
        }
    }
}
