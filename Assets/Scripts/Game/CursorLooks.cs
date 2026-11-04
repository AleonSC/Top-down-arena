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
        const float CycleCursorLife = 0.8f;  // the Light Cycle cursor's wall fades fast
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

        readonly List<CursorPt> menuCursorHist = new List<CursorPt>();
        float menuCursorFire;

        // Menus, the lobby and match overlays: the system pointer is hidden and every look draws its own corner pointer instead.
        // ---- Classic: the Corner pointer that turns into the reticle (mockup: Cursor Morph Lab, "Corner" + "Lock-on") ----
        const float CursorMorphLen = 0.6f;   // seconds for the pointer <-> reticle morph
        const float PointerArm = 14f, PointerWidth = 2.4f, PointerInk = 4.4f;
        float cursorMorph;                   // 0 = menu pointer, 1 = in-game reticle
        bool cursorWantPointer;              // set each frame by Update: menus, and match overlays (pause, end screen, upgrade cards...)

        void StepCursorMorph(bool pointer)
        {
            float target = pointer ? 0f : 1f;
            cursorMorph = fxOn && !fxReduced ? MathX.MoveTowards(cursorMorph, target, Time.unscaledDeltaTime / CursorMorphLen) : target;
        }

        static float EaseInOut3(float u) => u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;
        static float EaseOutExpo(float u) => u >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * u);

        /// The Classic cursor at morph `k` (0 pointer, 1 reticle; `f` is the fire swell). Lock-on: the L-bracket pointer collapses
        /// to a dot (k 0 to 0.3), the ring snaps out with an overshoot and a ripple, then the ticks slam in. At k = 1 it is exactly CurClassic.
        void DrawClassicMorph(V2 m, float f, float k)
        {
            if (k >= 1f)
            {
                CurClassic(m, f);
                return;
            }
            var white = Rgba.White;
            const float pivot = 0.3f;
            if (k < pivot)
            {
                float arm = PointerArm * (1f - EaseInOut3(k / pivot));
                if (arm < 0.4f)
                    return;
                V2 a = m + new V2(arm, 0f), c = m + new V2(0f, arm);
                var ink = new Rgba(0.016f, 0.024f, 0.043f, 0.55f);
                Line(a, m, PointerInk, ink);
                Line(m, c, PointerInk, ink);
                Circle(m, PointerInk / 2f, ink, 8);
                Line(a, m, PointerWidth, white);
                Line(m, c, PointerWidth, white);
                Circle(m, PointerWidth / 2f, white, 8);
                return;
            }
            float u = MathX.Clamp01((k - pivot) / (1f - pivot));
            float w = EaseOutExpo(MathX.Clamp01((k - 0.45f) / 0.55f));
            float r = (8f + f * 3f) * EaseOutBack(u), g = 5f + f * 3f, off = (1f - w) * 16f;
            if (r > 0.3f)
                Arc(m, r, 1.5f, white.WithAlpha(0.7f), 16);
            var tick = white.WithAlpha(0.7f * MathX.Clamp01(w * 3f));
            for (int i = 0; i < 4; i++)
            {
                V2 d = i == 0 ? new V2(-1f, 0f) : i == 1 ? new V2(1f, 0f) : i == 2 ? new V2(0f, -1f) : new V2(0f, 1f);
                Line(m + d * (g + off), m + d * (g + 8f + off), 1.5f, tick);
            }
            Arc(m, 8f + EaseOut3(u) * 28f, 1.2f, white.WithAlpha((1f - u) * (1f - u) * 0.8f), 24);
        }

        /// Called at the end of OnGUI: the equipped crosshair (or the Classic pointer) at the mouse, over everything.
        void DrawMenuCursor()
        {
            if (!cursorWantPointer || Event.current.type != EventType.Repaint)
                return;  // (cursorWantPointer: menus, lobby and match overlays; the arena draws its own while you play)
            float t = Time.unscaledTime;
            Vector3 mp = Input.mousePosition;
            var m = new V2((mp.x - offX) / scale, (UnityEngine.Screen.height - mp.y - offY) / scale);
            menuCursorFire = Input.GetMouseButton(0) ? 1f : Mathf.Max(0f, menuCursorFire - Time.unscaledDeltaTime / CursorFireLen);
            menuCursorHist.Add(new CursorPt { At = m, T = t });
            while (menuCursorHist.Count > 0 && t - menuCursorHist[0].T > CycleCursorLife)
                menuCursorHist.RemoveAt(0);
            if (BeginUiGl())
            {
                UiModelAt(new V2(0f, 0f), 1f);
                DrawLookMorph(CursorLook(), m, t, menuCursorFire, cursorMorph, menuCursorHist);
                EndUiGl();
            }
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

        // ---- Corner pointers for the ten rank looks: each look's own colour and flourish on the L-bracket, morphing into its crosshair ----
        static readonly Rgba[] LookTints =
        {
            Rgba.White,                           // 0 Classic (drawn by DrawClassicMorph)
            new Rgba(0.91f, 0.94f, 0.97f),        // 1 Fine Cross
            new Rgba(1f, 0.71f, 0.28f),           // 2 Corner Sight
            new Rgba(1f, 0.54f, 0.24f),           // 3 Ember Ring
            new Rgba(0.75f, 0.91f, 1f),           // 4 Frost Snowflake
            new Rgba(0.3f, 0.88f, 0.74f),         // 5 Circuit Sight
            new Rgba(1f, 0.89f, 0.37f),           // 6 Storm Arc
            new Rgba(0.61f, 0.42f, 1f),           // 7 Void Eye
            new Rgba(1f, 0.82f, 0.3f),            // 8 Gold Diamond
            Rgba.White,                           // 9 Prism Ring (cycles through the spectrum)
            new Rgba(70f / 255f, 230f / 255f, 1f) // 10 Light Cycle
        };

        /// Any look at morph `k`: 0 is its corner pointer, 1 its crosshair. The bracket collapses to a dot (k 0 to 0.3), then the crosshair
        /// swells out from the hotspot with an overshoot and a ripple in the look's colour. Classic has its own (DrawClassicMorph).
        void DrawLookMorph(int look, V2 m, float t, float f, float k, List<CursorPt> hist)
        {
            if (look <= 0 || look >= LookTints.Length)
            {
                DrawClassicMorph(m, f, k);
                return;
            }
            if (k >= 1f)
            {
                DrawCursorLook(look, m, t, f, hist);
                return;
            }
            const float pivot = 0.3f;
            if (k < pivot)
            {
                DrawLookPointer(look, m, t, 1f - EaseInOut3(k / pivot));
                return;
            }
            float u = MathX.Clamp01((k - pivot) / (1f - pivot)), z = Mathf.Max(0.01f, EaseOutBack(u));
            glShift = m * (1f - z);  // draw the crosshair scaled about the hotspot
            glZoom = z;
            DrawCursorLook(look, m, t, f, hist);
            glShift = V2.Zero;
            glZoom = 1f;
            Rgba tint = look == 9 ? Spectrum(t * 0.12f) : LookTints[look];
            Arc(m, 8f + EaseOut3(u) * 28f, 1.2f, tint.WithAlpha((1f - u) * (1f - u) * 0.8f), 24);
        }

        /// The corner pointer of a look. `s` (1 to 0) shrinks it toward the hotspot as the morph begins.
        void DrawLookPointer(int look, V2 m, float t, float s)
        {
            float arm = PointerArm * s;
            if (arm < 0.4f)
                return;
            Rgba c = LookTints[look];
            V2 a = m + new V2(arm, 0f), b = m + new V2(0f, arm);
            float w = PointerWidth;
            if (look == 6)  // Storm: the arms crackle (a new jitter every 0.08 s) over a soft glow
            {
                int st = Mathf.FloorToInt(t / 0.08f);
                V2 ja = m + new V2(arm * 0.5f, (TrailHash(st * 7) - 0.5f) * 5f * s), jb = m + new V2((TrailHash(st * 11) - 0.5f) * 5f * s, arm * 0.5f);
                Line(a, ja, 5.5f, c.WithAlpha(0.15f)); Line(ja, m, 5.5f, c.WithAlpha(0.15f));
                Line(b, jb, 5.5f, c.WithAlpha(0.15f)); Line(jb, m, 5.5f, c.WithAlpha(0.15f));
                Line(a, ja, w, c); Line(ja, m, w, c); Line(b, jb, w, c); Line(jb, m, w, c);
                Circle(m, w / 2f, c, 8);
                return;
            }
            var ink = new Rgba(0.016f, 0.024f, 0.043f, 0.55f);
            Line(a, m, PointerInk, ink); Line(m, b, PointerInk, ink); Circle(m, PointerInk / 2f, ink, 8);
            if (look == 9)  // Prism: each arm a different spectrum colour, a white dot on the corner
            {
                Line(a, m, w, Spectrum(t * 0.12f)); Line(m, b, w, Spectrum(t * 0.12f + 0.33f));
                Circle(m, w / 2f + 0.8f * s, Rgba.White, 8);
                return;
            }
            if (look == 7)  // Void: a dark disc with a violet rim inside the corner
            {
                V2 d = m + new V2(6.5f, 6.5f) * s;
                Circle(d, 4.5f * s, new Rgba(0.04f, 0.02f, 0.08f, 0.88f), 14);
                Arc(d, 4.5f * s, 1.4f, c.WithAlpha(0.35f + 0.3f * Mathf.Sin(t * 2.5f)), 14);
            }
            Line(a, m, w, c); Line(m, b, w, c); Circle(m, w / 2f, c, 8);
            switch (look)
            {
                case 1:  // Fine Cross: the dot
                    Circle(m + new V2(5f, 5f) * s, 1.4f * s, c, 8);
                    break;
                case 2:  // Corner Sight: an aim dot inside the corner
                    Circle(m + new V2(6f, 6f) * s, 2f * s, c, 8);
                    break;
                case 3:  // Ember: sparks rising off the bracket
                    for (int i = 0; i < 4; i++)
                    {
                        float q = t * 40f + i * 53f, ph = MathX.Fmod(q, 30f) / 30f;
                        int seed = Mathf.FloorToInt(q / 30f);
                        V2 p = m + new V2(3f + (TrailHash(i * 7 + seed)) * 12f, 3f + (TrailHash(i * 5 + seed) * 6f) - ph * 18f) * s;
                        Circle(p, (2f * (1f - ph) + 0.4f) * s, (i % 2 == 0 ? new Rgba(1f, 0.71f, 0.28f) : new Rgba(1f, 0.42f, 0.1f)).WithAlpha(1f - ph), 6);
                    }
                    break;
                case 4:  // Frost: a small slow snowflake
                    for (int q = 0; q < 6; q++)
                    {
                        V2 dir = V2.FromAngle(q * MathX.Tau / 6f + t * 0.4f);
                        V2 o = m + new V2(7f, 7f) * s;
                        Line(o, o + dir * (4f * s), 1.3f, c);
                    }
                    break;
                case 5:  // Circuit: square nodes on the ends and a pulse running along the bracket
                {
                    FillRect(a.X - 2f, a.Y - 2f, 4f, 4f, c);
                    FillRect(b.X - 2f, b.Y - 2f, 4f, 4f, c);
                    float p = MathX.Fmod(t / 1.2f, 1f) * 2f * arm;
                    V2 pt = p < arm ? a + (m - a) * (p / arm) : m + (b - m) * ((p - arm) / arm);
                    Circle(pt, 3.4f, c.WithAlpha(0.3f), 10);
                    Circle(pt, 1.8f, new Rgba(0.91f, 1f, 0.98f), 8);
                    break;
                }
                case 8:  // Gold: a twinkle at the end of the long arm
                {
                    float tw = 0.5f + 0.5f * Mathf.Sin(t * 3.3f), sr = (3.5f * tw + 1.5f) * s;
                    var wh = new Rgba(1f, 0.97f, 0.82f);
                    V2 sp = a + new V2(2f, 2f) * s;
                    Line(sp - new V2(sr, 0f), sp + new V2(sr, 0f), 1.3f, wh);
                    Line(sp - new V2(0f, sr), sp + new V2(0f, sr), 1.3f, wh);
                    break;
                }
                case 10:  // Light Cycle: a square head on the corner
                    FillRect(m.X - 3f * s, m.Y - 3f * s, 6f * s, 6f * s, c);
                    FillRect(m.X - 1f, m.Y - 1f, 2f, 2f, new Rgba(0.04f, 0.05f, 0.06f));
                    break;
            }
        }
    }
}
