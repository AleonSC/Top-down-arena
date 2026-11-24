using UnityEngine;

namespace Arena
{
    /// Lich body rework mockups (three directions, picked with LichLook; 0 keeps the old heptagon). Each faces its nearest
    /// player and takes its phase colour: The Crypt soul green, Soul Harvest spirit cyan, Undeath spectre violet (and see-
    /// through), the Final Rite rose. A: the Hooded Sorcerer (ragged cloak, skull in a hood, bone pauldrons, soul staff).
    /// B: the Crowned Skull (a huge floating cranium, a bone crown, two floating hands, a turning heptagram under it).
    /// C: the Wraith (a see-through shroud trailing tatters, an open ribcage with its soul gem, long clawed arms).
    public partial class ArenaApp
    {
        internal static int LichLook;  // 0 old, 1 A, 2 B, 3 C (preview: LICHLOOK=1..3)

        static readonly Rgba LRobe = new Rgba(0.07f, 0.1f, 0.09f), LRobeHi = new Rgba(0.13f, 0.19f, 0.16f), LSocket = new Rgba(0.02f, 0.03f, 0.03f),
            LGold = new Rgba(0.95f, 0.78f, 0.35f), LWood = new Rgba(0.22f, 0.17f, 0.12f);

        struct LichFrame
        {
            public V2 O, F, S;
            public float K;
            public V2 P(float x, float y) => O + F * (x * K) + S * (y * K);
        }

        static Rgba LichStageCol(Enemy e) => e.Lich == null ? SoulCol : e.Lich.Stage == LichStage.Harvest ? SpiritCol
            : e.Lich.Stage == LichStage.Undeath ? SpectreCol : e.Lich.Stage == LichStage.LastStand ? new Rgba(1f, 0.42f, 0.6f) : SoulCol;

        float LichFacing(V2 pos)
        {
            float best = 1e9f;
            V2 to = new V2(0f, 1f);
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(pos) < best)
                {
                    best = p.Disp.DistanceTo(pos);
                    to = p.Disp - pos;
                }
            return Mathf.Atan2(to.Y, to.X);
        }

