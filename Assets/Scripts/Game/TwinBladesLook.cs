using UnityEngine;

namespace Arena
{
    /// The Twin Blades' Crescent Waltz, held and swung (Docs/Mockups/twin-blades-rework.html; the motion is Core/TwinBladesMotion.cs,
    /// which the host also times the hits from). The Knight's model leaves its arms and sword out with the Twin Blades; this draws
    /// both arms, a pauldron on each shoulder, a curved moon-white sabre in each glove and their shadows:
    ///   - held at the ready, breathing (DrawTwinIdle, from the player loop);
    ///   - each chain step (FxKind.TwinStep, its Radius = the swordsman's id * 10 + the step), played from its age with a pale
    ///     trail off each cutting blade (DrawTwinStepFx); the Shear's crescent is an ordinary sword wave;
    ///   - Twin Fang: the wind-up and release (FxKind.TwinFang, DrawTwinFangFx), then empty hands while both sabres spin as
    ///     one pinwheel where the host says they are (Player.FangOut, FangAt; smoothed here, DrawFangPair), then the catch.
    /// Five diamonds over the swordsman's head show the chain (DrawTwinPips), and a ring under your own feet fills as Twin Fang
    /// comes back.
    public partial class ArenaApp
    {
        static readonly Rgba SabreHi = new Rgba(0.93f, 0.95f, 0.98f), SabreLo = new Rgba(0.62f, 0.68f, 0.78f), SabreEdge = new Rgba(0.92f, 0.96f, 1f),
            SabreGold = new Rgba(0.85f, 0.66f, 0.26f), SabreGrip = new Rgba(0.16f, 0.14f, 0.13f), TwinTrail = new Rgba(0.67f, 0.84f, 1f);
        const int TwinTrailN = 9;
        const float TwinTrailDt = 0.012f;

        /// The step effect of this player that is playing now (the newest), or null; `fang` too for Twin Fang.
        Effect TwinFxOf(int id, out Effect fang)
        {
            Effect best = null;
            fang = null;
            foreach (var fx in game.Effects)
            {
                if (fx.T >= fx.Dur)
                    continue;
                if (fx.Kind == FxKind.TwinStep && Mathf.RoundToInt(fx.Radius) / 10 == id && (best == null || fx.T < best.T))
                    best = fx;
                else if (fx.Kind == FxKind.TwinFang && Mathf.RoundToInt(fx.Radius) == id && (fang == null || fx.T < fang.T))
                    fang = fx;
            }
            if (best != null && fang != null)
            {
                if (fang.T < best.T) best = null; else fang = null;  // whichever began last plays
            }
            return best;
        }

