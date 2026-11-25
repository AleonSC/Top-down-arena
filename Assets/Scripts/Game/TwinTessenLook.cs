using UnityEngine;

namespace Arena
{
    /// The Twin Tessen's look (the "Twin Tessen" mockup): an iron war fan in each hand, cream paper with a red sun band, dark
    /// guard sticks and a teal iron rim, posed from Game.TtPose (the same poses the hits are timed from):
    ///   - in hand (DrawHeldTessen, from the model's two-hand frames; TtHandPose places the hands and turns the fans): shut at the
    ///     ready, snapping open and sweeping with each step of the Crane Dance, pulled back while the gust charges, flung forward
    ///     as it goes;
    ///   - each step's trail (FxKind.TessenStep, Radius = id * 10 + step): a paper-white crescent over the swept arc, the Petal
    ///     Spin's ring of petals, the Crane Bloom's petal wave;
    ///   - the Petal Spin's petals flying to who they heal (FxKind.TessenPetals);
    ///   - the Kamikaze Gust (FxKind.TessenGust): a cone of wind with a bright front racing out, streaks and petals; gold for a Typhoon;
    ///   - under the player: the charging cone with wind streaming in, Tailwind streaks, the Breeze arc over the head (gold and
    ///     pulsing when full: the next gust is a Typhoon).
    public partial class ArenaApp
    {
        static readonly Rgba TtInk = new Rgba(0.23f, 0.15f, 0.13f), TtGuard = new Rgba(0.1f, 0.15f, 0.26f), TtPaperDeep = new Rgba(0.9f, 0.82f, 0.69f);
        float ttOpenL = 0.2f, ttOpenR = 0.2f;  // the fans' openness for the two frames being drawn (set by TtHandPose)

        static float TtHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }

