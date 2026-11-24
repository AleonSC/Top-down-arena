using UnityEngine;

namespace Arena
{
    /// The Vortex ultimate's look (Core/Vortex.cs; Docs/Mockups/vortex.html). Wind-up: a dashed ring of dust closing in from
    /// half again its size. Storm: a pale field deepening toward the rim, six trailing spiral arms, wind lines going round
    /// faster near the eye, leaves and grit thrown round the outside, a cloud wall with two dashed rings on the edge, a faint
    /// ring round the calm eye and the time left draining round the outside. Swept-away shots (FxKind.VortexWisp) curl off
    /// round it and fade. Drawing only: everything comes from the replicated VortexT, VortexAge and VortexR.
    public partial class ArenaApp
    {
        static readonly Rgba VxPale = new Rgba(0.86f, 1f, 0.98f), VxMint = new Rgba(0.56f, 0.88f, 0.86f);

        /// Under the player (DrawPlayerAuras): the whole storm.
        void DrawVortex(Player p, float t)
        {
            if (p.VortexT <= 0f || p.VortexR <= 0f)
                return;
            V2 c = p.Disp;
            float R = p.VortexR, age = p.VortexAge, eye = Game.VortexEye;
            if (age < Game.VortexWindup)
            {
                DrawVortexWindup(c, R, age / Game.VortexWindup, t);
                return;
            }
            float S = Game.VortexPower(age, p.VortexT);
            float A = Mathf.Max(S, 0.15f);
            if (!fxOn)
            {
                Arc(c, R, 3f, VxPale.WithAlpha(0.7f * A), 48);
                for (int k = 0; k < 6; k++)
                    Line(c + V2.FromAngle(t * 2.4f + MathX.Tau * k / 6f) * eye, c + V2.FromAngle(t * 2.4f + MathX.Tau * k / 6f - 1.2f) * R, 2f, VxPale.WithAlpha(0.45f * A));
                return;
            }

            // the field: clear in the eye, deeper toward the rim
            const int N = 48;
            Rgba rim = VxMint.WithAlpha(0.16f * A), mid = VxMint.WithAlpha(0f);
            for (int i = 0; i < N; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / N), d1 = V2.FromAngle(MathX.Tau * (i + 1) / N);
                VTri(c + d0 * eye, mid, c + d0 * R, rim, c + d1 * R, rim);
                VTri(c + d0 * eye, mid, c + d1 * R, rim, c + d1 * eye, mid);
            }

            // six spiral arms trailing behind the turn, brightest halfway out
            float rot = t * 2.4f;
            for (int k = 0; k < 6; k++)
            {
                V2 prev = c + V2.FromAngle(rot + MathX.Tau * k / 6f) * eye;
                for (float r = eye + 8f; r <= R; r += 8f)
                {
                    float f = Mathf.Sin(Mathf.PI * (r - eye) / (R - eye));
                    V2 at = c + V2.FromAngle(rot + MathX.Tau * k / 6f - (r - eye) * 0.011f) * r;
                    Line(prev, at, 1f + 5f * f, VxPale.WithAlpha(0.2f * f * A));
                    prev = at;
                }
            }

            // wind lines: faster near the eye, as the enemies go
            int lines = fxReduced ? 40 : 90;
            for (int i = 0; i < lines; i++)
            {
                float rr = eye + Hash01(i + 0.37f) * (R + 12f - eye);
                float w = Game.VortexSwirl * 1.25f / rr, len = 0.25f + 0.45f * Hash01(i * 3.1f);
                float a = t * w + Hash01(i * 7.7f) * MathX.Tau;
                Arc(c, rr, 0.8f + 1.6f * Hash01(i * 5.3f), Rgba.White.WithAlpha((0.15f + 0.4f * Hash01(i * 1.9f)) * A), 6, a - len, len);
            }

            // leaves and grit thrown round the outside
            for (int i = 0; i < 36; i++)
            {
                float rr = R * (0.55f + 0.45f * Hash01(i * 2.3f + 1f)), a = t * Game.VortexSwirl * 1.1f / rr + i * 1.7f;
                V2 at = c + V2.FromAngle(a) * rr, dir = V2.FromAngle(t * 6f + i);
                Rgba col = i % 3 == 0 ? new Rgba(0.47f, 0.62f, 0.36f, 0.85f * A) : new Rgba(0.67f, 0.62f, 0.53f, 0.85f * A);
                Line(at - dir * 2.5f, at + dir * 2.5f, i % 3 == 0 ? 3f : 2f, col);
            }

            // the cloud wall and the edge
            for (int i = 0; i < 28; i++)
            {
                float an = i / 28f * MathX.Tau + t * 1.4f, pr = R + Mathf.Sin(t * 3f + i * 2f) * 4f;
                GlowDisc(c + V2.FromAngle(an) * pr, 12f + 3f * Mathf.Sin(t * 4f + i), VxPale, 0.3f * A);
            }
            DashRing(c, R, 22f, 10f, t * 90f, 2.5f, VxPale.WithAlpha(0.55f * A));
            DashRing(c, R - 12f, 10f, 14f, t * 130f, 2f, VxPale.WithAlpha(0.35f * A));
            Arc(c, eye, 1.2f, VxPale.WithAlpha(0.22f * A), 32);

            // time left, draining from the top
            float rem = Mathf.Clamp01(p.VortexT / Game.VortexTime);
            Arc(c, R + 12f, 3f, VxPale.WithAlpha(0.12f * A), 64);
            Arc(c, R + 12f, 3f, VxPale.WithAlpha(0.85f * A), 64, -MathX.Pi / 2f, MathX.Tau * rem);
        }

        /// The wind-up: a dashed ring of dust closing in from 1.5 R to R (k 0..1).
        void DrawVortexWindup(V2 c, float R, float k, float t)
        {
            float r = R * (1.5f - 0.5f * EaseOut3(k));
            if (!fxOn)
            {
                Arc(c, r, 2f, VxPale.WithAlpha(0.6f * k + 0.2f), 48);
                return;
            }
            DashRing(c, r, 10f, 14f, -t * 120f, 3f, VxPale.WithAlpha(0.15f + 0.55f * k));
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * MathX.Tau + t * 3f, rr = r * (1f - 0.15f * Mathf.Sin(i));
                Circle(c + V2.FromAngle(a) * rr, 1.6f, new Rgba(0.78f, 0.75f, 0.63f, 0.6f * k), 6);
            }
        }

        /// FxKind.VortexWisp: an enemy shot swept away, curling round the storm (Pos2 = its middle) and fading.
        void DrawVortexWispFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            V2 off = fx.Pos - fx.Pos2;
            float r0 = Mathf.Max(20f, off.Length), a0 = off.Angle;
            float a = a0 + 1.1f * EaseOut3(f), r = r0 + 40f * f;
            Arc(fx.Pos2, r, 2f, VxPale.WithAlpha(0.6f * (1f - f)), 8, a - 0.35f, 0.35f);
            Circle(fx.Pos2 + V2.FromAngle(a) * r, Mathf.Max(1.5f, fx.Radius) * (1f - 0.6f * f), fx.Color.Mix(VxPale, f).WithAlpha(1f - f), 10);
        }
    }
}
