using UnityEngine;

namespace Arena
{
    /// Eye of Calm's look (Core/EyeOfCalm.cs; Docs/Mockups/eye-of-calm.html). Drawing only, from the replicated EyeT, EyeAge and
    /// EyeWallR and FxKind.EyeGust:
    ///   - the storm: a dark cloud band from the eye to the wall's edge (thickest just outside the eye), five spiral arms
    ///     trailing the turn, wind and rain streaks going round faster near the eye, grit thrown round it and a cloud wall on
    ///     the outer edge; it grows out from the eye over the spin-up;
    ///   - the calm eye: a dashed white rim turning slowly, a soft green light inside and healing motes rising in it;
    ///   - the time left draining round the outside;
    ///   - the final gust (FxKind.EyeGust): a gold ring rolling out past the wall and the storm's last streaks flying off.
    /// Swept-away shots are FxKind.VortexWisp (VortexLook.cs). With Visual effects off: plain rings.
    public partial class ArenaApp
    {
        static readonly Rgba EyeCloud = new Rgba(0.27f, 0.5f, 0.6f), EyeWhite = new Rgba(0.88f, 0.97f, 1f);

        /// Under the player (DrawPlayerAuras): the whole storm.
        void DrawEyeOfCalm(Player p, float t)
        {
            if (!Game.EyeOn(p) || p.EyeWallR <= 0f || LaunchHides(p))
                return;
            V2 c = p.Disp;
            float eye = Game.EyeRadius, R = Game.EyeReach(p.EyeWallR, p.EyeAge), A = Game.EyePower(p.EyeAge);
            float end = Mathf.Clamp01(p.EyeT / 0.3f);  // (it eases out as the gust takes over)
            A *= end;
            if (!fxOn)
            {
                Arc(c, eye, 2f, Game.EyeCalm.WithAlpha(0.8f * A), 40);
                Arc(c, R, 3f, Game.EyeStorm.WithAlpha(0.7f * A), 56);
                for (int k = 0; k < 5; k++)
                    Line(c + V2.FromAngle(t * 1.9f + MathX.Tau * k / 5f) * eye, c + V2.FromAngle(t * 1.9f + MathX.Tau * k / 5f - 1.6f) * R, 2f, Game.EyeStorm.WithAlpha(0.45f * A));
                return;
            }

            // the cloud band: clear in the eye, thickest just outside it, thinning to the edge
            const int N = 56;
            float peak = eye + (R - eye) * 0.25f;
            Rgba clear = EyeCloud.WithAlpha(0f), thick = EyeCloud.WithAlpha(0.34f * A), thin = EyeCloud.WithAlpha(0.12f * A);
            for (int i = 0; i < N; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / N), d1 = V2.FromAngle(MathX.Tau * (i + 1) / N);
                VTri(c + d0 * eye, clear, c + d0 * peak, thick, c + d1 * peak, thick);
                VTri(c + d0 * eye, clear, c + d1 * peak, thick, c + d1 * eye, clear);
                VTri(c + d0 * peak, thick, c + d0 * R, thin, c + d1 * R, thin);
                VTri(c + d0 * peak, thick, c + d1 * R, thin, c + d1 * peak, thick);
            }

            // five spiral arms trailing the turn
            float rot = t * 1.9f;
            for (int k = 0; k < 5; k++)
            {
                V2 prev = c + V2.FromAngle(rot + MathX.Tau * k / 5f) * eye;
                for (float r = eye + 8f; r <= R; r += 8f)
                {
                    float f = Mathf.Sin(Mathf.PI * (r - eye) / Mathf.Max(1f, R - eye));
                    V2 at = c + V2.FromAngle(rot + MathX.Tau * k / 5f - (r - eye) * 0.0085f) * r;
                    Line(prev, at, 2f + 12f * f, EyeWhite.WithAlpha(0.1f * f * A));
                    Line(prev, at, 1f + 2f * f, EyeWhite.WithAlpha(0.2f * f * A));
                    prev = at;
                }
            }

            // wind and rain streaks: faster near the eye
            int lines = fxReduced ? 60 : 140;
            for (int i = 0; i < lines; i++)
            {
                float rr = eye + 4f + Hash01(i + 0.61f) * (R - eye);
                float w = 340f / rr, len = (0.12f + 0.3f * Hash01(i * 3.7f)) * (90f / rr + 0.4f);
                float a = t * w + Hash01(i * 7.3f) * MathX.Tau;
                bool rain = i % 4 == 0;
                Arc(c, rr, rain ? 1f : 0.8f + 1.4f * Hash01(i * 5.1f),
                    (rain ? new Rgba(0.7f, 0.85f, 1f) : EyeWhite).WithAlpha((0.15f + 0.45f * Hash01(i * 1.7f)) * A), 6, a - len, len);
            }

