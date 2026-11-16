using UnityEngine;

namespace Arena
{
    /// The Blood Lash's chains (Core/BloodLash.cs; mockup Docs/Mockups/blood-lash-chains.html). A chain of clotted-blood links runs
    /// from the Hemomancer's whip hand to every enemy in `Player.Chains`: it sags and sways, grows thicker with each link, flickers
    /// when its links are about to drop (the client doesn't know the timers, so it flickers once the enemy is past half the break
    /// distance), and red diamonds over the enemy count its links. Martyr's Leash pulls every chain taut and bright, a pulse runs
    /// out along each, rings beat out from the Hemomancer and a ring round them empties over the Leash's 3 s.
    public partial class ArenaApp
    {
        static readonly Rgba BlDark = new Rgba(0.36f, 0.03f, 0.07f), BlRed = new Rgba(0.85f, 0.1f, 0.18f), BlBright = new Rgba(1f, 0.36f, 0.42f);
        static readonly Rgba BlHot = new Rgba(1f, 0.72f, 0.68f);

        /// Called for each player holding chains or the Leash.
        void DrawLashChains(Player p, float t)
        {
            if (p.Downed)
                return;
            V2 hand = p.Disp + V2.FromAngle(p.Aim) * 14f;
            bool taut = p.LeashOn;
            for (int k = 0; k < p.Chains.Count; k++)
                if (game.Enemies.TryGetValue(p.Chains[k], out var e) && e.Hp > 0f)
                    DrawLashChain(hand, e, p.ChainLinks[k], taut, t, p.LeashT);
            if (!taut)
                return;
            float u = Mathf.Clamp01(p.LeashT / Game.LeashMax);
            for (int q = 0; q < 3; q++)  // rings beating out from you
            {
                float f = MathX.Fmod(t * 1.3f + q / 3f, 1f);
                Arc(p.Disp, p.Def.BodyR + 6f + f * 70f, 2f, BlBright.WithAlpha(0.35f * (1f - f)), 32);
            }
            Arc(p.Disp, p.Def.BodyR + 10f, 3.5f, BlDark.WithAlpha(0.7f), 32);
            Arc(p.Disp, p.Def.BodyR + 10f, 3.5f, BlBright.WithAlpha(0.95f), 32, -MathX.Pi / 2f, MathX.Tau * (1f - u));
        }

        /// A point `u` of the way along a chain from `from` (d: to the enemy), sagging `sag` px to one side and down.
        static V2 BlAt(V2 from, V2 d, V2 nrm, float sag, float u)
        {
            float b = Mathf.Sin(u * MathX.Pi) * sag;
            return from + d * u + nrm * b + new V2(0f, b * 0.4f);
        }

