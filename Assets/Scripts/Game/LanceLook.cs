using UnityEngine;

namespace Arena
{
    /// The Lance, held and swung (Docs/Mockups/lance-rework.html: the Breach, Hook & Haul and the Winged War Lance; the motion is
    /// Core/LanceMotion.cs, which the host also times the hits from). The Knight's model leaves its arms and weapon out with the
    /// Lance; this draws both arms, a pauldron on each shoulder, the Winged War Lance in the right glove (an ash shaft, a steel leaf
    /// head with two lugs, a swallow-tail pennant streaming back, a butt spike), a heater shield on the left arm, and the shadows:
    ///   - held at the ready, couched along your right side, breathing (DrawLanceIdle, from the player loop);
    ///   - each chain step (FxKind.LanceStep, Radius = the knight's id * 10 + the step), played from its age with a pale trail off
    ///     the lance (DrawLanceStepFx); the jabs throw a glowing wedge out along the thrust, the Breaching Thrust a glowing box that
    ///     slides out to its reach;
    ///   - Hook & Haul (FxKind.LanceHook): the lance shoots out through the glove to the enemy, a dashed haul line as it hooks,
    ///     then hauled back in (DrawLanceHookFx);
    ///   - each enemy a step hits (FxKind.LanceSpark): a white flash and hot streaks along the push; each Impact
    ///     (FxKind.LanceImpact): an orange burst and ring, and cracks in the floor when it was a wall (its number and tag are
    ///     Juice.cs's).
    /// Four diamonds over the knight's head show the chain (DrawLancePips): the fourth glows when the Breaching Thrust is next, or
    /// when a hook has primed it; a ring under your own feet fills as the hook comes back.
    public partial class ArenaApp
    {
        static readonly Rgba LnGlow = new Rgba(0.588f, 0.831f, 1f), LnHot = new Rgba(0.91f, 0.969f, 1f), LnImp = new Rgba(1f, 0.651f, 0.282f),
            LnDark = new Rgba(0.051f, 0.114f, 0.22f), LnArm = new Rgba(0.294f, 0.51f, 0.863f), LnGlove = new Rgba(0.608f, 0.655f, 0.722f),
            LnShield = new Rgba(0.173f, 0.361f, 0.714f), LnGold = new Rgba(0.847f, 0.69f, 0.29f), LnOutline = new Rgba(0.031f, 0.047f, 0.078f, 0.9f),
            LnPennant = new Rgba(0.239f, 0.471f, 0.863f);
        const int LnTrailN = 8;
        const float LnTrailDt = 0.012f, LnTrailSpan = 0.1f, LnThrustAt = 0.46f;

        /// The step or hook effect of this player that is playing now (the newest), or null.
        Effect LanceFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
            {
                if (fx.T >= fx.Dur || (fx.Kind != FxKind.LanceStep && fx.Kind != FxKind.LanceHook))
                    continue;
                if (Mathf.RoundToInt(fx.Radius) / 10 == id && (best == null || fx.T < best.T))
                    best = fx;
            }
            return best;
        }

