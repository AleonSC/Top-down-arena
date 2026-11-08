using UnityEngine;

namespace Arena
{
    /// Exsanguinate's Barbed Lash (Core/Exsanguinate.cs), from Docs/Mockups/exsanguinate-rework.html take B. Drawn from the age of each
    /// FxKind.Exsanguinate:
    ///   the cast (Pos2.X = -1): ten whips crack out of the Hemomancer to the edge of the reach and back, a ring races out to it;
    ///   a tendril (Pos2.X = the enemy's id): your barbed whip with a bone hook reaches 80% of the way to the enemy; on the hook its
    ///     own veins tear out toward you with a spray of blood; they braid together and a knot pulses where they meet; the yank drags
    ///     the knot in toward you as the enemy comes (its position is replicated), blood running down the strand into you; then it
    ///     fades. A heavy enemy's tendril just goes taut and trembles. It is drawn between where the Hemomancer and enemy are now;
    ///   the impact (Pos2.X = -2): a burst ring and drops thrown off the Hemomancer (Juice.cs adds the shake and hit-pause).
    /// With Visual effects off: the strands and knots only, no glow, spray or drops.
    public partial class ArenaApp
    {
        static readonly Rgba ExOutline = new Rgba(0.24f, 0.01f, 0.04f), ExBlood = new Rgba(0.69f, 0.07f, 0.16f);
        static readonly Rgba ExLight = new Rgba(1f, 0.48f, 0.53f), ExBone = new Rgba(0.95f, 0.89f, 0.89f);

