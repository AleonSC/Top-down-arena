using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Bone Spear's look, the Marionette (Core/BoneSpear.cs; mockup Docs/Mockups/bone-spear-rework.html).
    ///   - The thread (DrawBsThread, from the replicated `Player.BsTarget`/`BsT`): a sagging line of blood from the hand to the
    ///     marked enemy with beads running along it, pulled straight and bright by Reel, a glow running down it at each press;
    ///     the mark is three bone ticks circling the enemy. Sympathy (`SympBound`): dashed threads from the mark to each bound
    ///     enemy, a bone ring on each and a ring round the mark that empties over its 4 s.
    ///   - Each move (FxKind.BsBlow, drawn from its age; the blow lands at Game.BsHitDelay): Pulse, a phantom fist driving in
    ///     from your side and blood thrown out the far side; Reel, the thread snapping taut with chevrons running back to you;
    ///     Heart Lift, blood spikes erupting under it while it rides up off its shadow (BsLiftOf, read by DrawEnemy); Marrow
    ///     Burst, veins swelling, then a flash, a burst and bone fragments flying (the cutting shards are Defs.BoneSpearShot
    ///     bullets). Sympathy echoes draw smaller and fainter.
    public partial class ArenaApp
    {
        static readonly Rgba BsDark = Hx(0x5a0610), BsRedC = Hx(0xe0283e), BsBright = Hx(0xff5a6a), BsHot = Hx(0xffd8dc);
        static readonly Rgba BsBoneC = Hx(0xefe4cc), BsBoneShade = Hx(0xb9a98a);
        const float BsLiftPx = 38f;

        readonly Dictionary<int, float> bsLift = new Dictionary<int, float>();
        float bsLiftAt = -1f;
        Game bsLiftGame;

        static float BsEase(float k) => 1f - (1f - k) * (1f - k) * (1f - k);

        /// How high (px) enemy `e` rides above its shadow: Heart Lift throws it up and lets it drop.
        float BsLiftOf(Enemy e)
        {
            if (bsLiftGame != game || bsLiftAt != game.AnimT)
            {
                bsLiftGame = game;
                bsLiftAt = game.AnimT;
                bsLift.Clear();
                foreach (var fx in game.Effects)
                {
                    if (fx.Kind != FxKind.BsBlow)
                        continue;
                    int code = (int)(fx.Radius + 0.5f);
                    if (code % 10 != 3)
                        continue;
                    float a = fx.T - Game.BsLiftHit;
                    if (a <= 0f || a >= Game.BsLiftAir)
                        continue;
                    float h = BsLiftPx * Mathf.Sin(MathX.Pi * a / Game.BsLiftAir) * (code >= 10 ? 0.7f : 1f);
                    int id = (int)fx.Pos2.X;
                    if (!bsLift.TryGetValue(id, out var was) || was < h)
                        bsLift[id] = h;
                }
            }
            return bsLift.Count > 0 && bsLift.TryGetValue(e.Id, out var lift) ? lift : 0f;
        }

        /// Where an enemy is drawn (lifted by Heart Lift).
        V2 BsBody(Enemy e) => e.Disp - new V2(0f, BsLiftOf(e));

        static V2 BsHand(Player p) => p.Disp + V2.FromAngle(p.Aim) * 14f;

        static V2 BsBez(V2 a, V2 c, V2 b, float u)
        {
            float v = 1f - u;
            return a * (v * v) + c * (2f * v * u) + b * (u * u);
        }

        /// The newest blow `p` sent on enemy `id` (null: none playing).
        Effect BsLatest(Player p, int id, bool echoes)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.BsBlow && (int)fx.Pos2.Y == p.Id && (id < 0 || (int)fx.Pos2.X == id)
                    && (echoes || fx.Radius < 9.5f) && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// Called for each player with a mark or Sympathy up.
        void DrawBsThread(Player p, float t)
        {
            if (p.Downed || p.Weapon.Id != WeaponId.BoneSpear)
                return;
            if (p.SympT > 0f && game.Enemies.TryGetValue(p.SympSrc, out var src) && src.Hp > 0f)
            {
                V2 sp = BsBody(src);
                float fade = Mathf.Clamp01(p.SympT / 0.4f);
                foreach (int id in p.SympBound)
                    if (game.Enemies.TryGetValue(id, out var o) && o.Hp > 0f)
                    {
                        V2 op = BsBody(o), d = op - sp;
                        float len = d.Length;
                        if (len < 4f)
                            continue;
                        int n = Mathf.Max(4, (int)(len / 10f));
                        for (int i = 0; i < n; i += 2)  // dashed
                            Line(sp + d * ((float)i / n), sp + d * ((float)(i + 1) / n), 1.6f, BsRedC.WithAlpha(0.75f * fade));
                        float f = MathX.Fmod(t * 0.9f + id * 0.37f, 1f);
                        Circle(sp + d * f, 2f, BsBright.WithAlpha(0.9f * fade), 8);
                        Arc(op, o.Radius + 6f, 1.4f, BsBoneShade.WithAlpha(0.8f * fade), 24, t * 1.5f + id, MathX.Tau * 0.75f);
                    }
                float left = Mathf.Clamp01(p.SympT / Game.SympTime);
                Arc(sp, src.Radius + 15f, 2.5f, BsDark.WithAlpha(0.6f * fade), 32);
                Arc(sp, src.Radius + 15f, 2.5f, BsBright.WithAlpha(0.9f * fade), 32, -MathX.Pi / 2f, MathX.Tau * left);
            }
            if (p.BsTarget == 0 || p.BsT <= 0f || !game.Enemies.TryGetValue(p.BsTarget, out var e) || e.Hp <= 0f)
                return;
            float alpha = Mathf.Clamp01(p.BsT / 0.4f);
            V2 hand = BsHand(p), to = BsBody(e), dd = to - hand;
            float L = dd.Length;
            if (L < 6f)
                return;
            var last = BsLatest(p, e.Id, false);
            int lastStep = last != null ? (int)(last.Radius + 0.5f) % 10 : 0;
            bool taut = last != null && lastStep == 2 && last.T < Game.BsReelHit + 0.3f;
            V2 nrm = (dd / L).Orthogonal();
            float sag = taut ? 0f : Mathf.Min(30f, L * 0.07f) + 5f * Mathf.Sin(t * 2.6f + e.Id);
            V2 c = (hand + to) * 0.5f + nrm * sag + new V2(0f, taut ? 0f : sag * 0.35f);
            int segs = Mathf.Clamp((int)(L / 12f), 8, 40);
            V2 prev = hand;
            for (int i = 1; i <= segs; i++)  // the glow, then the core
            {
                V2 pt = BsBez(hand, c, to, (float)i / segs);
                Line(prev, pt, taut ? 7f : 5f, (taut ? BsBright : BsRedC).WithAlpha((taut ? 0.3f : 0.16f) * alpha));
                prev = pt;
            }
            prev = hand;
            for (int i = 1; i <= segs; i++)
            {
                V2 pt = BsBez(hand, c, to, (float)i / segs);
                Line(prev, pt, taut ? 2.4f : 1.6f, (taut ? BsHot : BsRedC).WithAlpha(alpha));
                prev = pt;
            }
            for (int i = 0; i < 7; i++)  // beads of blood running down it to the mark
            {
                float u = MathX.Fmod(t * 1.3f + i / 7f, 1f);
                Circle(BsBez(hand, c, to, u), 1.9f, BsBright.WithAlpha(alpha * (0.5f + 0.5f * Mathf.Sin(u * MathX.Pi))), 8);
            }
            if (last != null && last.T < Game.BsHitDelay(lastStep) + 0.04f)  // a press: a glow runs down the thread
            {
                float u = Mathf.Clamp01(last.T / Mathf.Max(0.01f, Game.BsHitDelay(lastStep)));
                V2 g = BsBez(hand, c, to, u);
                Circle(g, 7f, BsBright.WithAlpha(0.35f * alpha), 12);
                Circle(g, 3.5f, BsHot.WithAlpha(alpha), 10);
            }
            Circle(hand, 4f + 1.2f * Mathf.Sin(t * 8f), BsBright.WithAlpha(0.45f * alpha), 10);
            // the mark: three bone ticks circling it
            float R = e.Radius + 9f;
            for (int i = 0; i < 3; i++)
            {
                float a = t * 1.8f + i * MathX.Tau / 3f;
                Arc(to, R, 2f, BsBoneC.WithAlpha(0.9f * alpha), 8, a, 0.6f);
                V2 dir = V2.FromAngle(a + 0.3f), tip = to + dir * (R + 7f), b = to + dir * (R + 1.5f);
                SetColor(BsBoneC.WithAlpha(alpha));
                Tri(tip, b + dir.Orthogonal() * 2.6f, b - dir.Orthogonal() * 2.6f);
            }
        }

        /// The phantom fist: knuckles along +dir at `at`, scale `s`.
        void BsFist(V2 at, V2 dir, float s, float a)
        {
            V2 n = dir.Orthogonal();
            float ang = Mathf.Atan2(dir.Y, dir.X);
            Quad(at - dir * (16f * s) - n * (6f * s), at + dir * (3f * s) - n * (9f * s), at + dir * (3f * s) + n * (9f * s), at - dir * (16f * s) + n * (6f * s), BsRedC.WithAlpha(0.35f * a));
            Quad(at - dir * (6f * s) - n * (8f * s), at + dir * (3f * s) - n * (9f * s), at + dir * (3f * s) + n * (9f * s), at - dir * (6f * s) + n * (8f * s), BsBright.WithAlpha(0.45f * a));
            for (int i = 0; i < 4; i++)
            {
                V2 k = at + dir * (6f * s) + n * ((-7.5f + 5f * i) * s);
                Ellipse(k, 5.4f * s, 3.5f * s, ang, BsHot.WithAlpha(0.8f * a), 10);
                Ellipse(k, 4.4f * s, 2.6f * s, ang, BsBright.WithAlpha(0.95f * a), 10);
            }
        }

        static float BsHash(float x)
        {
            float f = MathX.Fmod(Mathf.Sin(x * 12.9898f) * 43758.547f, 1f);
            return f < 0f ? f + 1f : f;
        }

        /// FxKind.BsBlow: one move on one enemy, from its age.
        void DrawBsBlowFx(Effect fx)
        {
            int code = (int)(fx.Radius + 0.5f), step = code % 10;
            bool echo = code >= 10;
            float s = echo ? 0.7f : 1f, al = echo ? 0.7f : 1f;
            float hit = Game.BsHitDelay(step), a = fx.T - hit, tail = Mathf.Max(0.01f, fx.Dur - hit);
            float r = 14f;
            V2 at = fx.Pos, ground = fx.Pos;
            if (game.Enemies.TryGetValue((int)fx.Pos2.X, out var e))
            {
                r = e.Radius;
                ground = e.Disp;
                at = BsBody(e);
            }
            V2 from = game.Players.TryGetValue((int)fx.Pos2.Y, out var p) ? BsHand(p) : at - new V2(200f, 0f);
            V2 dir = (at - from).Normalized();
            if (dir.IsZero)
                dir = new V2(1f, 0f);
            V2 n = dir.Orthogonal();
            float seed = fx.Pos.X * 0.37f + fx.Pos.Y * 0.71f + step;
            switch (step)
            {
                case 1:  // Pulse
                    if (a < 0f)
                    {
                        float q = BsEase(1f + a / hit);
                        V2 fp = at - dir * (r + 6f + 50f * (1f - q) * s);
                        for (int i = -1; i <= 1; i++)
                            Line(fp - dir * (20f * s) + n * (i * 6f * s), fp - dir * (36f * s) + n * (i * 6f * s), 1.2f, BsBright.WithAlpha(0.5f * al));
                        BsFist(fp, dir, s, al);
                    }
                    else
                    {
                        float k = Mathf.Clamp01(a / tail);
                        BsFist(at - dir * (r * 0.5f), dir, s * (1f + 0.5f * k), al * (1f - k));
                        float ang = Mathf.Atan2(dir.Y, dir.X);
                        Arc(at, r + 4f + 20f * BsEase(k) * s, 3f * (1f - k) + 0.5f, BsHot.WithAlpha(al * (1f - k)), 16, ang - 1.1f, 2.2f);
                        for (int i = 0; i < 9; i++)  // blood thrown out the far side
                        {
                            float sp = (BsHash(seed + i) - 0.5f) * 1.3f, dist = (10f + 55f * BsHash(seed + i * 3.1f)) * BsEase(k) * s;
                            V2 dp = at + V2.FromAngle(ang + sp) * (r * 0.6f + dist);
                            Circle(dp, 2.4f * (1f - 0.5f * k), BsRedC.WithAlpha(al * (1f - k)), 8);
                        }
                    }
                    break;
                case 2:  // Reel
                {
                    float k = a < 0f ? 0f : Mathf.Clamp01(a / tail);
                    if (a >= 0f)
                    {
                        Line(from, at, 4f * (1f - k) + 1f, BsHot.WithAlpha(0.7f * al * (1f - k)));
                        for (int i = 0; i < 4; i++)  // chevrons running back to the hand
                        {
                            float u = MathX.Fmod(k * 2.2f + i / 4f, 1f);
                            V2 c = V2.Lerp(at, from, u * 0.8f);
                            float cs = 6f * s;
                            Line(c - dir * cs + n * cs, c - dir * (cs * 2f), 2f, BsBright.WithAlpha(al * (1f - k)));
                            Line(c - dir * cs - n * cs, c - dir * (cs * 2f), 2f, BsBright.WithAlpha(al * (1f - k)));
                        }
                        for (int i = 0; i < 6; i++)  // drops dragged out behind it
                        {
                            V2 dp = at + dir * (r + 4f + 26f * BsHash(seed + i) * BsEase(k)) + n * ((BsHash(seed + i * 2.3f) - 0.5f) * r * 1.4f);
                            Circle(dp, 2f, BsRedC.WithAlpha(al * (1f - k)), 8);
                        }
                    }
                    Arc(at, r + 16f - 10f * (a < 0f ? 0f : BsEase(k)), 2.5f, BsBright.WithAlpha(al * (a < 0f ? 0.5f : 1f - k)), 24);
                    break;
                }
                case 3:  // Heart Lift
                {
                    if (a < 0f)
                    {
                        float q = 1f + a / hit;
                        Ellipse(ground, (r + 18f - 8f * q) * s, (r + 18f - 8f * q) * 0.45f * s, 0f, BsDark.WithAlpha(0.5f * al), 20);
                        break;
                    }
                    float k = Mathf.Clamp01(a / tail), grow = BsEase(Mathf.Min(1f, k * 3f)), fade = 1f - Mathf.Clamp01((k - 0.5f) / 0.5f);
                    Ellipse(ground, (r + 10f) * grow * s, (r + 10f) * 0.45f * grow * s, 0f, BsDark.WithAlpha(0.6f * fade * al), 20);
                    for (int i = 0; i < 8; i++)  // blood spikes erupting under it
                    {
                        float ang = i * MathX.Tau / 8f + seed;
                        V2 b = ground + new V2(Mathf.Cos(ang) * (r + 2f), Mathf.Sin(ang) * (r + 2f) * 0.45f) * grow * s;
                        float h = (16f + (i % 3) * 9f) * Mathf.Sin(MathX.Pi * Mathf.Min(1f, k * 1.6f)) * s;
                        if (h < 1f)
                            continue;
                        SetColor((i % 2 == 0 ? BsRedC : BsBright).WithAlpha(fade * al));
                        Tri(b - new V2(3f * s, 0f), b + new V2(Mathf.Cos(ang) * 4f, -h), b + new V2(3f * s, 0f));
                    }
                    for (int i = 0; i < 4; i++)  // drips falling off it
                    {
                        float u = MathX.Fmod(k * 3f + i * 0.25f, 1f);
                        V2 dp = V2.Lerp(at + new V2((BsHash(seed + i) - 0.5f) * r, r * 0.5f), ground, u);
                        Circle(dp, 1.8f, BsRedC.WithAlpha(fade * al * (1f - u)), 6);
                    }
                    break;
                }
                default:  // Marrow Burst
                {
                    if (a < 0f)
                    {
                        float q = 1f + a / hit;
                        Circle(at, r * (1f + 0.35f * q), BsRedC.WithAlpha(0.25f * q * al), 20);
                        for (int i = 0; i < 6; i++)  // veins swelling
                        {
                            V2 v = V2.FromAngle(i * MathX.Tau / 6f + seed);
                            Line(at + v * (r * 0.2f), at + v * (r * (0.5f + 0.8f * q)), 1.4f + q, BsBright.WithAlpha(q * al));
                        }
                        Arc(at, r + 4f + 4f * Mathf.Sin(q * 20f), 1.5f, BsHot.WithAlpha(q * al), 20);
                        break;
                    }
                    float k = Mathf.Clamp01(a / tail);
                    if (a < 0.06f)
                        Circle(at, r + 14f * s, BsHot.WithAlpha(0.85f * al * (1f - a / 0.06f)), 20);
                    Circle(at, (r + 46f * BsEase(k)) * s, BsDark.WithAlpha(0.5f * al * (1f - k)), 28);
                    Arc(at, (r + 50f * BsEase(k)) * s, 5f * (1f - k) + 1f, BsBright.WithAlpha(al * (1f - k)), 32);
                    for (int i = 0; i < 10; i++)  // bone fragments
                    {
                        float ang = BsHash(seed + i * 1.7f) * MathX.Tau, dist = (r + (30f + 60f * BsHash(seed + i)) * BsEase(k)) * s;
                        V2 fd = V2.FromAngle(ang), fp = at + fd * dist, sd = V2.FromAngle(ang + k * 9f + i);
                        SetColor((i % 3 == 0 ? BsBoneShade : BsBoneC).WithAlpha(al * (1f - k)));
                        Tri(fp + sd * 5f, fp - sd * 3f + sd.Orthogonal() * 2f, fp - sd * 3f - sd.Orthogonal() * 2f);
                    }
                    for (int i = 0; i < 12; i++)  // the blood crown
                    {
                        V2 dp = at + V2.FromAngle(i * MathX.Tau / 12f + seed) * ((r + 24f * BsEase(k)) * s);
                        Circle(dp, 2.6f * (1f - 0.6f * k), BsRedC.WithAlpha(al * (1f - k)), 8);
                    }
                    break;
                }
            }
        }
    }
}
