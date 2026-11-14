using UnityEngine;

namespace Arena
{
    /// MOCKUP: a player pressed against a wall lights it up where they touch (three looks to choose from; off in the
    /// game until one is picked). Drawn after the walls (WorldRenderer.cs), in the wall's own colour.
    public partial class ArenaApp
    {
        public static int WallTouchStyle;  // 0 off; 1 Surge, 2 Ripple, 3 Static (previews: WALLTOUCH)
        public static float WallTouchT = -1f;  // previews: the effect's clock (WALLT)

        void DrawWallTouch(float t)
        {
            var d = game.ActiveLayout;
            if (WallTouchStyle == 0 || d == null)
                return;
            if (WallTouchT >= 0f)
                t = WallTouchT;
            foreach (var p in game.Players.Values)
            {
                if (p.Downed)
                    continue;
                float r = p.Def.BodyR;
                foreach (var s in d.Solids)
                {
                    V2 at, n;
                    if (!WallContact(s, p.Disp, r + 3f, out at, out n))
                        continue;
                    if (WallTouchStyle == 1)
                        DrawTouchSurge(at, n, t);
                    else if (WallTouchStyle == 2)
                        DrawTouchRipple(at, n, t);
                    else
                        DrawTouchStatic(at, n, p.Disp, r, t, p.Id);
                }
            }
        }

        /// The point on the wall's face nearest the body, and the face's outward normal, when the body is within reach.
        static bool WallContact(Solid s, V2 c, float reach, out V2 at, out V2 n)
        {
            if (s.Round)
            {
                V2 dv = c - s.Pos;
                float dl = dv.Length;
                n = dl > 0.01f ? dv / dl : new V2(0f, -1f);
                at = s.Pos + n * s.R;
                return dl - s.R <= reach;
            }
            at = new V2(Mathf.Clamp(c.X, s.Pos.X - s.HW, s.Pos.X + s.HW), Mathf.Clamp(c.Y, s.Pos.Y - s.HH, s.Pos.Y + s.HH));
            V2 dd = c - at;
            float l = dd.Length;
            n = l > 0.01f ? dd / l : new V2(0f, -1f);
            if (Mathf.Abs(n.X) > Mathf.Abs(n.Y)) n = new V2(Mathf.Sign(n.X), 0f); else n = new V2(0f, Mathf.Sign(n.Y));
            return l <= reach;
        }

        /// A stretch of the wall's outline lit brightest at `at`, fading out along the face over `half` px.
        void LitEdge(V2 at, V2 tan, float half, float width, Rgba col, float a)
        {
            const int steps = 14;
            for (int i = -steps; i < steps; i++)
            {
                float u0 = (float)i / steps, u1 = (float)(i + 1) / steps, um = Mathf.Abs((u0 + u1) * 0.5f);
                float f = (1f - um) * (1f - um);
                Line(at + tan * (u0 * half), at + tan * (u1 * half), width, col.WithAlpha(a * f));
            }
        }

        /// 1 Surge: the outline flares white-hot round the touch, the wall's inside lights up and its hatching glows,
        /// a glow pools on the floor; a slow throb.
        void DrawTouchSurge(V2 at, V2 n, float t)
        {
            V2 tan = n.Orthogonal();
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 8f);
            GlowDisc(at, 90f, Ink, 0.55f * pulse);
            for (int i = -10; i < 10; i++)  // the wall's inside, lit in a band either side of the touch
            {
                float u0 = i / 10f, u1 = (i + 1) / 10f, um = Mathf.Abs(u0 + u1) * 0.5f, f = (1f - um) * (1f - um);
                V2 a0 = at + tan * (u0 * 120f), a1 = at + tan * (u1 * 120f);
                Quad(a0, a1, a1 - n * 22f, a0 - n * 22f, Ink.WithAlpha(0.35f * f * pulse));
            }
            // its hatching, white-hot nearest the touch: the wall's own 45 degree lines (x + y a multiple of 12)
            V2 diag = new V2(1f, -1f) * 0.7071f;
            if (diag.Dot(n) > 0f)
                diag = -diag;
            float s0 = at.X + at.Y, step = Mathf.Abs(tan.X + tan.Y);  // how far x + y moves per px along the face
            for (int k = -10; k <= 10; k++)
            {
                float c = (Mathf.Round(s0 / 12f) + k) * 12f, u = (c - s0) / ((tan.X + tan.Y) == 0f ? 1f : (tan.X + tan.Y));
                float f = 1f - Mathf.Abs(u) / 120f;
                if (f <= 0f || step < 0.5f)
                    continue;
                V2 o = at + tan * u;
                Line(o, o + diag * 30f, 2f, Rgba.White.Mix(Ink, 0.35f).WithAlpha(0.9f * f * f * pulse));
            }
            LitEdge(at, tan, 130f, 26f, Ink, 0.3f * pulse);
            LitEdge(at, tan, 110f, 9f, Ink, 0.8f * pulse);
            LitEdge(at, tan, 80f, 3.5f, Rgba.White, 1f);
        }

