using UnityEngine;

namespace Arena
{
    /// The Lightbringer, held and swung (Docs/Mockups/lightbringer-rework.html; the motion is Core/LightbringerMotion.cs, which the
    /// host also times the hits from). The Knight's model leaves its arms and sword out with the Lightbringer; this draws both arms,
    /// the longsword in the sword hand (white steel, gold guard, a sun-disc pommel; its fuller glows as the light gauge fills, and
    /// the whole blade burns when it is full) and its shadow:
    ///   - held at the ready, breathing (DrawLightIdle, from the player loop);
    ///   - each chain step (FxKind.LightStep, Radius = the knight's id * 10 + the step, 4 a Radiant Noon), played from its age with
    ///     a gold trail off the blade (DrawLightStepFx); Noon's ring of light, rays and pillar are drawn here too (the crescents are
    ///     ordinary sword waves);
    ///   - the Sunlance (FxKind.Sunlance): light gathering in the off hand and a tether to the ally, then the thrust and a lance
    ///     of light to them that flares as it lands (DrawSunlanceFx).
    /// Three diamonds over the knight's head show the chain (DrawLightPips), a ring under your own feet fills as the Sunlance comes
    /// back, and a gold hexagon turns round anyone a Mercy ward is on (DrawMercyWard).
    public partial class ArenaApp
    {
        static readonly Rgba LbSteelHi = new Rgba(0.95f, 0.96f, 0.98f), LbSteelLo = new Rgba(0.7f, 0.74f, 0.82f), LbGold = new Rgba(0.88f, 0.71f, 0.28f),
            LbGoldDark = new Rgba(0.29f, 0.2f, 0.04f), LbGrip = new Rgba(0.95f, 0.93f, 0.87f), LbLight = new Rgba(1f, 0.84f, 0.47f), LbHot = new Rgba(1f, 0.97f, 0.87f),
            LbHeal = new Rgba(0.47f, 0.93f, 0.59f), LbArmDark = new Rgba(0.16f, 0.14f, 0.06f);
        const int LbTrailN = 10;
        const float LbTrailDt = 0.012f;

        /// The step or lance effect of this player whose pose is playing now (the newest), or null. (Noon's ring and the lance's
        /// light outlast the pose; once it has settled back to the guard the sword is drawn held at the ready again.)
        Effect LightFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
            {
                if (fx.T >= fx.Dur)
                    continue;
                int code = Mathf.RoundToInt(fx.Radius);
                bool mine;
                if (fx.Kind == FxKind.LightStep)
                {
                    int kind = code % 10;
                    mine = code / 10 == id && kind >= 1 && kind <= 4 && fx.T < LightbringerMotion.Steps[(kind == 4 ? 3 : kind) - 1].Dur + LightbringerMotion.Recover;
                }
                else
                    mine = fx.Kind == FxKind.Sunlance && code / 1000 == id && fx.T < LightbringerMotion.Lance.Dur + LightbringerMotion.Recover;
                if (mine && (best == null || fx.T < best.T))
                    best = fx;
            }
            return best;
        }

        static float LightGlow(Player p) => p == null ? 0.2f : Mathf.Clamp01((float)p.Light / Game.LightMax);