        void DrawLashChain(V2 from, Enemy e, int links, bool taut, float t, float leashT)
        {
            V2 to = e.Disp, d = to - from;
            float len = d.Length;
            if (len < 4f)
                return;
            V2 dir = d / len, nrm = dir.Orthogonal();
            float sag = taut ? 0f : Mathf.Min(26f, len * 0.12f) * (1f + 0.3f * Mathf.Sin(t * 2.2f + e.Id));
            float fade = !taut && len > Game.LashBreak * 0.5f ? (Mathf.Sin(t * 30f) > 0f ? 0.45f : 1f) : 1f;
            int n = Mathf.Max(6, (int)(len / 9f));
            float w = 2.4f + links * 0.7f, lw = 1.5f + links * 0.35f;
            // a glow under the links
            V2 prev = from;
            for (int i = 1; i <= n; i++)
            {
                V2 pt = BlAt(from, d, nrm, sag, (float)i / n);
                Line(prev, pt, 5f + links * 3f, (taut ? BlBright : BlRed).WithAlpha((taut ? 0.3f : 0.16f) * fade));
                prev = pt;
            }
            // the links: an oval, then a bar through the next
            prev = from;
            for (int i = 1; i <= n; i++)
            {
                V2 pt = BlAt(from, d, nrm, sag, (float)i / n), m = (prev + pt) * 0.5f, a = (pt - prev);
                float al = a.Length;
                if (al < 0.5f)
                    continue;
                V2 ad = a / al, an = ad.Orthogonal();
                if (i % 2 == 1)
                {
                    Rgba c = (taut ? BlBright : BlRed).WithAlpha(fade);
                    V2 h = ad * 5.5f, s = an * w;
                    Line(m - h * 0.7f + s, m + h * 0.7f + s, lw, c);
                    Line(m - h * 0.7f - s, m + h * 0.7f - s, lw, c);
                    Line(m - h * 0.7f + s, m - h, lw, c);
                    Line(m - h * 0.7f - s, m - h, lw, c);
                    Line(m + h * 0.7f + s, m + h, lw, c);
                    Line(m + h * 0.7f - s, m + h, lw, c);
                }
                else
                    Line(m - ad * 5.5f, m + ad * 5.5f, lw, BlDark.WithAlpha(fade));
                prev = pt;
            }
            if (taut)  // a pulse running out along the chain
            {
                float u = MathX.Fmod(leashT * 1.6f + e.Id * 0.23f, 1f);
                GlowDisc(BlAt(from, d, nrm, sag, u), 7f, BlHot, 0.85f);
            }
            for (int i = 0; i < links; i++)  // a diamond over the enemy for each link
            {
                V2 c = e.Disp + new V2(-(links - 1) * 5f + i * 10f, -e.Radius - 11f);
                SetColor(BlHot);
                Quad(c + new V2(0f, -4.2f), c + new V2(4.2f, 0f), c + new V2(0f, 4.2f), c + new V2(-4.2f, 0f));
                SetColor(BlRed);
                Quad(c + new V2(0f, -3f), c + new V2(3f, 0f), c + new V2(0f, 3f), c + new V2(-3f, 0f));
            }
        }

        // --- The swing (MeleeLooks.cs hands every Blood Lash Slash effect here) ----------------------------------------------
        //
        // A whip, not a blade: the hand leads and the tip trails behind it, catching up as the sweep ends, so the lash bends
        // through the swing. The tip cracks part way through (a flash, a star and a ring where it is), blood flies off it, and
        // the glow pass draws a tapered crimson ribbon behind the tip (look A, Ribbon, of the three previewed). The Snap goes
        // straight out, a wave running down it. After the sweep the ordinary model eases back into the held pose (DrawSwingModel).

        public const float LashSweepTime = 0.17f, LashCrackAt = 0.55f, LashSnapCrackAt = 0.62f, LashTrailTime = 0.3f;
        const int LashSegs = 18;
        readonly V2[] lashPts = new V2[LashSegs + 1];

        /// The whip's points at `u` of the sweep (past 1 it settles), hand to tip, into lashPts. Returns the head's angle.
        float LashPose(V2 o, float aim, float half, float s, float reach, float u, bool snap)
        {
            float e = EaseOut3(Mathf.Clamp01(u)), hd;
            if (snap)
            {
                hd = aim;
                float ext = u < 0.62f ? EaseOut3(u / 0.62f) : 1f - 0.08f * Mathf.Clamp01((u - 0.62f) / 0.6f);
                V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
                V2 hand = o + dir * 14f;
                for (int i = 0; i <= LashSegs; i++)
                {
                    float f = (float)i / LashSegs, wave = Mathf.Sin(f * MathX.Tau * 1.3f - u * 18f) * 11f * (1f - e) * f;
                    lashPts[i] = hand + dir * ((reach - 14f) * f * ext) + side * wave;
                }
                return hd;
            }
            float sweep = half * 1.15f;  // a little past the arc each way: a whip overshoots
            hd = aim - s * sweep + s * 2f * sweep * e;
            float lag = u <= 1f ? 1.25f * (1f - e) + 0.12f : 0.12f * Mathf.Max(0f, 1f - (u - 1f) * 2f);
            float out01 = 0.5f + 0.5f * Mathf.Clamp01(u * 3f);
            V2 h = o + V2.FromAngle(hd) * 14f;
            for (int i = 0; i <= LashSegs; i++)
            {
                float f = (float)i / LashSegs, a = hd - s * lag * Mathf.Pow(f, 1.6f);
                float r = (reach - 14f) * f * out01, wob = Mathf.Sin(f * 6f - u * 20f) * 4f * f * (1f - e);
                lashPts[i] = h + V2.FromAngle(a) * r + V2.FromAngle(a + MathX.Pi / 2f) * wob;
            }
            return hd;
        }

