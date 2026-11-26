using UnityEngine;

namespace Arena
{
    /// The Lich's body (Docs/Mockups: the "Lich Rework Directions" renders, A then C). It faces its nearest player and takes
    /// its phase colour: The Crypt soul green, Soul Harvest spirit cyan, Undeath spectre violet (and see-through), the Final
    /// Rite rose. Phases 1-3: the Hooded Sorcerer (ragged cloak, skull in a hood, bone pauldrons, soul staff; a cloak rune
    /// and a soul mote more each phase). The Final Rite: the cloak is gone and the Wraith is left (a see-through shroud
    /// trailing tatters, an open ribcage round its beating soul gem, long clawed arms, soul embers streaming off).
    /// Also here: the graves (DrawGrave) and the phylacteries' soul jars (DrawPhylactery).
    public partial class ArenaApp
    {
        static readonly Rgba LRobe = new Rgba(0.07f, 0.1f, 0.09f), LRobeHi = new Rgba(0.13f, 0.19f, 0.16f), LSocket = new Rgba(0.02f, 0.03f, 0.03f),
            LWood = new Rgba(0.22f, 0.17f, 0.12f), LGoldEmber = new Rgba(1f, 0.72f, 0.35f);

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
            // into the Final Rite (it starts at once, no phase change): the cloak sheds over ShedLen
            if (stage == (int)LichStage.LastStand && lichSeenStage == (int)LichStage.Undeath && lichSeenId == e.Id)
                lichShedAt = t;
            lichSeenStage = stage;
            lichSeenId = e.Id;
            float shed = lichShedForce >= 0f ? lichShedForce : (t - lichShedAt) / ShedLen;
            if (stage == (int)LichStage.LastStand && shed >= 0f && shed < 1f)
                DrawLichShed(f, shed, glow, bone, hit, a, t);
            else if (stage == (int)LichStage.LastStand)
                DrawLichWraith(f, stage, glow, bone, hit, a, t);
            else
                DrawLichSorcerer(f, stage, glow, bone, hit, a, t);
        }

        int lichSeenStage = -1, lichSeenId = -1;  // the Lich's phase last frame, to catch the Final Rite starting
        float lichShedAt = -99f;                  // when its cloak started shedding (draw time)
        internal float lichShedForce = -1f;       // >= 0: draw the shedding that far through (preview: LICHSHED)
        const float ShedLen = 1.3f;

