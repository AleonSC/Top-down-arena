using UnityEngine;
using System.Collections.Generic;

namespace Arena
{
    /// The Nunchaku's Twin Storm, held and swung (Docs/Mockups/ninja-nunchaku.html; the motion is Core/NunchakuMotion.cs, which the
    /// host also times the hits from). The Ninja's model leaves its arms out with the Nunchaku; this draws both arms and a black
    /// lacquered chuck with crimson bands in each hand, its chain and its swinging stick, which trails the handle (the whip):
    ///   - tucked at the ready, breathing, or spinning the Twin Windmill's two discs while the guard is up (DrawNunIdle);
    ///   - each combo step and the Twin Counter (FxKind.NunStep, Radius = id * 10 + step), posed from its age with a crimson smear
    ///     off each swinging stick; the Twin Crush's shockwave and cracks (DrawNunStepFx) and the counter's lash (DrawNunCounterFx);
    ///   - changes of pose (a step ending, the guard going up or down) blend over NunBlend.
    /// Four diamonds over the Ninja's head show the combo; in the guard, eight ticks show the shots swatted into the counter.
    public partial class ArenaApp
    {
        static readonly Rgba NunLacquer = new Rgba(0.1f, 0.08f, 0.09f), NunBand = new Rgba(1f, 0.3f, 0.37f), NunCap = new Rgba(0.81f, 0.84f, 0.87f),
            NunChain = new Rgba(0.64f, 0.67f, 0.71f), NunInk = new Rgba(0.03f, 0.03f, 0.04f), NunSleeve = new Rgba(0.13f, 0.13f, 0.18f), NunWrap = new Rgba(0.78f, 0.6f, 0.4f);
        const float NunBlend = 0.1f, NunStickW = 4.6f, NunDiscIn = 0.2f, NunDiscOut = 0.08f;
        const int NunTrailN = 9;
        const float NunTrailDt = 0.014f;

        class NunView { public int Key = -1; public float Since = 99f; public NunchakuMotion.Pose From, Last; public float GuardSince = 99f, Disc; public bool Guard; }
        readonly Dictionary<int, NunView> nunViews = new Dictionary<int, NunView>();