        static readonly Rgba LashOutline = new Rgba(0.16f, 0.02f, 0.05f), LashBody = new Rgba(0.78f, 0.1f, 0.17f), LashShine = new Rgba(1f, 0.55f, 0.58f);

        /// One Blood Lash swing (from DrawSlashLayer): `light` is the glow pass, otherwise the whip and the drops.
        bool DrawLashSwing(Effect fx, bool light, MeleeSeen seen, V2 o, float aim, float half, float reach, float T, Rgba col)
        {
            bool snap = half < 0.5f;
            float s = seen.Sign, u = T / LashSweepTime, crackU = snap ? LashSnapCrackAt : LashCrackAt, crackT = crackU * LashSweepTime;
            int seed = MeleeSeed(fx);
            float endT = LashSweepTime + 0.08f;
            Effect newest;
            bool mine = !meleeNewest.TryGetValue(seen.Owner, out newest) || newest == fx;
            if (light)
            {
                LashRibbon(o, aim, half, s, reach, T, snap);
                float ca = T - crackT;  // the crack: a flash, a star and a ring at the tip
                if (ca >= 0f && ca < 0.3f)
                {
                    LashPose(o, aim, half, s, reach, crackU, snap);
                    V2 tip = lashPts[LashSegs];
                    float k = 1f - ca / 0.3f;
                    GlowDisc(tip, 16f + 36f * ca, LashShine, 0.9f * k);
                    GlowDisc(tip, 7f, Rgba.White, k);
                    for (int j = 0; j < 6; j++)
                    {
                        V2 d = V2.FromAngle(j * MathX.Tau / 6f + seed * 0.37f);
                        SoftLine(tip + d * 4f, tip + d * (10f + 70f * ca), 3f, Rgba.White.WithAlpha(0.8f * k));
                    }
                    Arc(tip, 6f + 80f * ca, 2.5f * k + 0.5f, LashShine.WithAlpha(0.7f * k), 24);
                }
                return true;
            }
            LashDrops(o, aim, half, s, reach, T, snap, seed, 14, crackT);
            if (!mine)
                return true;
            if (T >= endT)
            {
                float atEnd = snap ? aim : aim + s * half;
                DrawSwingModel(fx, seen, o, T, endT, atEnd, reach, s, false, 1f, col);
                return true;
            }
            seen.ModelEnd = endT + SwingRecover;
            LashPose(o, aim, half, s, reach, u, snap);
            DrawLashBody(lashPts, T >= crackT && T < crackT + 0.06f);
            return true;
        }

        static Rgba LashMix(Rgba a, Rgba b, float t) => new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

        /// The whip from its points: a dark outline, the blood-red body thinning to the tip, a shine down it.
        void DrawLashBody(V2[] pts, bool cracking)
        {
            int n = pts.Length - 1;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 1; i <= n; i++)
                {
                    float f = (float)i / n;
                    float w = pass == 0 ? 7.5f * (1f - f) + 2.6f : pass == 1 ? 5f * (1f - f) + 1.3f : 1.6f * (1f - f) + 0.4f;
                    Rgba c = pass == 0 ? LashOutline : pass == 1 ? LashMix(LashBody, LashShine, f * f * 0.6f) : LashShine.WithAlpha(0.75f);
                    V2 a = pts[i - 1], b = pts[i];
                    if (pass == 2)
                    {
                        V2 off = (b - a).Normalized().Orthogonal() * 0.8f;
                        a += off;
                        b += off;
                    }
                    Line(a, b, w, c);
                }
            Circle(pts[n], cracking ? 4.5f : 2.4f, cracking ? Rgba.White : LashShine, 10);
        }

