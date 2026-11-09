using UnityEngine;

namespace Arena
{
    /// Bone Phalanx (Core/BonePhalanx.cs), ported from the drawing code of Docs/Mockups/bone-phalanx.html
    /// (https://claude.ai/artifact/4c2gZtdFuRmaJzVnD7epfY). Everything is drawn from the zone's age, its Marched and its
    /// Bones bytes, plus three effects:
    ///   the gate     a jagged soul-green slit across the aim with pairs of rib bones curving off it, rising from the
    ///                middle out, dust, a scorched floor; it fades once the line has marched off;
    ///   the line     each skeleton steps up out of the slit (middle first) in a soul-green flash, then walks with the
    ///                line (feet stride by the distance marched): shadow, feet, pelvis, ribs, collar bone, skull with
    ///                soul fire in the eyes, a spear on its right and a convex bone tower shield with ribs, a boss and a
    ///                lit edge; cracks at two thirds and one third, a chipped corner and flickering eyes near the end;
    ///   PhalanxJab   the spear thrusts 9 px with a pale streak (read by the skeleton it belongs to);
    ///   PhalanxSpark a flash and a fan of sparks off the shield face, bone chips falling;
    ///   PhalanxBreak the flash (when broken), a dust ring, the shield toppling forward, the skull rolling off, bones
    ///                scattering into a pile that fades, and a last wisp of soul; for the crumble the skeleton stands
    ///                until its turn comes (from the ends in).
    /// With Visual effects off: a disc, a bar and a stick per skeleton, the shield darkening with its health, a plain line
    /// for the gate and a ring where one falls.
    public partial class ArenaApp
    {
        static readonly Rgba BoneWhite = C255(232, 228, 212), BoneMid = C255(207, 200, 178), BoneDark = C255(138, 131, 112), BoneRim = C255(79, 73, 60),
            BoneShield = C255(221, 214, 193), BoneShield2 = C255(185, 177, 154), BoneCrack = C255(59, 53, 42), BoneShaft = C255(109, 98, 80),
            BoneShaftLit = C255(155, 143, 116), BoneDust = C255(150, 140, 118), BoneSoul = C255(140, 255, 190), BoneSocket = C255(12, 15, 13);
        const float BoneJabTime = 0.28f;

        /// A frame for drawing one skeleton: x along `ang`, y across it, scaled by s.
        struct BoneFrame
        {
            public V2 O, U, V;
            public float S;
            public BoneFrame(V2 o, float ang, float s) { O = o; U = V2.FromAngle(ang); V = new V2(-U.Y, U.X); S = s; }
            public V2 P(float x, float y) => O + U * (x * S) + V * (y * S);
        }

        void BoneCurve(V2 a, V2 c, V2 b, float w, Rgba col)
        {
            V2 prev = a;
            for (int i = 1; i <= 5; i++)
            {
                float f = i / 5f, g = 1f - f;
                V2 next = a * (g * g) + c * (2f * g * f) + b * (f * f);
                Line(prev, next, w, col);
                prev = next;
            }
        }

        void BoneEllipse(BoneFrame f, float x, float y, float rx, float ry, Rgba col) =>
            Ellipse(f.P(x, y), rx * f.S, ry * f.S, f.U.Angle, col, 12);

        /// A convex shape in the frame, filled as a fan from (cx, 0).
        void BoneFan(BoneFrame f, float[] pts, float cx, Rgba fill)
        {
            SetColor(fill);
            V2 c = f.P(cx, 0f);
            int n = pts.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Tri(c, f.P(pts[i * 2], pts[i * 2 + 1]), f.P(pts[j * 2], pts[j * 2 + 1]));
            }
        }

