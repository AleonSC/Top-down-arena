using UnityEngine;

namespace Arena
{
    /// The Storm Hammer's looks (Core/StormHammer.cs; Docs/Mockups/storm-hammer-throw.html, Thunderhead, and storm-hammer-slam.html,
    /// Pulse). The held hammer reads its pose from the owner's FxKind.HammerThrow effect (DrawStormHammerHeld, called from DrawHeld
    /// in CharacterModels.cs): it rises over the shoulder and swells, snaps forward and is gone from the hand as the projectile
    /// leaves, then re-forms; for the slam it is raised, driven into the floor ahead and left there before re-forming. The thrown
    /// hammer is Thunderhead (DrawStormHammer, WorldShots.cs: a big steel hammer with a storm cloud trail and forks to the floor).
    /// The slam's shockwave is Pulse (FxKind.HammerSlam: a bright ring crawling with forks over a disc of static, a flash, a crater
    /// and cracks that cool), drawn in two passes: DrawHammerSlamFx (solid) and DrawHammerGlow (additive).
    public partial class ArenaApp
    {
        static readonly Rgba HammerBlue = new Rgba(0.49f, 0.75f, 1f), HammerHot = new Rgba(0.91f, 0.96f, 1f);

        struct HammerPoseData { public float Ang, Scale, Vis, Charge; public int Mode; public float Age; }

        /// The held hammer's pose: from its owner's live HammerThrow effect, or at rest.
        HammerPoseData HammerPoseOf(Player p)
        {
            var q = new HammerPoseData { Ang = 0.25f, Scale = 1.35f, Vis = 1f, Charge = 0.12f + 0.06f * Mathf.Sin(mT * 9f), Mode = -1 };
            Effect fx = null;
            foreach (var e in game.Effects)
                if (e.Kind == FxKind.HammerThrow && (int)e.Radius == p.Id && (fx == null || e.T < fx.T))
                    fx = e;
            if (fx == null)
                return q;
            float a = fx.T;
            q.Age = a;
            q.Mode = (int)fx.Pos2.X;
            q.Charge = 0.12f;
            if (q.Mode == 0)  // a throw
            {
                if (a < Game.HammerThrowRaise)
                {
                    float k = EaseOut3(a / Game.HammerThrowRaise);
                    q.Ang = Mathf.Lerp(0.25f, 2.7f, k); q.Scale = Mathf.Lerp(1.35f, 2f, k); q.Charge = 0.2f + 0.8f * k;
                }
                else if (a < Game.HammerThrowRelease)
                {
                    float u = Mathf.Clamp01((a - Game.HammerThrowRaise) / (Game.HammerThrowRelease - Game.HammerThrowRaise));
                    q.Ang = Mathf.Lerp(2.7f, 0f, u * u); q.Scale = 2f; q.Charge = 1f;
                }
                else if (a < Game.HammerThrowHide)
                    q.Vis = 0f;
                else if (a < Game.HammerThrowBack)
                    q.Vis = EaseOut3((a - Game.HammerThrowHide) / (Game.HammerThrowBack - Game.HammerThrowHide));
            }
            else  // a slam
            {
                if (a < Game.HammerSlamRaise)
                {
                    float k = EaseOut3(a / Game.HammerSlamRaise);
                    q.Ang = Mathf.Lerp(0.25f, 2.9f, k); q.Scale = Mathf.Lerp(1.35f, 2.2f, k); q.Charge = 0.2f + 0.8f * k;
                }
                else if (a < Game.HammerSlamWind)
                {
                    float u = Mathf.Clamp01((a - Game.HammerSlamRaise) / (Game.HammerSlamWind - Game.HammerSlamRaise));
                    q.Ang = Mathf.Lerp(2.9f, 0f, u * u * u); q.Scale = 2.2f; q.Charge = 1f;
                }
                else if (a < Game.HammerSlamHold)
                {
                    q.Ang = 0f; q.Scale = 2.2f; q.Charge = 0.15f + 0.65f * (1f - Mathf.Clamp01((a - Game.HammerSlamWind) / 0.9f));
                }
                else if (a < Game.HammerSlamBack)
                    q.Vis = EaseOut3((a - Game.HammerSlamHold) / (Game.HammerSlamBack - Game.HammerSlamHold));
            }
            return q;
        }