        /// Entering the Final Rite (k 0-1 through ShedLen). Until Split the Sorcerer strains: it shakes harder and harder,
        /// rose light splits its cloak in cracks and its runes blaze. Then the cloak bursts: its shards, the pauldrons and the
        /// staff fly off, spinning and burning out, a flash ring goes out, and the Wraith is left in the middle.
        const float LichSplit = 0.25f;
        void DrawLichShed(LichFrame f, float k, Rgba glow0, Rgba bone, float hit, float a, float t)
        {
            float K = f.K;
            Rgba rose = new Rgba(1f, 0.42f, 0.6f).WithAlpha(a), glow = glow0.Mix(rose, MathX.Clamp01(k / LichSplit)).WithAlpha(a);
            const int N = 22;
            if (k < LichSplit)
            {
                float u = k / LichSplit;
                f.O = f.O + new V2(Mathf.Sin(t * 61f), Mathf.Cos(t * 53f)) * (u * u * 2.6f * K);  // straining
                DrawLichSorcerer(f, 3, glow, bone, hit, a, t);
                GlowDisc(f.O, (40f + 30f * u) * K, rose, 0.25f * u * a);
                for (int i = 0; i < 7; i++)  // cracks of light splitting the cloak from the middle out
                {
                    float ang = MathX.Tau * i / 7f + 0.4f, len = (34f + 8f * Mathf.Max(0f, -Mathf.Cos(ang))) * Mathf.Min(1f, u * 1.4f);
                    V2 mid = f.P(Mathf.Cos(ang + 0.25f) * len * 0.5f, Mathf.Sin(ang + 0.25f) * len * 0.5f), end = f.P(Mathf.Cos(ang) * len, Mathf.Sin(ang) * len);
                    Line(f.O, mid, 4.5f * K, rose.WithAlpha(0.35f * u * a));
                    Line(mid, end, 4.5f * K, rose.WithAlpha(0.35f * u * a));
                    Line(f.O, mid, 1.6f * K, Rgba.White.Mix(rose, 0.3f).WithAlpha(u * a));
                    Line(mid, end, 1.6f * K, Rgba.White.Mix(rose, 0.3f).WithAlpha(u * a));
                }
                return;
            }
            float v = (k - LichSplit) / (1f - LichSplit), fly = 1f - (1f - v) * (1f - v) * (1f - v), fade = 1f - v;  // ease out
            // the Wraith steps out of it
            DrawLichWraith(f, 3, rose, bone, hit, a * Mathf.Min(1f, 0.35f + v * 1.5f), t);
            // the flash ring
            Arc(f.O, (40f + 130f * fly) * K, (6f * fade + 1f) * K, Rgba.White.Mix(rose, 0.3f).WithAlpha(fade * fade * a), 40);
            // the cloak's shards flying off, spinning, their edges burning
            Rgba robe = LRobe.WithAlpha(a * fade);
            for (int i = 0; i < N; i++)
            {
                float a0 = MathX.Tau * i / N, a1 = MathX.Tau * (i + 1) / N, am = (a0 + a1) * 0.5f;
                float back0 = Mathf.Max(0f, -Mathf.Cos(a0)), back1 = Mathf.Max(0f, -Mathf.Cos(a1));
                float r0 = 30f + 14f * back0 + (i % 2 == 0 ? 6f : -2f), r1 = 30f + 14f * back1;
                float spin = (i % 3 - 1) * 2.2f * fly, push = (40f + 18f * ((i * 7) % 5)) * fly;
                V2 dir = f.F * Mathf.Cos(am) + f.S * Mathf.Sin(am), c = f.O + dir * ((14f + push) * K);
                V2 p0 = f.P(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0) - f.O, p1 = f.P(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1) - f.O;
                V2 q0 = c + (p0 - dir * (14f * K)).Rotated(spin) * 0.55f, q1 = c + (p1 - dir * (14f * K)).Rotated(spin) * 0.55f, q2 = c + (dir * (-12f * K)).Rotated(spin);
                SetColor(robe);
                Tri(q0, q1, q2);
                Line(q0, q1, 1.8f * K, rose.Mix(LGoldEmber, 0.5f).WithAlpha(fade * a));
                if (i % 3 == 0)
                    GlowDisc(c, 6f * K, LGoldEmber, fade * 0.6f * a);
            }
            // the pauldrons and the staff thrown clear
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 sh = f.P(0f, sd * (17f + 60f * fly));
                Circle(sh, 8.5f * K * (1f - 0.4f * v), bone.WithAlpha(fade * a), 14);
            }
            V2 sm = f.P(10f + 20f * fly, 23f + 45f * fly), sd2 = (f.F * 28f * K).Rotated(1.8f * fly);
            Line(sm - sd2, sm + sd2, 3.6f * K, LWood.WithAlpha(fade * a));
            GlowDisc(sm + sd2, 14f * K * fade, rose, fade * 0.8f * a);
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
                SetColor(bone);
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

        static readonly Rgba LStone = new Rgba(0.25f, 0.28f, 0.27f), LStoneHi = new Rgba(0.4f, 0.44f, 0.42f), LSoil = new Rgba(0.1f, 0.09f, 0.08f),
            LSoilHi = new Rgba(0.2f, 0.17f, 0.14f), LBronze = new Rgba(0.55f, 0.42f, 0.25f);

