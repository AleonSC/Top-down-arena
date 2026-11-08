using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// One-off effects in the arena (DrawEffect and its FxKind looks: repulse, shatter, slashes, claws, chains,
    /// arc fields, light cones, icicles, jabs, bolts).
    public partial class ArenaApp
    {
        /// A short-lived effect, by kind.
        void DrawEffect(Effect fx, float t)
        {
            float f = fx.T / fx.Dur;
            switch (fx.Kind)
            {
                case FxKind.Boom:
                    if (HammerBoomDrawnByMelee(fx))
                        break;  // the Warhammer's own slam draws its impact (MeleeLooks.cs)
                    if (IsGrenadeBoom(fx))
                    {
                        DrawFragBlast(fx, f);  // SoldierLooks.cs
                        break;
                    }
                    if (IsFlakBoom(fx))
                    {
                        DrawFlakBlast(fx, f);  // TankGunLooks.cs
                        break;
                    }
                    Circle(fx.Pos, fx.Radius * (0.4f + 0.6f * f), fx.Color.WithAlpha(0.35f * (1f - f)), 24);
                    Arc(fx.Pos, fx.Radius * (0.5f + f), 3f, fx.Color.WithAlpha(1f - f), 32);
                    break;
                case FxKind.Debris:
                    DrawDebrisFx(fx, f);  // PropsLook.cs
                    break;
                case FxKind.Spark:
                    Arc(fx.Pos, Mathf.Max(1f, fx.Radius * f), 2f, fx.Color.WithAlpha(1f - f), 12);
                    break;
                case FxKind.Ring:
                    if (IsMendPulse(fx))
                    {
                        DrawMendPulseFx(fx, f);  // MendPulseLook.cs
                        break;
                    }
                    if (IsFrostNova(fx))
                    {
                        DrawFrostNova(fx);  // FrostNovaLook.cs
                        break;
                    }
                    if (IsGlacialTomb(fx))
                    {
                        DrawGlacialTomb(fx);  // GlacialTombLook.cs
                        break;
                    }
                    if (IsLifelinePlain(fx))
                        break;  // Lifeline's plain ring: its look draws from FxKind.Lifeline (LifelineLook.cs)
                    if (IsShadowStepPlain(fx))
                        break;  // the Shadow Step's plain ring: its look draws from FxKind.ShadowStep (ShadowStepLook.cs)
                    if (IsTankRing(fx))
                        break;  // Roar's and Bulldoze's plain rings: their looks draw from the cast's Callout (TankLooks.cs)
                    Arc(fx.Pos, fx.Radius * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                    break;
                case FxKind.Target:  // where a bomber will blow up: a faint zone with a ring closing in on it
                    Circle(fx.Pos, fx.Radius, fx.Color.WithAlpha(0.08f + 0.12f * f), 32);
                    Arc(fx.Pos, fx.Radius, 2f, fx.Color.WithAlpha(0.5f), 32);
                    Arc(fx.Pos, fx.Radius * (1.8f - 0.8f * f), 2f, fx.Color.WithAlpha(0.3f + 0.5f * f), 32);
                    break;
                case FxKind.Slash:
                    if (IsShadowStepPlain(fx))
                        break;  // the Shadow Step's strike is drawn by its look
                    DrawSlashFx(fx, f, t);
                    break;
                case FxKind.Jab:
                    DrawJabFx(fx, f);
                    break;
                case FxKind.Claw:
                    if (fxOn && IsBearClaw(fx))
                        DrawBearClawFx(fx);  // BearLooks.cs
                    else
                        DrawClawFx(fx, f);
                    break;
                case FxKind.Bolt:
                    DrawBoltFx(fx, f);
                    break;
                case FxKind.Shatter:
                    DrawShatterFx(fx, f);
                    break;
                case FxKind.Implode:
                    DrawImplodeFx(fx, f);  // BloodMageLook.cs
                    break;
                case FxKind.Bleed:
                    DrawBleedFx(fx, f);  // BloodMageLook.cs, like the four below
                    break;
                case FxKind.BloodShell:
                    DrawBloodShellFx(fx, f);
                    break;
                case FxKind.ShellHit:
                    DrawShellHitFx(fx, f);  // BloodShellLook.cs
                    break;
                case FxKind.StandHit:
                    DrawStandHitFx(fx, f);  // LastStandLook.cs
                    break;
                case FxKind.HolyCross:
                    DrawHolyCrossFx(fx);  // HolyCrossLook.cs
                    break;
                case FxKind.KatanaStrike:
                    DrawKatanaStrikeFx(fx);  // KatanaLook.cs
                    break;
                case FxKind.KatanaCut:
                    DrawKatanaCutFx(fx);
                    break;
                case FxKind.StaffShove:
                    if (!fxOn || !DrawStaffShoveLayer(fx, false))
                        DrawStaffShovePlain(fx, f);  // BoStaffLook.cs
                    break;
                case FxKind.CavalryCall:
                    DrawCavalryCallFx(fx);  // CavalryCallLook.cs
                    break;
                case FxKind.PactBind:
                    DrawPactBindFx(fx, f, t);  // ForcePactLook.cs, like the three below
                    break;
                case FxKind.PactSnap:
                    DrawPactSnapFx(fx, f);
                    break;
                case FxKind.PactDissolve:
                    DrawPactDissolveFx(fx, f);
                    break;
                case FxKind.PactRise:
                    DrawPactRiseFx(fx, f);
                    break;
                case FxKind.TitheSnap:
                    DrawTitheSnapFx(fx, f);  // VeinTitheLook.cs
                    break;
                case FxKind.RainRelease:
                    DrawRainReleaseFx(fx, f);  // SanguineRainLook.cs
                    break;
                case FxKind.HarvestThrow:
                    DrawHarvestThrowFx(fx);  // CrimsonHarvestLook.cs
                    break;
                case FxKind.TitanSlam:
                    DrawTitanFx(fx, false);  // TitanLook.cs (its cracks and rubble are the floor pass: DrawTitanFloor)
                    break;
                case FxKind.BurrowSnap:
                    DrawBurrowSnapFx(fx);  // BurrowerLook.cs
                    break;
                case FxKind.ShellPop:
                    DrawShellPopFx(fx, f);
                    break;
                case FxKind.Drain:
                    DrawDrainFx(fx, f);
                    break;
                case FxKind.Boil:
                    DrawBoilFx(fx, f, t);
                    break;
                case FxKind.PactBurst:
                    DrawPactBurstFx(fx, f);
                    break;
                case FxKind.Link:
                    if (!IsLifelinePlain(fx))  // (Lifeline's own beams: LifelineLook.cs)
                        DrawLinkFx(fx, f);  // AbilityLooks.cs, like Rewind
                    break;
                case FxKind.Rewind:
                    DrawRewindFx(fx, f);
                    break;
                case FxKind.Callout:
                    DrawTankCastFx(fx);  // Roar and Bulldoze's floor looks (TankLooks.cs); the name over the caster is on the HUD (AbilityLooks.cs)
                    break;
                case FxKind.Repulse:
                    DrawRepulseFx(fx, f);
                    break;
                case FxKind.Knock:
                    DrawKnockFx(fx);
                    break;
                case FxKind.RootLash:
                    DrawRootLashFx(fx, f);  // RootLook.cs
                    break;
                case FxKind.Strike:
                    DrawStrikeFx(fx, f);  // NimbusLook.cs
                    break;
                case FxKind.Lifeline:
                    DrawLifelineFx(fx);  // LifelineLook.cs
                    break;
                case FxKind.Meteor:
                    DrawMeteorFx(fx);  // ElementalistLooks.cs
                    break;
                case FxKind.Tsunami:
                    DrawTsunamiFx(fx, t);  // ElementalistLooks.cs
                    break;
                case FxKind.DeathLotus:
                    DrawDeathLotusFx(fx);  // DeathLotusLook.cs
                    break;
                case FxKind.RiotBlock:
                    DrawRiotBlockFx(fx);  // RiotGearLook.cs
                    break;
                case FxKind.ShadowStep:
                    DrawShadowStepFx(fx);  // ShadowStepLook.cs
                    break;
                case FxKind.LeapSlam:
                    DrawLeapSlamFx(fx);  // LeapSlamLook.cs
                    break;
                case FxKind.Icicle:
                    DrawIcicleFx(fx, f);
                    break;
                case FxKind.Splinter:
                    DrawSplinterFx(fx, f);
                    break;
                case FxKind.LightCone:
                    DrawLightConeFx(fx, f, t);
                    break;
                case FxKind.BloodCoat:
                    DrawBloodCoatFx(fx, f, t);  // BloodMageLook.cs
                    break;
                case FxKind.ArcField:
                    DrawArcFieldFx(fx, f);
                    break;
                case FxKind.ChainThrow:
                    DrawChainThrowFx(fx, f);
                    break;
                case FxKind.IonTrail:
                    DrawIonTrailFx(fx);
                    break;
                case FxKind.SteelWall:
                    DrawSteelWallFx(fx, f);
                    break;
                case FxKind.GreatRend:
                    DrawGreatRendFx(fx, f);  // GreatswordLook.cs
                    break;
            }
        }

        /// A parry shield breaking: cracks flash across it, then it bursts into jagged pieces with sparks.
        /// The pieces come from a generator seeded by where it broke, so everyone sees the same burst.
        /// The Repulsion Cannon's blast: a cone of air that flashes, then pressure waves rolling out to its reach,
        /// with streaks flying along it. Pos is the gun, Pos2 the reach along the aim, Radius half the cone.
        static readonly Rgba KnockDust = new Rgba(0.78f, 0.72f, 0.62f);

        /// A push sliding (Knock.cs): Pos where it was hit, Pos2 where it ends up, Radius the enemy's size. An impact
        /// puff where it was hit, a motion streak behind the enemy as it slides (following the same path and timing as
        /// the host's TickKnock), and puffs of dust kicked up along the way that linger and fade.
        void DrawKnockFx(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len, side = dir.Orthogonal();
            float r = Mathf.Max(6f, fx.Radius), slide = Game.KnockTime;
            float head = Mathf.Clamp01(fx.T / slide), tail = Mathf.Clamp01((fx.T - 0.1f) / slide);
            float fade = 1f - Mathf.Clamp01((fx.T - slide) / (fx.Dur - slide));
            // Impact: a quick ring and a flash on the side it was hit from.
            if (fx.T < 0.18f)
            {
                float k = fx.T / 0.18f;
                Arc(fx.Pos, r + 4f + 16f * k, 3f * (1f - k) + 1f, Rgba.White.WithAlpha(0.7f * (1f - k)), 18);
                Circle(fx.Pos - dir * r * 0.6f, r * 0.55f * (1f - k), Rgba.White.WithAlpha(0.45f * (1f - k)), 10);
            }
            // The streak: a band from tail to head, wide at the head, in the enemy's colour going pale.
            V2 h = fx.Pos + path * head, tl = fx.Pos + path * tail;
            if ((h - tl).Length > 1f && fade > 0f)
            {
                var col = fx.Color.Mix(Rgba.White, 0.5f);
                SetColor(col.WithAlpha(0.32f * fade));
                Tri(tl, h + side * r * 0.8f, h - side * r * 0.8f);
                for (int i = -1; i <= 1; i += 2)  // two thin speed lines at its edges
                    Line(tl + side * (i * r * 0.35f), h + side * (i * r * 0.7f), 1.5f, Rgba.White.WithAlpha(0.35f * fade));
            }
            // Dust kicked up behind it: each puff starts when the enemy passes, grows and fades.
            for (int i = 0; i < 4; i++)
            {
                float at = 0.15f + 0.22f * i;
                if (head < at)
                    break;
                float age = fx.T - at * slide, life = fx.Dur - at * slide;
                float k = Mathf.Clamp01(age / life);
                if (k >= 1f)
                    continue;
                V2 c = fx.Pos + path * at + side * ((i % 2 == 0 ? 1f : -1f) * r * 0.5f) - dir * (4f * k);
                Circle(c, r * (0.35f + 0.5f * k), KnockDust.WithAlpha(0.3f * (1f - k)), 10);
            }
        }

        void DrawRepulseFx(Effect fx, float f)
        {
            if (fxOn)
            {
                DrawRepulseBlast(fx, f);  // the cannon blast (RepulseLook.cs); with Visual effects off, the plain cone below
                return;
            }
            f = Mathf.Min(1f, f * (Game.RepulseFxTime / 0.35f));  // (the effect lives longer now, for the smoke; this look keeps its timing)
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, reach = mid.Length, half = fx.Radius;
            var col = fx.Color;
            if (f < 0.25f)  // the cone flashes
            {
                float k = 1f - f / 0.25f;
                SetColor(col.WithAlpha(0.22f * k));
                for (int i = 0; i < 10; i++)
                {
                    float a0 = aim - half + 2f * half * i / 10f, a1 = aim - half + 2f * half * (i + 1) / 10f;
                    Tri(c, c + V2.FromAngle(a0) * reach, c + V2.FromAngle(a1) * reach);
                }
            }
            for (int k = 0; k < 3; k++)  // pressure waves
            {
                float g = Mathf.Clamp01(f * 1.6f - k * 0.18f);
                if (g <= 0f || g >= 1f)
                    continue;
                Arc(c, 20f + (reach - 20f) * g, 5f * (1f - g) + 1.5f, col.Mix(Rgba.White, 0.4f).WithAlpha(0.85f * (1f - g)), 14, aim - half, 2f * half);
            }
            for (int i = 0; i < 7; i++)  // streaks
            {
                float a = aim - half * 0.85f + 1.7f * half * i / 6f, d0 = reach * (0.2f + 0.8f * f), d1 = d0 + 30f;
                Line(c + V2.FromAngle(a) * d0, c + V2.FromAngle(a) * Mathf.Min(reach, d1), 2f, Rgba.White.WithAlpha(0.6f * (1f - f)));
            }
        }

        void DrawShatterFx(Effect fx, float f)
        {
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, rr = mid.Length, half = fx.Radius, from = aim - half, span = half * 2f;
            float r0 = rr - 7f, r1 = rr + 9f;
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663) | 1u;
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            const float crackEnd = 0.18f;
            if (f < crackEnd)  // the shield whitens and cracks run across it
            {
                float k = f / crackEnd;
                Arc(c, rr, 14f, fx.Color.Mix(Rgba.White, 0.5f).WithAlpha(0.6f), 20, from, span);
                Arc(c, rr + 7f, 4f, Rgba.White.WithAlpha(0.95f), 20, from, span);
                for (int n = 0; n < 5; n++)
                {
                    float a = from + span * (0.1f + 0.8f * rnd()), rad = r1 + 1f;
                    V2 prev = c + V2.FromAngle(a) * rad;
                    for (int s = 1; s <= 3; s++)
                    {
                        rad = r1 + 1f - (r1 - r0 + 2f) * s / 3f * Mathf.Min(1f, k * 1.4f);
                        a += (rnd() - 0.5f) * 0.25f;
                        V2 next = c + V2.FromAngle(a) * rad;
                        Line(prev, next, 1.6f, Rgba.White.WithAlpha(0.6f + 0.4f * k));
                        prev = next;
                    }
                }
                return;
            }
            float g = (f - crackEnd) / (1f - crackEnd);
            float e = 1f - (float)Math.Pow(1f - g, 2.6f), fade = Mathf.Max(0f, 1f - (float)Math.Pow(g, 1.4f));
            Circle(c + V2.FromAngle(aim) * rr, (r1 - r0) * (0.4f + 0.5f * e), new Rgba(0.67f, 0.9f, 1f, 0.3f * (1f - g) * (1f - g)), 20);  // burst glow
            var bright = fx.Color.Mix(Rgba.White, 0.7f).WithAlpha(fade);
            for (int n = 0; n < 10; n++)  // sparks
            {
                V2 d = V2.FromAngle(aim + (rnd() - 0.5f) * span * 1.3f);
                V2 tip = c + d * (rr + (40f + 50f * rnd()) * e);
                Line(tip - d * 7f * (1f - g), tip, 2f, bright);
            }
            const int pieces = 9;
            var edges = new float[pieces + 1];
            for (int i = 1; i < pieces; i++)
                edges[i] = (i + (rnd() - 0.5f) * 0.6f) / pieces;
            edges[pieces] = 1f;
            for (int i = 0; i < pieces; i++)
            {
                float a0 = from + span * edges[i] + 0.02f, a1 = from + span * edges[i + 1] - 0.02f;
                float speed = 40f + 25f * rnd(), spin = (rnd() - 0.5f) * 5f;
                V2 off = V2.FromAngle((a0 + a1) / 2f + (rnd() - 0.5f) * 0.4f) * (speed * e);
                DrawShieldPiece(c, a0, a1, r0 + 2f * rnd(), r1 - 2f * rnd(), off, spin * e, (a1 - a0) * 0.25f, fx.Color.WithAlpha(0.55f * fade), bright);
            }
        }

        /// One curved piece of a broken shield (angles a0..a1, radii r0..r1, pointed sides), moved by off and
        /// turned by rot around its own middle.
        void DrawShieldPiece(V2 c, float a0, float a1, float r0, float r1, V2 off, float rot, float jag, Rgba fill, Rgba edge)
        {
            const int n = 4;
            float rm = (r0 + r1) / 2f;
            V2 mid = c + V2.FromAngle((a0 + a1) / 2f) * rm;
            var pts = new V2[2 * n + 4];
            int m = 0;
            for (int i = 0; i <= n; i++)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r1;
            pts[m++] = c + V2.FromAngle(a1 + jag) * rm;
            for (int i = n; i >= 0; i--)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r0;
            pts[m++] = c + V2.FromAngle(a0 - jag) * rm;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int i = 0; i < m; i++)
            {
                V2 d = pts[i] - mid;
                pts[i] = mid + off + new V2(d.X * cs - d.Y * sn, d.X * sn + d.Y * cs);
            }
            SetColor(fill);
            for (int i = 0; i < m; i++)
                Tri(mid + off, pts[i], pts[(i + 1) % m]);
            for (int i = 0; i < n; i++)  // bright outer edge
                Line(pts[i], pts[i + 1], 2.5f, edge);
        }

        /// A melee swing: a bright crescent sweeping across the arc, fading out (a jab or thrust is a streak).
        void DrawSlashFx(Effect fx, float f, float t)
        {
            if (DrawSlashE(fx, t))
                return;  // a player's melee swing with Visual effects on: the weapon + energy look (MeleeLooks.cs)
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return;
            if (half < 0f)  // a jab: the blade shoots forward, with speed lines along the cone
            {
                float grow = Mathf.Min(1f, f * 4f), len = reach * (0.3f + 0.7f * grow), fade = 1f - f;
                V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
                V2 tipAt = fx.Pos + dir * len;
                for (int k = -1; k <= 1; k += 2)  // the cone it covers, faintly
                    Line(fx.Pos, fx.Pos + V2.FromAngle(aim + k * -half) * len * 0.9f, 1.5f, fx.Color.WithAlpha(0.25f * fade));
                for (int k = 0; k < 4; k++)  // speed lines
                {
                    float o = (k % 2 == 0 ? 1f : -1f) * (0.35f + 0.15f * k) * (float)Math.Tan(-half) * len * 0.5f;
                    V2 a0 = fx.Pos + dir * len * (0.35f + 0.12f * k) + side * o;
                    Line(a0, a0 + dir * len * 0.25f, 1.5f, Rgba.White.WithAlpha(0.5f * fade));
                }
                Line(fx.Pos, tipAt, 10f * fade + 2f, fx.Color.WithAlpha(0.35f * fade));
                Line(fx.Pos, tipAt - dir * 6f, 3f, Rgba.White.WithAlpha(0.9f * fade));
                SetColor(Rgba.White.WithAlpha(0.95f * fade));
                Tri(tipAt + dir * 8f, tipAt - dir * 4f + side * 6f, tipAt - dir * 4f - side * 6f);
                return;
            }
            if (half < 0.2f)  // a thrust or a lunge: a streak along the line
            {
                Line(fx.Pos, fx.Pos2, 8f * (1f - f), fx.Color.WithAlpha(0.35f * (1f - f)));
                Line(fx.Pos, fx.Pos2, 2.5f, Rgba.White.WithAlpha(0.9f * (1f - f)));
                return;
            }
            if (IsHalberdFx(fx))  // effects off: the old arc, as the sweep starts (the effect now spans the whole swing)
            {
                f = (fx.T - HalberdMotion.Start1) / 0.3f;
                if (f < 0f || f >= 1f)
                    return;
            }
            float sweep = 2f * half * Mathf.Min(1f, f * 3f);
            float from = half >= MathX.Pi - 0.01f ? aim + t * 6f : aim - half;
            Arc(fx.Pos, reach * 0.78f, reach * 0.4f, fx.Color.WithAlpha(0.28f * (1f - f)), 24, from, sweep);
            Arc(fx.Pos, reach, 3f, Rgba.White.WithAlpha(0.9f * (1f - f)), 24, from, sweep);
        }

        /// Bear Totem: three claw marks rake across the arc (the sign of Radius says which way), over a faint
        /// crescent; they draw on quickly, then fade.
        void DrawClawFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = Mathf.Abs(fx.Radius), side = fx.Radius >= 0f ? 1f : -1f;
            if (reach < 1f)
                return;
            float g = Mathf.Min(1f, f * 4f), fade = 1f - f, sweep = 2f * half * g;
            float from = side > 0f ? aim - half : aim + half - sweep;  // rakes from one side to the other
            Arc(fx.Pos, reach * 0.72f, reach * 0.5f, new Rgba(0.9f, 0.3f, 0.25f, 0.16f * fade), 20, from, sweep);
            for (int k = -1; k <= 1; k++)
            {
                float r = reach * (0.78f + 0.13f * k);
                float lag = (1 - k * side) * 0.06f;  // the marks are staggered a little, like a paw's claws
                float a0 = from + (side > 0f ? 0f : sweep * lag), a1 = sweep * (1f - lag);
                Arc(fx.Pos, r, 4.5f * fade + 1f, fx.Color.WithAlpha(0.9f * fade), 14, a0, a1);
                Arc(fx.Pos, r, 1.5f, new Rgba(0.85f, 0.15f, 0.15f, 0.7f * fade), 14, a0, a1);  // a red core
            }
        }

        /// The Lance: its rectangular hitbox slides out from the Knight, lance tip leading.
        /// A Greatsword's crescent of steel hanging where its sweep's edge passed, fading as it goes.
        void DrawSteelWallFx(Effect fx, float f)
        {
            // Moonlit Crescent: a glassy arc that appears as the sweep passes, with a glint that runs along it, and a ripple
            // wherever a bullet died on it (the host's spark for it is the cue).
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius, fade = 1f - f * f, T = fx.T;
            if (reach < 1f)
                return;
            float sg = GreatWallSign(fx, fx.Pos);  // the way its cut swept (GreatswordLook.cs)
            float run0 = 2f * half * EaseOut3(Mathf.Clamp01(T / MeleeSweepTime));
            var moonCol = new Rgba(0.78f, 0.88f, 1f);
            if (run0 <= 0.01f)
                return;
            float from = aim - sg * half;
            Arc(fx.Pos, reach, Game.SteelWallBand * 2f, moonCol.WithAlpha(0.13f * fade), 24, from, sg * run0);
            Arc(fx.Pos, reach, 7f, moonCol.WithAlpha(0.4f * fade), 24, from, sg * run0);
            Arc(fx.Pos, reach, 2.4f, Rgba.White.WithAlpha(0.55f * fade), 24, from, sg * run0);
            float gl = Mathf.Clamp01((T - MeleeSweepTime) / (fx.Dur > MeleeSweepTime ? 0.84f * (fx.Dur - MeleeSweepTime) : 0.42f));
            if (gl > 0f && gl < 1f)
            {
                float ga = from + sg * 2f * half * gl;
                GlowDisc(fx.Pos + V2.FromAngle(ga) * reach, 26f, Rgba.White, 0.7f * fade);
                Arc(fx.Pos, reach, 2.4f, Rgba.White.WithAlpha(0.95f * fade), 6, ga - 0.08f, 0.16f);
            }
            var hitCol = Defs.BulletColor(Defs.KnightSlash);
            foreach (var sp in game.Effects)
            {
                if (sp.Kind != FxKind.Spark || sp.Radius != 8f || sp.Color.R != hitCol.R || sp.Color.G != hitCol.G || sp.Color.B != hitCol.B)
                    continue;
                V2 rel = sp.Pos - fx.Pos;
                float d = rel.Length, an = rel.Angle, diff = Mathf.Repeat(an - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (Mathf.Abs(d - reach) > Game.SteelWallBand + 10f || Mathf.Abs(diff) > half + 0.1f)
                    continue;
                float q = Mathf.Clamp01(sp.T / 0.4f);
                if (q >= 1f)
                    continue;
                GlowDisc(fx.Pos + V2.FromAngle(an) * reach, 22f * (1f - q * 0.4f), Rgba.White, 0.7f * (1f - q) * fade);  // the arc brightens where it was hit
                float run = 0.5f * EaseOut3(q);
                Arc(fx.Pos, reach, 2f, Rgba.White.WithAlpha(0.5f * (1f - q) * fade), 12, an - run, 2f * run);  // and a ripple runs along it
            }
        }

        /// A Railgun's ion trail: the stretch the round has passed through in the last second glows and crackles
        /// (the same stretch the host hurts enemies in: KitWeapons.cs TickIonTrails).
        void DrawIonTrailFx(Effect fx)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length, speed = fx.Radius;
            if (len < 1f || speed <= 0f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            float head = Mathf.Min(len, speed * fx.T), tail = Mathf.Max(0f, speed * (fx.T - Game.IonTrailTime));
            if (tail >= head)
                return;
            V2 a = fx.Pos + dir * tail, b = fx.Pos + dir * head;
            Line(a, b, Game.IonTrailWidth * 2f, fx.Color.WithAlpha(0.14f));
            Line(a, b, 2f, fx.Color.WithAlpha(0.7f));
            int n = Math.Max(2, (int)((head - tail) / 30f));
            for (int i = 0; i < n; i++)  // sparks jittering along it
            {
                float along = tail + (head - tail) * ((i + 0.5f) / n);
                float j = Mathf.Sin(fx.T * 47f + i * 2.3f) * Game.IonTrailWidth;
                V2 at = fx.Pos + dir * along;
                Line(at, at + side * j + dir * 6f, 1.2f, Rgba.White.WithAlpha(0.6f));
            }
        }

        /// A chain of links from `a` to `b`, with the sickle's blade at `b`.
        void DrawChain(V2 a, V2 b, float alpha)
        {
            V2 to = b - a;
            float len = to.Length;
            if (len < 1f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            int n = Math.Max(2, (int)(len / 9f));
            for (int i = 0; i <= n; i++)
            {
                V2 at = a + to * ((float)i / n);
                Circle(at, i % 2 == 0 ? 2.2f : 1.6f, Steel.WithAlpha((i % 2 == 0 ? 0.95f : 0.7f) * alpha), 6);
            }
            Arc(b - dir * 4f, 7f, 2.5f, Rgba.White.WithAlpha(0.9f * alpha), 8, dir.Angle - MathX.Pi / 2f, MathX.Pi);  // the blade, curving round
            Line(b - dir * 6f, b - dir * 1f + side * 6f, 2f, Steel.WithAlpha(alpha));
        }

        /// The Kusarigama's throw: the chain runs out to where it ended (and, if it hooked something, stays taut a moment).
        void DrawChainThrowFx(Effect fx, float f)
        {
            float run = Mathf.Min(1f, f / 0.35f), fade = fx.Radius > 0f ? 1f : 1f - Mathf.Max(0f, (f - 0.35f) / 0.65f);
            DrawChain(fx.Pos, V2.Lerp(fx.Pos, fx.Pos2, run), fade);
        }

        /// Arc Lightning going off: the strip flashes and jagged lightning crackles along and across it. The bolts
        /// come from a generator seeded by where it went off, so everyone sees the same crackle.
        void DrawArcFieldFx(Effect fx, float f)
        {
            if (ArcLightningDrawnAsThunder(fx))
                return;  // the Thunderhead bolt, drawn in the glow pass (ArcLightningLooks.cs)
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length, w = fx.Radius;
            if (len < 1f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            float fade = 1f - f;
            V2 a0 = fx.Pos + side * (w * Game.ArcNear), a1 = fx.Pos - side * (w * Game.ArcNear), e0 = fx.Pos + dir * len + side * w, e1 = fx.Pos + dir * len - side * w;  // a slight cone
            SetColor(fx.Color.WithAlpha(0.2f * fade));
            Quad(a0, e0, e1, a1);
            Line(a0, e0, 2f, fx.Color.WithAlpha(0.7f * fade));
            Line(a1, e1, 2f, fx.Color.WithAlpha(0.7f * fade));
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)(f * 8f) * 83492791) | 1u;  // re-rolls a few times as it fades
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            for (int k = 0; k < 3; k++)  // bolts running its length
            {
                V2 prev = fx.Pos + side * ((rnd() - 0.5f) * w * Game.ArcNear);
                int n = Math.Max(4, (int)(len / 40f));
                for (int i = 1; i <= n; i++)
                {
                    V2 next = fx.Pos + dir * (len * i / n) + side * ((rnd() - 0.5f) * w * 1.6f * Game.ArcTaper(len * i / n, len));
                    Line(prev, next, k == 0 ? 3f : 1.5f, (k == 0 ? Rgba.White : fx.Color).WithAlpha(fade));
                    prev = next;
                }
            }
        }

        /// Arc Lightning's charge: a ring filling round you (gold when full) and, for your own, the strip it will
        /// shock, reaching out as you hold.
        void DrawArcCharge(Player p, bool mine, float t)
        {
            if (fxOn)
                return;  // the halo, sparks and dashed strip, drawn in the glow pass (ArcLightningLooks.cs)
            float c = game.BowCharge(p, p.DrawT);
            bool full = c >= 1f;
            var gold = new Rgba(1f, 0.82f, 0.3f);
            var bolt = Defs.BulletColor(Defs.BoltShot);
            float r = p.Def.BodyR + 11f;
            Arc(p.Disp, r, 2.5f, new Rgba(1f, 1f, 1f, 0.12f), 32);
            Arc(p.Disp, r, full ? 4f : 2.5f, (full ? gold : bolt).WithAlpha(full ? 0.75f + 0.25f * Mathf.Sin(t * 20f) : 0.85f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, c));
            if (!mine)
                return;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            float len = Game.ArcLength(p, c), w = Game.ArcWidth(p);
            var col = (full ? gold : bolt).WithAlpha(0.35f + 0.35f * c);
            V2 a = p.Disp + side * (w * Game.ArcNear), b = p.Disp - side * (w * Game.ArcNear), ae = p.Disp + dir * len + side * w, be = p.Disp + dir * len - side * w;  // a slight cone
            SetColor(col.WithAlpha(0.05f + 0.1f * c));
            Quad(a, ae, be, b);
            Line(a, ae, 2f, col);
            Line(b, be, 2f, col);
            Line(ae, be, 3f, col.WithAlpha(0.6f + 0.4f * Mathf.Sin(t * 30f)));  // the crackling far edge
        }

        /// Chain Light's cone: a flash of light fanning out from the caster, brightest at its edges, with rays
        /// running out through it.
        void DrawLightConeFx(Effect fx, float f, float t)
        {
            if (ChainLightDrawnAsRadiance(fx))
                return;  // the radiance, drawn in the glow pass (ChainLightLooks.cs)
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, half = fx.Radius, aim = to.Angle;
            if (reach < 1f)
                return;
            float fade = 1f - f, grow = Mathf.Min(1f, f / 0.15f);
            float r = reach * (0.6f + 0.4f * grow);
            const int segs = 14;
            for (int k = 0; k < segs; k++)  // the cone, filled
            {
                float a0 = aim - half + 2f * half * k / segs, a1 = aim - half + 2f * half * (k + 1) / segs;
                SetColor(fx.Color.WithAlpha(0.22f * fade));
                Tri(fx.Pos, fx.Pos + V2.FromAngle(a0) * r, fx.Pos + V2.FromAngle(a1) * r);
            }
            Line(fx.Pos, fx.Pos + V2.FromAngle(aim - half) * r, 2.5f, Rgba.White.WithAlpha(0.7f * fade));
            Line(fx.Pos, fx.Pos + V2.FromAngle(aim + half) * r, 2.5f, Rgba.White.WithAlpha(0.7f * fade));
            Arc(fx.Pos, r, 3f, fx.Color.WithAlpha(0.8f * fade), segs, aim - half, 2f * half);
            for (int k = 0; k < 5; k++)  // rays
            {
                float a = aim - half * 0.8f + half * 1.6f * k / 4f;
                Line(fx.Pos + V2.FromAngle(a) * (r * 0.15f), fx.Pos + V2.FromAngle(a) * (r * (0.5f + 0.45f * f)), 1.5f, Rgba.White.WithAlpha(0.45f * fade));
            }
        }

        /// The Frost Lance's icicle: it shoots out from you to full length, holds a moment, cracks as its
        /// splinters fly (Splinter), then melts away.
        void DrawIcicleFx(Effect fx, float f)
        {
            if (fxOn)
            {
                DrawIcicleCrystal(fx, f);  // the crystal spear (FrostLook.cs); with Visual effects off, the plain wedge below
                return;
            }
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return;
            V2 dir = to / reach, side = dir.Orthogonal();
            float g = Mathf.Min(1f, f / 0.2f);
            g = 1f - (1f - g) * (1f - g);  // shoots out, easing to full length
            float fade = f < 0.6f ? 1f : 1f - (f - 0.6f) / 0.4f;
            var ice = new Rgba(0.78f, 0.94f, 1f);
            V2 l = fx.Pos + side * w, r = fx.Pos - side * w, tip = fx.Pos + dir * (reach * g);
            SetColor(fx.Color.WithAlpha(0.18f * fade));  // a cold haze round it
            Tri(l + side * 6f, tip + dir * 8f, r - side * 6f);
            SetColor(ice.WithAlpha(0.85f * fade));       // the icicle, tapering to its point
            Tri(l, tip, r);
            SetColor(Rgba.White.WithAlpha(0.6f * fade)); // light catching one face
            Tri(l, tip, fx.Pos + side * (w * 0.2f));
            Line(fx.Pos, tip, 1.2f, Rgba.White.WithAlpha(0.8f * fade));  // the bright ridge
            // Ridges of frost stepping down its length.
            for (int k = 1; k <= 4; k++)
            {
                float at = reach * g * k / 5f, half = w * (1f - (float)k / 5f);
                V2 c = fx.Pos + dir * at;
                Line(c + side * half, c + side * half * 0.3f + dir * 6f, 1.5f, Rgba.White.WithAlpha(0.5f * fade));
                Line(c - side * half, c - side * half * 0.3f + dir * 6f, 1.5f, Rgba.White.WithAlpha(0.5f * fade));
            }
            if (f > 0.35f)  // cracks run through it as it splinters
            {
                float k = Mathf.Min(1f, (f - 0.35f) / 0.15f);
                for (int i = 0; i < 3; i++)
                {
                    V2 c = fx.Pos + dir * (reach * (0.25f + 0.25f * i));
                    float half = w * (1f - (0.25f + 0.25f * i)) * k;
                    Line(c + side * half + dir * 4f, c - side * half - dir * 4f, 1.2f, new Rgba(0.35f, 0.6f, 0.85f, 0.8f * fade));
                }
            }
        }

        /// The Frost Lance's splinters: shards of ice bursting off both sides of the icicle (Pos to Pos2) out to
        /// Radius. Seeded by where it was cast, so everyone sees the same burst.
        void DrawSplinterFx(Effect fx, float f)
        {
            if (fxOn)
            {
                DrawSplinterCrystal(fx, f);  // tumbling crystal shards (FrostLook.cs)
                return;
            }
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, spread = fx.Radius;
            if (reach < 1f)
                return;
            V2 dir = to / reach, side = dir.Orthogonal();
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)fx.Pos2.X * 83492791) | 1u;
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            float e = 1f - (1f - f) * (1f - f);  // fast out, slowing
            float fade = 1f - f;
            var ice = new Rgba(0.78f, 0.94f, 1f);
            SetColor(fx.Color.WithAlpha(0.1f * fade));  // the frosted band they sweep
            V2 a0 = fx.Pos + side * (spread * e), a1 = fx.Pos - side * (spread * e);
            Tri(a0, a0 + dir * reach, a1 + dir * reach);
            Tri(a0, a1 + dir * reach, a1);
            int n = Math.Max(4, (int)(reach / 22f));
            for (int i = 0; i < n; i++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float along = reach * (i + 0.3f + 0.4f * rnd()) / n;
                    float tilt = (rnd() - 0.5f) * 0.8f, len = 7f + 6f * rnd();
                    V2 heading = (side * sgn + dir * tilt).Normalized();
                    V2 at = fx.Pos + dir * along + heading * (spread * e * (0.6f + 0.4f * rnd()));
                    V2 back = at - heading * len, across = heading.Orthogonal() * 2.2f;
                    SetColor(ice.WithAlpha(0.95f * fade));
                    Tri(at + heading * 3f, back + across, back - across);
                    Line(back - heading * 6f, back, 1f, Rgba.White.WithAlpha(0.4f * fade));  // a frost trail
                }
        }

        void DrawJabFx(Effect fx, float f)
        {
            if (DrawJabE(fx, Time.unscaledTime))
                return;  // the Lance or the Warhammer with Visual effects on (MeleeLooks.cs)
            if (fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G && fx.Color.B == Game.HammerBoomColor.B)
                return;  // the Warhammer's slam is a circle: with the melee look off, its blast ring (a Boom beside it) shows it
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return;
            V2 dir = to / reach, side = dir.Orthogonal();
            float g = Mathf.Min(1f, f / 0.35f);
            g = 1f - (1f - g) * (1f - g) * (1f - g);  // fast out, easing in to full reach
            float fade = f < 0.35f ? 1f : 1f - (f - 0.35f) / 0.65f;
            float front = reach * g;
            // The box, from your body to its sliding front edge.
            V2 a0 = fx.Pos + side * w, a1 = fx.Pos - side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
            SetColor(fx.Color.WithAlpha(0.2f * fade));
            Tri(a0, b0, b1);
            Tri(a0, b1, a1);
            Line(a0, b0, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(a1, b1, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(b0, b1, 4f, Rgba.White.WithAlpha(0.9f * fade));  // the leading edge
            // Speed streaks riding along inside it.
            for (int k = 0; k < 4; k++)
            {
                float o = (k - 1.5f) / 1.5f * w * 0.7f, at = front * (0.25f + 0.15f * (k % 2));
                V2 s0 = fx.Pos + dir * at + side * o;
                Line(s0, s0 + dir * front * 0.35f, 1.5f, Rgba.White.WithAlpha(0.45f * fade));
            }
            // The lance itself, slid out with its tip at the front edge.
            V2 tipAt = fx.Pos + dir * front, tail = fx.Pos + dir * Mathf.Max(0f, front - reach * 0.8f);
            Line(tail, tipAt - dir * 10f, 4f, Rgba.White.WithAlpha(0.85f * fade));
            SetColor(Rgba.White.WithAlpha(0.95f * fade));
            Tri(tipAt + dir * 6f, tipAt - dir * 14f + side * 7f, tipAt - dir * 14f - side * 7f);
        }

        /// Jagged lightning between two points.
        void DrawBoltFx(Effect fx, float f)
        {
            if (ChainLightDrawnAsRadiance(fx))
                return;  // Chain Light's beam, drawn in the glow pass (ChainLightLooks.cs)
            if (DrawJuicyBolt(fx, f))
                return;  // flickering, with a white-hot core (ShotLooks.cs)
            V2 a = fx.Pos, b = fx.Pos2, n = (b - a).Normalized().Orthogonal();
            V2 prev = a;
            for (int k = 1; k <= 6; k++)
            {
                V2 pt = k == 6 ? b : V2.Lerp(a, b, k / 6f) + n * (((k * 37 + fx.Pos.X) % 17f) - 8.5f);
                Line(prev, pt, 3f, fx.Color.WithAlpha(1f - f));
                prev = pt;
            }
        }

        bool ImCaged()
        {
            foreach (var c in game.Cages.Values)
                if (c.PlayerId == game.MyId)
                    return true;
            return false;
        }

        bool AnyWells()
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.GravityWell)
                    return true;
            return false;
        }
    }
}