        /// The sword held at the ready, when neither a step nor a lance is playing.
        void DrawLightIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Lightbringer || p.Downed)
                return;
            if (LightFxOf(p.Id) != null)
                return;
            var m = LightbringerMotion.Ready;
            float b = Mathf.Sin(t * 2.6f);
            m.Rr += 0.5f * b; m.Lr += 0.5f * b; m.Rb += 0.03f * b;
            DrawLightPose(p.Disp, p.Aim, m, p.Def.Color, LightGlow(p), 0f, t, 1f);
            DrawLightPips(p, t);
        }

        /// One chain step, from its effect's age.
        void DrawLightStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, kind = code % 10;
            if (kind < 1 || kind > 4)
                return;
            int step = kind == 4 ? 3 : kind;
            bool radiant = kind == 4;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            var s = LightbringerMotion.Steps[step - 1];
            float aim = (fx.Pos2 - fx.Pos).Angle, c = fx.T, t = Time.time;
            if (owner != null && c > s.Dur)  // settling back to the guard: turn to where they aim now
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((c - s.Dur) / LightbringerMotion.Recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            if (step == 3)
            {
                DrawSundial(fx, radiant, false);  // the cracks (LightbringerFxLook.cs); its light is in the glow pass
                if (!Juicy)
                    DrawSundial(fx, radiant, true);
            }
            bool current = owner == null || LightFxOf(id) == fx;
            if (!current)
                return;  // a newer step (or a lance) by the same player has taken over
            V2 o = owner != null ? owner.Disp : fx.Pos;
            float glow = radiant ? 1f : LightGlow(owner);
            DrawLightTrail(o, aim, s, c, glow);
            DrawLightPose(o, aim, LightbringerMotion.At(s, c), owner != null ? owner.Def.Color : new Rgba(0.85f, 0.76f, 0.48f), glow, 0f, t, 1f);
            if (owner != null)
                DrawLightPips(owner, t);
        }

        /// The gold trail off the blade while it cuts.
        void DrawLightTrail(V2 o, float aim, TwinBladesMotion.Step s, float c, float glow)
        {
            float wide = 1f + 0.35f * (glow >= 1f ? 1f : 0f);
            V2 pTip = default(V2), pBase = default(V2);
            bool have = false;
            for (int i = 0; i <= LbTrailN; i++)
            {
                float ci = c - i * LbTrailDt, k = ci / s.Dur;
                bool hot = ci >= 0f && k >= s.HotR0 - 0.04f && k <= s.HotR1 + 0.02f;
                if (!hot) { have = false; continue; }
                V2 hand, dir;
                TwinHand(o, aim, LightbringerMotion.At(s, ci), true, out hand, out dir);
                V2 tip = hand + dir * (5f + LightbringerMotion.BladeLen), bse = hand + dir * (5f + LightbringerMotion.BladeLen * 0.3f);
                if (have)
                {
                    float a0 = 1f - (i - 1f) / LbTrailN, a1 = 1f - (float)i / LbTrailN;
                    a0 *= a0; a1 *= a1;
                    VTri(pTip, LbLight.WithAlpha(0.36f * a0 * wide), tip, LbLight.WithAlpha(0.36f * a1 * wide), bse, LbLight.WithAlpha(0f));
                    VTri(pTip, LbLight.WithAlpha(0.36f * a0 * wide), bse, LbLight.WithAlpha(0f), pBase, LbLight.WithAlpha(0f));
                    Line(pTip, tip, (2.4f * a0 + 0.4f) * wide, LbHot.WithAlpha(0.9f * a0));
                }
                pTip = tip; pBase = bse; have = true;
            }
        }

        /// The Sunlance: the gather and tether, the thrust, the lance of light and its flare on the ally.
        void DrawSunlanceFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 1000, target = code % 1000;
            Player owner, ally;
            game.Players.TryGetValue(id, out owner);
            game.Players.TryGetValue(target, out ally);
            if (!Juicy)
                DrawSpearLight(fx);  // the Spear (LightbringerFxLook.cs); in the glow pass when Visual effects are on
            if (owner != null && LightFxOf(id) != fx)
                return;
            var s = LightbringerMotion.Lance;
            V2 o = owner != null ? owner.Disp : fx.Pos, to = ally != null ? ally.Disp : fx.Pos2;
            float c = fx.T, t = Time.time, aim = (to - o).Angle, release = LightbringerMotion.LanceRelease;
            float palm = c < release ? Mathf.Clamp01(c / (release * 0.8f)) : Mathf.Clamp01(1f - (c - release) / 0.15f);
            if (c < release)  // the tether to the ally, and a closing ring on them
            {
                float a = Mathf.Clamp01(c / 0.12f);
                DashedLine(o, to, 1.5f, LbHeal.WithAlpha(0.55f * a));
                float rr = 26f - 10f * (c / release);
                Arc(to, rr, 1.6f, LbHeal.WithAlpha(0.8f * a), 24);
            }
            DrawLightTrail(o, aim, s, c, 1f);
            var m = LightbringerMotion.At(s, c);
            DrawLightPose(o, aim, m, owner != null ? owner.Def.Color : new Rgba(0.85f, 0.76f, 0.48f), c < release ? 1f : LightGlow(owner), palm, t, 1f);
            if (owner != null)
                DrawLightPips(owner, t);
        }

        /// Mercy's ward on a player: the Hex plates (LightbringerFxLook.cs), here when Visual effects are off (the glow pass draws
        /// them otherwise).
        void DrawMercyWard(Player p, float t)
        {
            if (!Juicy)
                DrawMercyPlates(p, t);
        }

        // --- pieces ---------------------------------------------------------------------------------------------

        /// Both arms, pauldrons, the sword (and its shadow) and gloves. `palm` 0..1 lights the off hand (the Sunlance gathering).
        void DrawLightPose(V2 o, float aim, TwinBladesMotion.Pose m, Rgba armCol, float glow, float palm, float t, float alpha)
        {
            V2 hR, dR, hL, dL;
            TwinHand(o, aim, m, true, out hR, out dR);
            TwinHand(o, aim, m, false, out hL, out dL);
            DrawLongsword(hR + new V2(3f, 5f), dR.Angle, alpha, true, 0f, t);
            float body = aim + m.Sp + m.Tw;
            for (int k = 0; k < 2; k++)
            {
                V2 s = o + V2.FromAngle(body + (k == 0 ? 1.48f : -1.48f)) * 11.5f, hand = k == 0 ? hR : hL;
                V2 d = hand - s, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                if (n.X * (s.X - o.X) + n.Y * (s.Y - o.Y) < 0f)
                    n = n * -1f;
                V2 elbow = (s + hand) * 0.5f + n * Mathf.Max(0f, 7f - d.Length * 0.2f);
                Line(s, elbow, 8f, LbArmDark.WithAlpha(alpha));
                Line(elbow, hand, 8f, LbArmDark.WithAlpha(alpha));
                Line(s, elbow, 5.4f, armCol.WithAlpha(alpha));
                Line(elbow, hand, 5.4f, armCol.WithAlpha(alpha));
                Circle(s, 6.8f, LbArmDark.WithAlpha(alpha), 12);
                Circle(s, 5.6f, new Rgba(0.93f, 0.88f, 0.72f, alpha), 12);
                Circle(s - new V2(0.8f, 1.2f), 2.3f, new Rgba(1f, 0.97f, 0.88f, alpha), 8);
            }
            DrawLongsword(hR, dR.Angle, alpha, false, glow, t);
            if (palm > 0f)  // light gathering in the off hand
            {
                Circle(hL, 18f * palm + 4f, LbLight.WithAlpha(0.25f * palm * alpha), 16);
                Circle(hL, 9f * palm + 3f, LbHot.WithAlpha(0.55f * palm * alpha), 12);
                for (int i = 0; i < 8; i++)
                {
                    float k = Mathf.Repeat(t * 2.4f + i / 8f, 1f), a = i * MathX.Tau / 8f + t;
                    Circle(hL + V2.FromAngle(a) * ((1f - k) * 40f), 1.6f, LbHot.WithAlpha(palm * k * alpha), 6);
                }
            }
            foreach (var h in new[] { hR, hL })
            {
                Circle(h, 5.6f, LbArmDark.WithAlpha(alpha), 12);
                Circle(h, 4.4f, new Rgba(0.91f, 0.87f, 0.77f, alpha), 12);
                Circle(h - new V2(1.2f, 1.5f), 1.4f, new Rgba(1f, 1f, 1f, 0.5f * alpha), 6);
            }
        }

        /// The Lightbringer with its origin at the glove, pointing along `ang`: a sun-disc pommel, a wrapped white grip, a swept
        /// gold guard with a gem, and a straight white blade whose fuller glows with the gauge (`glow` 0..1; at 1 it burns).
        void DrawLongsword(V2 hand, float ang, float alpha, bool shadow, float glow, float t)
        {
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            float L = LightbringerMotion.BladeLen;
            if (shadow)
            {
                Rgba sh = new Rgba(0f, 0f, 0f, 0.26f * alpha);
                SetColor(sh);
                Quad(hand + dir * 5f - side * 2.8f, hand + dir * (5f + L - 9f) - side * 2.5f, hand + dir * (5f + L - 9f) + side * 2.5f, hand + dir * 5f + side * 2.8f);
                Line(hand - dir * 10f, hand + dir * 4f, 3.6f, sh);
                return;
            }
            if (glow > 0f)  // the blade's light
            {
                float pulse = glow >= 1f ? 0.85f + 0.15f * Mathf.Sin(t * 12f) : 1f;
                Line(hand + dir * 8f, hand + dir * (5f + L), (6f + 6f * glow) * pulse, LbLight.WithAlpha(0.18f * glow * alpha));
                Line(hand + dir * 10f, hand + dir * (5f + L - 2f), (3f + 3f * glow) * pulse, LbHot.WithAlpha(0.22f * glow * alpha));
            }
            V2 b0 = hand + dir * 5f, b1 = hand + dir * (5f + L - 9f), tip = hand + dir * (5f + L);
            SetColor(new Rgba(0.06f, 0.08f, 0.12f, 0.85f * alpha));  // the outline
            Quad(b0 - side * 3.6f, b1 - side * 3.3f, b1 + side * 3.3f, b0 + side * 3.6f);
            var ol = new Rgba(0.06f, 0.08f, 0.12f, 0.85f * alpha);
            VTri(b1 - side * 3.3f, ol, tip + dir * 1.2f, ol, b1 + side * 3.3f, ol);
            SetColor(LbSteelHi.Mix(LbSteelLo, 0.25f).WithAlpha(alpha));
            Quad(b0 - side * 2.8f, b1 - side * 2.5f, b1 + side * 2.5f, b0 + side * 2.8f);
            VTri(b1 - side * 2.5f, LbSteelHi.WithAlpha(alpha), tip, LbSteelHi.WithAlpha(alpha), b1 + side * 2.5f, LbSteelLo.WithAlpha(alpha));
            Line(hand + dir * 8f, hand + dir * (5f + L - 12f), 1.6f, LbLight.WithAlpha((0.45f + 0.55f * glow) * alpha));  // the fuller
            Line(hand + dir * 7f - side * 1.8f, hand + dir * (5f + L - 6f) - side * 1.4f, 0.7f, new Rgba(1f, 1f, 1f, 0.9f * alpha));  // the edge's shine
            // the guard: swept forward at the tips, a gem in the middle
            V2 g = hand + dir * 3.5f;
            Line(g - side * 9f - dir * 2.5f, g - side * 4f, 3f, LbGold.WithAlpha(alpha));
            Line(g - side * 4f, g + side * 4f, 3.2f, LbGold.WithAlpha(alpha));
            Line(g + side * 4f, g + side * 9f - dir * 2.5f, 3f, LbGold.WithAlpha(alpha));
            Circle(g, 1.7f, LbHot.WithAlpha(alpha), 8);
            // the grip and the sun-disc pommel
            Line(hand - dir * 7f, hand + dir * 2f, 3.6f, LbGrip.WithAlpha(alpha));
            for (int i = 0; i < 3; i++)
            {
                V2 w0 = hand - dir * (5.5f - i * 2.5f);
                Line(w0 - side * 1.8f, w0 + dir * 1f + side * 1.8f, 0.8f, new Rgba(0.72f, 0.65f, 0.49f, alpha));
            }
            V2 pm = hand - dir * 9.5f;
            for (int i = 0; i < 8; i++)
            {
                V2 d = V2.FromAngle(i * MathX.Tau / 8f + t * 0.6f);
                Line(pm + d * 4f, pm + d * 6f, 1f, LbGold.WithAlpha(0.9f * alpha));
            }
            Circle(pm, 3.9f, LbGoldDark.WithAlpha(alpha), 12);
            Circle(pm, 3.2f, LbGold.WithAlpha(alpha), 12);
        }

        /// Three diamonds over the knight's head: lit for each step thrown in this chain; the third glows when Noon is next.
        void DrawLightPips(Player p, float t)
        {
            if (p.Id == game.MyId && game.SunlanceCd > 0f)  // the Sunlance coming back: a ring filling under your feet
            {
                float k = 1f - game.SunlanceCd / Game.LanceCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, LbLight.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            int step = TwinBladesMotion.StepOf(p.Combo);
            if (step == 0 || step > 3)
                return;
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            for (int i = 0; i < 3; i++)
            {
                V2 at = c + new V2((i - 1f) * 10f, 0f);
                float r = i == 2 ? 3.8f : 3f;
                bool lit = i < step, next = i == 2 && step == 2;
                Rgba col = lit ? LbLight : next ? LbHot.WithAlpha(0.6f + 0.4f * Mathf.Sin(t * 14f)) : new Rgba(1f, 1f, 1f, 0.15f);
                Polygon(at, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                Polygon(at, r, 4, 0f, col);
            }
        }
    }
}