        /// The sabres held at the ready, when neither a step nor a throw is playing; empty hands while Twin Fang is out, the catch.
        void DrawTwinIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.TwinBlades)
                return;
            chickenOn = IsChicken(p);  // ChickenLook.cs
            float caught = WatchFang(p);
            Effect fang;
            var step = TwinFxOf(p.Id, out fang);
            bool windUp = fang != null && fang.T < TwinBladesMotion.FangRelease;
            if (p.FangOut >= 2)
                DrawFangPair(p);
            if (windUp || step != null && p.FangOut == 0)
                return;  // the effect draws the move
            Rgba arm = p.Def.Color;
            float b = Mathf.Sin(t * 2.6f);
            if (p.FangOut != 0 || fang != null)
            {
                var e = TwinBladesMotion.At(TwinBladesMotion.Fang, TwinBladesMotion.FangOpen);  // empty hands, reaching a little after the pair
                e.Rr += 0.6f * b; e.Lr += 0.6f * b;
                DrawTwinPose(p.Disp, p.Aim, e, arm, false, false, 1f);
                DrawTwinPips(p, t);
                return;
            }
            if (caught < TwinBladesMotion.FangTotal - TwinBladesMotion.FangCatch + TwinBladesMotion.Recover)
            {
                DrawTwinPose(p.Disp, p.Aim, TwinBladesMotion.At(TwinBladesMotion.Fang, TwinBladesMotion.FangCatch + caught), arm, true, true, 1f);
                DrawTwinPips(p, t);
                return;
            }
            var m = TwinBladesMotion.Ready;
            chickR = ChickIdle(t, true);
            chickL = ChickIdle(t, false);
            m.Rr += 0.5f * b; m.Lr += 0.5f * b; m.Rb += 0.02f * b; m.Lb -= 0.02f * b;
            DrawTwinPose(p.Disp, p.Aim, m, arm, true, true, 1f);
            DrawTwinPips(p, t);
        }

        class FangView { public byte Was; public V2 Shown; public float Caught = 99f, Spin; public readonly V2[] Trail = new V2[10]; public int TrailN; }
        readonly System.Collections.Generic.Dictionary<int, FangView> fangViews = new System.Collections.Generic.Dictionary<int, FangView>();

        /// Follows a player's thrown pair from the snapshots (smoothed, its spin and trail kept here); returns seconds since the catch.
        float WatchFang(Player p)
        {
            FangView v;
            if (!fangViews.TryGetValue(p.Id, out v))
                fangViews[p.Id] = v = new FangView { Shown = p.FangAt };
            float dt = Time.deltaTime;
            if (p.FangOut >= 2)
            {
                if (v.Was < 2)
                {
                    v.Shown = p.Disp;  // out of the hand
                    v.TrailN = 0;
                }
                v.Shown = dt > 0f ? v.Shown + (p.FangAt - v.Shown) * (1f - Mathf.Exp(-dt * 20f)) : p.FangAt;
                v.Spin -= dt * Game.FangSpinRate;
                for (int i = Mathf.Min(v.TrailN, v.Trail.Length - 1); i > 0; i--)
                    v.Trail[i] = v.Trail[i - 1];
                v.Trail[0] = v.Shown;
                v.TrailN = Mathf.Min(v.TrailN + 1, v.Trail.Length);
            }
            else if (v.Was != 0)
                v.Caught = 0f;  // just caught
            else
                v.Caught += dt;
            v.Was = p.FangOut;
            return v.Caught;
        }

        /// The thrown pair: two sabres pommel to pommel, spun as one pinwheel, a blur of their sweep, a streak behind, shadows.
        void DrawFangPair(Player p)
        {
            FangView v;
            if (!fangViews.TryGetValue(p.Id, out v))
                return;
            V2 c = v.Shown;
            float spin = v.Spin != 0f ? v.Spin : -Time.time * Game.FangSpinRate, reach = 8f + TwinBladesMotion.BladeLen;
            for (int i = 1; i < v.TrailN; i++)  // the streak behind it
            {
                float k = 1f - (float)i / v.TrailN;
                Line(v.Trail[i - 1], v.Trail[i], 16f * k, TwinTrail.WithAlpha(0.22f * k));
            }
            for (int h = 0; h < 2; h++)
            {
                float a = spin + h * MathX.Pi;
                DrawSabre(c + V2.FromAngle(a) * 8f + new V2(3f, 5f), a, 1f, true);  // shadows on the floor
                const int N = 10;
                for (int i = 0; i < N; i++)  // the blur of its sweep: a fading wedge behind the blade (it spins backwards)
                {
                    float a0 = a + 1.5f * i / N, a1 = a + 1.5f * (i + 1) / N, k0 = 1f - (float)i / N, k1 = 1f - (i + 1f) / N;
                    V2 p0 = c + V2.FromAngle(a0) * reach, p1 = c + V2.FromAngle(a1) * reach;
                    VTri(c, TwinTrail.WithAlpha(0f), p0, TwinTrail.WithAlpha(0.55f * k0), p1, TwinTrail.WithAlpha(0.55f * k1));
                    Line(p0, p1, 2.6f * k0 + 0.4f, new Rgba(0.93f, 0.97f, 1f, 0.9f * k0));
                }
            }
            for (int h = 0; h < 2; h++)
            {
                float a = spin + h * MathX.Pi;
                DrawSabre(c + V2.FromAngle(a + 0.3f) * 8f, a + 0.3f, 0.3f, false);  // a ghost just behind
                DrawSabre(c + V2.FromAngle(a) * 8f, a, 1f, false);
            }
            Circle(c, 4.5f, SabreGold, 10);
            Circle(c, 2.2f, new Rgba(1f, 0.95f, 0.8f), 8);
        }

        /// One chain step, from its effect's age.
        void DrawTwinStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, step = code % 10;
            if (step < 1 || step > 5)
                return;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            if (owner != null)
            {
                Effect fang;
                if (TwinFxOf(id, out fang) != fx)
                    return;  // a newer step (or a throw) by the same player has taken over
            }
            var s = TwinBladesMotion.Steps[step - 1];
            chickenOn = IsChicken(owner);
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float aim = TwinAim(fx, owner, s.Dur), c = fx.T;
            Rgba arm = owner != null ? owner.Def.Color : new Rgba(0.29f, 0.66f, 0.61f);
            if (chickenOn)
            {
                DrawChickenTrail(s, o, aim, c, true);
                DrawChickenTrail(s, o, aim, c, false);
                chickR = ChickAt(s, c, true);
                chickL = ChickAt(s, c, false);
                DrawTwinPose(o, aim, TwinBladesMotion.At(s, c), arm, true, true, 1f);
                if (owner != null)
                    DrawTwinPips(owner, Time.time);
                return;
            }
            // the pale trail off each blade while it cuts
            for (int h = 0; h < 2; h++)
            {
                float h0 = h == 0 ? s.HotR0 : s.HotL0, h1 = h == 0 ? s.HotR1 : s.HotL1;
                V2 pTip = default(V2), pBase = default(V2);
                bool have = false;
                for (int i = 0; i <= TwinTrailN; i++)
                {
                    float ci = c - i * TwinTrailDt, k = ci / s.Dur;
                    bool hot = ci >= 0f && k >= h0 - 0.04f && k <= h1 + 0.02f;
                    if (!hot) { have = false; continue; }
                    var mi = TwinBladesMotion.At(s, ci);
                    V2 hand, dir;
                    TwinHand(o, aim, mi, h == 0, out hand, out dir);
                    V2 tip = hand + dir * (5f + TwinBladesMotion.BladeLen), bse = hand + dir * (5f + TwinBladesMotion.BladeLen * 0.3f);
                    if (have)
                    {
                        float a0 = 1f - (i - 1f) / TwinTrailN, a1 = 1f - (float)i / TwinTrailN;
                        a0 *= a0; a1 *= a1;
                        VTri(pTip, TwinTrail.WithAlpha(0.32f * a0), tip, TwinTrail.WithAlpha(0.32f * a1), bse, TwinTrail.WithAlpha(0f));
                        VTri(pTip, TwinTrail.WithAlpha(0.32f * a0), bse, TwinTrail.WithAlpha(0f), pBase, TwinTrail.WithAlpha(0f));
                        Line(pTip, tip, 2.2f * a0 + 0.4f, new Rgba(0.93f, 0.97f, 1f, 0.85f * a0));
                    }
                    pTip = tip; pBase = bse; have = true;
                }
            }
            DrawTwinPose(o, aim, TwinBladesMotion.At(s, c), arm, true, true, 1f);
            if (owner != null)
                DrawTwinPips(owner, Time.time);
        }

        /// The step's aim, turning to where its owner aims now as it settles back to the guard.
        static float TwinAim(Effect fx, Player owner, float dur)
        {
            float aim = (fx.Pos2 - fx.Pos).Angle;
            if (owner != null && fx.T > dur)
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((fx.T - dur) / TwinBladesMotion.Recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            return aim;
        }

        /// Twin Fang's wind-up and release (the pair in flight is drawn from the player loop, DrawTwinIdle).
        void DrawTwinFangFx(Effect fx)
        {
            int id = Mathf.RoundToInt(fx.Radius);
            Player owner;
            game.Players.TryGetValue(id, out owner);
            if (owner != null)
            {
                Effect fang;
                TwinFxOf(id, out fang);
                if (fang != fx)
                    return;
            }
            if (fx.T >= TwinBladesMotion.FangRelease)
                return;
            chickenOn = IsChicken(owner);
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float aim = (fx.Pos2 - fx.Pos).Angle;
            DrawTwinPose(o, aim, TwinBladesMotion.At(TwinBladesMotion.Fang, fx.T), owner != null ? owner.Def.Color : new Rgba(0.29f, 0.66f, 0.61f), true, true, 1f);
        }

        // --- pieces ---------------------------------------------------------------------------------------------

        static void TwinHand(V2 o, float aim, TwinBladesMotion.Pose m, bool right, out V2 hand, out V2 dir)
        {
            float a = right ? m.Ra : m.La, r = right ? m.Rr : m.Lr, b = right ? m.Rb : m.Lb;
            hand = o + V2.FromAngle(aim + m.Sp + a) * r;
            dir = V2.FromAngle(aim + m.Sp + b);
        }

        /// Both arms, pauldrons, sabres (those still in hand) and gloves, with the sabres' shadows on the floor.
        void DrawTwinPose(V2 o, float aim, TwinBladesMotion.Pose m, Rgba armCol, bool bladeR, bool bladeL, float alpha)
        {
            V2 hR, dR, hL, dL;
            TwinHand(o, aim, m, true, out hR, out dR);
            TwinHand(o, aim, m, false, out hL, out dL);
            V2 sh = new V2(3f, 5f);
            chickCur = chickR;
            if (bladeR) DrawSabre(hR + sh, dR.Angle, alpha, true);
            chickCur = chickL;
            if (bladeL) DrawSabre(hL + sh, dL.Angle, alpha, true);
            float body = aim + m.Sp + m.Tw;
            for (int k = 0; k < 2; k++)
            {
                V2 s = o + V2.FromAngle(body + (k == 0 ? 1.48f : -1.48f)) * 11.5f, hand = k == 0 ? hR : hL;
                V2 d = hand - s, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                if (n.X * (s.X - o.X) + n.Y * (s.Y - o.Y) < 0f)
                    n = n * -1f;
                V2 elbow = (s + hand) * 0.5f + n * Mathf.Max(0f, 7f - d.Length * 0.2f);
                Line(s, elbow, 8f, new Rgba(0.05f, 0.18f, 0.17f, alpha));
                Line(elbow, hand, 8f, new Rgba(0.05f, 0.18f, 0.17f, alpha));
                Line(s, elbow, 5.4f, armCol.WithAlpha(alpha));
                Line(elbow, hand, 5.4f, armCol.WithAlpha(alpha));
                Circle(s, 6.8f, new Rgba(0.05f, 0.18f, 0.17f, alpha), 12);
                Circle(s, 5.6f, new Rgba(0.7f, 0.76f, 0.84f, alpha), 12);
                Circle(s - new V2(0.8f, 1.2f), 2.3f, new Rgba(0.86f, 0.9f, 0.95f, alpha), 8);
            }
            chickCur = chickR;
            if (bladeR) DrawSabre(hR, dR.Angle, alpha, false);
            chickCur = chickL;
            if (bladeL) DrawSabre(hL, dL.Angle, alpha, false);
            chickCur = default(ChickState);
            foreach (var h in new[] { hR, hL })
            {
                Circle(h, 5.6f, new Rgba(0.16f, 0.11f, 0.06f, alpha), 12);
                Circle(h, 4.4f, new Rgba(0.72f, 0.56f, 0.34f, alpha), 12);
                Circle(h - new V2(1.2f, 1.5f), 1.4f, new Rgba(1f, 0.93f, 0.78f, 0.45f * alpha), 6);
            }
        }

        /// A curved sabre with its origin at the glove, pointing along `ang`: grip, gold knuckle bow, a blade that curls
        /// toward its edge side, a bright edge line. `shadow`: its shadow only.
        void DrawSabre(V2 hand, float ang, float alpha, bool shadow)
        {
            if (chickenOn)
            {
                var cs = chickCur;
                if (cs.Stretch == 0f) cs.Stretch = 1f;  // (the thrown pair: no pose state)
                DrawChicken(hand, ang, alpha, shadow, cs);
                return;
            }
            V2.Axes(ang, out V2 dir, out V2 side);
            float L = TwinBladesMotion.BladeLen;
            const int N = 7;
            Rgba shc = new Rgba(0f, 0f, 0f, 0.26f * alpha);
            V2 prevA = default(V2), prevB = default(V2), prevE = default(V2);
            for (int i = 0; i <= N; i++)
            {
                float u = (float)i / N, x = 5f + L * u, curl = 4.5f * u * u, w = 2.6f * (1f - 0.75f * u * u);
                V2 c = hand + dir * x + side * curl;
                V2 a = c - side * w, b = c + side * w * 0.8f, e = c + side * (w * 0.8f - 0.6f);
                if (i > 0)
                {
                    Rgba col = shadow ? shc : SabreHi.Mix(SabreLo, u * 0.6f).WithAlpha(alpha);
                    SetColor(col);
                    Quad(prevA, a, b, prevB);
                    if (!shadow)
                        Line(prevE, e, 0.9f, SabreEdge.WithAlpha(0.9f * alpha));
                }
                prevA = a; prevB = b; prevE = e;
            }
            if (shadow)
            {
                Line(hand - dir * 6f, hand + dir * 4f, 3.2f, shc);
                return;
            }
            Line(hand - dir * 6f, hand + dir * 3f, 3.2f, SabreGrip.WithAlpha(alpha));                 // the grip
            Line(hand + dir * 3.5f - side * 4.5f, hand + dir * 3.5f + side * 4.5f, 2.6f, SabreGold.WithAlpha(alpha));  // the guard
            V2 k0 = hand + dir * 3.5f - side * 4f, k1 = hand - dir * 2f - side * 8.5f, k2 = hand - dir * 7f - side * 3f;  // the knuckle bow
            Line(k0, k1, 1.6f, SabreGold.WithAlpha(alpha));
            Line(k1, k2, 1.6f, SabreGold.WithAlpha(alpha));
            Circle(hand - dir * 7f, 2f, SabreGold.WithAlpha(alpha), 8);                                // the pommel
        }

        /// Five diamonds over the swordsman's head: lit for each step thrown in this chain; the fifth glows when it is next.
        void DrawTwinPips(Player p, float t)
        {
            int step = TwinBladesMotion.StepOf(p.Combo);
            if (p.Id == game.MyId && game.TwinFangCd > 0f)  // Twin Fang coming back: a ring filling under your feet
            {
                float k = 1f - game.TwinFangCd / Game.FangCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, TwinTrail.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            if (step == 0)
                return;
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            for (int i = 0; i < 5; i++)
            {
                V2 at = c + new V2((i - 2f) * 9f, 0f);
                float r = i == 4 ? 3.8f : 3f;
                bool lit = i < step, next = i == 4 && step == 4;
                Rgba col = lit ? TwinTrail : next ? new Rgba(1f, 0.85f, 0.45f, 0.6f + 0.4f * Mathf.Sin(t * 14f)) : new Rgba(1f, 1f, 1f, 0.15f);
                Polygon(at, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                Polygon(at, r, 4, 0f, col);
            }
        }
    }
}