        // A point of the hammer's own frame (the haft runs -20..16, the head sits at +16, 24 across), rotated and scaled.
        static void HamPt(float ang, float k, float x, float y, out float ox, out float oy)
        {
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            ox = (x * c - y * s) * k; oy = (x * s + y * c) * k;
        }

        void HamRect(Rgba col, float ang, float k, float x0, float y0, float x1, float y1)
        {
            float ax, ay, bx, by, cx, cy, dx, dy;
            HamPt(ang, k, x0, y0, out ax, out ay); HamPt(ang, k, x1, y0, out bx, out by);
            HamPt(ang, k, x1, y1, out cx, out cy); HamPt(ang, k, x0, y1, out dx, out dy);
            MPoly(col, ax, ay, bx, by, cx, cy, dx, dy);
        }

        void HamLine(Rgba col, float ang, float k, float x0, float y0, float x1, float y1, float w)
        {
            float ax, ay, bx, by;
            HamPt(ang, k, x0, y0, out ax, out ay); HamPt(ang, k, x1, y1, out bx, out by);
            MLine(ax, ay, bx, by, w * k, col);
        }

        /// The Storm Hammer in the hand (grip at the frame's origin): the plain model with Visual effects off, otherwise the big
        /// Thunderhead hammer in whatever pose the throw or slam has it in.
        void DrawStormHammerHeld(Player p, float t)
        {
            if (!fxOn)
            {
                MLine(-6f, 0f, 22f, 0f, 3f, MWood); MRect(20f, -8f, 12f, 16f, Hx(0x7d8796)); MRect(26f, -8f, 3f, 16f, Hx(0xffe04a));
                return;
            }
            var q = HammerPoseOf(p);
            if (q.Vis < 0.02f)
                return;
            float keep = mAlpha;
            mAlpha *= q.Vis;
            float ang = q.Ang, k = q.Scale, ch = q.Charge;
            float hx, hy;
            HamPt(ang, k, 16f, 0f, out hx, out hy);
            if (ch > 0.15f)
                MCirc(hx, hy, 17f * k * 0.7f, HammerBlue.WithAlpha(0.16f * ch), 14);
            HamLine(Hx(0x4b3524), ang, k, -20f, 0f, 16f, 0f, 4.4f);                      // the haft
            HamLine(Hx(0x7a5a3c), ang, k, -20f, -0.4f, 16f, -0.4f, 2.4f);
            for (int i = 0; i < 4; i++) HamLine(Hx(0x2c2118), ang, k, -18f + i * 3f, -2.2f, -16.5f + i * 3f, 2.2f, 1f);  // grip wrap
            HamRect(Hx(0x3a4352), ang, k, 9f, -12f, 23f, 12f);                            // the head
            HamRect(Hx(0x707b90), ang, k, 9f, -12f, 23f, -4f);
            HamRect(Hx(0xa6b2c6), ang, k, 9f, -12f, 23f, -9.8f);
            HamRect(Hx(0x2a313d), ang, k, 7.5f, -13f, 10.5f, 13f);
            HamRect(Hx(0x2a313d), ang, k, 21.5f, -13f, 24.5f, 13f);
            HamRect(HammerBlue.WithAlpha(0.55f + 0.4f * ch), ang, k, 14f, -12f, 18f, 12f);  // the rune band
            HamRect(HammerHot.WithAlpha(0.55f + 0.4f * ch), ang, k, 15.4f, -12f, 16.6f, 12f);
            int n = 1 + Mathf.RoundToInt(ch * 3f), ft = Mathf.FloorToInt(t * 22f);        // sparks crawling over the head
            for (int i = 0; i < n; i++)
            {
                float sd = ft * 1.7f + i * 5.3f, side = Rand(sd) < 0.5f ? -1f : 1f;
                float sx, sy;
                HamPt(ang, k, 16f + (Rand(sd + 1f) - 0.5f) * 10f, side * 12f, out sx, out sy);
                float a2 = Rand(sd + 2f) * MathX.Tau, r2 = (8f + Rand(sd + 3f) * 12f) * k * 0.6f;
                MLine(sx, sy, sx + Mathf.Cos(a2) * r2, sy + Mathf.Sin(a2) * r2, 1.2f, HammerHot.WithAlpha(0.5f + 0.5f * ch));
            }
            if (q.Mode >= 0 && q.Vis < 1f)  // re-forming: sparks pulling in to the hand
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathX.Tau / 8f + Rand(i) * 1.2f, d = (1f - q.Vis) * (30f + Rand(i + 9f) * 20f);
                    MRect(Mathf.Cos(a) * d + 14f - 1.2f, Mathf.Sin(a) * d - 1.2f, 2.4f, 2.4f, HammerHot.WithAlpha(1f - q.Vis));
                }
            mAlpha = keep;
        }

