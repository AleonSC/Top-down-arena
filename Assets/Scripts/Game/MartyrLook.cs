using UnityEngine;

namespace Arena
{
    /// The Healer Hemomancer's Martyr in look A, "Offering" (Docs/Mockups/martyr.html). Drawing only.
    ///   Cast:  a cross opens on the caster's chest and a string of nine blood beads (a rosary) carries across to the ally.
    ///   Revive: the ally rises under a flash, two rings, a halo that floats up and petals of blood.
    ///   Ward:  a halo of 12 beads and thorns turns round the ally, one bead a second (read from the replicated
    ///          Player.WardT, so every screen agrees); the last 3 s flicker and it spins faster.
    ///   Spent: the halo's beads fly outward and the revive burst plays (FxKind.MartyrCast, Radius 2).
    /// FxKind.MartyrCast: Pos = the Hemomancer, Pos2 = the ally, Radius = 1 revive, 0 ward put on, 2 ward spent.
    /// With Visual effects off it is a plain ring (and a line for the blood) and the old pulsing ward ring.
    public partial class ArenaApp
    {
        static readonly Rgba MartyrRose = new Rgba(1f, 0.42f, 0.49f), MartyrPink = new Rgba(1f, 0.7f, 0.74f), MartyrFlare = new Rgba(1f, 0.95f, 0.95f);

        static V2 MartyrBow(V2 a, V2 b, float bow, float u)
        {
            V2 d = b - a;
            float len = d.Length;
            if (len < 1f)
                return a;
            return a + d * u + d.Orthogonal() / len * (Mathf.Sin(u * MathX.Pi) * bow);
        }

        void MartyrGlow(V2 c, float r, Rgba col, float a)
        {
            if (a <= 0f)
                return;
            for (int k = 0; k < 4; k++)
                Circle(c, r * (1f - k * 0.22f), col.WithAlpha(Mathf.Clamp01(a) * 0.22f), 20);
        }

        /// A flat ellipse outline (the halo above a head), as short segments.
        void MartyrEllipse(V2 c, float rx, float ry, float w, Rgba col)
        {
            V2 prev = c + new V2(rx, 0f);
            for (int i = 1; i <= 20; i++)
            {
                float a = MathX.Tau * i / 20f;
                V2 next = c + new V2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Line(prev, next, w, col);
                prev = next;
            }
        }

        /// The cross cut in the caster's chest.
        void MartyrChest(V2 at, float a)
        {
            if (a >= 0.5f)
                return;
            float f = a / 0.5f, fade = 1f - f;
            Rgba col = MartyrRose.WithAlpha(fade);
            Line(at + new V2(0f, -8f - f * 6f), at + new V2(0f, 8f + f * 6f), 2.5f, col);
            Line(at + new V2(-6f - f * 4f, -3f), at + new V2(6f + f * 4f, -3f), 2.5f, col);
            Arc(at, 16f + f * 22f, 2f, BloodRed.WithAlpha(fade), 28);
        }

        /// The string of beads from the caster to the ally.
        void MartyrBeads(V2 from, V2 to, float a)
        {
            if (a >= 0.95f || from.DistanceTo(to) < 30f)
                return;
            const float flight = 0.45f;
            float head = Mathf.Clamp01(a / flight * 1.2f), thread = 0.3f * (1f - Mathf.Clamp01((a - 0.5f) / 0.45f));
            V2 prev = from;
            for (int k = 1; k <= 24; k++)  // the faint thread under the beads
            {
                V2 pt = MartyrBow(from, to, 34f, EaseOut3(head * k / 24f));
                Line(prev, pt, 2f, BloodRed.WithAlpha(thread));
                prev = pt;
            }
            for (int i = 0; i < 9; i++)
            {
                float u = Mathf.Clamp01(a / flight * 1.2f - i * 0.07f);
                if (u <= 0f || u >= 1f)
                    continue;
                V2 q = MartyrBow(from, to, 34f, EaseOut3(u));
                float al = Mathf.Min(1f, (1f - u) * 5f);
                MartyrGlow(q, 10f, new Rgba(1f, 0.35f, 0.43f), 0.5f * al);
                Circle(q, 3.6f, MartyrRose.WithAlpha(al), 10);
                Circle(q + new V2(-1f, -1f), 1.3f, MartyrFlare.WithAlpha(al), 6);
            }
        }