        void BoneOutline(BoneFrame f, float[] pts, float w, Rgba col)
        {
            int n = pts.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Line(f.P(pts[i * 2], pts[i * 2 + 1]), f.P(pts[j * 2], pts[j * 2 + 1]), w, col);
            }
        }

        static readonly float[] ShieldWhole = { 0f, -12.4f, 4.5f, -12f, 7f, -6f, 7.6f, 0f, 7f, 6f, 4.5f, 12f, 0f, 12.4f, -1.2f, 0f };
        static readonly float[] ShieldChipped = { 0f, -12.4f, 2f, -12.2f, 6f, -8.2f, 7f, -6f, 7.6f, 0f, 7f, 6f, 4.5f, 12f, 0f, 12.4f, -1.2f, 0f };

        /// One skeleton facing `ang`, its shield's face line at `at` (the body behind). hp: 0..1; walk: stride phase;
        /// jab: seconds since its last spear thrust (or -1); a: alpha; s: size (it grows as it steps out).
        void DrawPhalanxSkeleton(V2 at, float ang, float hp, float walk, float jab, float a, float s, float seed)
        {
            var f = new BoneFrame(at, ang, s);
            float keep = glAlpha;
            glAlpha = keep * a;
            float th = jab >= 0f && jab < BoneJabTime ? (jab < 0.07f ? KEo(jab / 0.07f) : 1f - KEo((jab - 0.07f) / 0.21f)) : 0f, sx = th * 9f;
            if (!fxOn)  // plain: a disc, a stick, a bar that darkens as it wears
            {
                Circle(f.P(-8f, 0f), 7f * s, BoneMid, 12);
                Line(f.P(-14f, 7f), f.P(14f + sx, 7f), 2f, BoneDark);
                Rgba sh = BoneRim.Mix(BoneShield, 0.35f + 0.65f * hp);
                SetColor(sh);
                Quad(f.P(0f, -12f), f.P(6f, -12f), f.P(6f, 12f), f.P(0f, 12f));
                BoneOutline(f, new[] { 0f, -12f, 6f, -12f, 6f, 12f, 0f, 12f }, 1.5f, BoneRim);
                glAlpha = keep;
                return;
            }
            float st = Mathf.Sin(walk), bob = Mathf.Abs(Mathf.Cos(walk)) * 1.2f;
            BoneEllipse(f, -6f, 3f, 13f, 11f, new Rgba(0f, 0f, 0f, 0.38f));
            BoneEllipse(f, -8f + st * 4f, -5f, 3.2f, 2.2f, BoneDark);  // feet
            BoneEllipse(f, -8f - st * 4f, 5f, 3.2f, 2.2f, BoneDark);
            BoneEllipse(f, -15f, 0f, 3.5f, 5.5f, BoneMid);  // pelvis and spine
            Line(f.P(-15f, 0f), f.P(-8f, 0f), 2.4f * s, BoneDark);
            for (int k = 0; k < 3; k++)  // ribs
            {
                float rx = -12f + k * 2.4f;
                BoneCurve(f.P(rx, 0f), f.P(rx - 1f, -7f + k), f.P(rx - 4f, -7.5f + k), 1.6f * s, BoneMid);
                BoneCurve(f.P(rx, 0f), f.P(rx - 1f, 7f - k), f.P(rx - 4f, 7.5f - k), 1.6f * s, BoneMid);
            }
            Line(f.P(-7f, -9f), f.P(-7f, 9f), 2.6f * s, BoneWhite);  // collar bone, then the arms to the shield and the spear
            Line(f.P(-7f, -9f), f.P(-1f, -7f), 2f * s, BoneMid);
            Line(f.P(-7f, 9f), f.P(-2f, 8f), 2f * s, BoneMid);
            Line(f.P(-17f + sx, 7f), f.P(15f + sx, 7f), 2.4f * s, BoneShaft);  // the spear
            Line(f.P(-17f + sx, 7f), f.P(15f + sx, 7f), 1.2f * s, BoneShaftLit);
            SetColor(BoneWhite);
            Tri(f.P(15f + sx, 4.6f), f.P(24f + sx, 7f), f.P(15f + sx, 9.4f));
            BoneOutline(f, new[] { 15f + sx, 4.6f, 24f + sx, 7f, 15f + sx, 9.4f }, 1f, BoneRim);
            if (th > 0.4f)
                GlowLine(f.P(17f + sx, 7f), f.P(30f + sx, 7f), 2.5f, new Rgba(1f, 0.98f, 0.9f, 0.6f * th));
            // the skull, soul fire in its eyes (flickering near the end)
            V2 skull = f.P(-7f + bob * 0.4f, 0f);
            Circle(skull, 5.6f * s, BoneWhite, 14);
            Arc(skull, 5.6f * s, 1.3f, BoneRim, 14);
            BoneEllipse(f, -3.6f, 0f, 2f, 3.4f, BoneMid);
            float flick = hp < 0.33f ? 0.45f + 0.55f * KHash(Mathf.Floor(Time.unscaledTime * 11f) + seed) : 1f;
            for (int e = -1; e <= 1; e += 2)
            {
                Circle(f.P(-4.4f, e * 2.1f), 1.3f * s, BoneSocket, 8);
                GlowDisc(f.P(-4.2f, e * 2.1f), 3.6f * s, BoneSoul, 0.8f * flick);
            }
            // the tower shield: convex front, ribs across it, a dark rim, a lit edge, cracks as it wears
            var pts = hp < 0.34f ? ShieldChipped : ShieldWhole;
            BoneFan(f, pts, 3f, BoneShield);
            SetColor(BoneShield2);
            Quad(f.P(-1f, -11.5f), f.P(1f, -11.5f), f.P(1f, 11.5f), f.P(-1f, 11.5f));
            for (float r = -10f; r <= 10f; r += 4f)
                Line(f.P(0.6f, r), f.P(6.4f, r + 0.6f), 1f, new Rgba(0.43f, 0.39f, 0.31f, 0.55f));
            Line(f.P(6.2f, -9.5f), f.P(6.2f, 9.5f), 1.2f, new Rgba(1f, 1f, 1f, 0.35f));
            if (hp < 0.67f)
            {
                Line(f.P(7.4f, -3f), f.P(4.2f, -1.5f), 1.1f, BoneCrack);
                Line(f.P(4.2f, -1.5f), f.P(5f, 1.2f), 1.1f, BoneCrack);
                Line(f.P(5f, 1.2f), f.P(1.6f, 3.4f), 1.1f, BoneCrack);
            }
            if (hp < 0.34f)
            {
                Line(f.P(7f, 5f), f.P(3.4f, 7.6f), 1.1f, BoneCrack);
                Line(f.P(3.4f, 7.6f), f.P(4.2f, 10f), 1.1f, BoneCrack);
                Line(f.P(4.2f, 10f), f.P(0.6f, 11.2f), 1.1f, BoneCrack);
                Line(f.P(3.4f, 7.6f), f.P(1.2f, 5.8f), 1.1f, BoneCrack);
            }
            BoneOutline(f, pts, 1.6f, BoneRim);
            Circle(f.P(4.4f, 0f), 2.1f * s, BoneWhite, 8);
            Arc(f.P(4.4f, 0f), 2.1f * s, 1f, BoneRim, 8);
            glAlpha = keep;
        }

        /// The gate: a soul-green slit across the aim, rib bones curving off it, dust; it fades once the line has gone.
        void DrawPhalanxGate(Zone z, float age)
        {
            int n = Game.PhalanxSkeletons(z);
            float marchAt = Game.PhalanxMarchAt(n), half = n * Game.PhalanxSpacing / 2f + 14f;
            float open = KEo(age / Game.PhalanxGate), close = Mathf.Clamp01((age - (marchAt + 0.25f)) / 0.7f), a = 1f - close;
            if (Game.PhalanxCrumbling(z))
                a *= Mathf.Clamp01(z.T / Game.PhalanxCrumble);
            if (a <= 0.01f)
                return;
            float crack = Mathf.Clamp01(age / 0.25f);
            var f = new BoneFrame(Game.PhalanxGatePos(z), z.Aim, 1f);
            if (!fxOn)
            {
                Line(f.P(0f, -half * crack), f.P(0f, half * crack), 3f, BoneWhite.WithAlpha(0.7f * a));
                return;
            }
            float keep = glAlpha;
            glAlpha = keep * a;
            Ellipse(f.O, 26f * open + 4f, half * crack + 8f, z.Aim, new Rgba(0f, 0f, 0f, 0.35f), 20);  // scorched floor
            float gw = 9f * open * (1f - close * 0.5f);
            if (gw > 0.2f)  // the soul light in the slit, falling off to both sides
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    V2 a0 = f.P(0f, -half * crack), a1 = f.P(0f, half * crack), b0 = f.P(sd * gw * 3f, -half * crack), b1 = f.P(sd * gw * 3f, half * crack);
                    var core = BoneSoul.WithAlpha(0.55f * open);
                    var rim = BoneSoul.WithAlpha(0f);
                    VTri(a0, core, a1, core, b1, rim);
                    VTri(a0, core, b1, rim, b0, rim);
                }
            const int steps = 24;  // the slit, jagged
            for (int k = 0; k < steps; k++)
            {
                float y0 = -half * crack + k / (float)steps * half * crack * 2f, y1 = -half * crack + (k + 1) / (float)steps * half * crack * 2f;
                float j0 = (KHash(k * 3.1f) - 0.5f) * 5f, j1 = (KHash((k + 1) * 3.1f) - 0.5f) * 5f;
                SetColor(C255(5, 8, 7));
                Quad(f.P(j0 - gw * 0.5f, y0), f.P(j1 - gw * 0.5f, y1), f.P(j1 + gw * 0.5f, y1), f.P(j0 + gw * 0.5f, y0));
                Line(f.P(j0 - gw * 0.5f, y0), f.P(j1 - gw * 0.5f, y1), 1.5f, BoneSoul.WithAlpha(0.8f * open));
                Line(f.P(j0 + gw * 0.5f, y0), f.P(j1 + gw * 0.5f, y1), 1.5f, BoneSoul.WithAlpha(0.8f * open));
            }
            for (int r = 0; r <= n; r++)  // ribs off the seam, rising from the middle out
            {
                float yy = (r - n / 2f) * Game.PhalanxSpacing, rise = KEo((age - 0.08f - Mathf.Abs(r - n / 2f) * 0.035f) / 0.3f);
                if (rise <= 0f)
                    continue;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float L = (15f + KHash(r + sd) * 5f) * rise;
                    V2 p0 = f.P(sd * gw * 0.6f, yy), c = f.P(sd * (gw * 0.6f + L * 0.7f), yy - 6f), p1 = f.P(sd * (gw * 0.6f + L), yy + 4f);
                    BoneCurve(p0, c, p1, 3.2f, BoneDark);
                    BoneCurve(p0, c, p1, 1.8f, BoneWhite);
                }
            }
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Circle(f.P(0f, sd * half * crack), 3.5f, BoneWhite, 10);
                Arc(f.P(0f, sd * half * crack), 3.5f, 1.2f, BoneRim, 10);
            }
            if (age < 1.2f)
                for (int sd = -1; sd <= 1; sd += 2)
                    KPuffs(f.O, age, 8, sd * 7f + z.Id, (f.U * sd).Angle, 2.4f, 60f, 0.8f, BoneDust, 10f, 0.35f);
            glAlpha = keep;
        }

        /// The zone (under the enemies): the gate, then every standing skeleton.
        void DrawPhalanx(Zone z, float t)
        {
            float age = Game.PhalanxAge(z);
            DrawPhalanxGate(z, age);
            int n = Game.PhalanxSkeletons(z);
            if (z.Bones == null)
                return;
            for (int i = 0; i < n; i++)
            {
                if (z.Bones[i] == 0)
                    continue;  // broken or crumbling: its PhalanxBreak effect draws it now
                float a = (age - Game.PhalanxOutAt(i, n)) / Game.PhalanxStepTime;
                if (a <= 0f)
                    continue;
                float e = KEo(a), lane = Game.PhalanxLane(i, n);
                V2 at = Game.PhalanxWorld(z, -14f * (1f - e), lane);
                DrawPhalanxSkeleton(at, z.Aim, z.Bones[i] / 255f, z.Marched / 9f + i * 1.3f, PhalanxJabAge(z, i), Mathf.Clamp01(a * 2.5f), 0.75f + 0.25f * e, z.Id * 13f + i);
                if (fxOn && a < 1f)
                    GlowDisc(Game.PhalanxWorld(z, 4f, lane), 14f * (1f - a), BoneSoul, 0.5f * (1f - a));
            }
        }

        /// Seconds since skeleton i's last spear thrust (the PhalanxJab effect sent from its spear's butt), or -1.
        float PhalanxJabAge(Zone z, int i)
        {
            V2 butt = Game.PhalanxWorld(z, -10f, Game.PhalanxLane(i, Game.PhalanxSkeletons(z)) + 6f);
            float best = -1f;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.PhalanxJab && fx.T < BoneJabTime && fx.Pos.DistanceTo(butt) < 10f && (best < 0f || fx.T < best))
                    best = fx.T;
            return best;
        }

        /// A shot stopped on a shield.
        void DrawPhalanxSparkFx(Effect fx)
        {
            float age = fx.T, f = age / 0.3f;
            if (age > 0.3f)
                return;
            if (!fxOn)
            {
                if (age < 0.12f)
                    Circle(fx.Pos, 4f, new Rgba(1f, 0.94f, 0.78f, 0.9f), 8);
                return;
            }
            GlowDisc(fx.Pos, 14f * (1f - f) + 3f, new Rgba(1f, 0.97f, 0.86f), 0.9f * (1f - f));
            KSparks(fx.Pos, age, 7, fx.Pos.X * 0.37f + fx.Pos.Y, fx.Pos2.Angle, 2.6f, 140f, 0.3f, new Rgba(1f, 0.93f, 0.75f), 1.4f);
            for (int k = 0; k < 3; k++)  // bone chips
            {
                V2 d = V2.FromAngle(fx.Pos2.Angle + (KHash(k + fx.Pos.Y) * 2f - 1f));
                V2 c = fx.Pos + d * (4f + 14f * KEo(f)) + new V2(0f, f * 6f);
                FillRect(c.X, c.Y, 2f, 2f, BoneMid.WithAlpha(1f - f));
            }
        }

        /// A skeleton falling apart (broken, or the crumble when its turn comes), then its pile of bones fading.
        void DrawPhalanxBreakFx(Effect fx)
        {
            bool crumble = fx.Radius >= 1f;
            float delay = crumble ? fx.Radius - 1f : 0f, age = fx.T - delay, ang = fx.Pos2.Angle;
            if (age < 0f)  // the crumble: it stands until its turn
            {
                DrawPhalanxSkeleton(fx.Pos + V2.FromAngle(ang) * 8f, ang, 0.8f, 0f, -1f, 1f, 1f, fx.Pos.X);
                return;
            }
            float life = fx.Dur - delay;
            if (age > life)
                return;
            if (!fxOn)
            {
                if (age < 0.4f)
                    Arc(fx.Pos, 8f + age * 30f, 2f, BoneWhite.WithAlpha(1f - age / 0.4f), 16);
                return;
            }
            float f = Mathf.Clamp01(age / 0.45f), fade = age > life - 0.9f ? Mathf.Clamp01((life - age) / 0.9f) : 1f, seed = fx.Pos.X * 0.13f + fx.Pos.Y * 0.07f;
            var bf = new BoneFrame(fx.Pos, ang, 1f);
            float keep = glAlpha;
            glAlpha = keep * fade;
            if (!crumble && age < 0.25f)
                GlowDisc(bf.P(6f, 0f), 26f * (1f - age / 0.25f) + 6f, new Rgba(1f, 0.98f, 0.88f), 0.75f);
            if (age < 0.6f)
                KPuffs(bf.P(-2f, 0f), age, 6, seed, 0f, MathX.Tau, 40f, 0.6f, BoneDust, 9f, 0.22f);
            for (int k = 0; k < 7; k++)  // the pile
            {
                float a = KHash(seed + k) * MathX.Tau, L = 6f + KHash(seed + k * 2f) * 5f, r = KEo(f) * (4f + KHash(seed + k * 3f) * 12f);
                float ba = a + f * (KHash(seed + k * 4f) - 0.5f) * 4f;
                V2 c = bf.P(Mathf.Cos(a) * r - 4f, Mathf.Sin(a) * r), d = V2.FromAngle(ang + ba) * (L / 2f);
                Line(c - d, c + d, 3f, BoneDark);
                Line(c - d, c + d, 1.6f, BoneWhite);
                Circle(c - d, 1.6f, BoneWhite, 6);
                Circle(c + d, 1.6f, BoneWhite, 6);
            }
            var shf = new BoneFrame(bf.P(6f + 8f * KEo(f), 0f), ang + f * (KHash(seed) - 0.5f) * 1.4f, 1f);  // the shield, toppled
            var plate = new[] { -3f, -12f, 3f, -12f, 3f, 12f, -3f, 12f };
            BoneFan(shf, plate, 0f, BoneShield2);
            BoneOutline(shf, plate, 1.4f, BoneRim);
            Line(shf.P(-2f, -4f), shf.P(2f, 3f), 1f, BoneCrack);
            V2 skull = bf.P(-6f + KEo(f) * (10f + KHash(seed + 9f) * 10f), (KHash(seed + 11f) - 0.5f) * 24f * KEo(f));  // rolled off
            Circle(skull, 5f, BoneWhite, 12);
            Arc(skull, 5f, 1.2f, BoneRim, 12);
            Circle(skull + bf.U * 2f - bf.V * 1.8f, 1.1f, BoneSocket, 6);
            Circle(skull + bf.U * 2f + bf.V * 1.8f, 1.1f, BoneSocket, 6);
            if (age < 1.2f)  // a last wisp of soul leaving
            {
                float w = age / 1.2f;
                GlowDisc(skull + new V2(-w * 8f, -w * 14f), 7f * (1f - w) + 2f, BoneSoul, 0.6f * (1f - w));
            }
            glAlpha = keep;
        }
    }
}