        /// The lance held at the ready, when neither a step nor a hook is playing.
        void DrawLanceIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Lance || p.Downed)
                return;
            if (LanceFxOf(p.Id) != null)
                return;
            var m = LanceMotion.Ready;
            m.Rr += 0.6f * Mathf.Sin(t * 2.4f);
            DrawLancePose(p.Disp, p.Aim, m, 0f, false, t, 1f);
            DrawLancePips(p, t);
        }

        /// The move's aim, turning to where its owner aims now as it settles back to the guard.
        static float LanceAim(Effect fx, Player owner, float dur)
        {
            float aim = (fx.Pos2 - fx.Pos).Angle;
            if (owner != null && fx.T > dur)
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((fx.T - dur) / LanceMotion.Recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            return aim;
        }

        /// One chain step, from its effect's age.
        void DrawLanceStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, step = code % 10;
            if (step < 1 || step > 4)
                return;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            var s = LanceMotion.Steps[step - 1];
            float c = fx.T, t = Time.time, aimAt = (fx.Pos2 - fx.Pos).Angle;
            // Where the thrust's light was left: where you stood as it went out (you step forward through the move).
            float moved = Mathf.Clamp01((LnThrustAt - LanceMotion.MoveFrom[step - 1]) / (LanceMotion.MoveTo[step - 1] - LanceMotion.MoveFrom[step - 1]));
            V2 dirAt = V2.FromAngle(aimAt), anchor = fx.Pos + dirAt * (s.Fwd * moved);
            float q = c - s.Dur * LnThrustAt;
            if (step == 4)
                DrawBreachBox(anchor, dirAt, (fx.Pos2 - fx.Pos).Length - s.Fwd * moved, fx.Color.A * 100f, q);
            if (owner != null && LanceFxOf(id) != fx)
            {
                if (step <= 2)
                    DrawThrustWedge(anchor, dirAt, q);
                return;  // a newer step (or the hook) by the same player has taken over
            }
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float aim = LanceAim(fx, owner, s.Dur);
            DrawLanceTrail(o, aim, s, c, 0f);
            if (step <= 2)
                DrawThrustWedge(anchor, dirAt, q);
            DrawLancePose(o, aim, LanceMotion.At(s, c), 0f, c < s.Dur, t, 1f);
            if (owner != null)
                DrawLancePips(owner, t);
        }

        /// Hook & Haul, from its effect's age: the lance out through the glove to the enemy, the haul line, back in.
        void DrawLanceHookFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10;
            bool hooked = code % 10 == 1;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            if (owner != null && LanceFxOf(id) != fx)
                return;
            var s = LanceMotion.Hook;
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float c = fx.T, t = Time.time, aim = LanceAim(fx, owner, s.Dur), dist = (fx.Pos2 - fx.Pos).Length;
            float slide = LanceMotion.HookSlide(c, dist);
            var m = LanceMotion.At(s, c);
            float q = c - s.Dur * LanceMotion.HookHit;
            if (hooked && q >= 0f && q < 0.25f)  // the haul: a dashed line running from where it was hooked to the glove
            {
                V2 hand, dir;
                TwinHand(o, aim, m, true, out hand, out dir);
                LanceHaulLine(fx.Pos2, hand, q / 0.25f);
            }
            DrawLancePose(o, aim, m, slide, c < s.Dur, t, 1f);
            if (owner != null)
                DrawLancePips(owner, t);
        }

        void LanceHaulLine(V2 from, V2 to, float k)
        {
            float al = 1f - k, len = from.DistanceTo(to), off = k * 30f;
            V2 d = (to - from) / Mathf.Max(1f, len);
            for (float x = off % 12f - 12f; x < len; x += 12f)
            {
                float a = Mathf.Max(0f, x), b = Mathf.Min(len, x + 6f);
                if (b > a)
                    Line(from + d * a, from + d * b, 2f, LnGlow.WithAlpha(0.8f * al));
            }
        }

        /// The pale trail off the lance while it cuts or thrusts.
        void DrawLanceTrail(V2 o, float aim, TwinBladesMotion.Step s, float c, float slide)
        {
            V2 pTip = default(V2), pBase = default(V2);
            bool have = false;
            for (int i = 0; i <= LnTrailN; i++)
            {
                float ci = c - i * LnTrailDt, k = ci / s.Dur;
                bool hot = ci >= 0f && k >= s.HotR0 && k <= s.HotR1;
                if (!hot) { have = false; continue; }
                V2 hand, dir;
                TwinHand(o, aim, LanceMotion.At(s, ci), true, out hand, out dir);
                V2 tip = hand + dir * (LanceMotion.Len + slide), bse = hand + dir * ((LanceMotion.Len + slide) * 0.45f);
                if (have)
                {
                    float a = Mathf.Pow(1f - (i * LnTrailDt) / LnTrailSpan, 1.5f), a0 = Mathf.Pow(1f - ((i - 1) * LnTrailDt) / LnTrailSpan, 1.5f);
                    if (a > 0f)
                    {
                        VTri(pTip, LnGlow.WithAlpha(0.2f * a0), tip, LnGlow.WithAlpha(0.2f * a), bse, LnGlow.WithAlpha(0.2f * a));
                        VTri(pTip, LnGlow.WithAlpha(0.2f * a0), bse, LnGlow.WithAlpha(0.2f * a), pBase, LnGlow.WithAlpha(0.2f * a0));
                        Line(pTip, tip, 2f * a + 0.4f, LnHot.WithAlpha(0.85f * a));
                    }
                }
                pTip = tip; pBase = bse; have = true;
            }
        }

        /// A jab's light: a glowing wedge out along the thrust that fades in 0.22 s. `q`: seconds since it went out.
        void DrawThrustWedge(V2 o, V2 dir, float q)
        {
            if (q < 0f || q >= 0.22f)
                return;
            float al = 1f - q / 0.22f, L = 182f * (q < 0.04f ? q / 0.04f : 1f), w = 18f * (1f - q / 0.3f);
            if (L <= 22f)
                return;
            V2 side = new V2(-dir.Y, dir.X);
            float a20 = 0.45f * al * Mathf.Clamp01(20f / (0.4f * L));
            VTri(o + dir * 20f - side * w, LnGlow.WithAlpha(a20), o + dir * L, LnHot.WithAlpha(0.85f * al), o + dir * 20f + side * w, LnGlow.WithAlpha(a20));
            Line(o + dir * 24f, o + dir * (L + 4f), 1.5f, new Rgba(1f, 1f, 1f, al));
        }

        /// The Breaching Thrust's light: a box from where you stood out to its reach, its front sliding out in 0.09 s and fading
        /// over 0.45 s. `q`: seconds since it went out; `w`: its half width.
        void DrawBreachBox(V2 o, V2 dir, float len, float w, float q)
        {
            if (q < 0f || q >= 0.55f || len < 1f)
                return;
            float g = 1f - Mathf.Pow(1f - Mathf.Clamp01(q / 0.09f), 3f), front = len * g, fade = q < 0.09f ? 1f : 1f - Mathf.Clamp01((q - 0.09f) / 0.45f);
            V2 side = new V2(-dir.Y, dir.X), a0 = o - side * w, a1 = o + side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
            var clear = LnGlow.WithAlpha(0f);
            VTri(a0, clear, b0, LnGlow.WithAlpha(0.35f * fade), b1, LnGlow.WithAlpha(0.35f * fade));
            VTri(a0, clear, b1, LnGlow.WithAlpha(0.35f * fade), a1, clear);
            Line(b0, b1, 14f * fade + 3f, LnGlow.WithAlpha(0.5f * fade));
            Line(b0, b1, 2f, new Rgba(1f, 1f, 1f, 0.9f * fade));
        }

        static float LnRnd(float n)
        {
            float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        /// An enemy a Lance step hit: a white flash, then hot streaks thrown along the push.
        void DrawLanceSparkFx(Effect fx)
        {
            float q = fx.T;
            if (q >= 0.4f)
                return;
            bool big = fx.Radius > 0.75f;
            float sc = big ? 1.3f : 0.85f, ang = (fx.Pos2 - fx.Pos).Angle, seed = fx.Pos.X * 0.37f + fx.Pos.Y * 0.71f;
            if (q < 0.06f)
            {
                float a = 0.6f * (1f - q / 0.06f);
                const int n = 12;
                for (int i = 0; i < n; i++)
                    VTri(fx.Pos, new Rgba(1f, 1f, 1f, a), fx.Pos + V2.FromAngle(i * MathX.Tau / n) * (22f * sc), LnGlow.WithAlpha(0f), fx.Pos + V2.FromAngle((i + 1) * MathX.Tau / n) * (22f * sc), LnGlow.WithAlpha(0f));
            }
            if (q < 0.22f)
                for (int i = 0; i < 6; i++)
                {
                    float s = (LnRnd(seed + i) - 0.5f) * 1.4f, r0 = 10f + q * 200f * (0.6f + 0.4f * LnRnd(seed + i + 2)) * sc, l = 6f + 8f * LnRnd(seed + i + 4);
                    V2 d = V2.FromAngle(ang + s);
                    Line(fx.Pos + d * r0, fx.Pos + d * (r0 + l), 1.6f, LnHot.WithAlpha(0.95f * (1f - q / 0.22f)));
                }
        }

        /// An Impact: an orange burst and ring where a shoved enemy hit another or a wall, and cracks in the floor at a wall.
        void DrawLanceImpactFx(Effect fx)
        {
            float q = fx.T, seed = fx.Pos.X * 0.53f + fx.Pos.Y * 0.29f;
            if (Mathf.RoundToInt(fx.Pos2.Y) == -1)  // a wall: the floor cracks where it hit
            {
                float al = Mathf.Clamp01(1f - q / 1.1f);
                var crack = new Rgba(0.078f, 0.086f, 0.118f, 0.85f * al);
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * MathX.Tau + LnRnd(seed + i) * 0.5f, l = 16f + LnRnd(seed + i + 3) * 26f;
                    V2 m = fx.Pos + V2.FromAngle(a) * (l * 0.5f) + new V2(LnRnd(i + seed) * 5f, 0f);
                    Line(fx.Pos, m, 2f, crack);
                    Line(m, fx.Pos + V2.FromAngle(a + 0.12f) * l, 2f, crack);
                }
                Circle(fx.Pos, 16f, LnImp.WithAlpha(0.22f * al), 16);
            }
            if (q >= 0.4f)
                return;
            float k = 1f - q / 0.4f;
            const int n = 14;
            for (int i = 0; i < n; i++)
                VTri(fx.Pos, new Rgba(1f, 0.94f, 0.82f, 0.7f * k), fx.Pos + V2.FromAngle(i * MathX.Tau / n) * 26f, LnImp.WithAlpha(0f), fx.Pos + V2.FromAngle((i + 1) * MathX.Tau / n) * 26f, LnImp.WithAlpha(0f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * MathX.Tau + LnRnd(seed + i), r = 8f + q * 90f;
                V2 d = V2.FromAngle(a);
                Line(fx.Pos + d * r, fx.Pos + d * (r + 8f), 2f, LnImp.WithAlpha(k));
            }
            Arc(fx.Pos, 10f + q * 55f, 2.5f, LnImp.WithAlpha(0.8f * k), 28);
        }

        /// The additive glow (Juice.cs DrawGlowPass): the flashes of hits and Impacts.
        void DrawLanceGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.LanceSpark && fx.T < 0.1f)
                    GlowDisc(fx.Pos, fx.Radius > 0.75f ? 30f : 20f, LnGlow, 0.5f * (1f - fx.T / 0.1f));
                else if (fx.Kind == FxKind.LanceImpact && fx.T < 0.3f)
                    GlowDisc(fx.Pos, 34f, LnImp, 0.6f * (1f - fx.T / 0.3f));
            }
        }

        // --- pieces ---------------------------------------------------------------------------------------------

        /// Both arms, pauldrons, the shield, the lance (and its shadow) and the gloves. `slide`: how far the lance has slid out
        /// through the glove (the hook); `glow`: its edge lit (a move playing).
        void DrawLancePose(V2 o, float aim, TwinBladesMotion.Pose m, float slide, bool glow, float t, float alpha)
        {
            V2 hR, dR, hL, dL;
            TwinHand(o, aim, m, true, out hR, out dR);
            TwinHand(o, aim, m, false, out hL, out dL);
            DrawWingedLance(hR + new V2(3f, 5f) + dR * slide, dR, true, false, t, alpha);
            float body = aim + m.Sp + m.Tw;
            V2 sR = o + LnRot(new V2(1f, 11.5f), body), sL = o + LnRot(new V2(1f, -11.5f), body);
            LanceArm(o, sL, hL, alpha);
            DrawHeaterShield(hL, dL, alpha);
            LanceArm(o, sR, hR, alpha);
            LancePauldron(sR, body, alpha);
            LancePauldron(sL, body, alpha);
            DrawWingedLance(hR + dR * slide, dR, false, glow, t, alpha);
            foreach (var h in new[] { hR, hL })
            {
                Circle(h, 5.3f, LnDark.WithAlpha(alpha), 12);
                Circle(h, 4.6f, LnGlove.WithAlpha(alpha), 12);
                Circle(h - new V2(1.2f, 1.5f), 1.4f, new Rgba(1f, 1f, 1f, 0.4f * alpha), 6);
            }
        }

        static V2 LnRot(V2 p, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new V2(p.X * c - p.Y * s, p.X * s + p.Y * c);
        }

        /// An arm from the shoulder `s` to the glove `h`, bowed at the elbow (away from the body) the shorter it is.
        void LanceArm(V2 o, V2 s, V2 h, float alpha)
        {
            V2 d = h - s;
            float l = Mathf.Max(0.001f, d.Length);
            V2 n = new V2(-d.Y / l, d.X / l);
            if (n.X * (s.X - o.X) + n.Y * (s.Y - o.Y) < 0f)
                n = n * -1f;
            V2 e = (s + h) * 0.5f + n * Mathf.Max(0f, 8f - l * 0.25f);
            for (int pass = 0; pass < 2; pass++)
            {
                float w = pass == 0 ? 8f : 5.4f;
                Rgba col = (pass == 0 ? LnDark : LnArm).WithAlpha(alpha);
                V2 prev = s;
                for (int i = 1; i <= 6; i++)
                {
                    float u = i / 6f, mu = 1f - u;
                    V2 pt = s * (mu * mu) + e * (2f * mu * u) + h * (u * u);
                    Line(prev, pt, w, col);
                    Circle(pt, w * 0.5f, col, 8);
                    prev = pt;
                }
                Circle(s, w * 0.5f, col, 8);
            }
        }

        /// A pauldron: a steel oval across the shoulder, lit on its outer side.
        void LancePauldron(V2 c, float body, float alpha)
        {
            V2 ax = V2.FromAngle(body), ay = new V2(-ax.Y, ax.X);
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n;
                VTri(c, LnDark.WithAlpha(alpha), c + ax * (Mathf.Cos(a0) * 7.6f) + ay * (Mathf.Sin(a0) * 6.2f), LnDark.WithAlpha(alpha), c + ax * (Mathf.Cos(a1) * 7.6f) + ay * (Mathf.Sin(a1) * 6.2f), LnDark.WithAlpha(alpha));
            }
            Rgba top = new Rgba(0.859f, 0.886f, 0.925f, alpha), bot = new Rgba(0.545f, 0.592f, 0.667f, alpha), mid = top.Mix(bot, 0.5f);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * MathX.Tau / n, a1 = (i + 1) * MathX.Tau / n, y0 = Mathf.Sin(a0), y1 = Mathf.Sin(a1);
                VTri(c, mid, c + ax * (Mathf.Cos(a0) * 6.8f) + ay * (y0 * 5.4f), top.Mix(bot, (y0 + 1f) * 0.5f), c + ax * (Mathf.Cos(a1) * 6.8f) + ay * (y1 * 5.4f), top.Mix(bot, (y1 + 1f) * 0.5f));
            }
        }

        /// The heater shield on the left glove, seen edge-on from above: a curved plate facing `face`, gold-rimmed, with a boss.
        void DrawHeaterShield(V2 hand, V2 face, float alpha)
        {
            float ang = face.Angle;
            V2 c = hand - face * 9f;
            Arc(c, 12.25f, 5.5f, LnShield.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            Arc(c, 15f, 1.6f, LnDark.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            Arc(c, 9.5f, 1.6f, LnDark.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 d = V2.FromAngle(ang + 0.95f * k);
                Line(c + d * 9.5f, c + d * 15f, 1.6f, LnDark.WithAlpha(alpha));
            }
            Arc(c, 14f, 1.3f, LnGold.WithAlpha(alpha), 14, ang - 0.88f, 1.76f);
            Arc(c, 11.5f, 1f, new Rgba(1f, 1f, 1f, 0.25f * alpha), 10, ang - 0.7f, 0.9f);
            Circle(hand + face * 3.4f, 1.8f, LnGold.WithAlpha(alpha), 8);
        }

        // the lance's frame while it is drawn: LP(u, v) is a point on it (u along it from the glove, v across, + to its right)
        V2 lnGrip, lnDir, lnSide;
        V2 LP(float u, float v) => lnGrip + lnDir * u + lnSide * v;

        static V2 Q(V2 a, V2 b, V2 c, float t)
        {
            float m = 1f - t;
            return a * (m * m) + b * (2f * m * t) + c * (t * t);
        }

        /// The Winged War Lance with its grip at `grip`, pointing along `dir`: an ash shaft with a butt spike and a wrapped grip,
        /// a swallow-tail pennant streaming back from behind the head, a socket with two lugs, a steel leaf blade (its edge lit
        /// while `glow`). `shadow`: its shadow only.
        void DrawWingedLance(V2 grip, V2 dir, bool shadow, bool glow, float t, float alpha)
        {
            const float L = LanceMotion.Len, B = LanceMotion.Butt;
            lnGrip = grip; lnDir = dir; lnSide = new V2(-dir.Y, dir.X);
            if (shadow)
            {
                Line(LP(-B, 0f), LP(L, 0f), 5f, new Rgba(0f, 0f, 0f, 0.28f * alpha));
                return;
            }
            var dark = LnOutline.WithAlpha(0.9f * alpha);
            Line(LP(-B, 0f), LP(L - 30f, 0f), 4.6f, new Rgba(0.165f, 0.102f, 0.055f, alpha));
            Line(LP(-B, 0f), LP(L - 30f, 0f), 3.2f, new Rgba(0.576f, 0.38f, 0.227f, alpha));
            Line(LP(-B + 2f, -0.8f), LP(L - 32f, -0.8f), 0.8f, new Rgba(0.788f, 0.604f, 0.4f, alpha));
            SetColor(new Rgba(0.722f, 0.761f, 0.816f, alpha));
            Tri(LP(-B, -2f), LP(-B - 7f, 0f), LP(-B, 2f));
            Line(LP(-9f, 0f), LP(6f, 0f), 5.4f, new Rgba(0.227f, 0.149f, 0.09f, alpha));
            // the pennant: a swallow tail streaming back from behind the head, rippling
            float w = Mathf.Sin(t * 10f) * 2.2f, w2 = Mathf.Sin(t * 10f + 1.3f) * 2.6f;
            const int n = 7;
            var top = new V2[n + 1];
            var bot = new V2[n + 1];
            top[0] = LP(L - 38f, 1.6f);
            for (int i = 1; i <= n; i++)
                top[i] = Q(LP(L - 48f, 1.6f), LP(L - 60f, 6f + w), LP(L - 78f, 10f + w2), (i - 1f) / (n - 1f));
            for (int i = 0; i <= n; i++)
                bot[i] = Q(LP(L - 38f, 6.5f), LP(L - 56f, 13f + w), LP(L - 74f, 15f + w2), (float)i / n);
            V2 notch = LP(L - 66f, 6.5f + w * 0.6f);
            var pen = LnPennant.WithAlpha(alpha);
            SetColor(pen);
            for (int i = 0; i < n - 1; i++)
            {
                Tri(top[i], top[i + 1], bot[i + 1]);
                Tri(top[i], bot[i + 1], bot[i]);
            }
            Tri(top[n - 1], top[n], notch);
            Tri(top[n - 1], notch, bot[n - 1]);
            Tri(bot[n - 1], notch, bot[n]);
            var rim = LnOutline.WithAlpha(0.9f * alpha);
            for (int i = 0; i < n; i++)
            {
                Line(top[i], top[i + 1], 0.9f, rim);
                Line(bot[i], bot[i + 1], 0.9f, rim);
            }
            Line(top[n], notch, 0.9f, rim);
            Line(notch, bot[n], 0.9f, rim);
            Line(top[0], bot[0], 0.9f, rim);
            V2 prev = LP(L - 40f, 4f);
            for (int i = 1; i <= 6; i++)  // its gold stripe
            {
                V2 pt = Q(LP(L - 40f, 4f), LP(L - 56f, 9f + w), LP(L - 70f, 10.5f + w2), i / 6f);
                Line(prev, pt, 1.2f, LnGold.WithAlpha(alpha));
                prev = pt;
            }
            // the socket and its two lugs
            SetColor(new Rgba(0.604f, 0.651f, 0.722f, alpha));
            Quad(LP(L - 36f, -2.7f), LP(L - 28f, -2.7f), LP(L - 28f, 2.7f), LP(L - 36f, 2.7f));
            Line(LP(L - 36f, -2.7f), LP(L - 28f, -2.7f), 0.9f, dark);
            Line(LP(L - 36f, 2.7f), LP(L - 28f, 2.7f), 0.9f, dark);
            Line(LP(L - 36f, -2.7f), LP(L - 36f, 2.7f), 0.9f, dark);
            Line(LP(L - 28f, -2.7f), LP(L - 28f, 2.7f), 0.9f, dark);
            Line(LP(L - 31f, -2.4f), LP(L - 35f, -8f), 2f, new Rgba(0.682f, 0.722f, 0.776f, alpha));
            Line(LP(L - 31f, 2.4f), LP(L - 35f, 8f), 2f, new Rgba(0.682f, 0.722f, 0.776f, alpha));
            // the leaf blade
            const int bn = 8;
            var bt = new V2[bn + 1];
            var bb = new V2[bn + 1];
            for (int i = 0; i <= bn; i++)
            {
                float u = (float)i / bn;
                bt[i] = Q(LP(L - 28f, -2.4f), LP(L - 15f, -6.8f), LP(L, 0f), u);
                bb[i] = Q(LP(L - 28f, 2.4f), LP(L - 15f, 6.8f), LP(L, 0f), u);
            }
            SetColor(new Rgba(0.91f, 0.933f, 0.965f, alpha));
            for (int i = 0; i < bn; i++)
            {
                Tri(bt[i], bt[i + 1], bb[i + 1]);
                Tri(bt[i], bb[i + 1], bb[i]);
            }
            for (int i = 0; i < bn; i++)
            {
                Line(bt[i], bt[i + 1], 1f, dark);
                Line(bb[i], bb[i + 1], 1f, dark);
            }
            Line(bt[0], bb[0], 1f, dark);
            Line(LP(L - 26f, 0f), LP(L - 5f, 0f), 0.9f, new Rgba(0.353f, 0.408f, 0.502f, 0.85f * alpha));
            if (glow)
            {
                V2 gp = LP(L - 26f, -2.8f);
                for (int i = 1; i <= 6; i++)
                {
                    V2 pt = Q(LP(L - 26f, -2.8f), LP(L - 15f, -6f), LP(L - 1f, -0.4f), i / 6f);
                    Line(gp, pt, 0.9f, LnHot.WithAlpha(0.9f * alpha));
                    gp = pt;
                }
            }
        }

        /// Four diamonds over the knight's head: lit for each step thrown in this chain; the fourth glows when the Breaching
        /// Thrust is next (or a hook primed it).
        void DrawLancePips(Player p, float t)
        {
            if (p.Id == game.MyId && game.LanceHookCd > 0f)  // Hook & Haul coming back: a ring filling under your feet
            {
                float k = 1f - game.LanceHookCd / Game.HookCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, LnGlow.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            int step = TwinBladesMotion.StepOf(p.Combo);
            if (step == 0 && !p.LancePrimed)
                return;
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            for (int i = 0; i < 4; i++)
            {
                V2 at = c + new V2((i - 1.5f) * 10f, 0f);
                float r = i == 3 ? 3.8f : 2.9f;
                bool lit = i < step, glow = i == 3 && (p.LancePrimed || step == 3);
                Rgba col = lit ? LnGlow : glow ? LnHot.WithAlpha(0.55f + 0.35f * Mathf.Sin(t * 14f)) : new Rgba(1f, 1f, 1f, 0.14f);
                Polygon(at, (r + (glow ? 0.8f : 0.5f)) * 1.414f, 4, 0f, glow ? LnHot.WithAlpha(0.95f) : new Rgba(0f, 0f, 0f, 0.6f));  // (a square of side 2r turned to a diamond)
                Polygon(at, r * 1.414f, 4, 0f, col);
            }
        }
    }
}