        /// 2 Ripple: bright pulses slide out along the face from the touch, like a force field taking the weight,
        /// with half rings bulging off the face.
        void DrawTouchRipple(V2 at, V2 n, float t)
        {
            V2 tan = n.Orthogonal();
            GlowDisc(at, 60f, Ink, 0.5f);
            LitEdge(at, tan, 40f, 4f, Rgba.White, 1f);
            const float period = 0.9f, travel = 170f;
            for (int i = 0; i < 4; i++)
            {
                float f = MathX.Fmod(t / period + i / 4f, 1f), d = 10f + f * travel, a = 1f - f;
                foreach (float sd in new[] { -1f, 1f })
                {
                    V2 c = at + tan * (sd * d);
                    float len = 10f + 8f * (1f - f);
                    Line(c - tan * len, c + tan * len, 22f, Ink.WithAlpha(0.22f * a));
                    Line(c - tan * len, c + tan * len, 8f, Ink.WithAlpha(0.6f * a));
                    Line(c - tan * len * 0.7f, c + tan * len * 0.7f, 3f, Rgba.White.WithAlpha(a));
                }
                Arc(at, 12f + f * 46f, 2.5f, Ink.WithAlpha(0.8f * a), 20, n.Angle - MathX.Pi * 0.5f, MathX.Pi);
                Arc(at, 12f + f * 46f, 8f, Ink.WithAlpha(0.18f * a), 20, n.Angle - MathX.Pi * 0.5f, MathX.Pi);
            }
        }

        /// 3 Static: jagged arcs of electricity crawl along the face from the touch, flickering, with sparks.
        void DrawTouchStatic(V2 at, V2 n, V2 body, float r, float t, int seed)
        {
            V2 tan = n.Orthogonal();
            int frame = Mathf.FloorToInt(t * 20f);
            float flick = TouchHash(frame * 7 + seed) > 0.25f ? 1f : 0.5f;
            GlowDisc(at, 70f, Ink, 0.5f * flick);
            LitEdge(at, tan, 90f, 8f, Ink, 0.7f * flick);
            LitEdge(at, tan, 60f, 3f, Rgba.White, flick);
            for (int b = 0; b < 4; b++)
            {
                float sd = b % 2 == 0 ? 1f : -1f, len = 50f + 70f * TouchHash(frame * 13 + b * 5 + seed);
                V2 prev = at;
                const int segs = 8;
                for (int k = 1; k <= segs; k++)
                {
                    float u = (float)k / segs;
                    V2 q = at + tan * (sd * len * u) + n * ((TouchHash(frame * 31 + b * 17 + k * 3 + seed) - 0.5f) * 16f * (1f - u * 0.5f));
                    Line(prev, q, 7f, Ink.WithAlpha(0.35f * (1f - u * 0.6f)));
                    Line(prev, q, 1.8f, Rgba.White.WithAlpha(1f - u * 0.5f));
                    prev = q;
                }
            }
            for (int k = 0; k < 2; k++)  // a short bolt from the body to the wall
            {
                V2 from = body + n * 0f - n * (r * 0.4f), prev = from;
                for (int j = 1; j <= 3; j++)
                {
                    V2 q = V2.Lerp(from, at, j / 3f) + tan * ((TouchHash(frame * 5 + j * 9 + k * 41) - 0.5f) * 14f);
                    Line(prev, q, 1.6f, Rgba.White);
                    prev = q;
                }
            }
            for (int s = 0; s < 8; s++)  // sparks skittering off the face
            {
                float u = (TouchHash(frame * 3 + s * 11 + seed) - 0.5f) * 160f;
                Circle(at + tan * u + n * (2f + TouchHash(frame + s * 7) * 10f), 1.8f, Rgba.White.Mix(Ink, 0.4f), 6);
            }
        }

        static float TouchHash(int i)
        {
            uint x = (uint)i * 2654435761u;
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xffffff) / 16777216f;
        }
    }
}