        void DrawLichBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float a = col.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f;
            int stage = e.Lich == null ? 0 : (int)e.Lich.Stage;
            var f = new LichFrame { O = pos, F = V2.FromAngle(LichFacing(pos)), K = r / 36f };
            f.S = f.F.Orthogonal();
            Rgba glow = LichStageCol(e).WithAlpha(a), bone = BoneCol.Mix(Rgba.White, hit).WithAlpha(a);
            Ellipse(pos + new V2(0f, r * 0.35f), r * 1.05f, r * 0.55f, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 24);  // hovering shadow
            if (LichLook == 1)
                DrawLichSorcerer(f, stage, glow, bone, hit, a, t);
            else if (LichLook == 2)
                DrawLichSkull(f, stage, glow, bone, hit, a, t);
            else
                DrawLichWraith(f, stage, glow, bone, hit, a, t);
        }

        /// Glowing eyes: a soft bloom, a hot core.
        void LichEyes(V2 l, V2 rr, float s, Rgba glow, float a, float t)
        {
            float fl = 0.85f + 0.15f * Mathf.Sin(t * 9f);
            GlowDisc(l, s * 3.2f, glow, 0.55f * a * fl);
            GlowDisc(rr, s * 3.2f, glow, 0.55f * a * fl);
            Circle(l, s, Rgba.White.Mix(glow, 0.35f).WithAlpha(a), 10);
            Circle(rr, s, Rgba.White.Mix(glow, 0.35f).WithAlpha(a), 10);
        }

        // ---- A: the Hooded Sorcerer ----
        void DrawLichSorcerer(LichFrame f, int stage, Rgba glow, Rgba bone, float hit, float a, float t)
        {
            float K = f.K;
            Rgba robe = LRobe.Mix(Rgba.White, hit * 0.6f).WithAlpha(a), robeHi = LRobeHi.Mix(Rgba.White, hit * 0.6f).WithAlpha(a);
            GlowDisc(f.O, 58f * K, glow, 0.16f * a);
            // the cloak: a ragged hem, longer behind, its tatters stirring
            const int N = 22;
            var hem = new V2[N];
            for (int i = 0; i < N; i++)
            {
                float ang = MathX.Tau * i / N, back = Mathf.Max(0f, -Mathf.Cos(ang));
                float rad = 30f + 14f * back + (i % 2 == 0 ? 6f + 3f * Mathf.Sin(t * 4f + i) : -2f) * (0.4f + back);
                hem[i] = f.P(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.95f);
            }
            SetColor(robe);
            for (int i = 0; i < N; i++)
                Tri(f.O, hem[i], hem[(i + 1) % N]);
            for (int i = 0; i < N; i++)
                Line(hem[i], hem[(i + 1) % N], 1.6f * K, glow.WithAlpha(0.35f * a));
            // folds sweeping back from the shoulders
            for (int k = -2; k <= 2; k++)
                Line(f.P(-2f, k * 7f), f.P(-34f - 4f * Mathf.Abs(k), k * 12f), 2.2f * K, robeHi);
            // phase marks: soul runes glowing on the cloak (more each phase)
            for (int k = 0; k <= stage; k++)
            {
                V2 c = f.P(-22f - 4f * (k % 2), (k - stage / 2f) * 11f);
                Arc(c, 3.2f * K, 1.4f * K, glow.WithAlpha(0.8f * a), 10);
                Line(c - f.F * (3f * K), c + f.F * (3f * K), 1.2f * K, glow.WithAlpha(0.8f * a));
            }
            // the staff in its right hand, its soul orb out front
            V2 s0 = f.P(-18f, 24f), s1 = f.P(38f, 22f);
            Line(s0, s1, 3.6f * K, LWood.WithAlpha(a));
            Arc(s1, 6f * K, 1.6f * K, bone, 14, 0f, MathX.Tau);
            GlowDisc(s1, 20f * K, glow, 0.8f * a);
            Circle(s1, 4.6f * K, Rgba.White.Mix(glow, 0.5f).WithAlpha(a), 14);
            // bone pauldrons with spikes
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 sh = f.P(0f, sd * 17f);
                Circle(sh, 8.5f * K, bone.Mix(LRobe, 0.15f), 16);
                Arc(sh, 8.5f * K, 1.2f * K, new Rgba(0.4f, 0.38f, 0.33f, a), 16);
                for (int k = 0; k < 3; k++)
                {
                    V2 d = (f.S * sd * 0.9f + f.F * (k - 1) * 0.55f).Normalized();
                    SetColor(bone);
                    Tri(sh + d.Orthogonal() * (2.4f * K) + d * (6f * K), sh - d.Orthogonal() * (2.4f * K) + d * (6f * K), sh + d * (14f * K));
                }
            }
            // the left hand raised, a soul flame on its palm
            V2 lh = f.P(16f, -22f);
            for (int k = -2; k <= 2; k++)
                Line(lh, lh + (f.F * 1f + f.S * (k * 0.25f - 0.2f)).Normalized() * (5.5f * K), 1.4f * K, bone);
            Circle(lh, 3.2f * K, bone, 10);
            GlowDisc(lh + f.F * (5f * K), 12f * K, glow, (0.55f + 0.25f * Mathf.Sin(t * 13f)) * a);
            // the hood and the skull inside it
            V2 hd = f.P(6f, 0f);
            Circle(hd, 15f * K, robe, 24);
            Arc(hd, 15f * K, 1.6f * K, glow.WithAlpha(0.5f * a), 24);
            Ellipse(f.P(11f, 0f), 8.5f * K, 9f * K, Mathf.Atan2(f.F.Y, f.F.X), bone, 18);
            Circle(f.P(15.5f, -3.6f), 2.6f * K, LSocket.WithAlpha(a), 10);
            Circle(f.P(15.5f, 3.6f), 2.6f * K, LSocket.WithAlpha(a), 10);
            LichEyes(f.P(15.8f, -3.6f), f.P(15.8f, 3.6f), 1.2f * K, glow, a, t);
            // a circlet of bone spikes round the back of the hood
            for (int k = -3; k <= 3; k++)
            {
                V2 d = (f.F * -Mathf.Cos(k * 0.38f) + f.S * Mathf.Sin(k * 0.38f)).Normalized();
                V2 b = hd + d * (13f * K);
                SetColor(stage >= 3 ? LGold.WithAlpha(a) : bone);
                Tri(b + d.Orthogonal() * (2.2f * K), b - d.Orthogonal() * (2.2f * K), b + d * ((6f + (k == 0 ? 3f : 0f)) * K));
            }
            // soul motes circling it
            for (int k = 0; k < 3 + stage; k++)
            {
                float ang = t * 1.6f + MathX.Tau * k / (3 + stage);
                V2 m = f.O + V2.FromAngle(ang) * (50f * K);
                GlowDisc(m, 7f * K, glow, 0.6f * a);
                Circle(m, 1.8f * K, Rgba.White.WithAlpha(0.9f * a), 8);
            }
        }

        // ---- B: the Crowned Skull ----
        void DrawLichSkull(LichFrame f, int stage, Rgba glow, Rgba bone, float hit, float a, float t)
        {
            float K = f.K, spin = t * 0.5f;
            // the heptagram turning under it
            GlowDisc(f.O, 62f * K, glow, 0.14f * a);
            Arc(f.O, 50f * K, 1.6f * K, glow.WithAlpha(0.55f * a), 56);
            Arc(f.O, 44f * K, 1f * K, glow.WithAlpha(0.3f * a), 56);
            var pt = new V2[7];
            for (int i = 0; i < 7; i++)
                pt[i] = f.O + V2.FromAngle(spin + MathX.Tau * i / 7f) * (47f * K);
            for (int i = 0; i < 7; i++)
                Line(pt[i], pt[(i + 3) % 7], 1.4f * K, glow.WithAlpha(0.45f * a));
            for (int i = 0; i < 7; i++)
            {
                Circle(pt[i], 3f * K, LSocket.WithAlpha(a), 10);
                Arc(pt[i], 3f * K, 1.2f * K, glow.WithAlpha(0.9f * a), 10);
            }
            // the floating hands, fingers spread, a soul spark between them
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float bob = Mathf.Sin(t * 3f + sd) * 2f;
                V2 h = f.P(12f + bob, sd * 37f);
                Ellipse(h, 5f * K, 4f * K, Mathf.Atan2(f.F.Y, f.F.X), bone, 12);
                for (int k = 0; k < 4; k++)
                {
                    V2 d = (f.F * 1f + f.S * (-sd * 0.15f + (k - 1.5f) * 0.28f)).Normalized();
                    V2 j = h + d * (5f * K), tip = j + (d + f.S * (-sd * 0.35f)).Normalized() * (5f * K);
                    Line(h, j, 1.6f * K, bone);
                    Line(j, tip, 1.3f * K, bone);
                }
                GlowDisc(f.P(24f + bob, sd * 32f), 10f * K, glow, (0.45f + 0.3f * Mathf.Sin(t * 11f + sd)) * a);
            }
            // the cranium: seen from above, the face at its front edge
            V2 c = f.P(-3f, 0f);
            float ang = Mathf.Atan2(f.F.Y, f.F.X);
            Rgba dome = bone.Mix(new Rgba(0.45f, 0.43f, 0.37f), 0.45f), domeHi = bone.Mix(new Rgba(0.45f, 0.43f, 0.37f), 0.2f);
            Ellipse(c, 27f * K, 24f * K, ang, dome, 32);
            Ellipse(f.P(-7f, -4f), 17f * K, 14f * K, ang, domeHi, 28);  // the lit side of the dome
            Line(f.P(-29f, 0f), f.P(12f, 0f), 1.2f * K, new Rgba(0.35f, 0.33f, 0.29f, a));  // the suture down the middle
            // cracks glowing through, more each phase
            for (int k = 0; k < stage * 2; k++)
            {
                float y0 = (k % 2 == 0 ? -1f : 1f) * (6f + 4f * k);
                V2 p0 = f.P(-20f + 3f * k, y0 * 0.6f), p1 = f.P(-12f + 2f * k, y0), p2 = f.P(-4f, y0 * 1.3f);
                Line(p0, p1, 2.6f * K, glow.WithAlpha(0.35f * a));
                Line(p1, p2, 2.6f * K, glow.WithAlpha(0.35f * a));
                Line(p0, p1, 1f * K, Rgba.White.Mix(glow, 0.4f).WithAlpha(a));
                Line(p1, p2, 1f * K, Rgba.White.Mix(glow, 0.4f).WithAlpha(a));
            }
            // brow, sockets, nose and jaw at the front
            SetColor(bone.Mix(new Rgba(0.6f, 0.58f, 0.5f), 0.3f));
            Quad(f.P(14f, -15f), f.P(24f, -11f), f.P(24f, 11f), f.P(14f, 15f));
            Ellipse(f.P(25f, 0f), 5f * K, 11f * K, ang, bone.Mix(new Rgba(0.6f, 0.58f, 0.5f), 0.3f), 16);  // the jaw jutting out
            for (int k = -2; k <= 2; k++)
                Line(f.P(28f, k * 3.2f), f.P(31f, k * 3.2f), 1.6f * K, bone);
            Ellipse(f.P(18f, -7f), 4.4f * K, 4.6f * K, ang, LSocket.WithAlpha(a), 12);
            Ellipse(f.P(18f, 7f), 4.4f * K, 4.6f * K, ang, LSocket.WithAlpha(a), 12);
            SetColor(LSocket.WithAlpha(a));
            Tri(f.P(21f, -1.6f), f.P(21f, 1.6f), f.P(25f, 0f));
            LichEyes(f.P(19f, -7f), f.P(19f, 7f), 1.6f * K, glow, a, t);
            // the crown: a gold band round the dome with bone-and-gold spikes, a soul gem at the front
            Arc(c, 21f * K, 2f * K, LGold.Mix(LRobe, 0.25f).WithAlpha(a), 32);
            for (int k = 0; k < 9; k++)
            {
                float g = ang + MathX.Pi + (k - 4f) * 0.4f;
                V2 d = V2.FromAngle(g), b = c + d * (20f * K);
                SetColor((k % 2 == 0 ? LGold : bone).WithAlpha(a));
                Tri(b + d.Orthogonal() * (2.6f * K), b - d.Orthogonal() * (2.6f * K), b + d * ((k == 4 ? 15f : 9f) * K));
            }
            V2 gem = f.P(14f, 0f);
            GlowDisc(gem, 9f * K, glow, 0.5f * a);
            SetColor(Rgba.White.Mix(glow, 0.5f).WithAlpha(a));
            Quad(gem + f.F * (3.2f * K), gem + f.S * (2.4f * K), gem - f.F * (3.2f * K), gem - f.S * (2.4f * K));
        }

        // ---- C: the Wraith ----
        void DrawLichWraith(LichFrame f, int stage, Rgba glow, Rgba bone, float hit, float a, float t)
        {
            float K = f.K;
            float ang = Mathf.Atan2(f.F.Y, f.F.X);
            Rgba shroud = new Rgba(0.1f, 0.16f, 0.15f).Mix(glow, 0.25f).Mix(Rgba.White, hit * 0.5f);
            GlowDisc(f.O, 64f * K, glow, 0.18f * a);
            // the tatters trailing behind, rippling
            for (int k = -3; k <= 3; k++)
            {
                V2 prev = f.P(-6f, k * 6.5f);
                float w0 = 7f * K;
                for (int s = 1; s <= 6; s++)
                {
                    float x = -6f - s * (8f + 1.5f * (3 - Mathf.Abs(k))), y = k * (6.5f + s * 1.6f) + Mathf.Sin(t * 5f - s * 0.9f + k * 1.7f) * (1.2f * s);
                    V2 next = f.P(x, y);
                    float w = w0 * (1f - s / 7f);
                    Line(prev, next, w, shroud.Mix(glow, 0.1f * s).WithAlpha(a * (0.95f - s * 0.12f)));
                    prev = next;
                }
            }
            // the shroud: two see-through layers, a glowing rim
            Ellipse(f.P(-2f, 0f), 31f * K, 26f * K, ang, shroud.WithAlpha(0.85f * a), 28);
            Ellipse(f.P(2f, 0f), 23f * K, 19f * K, ang, shroud.Mix(glow, 0.18f).WithAlpha(0.85f * a), 24);
            Arc(f.P(-2f, 0f), 30f * K, 1.6f * K, glow.WithAlpha(0.5f * a), 28);
            // the long arms reaching forward, clawed
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float reach = Mathf.Sin(t * 2.4f + sd * 1.3f) * 3f;
                V2 sh = f.P(4f, sd * 15f), el = f.P(18f + reach, sd * 25f), wr = f.P(34f + reach, sd * 20f);
                Line(sh, el, 2.6f * K, bone);
                Line(el, wr, 2.2f * K, bone);
                Circle(el, 2.2f * K, bone, 8);
                for (int k = 0; k < 3; k++)
                {
                    V2 d = (f.F + f.S * (sd * (0.1f - 0.35f * k))).Normalized();
                    V2 j = wr + d * (5f * K), tip = j + (d - f.S * (sd * 0.5f)).Normalized() * (5f * K);
                    Line(wr, j, 1.4f * K, bone);
                    Line(j, tip, 1.1f * K, bone);
                }
            }
            // the ribcage: a spine, four ribs a side, the soul gem caged in it
            Line(f.P(-18f, 0f), f.P(10f, 0f), 2.6f * K, bone);
            for (int k = 0; k < 4; k++)
            {
                float x = 6f - k * 6f, span = 14f - k * 1.8f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    V2 p0 = f.P(x, 0f), p1 = f.P(x + 2f, sd * span * 0.75f), p2 = f.P(x - 3f, sd * span);
                    Line(p0, p1, 2f * K, bone);
                    Line(p1, p2, 1.8f * K, bone);
                }
            }
            V2 heart = f.P(-3f, 0f);
            float beat = 1f + 0.18f * Mathf.Max(0f, Mathf.Sin(t * (4f + 2f * stage)));
            GlowDisc(heart, 15f * K * beat, glow, 0.95f * a);
            SetColor(Rgba.White.Mix(glow, 0.45f).WithAlpha(a));
            Quad(heart + f.F * (5f * K * beat), heart + f.S * (3.4f * K * beat), heart - f.F * (5f * K * beat), heart - f.S * (3.4f * K * beat));
            // the skull out front, a wisp of a hood over it
            V2 sk = f.P(16f, 0f);
            Arc(sk, 11f * K, 3f * K, shroud.WithAlpha(0.8f * a), 18, ang + MathX.Pi * 0.55f, MathX.Pi * 0.9f);
            Ellipse(sk, 8f * K, 8.5f * K, ang, bone, 18);
            Ellipse(f.P(20f, -3.4f), 2.6f * K, 2.8f * K, ang, LSocket.WithAlpha(a), 10);
            Ellipse(f.P(20f, 3.4f), 2.6f * K, 2.8f * K, ang, LSocket.WithAlpha(a), 10);
            LichEyes(f.P(20.5f, -3.4f), f.P(20.5f, 3.4f), 1.2f * K, glow, a, t);
            // the Final Rite: embers of soul fire streaming off it
            for (int k = 0; k < stage * 2; k++)
            {
                float ph = MathX.Fmod(t * 0.9f + k * 0.37f, 1f);
                V2 m = f.P(-10f - ph * 50f, Mathf.Sin(k * 2.3f) * 22f + Mathf.Sin(t * 3f + k) * 4f);
                GlowDisc(m, 6f * K, glow, 0.7f * (1f - ph) * a);
            }
        }
    }
}