        static float ExOutC(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        static float ExOutBack(float x) { x = Mathf.Clamp01(x); const float c = 1.9f; return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }

        struct ExStrand
        {
            public V2 A, B, N;
            public float Len, Amp, Wave, Ph, Side, Twist;
        }

        /// A point `u` of the way along a writhing strand.
        V2 ExAt(ExStrand s, float u, float t)
        {
            float env = Mathf.Sin(Mathf.PI * u) * Mathf.Min(1f, s.Len / 90f);
            float off = s.Amp * env * (0.7f * Mathf.Sin(u * 9f + s.Ph + t * s.Wave) + 0.3f * Mathf.Sin(u * 21f - s.Ph * 1.7f + t * s.Wave * 1.6f)) * s.Side;
            if (s.Twist > 0f)
                off += s.Twist * Mathf.Sin(u * 38f + t * 10f);
            return V2.Lerp(s.A, s.B, u) + s.N * off;
        }

        /// The strand from u0 to u1, its width going w0 to w1: a dark rim, the blood, a light line along it.
        void ExRibbon(ExStrand s, float u0, float u1, float w0, float w1, float a, float t)
        {
            if (u1 - u0 < 0.002f)
                return;
            int n = Mathf.Max(4, (int)(26f * (u1 - u0)));
            V2 prev = ExAt(s, u0, t);
            for (int j = 1; j <= n; j++)
            {
                float k = j / (float)n;
                V2 pt = ExAt(s, Mathf.Lerp(u0, u1, k), t);
                float w = Mathf.Lerp(w0, w1, k);
                Line(prev, pt, w + 2.5f, ExOutline.WithAlpha(a));
                Line(prev, pt, w, ExBlood.WithAlpha(a));
                Line(prev + new V2(0f, -w * 0.22f), pt + new V2(0f, -w * 0.22f), Mathf.Max(0.8f, w * 0.18f), ExLight.WithAlpha(0.7f * a));
                prev = pt;
            }
        }

        /// Barbs along your whip, swept back toward you.
        void ExBarbs(ExStrand s, float u1, float a, float t)
        {
            SetColor(ExOutline.WithAlpha(a));
            for (float u = 0.06f; u < u1 - 0.04f; u += 0.09f)
            {
                V2 p = ExAt(s, u, t), q = ExAt(s, u + 0.02f, t), d = (q - p).Normalized(), n = d.Orthogonal();
                Tri(p + n * 2f, p + n * 7f - d * 5f, p - d);
                Tri(p - n * 2f, p - n * 7f - d * 5f, p - d);
            }
        }

        /// The bone hook on the whip's tip, before it catches.
        void ExHook(ExStrand s, float u, float a, float t)
        {
            V2 p = ExAt(s, u, t), d = (p - ExAt(s, Mathf.Max(0f, u - 0.03f), t)).Normalized(), n = d.Orthogonal();
            SetColor(ExBone.WithAlpha(a));
            Tri(p - d * 3f, p + d * 4f + n * 9f, p + n * 2f);
            Tri(p - d * 3f, p + d * 4f - n * 9f, p - n * 2f);
            Circle(p, 2.5f, ExOutline.WithAlpha(a), 8);
        }

        /// WorldEffects.cs: FxKind.Exsanguinate.
        void DrawExsanguinateFx(Effect fx, float t)
        {
            int kind = (int)fx.Pos2.X;
            Player caster;
            game.Players.TryGetValue((int)fx.Pos2.Y, out caster);
            V2 from = caster != null ? caster.Disp : fx.Pos;
            if (kind == -1)
                ExCast(fx, from, t);
            else if (kind == -2)
                ExImpact(fx, from);
            else if (caster != null && game.Enemies.TryGetValue(kind, out var e) && e.Hp > 0f)
                ExTendril(fx, caster, e, t);
        }

        void ExCast(Effect fx, V2 from, float t)
        {
            float age = fx.T, reach = fx.Radius;
            float rk = Mathf.Clamp01(age / 0.22f);
            Arc(from, Mathf.Max(4f, Mathf.Lerp(10f, reach, ExOutC(rk))), 5f * (1f - 0.6f * rk), ExLight.WithAlpha(1f - rk), 48);
            float k = Mathf.Clamp01((age - 0.08f) / 0.3f);
            if (k <= 0f || k >= 1f)
                return;
            float outK = k < 0.5f ? ExOutC(k * 2f) : 1f - Mathf.Pow((k - 0.5f) * 2f, 3f);
            for (int i = 0; i < 10; i++)
            {
                float an = i / 10f * MathX.Tau + CavHash(i + (int)fx.Pos.X) * 0.3f;
                V2 dir = V2.FromAngle(an);
                var s = new ExStrand { A = from + dir * 14f, B = from + dir * reach, N = dir.Orthogonal(), Len = reach, Amp = 16f, Wave = 22f, Ph = i * 1.7f, Side = 1f };
                ExRibbon(s, 0f, outK, 5f, 1f, 0.75f, t);
                if (outK > 0.85f && fxOn)
                    Arc(ExAt(s, outK, t), 6f, 1.5f, ExLight.WithAlpha(0.6f), 12);  // the crack at the tip
            }
        }

        void ExImpact(Effect fx, V2 from)
        {
            float f = Mathf.Clamp01(fx.T / 0.4f);
            Arc(from, Mathf.Lerp(16f, 64f, ExOutC(f)), 5f * (1f - 0.6f * f), ExLight.WithAlpha(1f - f), 32);
            if (!fxOn)
                return;
            GlowDisc(from, 40f * (1f - f * 0.5f), ExLight, 0.5f * (1f - f));
            for (int i = 0; i < 14; i++)
            {
                V2 d = V2.FromAngle(CavHash(i * 7 + (int)fx.Pos.X) * MathX.Tau);
                GlowDisc(from + d * (18f + 60f * Mathf.Sqrt(f) * (0.4f + CavHash(i + 3))), 3f, ExBlood, 0.9f * (1f - f));
            }
        }

        void ExTendril(Effect fx, Player caster, Enemy e, float t)
        {
            float stagger = Mathf.Max(0f, fx.Dur - Game.ExsShow), a = fx.T - stagger;
            if (a < Game.ExsWhipStart)
                return;
            bool heavy = fx.Color.A > 0.75f;
            float pull = heavy ? 0f : Game.ExsPull(a);
            bool joined = a >= Game.ExsBraid;
            float gp = joined ? 1f : Mathf.Min(1.06f, ExOutBack((a - Game.ExsWhipStart) / (Game.ExsHook - Game.ExsWhipStart)));
            float ge = joined ? 1f : ExOutC((a - Game.ExsHook) / (Game.ExsBraid - Game.ExsHook));
            float fade = Mathf.Clamp01((a - Game.ExsFade) / (Game.ExsShow - Game.ExsFade)), al = 1f - fade, ws = 1f - 0.7f * fade;
            if (al <= 0.01f)
                return;
            float m = Mathf.Lerp(0.8f, 0.45f, pull);
            V2 to = e.Disp, d = to - caster.Disp;
            float len = Mathf.Max(1f, d.Length);
            V2 dir = d / len;
            var s = new ExStrand
            {
                A = caster.Disp + dir * (caster.Def.BodyR * 0.8f), B = to - dir * Mathf.Max(0f, e.Radius - 2f), N = dir.Orthogonal(),
                Ph = CavHash(e.Id) * 6.28f, Side = e.Id % 2 == 0 ? -1f : 1f,
                Amp = joined ? Mathf.Lerp(9f, 2f, Mathf.Clamp01((a - Game.ExsBraid) / 0.3f)) : 26f,
                Wave = joined ? 6f : 16f,
                Twist = joined ? 5f * (1f - Mathf.Clamp01((a - 1f) / 0.3f)) : 0f,
            };
            s.Len = (s.B - s.A).Length;
            if (heavy && joined)
            {
                s.Amp = 1.2f;  // too heavy to move: taut and trembling
                s.A += s.N * Mathf.Sin(t * 70f) * 0.8f;
            }
            if (ge > 0f)  // its own veins, torn out toward you
                ExRibbon(s, joined ? m - 0.12f : 1f - ge * (1f - m), 1f, joined ? 3.5f * ws : 1.2f, 5.5f * ws * (heavy ? 1.3f : 1f), al, t);
            float tip = joined ? m + 0.1f : Mathf.Min(1f, gp * m);
            ExRibbon(s, 0f, tip, 8f * ws, joined ? 3.5f * ws : 1.3f, al, t);  // your whip
            ExBarbs(s, tip, al, t);
            if (!joined)
                ExHook(s, tip, al, t);
            if (fxOn && a >= Game.ExsHook && a < Game.ExsHook + 0.3f)  // the veins rip out: a spray toward you
            {
                float sf = (a - Game.ExsHook) / 0.3f;
                for (int i = 0; i < 7; i++)
                {
                    float an = Mathf.Atan2(-dir.Y, -dir.X) + (CavHash(i + e.Id * 11) - 0.5f) * 1.6f;
                    GlowDisc(to + V2.FromAngle(an) * (e.Radius + 40f * Mathf.Sqrt(sf) * (0.4f + CavHash(i + 5))), 2.6f, ExBlood, 0.95f * (1f - sf));
                }
            }
            if (!joined)
                return;
            V2 j = ExAt(s, m, t);  // the knot where they braided
            float kp = 1f + 0.25f * Mathf.Sin(t * 14f + s.Ph);
            Circle(j, (5.2f * kp + 1.5f) * ws, ExOutline.WithAlpha(al), 14);
            Circle(j, 5.2f * kp * ws, ExBlood.WithAlpha(al), 14);
            Circle(j + new V2(-1.3f, -1.3f), 1.8f * ws, ExLight.WithAlpha(al), 8);
            if (!fxOn)
                return;
            for (int k = 0; k < 5; k++)  // blood running down the strand into you
            {
                float u = 1f - MathX.Fmod(t * 2.2f + k / 5f + s.Ph, 1f);
                GlowDisc(ExAt(s, u, t), 2.6f * ws, ExLight, 0.9f * al);
            }
        }
    }
}
