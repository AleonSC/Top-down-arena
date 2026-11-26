using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Vowguard, held and swung, in the Radiant look (Docs/Mockups/vowguard-models.html; the poses are Core/VowguardMotion.cs,
    /// which the host also times the hits from). The Knight's model leaves its arms and weapon out with it; this draws both arms and
    /// pauldrons, an arming sword in the right glove and a heater shield on the left arm:
    ///   - the sword: a white blade edged in the stance's colour, a winged steel guard round a gem; the shield: a faceted steel frame
    ///     round a glowing core, throwing a lattice of light in front while it guards (DrawVgSword, DrawVgShield; their light in the
    ///     glow pass, DrawVgGlow);
    ///   - the motion (DrawVgIdle, VgView): the drawn pose follows the move's on springs (overshoot and settle), strikes pull back
    ///     first (VowguardMotion.ShownPoseAt), each hit holds a moment (ShownT, with a shake), the shoulders turn with the swing, a
    ///     fast blade leaves three afterimages, a stance switch twirls the sword while the shield swings across, and at rest it sways;
    ///   - each step's light (DrawVgStepFx): gold cuts, lanes, bursts and rings in Valor, jade sweeps and pulses in Sanctuary,
    ///     green heal rings, the Cleave's crescent, a blue ward ring;
    ///   - the Aegis Dome for its life (FxKind.VgDome, DrawVgDomeFx);
    ///   - over the head two rows of diamonds, the stance you're in on top (one per step of your chain, finishers edged white),
    ///     the other dim under it; Faith as six dots under your feet; Sanctuary's guard as a faint jade arc in front.
    public partial class ArenaApp
    {
        static readonly Rgba VgEdgeDark = new Rgba(0.16f, 0.18f, 0.23f),
            VgGrip = new Rgba(0.36f, 0.27f, 0.2f), VgHealGreen = new Rgba(0.56f, 0.88f, 0.56f),
            VgWhite = new Rgba(0.98f, 0.99f, 1f), VgSilver = new Rgba(0.84f, 0.88f, 0.93f), VgFrame = new Rgba(0.75f, 0.78f, 0.85f);
        const float VgLean = 0.35f;  // a move's lean is drawn this much (the body itself doesn't move but for a dash)
        const float VgBlade = 45f;   // grip to point
        const int VgHist = 12;       // drawn poses kept for the afterimages

        // the springs each drawn channel follows its pose on: stiffness, damping ratio (sword, reach, shield, shield out, lean x/y, shield size)
        static readonly float[] VgSpringK = { 1100f, 900f, 650f, 750f, 700f, 700f, 800f }, VgSpringZ = { 0.42f, 0.5f, 0.55f, 0.45f, 0.7f, 0.7f, 0.6f };

        class VgView
        {
            public int Stance = -1;
            public float Blend = 1f;  // 0-1 from the old stance's rest pose to this one's
            public VgPose From;
            public float[] X = new float[7], Vel = new float[7];  // the drawn pose and its speed
            public bool Live;
            public float Twirl = -1f;  // 0-1 through a stance switch's twirl
            public Effect Fx;          // the step drawn last frame (its Wrap comes off when it ends)
            public float Clock;
            public readonly float[] HistT = new float[VgHist], HistSword = new float[VgHist], HistReach = new float[VgHist], HistAim = new float[VgHist];
            public readonly V2[] HistLean = new V2[VgHist];
            public int HistN;
        }

        readonly Dictionary<int, VgView> vgViews = new Dictionary<int, VgView>();

        /// The step effect of this player that is playing now (the newest), or null.
        Effect VgFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in FxOf(FxKind.VgStep))
            {
                if (fx.Kind != FxKind.VgStep || fx.T >= fx.Dur || Mathf.RoundToInt(fx.Radius) / 100 != id)
                    continue;
                if (best == null || fx.T < best.T)
                    best = fx;
            }
            return best;
        }

        static Rgba VgCol(int stance) => stance == 0 ? Game.VgGold : Game.VgJade;

        static float[] VgArr(VgPose p) => new[] { p.Sword, p.Reach, p.Shield, p.ShieldD, p.Dx, p.Dy, p.ShieldS };
        static VgPose VgOf(float[] a) => new VgPose { Sword = a[0], Reach = a[1], Shield = a[2], ShieldD = a[3], Dx = a[4], Dy = a[5], ShieldS = a[6] };

        /// The Vowguard at the ready or mid-step, with its pips (from the player loop).
        void DrawVgIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Vowguard || p.Downed)
                return;
            VgView v;
            if (!vgViews.TryGetValue(p.Id, out v))
                vgViews[p.Id] = v = new VgView { Stance = p.VgStance, From = VowguardMotion.Rest(p.VgStance) };
            if (v.Stance != p.VgStance)
            {
                v.From = VgRestBlend(v);
                v.Stance = p.VgStance;
                v.Blend = 0f;
                if (v.Live)
                    v.Twirl = 0f;
            }
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);
            v.Clock += dt;
            v.Blend = Mathf.Min(1f, v.Blend + dt / Game.VgSwitchCd);
            var fx = VgFxOf(p.Id);
            var m = fx != null ? VowguardMotion.Of(Mathf.RoundToInt(fx.Radius) % 100) : null;
            float dur = fx != null ? fx.Dur - Game.VgFxTail : 1f;
            bool playing = m != null && m.Stance == p.VgStance && fx.T < dur, stopped = false;
            if (v.Fx != null && (v.Fx != fx || !playing))
            {
                var was = VowguardMotion.Of(Mathf.RoundToInt(v.Fx.Radius) % 100);
                if (was != null && was.Wrap != 0f)  // a full turn ends a turn round: take it off so the springs don't unwind it
                {
                    v.X[0] -= was.Wrap;
                    v.X[2] -= was.Wrap;
                }
            }
            v.Fx = playing ? fx : null;
            VgPose target;
            float aim = p.Aim;
            if (playing)
            {
                float shown = VowguardMotion.ShownT(m, fx.T / dur * m.Dur, out stopped) / m.Dur;
                target = VowguardMotion.ShownPoseAt(m, shown);
                aim = (fx.Pos2 - fx.Pos).Angle;
                if (m.Keep)
                    target.Dx = target.Dy = 0f;  // (the body itself dashes)
                v.Twirl = -1f;
            }
            else
            {
                target = VgRestBlend(v);
                if (v.Twirl >= 0f)
                {
                    v.Twirl += dt / 0.42f;
                    float e = v.Twirl < 0.5f ? 4f * v.Twirl * v.Twirl * v.Twirl : 1f - Mathf.Pow(-2f * Mathf.Min(1f, v.Twirl) + 2f, 3f) / 2f;
                    target.Sword -= 360f * e;
                    if (v.Twirl >= 1f)
                    {
                        v.Twirl = -1f;
                        target.Sword += 360f;
                        v.X[0] += 360f;
                    }
                }
                float b = t * MathX.Tau * 0.55f + p.Id * 1.3f;  // breathing
                target.Sword += Mathf.Sin(b) * 3f;
                target.ShieldD += Mathf.Sin(b + 1f) * 0.7f;
                target.Shield += Mathf.Sin(b + 0.5f) * 2f;
            }
            var tg = VgArr(target);
            if (!v.Live)
            {
                v.Live = true;
                for (int i = 0; i < 7; i++) { v.X[i] = tg[i]; v.Vel[i] = 0f; }
            }
            else if (!stopped)
            {
                float h = dt / 3f;
                for (int s = 0; s < 3; s++)
                    for (int i = 0; i < 7; i++)
                    {
                        float c = 2f * VgSpringZ[i] * Mathf.Sqrt(VgSpringK[i]);
                        v.Vel[i] += (VgSpringK[i] * (tg[i] - v.X[i]) - c * v.Vel[i]) * h;
                        v.X[i] += v.Vel[i] * h;
                    }
            }
            var pose = VgOf(v.X);
            var rest = VowguardMotion.Rest(p.VgStance);
            float twist = (Mathf.Clamp((pose.Sword - rest.Sword) * 0.14f, -26f, 26f) + Mathf.Clamp(pose.ShieldD - rest.ShieldD, -10f, 20f) * 0.6f) * MathX.Pi / 180f;
            V2 o = p.Disp;
            if (stopped)
                o += new V2(Mathf.Sin(t * 97f), Mathf.Cos(t * 83f)) * 0.7f;  // the hit's shake
            int k = v.HistN % VgHist;
            v.HistT[k] = v.Clock; v.HistSword[k] = pose.Sword; v.HistReach[k] = pose.Reach; v.HistAim[k] = aim;
            v.HistLean[k] = LnRot(new V2(pose.Dx * VgLean, pose.Dy * VgLean), aim);
            v.HistN++;
            float guard = p.VgStance == 1 ? (pose.ShieldS > 1.15f ? 1f : 0.45f) : (Game.VgGuardHalf(p) > 0f ? 0.6f : 0f);
            DrawVgGhosts(v, o, VgCol(p.VgStance));
            DrawVgPose(o, aim, pose, VgCol(p.VgStance), 1f, twist, guard);
            DrawVgGuard(p, t);
            DrawVgPips(p, t);
        }

        static VgPose VgRestBlend(VgView v)
        {
            float u = v.Blend * v.Blend * (3f - 2f * v.Blend);
            return VgPose.Lerp(v.From, VowguardMotion.Rest(v.Stance), u);
        }

        /// The drawn pose `back` seconds ago (the nearest kept), or false if none is that old.
        static bool VgPast(VgView v, float back, out int at)
        {
            at = -1;
            int n = Mathf.Min(v.HistN, VgHist);
            float best = 1e9f;
            for (int j = 0; j < n; j++)
            {
                int i = (v.HistN - 1 - j) % VgHist;
                float d = Mathf.Abs(v.Clock - v.HistT[i] - back);
                if (d < best) { best = d; at = i; }
            }
            return at >= 0 && best < 0.02f;
        }

        /// A fast blade's three afterimages in the stance's colour.
        void DrawVgGhosts(VgView v, V2 o, Rgba col)
        {
            int now = (v.HistN - 1) % VgHist, then;
            if (!VgPast(v, 0.05f, out then))
                return;
            float speed = Mathf.Abs(v.HistSword[now] - v.HistSword[then]) + Mathf.Abs(v.HistReach[now] - v.HistReach[then]) * 60f;
            float k = Mathf.Clamp01((speed - 10f) / 40f);
            if (k <= 0f)
                return;
            for (int g = 0; g < 3; g++)
            {
                int i;
                if (!VgPast(v, 0.033f * (g + 1), out i))
                    continue;
                V2 d = V2.FromAngle(v.HistAim[i] + v.HistSword[i] * MathX.Pi / 180f);
                V2 grip = o + v.HistLean[i] + d * (7f + 7f * v.HistReach[i]);
                VgFill(VgBladePts(grip, d, 3.2f, 2.4f), col.WithAlpha((0.45f - 0.15f * g) * k));
            }
        }

        /// Both arms, pauldrons, the shield and the sword for a pose round `o` facing `aim`; the shoulders turned by `twist`
        /// (radians), the shield's light lattice as strong as `guard` (0-1).
        void DrawVgPose(V2 o, float aim, VgPose m, Rgba stanceCol, float alpha, float twist = 0f, float guard = 0f)
        {
            o += LnRot(new V2(m.Dx * VgLean, m.Dy * VgLean), aim);
            float sw = aim + m.Sword * MathX.Pi / 180f, sh = aim + m.Shield * MathX.Pi / 180f;
            V2 dS = V2.FromAngle(sw), dL = V2.FromAngle(sh);
            V2 hR = o + dS * (7f + 7f * m.Reach), hL = o + dL * (m.ShieldD * 0.7f + 3f);
            V2 sR = o + LnRot(new V2(1f, 9.5f), aim + twist), sL = o + LnRot(new V2(1f, -9.5f), aim + twist);
            DrawVgSword(hR + new V2(3f, 4f), dS, true, stanceCol, alpha);  // its shadow
            LanceArm(o, sR, hR, alpha);
            DrawVgSword(hR, dS, false, stanceCol, alpha);
            LanceArm(o, sL, hL, alpha);
            DrawVgShield(hL, dL, m.ShieldS, stanceCol, alpha, guard);
            LancePauldron(sR, aim + twist, alpha);
            LancePauldron(sL, aim + twist, alpha);
            Circle(hR, 4.6f, LnDark.WithAlpha(alpha), 12);
            Circle(hR, 3.9f, LnGlove.WithAlpha(alpha), 12);
        }

        /// A convex shape filled as a fan.
        void VgFill(V2[] pts, Rgba col)
        {
            for (int i = 1; i + 1 < pts.Length; i++)
                VTri(pts[0], col, pts[i], col, pts[i + 1], col);
        }

        /// A closed outline.
        void VgOutline(V2[] pts, float w, Rgba col)
        {
            for (int i = 0; i < pts.Length; i++)
                Line(pts[i], pts[(i + 1) % pts.Length], w, col);
        }

        /// The blade from the guard to the point: `w0` wide at the guard, `w1` where it starts to taper.
        static V2[] VgBladePts(V2 grip, V2 dir, float w0, float w1)
        {
            V2 s = new V2(-dir.Y, dir.X);
            return new[] { grip + dir * 4f - s * w0, grip + dir * (VgBlade - 7f) - s * w1, grip + dir * VgBlade, grip + dir * (VgBlade - 7f) + s * w1, grip + dir * 4f + s * w0 };
        }

        /// The Radiant arming sword with its grip at `grip`, pointing along `dir`: a silver pommel, a leather grip, a winged guard
        /// round a gem, a white blade edged in the stance's colour.
        void DrawVgSword(V2 grip, V2 dir, bool shadow, Rgba col, float alpha)
        {
            V2 s = new V2(-dir.Y, dir.X);
            if (shadow)
            {
                Line(grip - dir * 6f, grip + dir * (VgBlade - 1f), 5f, new Rgba(0f, 0f, 0f, 0.22f * alpha));
                return;
            }
            Line(grip - dir * 7f, grip + dir * 2f, 3.8f, VgEdgeDark.WithAlpha(alpha));
            Line(grip - dir * 6.5f, grip + dir * 1.5f, 2.6f, VgGrip.WithAlpha(alpha));
            for (float u = -4.5f; u < 1f; u += 2.5f)
                Line(grip + dir * u - s * 1.3f, grip + dir * (u + 0.9f) + s * 1.3f, 0.6f, new Rgba(0f, 0f, 0f, 0.45f * alpha));
            V2 pom = grip - dir * 8.6f;
            Circle(pom, 3.3f, VgEdgeDark.WithAlpha(alpha), 10);
            Circle(pom, 2.5f, VgSilver.WithAlpha(alpha), 10);
            Circle(pom - dir * 0.6f - s * 0.7f, 0.9f, new Rgba(1f, 1f, 1f, 0.7f * alpha), 6);
            var blade = VgBladePts(grip, dir, 3.2f, 2.4f);
            VgOutline(blade, 4f, col.WithAlpha(0.3f * alpha));  // the edge's light
            VgOutline(blade, 1.6f, col.WithAlpha(alpha));
            VgFill(blade, VgWhite.WithAlpha(alpha));
            Line(grip + dir * 8f, grip + dir * (VgBlade - 9f), 1f, col.WithAlpha(0.8f * alpha));
            for (int side = -1; side <= 1; side += 2)  // the guard's wings
            {
                VTri(grip + dir * 1f + s * (1.5f * side), VgEdgeDark.WithAlpha(alpha), grip + dir * 6f + s * (3f * side), VgEdgeDark.WithAlpha(alpha), grip + s * (10.5f * side), VgEdgeDark.WithAlpha(alpha));
                VTri(grip + dir * 1.5f + s * (2f * side), VgSilver.WithAlpha(alpha), grip + dir * 5f + s * (3f * side), VgSilver.WithAlpha(alpha), grip + dir * 1f + s * (9f * side), VgSilver.WithAlpha(alpha));
            }
            Circle(grip + dir * 3f, 2.6f, VgEdgeDark.WithAlpha(alpha), 10);
            Circle(grip + dir * 3f, 1.9f, col.WithAlpha(alpha), 10);
        }

        /// The Radiant heater shield on the left glove, seen edge-on from above: a faceted steel frame facing `face` round a core
        /// in the stance's colour, a white boss; while it guards (`guard` 0-1) a lattice of light in front.
        void DrawVgShield(V2 hand, V2 face, float scale, Rgba col, float alpha, float guard = 0f)
        {
            const int n = 5;
            float ang = face.Angle, k = scale, span = 2f + (k - 1f) * 1.1f, r0 = 10f * k, r1 = 16.5f * k;
            V2 c = hand - face * (10f * k);
            Arc(c, (r0 + r1) * 0.5f, r1 - r0 + 3f, LnDark.WithAlpha(alpha), 14, ang - span * 0.5f - 0.04f, span + 0.08f);
            for (int i = 0; i < n; i++)
            {
                V2 a0 = V2.FromAngle(ang - span * 0.5f + span * i / n), a1 = V2.FromAngle(ang - span * 0.5f + span * (i + 1) / n);
                VgFill(new[] { c + a0 * r0, c + a0 * r1, c + a1 * r1, c + a1 * r0 }, VgFrame.WithAlpha(alpha));
                VgFill(new[] { c + a0 * (r0 + 2.4f), c + a0 * (r1 - 2.4f), c + a1 * (r1 - 2.4f), c + a1 * (r0 + 2.4f) }, col.WithAlpha(0.95f * alpha));
                if (i > 0)
                    Line(c + a0 * r0, c + a0 * r1, 1f, LnDark.WithAlpha(alpha));
                if (i >= 1 && i <= 3)
                    Line(c + a0 * r1, c + a1 * r1, 0.9f, new Rgba(1f, 1f, 1f, 0.6f * alpha));
            }
            V2 boss = c + face * ((r0 + r1) * 0.5f);
            Circle(boss, 3f * k, LnDark.WithAlpha(alpha), 10);
            Circle(boss, 2.2f * k, VgWhite.WithAlpha(alpha), 10);
            if (guard > 0f)
            {
                float R = r1 + 7f, sp = span + 0.5f;
                for (int j = 0; j < 3; j++)
                    Arc(c, R + j * 4f, 1.2f, col.WithAlpha(0.32f * guard * (1f - j * 0.25f) * alpha), 16, ang - sp * 0.5f, sp);
                for (int i = 0; i <= 6; i++)
                {
                    V2 d = V2.FromAngle(ang - sp * 0.5f + sp * i / 6f);
                    Line(c + d * R, c + d * (R + 8f), 1f, col.WithAlpha(0.25f * guard * alpha));
                }
            }
        }

        /// The Radiant light in the additive glow pass (Juice.cs DrawGlowPass): the sword's gem and blade, the shield's core.
        void DrawVgGlow()
        {
            foreach (var p in game.Players.Values)
            {
                VgView v;
                if (p.Weapon.Id != WeaponId.Vowguard || p.Downed || !vgViews.TryGetValue(p.Id, out v) || !v.Live)
                    continue;
                var m = VgOf(v.X);
                int last = (v.HistN - 1 + VgHist) % VgHist;
                float aim = v.HistN > 0 ? v.HistAim[last] : p.Aim;
                V2 o = p.Disp + LnRot(new V2(m.Dx * VgLean, m.Dy * VgLean), aim);
                V2 dS = V2.FromAngle(aim + m.Sword * MathX.Pi / 180f), dL = V2.FromAngle(aim + m.Shield * MathX.Pi / 180f);
                V2 hR = o + dS * (7f + 7f * m.Reach), hL = o + dL * (m.ShieldD * 0.7f + 3f);
                Rgba col = VgCol(p.VgStance);
                GlowDisc(hR + dS * 3f, 7f, col, 0.5f);
                for (int i = 1; i <= 3; i++)
                    GlowDisc(hR + dS * (VgBlade * i / 4f), 8f, col, 0.18f);
                V2 c = hL - dL * (10f * m.ShieldS);
                GlowDisc(c + dL * (13.25f * m.ShieldS), 12f * m.ShieldS, col, p.VgStance == 1 ? 0.45f : 0.3f);
            }
        }

        /// Sanctuary's guard (or a move's wider one) as a faint arc in front.
        void DrawVgGuard(Player p, float t)
        {
            float half = Game.VgGuardHalf(p);
            if (half <= 0f && p.VgStance == 1)
                half = Game.VgSanctHalf;
            if (half <= 0f)
                return;
            float r = Game.VgGuardRadius(p);
            Arc(p.Disp, r + 2f, 1.6f, Game.VgJade.WithAlpha(0.28f + 0.06f * Mathf.Sin(t * 3f)), 20, p.Aim - half, half * 2f);
        }

        /// Two rows of diamonds over the head (this stance's chain lit up to where it is, the other dim) and the Faith dots.
        void DrawVgPips(Player p, float t)
        {
            for (int row = 0; row < 2; row++)
            {
                int st = row == 0 ? p.VgStance : p.VgStance ^ 1;
                var chain = Game.VgChainOf(p, st);
                int at = Game.VgAt(p, st), n = chain.Length;
                float gap = row == 0 ? 9f : 6.5f, r = row == 0 ? 2.6f : 1.7f;
                V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 17f + row * 8f);
                Rgba col = VgCol(st);
                for (int i = 0; i < n; i++)
                {
                    V2 at2 = c + new V2((i - (n - 1) / 2f) * gap, 0f);
                    bool fin = VowguardMotion.Of(chain[i]).Fin, lit = i < at;
                    float a = row == 0 ? 1f : 0.45f;
                    Polygon(at2, (r + (fin ? 1.1f : 0.6f)) * 1.414f, 4, 0f, (fin && row == 0 ? new Rgba(1f, 1f, 1f, 0.8f) : new Rgba(0f, 0f, 0f, 0.6f)).WithAlpha(a));
                    Polygon(at2, r * 1.414f, 4, 0f, lit ? col.WithAlpha(a) : new Rgba(1f, 1f, 1f, 0.14f * a));
                }
            }
            V2 f0 = p.Disp + new V2(0f, p.Def.BodyR + 9f);
            for (int i = 0; i < Game.VgFaithMax; i++)
            {
                bool lit = i < p.VgFaith;
                V2 at = f0 + new V2((i - (Game.VgFaithMax - 1) / 2f) * 6.5f, 0f);
                Circle(at, 2.2f, lit ? new Rgba(1f, 0.94f, 0.72f, 0.85f + 0.15f * Mathf.Sin(t * 6f + i)) : new Rgba(1f, 0.94f, 0.72f, 0.16f), 8);
            }
        }

        // --- the light of each step --------------------------------------------------------------------------------

        /// A filled band between radii r0 and r1 from angle a0 to a1 (radians), round `o`.
        void VgBand(V2 o, float r0, float r1, float a0, float a1, Rgba col)
        {
            if (a1 < a0) { float x = a0; a0 = a1; a1 = x; }
            if (a1 - a0 < 0.001f || r1 <= r0)
                return;
            Arc(o, (r0 + r1) * 0.5f, r1 - r0, col, Mathf.Max(6, Mathf.CeilToInt((a1 - a0) * 12f)), a0, a1 - a0);
        }

        static float VgWin(float t, float a, float b) => t < a || t > b ? -1f : (t - a) / (b - a);
        static float VgEase(float x) => x < 0.5f ? 2f * x * x : 1f - (-2f * x + 2f) * (-2f * x + 2f) / 2f;

        /// A cut sweeping from a0 to a1 (degrees from the aim) between t0 and t1, fading for 0.2 after.
        void VgSweep(V2 o, float aim, float t, float t0, float t1, float a0, float a1, float r, Rgba col)
        {
            if (t < t0 || t > t1 + 0.2f)
                return;
            float q = VgEase(Mathf.Clamp01((t - t0) / (t1 - t0))), al = t <= t1 ? 1f : 1f - (t - t1) / 0.2f;
            float A0 = aim + a0 * MathX.Pi / 180f, cur = aim + (a0 + (a1 - a0) * q) * MathX.Pi / 180f;
            VgBand(o, 20f, r, A0, cur, col.WithAlpha(0.12f * al));
            VgBand(o, r - 3f, r, A0, cur, col.WithAlpha(0.85f * al));
            VgBand(o, r * 0.7f - 0.75f, r * 0.7f + 0.75f, A0, cur, new Rgba(1f, 1f, 1f, 0.3f * al));
        }

        void VgBurst(V2 at, float t, float t0, float t1, Rgba col, float R = 30f)
        {
            float p = VgWin(t, t0, t1);
            if (p < 0f)
                return;
            Arc(at, 8f + R * VgEase(p), 4f * (1f - p) + 0.5f, col.WithAlpha(1f - p), 24);
            for (int i = 0; i < 7; i++)
            {
                V2 d = V2.FromAngle(i / 7f * MathX.Tau + 0.3f);
                float r0 = 10f + R * 0.6f * p;
                Line(at + d * r0, at + d * (r0 + 8f), 1.8f, col.WithAlpha(1f - p));
            }
        }

        void VgLane(V2 o, V2 dir, float x0, float len, float w, float t, float t0, float t1, Rgba col)
        {
            float p = VgWin(t, t0, t1);
            if (p < 0f)
                return;
            float a = 1f - p, l = len * Mathf.Clamp01(p * 3f);
            Line(o + dir * x0, o + dir * (x0 + l), w, col.WithAlpha(0.16f * a));
            Line(o + dir * x0, o + dir * (x0 + l), 2f, new Rgba(1f, 1f, 1f, 0.6f * a));
        }

        void VgPulse(V2 o, float R, float t, float t0, float t1, Rgba col)
        {
            float p = VgWin(t, t0, t1);
            if (p < 0f)
                return;
            float r = R * VgEase(p);
            Circle(o, r, col.WithAlpha(0.07f * (1f - p)), 40);
            Arc(o, r, 6f * (1f - p) + 0.5f, col.WithAlpha(0.9f * (1f - p)), 40);
        }

        /// One step's light, from its effect's age.
        void DrawVgStepFx(Effect fx)
        {
            int id = Mathf.RoundToInt(fx.Radius);
            var m = VowguardMotion.Of(id % 100);
            if (m == null)
                return;
            Player owner;
            game.Players.TryGetValue(id / 100, out owner);
            float dur = Mathf.Max(0.05f, fx.Dur - Game.VgFxTail), t = fx.T / dur;
            float aim = (fx.Pos2 - fx.Pos).Angle;
            V2 dir = V2.FromAngle(aim), right = new V2(-dir.Y, dir.X);
            V2 o = fx.Pos;
            var pose = VowguardMotion.PoseAt(m, Mathf.Min(1f, t));
            V2 lean = m.Keep ? new V2(0f, 0f) : dir * (pose.Dx * VgLean) + right * (pose.Dy * VgLean);
            V2 k = (m.Keep && owner != null ? owner.Disp : o) + lean;
            Rgba gold = Game.VgGold, jade = Game.VgJade;
            V2 shieldAt = k + V2.FromAngle(aim + pose.Shield * MathX.Pi / 180f) * pose.ShieldD;
            switch (m.Id)
            {
                case VgMove.EdgeCut:
                    VgSweep(k, aim, t, 0.28f, 0.62f, 100f, -65f, 115f, gold);
                    break;
                case VgMove.RimBash:
                    VgBurst(k + dir * 58f, t, 0.45f, 0.75f, gold);
                    break;
                case VgMove.LungeThrust:
                    VgLane(k, dir, 14f, 165f, 26f, t, 0.45f, 0.85f, gold);
                    break;
                case VgMove.ShieldHook:
                {
                    float p = VgWin(t, 0.42f, 0.9f);
                    if (p < 0f)
                        break;
                    float tx = 140f - 60f * VgEase(p);
                    for (int i = 0; i < 3; i++)
                    {
                        V2 c = k + dir * (tx - 14f - i * 14f);
                        Line(c + dir * 6f - right * 8f, c, 2.2f, gold.WithAlpha(1f - p));
                        Line(c + dir * 6f + right * 8f, c, 2.2f, gold.WithAlpha(1f - p));
                    }
                    break;
                }
                case VgMove.TwinStrike:
                    VgSweep(k, aim, t, 0.2f, 0.42f, 100f, -60f, 105f, gold);
                    VgBurst(k + dir * 62f, t, 0.68f, 0.95f, gold);
                    break;
                case VgMove.MercySpin:
                    VgSweep(k, aim, t, 0.15f, 0.85f, 80f, -280f, 95f, gold);
                    break;
                case VgMove.JudgmentCleave:
                {
                    VgLane(k, dir, 14f, 130f, 30f, t, 0.52f, 0.85f, gold);
                    float p = VgWin(t, 0.55f, 1.25f);
                    if (p < 0f)
                        break;
                    V2 c = k + dir * (40f + p * 300f);
                    float a = 1f - p * 0.7f;
                    VgBand(c, 40f, 50f, aim - 1.05f, aim + 1.05f, gold.WithAlpha(0.55f * a));
                    VgBand(c, 47f, 50f, aim - 0.95f, aim + 0.95f, new Rgba(1f, 1f, 1f, 0.8f * a));
                    break;
                }
                case VgMove.ShieldCharge:
                {
                    float p = VgWin(t, 0.2f, 0.62f);
                    if (p >= 0f)
                        for (int i = -2; i <= 2; i++)
                            Line(k - dir * 60f + right * (i * 6f), k - dir * 18f + right * (i * 6f), 1.6f, gold.WithAlpha(0.7f * (1f - p)));
                    VgBurst(k + dir * 30f, t, 0.52f, 0.8f, gold, 36f);
                    break;
                }
                case VgMove.HallowedSlam:
                {
                    float p = VgWin(t, 0.52f, 0.98f);
                    if (p >= 0f)
                    {
                        float r = 20f + 100f * VgEase(p);
                        Circle(k, r, gold.WithAlpha(0.12f * (1f - p)), 40);
                        Arc(k, r, 7f * (1f - p) + 0.5f, gold.WithAlpha(1f - p), 40);
                    }
                    VgPulse(k, 160f, t, 0.58f, 1f, VgHealGreen);
                    break;
                }
                case VgMove.RaiseGuard:
                {
                    float p = VgWin(t, 0.1f, 0.9f);
                    if (p < 0f)
                        break;
                    float a = Mathf.Sin(p * MathX.Pi), h = 80f * MathX.Pi / 180f;
                    VgBand(k, 28f, 44f, aim - h, aim + h, jade.WithAlpha(0.12f * a));
                    VgBand(k, 41f, 44f, aim - h, aim + h, jade.WithAlpha(0.85f * a));
                    break;
                }
                case VgMove.GuardedJab:
                    VgLane(k + right * 6f, dir, 20f, 140f, 14f, t, 0.45f, 0.8f, jade);
                    break;
                case VgMove.DeflectSweep:
                    VgSweep(k, aim, t, 0.25f, 0.6f, -70f, 70f, 82f, jade);
                    break;
                case VgMove.Sidestep:
                {
                    float p = VgWin(t, 0.02f, 0.7f);
                    if (p >= 0f)
                        Circle(o, 13f, jade.WithAlpha(0.3f * (1f - p)), 18);
                    break;
                }
                case VgMove.CoverAlly:
                {
                    float p = VgWin(t, 0.05f, 0.7f);
                    if (p >= 0f && owner != null)
                        Line(o, owner.VgDashTo, 1.6f, jade.WithAlpha(0.6f * (1f - p)));
                    break;
                }
                case VgMove.RallyTap:
                    VgBurst(shieldAt, t, 0.42f, 0.65f, jade, 16f);
                    VgPulse(k, 160f, t, 0.45f, 1.1f, VgHealGreen);
                    break;
                case VgMove.BulwarkAbsorb:
                {
                    float p = VgWin(t, 0.1f, 0.82f);
                    if (p >= 0f)
                    {
                        Circle(shieldAt, 8f + 12f * p, jade.WithAlpha(0.15f + 0.3f * p), 18);
                        float h = 90f * MathX.Pi / 180f;
                        VgBand(k, 38f, 40f + 5f * p, aim - h, aim + h, jade.WithAlpha(0.5f + 0.4f * p));
                    }
                    VgPulse(k, 170f, t, 0.82f, 1.15f, VgHealGreen);
                    break;
                }
                case VgMove.AegisDome:
                    VgBurst(shieldAt, t, 0.42f, 0.7f, jade, 24f);
                    break;
                case VgMove.SanctifiedWard:
                    VgPulse(k, 180f, t, 0.45f, 1.1f, Game.VgWardBlue);
                    break;
            }
        }

        /// An Aegis Dome for its life: a jade dome over where the shield was planted, fading at the end.
        void DrawVgDomeFx(Effect fx)
        {
            float grow = Mathf.Clamp01(fx.T / 0.25f), fade = Mathf.Clamp01((fx.Dur - fx.T) / 0.3f), r = fx.Radius * VgEase(grow);
            float a = fade, t = fx.T;
            Circle(fx.Pos, r, Game.VgJade.WithAlpha(0.09f * a), 48);
            Arc(fx.Pos, r, 2.4f, Game.VgJade.WithAlpha(0.8f * a), 48);
            Arc(fx.Pos, r - 6f, 1f, Game.VgJade.WithAlpha((0.25f + 0.1f * Mathf.Sin(t * 8f)) * a), 48);
            for (int i = 0; i < 6; i++)  // a hex lattice turning slowly round the rim
            {
                V2 at = fx.Pos + V2.FromAngle(i * MathX.Tau / 6f + t * 0.6f) * (r * 0.62f);
                Polygon(at, 9f, 6, t * 0.6f, Game.VgJade.WithAlpha(0.08f * a));
            }
        }
    }
}
