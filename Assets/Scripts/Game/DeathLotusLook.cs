using UnityEngine;

namespace Arena
{
    /// The Ninja's Death Lotus (Docs/Mockups/death-lotus-looks.html, look D "Crimson Reaping"). Drawing only: the damage lands
    /// on the cast tick; this shows it as a string of cuts.
    ///
    /// The host sends one FxKind.DeathLotus for the cast (Pos2.X = -1: Pos = the Ninja, Pos2.Y = their id, Radius = targets) and
    /// one per enemy struck (Pos = the enemy, Pos2.X = seconds until its cuts land, Radius = its size, lasting that delay plus
    /// the stain time in Ninja.cs (LotusStain)). The effect's age is the clock:
    ///   the cast darkens the arena, holds a crimson glow on the Ninja for their 1 s of invulnerability, and kicks the camera;
    ///   each enemy takes three white-hot cuts with a crimson glow (0.03 s apart, gone in 0.3 s) and leaves a crimson lotus
    ///   stain on the floor that stays for about 1.7 s and fades.
    /// With Visual effects off each hit is just a plain pair of crossing cuts: no dark, glow, stain or kick.
    public partial class ArenaApp
    {
        static readonly Rgba LotusRed = new Rgba(1f, 0.18f, 0.42f), LotusPink = new Rgba(1f, 0.55f, 0.75f);

        /// A tapered cut from a to b, drawn up to `prog` of the way.
        void LotusCut(V2 a, V2 b, float w, Rgba c, float prog)
        {
            V2 end = a + (b - a) * prog, d = end - a;
            V2 n = d.Normalized().Orthogonal();
            SetColor(c);
            const int N = 8;
            V2 l0 = a, r0 = a;
            for (int k = 1; k <= N; k++)
            {
                float u = k / (float)N, h = Mathf.Sin(Mathf.PI * u) * w / 2f;
                V2 mid = a + d * u, l1 = mid + n * h, r1 = mid - n * h;
                Quad(l0, l1, r1, r0);
                l0 = l1; r0 = r1;
            }
        }

        /// A petal from `at` out along `ang`, `len` long and `wid` across at its widest.
        void LotusPetal(V2 at, float ang, float len, float wid, Rgba c)
        {
            V2 dir = V2.FromAngle(ang), n = dir.Orthogonal();
            SetColor(c);
            const int N = 6;
            V2 l0 = at, r0 = at;
            for (int k = 1; k <= N; k++)
            {
                float u = k / (float)N, h = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)) * wid / 2f;
                V2 mid = at + dir * (len * u);
                V2 l1 = mid + n * h, r1 = mid - n * h;
                Quad(l0, l1, r1, r0);
                l0 = l1; r0 = r1;
            }
        }

        /// WorldEffects.cs: FxKind.DeathLotus.
        void DrawDeathLotusFx(Effect fx)
        {
            float age = fx.T;
            if (fx.Pos2.X < 0f)
            {
                DrawDeathLotusCast(fx, age);
                return;
            }
            float delay = fx.Pos2.X, a = age - delay;
            float s = Mathf.Clamp(fx.Radius / 22f, 0.7f, 2.5f);
            float rnd = Hash01(fx.Pos.X * 0.13f + fx.Pos.Y * 0.71f);
            if (!fxOn)
            {
                if (a >= 0f && a < 0.2f)
                {
                    float h = 30f * s;
                    Line(fx.Pos + new V2(-h, -h), fx.Pos + new V2(h, h), 3f, LotusPink.WithAlpha(1f - a / 0.2f));
                    Line(fx.Pos + new V2(-h, h), fx.Pos + new V2(h, -h), 3f, LotusPink.WithAlpha(1f - a / 0.2f));
                }
                return;
            }
            float fade = 1f - Mathf.Clamp01((age - (fx.Dur - 0.5f)) / 0.5f);
            if (a >= 0.04f && fade > 0f)  // the stain, under the cuts
            {
                float u = Mathf.Clamp01((a - 0.04f) / 0.36f);
                float grow = u * u * (3f - 2f * u) * (1f + 0.18f * Mathf.Sin(u * Mathf.PI));
                for (int k = 0; k < 5; k++)
                {
                    float ang = rnd * 3f + MathX.Tau * k / 5f;
                    V2 tip = fx.Pos + V2.FromAngle(ang) * (48f * s * grow);
                    LotusPetal(fx.Pos, ang, 48f * s * grow, 18f * s * grow, LotusRed.WithAlpha(0.55f * fade));
                    Line(fx.Pos, tip, 1.5f, LotusPink.WithAlpha(0.35f * fade));
                }
                Circle(fx.Pos, 8f * s * grow, new Rgba(0.31f, 0f, 0.09f, 0.9f * fade), 12);
            }
            for (int k = 0; k < 3; k++)  // three cuts, 0.03 s apart
            {
                float ca = a - k * 0.03f;
                if (ca < 0f || ca > 0.3f)
                    continue;
                float ang = rnd * Mathf.PI + k * 1.05f, al = 1f - ca / 0.3f;
                float p = Mathf.Clamp01(ca / 0.06f);
                p = p * p * (3f - 2f * p);
                V2 d = V2.FromAngle(ang) * (92f * s), A = fx.Pos - d, B = fx.Pos + d;
                LotusCut(A, B, 40f * s, LotusRed.WithAlpha(0.18f * al), p);  // glow
                LotusCut(A, B, 18f * s, LotusRed.WithAlpha(al), p);
                LotusCut(A, B, 8f * s, Rgba.White.WithAlpha(al), p);
            }
        }

        /// The cast itself: the dark, the glow on the Ninja.
        void DrawDeathLotusCast(Effect fx, float age)
        {
            if (!fxOn)
                return;
            float gap = Mathf.Max(0.01f, (fx.Dur - 1.5f) / Mathf.Max(1f, fx.Radius));
            float chain = fx.Radius * gap;  // how long the cuts run
            float dim = Mathf.Clamp01((age - 0.02f) / 0.3f) * (1f - Mathf.Clamp01((age - (chain + 0.3f)) / 0.6f));
            if (dim > 0f)
            {
                SetColor(new Rgba(0.03f, 0f, 0.02f, 0.45f * dim));
                V2 c = fx.Pos;
                Quad(c + new V2(-4000f, -4000f), c + new V2(4000f, -4000f), c + new V2(4000f, 4000f), c + new V2(-4000f, 4000f));
            }
            Player p;
            if (age < 1f && game.Players.TryGetValue((int)fx.Pos2.Y, out p) && !LaunchHides(p))
            {
                float g = 1f - Mathf.Clamp01((age - 0.7f) / 0.3f);
                float r = p.Def.BodyR + 4f;
                Arc(p.Disp, r + 6f, 12f, LotusRed.WithAlpha(0.18f * g), 28);
                Arc(p.Disp, r, 3f, LotusRed.WithAlpha(0.9f * g), 28);
            }
        }
    }
}
