using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Vowguard, held and swung (Docs/Mockups/vowguard.html; the poses are Core/VowguardMotion.cs, which the host also
    /// times the hits from). The Knight's model leaves its arms and weapon out with it; this draws both arms and pauldrons, an
    /// arming sword in the right glove and a heater shield on the left arm:
    ///   - at the ready in the stance you're in (Valor: sword forward, shield on the arm; Sanctuary: shield up front, sword
    ///     tucked), easing between them on a switch, or mid-step from the newest FxKind.VgStep's age (DrawVgIdle);
    ///   - each step's light (DrawVgStepFx): gold cuts, lanes, bursts and rings in Valor, jade sweeps and pulses in Sanctuary,
    ///     green heal rings, the Cleave's crescent, a blue ward ring;
    ///   - the Aegis Dome for its life (FxKind.VgDome, DrawVgDomeFx);
    ///   - over the head two rows of diamonds, the stance you're in on top (one per step of your chain, finishers edged white),
    ///     the other dim under it; Faith as six dots under your feet; Sanctuary's guard as a faint jade arc in front.
    public partial class ArenaApp
    {
        static readonly Rgba VgSteel = new Rgba(0.93f, 0.95f, 0.98f), VgEdgeDark = new Rgba(0.16f, 0.18f, 0.23f), VgGuardGold = new Rgba(0.85f, 0.66f, 0.24f),
            VgGrip = new Rgba(0.36f, 0.27f, 0.2f), VgShieldFace = new Rgba(0.84f, 0.86f, 0.9f), VgHealGreen = new Rgba(0.56f, 0.88f, 0.56f);
        const float VgLean = 0.25f;  // a move's lean is drawn this much (the body itself doesn't move but for a dash)

        class VgView
        {
            public int Stance = -1;
            public float Blend = 1f;  // 0-1 from the old stance's rest pose to this one's
            public VgPose From;
        }

        readonly Dictionary<int, VgView> vgViews = new Dictionary<int, VgView>();

        /// The step effect of this player that is playing now (the newest), or null.
        Effect VgFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.VgStep || fx.T >= fx.Dur || Mathf.RoundToInt(fx.Radius) / 100 != id)
                    continue;
                if (best == null || fx.T < best.T)
                    best = fx;
            }
            return best;
        }

        static Rgba VgCol(int stance) => stance == 0 ? Game.VgGold : Game.VgJade;

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
            }
            v.Blend = Mathf.Min(1f, v.Blend + Time.deltaTime / Game.VgSwitchCd);
            var fx = VgFxOf(p.Id);
            var m = fx != null ? VowguardMotion.Of(Mathf.RoundToInt(fx.Radius) % 100) : null;
            float dur = fx != null ? fx.Dur - Game.VgFxTail : 1f;
            VgPose pose;
            float aim = p.Aim;
            if (m != null && m.Stance == p.VgStance && fx.T < dur)
            {
                pose = VowguardMotion.PoseAt(m, fx.T / dur);
                aim = (fx.Pos2 - fx.Pos).Angle;
                if (m.Keep)
                    pose.Dx = pose.Dy = 0f;  // (the body itself dashes)
            }
            else
                pose = VgRestBlend(v);
            DrawVgPose(p.Disp, aim, pose, VgCol(p.VgStance), 1f);
            DrawVgGuard(p, t);
            DrawVgPips(p, t);
        }

        static VgPose VgRestBlend(VgView v)
        {
            float u = v.Blend * v.Blend * (3f - 2f * v.Blend);
            return VgPose.Lerp(v.From, VowguardMotion.Rest(v.Stance), u);
        }

        /// Both arms, pauldrons, the shield and the sword for a pose round `o` facing `aim`.
        void DrawVgPose(V2 o, float aim, VgPose m, Rgba stanceCol, float alpha)
        {
            o += LnRot(new V2(m.Dx * VgLean, m.Dy * VgLean), aim);
            float sw = aim + m.Sword * MathX.Pi / 180f, sh = aim + m.Shield * MathX.Pi / 180f;
            V2 dS = V2.FromAngle(sw), dL = V2.FromAngle(sh);
            V2 hR = o + dS * (7f + 7f * m.Reach), hL = o + dL * (m.ShieldD * 0.7f + 3f);
            V2 sR = o + LnRot(new V2(1f, 9.5f), aim), sL = o + LnRot(new V2(1f, -9.5f), aim);
            DrawVgSword(hR + new V2(3f, 4f), dS, true, alpha);  // its shadow
            LanceArm(o, sR, hR, alpha);
            DrawVgSword(hR, dS, false, alpha);
            LanceArm(o, sL, hL, alpha);
            DrawVgShield(hL, dL, m.ShieldS, stanceCol, alpha);
            LancePauldron(sR, aim, alpha);
            LancePauldron(sL, aim, alpha);
            Circle(hR, 4.6f, LnDark.WithAlpha(alpha), 12);
            Circle(hR, 3.9f, LnGlove.WithAlpha(alpha), 12);
        }

        /// The arming sword with its grip at `grip`, pointing along `dir`: a pommel, a leather grip, a gold cross, a bright blade.
        void DrawVgSword(V2 grip, V2 dir, bool shadow, float alpha)
        {
            V2 side = new V2(-dir.Y, dir.X);
            if (shadow)
            {
                Line(grip - dir * 6f, grip + dir * 40f, 4f, new Rgba(0f, 0f, 0f, 0.22f * alpha));
                return;
            }
            Circle(grip - dir * 7f, 2.6f, VgGuardGold.WithAlpha(alpha), 8);
            Line(grip - dir * 6f, grip + dir * 2f, 3.2f, VgGrip.WithAlpha(alpha));
            Line(grip + dir * 3f - side * 6f, grip + dir * 3f + side * 6f, 2.6f, VgGuardGold.WithAlpha(alpha));
            Line(grip + dir * 4f, grip + dir * 37f, 4.6f, VgEdgeDark.WithAlpha(alpha));
            Line(grip + dir * 4f, grip + dir * 36f, 3f, VgSteel.WithAlpha(alpha));
            VTri(grip + dir * 36f + side * 1.5f, VgSteel.WithAlpha(alpha), grip + dir * 36f - side * 1.5f, VgSteel.WithAlpha(alpha), grip + dir * 41f, VgSteel.WithAlpha(alpha));
            Line(grip + dir * 6f, grip + dir * 30f, 0.8f, new Rgba(0.6f, 0.66f, 0.75f, alpha));  // the fuller
        }

        /// The heater shield on the left glove, seen edge-on from above: a curved plate facing `face`, rimmed in the stance's colour.
        void DrawVgShield(V2 hand, V2 face, float scale, Rgba rim, float alpha)
        {
            float ang = face.Angle, k = scale;
            V2 c = hand - face * (9f * k);
            Arc(c, 12.25f * k, 5.5f * k, VgShieldFace.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            Arc(c, 15f * k, 1.6f, LnDark.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            Arc(c, 9.5f * k, 1.6f, LnDark.WithAlpha(alpha), 14, ang - 0.95f, 1.9f);
            for (int s = -1; s <= 1; s += 2)
            {
                V2 d = V2.FromAngle(ang + 0.95f * s);
                Line(c + d * (9.5f * k), c + d * (15f * k), 1.6f, LnDark.WithAlpha(alpha));
            }
            Arc(c, 14f * k, 1.4f, rim.WithAlpha(alpha), 14, ang - 0.88f, 1.76f);
            Arc(c, 11.5f * k, 1f, new Rgba(1f, 1f, 1f, 0.35f * alpha), 10, ang - 0.7f, 0.9f);
            Circle(hand + face * 3.4f, 1.8f, VgGuardGold.WithAlpha(alpha), 8);
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
