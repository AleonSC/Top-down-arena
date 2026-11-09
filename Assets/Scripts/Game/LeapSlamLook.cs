using UnityEngine;

namespace Arena
{
    /// The Knight's Leap Slam with Visual effects on (Docs/Mockups/leap-slam-rework.html, look C "Bulwark"). Drawing only; the leap,
    /// the 50 damage, the stun and the radius are unchanged, and with effects off the plain tan ring draws as before.
    ///
    /// The host sends one FxKind.LeapSlam at the cast (Pos = where they leapt from, Pos2 = where they land, Radius = their id * 1000 +
    /// the slam's radius, Color.A = 1 when mastered, Dur = LeapTime + LeapFxTail + the stun) and the look runs off its clock:
    ///   0 s          a hex ring and steel sparks at their feet; they swell and rise (`LeapHeight`) while their shadow stays on the floor;
    ///   0 to 0.45 s  a landing marker on the floor: a hexagon closing in on the true radius, with sparks trailing the knight;
    ///   0.45 s       impact: a white flash, three hex pressure rings one after another, steel plates and sparks thrown out, and a hex rune
    ///                left on the floor (mastered: a dashed ring at 160 px, where the capstone erases bullets);
    ///   0.45 s on    each enemy it stunned is held in a hex cage whose ring drains over the stun (`LeapCaged`: the ice block is not drawn).
    public partial class ArenaApp
    {
        const float LeapLiftPx = 56f, LeapSwell = 0.55f;
        static readonly Rgba LeapSteel = new Rgba(0.59f, 0.78f, 1f), LeapHot = new Rgba(0.88f, 0.95f, 1f), LeapPlate = new Rgba(0.69f, 0.75f, 0.82f);

        /// The live LeapSlam effects, oldest first, and what they say about a stun: the stun's length, from the effect's own clock.
        static float LeapStunOf(Effect fx) => Mathf.Max(0.1f, fx.Dur - Game.LeapTime - Game.LeapFxTail);

        static float LeapRadiusOf(Effect fx) => fx.Radius - (int)(fx.Radius / 1000f + 0.001f) * 1000f;