        /// This player's newest step effect, or null.
        Effect TtStepFx(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.TessenStep && fx.T < fx.Dur && Mathf.RoundToInt(fx.Radius) / 10 == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// This player's newest gust, or null.
        Effect TtGustFx(Player p)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.TessenGust && fx.T < fx.Dur && fx.Pos.DistanceSq(p.Pos) < 60f * 60f && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// Where the fans are now: from the charge, the gust's fling, the newest step (easing back to rest after it), or at rest.
        void TtPoseNow(Player p, out float aL, out float rL, out float oL, out float aR, out float rR, out float oR)
        {
            Game.TtPose(0, 0f, out aL, out rL, out oL, out aR, out rR, out oR);
            if (p.Downed)
                return;
            if (p.TtCharging)
            {
                float r;
                Game.TtChargePose(Mathf.Clamp01(p.TtCharge / Game.TtChargeFull), out aL, out aR, out r);
                rL = rR = r;
                oL = oR = 1f;
                return;
            }
            var g = TtGustFx(p);
            if (g != null && g.T < 0.3f)
            {
                float k = Game.TtEase(g.T / 0.14f), back = Mathf.Clamp01((g.T - 0.16f) / 0.14f);
                float cl, cr, cRad;
                Game.TtChargePose(1f, out cl, out cr, out cRad);
                float fl = Mathf.Lerp(cl, -0.4f, k), fr = Mathf.Lerp(cr, 0.4f, k);
                aL = Mathf.Lerp(fl, aL, back); aR = Mathf.Lerp(fr, aR, back);
                rL = rR = Mathf.Lerp(Mathf.Lerp(cRad, 22f, k), 14f, back);
                oL = oR = Mathf.Lerp(1.1f, 0.2f, back);
                return;
            }
            var fx = TtStepFx(p.Id);
            if (fx == null)
                return;
            int step = Mathf.RoundToInt(fx.Radius) % 10;
            if (step < 1 || step > Game.TtSteps)
                return;
            float dur = Game.TtDur[step - 1];
            float bL, bRl, bOl, bR, bRr, bOr;
            Game.TtPose(step, Mathf.Clamp01(fx.T / dur), out bL, out bRl, out bOl, out bR, out bRr, out bOr);
            float rest = Mathf.Clamp01((fx.T - dur) / 0.2f);
            rest = rest * rest * (3f - 2f * rest);
            if (step == 4)  // (the twirl ends a full two turns round: ease home the short way)
            {
                bL = Mathf.Repeat(bL + MathX.Pi, MathX.Tau) - MathX.Pi;
                bR = Mathf.Repeat(bR + MathX.Pi, MathX.Tau) - MathX.Pi;
            }
            aL = Mathf.Lerp(bL, aL, rest); rL = Mathf.Lerp(bRl, rL, rest); oL = Mathf.Lerp(bOl, oL, rest);
            aR = Mathf.Lerp(bR, aR, rest); rR = Mathf.Lerp(bRr, rR, rest); oR = Mathf.Lerp(bOr, oR, rest);
        }

        /// The model's hands (model frame: +x your aim, +y your right) out to each fan, and each fan turned to point along its angle.
        void TtHandPose(Player p, ref float rhx, ref float rhy, ref float lhx, ref float lhy, out float twist, out float climbR, out float climbL)
        {
            float aL, rL, oL, aR, rR, oR;
            TtPoseNow(p, out aL, out rL, out oL, out aR, out rR, out oR);
            rhx = Mathf.Cos(aR) * rR; rhy = Mathf.Sin(aR) * rR;
            lhx = Mathf.Cos(aL) * rL; lhy = Mathf.Sin(aL) * rL;
            twist = 0f;
            climbR = aR;   // (the right frame turns by -twist + climbR)
            climbL = -aL;  // (the left by twist - climbL)
            ttOpenL = oL;
            ttOpenR = oR;
        }

        /// One fan in its hand's frame (pivot in the hand, +x out along the fan): mHeldTube says which hand (1 right).
        void DrawHeldTessen(Player p, float t, int hold)
        {
            float open = Mathf.Clamp(mHeldTube == 1 ? ttOpenR : ttOpenL, 0.12f, 1.25f), half = open * 75f * Mathf.Deg2Rad;
            const float R = 17f;
            const int N = 10;
            // the paper: deep near the pivot, pale out to the rim
            for (int i = 0; i < N; i++)
            {
                float a0 = -half + 2f * half * i / N, a1 = -half + 2f * half * (i + 1) / N;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                MPoly(TtPaperDeep, 2f * c0, 2f * s0, R * 0.5f * c0, R * 0.5f * s0, R * 0.5f * c1, R * 0.5f * s1, 2f * c1, 2f * s1);
                MPoly(Game.TtPaper, R * 0.5f * c0, R * 0.5f * s0, R * c0, R * s0, R * c1, R * s1, R * 0.5f * c1, R * 0.5f * s1);
            }
            if (open > 0.45f)
                MArc(0f, 0f, R * 0.64f, -half * 0.96f, half * 0.96f, 3.6f, Game.TtSun);  // the red sun band
            int ribs = 7;
            for (int i = 1; i < ribs; i++)  // the ribs
            {
                float a = -half + 2f * half * i / ribs;
                MLine(1.5f * Mathf.Cos(a), 1.5f * Mathf.Sin(a), R * Mathf.Cos(a), R * Mathf.Sin(a), 0.5f, TtInk.WithAlpha(0.45f));
            }
            MArc(0f, 0f, R, -half, half, 1.8f, Game.TtWind);  // the iron rim
            MLine(0f, 0f, R * Mathf.Cos(-half), R * Mathf.Sin(-half), 2.4f, TtGuard);  // the guard sticks
            MLine(0f, 0f, R * Mathf.Cos(half), R * Mathf.Sin(half), 2.4f, TtGuard);
            MCirc(0f, 0f, 1.6f, Game.TtGold, 8);  // the rivet
        }

        /// FxKind.TessenStep: each step's trail, from its age.
        void DrawTessenStep(Effect fx)
        {
            int step = Mathf.RoundToInt(fx.Radius) % 10;
            if (step < 1 || step > Game.TtSteps)
                return;
            float dur = Game.TtDur[step - 1], aim = (fx.Pos2 - fx.Pos).Angle, reach = (fx.Pos2 - fx.Pos).Length, k = Mathf.Clamp01(fx.T / dur);
            float fade = 1f - Mathf.Clamp01((fx.T - dur * 0.6f) / (fx.Dur - dur * 0.6f));
            V2 c = fx.Pos;
            Player p;
            if (game.Players.TryGetValue(Mathf.RoundToInt(fx.Radius) / 10, out p) && step < 5)
                c = p.Disp;  // (a melee trail stays on you)
            float aL0, rL, oL, aR0, rR, oR, aL, aR;
            Game.TtPose(step, 0f, out aL0, out rL, out oL, out aR0, out rR, out oR);
            Game.TtPose(step, k, out aL, out rL, out oL, out aR, out rR, out oR);
            float seed = fx.Pos.X * 0.37f + fx.Pos.Y * 1.13f;
            switch (step)
            {
                case 1:
                    TtSweep(c, reach * 0.8f, aim + aL0, aL - aL0, fade);
                    break;
                case 2:
                    TtSweep(c, reach * 0.8f, aim + aR0, aR - aR0, fade);
                    break;
                case 3:
                    TtSweep(c, reach * 0.82f, aim + aL0, aL - aL0, fade);
                    TtSweep(c, reach * 0.82f, aim + aR0, aR - aR0, fade);
                    if (k > Game.TtHitAt)  // the crossing's spark
                        Circle(c + V2.FromAngle(aim) * reach * 0.55f, 9f * fade, Game.TtPaper.WithAlpha(0.7f * fade), 12);
                    break;
                case 4:
                {
                    float r = reach * 0.85f;
                    Arc(c, r, 10f, Game.TtPetal.WithAlpha(0.18f * fade), 40);
                    Arc(c, r, 3f, Game.TtPaper.WithAlpha(0.75f * fade), 32, aim + aL, -2.2f);
                    Arc(c, r, 3f, Game.TtPaper.WithAlpha(0.75f * fade), 32, aim + aR, -2.2f);
                    if (fx.T > dur * Game.TtHitAt)  // petals shaken off the rim
                        for (int i = 0; i < 14; i++)
                        {
                            float a = TtHash(seed + i) * MathX.Tau, tau = fx.T - dur * Game.TtHitAt, d = r * (0.7f + 0.3f * TtHash(seed + i * 2.1f)) + 90f * (1f - Mathf.Exp(-4f * tau));
                            TtPetalAt(c + V2.FromAngle(a + tau * 2f) * d, a + fx.T * 9f, 1f, fade);
                        }
                    break;
                }
                default:
                {
                    // the Crane Bloom: a cone of petals and a bright front racing out to its reach
                    float half = Game.TtArc[4] * 0.5f * Mathf.Deg2Rad, wk = Mathf.Clamp01((fx.T - dur * Game.TtHitAt) / 0.3f);
                    if (wk <= 0f)
                        break;
                    float front = reach * Game.TtEase(wk), al = 1f - wk * wk;
                    TtCone(c, aim, half, front, Game.TtPetal.WithAlpha(0.26f * al));
                    Arc(c, front, 7f * al + 2f, Game.TtPaper.WithAlpha(0.95f * al), 24, aim - half, 2f * half);
                    Arc(c, front - 10f, 4f, Game.TtSun.WithAlpha(0.55f * al), 24, aim - half * 0.9f, 1.8f * half);
                    for (int i = 0; i < 26; i++)
                    {
                        float u = TtHash(seed + i * 1.7f), a = aim + (u - 0.5f) * 2f * half, d = front * (0.35f + 0.65f * TtHash(seed + i * 3.1f));
                        TtPetalAt(c + V2.FromAngle(a) * d, a + fx.T * 8f + i, 1.1f, al);
                    }
                    break;
                }
            }
        }

        /// A crescent of paper-white air over a fan's sweep: from `start` through `span`, thickest at the leading edge.
        void TtSweep(V2 c, float r, float start, float span, float al)
        {
            if (Mathf.Abs(span) < 0.05f || al <= 0f)
                return;
            int segs = Mathf.Max(6, (int)(Mathf.Abs(span) * 10f));
            Arc(c, r - 6f, 12f, Game.TtPetal.WithAlpha(0.14f * al), segs, start, span);
            const int K = 5;
            for (int i = 0; i < K; i++)  // brighter and thicker toward the leading edge
            {
                float u0 = i / (float)K, u1 = (i + 1f) / K;
                Arc(c, r, 1f + 3.5f * u1, Game.TtPaper.WithAlpha((0.25f + 0.7f * u1) * al), Mathf.Max(3, segs / K), start + span * u0, span * (u1 - u0));
            }
        }

        /// A filled cone from `c` along `aim`, `half` either side, out to `r`.
        void TtCone(V2 c, float aim, float half, float r, Rgba col)
        {
            if (r < 2f)
                return;
            SetColor(col);
            const int N = 20;
            V2 prev = c + V2.FromAngle(aim - half) * r;
            for (int i = 1; i <= N; i++)
            {
                V2 next = c + V2.FromAngle(aim - half + 2f * half * i / N) * r;
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// One petal: a small pink leaf turned to `rot`.
        void TtPetalAt(V2 at, float rot, float size, float al)
        {
            if (al <= 0.01f)
                return;
            V2 d = V2.FromAngle(rot) * (4.5f * size), n = d.Orthogonal() * 0.5f;
            SetColor(Game.TtPetal.WithAlpha(al));
            Tri(at - d, at + n, at + d);
            Tri(at - d, at + d, at - n);
        }

        /// FxKind.TessenPetals: the Petal Spin's petals burst out, then curve in to who they heal (Pos2.X; -1: they drift and fade).
        void DrawTessenPetals(Effect fx)
        {
            int target = Mathf.RoundToInt(fx.Pos2.X), n = Mathf.Max(1, Mathf.RoundToInt(fx.Pos2.Y));
            Player from, to;
            V2 o = game.Players.TryGetValue(Mathf.RoundToInt(fx.Radius), out from) ? from.Disp : fx.Pos;
            bool homing = target >= 0 && game.Players.TryGetValue(target, out to);
            to = homing ? game.Players[target] : null;
            float u = Mathf.Clamp01(fx.T / Game.TtPetalFlight), seed = fx.Pos.X + fx.Pos.Y * 0.71f;
            for (int i = 0; i < n; i++)
            {
                float a = i * MathX.Tau / n + TtHash(seed) * MathX.Tau;
                V2 out1 = o + V2.FromAngle(a) * (28f + 46f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.4f)));
                V2 at;
                float al;
                if (homing)
                {
                    float h = Game.TtEase(Mathf.Clamp01((u - 0.25f) / 0.75f));
                    at = V2.Lerp(out1, to.Disp, h);
                    al = fx.T > Game.TtPetalFlight ? 1f - (fx.T - Game.TtPetalFlight) / 0.15f : 1f;
                }
                else
                {
                    at = o + V2.FromAngle(a + u) * (28f + 70f * u);
                    al = 1f - u;
                }
                TtPetalAt(at, a + fx.T * 11f, 1f, al);
            }
            if (homing && fx.T > Game.TtPetalFlight)  // the heal blooming as they land
            {
                float q = (fx.T - Game.TtPetalFlight) / 0.15f;
                Arc(to.Disp, to.Def.BodyR + 4f + 10f * q, 2f, new Rgba(0.36f, 0.95f, 0.54f, 0.8f * (1f - q)), 20);
            }
        }

        /// FxKind.TessenGust: the Kamikaze Gust (Pos2 = aim, half-angle; Radius its reach; gold: a Typhoon).
        void DrawTessenGust(Effect fx)
        {
            float aim = fx.Pos2.X, half = fx.Pos2.Y, reach = fx.Radius, k = Mathf.Clamp01(fx.T / fx.Dur), front = reach * Game.TtEase(k * 1.25f), al = 1f - k;
            V2 c = fx.Pos;
            Rgba col = fx.Color;
            float seed = fx.Pos.X * 0.53f + fx.Pos.Y * 0.91f;
            TtCone(c, aim, half, front, col.WithAlpha(0.13f * al));
            Arc(c, front, 9f * al + 2f, col.WithAlpha(0.85f * al), 36, aim - half, 2f * half);
            Arc(c, front - 12f, 4f, Rgba.White.WithAlpha(0.5f * al), 30, aim - half * 0.85f, 1.7f * half);
            Line(c + V2.FromAngle(aim - half) * 20f, c + V2.FromAngle(aim - half) * front, 1.5f, col.WithAlpha(0.4f * al));
            Line(c + V2.FromAngle(aim + half) * 20f, c + V2.FromAngle(aim + half) * front, 1.5f, col.WithAlpha(0.4f * al));
            int streaks = fxReduced ? 18 : 60;
            for (int i = 0; i < streaks; i++)
            {
                float a = aim + (TtHash(seed + i) - 0.5f) * 2f * half, sp = 0.55f + 0.45f * TtHash(seed + i * 2.7f);
                float d = Mathf.Min(reach, reach * Game.TtEase(k * 1.4f * sp) + 20f), len = 16f + 30f * TtHash(seed + i * 4.1f);
                V2 dir = V2.FromAngle(a);
                Line(c + dir * Mathf.Max(8f, d - len), c + dir * d, i % 3 == 0 ? 2f : 1.3f, (i % 2 == 0 ? col : Rgba.White).WithAlpha(0.75f * al));
            }
            for (int i = 0; i < 18; i++)
            {
                float a = aim + (TtHash(seed + i * 5.3f) - 0.5f) * 2f * half, d = front * (0.3f + 0.7f * TtHash(seed + i * 6.7f));
                TtPetalAt(c + V2.FromAngle(a) * d, a + fx.T * 12f + i, 1.1f, al);
            }
        }

        /// Under a player with the Twin Tessen: the gust's cone filling while it charges, wind streaming in, Tailwind streaks
        /// (anyone), and the Breeze arc over the head.
        void DrawTessenUnder(Player p, float t)
        {
            if (p.TtTailT > 0f && !p.Downed)  // Tailwind: streaks blowing back off you
            {
                float al = Mathf.Clamp01(p.TtTailT / 0.4f);
                for (int i = -1; i <= 1; i++)
                {
                    float a = p.Aim + MathX.Pi + i * 0.35f, w = Mathf.Sin(t * 20f + i) * 5f;
                    V2 d = V2.FromAngle(a);
                    Line(p.Disp + d * (p.Def.BodyR + 2f), p.Disp + d * (p.Def.BodyR + 14f + w), 1.6f, Game.TtWind.WithAlpha(0.55f * al));
                }
            }
            if (p.Weapon.Id != WeaponId.TwinTessen || p.Downed)
                return;
            bool typhoon = p.TtBreeze >= Game.TtBreezeMax - 0.5f;
            Rgba col = typhoon ? Game.TtGold : Game.TtWind;
            if (p.TtCharging)
            {
                float c = Mathf.Clamp01(p.TtCharge / Game.TtChargeFull), half = game.TtGustHalfOf(p, typhoon), reach = Game.TtGustReachOf(p);
                TtCone(p.Disp, p.Aim, half, reach, col.WithAlpha(0.04f + 0.06f * c));
                for (int s = -1; s <= 1; s += 2)
                {
                    V2 d = V2.FromAngle(p.Aim + s * half);
                    for (float u = 0.08f; u < 1f; u += 0.08f)  // dashed edges
                        Line(p.Disp + d * (reach * u), p.Disp + d * (reach * (u + 0.04f)), 1.4f, col.WithAlpha(0.45f));
                }
                Arc(p.Disp, reach * (0.15f + 0.85f * c), 2f, col.WithAlpha(0.35f), 30, p.Aim - half, 2f * half);  // how far it will throw
                if (!fxReduced)
                    for (int i = 0; i < 16; i++)  // wind streaming into the fans
                    {
                        float life = 0.38f, ph = (t / life + TtHash(i + 0.5f)) % 1f, cyc = Mathf.Floor(t / life + TtHash(i + 0.5f));
                        float a = p.Aim + (TtHash(i * 7.1f + cyc) - 0.5f) * 2.4f, d = (150f + 90f * TtHash(i * 3.3f + cyc)) * (1f - ph);
                        V2 dir = V2.FromAngle(a);
                        Line(p.Disp + dir * (d + 16f), p.Disp + dir * Mathf.Max(p.Def.BodyR, d), 1.4f, col.WithAlpha(0.8f * Mathf.Sin(Mathf.PI * ph)));
                    }
            }
            // Breeze over the head: a short arc that fills, gold and pulsing when full
            float f = p.TtBreeze / Game.TtBreezeMax, r = p.Def.BodyR + 11f, a0 = -MathX.Pi / 2f - 0.75f;
            V2 head = p.Disp;
            Arc(head, r, 3f, new Rgba(0f, 0f, 0f, 0.35f), 16, a0, 1.5f);
            if (f > 0f)
                Arc(head, r, 3f, (typhoon ? Game.TtGold.WithAlpha(0.75f + 0.25f * Mathf.Sin(t * 8f)) : Game.TtWind.WithAlpha(0.85f)), 16, a0, 1.5f * f);
        }

        /// The glow pass: the gust's front and a full Breeze bar.
        void DrawTessenGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.TessenGust || fx.T >= fx.Dur)
                    continue;
                float k = Mathf.Clamp01(fx.T / fx.Dur), front = fx.Radius * Game.TtEase(k * 1.25f);
                for (int i = 0; i < 5; i++)
                    GlowDisc(fx.Pos + V2.FromAngle(fx.Pos2.X + (i / 4f - 0.5f) * 1.6f * fx.Pos2.Y) * front, 30f, fx.Color, 0.22f * (1f - k));
            }
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.TwinTessen && !p.Downed && p.TtBreeze >= Game.TtBreezeMax - 0.5f)
                    GlowDisc(p.Disp + V2.FromAngle(-MathX.Pi / 2f) * (p.Def.BodyR + 11f), 14f, Game.TtGold, 0.25f);
        }
    }
}