            // grit and leaves thrown round
            for (int i = 0; i < 40; i++)
            {
                float rr = eye + 20f + (R - eye - 20f) * Hash01(i * 2.9f + 1f), a = t * 300f / rr + i * 1.9f;
                V2 at = c + V2.FromAngle(a) * rr, dir = V2.FromAngle(t * 7f + i);
                Rgba col = i % 3 == 0 ? new Rgba(0.45f, 0.62f, 0.38f, 0.8f * A) : new Rgba(0.6f, 0.55f, 0.46f, 0.8f * A);
                Line(at - dir * 2.5f, at + dir * 2.5f, i % 3 == 0 ? 3f : 2f, col);
            }

            // the cloud wall on the outer edge
            for (int i = 0; i < 34; i++)
            {
                float an = i / 34f * MathX.Tau + t * 0.9f, pr = R + Mathf.Sin(t * 2.6f + i * 2f) * 5f;
                GlowDisc(c + V2.FromAngle(an) * pr, 14f + 4f * Mathf.Sin(t * 3f + i), EyeWhite, 0.24f * A);
            }
            DashRing(c, R, 26f, 12f, t * 70f, 2.5f, EyeWhite.WithAlpha(0.45f * A));

            // the calm eye: a slow dashed rim, green light, healing motes rising
            DashRing(c, eye, 14f, 10f, -t * 30f, 2.2f, EyeWhite.WithAlpha(0.6f * A));
            Arc(c, eye - 3f, 4f, Game.EyeCalm.WithAlpha(0.14f * A), 40);
            for (int i = 0; i < 12; i++)
            {
                float ph = Mathf.Repeat(t * 0.6f + Hash01(i * 4.3f), 1f);
                float a = Hash01(i * 9.1f) * MathX.Tau, rr = eye * 0.8f * Mathf.Sqrt(Hash01(i * 6.7f + 2f));
                V2 at = c + V2.FromAngle(a) * rr + new V2(0f, -18f * ph);
                Circle(at, 1.8f, Game.EyeCalm.WithAlpha(0.8f * A * Mathf.Sin(Mathf.PI * ph)), 6);
            }

            // the time left, draining from the top
            float rem = Mathf.Clamp01(p.EyeT / Game.EyeTime);
            Arc(c, R + 14f, 3f, EyeWhite.WithAlpha(0.12f * A), 64);
            Arc(c, R + 14f, 3f, Game.EyeCalm.WithAlpha(0.85f * A), 64, -MathX.Pi / 2f, MathX.Tau * rem);
        }

        /// WorldEffects.cs: FxKind.EyeGust, the wall blowing outward.
        void DrawEyeGustFx(Effect fx)
        {
            V2 c = fx.Pos;
            float R = fx.Radius, f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), fade = 1f - f, roll = EaseOut3(f);
            float r = Mathf.Lerp(R * 0.7f, R + 90f, roll);
            if (!fxOn)
            {
                Arc(c, r, 3f, Game.EyeGold.WithAlpha(fade), 48);
                return;
            }
            Arc(c, r, 3f + 8f * fade, Game.EyeGold.WithAlpha(0.85f * fade), 64);
            Arc(c, r - 18f, 2f, EyeWhite.WithAlpha(0.5f * fade), 64);
            for (int i = 0; i < 40; i++)  // the storm's last streaks flying off
            {
                float a = Hash01(i * 3.3f) * MathX.Tau + roll * 0.6f, rr = Mathf.Lerp(Game.EyeRadius + Hash01(i * 1.3f) * (R - Game.EyeRadius), R + 120f, roll);
                Arc(c, rr, 1.5f, EyeWhite.WithAlpha(0.6f * fade), 5, a - 0.12f, 0.12f);
            }
        }

        /// Juice.DrawGlowPass: the eye's calm light and the gust's flash.
        void DrawEyeOfCalmGlow()
        {
            foreach (var p in game.Players.Values)
                if (Game.EyeOn(p) && !LaunchHides(p))
                    GlowDisc(p.Disp, Game.EyeRadius * 1.2f, Game.EyeCalm, 0.16f * Game.EyePower(p.EyeAge));
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.EyeGust && fx.T < 0.3f)
                    GlowDisc(fx.Pos, fx.Radius, Game.EyeGold, 0.4f * (1f - fx.T / 0.3f));
        }
    }
}