        /// Where the ally rises: flash, rings, petals, and (big) a halo floating up.
        void MartyrBurst(V2 b, float age, bool big)
        {
            float k = big ? 1f : 0.55f;
            Arc(b, 14f + age * 110f * k, 3f, MartyrRose.WithAlpha(Mathf.Clamp01(1f - age / 0.9f)), 40);
            Arc(b, 14f + Mathf.Max(0f, age - 0.15f) * 90f * k, 2f, MartyrPink.WithAlpha(Mathf.Clamp01(1f - (age - 0.15f) / 0.8f)), 36);
            MartyrGlow(b, 44f * k, new Rgba(1f, 0.63f, 0.69f), 0.7f * (1f - age / 0.5f));
            int n = big ? 10 : 5;
            for (int i = 0; i < n; i++)
            {
                float an = i / (float)n * MathX.Tau + 0.3f, r = 20f + age * 30f * k;
                V2 at = b + new V2(Mathf.Cos(an) * r, Mathf.Sin(an) * r * 0.6f - age * 55f * k);
                DrawDrop(at, 3.1f, new V2(0f, -1f), (i % 2 == 0 ? MartyrPink : MartyrRose).WithAlpha(Mathf.Clamp01(1f - age / 1.2f)));
            }
            if (big)
                MartyrEllipse(b + new V2(0f, -18f - age * 40f), 18f, 6f, 2.5f, MartyrPink.WithAlpha(Mathf.Clamp01(1f - age / 1.1f)));
        }

        void DrawMartyrFx(Effect fx, float f)
        {
            int mode = (int)fx.Radius;
            float a = fx.T;
            if (!fxOn)
            {
                if (mode != 2 && f < 0.5f)
                    Line(fx.Pos, fx.Pos2, 4f, BloodRed.WithAlpha(0.5f * (1f - f * 2f)));
                float rr = (mode == 0 ? 60f : 90f) * Mathf.Min(1f, a / 0.5f);
                Arc(fx.Pos2, rr, 2.5f, BloodRed.WithAlpha(Mathf.Clamp01(1f - a / 0.6f)), 32);
                return;
            }
            if (mode == 2)  // the ward is spent: its beads fly out, the revive burst plays
            {
                for (int i = 0; i < 12; i++)
                {
                    float an = i / 12f * MathX.Tau + a * 1.5f, r = HaloRadius(null) + a * 150f;
                    DrawDrop(fx.Pos2 + V2.FromAngle(an) * r, 3.2f, V2.FromAngle(an), MartyrRose.WithAlpha(Mathf.Clamp01(1f - a / 0.8f)));
                }
                MartyrBurst(fx.Pos2, a, true);
                return;
            }
            MartyrChest(fx.Pos, a);
            MartyrBeads(fx.Pos, fx.Pos2, a);
            if (a < 1.3f)
                MartyrBurst(fx.Pos2, a, mode == 1);
        }

        static float HaloRadius(Player p) => (p != null ? p.Def.BodyR : 16f) * 1.9f;

        /// The ward on a player: twelve beads and thorns turning round them, one bead a second.
        void DrawMartyrHalo(Player p, float t)
        {
            const int N = 12;
            float left = Mathf.Clamp(p.WardT, 0f, Game.MartyrWardTime), rem = left / Game.MartyrWardTime;
            V2 b = p.Disp;
            if (!fxOn)
            {
                Arc(b, p.Def.BodyR + 13f, 2f, new Rgba(0.95f, 0.15f, 0.25f, 0.45f + 0.35f * Mathf.Sin(t * (4f + 8f * (1f - rem)))), 24);
                return;
            }
            float sc = EaseOut3((Game.MartyrWardTime - left) / 0.4f), R = HaloRadius(p) * sc;
            int lit = Mathf.CeilToInt(left - 1e-4f);
            float spin = t * (0.5f + (1f - rem) * 1.6f), flick = left < 3f ? 0.6f + 0.4f * Mathf.Sin(t * 30f) : 1f;
            Arc(b, R, 1f, BloodRed.WithAlpha(0.22f * sc), 36);
            for (int i = 0; i < N; i++)
            {
                float an = spin + i / (float)N * MathX.Tau, mid = an + MathX.Tau / N / 2f;
                V2 at = b + V2.FromAngle(an) * R, tx = b + V2.FromAngle(mid) * R, o = V2.FromAngle(mid);
                bool on = i < lit;
                SetColor((on ? BloodRed : BloodDark).WithAlpha((on ? 0.9f : 0.3f) * sc * flick));  // the thorn between beads
                Tri(tx + o * 8f, tx + o.Orthogonal() * -2.2f, tx + o.Orthogonal() * 2.2f);
                if (on)
                {
                    MartyrGlow(at, 9f, new Rgba(1f, 0.35f, 0.43f), 0.55f * sc * flick);
                    Circle(at, 3.2f, MartyrRose.WithAlpha(sc * flick), 10);
                    Circle(at + new V2(-0.9f, -0.9f), 1.1f, MartyrFlare.WithAlpha(sc * flick), 6);
                }
                else
                    Circle(at, 1.8f, new Rgba(0.29f, 0.125f, 0.16f, 0.55f * sc), 6);
            }
        }
    }
}