        /// A grave: a weathered headstone (a chipped corner, a crack, a carved soul rune) over a heaped plot of earth whose
        /// cracks glow in the phase colour, souls drifting up out of it. heat 0-1: how stirred it is.
        void DrawGrave(V2 g, Rgba glow, float heat, float t, int i)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f + i * 1.7f);
            GlowDisc(g + new V2(0f, 40f), 46f, glow, (0.08f + 0.22f * heat) * pulse);
            // the plot: heaped earth in front of the stone
            Ellipse(g + new V2(0f, 44f), 24f, 20f, 0f, new Rgba(0f, 0f, 0f, 0.35f), 20);
            Ellipse(g + new V2(0f, 41f), 21f, 18f, 0f, LSoil, 20);
            Ellipse(g + new V2(-3f, 37f), 15f, 11f, 0f, LSoilHi, 16);
            for (int k = 0; k < 4; k++)  // clods
                Circle(g + new V2(-14f + k * 9f, 46f + (k % 2) * 6f), 2.4f, LSoilHi.Mix(LSoil, 0.4f), 6);
            Rgba crack = glow.WithAlpha((0.35f + 0.6f * heat) * pulse);
            Line(g + new V2(-10f, 33f), g + new V2(-2f, 41f), 1.6f, crack);
            Line(g + new V2(-2f, 41f), g + new V2(9f, 38f), 1.6f, crack);
            Line(g + new V2(-2f, 41f), g + new V2(1f, 52f), 1.6f, crack);
            // souls drifting up out of the earth
            for (int k = 0; k < 2; k++)
            {
                float ph = MathX.Fmod(t * 0.35f + k * 0.5f + i * 0.23f, 1f);
                V2 m = g + new V2(-6f + 12f * k + Mathf.Sin(t * 2f + k * 3f + i) * 4f, 40f - ph * 70f);
                GlowDisc(m, 7f + 4f * heat, glow, (1f - ph) * (0.25f + 0.5f * heat));
            }
            // the headstone and its shadow
            SetColor(new Rgba(0f, 0f, 0f, 0.35f));
            Quad(g + new V2(-20f, 22f), g + new V2(20f, 22f), g + new V2(22f, 28f), g + new V2(-22f, 28f));
            SetColor(LStone);
            Quad(g + new V2(-17f, -8f), g + new V2(17f, -8f), g + new V2(18f, 24f), g + new V2(-18f, 24f));
            Arc(g + new V2(0f, -8f), 8.5f, 17f, LStone, 20, MathX.Pi, MathX.Pi);  // the rounded top
            SetColor(LStoneHi.WithAlpha(0.5f));
            Quad(g + new V2(-17f, -8f), g + new V2(-12f, -8f), g + new V2(-13f, 24f), g + new V2(-18f, 24f));  // the lit edge
            SetColor(LSoil);
            Tri(g + new V2(17.5f, -6f), g + new V2(9f, -21f), g + new V2(17.5f, -18f));  // a chipped corner
            Arc(g + new V2(0f, -8f), 17f, 1.6f, LStoneHi, 20, MathX.Pi, MathX.Pi * 0.72f);
            Line(g + new V2(-17f, 24f), g + new V2(-17f, -8f), 1.6f, LStoneHi);
            Line(g + new V2(-19f, 24f), g + new V2(19f, 24f), 2f, new Rgba(0.17f, 0.19f, 0.18f));
            // moss at the foot
            for (int k = 0; k < 5; k++)
                Circle(g + new V2(-14f + k * 6.5f, 22f - (k % 2) * 2f), 3f, new Rgba(0.2f, 0.32f, 0.2f, 0.85f), 6);
            // the carved rune: a ring and a cross, glowing as it stirs
            Rgba rune = LSocket.Mix(glow, 0.15f + 0.85f * heat * pulse);
            Arc(g + new V2(0f, -2f), 6.5f, 1.8f, rune, 16);
            Line(g + new V2(0f, -13f), g + new V2(0f, 14f), 2f, rune);
            Line(g + new V2(-6f, -2f), g + new V2(6f, -2f), 2f, rune);
            if (heat > 0.5f)
                GlowDisc(g + new V2(0f, -2f), 18f, glow, 0.5f * heat * pulse);
            Line(g + new V2(-9f, 8f), g + new V2(-4f, 14f), 1.2f, new Rgba(0.12f, 0.13f, 0.12f));
            Line(g + new V2(-4f, 14f), g + new V2(-7f, 21f), 1.2f, new Rgba(0.12f, 0.13f, 0.12f));
        }

        /// A phylactery: a glass soul jar in bronze bands, a soul swirling inside and a little skull for a stopper. Its glass
        /// cracks (glowing) as it loses health.
        void DrawPhylactery(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float a = col.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f, hp = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            float K = r / 18f;
            Rgba glow = SoulCol, bone = BoneCol.Mix(Rgba.White, hit).WithAlpha(a), bronze = LBronze.Mix(Rgba.White, hit).WithAlpha(a);
            V2 c = pos + new V2(0f, 3f * K);
            Ellipse(pos + new V2(0f, 18f * K), 13f * K, 4f * K, 0f, new Rgba(0f, 0f, 0f, 0.35f * a), 16);
            // the soul inside
            Circle(c, 13f * K, new Rgba(0.04f, 0.1f, 0.08f, 0.9f * a), 22);
            GlowDisc(c, 15f * K, glow, (0.6f + 0.2f * Mathf.Sin(t * 5f + e.Id)) * a);
            for (int k = 0; k < 3; k++)
            {
                V2 m = c + V2.FromAngle(t * 2.6f + MathX.Tau * k / 3f) * (6f * K);
                Circle(m, 2.2f * K, Rgba.White.Mix(glow, 0.4f).WithAlpha(0.9f * a), 8);
            }
            // the glass: a pale rim, a highlight
            Circle(c, 13f * K, glow.Mix(Rgba.White, 0.5f + 0.5f * hit).WithAlpha(0.16f * a), 22);
            Arc(c, 13f * K, 1.6f * K, glow.Mix(Rgba.White, 0.4f).WithAlpha(0.75f * a), 22);
            Arc(c, 9.5f * K, 1.6f * K, Rgba.White.WithAlpha(0.45f * a), 10, MathX.Pi * 1.1f, MathX.Pi * 0.45f);
            // cracks as it breaks
            Rgba ck = Rgba.White.Mix(glow, 0.3f).WithAlpha(a);
            if (hp < 0.67f)
            {
                Line(c + new V2(-11f, -4f) * K, c + new V2(-3f, 1f) * K, 1.3f * K, ck);
                Line(c + new V2(-3f, 1f) * K, c + new V2(-5f, 9f) * K, 1.3f * K, ck);
            }
            if (hp < 0.34f)
            {
                Line(c + new V2(10f, -6f) * K, c + new V2(4f, -1f) * K, 1.3f * K, ck);
                Line(c + new V2(4f, -1f) * K, c + new V2(8f, 7f) * K, 1.3f * K, ck);
                Line(c + new V2(4f, -1f) * K, c + new V2(-1f, -8f) * K, 1.3f * K, ck);
            }
            // bronze bands and the neck
            Arc(c, 13.5f * K, 2.2f * K, bronze, 12, MathX.Pi * 0.15f, MathX.Pi * 0.7f);
            SetColor(bronze);
            Quad(c + new V2(-6f, -12f) * K, c + new V2(6f, -12f) * K, c + new V2(5f, -16f) * K, c + new V2(-5f, -16f) * K);
            // the skull stopper
            V2 sk = c + new V2(0f, -20f) * K;
            Circle(sk, 5f * K, bone, 12);
            Quad(sk + new V2(-3f, 2f) * K, sk + new V2(3f, 2f) * K, sk + new V2(2.4f, 6f) * K, sk + new V2(-2.4f, 6f) * K);
            Circle(sk + new V2(-1.9f, 0.4f) * K, 1.3f * K, LSocket.WithAlpha(a), 6);
            Circle(sk + new V2(1.9f, 0.4f) * K, 1.3f * K, LSocket.WithAlpha(a), 6);
        }

        /// A bone prison: a ring of cracked earth, then curved rib bones rising out of it round the player, joined by a
        /// faint soul line once they're up, their tips glowing hotter until they fire inward; after that they go grey. The
        /// gap is framed by two taller bones topped with skulls, a gold chevron between them pointing the way out.
        void DrawBonePrison(BonePrison q, float t)
        {
            float k = MathX.Clamp01(q.T / Game.PrisonRise), up = q.T >= Game.PrisonRise ? 1f : 0f;
            float spent = up > 0f ? MathX.Clamp01((q.T - Game.PrisonRise) / 0.4f) : 0f;
            float R = Game.PrisonR;
            Arc(q.Center, R, 9f, new Rgba(0f, 0f, 0f, 0.3f), 48);  // the cracked earth
            Arc(q.Center, R, 1.5f, BoneCol.WithAlpha(0.12f + 0.2f * k), 48);
            var tips = new V2[Game.PrisonSpikes];
            for (int i = 0; i < Game.PrisonSpikes; i++)
            {
                float ang = Game.PrisonSpikeAngle(q, i);
                bool post = i == 0 || i == Game.PrisonSpikes - 1;
                V2 dir = V2.FromAngle(ang), side = dir.Orthogonal(), at = q.Center + dir * R;
                // a clod of earth heaved up at its foot
                Circle(at + dir * 3f, 6f + 2f * k, LSoil.WithAlpha(0.85f), 8);
                Circle(at + dir * 2f - side * 3f, 3f, LSoilHi.WithAlpha(0.85f), 6);
                // the bone: rising inward, curving to one side like a rib, tapering to a point
                float h = (post ? 34f : 26f) * k * (1f - 0.15f * spent), bend = (i % 2 == 0 ? 1f : -1f) * 5f * k;
                V2 mid = at - dir * (h * 0.55f) + side * (bend * 0.6f), tip = at - dir * h + side * bend;
                tips[i] = tip;
                Rgba b = BoneCol.Mix(new Rgba(0.45f, 0.45f, 0.42f), 0.55f * spent).WithAlpha(1f - 0.45f * spent);
                if (k > 0.05f)
                {
                    SetColor(b);
                    Quad(at + side * 5f, at - side * 5f, mid - side * 3.5f, mid + side * 3.5f);
                    Tri(mid + side * 3.5f, mid - side * 3.5f, tip);
                    Circle(at, 5.5f, b, 10);  // the knuckle at its root
                    Line(at - side * 5f, mid - side * 3.5f, 1.4f, new Rgba(0.55f, 0.53f, 0.47f, b.A));  // its shaded edge
                    Line(mid - side * 3.5f, tip, 1.2f, new Rgba(0.55f, 0.53f, 0.47f, b.A));
                }
                if (post && k > 0.5f)  // a skull on each gatepost
                {
                    Circle(tip, 5f, b, 12);
                    Circle(tip + side * 1.8f - dir * 0.6f, 1.2f, LSocket, 6);
                    Circle(tip - side * 1.8f - dir * 0.6f, 1.2f, LSocket, 6);
                }
                else if (k > 0.2f)  // its tip charging, a flash as it fires
                {
                    float charge = up > 0f ? (1f - spent) : k * k;
                    GlowDisc(tip, 7f + 6f * charge, SpiritCol, 0.25f + 0.6f * charge);
                }
            }
            // the soul line joining the bones (not across the gap)
            if (k > 0.6f)
                for (int i = 0; i + 1 < Game.PrisonSpikes; i++)
                    Line(tips[i], tips[i + 1], 1.4f, SpiritCol.WithAlpha((k - 0.6f) * 1.2f * (1f - spent)));
            // the way out: a gold chevron in the gap, pointing out
            V2 g = V2.FromAngle(q.Gap), gs = g.Orthogonal(), gc = q.Center + g * R;
            float gp = 0.55f + 0.45f * Mathf.Sin(t * 10f);
            Rgba gold = Rgba.Gold.WithAlpha(gp);
            GlowDisc(gc, 16f, Rgba.Gold, 0.35f * gp);
            Line(gc - g * 5f + gs * 7f, gc + g * 4f, 3f, gold);
            Line(gc - g * 5f - gs * 7f, gc + g * 4f, 3f, gold);
        }

        V2[] spiritLast = new V2[12];  // each spirit's last position, for which way it's going

        /// A wailing spirit: a pale ghost head, mouth open in a wail, trailing three wisps behind it. Circling it drifts
        /// round the Lich; diving it stretches out, its eyes burn and its trail streams long.
        void DrawWailingSpirit(LichSpirit sp, int i, V2 lich, float t)
        {
            V2 vel;
            if (sp.Diving && i < spiritLast.Length && (sp.Pos - spiritLast[i]).Length > 0.5f && (sp.Pos - spiritLast[i]).Length < 80f)
                vel = (sp.Pos - spiritLast[i]).Normalized();
            else
            {
                V2 rad = sp.Pos - lich;
                vel = rad.Length > 1f ? rad.Normalized().Orthogonal() : new V2(1f, 0f);
            }
            if (i < spiritLast.Length)
                spiritLast[i] = sp.Pos;
            float R = Game.SpiritR, dive = sp.Diving ? 1f : 0f;
            V2 back = vel * -1f, side = vel.Orthogonal();
            GlowDisc(sp.Pos, R * (3f + 1.5f * dive), SpiritCol, 0.35f + 0.3f * dive);
            // the wisps trailing behind: smooth ribbons thinning and fading to nothing
            for (int w = -1; w <= 1; w++)
            {
                const int Segs = 7;
                float len = R * (0.75f + 0.8f * dive) * (w == 0 ? 1.2f : 0.95f), w0 = R * (w == 0 ? 0.8f : 0.5f);
                V2 prev = sp.Pos + side * (w * R * 0.45f);
                V2 pside = side;
                for (int s = 1; s <= Segs; s++)
                {
                    float u0 = (s - 1f) / Segs, u1 = (float)s / Segs;
                    V2 next = sp.Pos + back * (len * s) + side * (w * R * (0.45f + 0.25f * u1) + Mathf.Sin(t * 8f - s * 0.8f + w * 2f + i) * 1.4f * s);
                    V2 nside = (next - prev).Length > 0.01f ? (next - prev).Normalized().Orthogonal() : pside;
                    float wa = w0 * (1f - u0), wb = w0 * (1f - u1);
                    Rgba ca = new Rgba(0.75f, 0.94f, 1f, 0.6f * (1f - u0)), cb = new Rgba(0.75f, 0.94f, 1f, 0.6f * (1f - u1));
                    VTri(prev + pside * wa, ca, prev - pside * wa, ca, next + nside * wb, cb);
                    VTri(prev - pside * wa, ca, next - nside * wb, cb, next + nside * wb, cb);
                    prev = next;
                    pside = nside;
                }
            }
            // the head, stretched along its dive
            Ellipse(sp.Pos, R * (1f + 0.35f * dive), R, Mathf.Atan2(vel.Y, vel.X), new Rgba(0.78f, 0.95f, 1f), 16);
            // its face, always upright: hollow eyes and a wailing mouth
            V2 c = sp.Pos;
            Rgba hole = new Rgba(0.05f, 0.16f, 0.2f);
            Ellipse(c + new V2(-3.4f, -2.4f), 1.9f, 2.6f, 0f, hole, 8);
            Ellipse(c + new V2(3.4f, -2.4f), 1.9f, 2.6f, 0f, hole, 8);
            float wail = 1f + 0.25f * Mathf.Sin(t * 14f + i);
            Ellipse(c + new V2(0f, 3.6f), 2.2f, 3.2f * wail, 0f, hole, 10);
            if (dive > 0f)
            {
                Circle(c + new V2(-3.4f, -2.4f), 1f, SpiritCol.Mix(Rgba.White, 0.5f), 6);
                Circle(c + new V2(3.4f, -2.4f), 1f, SpiritCol.Mix(Rgba.White, 0.5f), 6);
            }
        }
    }
}