        static float Rand(float s)
        {
            float v = Mathf.Sin(s * 127.1f + 311.7f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        // --- The thrown hammer: Thunderhead ------------------------------------------------------------------------

        V2 HamU(V2 o, V2 dir, V2 side, float k, float x, float y) => o + dir * (x * k) + side * (y * k);

        /// The thrown Storm Hammer: a big steel hammer (48 px across the head) tumbling end over end, a storm cloud dragging behind it
        /// and forks jumping to the floor, its head crackling.
        void DrawStormHammerThunder(Bullet b, Rgba c, float t)
        {
            float k = 2f, spin = -(t * 11f + b.Id * 1.3f);
            V2 dir = V2.FromAngle(spin), side = dir.Orthogonal(), o = b.Pos - dir * 2f * k;
            V2 back = b.Vel.Length > 1f ? -b.Vel.Normalized() : new V2(-1f, 0f), across = back.Orthogonal();
            Circle(b.Pos + new V2(5f, 9f), 22f, new Rgba(0f, 0f, 0f, 0.26f), 16);       // its shadow on the floor
            if (fxOn)
            {
                for (int i = 0; i < 9; i++)                                              // the storm it drags
                {
                    float sz = 13f + i * 2.4f + Rand(b.Id + i) * 6f;
                    V2 at = b.Pos + back * (10f + i * 13f) + across * (Rand(b.Id * 3f + i * 1.7f) - 0.5f) * 16f;
                    Circle(at, sz, new Rgba(0.27f, 0.35f, 0.59f, 0.3f * (1f - i / 9f)), 14);
                }
                int fn = Mathf.FloorToInt(t * 14f);
                for (int i = 0; i < 2; i++)                                              // forks off to the floor
                {
                    float sd = fn * 5f + i + b.Id, off = (Rand(sd) - 0.5f) * 90f;
                    DrawBolt(b.Pos + back * 4f, b.Pos + back * (34f + Rand(sd + 1f) * 40f) + across * (off + (off < 0f ? -24f : 24f)), 1.6f, c.Mix(HammerHot, 0.5f), (int)sd, 5);
                }
            }
            Circle(b.Pos, 44f, HammerBlue.WithAlpha(0.14f + 0.06f * Mathf.Sin(t * 40f + b.Id)), 22);  // its charge
            Line(HamU(o, dir, side, k, -20f, 0f), HamU(o, dir, side, k, 16f, 0f), 4.4f * k, new Rgba(0.29f, 0.21f, 0.14f));
            Line(HamU(o, dir, side, k, -20f, -0.4f), HamU(o, dir, side, k, 16f, -0.4f), 2.4f * k, new Rgba(0.48f, 0.35f, 0.24f));
            SetColor(new Rgba(0.23f, 0.26f, 0.32f));
            Quad(HamU(o, dir, side, k, 9f, -12f), HamU(o, dir, side, k, 23f, -12f), HamU(o, dir, side, k, 23f, 12f), HamU(o, dir, side, k, 9f, 12f));
            SetColor(new Rgba(0.44f, 0.48f, 0.56f));
            Quad(HamU(o, dir, side, k, 9f, -12f), HamU(o, dir, side, k, 23f, -12f), HamU(o, dir, side, k, 23f, -4f), HamU(o, dir, side, k, 9f, -4f));
            SetColor(new Rgba(0.16f, 0.19f, 0.24f));
            Quad(HamU(o, dir, side, k, 7.5f, -13f), HamU(o, dir, side, k, 10.5f, -13f), HamU(o, dir, side, k, 10.5f, 13f), HamU(o, dir, side, k, 7.5f, 13f));
            Quad(HamU(o, dir, side, k, 21.5f, -13f), HamU(o, dir, side, k, 24.5f, -13f), HamU(o, dir, side, k, 24.5f, 13f), HamU(o, dir, side, k, 21.5f, 13f));
            SetColor(HammerBlue.WithAlpha(0.95f));
            Quad(HamU(o, dir, side, k, 14f, -12f), HamU(o, dir, side, k, 18f, -12f), HamU(o, dir, side, k, 18f, 12f), HamU(o, dir, side, k, 14f, 12f));
            Line(HamU(o, dir, side, k, 16f, -12f), HamU(o, dir, side, k, 16f, 12f), 1.4f * k, HammerHot);
            V2 head = HamU(o, dir, side, k, 16f, 0f);
            for (int i = 0; i < 3; i++)                                                  // sparks off the head
            {
                float a = t * 23f + i * 2.1f + b.Id, sgn = i % 2 == 0 ? 1f : -1f;
                V2 s0 = head + side * (sgn * 12f * k) + dir * ((Rand(a) - 0.5f) * 10f * k);
                V2 s1 = s0 + V2.FromAngle(a) * 9f, s2 = s1 + V2.FromAngle(a + 1.2f) * 7f;
                Line(s0, s1, 1.4f, HammerHot.WithAlpha(0.9f));
                Line(s1, s2, 1.4f, HammerBlue.WithAlpha(0.9f));
            }
        }

        // --- Pulse: the slam's shockwave ------------------------------------------------------------------------

        /// FxKind.HammerSlam's solid layer: the crater, the cracks that cool from white to dark, flying chips.
        void DrawHammerSlamFx(Effect fx)
        {
            float a = fx.T, R = fx.Radius, cool = Mathf.Clamp01(a / 2.2f), life = 1f - Mathf.Clamp01((a - 1.9f) / 0.5f);
            V2 c = fx.Pos;
            Circle(c, 38f, new Rgba(0.03f, 0.04f, 0.07f, (0.65f - 0.3f * cool) * life), 24);
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                float an = i * MathX.Tau / n + Rand(i) * 0.5f, len = (60f + Rand(i + 4f) * 90f) * EaseOut3(Mathf.Clamp01(a / 0.2f));
                V2 prev = c;
                for (int s = 1; s <= 4; s++)
                {
                    float aa = an + (Rand(i * 9f + s) - 0.5f) * 0.5f;
                    V2 pt = c + V2.FromAngle(aa) * (len * s / 4f);
                    Line(prev, pt, 2.4f, (cool < 0.4f ? HammerHot : HammerBlue).WithAlpha(((1f - cool) * 0.9f + 0.1f) * life));
                    prev = pt;
                }
            }
            if (!fxOn)
                return;
            for (int d = 0; d < 14 && a < 0.9f; d++)                                      // chips thrown up
            {
                float da = Rand(d) * MathX.Tau, sp = 70f + Rand(d + 3f) * 150f;
                V2 at = c + new V2(Mathf.Cos(da) * sp * a, Mathf.Sin(da) * sp * a * 0.6f - (190f * a - 250f * a * a) * 0.6f);
                Square(at, 4f, (Rand(d + 9f) < 0.5f ? HammerHot : HammerBlue).WithAlpha(1f - a / 0.9f));
            }
        }

        /// The additive layer of every Storm Hammer effect: the slam's flash, ring and forks; and the throw's release flash and
        /// the slam's wind-up (a dashed ring where the wave will reach, sparks drawing in to the crater spot).
        void DrawHammerGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.HammerSlam)
                {
                    float a = fx.T, R = fx.Radius;
                    V2 c = fx.Pos;
                    float f = Mathf.Max(0f, 1f - a / 0.22f);
                    GlowDisc(c, 150f * f + 26f, HammerBlue, 0.9f * f);
                    GlowDisc(c, 40f * f + 12f, HammerHot, f);
                    float wr = R * EaseOut3(Mathf.Clamp01(a / 0.5f)), wa = Mathf.Max(0f, 1f - a / 0.62f);
                    if (wa <= 0f || wr < 2f)
                        continue;
                    GlowDisc(c, wr * 0.9f, HammerBlue, 0.1f * wa);
                    Arc(c, wr, 10f * wa + 2f, HammerBlue.WithAlpha(0.3f * wa), 72);
                    Arc(c, wr, 6f * wa + 1.5f, HammerBlue.WithAlpha(0.95f * wa), 72);
                    Arc(c, wr * 0.97f, 2.4f, HammerHot.WithAlpha(wa), 72);
                    Arc(c, wr * 0.6f, 3f * wa, HammerBlue.WithAlpha(0.5f * wa), 56);
                    if (a < 0.7f && fxOn)  // forks crawling along the edge, in and out
                    {
                        int fn = Mathf.FloorToInt(a * 26f), cnt = 26;
                        for (int m = 0; m < cnt; m++)
                        {
                            float sd = fn * 31f + m, an = m * MathX.Tau / cnt + Rand(sd) * 0.2f, dir = Rand(sd + 3f) < 0.6f ? 1f : -1f, len = 12f + Rand(sd + 2f) * 22f;
                            V2 p0 = c + V2.FromAngle(an) * (wr + (Rand(sd + 1f) - 0.5f) * 10f);
                            DrawBolt(p0, p0 + V2.FromAngle(an + 0.05f * dir) * (dir * len), 1.2f, HammerBlue.WithAlpha(wa), (int)sd, 2);
                        }
                    }
                }
                else if (fx.Kind == FxKind.HammerThrow)
                {
                    Player p;
                    if (!game.Players.TryGetValue((int)fx.Radius, out p))
                        continue;
                    float a = fx.T;
                    V2 dir = V2.FromAngle(p.Aim);
                    if (fx.Pos2.X < 0.5f)  // the release: a flash and a ring of sparks at the hand
                    {
                        float f = 1f - (a - Game.HammerThrowRelease) / 0.2f;
                        if (f > 0f && a >= Game.HammerThrowRelease)
                        {
                            V2 hand = p.Disp + dir * 44f;
                            GlowDisc(hand, 70f * f + 18f, HammerBlue, 0.8f * f);
                            for (int m = 0; m < 8 && fxOn; m++)
                                Square(hand + V2.FromAngle(p.Aim - 0.8f + m * 0.23f) * ((1f - f) * 56f + 8f), 3f, HammerHot.WithAlpha(f));
                        }
                    }
                    else if (a < Game.HammerSlamWind)  // the slam's wind-up: where the wave will reach, and sparks drawn in to the crater spot
                    {
                        float wk = Mathf.Clamp01(a / Game.HammerSlamRaise);
                        V2 at = p.Disp + dir * Game.HammerSlamReach;
                        float R = Game.HammerSlamRadius;
                        for (int i = 0; i < 28; i++)
                        {
                            float a0 = i * MathX.Tau / 28f;
                            Arc(at, R, 1.5f, HammerBlue.WithAlpha(0.1f + 0.22f * wk), 4, a0, MathX.Tau / 56f);
                        }
                        Arc(at, 28f, 1.5f, HammerBlue.WithAlpha(0.22f + 0.4f * wk), 24);
                        for (int m = 0; m < 12 && fxOn; m++)
                        {
                            float ma = m * MathX.Tau / 12f + a * 1.5f, md = (1f - ((a * 1.3f + m / 12f) % 1f)) * 70f * (0.4f + 0.6f * wk) + 14f;
                            Square(at + new V2(Mathf.Cos(ma) * md, Mathf.Sin(ma) * md * 0.9f), 3f, HammerHot.WithAlpha(0.5f * wk));
                        }
                    }
                }
            }
        }
    }
}
