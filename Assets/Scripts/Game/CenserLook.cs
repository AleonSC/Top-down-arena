using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Censer's Thurible Rite, held and swung (Docs/Mockups/censer-rework.html; the motion is Core/CenserMotion.cs, which the
    /// host also times the hits from). Drawing only, with Visual effects on. The Knight's model leaves its arms out with the
    /// Censer; this draws, once a frame for each player who has it (DrawCenser, from the player loop):
    ///   - both gloved hands together on a short brass haft, as the Halberd is held, each arm joined to a gold pauldron that turns
    ///     with the swing; the left hand lifts off in a blessing as a heal goes out;
    ///   - the chain, simulated (CenserRig): it sags at rest, goes taut and whips in a swing and drags behind when you walk;
    ///   - the head: a pierced bronze thurible with a domed lid and a cross, coals flickering through it, smoke streaming off it;
    ///   - each step (FxKind.CenserStep, Radius = id * 10 + step) played from its age, with a gold trail while the head can hit,
    ///     heal motes flying to each ally in reach as it lands, and the Benediction's ring and rays;
    ///   - Censer Cast (FxKind.CenserCast): the wind-up, the head arcing out to the cursor, the burst, the reel back;
    ///   - three pips over the head for the rite, an Ember flame under the feet for each Ember, and under your own feet a ring
    ///     filling as Censer Cast comes back.
    public partial class ArenaApp
    {
        static readonly Rgba CenserBronze = new Rgba(0.54f, 0.37f, 0.13f), CenserDark = new Rgba(0.18f, 0.11f, 0.03f), CenserLid = new Rgba(0.93f, 0.78f, 0.45f),
            CenserGlow = new Rgba(1f, 0.81f, 0.43f), CenserHot = new Rgba(1f, 0.96f, 0.86f), CenserHeal = new Rgba(0.55f, 0.96f, 0.67f),
            CenserEmber = new Rgba(1f, 0.59f, 0.24f), CenserSmoke = new Rgba(0.93f, 0.89f, 0.81f), CenserChain = new Rgba(0.89f, 0.76f, 0.48f),
            CenserGold = new Rgba(0.95f, 0.8f, 0.45f), CenserArmDark = new Rgba(0.16f, 0.12f, 0.05f);
        const int CenserLinks = 9, CenserHist = 28;
        const float CenserHistEvery = 0.035f, CenserSmokeLife = 1f;

        class CenserRig
        {
            public readonly V2[] Rope = new V2[CenserLinks + 1], Prev = new V2[CenserLinks + 1];
            public readonly V2[] Hist = new V2[CenserHist];
            public readonly float[] HistT = new float[CenserHist];
            public int HistI, Frame = -1;
            public float HistAcc;
            public bool Ready;
            public V2 Head, Vel;
        }

        readonly Dictionary<int, CenserRig> censerRigs = new Dictionary<int, CenserRig>();

        /// The rite's step effect of this player playing now, or the cast, whichever began last (null: at rest).
        Effect CenserFxOf(int id, out bool cast)
        {
            Effect best = null;
            cast = false;
            foreach (var fx in game.Effects)
            {
                if (fx.T >= fx.Dur || (fx.Kind != FxKind.CenserStep && fx.Kind != FxKind.CenserCast) || Mathf.RoundToInt(fx.Radius) / 10 != id)
                    continue;
                if (best == null || fx.T < best.T)
                {
                    best = fx;
                    cast = fx.Kind == FxKind.CenserCast;
                }
            }
            return best;
        }

        /// The Censer held, swung or thrown, for one player (from the player loop, after the body).
        void DrawCenser(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.Censer || p.Downed)
                return;
            CenserRig rig;
            if (!censerRigs.TryGetValue(p.Id, out rig))
                censerRigs[p.Id] = rig = new CenserRig();
            bool cast;
            var fx = CenserFxOf(p.Id, out cast);
            V2 o = p.Disp;
            float baseReach = Game.MeleeReach(p, p.Weapon.Reach), aim = p.Aim, c = 0f, z = 0f;
            int step = 0;
            CenserMotion.Pose m;
            CenserMotion.Step s = null;
            V2 head;
            if (fx != null && !cast)
            {
                step = Mathf.RoundToInt(fx.Radius) % 10;
                if (step < 1 || step > 3)
                    step = 1;
                s = CenserMotion.Steps[step - 1];
                c = fx.T;
                aim = TwinAimOf(fx, p, s.Dur, CenserMotion.Recover);
                float reach = (fx.Pos2 - fx.Pos).Length - (step == 3 ? Game.MeleeReach(p, Game.BenedictionReach) : 0f);
                m = CenserMotion.At(s, c);
                head = CenserMotion.Head(o, aim, m, reach);
                baseReach = reach;
            }
            else if (fx != null)
            {
                s = CenserMotion.Cast;
                c = fx.T;
                aim = (fx.Pos2 - fx.Pos).Angle;
                m = CenserMotion.At(s, c);
                V2 from = CenserMotion.Head(o, aim, CenserMotion.At(s, CenserMotion.CastRelease), baseReach);
                head = CenserMotion.CastHead(from, fx.Pos2, CenserMotion.Head(o, aim, m, baseReach), c, out z);
            }
            else
            {
                m = CenserMotion.Ready;
                m.Ha += 0.12f * Mathf.Sin(t * 2.4f);
                m.Hr += 1.5f * Mathf.Sin(t * 1.7f);
                head = CenserMotion.Head(o, aim, m, baseReach);
            }
            V2 grip = CenserMotion.Grip(o, aim, m);
            UpdateCenserRig(rig, o, grip, ref head, fx == null);

            // smoke off the head, under everything
            for (int i = 0; i < CenserHist; i++)
            {
                float age = t - rig.HistT[i];
                if (age < 0f || age > CenserSmokeLife || rig.HistT[i] == 0f)
                    continue;
                float u = age / CenserSmokeLife;
                uint r = (uint)(i * 7919 + p.Id * 31);
                V2 at = rig.Hist[i] + new V2((Rand01(ref r) - 0.5f) * 10f * u, -16f * u);
                Circle(at, 3f + 11f * u, CenserSmoke.WithAlpha(0.13f * (1f - u) * Mathf.Min(1f, u * 6f)), 10);
            }
            // the trail while the head can hit
            if (s != null && !cast)
                DrawCenserTrail(o, aim, s, c, baseReach, step == 3);
            // shadows: chain and head
            V2 sh = new V2(3f, 5f);
            for (int i = 0; i < CenserLinks; i++)
                Line(rig.Rope[i] + sh, rig.Rope[i + 1] + sh, 2.2f, new Rgba(0f, 0f, 0f, 0.22f));
            Circle(head + sh + new V2(z * 14f, z * 20f), 8.5f * (1f + z * 0.3f), new Rgba(0f, 0f, 0f, 0.3f - z * 0.12f), 12);

            // arms, pauldrons, the haft and both hands
            float body = aim + m.Sp + m.Tw;
            float bless = m.Bless;
            if (s != null && !cast)
            {
                float land = s.Dur * s.Land;
                if (c >= land && c < land + 0.5f)
                    bless = Mathf.Max(bless, 1f - (c - land) / 0.5f);
            }
            V2 along = head - grip;
            along = along.IsZero ? V2.FromAngle(aim) : along.Normalized();
            V2 handR = grip + along * 3f, handL = grip - along * 3f;
            V2 blessAt = o + V2.FromAngle(body - 0.4f) * 20f;
            handL = handL + (blessAt - handL) * Mathf.Clamp01(bless);
            Rgba armc = p.Def.Color;
            for (int k = 0; k < 2; k++)
            {
                V2 sPt = o + V2.FromAngle(body + (k == 0 ? 1.48f : -1.48f)) * 11.5f, hand = k == 0 ? handR : handL;
                V2 d = hand - sPt, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                if (n.X * (sPt.X - o.X) + n.Y * (sPt.Y - o.Y) < 0f)
                    n = n * -1f;
                V2 elbow = (sPt + hand) * 0.5f + n * Mathf.Max(0f, 7f - d.Length * 0.2f);
                Line(sPt, elbow, 8f, CenserArmDark);
                Line(elbow, hand, 8f, CenserArmDark);
                Line(sPt, elbow, 5.4f, armc);
                Line(elbow, hand, 5.4f, armc);
                Circle(sPt, 6.8f, CenserArmDark, 12);
                Circle(sPt, 5.6f, CenserGold, 12);
                Circle(sPt - new V2(0.8f, 1.2f), 2.2f, new Rgba(1f, 0.93f, 0.7f), 8);
            }
            Line(grip - along * 6f, grip + along * 8f, 4.6f, CenserDark);  // the brass haft the chain hangs from
            Line(grip - along * 6f, grip + along * 8f, 3f, CenserGold);
            Arc(grip + along * 9.5f, 2.4f, 1.4f, CenserGold, 8);
            // the chain
            for (int i = 0; i < CenserLinks; i++)
            {
                Line(rig.Rope[i], rig.Rope[i + 1], i % 2 == 0 ? 2.8f : 2.2f, CenserDark);
                Line(rig.Rope[i], rig.Rope[i + 1], i % 2 == 0 ? 1.5f : 1f, CenserChain);
            }
            // the head
            float glow = s != null ? 1f : 0.45f, sc = 1f + z * 0.5f;
            Circle(head, (20f + 6f * glow) * sc, CenserGlow.WithAlpha(0.08f + 0.06f * glow), 16);
            Circle(head, (13f + 3f * glow) * sc, CenserGlow.WithAlpha(0.12f + 0.08f * glow), 14);
            Polygon(head, 10.4f * sc, 8, t * 0.8f + 0.39f, CenserDark);
            Polygon(head, 9f * sc, 8, t * 0.8f + 0.39f, CenserBronze);
            for (int k = 0; k < 8; k++)
            {
                float a = t * 0.8f + k * MathX.Tau / 8f, fl = 0.55f + 0.45f * Mathf.Sin(t * 13f + k * 1.7f);
                Circle(head + V2.FromAngle(a) * 6.4f * sc, 1.35f * sc, new Rgba(1f, 0.6f + 0.25f * fl, 0.27f, (0.6f + 0.4f * glow) * fl), 6);
            }
            Circle(head, 5.6f * sc, CenserDark, 12);
            Circle(head, 4.8f * sc, CenserLid, 12);
            V2 cx = V2.FromAngle(t * 0.8f) * 3f * sc, cy = cx.Orthogonal();
            Line(head - cx, head + cx, 1.3f, CenserDark);
            Line(head - cy, head + cy, 1.3f, CenserDark);
            // hands over the haft, the blessing's glow
            if (bless > 0.05f)
                Circle(handL, 8f + 10f * bless, CenserHeal.WithAlpha(0.3f * bless), 14);
            foreach (var h in new[] { handR, handL })
            {
                Circle(h, 5.6f, new Rgba(0.16f, 0.11f, 0.06f), 12);
                Circle(h, 4.4f, new Rgba(0.72f, 0.56f, 0.34f), 12);
                Circle(h - new V2(1.2f, 1.5f), 1.4f, new Rgba(1f, 0.93f, 0.78f, 0.45f), 6);
            }

            if (s != null && !cast)
                DrawCenserHeal(p, o, aim, s, step, c, baseReach);
            if (cast)
                DrawCenserCastFx(fx, o, c);
            DrawCenserHud(p, t);
        }

        /// A step's aim, turning to where its owner aims now as it settles back to the ready.
        static float TwinAimOf(Effect fx, Player owner, float dur, float recover)
        {
            float aim = (fx.Pos2 - fx.Pos).Angle;
            if (owner != null && fx.T > dur)
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((fx.T - dur) / recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            return aim;
        }

        /// The chain, once a frame: pinned at the haft and the head, the links in between carried by their own speed.
        /// At rest the head itself is sprung toward its spot, so it lags and swings as you walk.
        void UpdateCenserRig(CenserRig rig, V2 o, V2 grip, ref V2 head, bool rest)
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f), now = Time.time;
            V2 along = head - grip;
            V2 top = grip + (along.IsZero ? V2.Zero : along.Normalized()) * 9.5f;
            if (!rig.Ready)
            {
                rig.Ready = true;
                rig.Head = head;
                for (int i = 0; i <= CenserLinks; i++)
                    rig.Rope[i] = rig.Prev[i] = top + (head - top) * (i / (float)CenserLinks);
            }
            if (rig.Frame != Time.frameCount)
            {
                rig.Frame = Time.frameCount;
                if (rest)
                {
                    rig.Vel = rig.Vel + (head - rig.Head) * (70f * dt);
                    rig.Vel = rig.Vel * Mathf.Pow(0.02f, dt);
                    rig.Head = rig.Head + rig.Vel * dt;
                    V2 off = rig.Head - o;
                    if (off.Length > 70f)
                        rig.Head = o + off.Normalized() * 70f;
                }
                else
                {
                    rig.Vel = (head - rig.Head) / dt;
                    rig.Head = head;
                }
                for (int i = 1; i < CenserLinks; i++)
                {
                    V2 v = (rig.Rope[i] - rig.Prev[i]) * 0.9f;
                    rig.Prev[i] = rig.Rope[i];
                    rig.Rope[i] = rig.Rope[i] + v;
                }
                rig.Rope[0] = rig.Prev[0] = top;
                rig.Rope[CenserLinks] = rig.Prev[CenserLinks] = rig.Head;
                float seg = Mathf.Max(top.DistanceTo(rig.Head) * 1.05f + 4f, 22f) / CenserLinks;
                for (int it = 0; it < 8; it++)
                    for (int i = 0; i < CenserLinks; i++)
                    {
                        V2 a = rig.Rope[i], b = rig.Rope[i + 1], d = b - a;
                        float len = Mathf.Max(0.001f, d.Length), f = (len - seg) / len * 0.5f;
                        if (i > 0) rig.Rope[i] = a + d * f;
                        if (i + 1 < CenserLinks) rig.Rope[i + 1] = b - d * f;
                    }
                rig.HistAcc += dt;
                if (rig.HistAcc >= CenserHistEvery)
                {
                    rig.HistAcc = 0f;
                    rig.Hist[rig.HistI] = rig.Head;
                    rig.HistT[rig.HistI] = now;
                    rig.HistI = (rig.HistI + 1) % CenserHist;
                }
            }
            head = rig.Head;
            rig.Rope[0] = top;
            rig.Rope[CenserLinks] = head;
        }

        /// The gold ribbon the head leaves while it can hit, sampled back along the motion.
        void DrawCenserTrail(V2 o, float aim, CenserMotion.Step s, float c, float reach, bool fin)
        {
            const int N = 12;
            V2 pTip = default(V2), pIn = default(V2);
            bool have = false;
            for (int i = 0; i <= N; i++)
            {
                float ci = c - i * 0.012f, k = ci / s.Dur;
                if (ci < 0f || k < s.Hot0 - 0.04f || k > s.Hot1 + 0.03f)
                {
                    have = false;
                    continue;
                }
                var mi = CenserMotion.At(s, ci);
                V2 tip = CenserMotion.Head(o, aim, mi, reach), g = CenserMotion.Grip(o, aim, mi), inner = g + (tip - g) * 0.45f;
                if (have)
                {
                    float a0 = 1f - (i - 1f) / N, a1 = 1f - (float)i / N;
                    a0 *= a0; a1 *= a1;
                    VTri(pTip, CenserGlow.WithAlpha(0.3f * a0), tip, CenserGlow.WithAlpha(0.3f * a1), inner, CenserGlow.WithAlpha(0f));
                    VTri(pTip, CenserGlow.WithAlpha(0.3f * a0), inner, CenserGlow.WithAlpha(0f), pIn, CenserGlow.WithAlpha(0f));
                    Line(pTip, tip, (fin ? 12f : 8f) * a0, CenserGlow.WithAlpha(0.22f * a0));
                    Line(pTip, tip, 2.4f * a0 + 0.5f, CenserHot.WithAlpha(0.85f * a0));
                }
                pTip = tip; pIn = inner; have = true;
            }
        }

        /// As a step lands: motes of light from the head to every ally in its heal's reach, a ring going out, and for the
        /// Benediction rays of light.
        void DrawCenserHeal(Player p, V2 o, float aim, CenserMotion.Step s, int step, float c, float reach)
        {
            float land = s.Dur * s.Land, q = c - land;
            if (q < 0f || q > 0.6f)
                return;
            float r = Game.CenserHealR[step - 1];
            float rq = Mathf.Clamp01(q / 0.4f), ra = 1f - Mathf.Clamp01(q / 0.6f);
            float ease = 1f - (1f - rq) * (1f - rq) * (1f - rq);
            Arc(o, r * ease, (step == 3 ? 7f : 4f) * ra + 1f, CenserHeal.WithAlpha(0.45f * ra), 40);
            Arc(o, r * ease * 0.97f, 1.2f, CenserHot.WithAlpha(0.5f * ra), 40);
            if (step == 3)
                for (int i = 0; i < 12; i++)
                {
                    float a = i * MathX.Tau / 12f + q * 0.6f, L = 60f + 90f * ease;
                    Line(o + V2.FromAngle(a) * 14f, o + V2.FromAngle(a) * L, 3f * ra, CenserHot.WithAlpha(0.35f * ra));
                }
            V2 from = CenserMotion.Head(o, aim, CenserMotion.At(s, land), reach);
            foreach (var ally in game.Players.Values)
            {
                if (ally.Id == p.Id || ally.Downed || ally.Disp.DistanceTo(o) > r)
                    continue;
                for (int k = 0; k < (step == 3 ? 5 : 3); k++)
                {
                    float mq = (q - k * 0.035f) / (0.32f + k * 0.05f);
                    if (mq < 0f || mq > 1f)
                        continue;
                    uint seed = (uint)(ally.Id * 97 + k * 13 + step);
                    float side = (Rand01(ref seed) - 0.5f) * 2f;
                    V2 d = ally.Disp - from, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                    V2 mid = (from + ally.Disp) * 0.5f + n * (d.Length * 0.35f * side);
                    float e = mq < 0.5f ? 2f * mq * mq : 1f - Mathf.Pow(-2f * mq + 2f, 2f) / 2f, e0 = Mathf.Max(0f, e - 0.12f);
                    V2 a = Bez3(from, mid, ally.Disp, e), b = Bez3(from, mid, ally.Disp, e0);
                    Line(b, a, 2.2f, CenserHeal.WithAlpha(0.7f));
                    Circle(a, 6f, CenserHeal.WithAlpha(0.35f), 10);
                    Circle(a, 2.6f, CenserHot, 8);
                }
                if (q > 0.3f && q < 0.6f)
                    Circle(ally.Disp, 16f + 14f * (q - 0.3f) / 0.3f, CenserHeal.WithAlpha(0.3f * (0.6f - q) / 0.3f), 16);
            }
        }

        static V2 Bez3(V2 a, V2 b, V2 c, float t)
        {
            float m = 1f - t;
            return a * (m * m) + b * (2f * m * t) + c * (t * t);
        }

        /// Censer Cast: the aim at the start, the burst where the head lands.
        void DrawCenserCastFx(Effect fx, V2 o, float c)
        {
            if (c < CenserMotion.CastLand)
            {
                float a = 0.45f * (1f - c / CenserMotion.CastLand);
                V2 d = fx.Pos2 - o;
                float L = d.Length;
                V2 u = L > 1f ? d / L : new V2(1f, 0f);
                for (float x = 10f; x < L - 6f; x += 12f)
                    Line(o + u * x, o + u * Mathf.Min(x + 5f, L), 1.6f, CenserGlow.WithAlpha(a));
                Arc(fx.Pos2, Game.CastBurstR * (1.3f - 0.3f * c / CenserMotion.CastLand), 1.6f, CenserGlow.WithAlpha(a), 32);
                return;
            }
            float q = c - CenserMotion.CastLand;
            if (q > 0.6f)
                return;
            float al = 1f - q / 0.6f, k = 1f - Mathf.Pow(1f - Mathf.Clamp01(q / 0.25f), 3f);
            if (q < 0.1f)
                Circle(fx.Pos2, 46f, CenserHot.WithAlpha(0.5f * (1f - q / 0.1f)), 20);
            Arc(fx.Pos2, Game.CastBurstR * 1.3f * k, 7f * al + 1f, CenserGlow.WithAlpha(0.75f * al), 36);
            for (int i = 0; i < 8; i++)
            {
                V2 d = V2.FromAngle(i * MathX.Tau / 8f + 0.2f);
                Line(fx.Pos2 + d * (12f + q * 120f), fx.Pos2 + d * (26f + q * 150f), 2f, CenserHot.WithAlpha(al));
            }
            int embers = Mathf.RoundToInt(fx.Radius) % 10;
            for (int i = 0; i < 4 + embers * 3; i++)
            {
                uint r = (uint)(i * 131 + 7);
                float a = Rand01(ref r) * MathX.Tau, sp = 120f + Rand01(ref r) * 200f, life = 0.35f + Rand01(ref r) * 0.25f;
                if (q > life)
                    continue;
                float dist = sp * (1f - Mathf.Exp(-q * 5f)) / 5f;
                V2 at = fx.Pos2 + V2.FromAngle(a) * dist;
                Circle(at, 2.2f, CenserEmber.WithAlpha(1f - q / life), 6);
            }
        }

        /// Three diamonds over the head for the rite (the third, the Benediction, glows when it is next), an Ember flame under
        /// the feet for each Ember, and under your own feet a ring filling as Censer Cast comes back.
        void DrawCenserHud(Player p, float t)
        {
            if (p.Id == game.MyId && game.CenserCastCd > 0f)
            {
                float k = 1f - game.CenserCastCd / Game.CastCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, CenserGlow.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            int cap = game.EmberCap(p), n = Mathf.Min(cap, p.Embers);
            for (int i = 0; i < cap; i++)
            {
                V2 at = p.Disp + new V2((i - (cap - 1) / 2f) * 9f, p.Def.BodyR + 12f);
                if (i < n)
                {
                    float fl = 1f + 0.18f * Mathf.Sin(t * 16f + i * 2f);
                    Circle(at, 7f * fl, CenserEmber.WithAlpha(0.25f), 10);
                    SetColor(new Rgba(1f, 0.69f, 0.29f));
                    Tri(at + new V2(0f, -6f * fl), at + new V2(3.2f, 1f), at + new V2(-3.2f, 1f));
                    Circle(at + new V2(0f, 0.6f), 3.1f, new Rgba(1f, 0.69f, 0.29f), 8);
                    Circle(at + new V2(0f, 0.6f), 1.2f, new Rgba(1f, 0.94f, 0.75f), 6);
                }
                else
                    Arc(at, 2.2f, 1f, CenserEmber.WithAlpha(0.35f), 8);
            }
            int step = CenserMotion.StepOf(p.Combo);
            if (step == 0)
                return;
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 16f);
            for (int i = 0; i < 3; i++)
            {
                V2 at = c + new V2((i - 1f) * 10f, 0f);
                float r = i == 2 ? 3.8f : 3f;
                bool lit = i < step, next = i == 2 && step == 2;
                Rgba col = lit ? CenserGlow : next ? new Rgba(1f, 0.85f, 0.45f, 0.6f + 0.4f * Mathf.Sin(t * 14f)) : new Rgba(1f, 1f, 1f, 0.15f);
                Polygon(at, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                Polygon(at, r, 4, 0f, col);
            }
        }
    }
}