        /// The step effect of this player that is playing now (the newest), or null.
        Effect NunFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in FxOf(FxKind.NunStep))
                if (fx.Kind == FxKind.NunStep && fx.T < fx.Dur && Mathf.RoundToInt(fx.Radius) / 10 == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// The pose a player's chucks are in `back` seconds ago (0: now): a step's, the guard's or the ready pose, and the aim it's along.
        NunchakuMotion.Pose NunTarget(Player p, NunView v, Effect fx, float t, float back, out float aim)
        {
            aim = p.Aim;
            if (fx != null)
            {
                var s = NunchakuMotion.StepOf(Mathf.RoundToInt(fx.Radius) % 10);
                if (s != null && fx.T - back < s.Dur)
                {
                    aim = (fx.Pos2 - fx.Pos).Angle;
                    return NunchakuMotion.At(s, fx.T - back, t - back);
                }
            }
            if (v.Guard)
                return NunchakuMotion.Guard(v.GuardSince - back);
            return NunchakuMotion.Ready(t - back);
        }

        /// The chucks in hand: tucked, swung or spinning the guard (from the player loop).
        void DrawNunIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Nunchaku || p.Downed)
                return;
            NunView v;
            if (!nunViews.TryGetValue(p.Id, out v))
                nunViews[p.Id] = v = new NunView { From = NunchakuMotion.Ready(t), Last = NunchakuMotion.Ready(t) };
            float dt = Time.deltaTime;
            if (p.NunGuard != v.Guard)
            {
                v.Guard = p.NunGuard;
                v.GuardSince = 0f;
            }
            else
                v.GuardSince += dt;
            var fx = NunFxOf(p.Id);
            var step = fx != null ? NunchakuMotion.StepOf(Mathf.RoundToInt(fx.Radius) % 10) : null;
            if (step != null && fx.T >= step.Dur)
                step = null;  // (settling back: the ready or guard pose takes it from here)
            int key = step != null ? fx.GetHashCode() : v.Guard ? 1 : 0;
            if (key != v.Key)
            {
                if (key == 1 && v.Key != 0)
                    v.GuardSince = 0f;  // back from the counter: the windmill spins up again from the guard's start
                v.Key = key;
                v.From = v.Last;
                v.Since = 0f;
            }
            else
                v.Since += dt;
            float aim, aimLag;
            var now = NunTarget(p, v, step != null ? fx : null, t, 0f, out aim);
            var lag = NunTarget(p, v, step != null ? fx : null, t, NunchakuMotion.Lag, out aimLag);
            float k = Mathf.Clamp01(v.Since / NunBlend);
            k = k * k * (3f - 2f * k);
            if (k < 1f)
            {
                now = NunchakuMotion.Mix(v.From, now, k);
                lag = NunchakuMotion.Mix(v.From, lag, k);
            }
            v.Last = now;
            V2 o = p.Disp;
            // the windmill's discs: gone while an attack plays, spun back up as the chucks return to it
            float discTo = v.Guard && step == null ? 1f : 0f;
            v.Disc = Mathf.MoveTowards(v.Disc, discTo, dt / (discTo > v.Disc ? NunDiscIn : NunDiscOut));
            if (v.Disc > 0.01f)
                DrawNunDiscs(p, o, p.Aim, t, v.Disc * v.Disc * (3f - 2f * v.Disc));
            if (step != null)
                DrawNunTrail(o, aim, fx, step, t);
            DrawNunPose(o, aim, now, lag, 1f);
            DrawNunPips(p, t);
        }

        /// The crimson smear off each swinging stick while a step plays: the stick's sweep a few moments back, fading.
        void DrawNunTrail(V2 o, float aim, Effect fx, NunchakuMotion.Step s, float t)
        {
            for (int h = 0; h < 2; h++)
            {
                V2 pTip = default(V2), pBase = default(V2);
                bool have = false;
                for (int i = 0; i <= NunTrailN; i++)
                {
                    float ci = fx.T - i * NunTrailDt;
                    if (ci < 0f || ci >= s.Dur) { have = false; continue; }
                    var m = NunchakuMotion.At(s, ci, t);
                    var ml = NunchakuMotion.At(s, ci - NunchakuMotion.Lag, t);
                    V2 hand, a, b, c;
                    NunPoints(o, aim, h == 0 ? m.R : m.L, h == 0 ? ml.R : ml.L, out hand, out a, out b, out c);
                    if (have)
                    {
                        float moved = (c - pTip).Length;
                        float a0 = 1f - (i - 1f) / NunTrailN, a1 = 1f - (float)i / NunTrailN, sp = Mathf.Clamp01(moved / 7f);
                        a0 *= a0 * sp; a1 *= a1 * sp;
                        VTri(pTip, NunBand.WithAlpha(0.3f * a0), c, NunBand.WithAlpha(0.3f * a1), b, NunBand.WithAlpha(0f));
                        VTri(pTip, NunBand.WithAlpha(0.3f * a0), b, NunBand.WithAlpha(0f), pBase, NunBand.WithAlpha(0f));
                        Line(pTip, c, 2.4f * a0 + 0.4f, new Rgba(1f, 0.82f, 0.84f, 0.8f * a0));
                    }
                    pTip = c; pBase = b; have = true;
                }
            }
        }

        /// One hand's chuck: the hand, the held stick's end, the chain's end, the swinging stick's tip.
        static void NunPoints(V2 o, float aim, NunchakuMotion.Hand m, NunchakuMotion.Hand lag, out V2 hand, out V2 a, out V2 b, out V2 c)
        {
            V2 fwd = V2.FromAngle(aim), side = V2.FromAngle(aim + MathX.Pi / 2f);
            hand = o + fwd * m.X + side * m.Y;
            a = hand + V2.FromAngle(aim + m.Th) * NunchakuMotion.Handle;
            float free = NunchakuMotion.FreeAngle(m.Th, lag.Th);
            V2 d = V2.FromAngle(aim + free);
            b = a + d * NunchakuMotion.Link;
            c = b + d * NunchakuMotion.Stick;
        }

        /// Both arms, both chucks (their shadows on the floor first) and the wrapped hands.
        void DrawNunPose(V2 o, float aim, NunchakuMotion.Pose m, NunchakuMotion.Pose lag, float alpha)
        {
            V2 hR, aR, bR, cR, hL, aL, bL, cL;
            NunPoints(o, aim, m.R, lag.R, out hR, out aR, out bR, out cR);
            NunPoints(o, aim, m.L, lag.L, out hL, out aL, out bL, out cL);
            V2 sh = new V2(3f, 5f);
            Rgba shc = new Rgba(0f, 0f, 0f, 0.26f * alpha);
            foreach (var q in new[] { new[] { hR, aR, bR, cR }, new[] { hL, aL, bL, cL } })
            {
                Line(q[0] + sh, q[1] + sh, NunStickW, shc);
                Line(q[1] + sh, q[2] + sh, 1.4f, shc);
                Line(q[2] + sh, q[3] + sh, NunStickW, shc);
            }
            for (int k = 0; k < 2; k++)  // the arms, shoulder to hand
            {
                V2 s = o + V2.FromAngle(aim + (k == 0 ? 1.45f : -1.45f)) * 10.5f, hand = k == 0 ? hR : hL;
                V2 d = hand - s, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                if (n.X * (s.X - o.X) + n.Y * (s.Y - o.Y) < 0f)
                    n = n * -1f;
                V2 elbow = (s + hand) * 0.5f + n * Mathf.Max(0f, 6f - d.Length * 0.2f);
                Line(s, elbow, 7.4f, NunInk.WithAlpha(alpha));
                Line(elbow, hand, 7.4f, NunInk.WithAlpha(alpha));
                Line(s, elbow, 5f, NunSleeve.WithAlpha(alpha));
                Line(elbow, hand, 5f, NunSleeve.WithAlpha(alpha));
                Circle(s, 5.2f, NunInk.WithAlpha(alpha), 12);
                Circle(s, 4.2f, NunSleeve.Mix(NunBand, 0.12f).WithAlpha(alpha), 12);
            }
            DrawNunChuck(hR, aR, bR, cR, alpha);
            DrawNunChuck(hL, aL, bL, cL, alpha);
            foreach (var h in new[] { hR, hL })
            {
                Circle(h, 4.6f, NunInk.WithAlpha(alpha), 12);
                Circle(h, 3.5f, NunWrap.WithAlpha(alpha), 12);
                Line(h - new V2(2.4f, 0.8f), h + new V2(2.4f, -0.8f), 0.8f, new Rgba(0.45f, 0.32f, 0.2f, 0.8f * alpha));  // the wrap's turns
            }
        }

        /// One chuck: the held stick, a chain of four links, the swinging stick.
        void DrawNunChuck(V2 h, V2 a, V2 b, V2 c, float alpha)
        {
            NunStick(h - (a - h).Normalized() * 3f, a, alpha);
            for (int i = 0; i <= 3; i++)
            {
                V2 at = a + (b - a) * (i / 3f);
                Circle(at, 1.9f, NunInk.WithAlpha(alpha), 8);
                Circle(at, 1.3f, NunChain.WithAlpha(alpha), 8);
            }
            NunStick(b, c, alpha);
        }

        /// A stick from `a` to `b`: black lacquer, steel caps at both ends, a crimson band inside each, a thin highlight.
        void NunStick(V2 a, V2 b, float alpha)
        {
            V2 d = b - a;
            float n = d.Length;
            if (n < 0.01f)
                return;
            V2 u = d * (1f / n), side = u.Orthogonal();
            Line(a, b, NunStickW + 1.8f, NunInk.WithAlpha(alpha));
            Line(a, b, NunStickW, NunLacquer.WithAlpha(alpha));
            Line(a + u * 0.8f, a + u * 3.5f, NunStickW + 0.4f, NunCap.WithAlpha(alpha));
            Line(b - u * 3.5f, b - u * 0.8f, NunStickW + 0.4f, NunCap.WithAlpha(alpha));
            Line(a + u * 5.5f, a + u * 7.8f, NunStickW + 0.2f, NunBand.WithAlpha(alpha));
            Line(b - u * 7.8f, b - u * 5.5f, NunStickW + 0.2f, NunBand.WithAlpha(alpha));
            Line(a + side * (NunStickW * 0.22f) + u * 4f, b + side * (NunStickW * 0.22f) - u * 4f, 0.8f, new Rgba(1f, 1f, 1f, 0.2f * alpha));
        }

        /// The Twin Windmill's two discs: a faint crimson disc at each side in front, three arcs spinning in it (opposite ways).
        /// `k` 0..1 fades and grows them in (an attack from the guard drops them; they spin back up after).
        void DrawNunDiscs(Player p, V2 o, float aim, float t, float k)
        {
            float r = Game.NunDisc(p) * (0.55f + 0.45f * k);
            for (int h = 0; h < 2; h++)
            {
                V2 c = Game.NunDiscAt(o, aim, h == 0);
                const int N = 20;
                for (int i = 0; i < N; i++)
                {
                    float a0 = MathX.Tau * i / N, a1 = MathX.Tau * (i + 1) / N;
                    VTri(c, NunBand.WithAlpha(0.02f * k), c + V2.FromAngle(a0) * r, NunBand.WithAlpha(0.13f * k), c + V2.FromAngle(a1) * r, NunBand.WithAlpha(0.13f * k));
                }
                float spin = (h == 0 ? -1f : 1f) * NunchakuMotion.GuardSpin * t;
                for (int i = 0; i < 3; i++)
                    Arc(c, r - 5f - i * 7f, 2f - i * 0.45f, NunBand.WithAlpha((0.5f - 0.1f * i) * k), 12, spin + i * 2.1f, 1.4f);
                Arc(c, r, 1f, NunBand.WithAlpha(0.25f * k), 28);
            }
        }

        /// Four diamonds over the head for the combo (the fourth, the Twin Crush, glows when it's next); in the guard, a tick for
        /// each shot swatted into the counter.
        void DrawNunPips(Player p, float t)
        {
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            if (p.NunGuard)
            {
                for (int i = 0; i < Game.NunSwatCap; i++)
                {
                    V2 at = c + new V2((i - (Game.NunSwatCap - 1) / 2f) * 6f, 0f);
                    bool lit = i < p.NunSwats;
                    Line(at - new V2(0f, 3.5f), at + new V2(0f, 3.5f), 3.2f, new Rgba(0f, 0f, 0f, 0.55f));
                    Line(at - new V2(0f, 2.8f), at + new V2(0f, 2.8f), 2f, lit ? NunBand : new Rgba(1f, 1f, 1f, 0.15f));
                }
                return;
            }
            int step = Game.NunStepOf(p.Combo);
            if (step == 0)
                return;
            for (int i = 0; i < 4; i++)
            {
                V2 at = c + new V2((i - 1.5f) * 9f, 0f);
                float r = i == 3 ? 3.8f : 3f;
                bool lit = i < step, next = i == 3 && step == 3;
                Rgba col = lit ? NunBand : next ? new Rgba(1f, 0.6f, 0.62f, 0.6f + 0.4f * Mathf.Sin(t * 14f)) : new Rgba(1f, 1f, 1f, 0.15f);
                Polygon(at, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                Polygon(at, r, 4, 0f, col);
            }
        }

        /// A step on the ground: the Twin Crush's shockwave, white flash and cracks where the chucks land (and an aftershock ring
        /// further out for each extra projectile). The chucks themselves are drawn from the player loop (DrawNunIdle).
        void DrawNunStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), step = code % 10;
            if (step != 4)
                return;
            var s = NunchakuMotion.Steps[3];
            float land = s.Dur * Game.NunCrushAt;
            V2 dir = (fx.Pos2 - fx.Pos).Normalized();
            if (dir.IsZero)
                dir = new V2(1f, 0f);
            for (int k = 0; k < 4; k++)
            {
                float u = fx.T - land - Game.NunAftershockDelay * k;
                if (u < 0f || u > 0.7f || k > 0 && fx.Dur < s.Dur + NunchakuMotion.Recover + Game.NunAftershockDelay * k - 0.001f)
                    continue;
                V2 at = fx.Pos + dir * (Game.NunCrushAt0 + Game.NunAftershockOut * k);
                float R = Game.NunCrushR * (k == 0 ? 1f : 0.85f), v = Mathf.Clamp01(u / 0.3f), e = 1f - (1f - v) * (1f - v) * (1f - v);
                float fade = 1f - Mathf.Clamp01(u / 0.7f);
                Arc(at, R * e, 3.4f * (1f - v) + 0.8f, NunBand.WithAlpha((1f - v) * 0.9f), 40);
                Arc(at, R * e * 0.92f, 1.2f, new Rgba(1f, 0.9f, 0.9f, (1f - v) * 0.6f), 40);
                if (u < 0.12f)
                    Circle(at, 10f + 50f * u, new Rgba(1f, 1f, 1f, 0.45f * (1f - u / 0.12f)), 20);
                for (int i = 0; i < 9; i++)  // cracks out from the impact
                {
                    float a = i * 0.7f + at.X * 0.013f, l = R * 0.5f * (1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.12f), 3f));
                    V2 m = at + V2.FromAngle(a + 0.15f) * l * 0.55f;
                    Line(at + V2.FromAngle(a) * 6f, m, 1.6f, new Rgba(0f, 0f, 0f, 0.6f * fade));
                    Line(m, at + V2.FromAngle(a - 0.1f) * l, 1.1f, new Rgba(0f, 0f, 0f, 0.5f * fade));
                    Line(m, at + V2.FromAngle(a - 0.1f) * l, 0.6f, NunBand.WithAlpha(0.45f * fade * (1f - v)));
                }
            }
        }

        /// The Twin Counter's lash: a bright tapered streak out to its reach as it lands, thicker for every shot swatted into it.
        void DrawNunCounterFx(Effect fx)
        {
            float land = NunchakuMotion.Counter.Dur * Game.NunCounterAt, u = fx.T - land;
            if (u < 0f)
                return;
            float al = 1f - Mathf.Clamp01(u / 0.35f), w = 3f + 0.8f * fx.Radius;
            V2 dir = (fx.Pos2 - fx.Pos).Normalized(), side = dir.Orthogonal();
            V2 a = fx.Pos + dir * 14f, b = fx.Pos2;
            VTri(a + side * w, NunBand.WithAlpha(0.75f * al), a - side * w, NunBand.WithAlpha(0.75f * al), b, NunBand.WithAlpha(0f));
            Line(a, a + (b - a) * (0.4f + 0.6f * Mathf.Clamp01(u / 0.08f)), 1.2f, new Rgba(1f, 1f, 1f, al));
            if (fx.Radius >= 1f)
                for (int i = 0; i < 6; i++)  // sparks along it, one for each couple of swats
                {
                    float k = (i + 0.5f) / 6f;
                    V2 at = a + (b - a) * k + side * Mathf.Sin(i * 2.3f + fx.Pos.X) * w * 1.6f * Mathf.Clamp01(u / 0.2f);
                    Circle(at, 1.6f, NunBand.Mix(new Rgba(1f, 1f, 1f), 0.5f).WithAlpha(al), 6);
                }
        }
    }
}