        /// Blood flung off the tip once it has cracked: drops thrown along its swing, slowing and shrinking.
        void LashDrops(V2 o, float aim, float half, float s, float reach, float T, bool snap, int seed, int count, float crackT)
        {
            uint r = (uint)(seed * 7 + 3);
            for (int i = 0; i < count; i++)
            {
                float born = crackT + (LashSweepTime - crackT + 0.04f) * Rand01(ref r);
                float spd = 110f + 190f * Rand01(ref r), spread = (Rand01(ref r) - 0.5f) * 0.9f, size = 2.4f + 3f * Rand01(ref r);
                float age = T - born;
                if (age < 0f || age > 0.55f)
                    continue;
                LashPose(o, aim, half, s, reach, born / LashSweepTime, snap);
                V2 from = lashPts[LashSegs], along = (lashPts[LashSegs] - lashPts[LashSegs - 2]).Normalized();
                V2 dir = snap ? (along + along.Orthogonal() * spread * 1.6f).Normalized()
                              : (along * 0.55f + (from - o).Normalized() * 0.6f + along.Orthogonal() * spread).Normalized();
                float dd = spd * age * (1f - age * 0.85f), k = 1f - age / 0.55f;
                V2 at = from + dir * dd;
                Circle(at, size * (0.5f + 0.5f * k) + 0.8f, LashOutline.WithAlpha(0.8f * k), 8);
                Circle(at, size * (0.5f + 0.5f * k), (i % 4 == 0 ? LashShine : LashBody).WithAlpha(k), 8);
            }
        }

        /// The tip's last positions back to `T - LashTrailTime * 0.4`, newest first (time steps of `dt`), into `trail`.
        int LashTrail(V2 o, float aim, float half, float s, float reach, float T, bool snap, V2[] trail, float dt)
        {
            int n = 0;
            for (int k = 0; k < trail.Length; k++)
            {
                float t = T - k * dt;
                if (t < 0f || t > LashSweepTime + 0.05f)
                {
                    if (t < 0f)
                        break;
                    continue;
                }
                LashPose(o, aim, half, s, reach, t / LashSweepTime, snap);
                trail[n++] = lashPts[LashSegs];
            }
            return n;
        }

        readonly V2[] lashTrail = new V2[20];

        /// The trail: a streak behind the tip, wide and bright at the tip, thinning to nothing; it fades after the sweep.
        void LashRibbon(V2 o, float aim, float half, float s, float reach, float T, bool snap)
        {
            float fade = T < LashSweepTime ? 1f : 1f - Mathf.Clamp01((T - LashSweepTime) / LashTrailTime);
            if (fade <= 0f)
                return;
            int n = LashTrail(o, aim, half, s, reach, Mathf.Min(T, LashSweepTime + 0.05f), snap, lashTrail, 0.011f);
            for (int k = 1; k < n; k++)
            {
                float f0 = 1f - (k - 1f) / (n - 1), f1 = 1f - (float)k / (n - 1);
                Ribbon(lashTrail[k - 1], lashTrail[k], 15f * f0 * fade + 1f, 15f * f1 * fade + 1f, LashBody.WithAlpha(0.95f * f0 * fade), LashBody.WithAlpha(0.95f * f1 * fade));
                Ribbon(lashTrail[k - 1], lashTrail[k], 4f * f0 + 0.5f, 4f * f1 + 0.5f, LashShine.WithAlpha(f0 * fade), LashShine.WithAlpha(f1 * fade));
            }
        }

    }
}