        /// How high a knight is in the air, 0 to 1 and back (0 when they aren't leaping, or with effects off).
        bool LeapHeight(Player p, out float h)
        {
            h = 0f;
            if (!fxOn || !ReferenceEquals(game, session.Game))
                return false;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.LeapSlam && fx.T < Game.LeapTime && (int)(fx.Radius / 1000f + 0.001f) == p.Id)
                {
                    h = Mathf.Sin(Mathf.PI * Mathf.Clamp01(fx.T / Game.LeapTime));
                    return true;
                }
                else if (fx.Kind == FxKind.MercyLeap && fx.T < Game.MercyLeapTime && (int)(fx.Radius / 100f + 0.001f) == p.Id)
                {
                    h = 0.43f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(fx.T / Game.MercyLeapTime));  // Misericorde's leap: lower, 24 px up (KnightDpsLook.cs)
                    return true;
                }
            return false;
        }

        /// Is this enemy held in a slam's cage right now (so the ice block and frost tint stay off it)? `left` is how much of the stun remains, 0 to 1.
        bool LeapCaged(Enemy e, out float left)
        {
            left = 0f;
            if (!fxOn || !e.Frozen || e.Def.IsBoss)
                return false;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.LeapSlam || fx.T < Game.LeapTime)
                    continue;
                float stun = LeapStunOf(fx), m = fx.T - Game.LeapTime;
                if (m < stun && e.Disp.DistanceTo(fx.Pos2) <= LeapRadiusOf(fx) + e.Radius + 6f)
                {
                    left = 1f - m / stun;
                    return true;
                }
            }
            return false;
        }

        bool LeapCaged(Enemy e)
        {
            float left;
            return LeapCaged(e, out left);
        }

        /// The landing's plain Boom (disc glow and sparks) that the look replaces; its scorch mark, ripple and shake stay.
        bool IsLeapBoom(Effect fx) => fxOn && fx.Kind == FxKind.Boom && SameRgb(fx.Color, Game.LeapBoomColor);

        void LeapHex(V2 c, float r, float rot, float w, Rgba col)
        {
            if (col.A <= 0.01f || r <= 0.5f)
                return;
            for (int i = 0; i < 6; i++)
                Line(c + V2.FromAngle(rot + i * MathX.Tau / 6f) * r, c + V2.FromAngle(rot + (i + 1) * MathX.Tau / 6f) * r, Mathf.Max(0.6f, w), col);
        }

        /// WorldEffects.cs: FxKind.LeapSlam.
        void DrawLeapSlamFx(Effect fx)
        {
            if (!fxOn)
                return;
            float a = fx.T, R = LeapRadiusOf(fx), stun = LeapStunOf(fx);
            int owner = (int)(fx.Radius / 1000f + 0.001f);
            bool mastered = fx.Color.A > 0.9f;
            V2 from = fx.Pos, at = fx.Pos2, dir = (at - from).Length > 1f ? (at - from).Normalized() : new V2(1f, 0f);

            // The launch: a hex ring and steel sparks where they left the floor.
            if (a < 0.32f)
            {
                float u = a / 0.32f;
                LeapHex(from, 8f + 44f * EaseOut3(u), 0f, 3f * (1f - u) + 0.6f, LeapHot.WithAlpha(0.9f * (1f - u)));
                GlowDisc(from, 40f * (1f - u), LeapSteel, 0.45f * (1f - u));
                for (int i = 0; i < 6; i++)
                {
                    float an = Hash01(i * 2.7f) * MathX.Tau, d = 180f * a;
                    V2 sp = from + V2.FromAngle(an) * d;
                    Line(sp, sp + V2.FromAngle(an) * 8f * (1f - u), 1.8f, LeapHot.WithAlpha(1f - u));
                }
            }

            // In the air: the landing marker closing in, a glow round the knight and sparks off the back of it.
            if (a < Game.LeapTime)
            {
                float p = a / Game.LeapTime;
                Circle(at, R, LeapSteel.WithAlpha(0.07f + 0.12f * p), 40);
                Arc(at, R, 1.8f, LeapSteel.WithAlpha(0.35f + 0.4f * p), 48);
                LeapHex(at, R * (1.7f - 0.7f * EaseOut3(p)), a * 2f, 3f, LeapHot.WithAlpha(0.9f));
                for (int i = 0; i < 4; i++)
                {
                    V2 d = V2.FromAngle(i * MathX.Tau / 4f + MathX.Pi / 4f);
                    Line(at + d * (R - 7f), at + d * (R + 10f), 3f, LeapHot.WithAlpha(0.9f));
                }
                Player pl;
                if (game.Players.TryGetValue(owner, out pl))
                {
                    float h = Mathf.Sin(Mathf.PI * p);
                    V2 kp = pl.Disp + new V2(0f, -LeapLiftPx * h);
                    GlowDisc(kp, 62f, LeapHot, 0.2f + 0.3f * h);
                    int step = (int)(a * 45f);
                    for (int i = 0; i < 5; i++)
                    {
                        float an = Hash01(step * 7.1f + i * 3.3f) * MathX.Tau, d = 8f + 16f * Hash01(step * 3.7f + i);
                        V2 sp = kp + V2.FromAngle(an) * d;
                        Line(sp, sp - dir * 16f, 1.8f, LeapHot.WithAlpha(0.8f));
                    }
                }
            }

            float m = a - Game.LeapTime;
            if (m < 0f)
                return;

            // The impact.
            if (m < 0.2f)
                GlowDisc(at, R * 0.9f, Rgba.White, 0.55f * (1f - m / 0.2f));
            if (m < 0.6f)
                GlowDisc(at, 26f + (R + 40f) * EaseOut3(m / 0.6f), LeapSteel, 0.5f * (1f - m / 0.6f));
            if (m < 0.38f)
            {
                float u = m / 0.38f;
                Arc(at, 12f + (R - 12f) * EaseOut3(u), 8f * (1f - 0.6f * u) + 0.6f, LeapHot.WithAlpha(1f - u), 56);
            }
            if (m < 0.3f)
            {
                float u = m / 0.3f;
                Arc(at, 8f + (R * 0.8f - 8f) * EaseOut3(u), 3.5f * (1f - 0.6f * u) + 0.5f, Rgba.White.WithAlpha(0.9f * (1f - u)), 48);
            }
            for (int i = 0; i < 3; i++)  // three pressure rings, one after another
            {
                float u = (m - i * 0.07f) / (0.4f + i * 0.08f);
                if (u < 0f || u >= 1f)
                    continue;
                LeapHex(at, 10f + (R * (0.7f + i * 0.22f) - 10f) * EaseOut3(u), i * 0.2f, (6f - i) * (1f - 0.5f * u) + 0.6f, (i == 0 ? LeapHot : LeapSteel).WithAlpha(1f - u));
            }
            for (int i = 0; i < 14; i++)  // plates
            {
                float h1 = Hash01(i * 2.1f + 1f), h2 = Hash01(i * 3.3f + 2f), life = 0.5f + 0.3f * h2;
                if (m >= life)
                    continue;
                V2 d = V2.FromAngle(h1 * MathX.Tau);
                V2 pp = at + d * ((200f + 230f * h2) * (1f - Mathf.Exp(-3f * m)) / 3f);
                float sz = 4f + 5f * h2, rot = h1 * MathX.Tau + m * (h2 - 0.5f) * 16f, fade = Mathf.Min(1f, (1f - m / life) * 2f);
                V2 u0 = V2.FromAngle(rot) * sz, u1 = V2.FromAngle(rot + MathX.Pi / 2f) * (sz * 0.55f);
                SetColor(LeapPlate.WithAlpha(fade));
                Quad(pp - u0 - u1, pp + u0 - u1, pp + u0 + u1, pp - u0 + u1);
            }
            for (int i = 0; i < 22; i++)  // sparks
            {
                float h1 = Hash01(i * 1.9f + 5f), h2 = Hash01(i * 4.1f + 9f), life = 0.35f;
                if (m >= life)
                    continue;
                V2 d = V2.FromAngle(h1 * MathX.Tau);
                float v = 230f + 300f * h2;
                V2 sp = at + d * (v * m);
                Line(sp - d * (v * 0.05f), sp, 2.2f, LeapHot.WithAlpha(1f - m / life));
            }

            // The rune left on the floor, and (mastered) the ring where the capstone erases bullets.
            float rise = Mathf.Clamp01(m / 0.35f), fall = 1f - Mathf.Clamp01((m - (stun - 0.1f)) / 0.5f);
            if (fall > 0.01f)
            {
                LeapHex(at, R * 0.97f, 0f, 2.6f, LeapSteel.WithAlpha(0.8f * rise * fall));
                LeapHex(at, R * 0.6f, 0.26f, 1.6f, LeapSteel.WithAlpha(0.45f * rise * fall));
                if (mastered)
                    for (int i = 0; i < 18; i++)
                        Arc(at, 160f, 1.8f, LeapHot.WithAlpha(0.4f * rise * fall), 4, m * 0.4f + i * MathX.Tau / 18f, MathX.Tau / 36f);
            }

            // A hex cage round each enemy it stunned, its ring draining over the stun.
            if (m < stun)
            {
                float left = 1f - m / stun;
                foreach (var e in game.Enemies.Values)
                {
                    if (!e.Frozen || e.Def.IsBoss || e.Disp.DistanceTo(at) > R + e.Radius + 6f)
                        continue;
                    float rr = e.Radius + 13f;
                    Arc(e.Disp, rr + 4f, 3f, LeapSteel.WithAlpha(0.95f), 28, -MathX.Pi / 2f, MathX.Tau * left);
                    LeapHex(e.Disp, rr + 10f, 0f, 2f, LeapSteel.WithAlpha(0.25f + 0.7f * left));
                    for (int i = 0; i < 6; i++)
                    {
                        V2 d = V2.FromAngle(i * MathX.Tau / 6f);
                        Line(e.Disp + d * (rr + 10f), e.Disp + d * (rr + 17f), 2f, LeapHot.WithAlpha(0.8f));
                    }
                }
            }
        }
    }
}
