using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Knight's Halberd, held and swung (Docs/Mockups/halberd-swing.html, the Cleaver look with the 0.12 s wind-up). Drawing
    /// only, with Visual effects on; the motion's numbers are Core/HalberdMotion.cs (the host times each step's hit from them).
    ///   - held: both gloved hands on the pole, each joined by an arm to a pauldron that turns with the swing; the blade is
    ///     carried raised and has a shadow on the floor (DrawHalberdIdle, from the player loop: the model leaves its stick out);
    ///   - swung: the click's effect (FxKind.Slash, its Dur is HalberdMotion.Total) plays the pull back, the sweep, the head
    ///     turning over for the return sweep, the follow-through and the recovery (DrawHalberdFx, from DrawSlashLayer);
    ///   - Cleaver: a short hot trail, three lagging ghosts of the head, dust kicked up along the tip's path, orange sparks,
    ///     a dust ring and a cut mark on each enemy the edge reaches, and a small shake on your own hits.
    public partial class ArenaApp
    {
        static readonly Rgba HalberdSteelHi = new Rgba(0.82f, 0.85f, 0.9f), HalberdSteelLo = new Rgba(0.43f, 0.48f, 0.57f), HalberdEdge = new Rgba(1f, 0.81f, 0.59f),
            HalberdOrange = new Rgba(1f, 0.67f, 0.36f), HalberdHot = new Rgba(1f, 0.93f, 0.78f), HalberdWood = new Rgba(0.18f, 0.14f, 0.09f),
            HalberdLeather = new Rgba(0.36f, 0.25f, 0.16f), HalberdBrass = new Rgba(0.72f, 0.59f, 0.35f);

        // The axe blade's outline in the head's own frame (u out along the pole, v across it, in px): sampled once.
        static readonly float[] BladeU = BladeOutline(true), BladeV = BladeOutline(false);
        const int TrailN = 12;
        const float TrailSpan = 0.1f;

        static float[] BladeOutline(bool u)
        {
            var pts = new List<float>();
            for (int i = 0; i <= 6; i++)  // the edge's belly: a curve from the spike's root out to the point
            {
                float t = i / 6f, m = 1f - t;
                pts.Add(u ? m * m * m * -13f + 3f * m * m * t * -13f + 3f * m * t * t * -20f + t * t * t * -31f
                          : m * m * m * 3f + 3f * m * m * t * 15f + 3f * m * t * t * 26f + t * t * t * 27.5f);
            }
            for (int i = 1; i <= 5; i++)  // back along the beard to the socket
            {
                float t = i / 5f, m = 1f - t;
                pts.Add(u ? m * m * -31f + 2f * m * t * -52f + t * t * -59f : m * m * 27.5f + 2f * m * t * 25f + t * t * 10f);
            }
            pts.Add(u ? -57f : 3f);
            return pts.ToArray();
        }

        // the head's frame while one is drawn: HP(u, v) is a point on it (the roll squeezes v: edge-on halfway through a turn)
        V2 hTip, hDir, hSide;
        float hSc, hCr;
        V2 HP(float u, float v) => hTip + hDir * (u * hSc) + hSide * (v * hSc * hCr);

        readonly Dictionary<int, Effect> halberdNewest = new Dictionary<int, Effect>();
        readonly V2[] trTip = new V2[TrailN + 1], trDir = new V2[TrailN + 1];
        readonly float[] trR = new float[TrailN + 1];
        readonly bool[] trOk = new bool[TrailN + 1];

        /// A Halberd effect: the click's whole motion. (The old effect had a Dur of 0.3 s; none of the other Slash effects last this long.)
        static bool IsHalberdFx(Effect fx) => fx.Kind == FxKind.Slash && fx.Radius >= 0.2f
            && (Mathf.Abs(fx.Dur - HalberdMotion.Total(false)) < 0.002f || Mathf.Abs(fx.Dur - HalberdMotion.Total(true)) < 0.002f);

        static bool HalberdUp(Effect fx) => Mathf.Abs(fx.Dur - HalberdMotion.Total(true)) < 0.002f;

        /// True while this player's swing effect is on screen (the idle pose waits). The swing starts where you stand.
        bool HalberdBusy(Player p)
        {
            foreach (var fx in game.Effects)
            {
                if (!IsHalberdFx(fx) || fx.T >= fx.Dur - 0.001f)
                    continue;
                // The swing is drawn on its owner's body as they move (MeleeOrigin), so match by owner once it's known; moving away from
                // where the swing started must not let the idle Halberd show up beside it. (Not yet seen: where it started.)
                MeleeSeen seen;
                if (meleeSeen.TryGetValue(fx, out seen) ? seen.Owner == p.Id : (fx.Pos.DistanceTo(p.Pos) < 44f || fx.Pos.DistanceTo(p.Disp) < 44f))
                    return true;
            }
            return false;
        }

        /// The Halberd held at the ready (no swing playing): drawn over the body, in place of the model's own stick.
        void DrawHalberdIdle(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.Halberd || HalberdBusy(p))
                return;
            var m = new HalberdMotion.Pose { A = HalberdMotion.RestA + 0.02f * Mathf.Sin(t * 2.6f), K = HalberdMotion.RestK + 0.008f * Mathf.Sin(t * 2.6f), Lift = 0.5f };
            DrawHalberd(p.Disp, p.Aim, m, Game.MeleeReach(p, p.Weapon.Reach), p.Def.Color, 1f, false);
        }

        /// A Halberd swing in the Cleaver look: the pose now, plus the trail and the hits. `light` is the additive pass.
        bool DrawHalberdFx(Effect fx, bool light)
        {
            bool up = HalberdUp(fx);
            var seen = MeleeOwnerOf(fx, fx.Dur, false, 0.01f);
            if (seen == null)
                return false;
            Effect cur;
            bool have = halberdNewest.TryGetValue(seen.Owner, out cur);
            if (!have || cur == fx || cur.T >= cur.Dur || fx.T < cur.T)
                halberdNewest[seen.Owner] = fx;
            else
                return true;  // a newer swing by the same player has taken over
            float c = fx.T, aim = (fx.Pos2 - fx.Pos).Angle, reach = seen.BaseReach;
            V2 o = MeleeOrigin(fx, seen);
            Player owner;
            game.Players.TryGetValue(seen.Owner, out owner);
            float end = HalberdMotion.End(up) + HalberdMotion.Follow;
            if (owner != null && c > end)  // coming back to the ready: turn to where you are aiming now
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((c - end) / HalberdMotion.Recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            var m = HalberdMotion.At(c, up);
            Rgba arm = owner != null ? owner.Def.Color : default(Rgba);
            int seed = MeleeSeed(fx);

            SampleHalberdTrail(o, aim, reach, c, up);
            if (light)
            {
                // the hot trail: a short ribbon from the blade's belly to its tip, brighter along the tip
                for (int i = 0; i < TrailN; i++)
                {
                    if (!trOk[i] || !trOk[i + 1])
                        continue;
                    float a0 = 1f - (float)i / TrailN, a1 = 1f - (float)(i + 1) / TrailN;
                    a0 = a0 * a0; a1 = a1 * a1;
                    V2 i0 = o + trDir[i] * (trR[i] - 60f), i1 = o + trDir[i + 1] * (trR[i + 1] - 60f);
                    VTri(trTip[i], HalberdOrange.WithAlpha(0.34f * a0), trTip[i + 1], HalberdOrange.WithAlpha(0.34f * a1), i1, HalberdOrange.WithAlpha(0f));
                    VTri(trTip[i], HalberdOrange.WithAlpha(0.34f * a0), i1, HalberdOrange.WithAlpha(0f), i0, HalberdOrange.WithAlpha(0f));
                    V2 j0 = o + trDir[i] * (trR[i] - 16f), j1 = o + trDir[i + 1] * (trR[i + 1] - 16f);
                    VTri(trTip[i], HalberdHot.WithAlpha(0.5f * a0), trTip[i + 1], HalberdHot.WithAlpha(0.5f * a1), j1, HalberdHot.WithAlpha(0f));
                    VTri(trTip[i], HalberdHot.WithAlpha(0.5f * a0), j1, HalberdHot.WithAlpha(0f), j0, HalberdHot.WithAlpha(0f));
                }
                HalberdHits(o, aim, reach, c, up, seen, seed, true);
                return true;
            }

            HalberdDust(o, aim, reach, c, up);
            for (int i = 0; i < TrailN; i++)  // the tip's own bright line
                if (trOk[i] && trOk[i + 1])
                    Line(trTip[i], trTip[i + 1], 2.4f, new Rgba(1f, 0.94f, 0.86f, 0.85f * (1f - (float)i / TrailN)));
            if (m.Pass > 0)  // three lagging ghosts of the head
                for (int g = 1; g <= 3; g++)
                {
                    var gp = HalberdMotion.At(c - 0.03f * g, up);
                    if (gp.Pass > 0)
                        DrawHalberd(o, aim, gp, reach, arm, 0.34f - 0.1f * (g - 1), true);
                }
            DrawHalberd(o, aim, m, reach, arm, 1f, false);
            HalberdHits(o, aim, reach, c, up, seen, seed, false);
            return true;
        }

        void SampleHalberdTrail(V2 o, float aim, float reach, float c, bool up)
        {
            for (int i = 0; i <= TrailN; i++)
            {
                float t = c - TrailSpan * i / TrailN;
                var m = t > 0f ? HalberdMotion.At(t, up) : new HalberdMotion.Pose();
                trOk[i] = t > 0f && m.Pass > 0;
                trDir[i] = V2.FromAngle(aim + m.A);
                trR[i] = reach * m.K;
                trTip[i] = o + trDir[i] * trR[i];
            }
        }

        /// Dust kicked up along the tip's path, drifting out the way the blade went.
        void HalberdDust(V2 o, float aim, float reach, float c, bool up)
        {
            for (int pass = 1; pass <= (up ? 2 : 1); pass++)
            {
                float ps = pass == 1 ? HalberdMotion.Start1 : HalberdMotion.Start2, sd = pass == 1 ? 1f : -1f;
                for (int j = 0; j < 16; j++)
                {
                    float tj = ps + j * HalberdMotion.SweepTime / 16f, age = c - tj;
                    if (age <= 0f || age > 0.6f)
                        continue;
                    var mj = HalberdMotion.At(tj, up);
                    uint r = (uint)(j * 977 + pass * 131);
                    float jit = (Rand01(ref r) - 0.5f) * 8f, spread = 10f + Rand01(ref r) * 22f;
                    V2 d = V2.FromAngle(aim + mj.A), side = new V2(-d.Y, d.X);
                    V2 at = o + d * (reach * mj.K + jit + age * 26f) + side * (sd * age * spread);
                    float u = age / 0.6f;
                    Circle(at, 3f + u * 11f, new Rgba(0.69f, 0.59f, 0.47f, 0.24f * (1f - u) * (1f - u)), 10);
                }
            }
        }

        /// What the edge does to each enemy it reaches, timed to the blade getting there (worked out from the motion and
        /// where the enemies are drawn: the host's hits are its own): a white flash, orange sparks, a dust ring and a cut
        /// mark across it. Your own first cut of each sweep holds the picture a moment and shakes the screen.
        void HalberdHits(V2 o, float aim, float reach, float c, bool up, MeleeSeen seen, int seed, bool light)
        {
            for (int pass = 1; pass <= (up ? 2 : 1); pass++)
            {
                float first = 99f;
                foreach (var e in game.Enemies.Values)
                {
                    V2 rel = e.Disp - o;
                    float d = rel.Length;
                    if (d - e.Radius > reach || d < 1f)
                        continue;
                    float diff = Mathf.Repeat(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                    if (Mathf.Abs(diff) > HalberdMotion.Sweep + 0.2f)
                        continue;
                    float hitT = HalberdMotion.CrossTime(Mathf.Clamp(diff, -HalberdMotion.Sweep, HalberdMotion.Sweep), pass), q = c - hitT;
                    first = Mathf.Min(first, hitT);
                    if (q < 0f || q > 0.55f)
                        continue;
                    bool big = pass == 1;
                    float sc = big ? 1f : 0.7f;
                    V2 at = e.Disp;
                    if (light)
                    {
                        if (q < 0.06f)
                            GlowDisc(at, 30f * sc, HalberdOrange, 0.9f * (1f - q / 0.06f));
                        continue;
                    }
                    int es = seed + e.Id * 31 + pass * 7;
                    uint r = (uint)es;
                    if (q < 0.4f)
                        for (int i = 0; i < (big ? 14 : 8); i++)  // sparks thrown off the cut
                        {
                            float th = aim + diff + (Rand01(ref r) - 0.5f) * 2.2f, sp = (130f + 240f * Rand01(ref r)) * sc, rr = sp * q, l = 7f + 9f * Rand01(ref r);
                            V2 dd = V2.FromAngle(th);
                            Line(at + dd * rr, at + dd * (rr + l), 2f, (i % 3 == 0 ? HalberdHot : HalberdOrange).WithAlpha(1f - q / 0.4f));
                        }
                    if (q < 0.45f)
                        Arc(at, 6f + q * 95f * sc, 5f * (1f - q / 0.45f) + 1f, new Rgba(0.59f, 0.49f, 0.38f, 0.4f * (1f - q / 0.45f)), 18);
                    if (q < 0.45f)  // the cut mark, along the way the blade travelled
                    {
                        float al = 1f - q / 0.45f, ba = aim + HalberdMotion.At(hitT, up).A;
                        V2 n = new V2(-Mathf.Sin(ba), Mathf.Cos(ba));
                        Line(at - n * 20f, at + n * 20f, 3.2f, HalberdOrange.WithAlpha(0.7f * al));
                        Line(at - n * 20f, at + n * 20f, 1.3f, Rgba.White.WithAlpha(al));
                    }
                }
                int bit = 1 << pass;
                if (!light && (seen.Fired & bit) == 0 && first < 90f && c >= first)
                {
                    seen.Fired |= bit;
                    if (seen.Owner == game.MyId)
                        HalberdOwnCut(pass == 1);
                }
            }
        }

        /// The weight of your own cut: a hit-stop and a kick, a little less on the return sweep.
        void HalberdOwnCut(bool first)
        {
            Hold(first ? 0.04f : 0.03f, 0.15f);
            Shake(first ? 0.12f : 0.08f);
        }

        /// The Halberd itself at a pose: shadow on the floor, arms and gloves, the pole and its axe head. `ghost`: just a
        /// faint copy of the head.
        void DrawHalberd(V2 o, float aim, HalberdMotion.Pose m, float reach, Rgba armCol, float alpha, bool ghost)
        {
            if (alpha <= 0f)
                return;
            float a = aim + m.A, tw = 0.3f * m.A;
            V2 dir = V2.FromAngle(a), side = new V2(-dir.Y, dir.X);
            float tipR = reach * m.K, sc = 1f + 0.2f * m.Lift, cr = Mathf.Cos(m.Roll), pole = reach * 0.95f;
            for (int pass = 0; pass < 2; pass++)  // 0: its shadow on the floor, 1: the weapon
            {
                bool sh = pass == 0;
                if (ghost && sh)
                    continue;
                V2 off = sh ? new V2(5f * m.Lift + 2f, 7f * m.Lift + 3f) : new V2(0f, 0f);
                hTip = o + dir * tipR + off; hDir = dir; hSide = side; hSc = sc; hCr = cr;
                V2 but = hTip - dir * pole;
                Rgba shc = new Rgba(0f, 0f, 0f, 0.26f * alpha);
                if (!sh && !ghost)
                {
                    DrawHalberdArms(o, aim, a, tw, armCol, alpha);
                }
                if (!ghost)
                {
                    Line(but, hTip - dir * 24f, 6.2f * sc, sh ? shc : HalberdWood.WithAlpha(alpha));
                    if (!sh)
                    {
                        Line(but, hTip - dir * 24f, 3.6f * sc, MeleeHandle.WithAlpha(alpha));
                        for (int g = 0; g < 2; g++)  // the grips, where the hands are
                        {
                            float gd = g == 0 ? 16f : 40f;
                            Line(o + dir * (gd - 10f), o + dir * (gd + 10f), 7.4f * sc, new Rgba(0.14f, 0.09f, 0.06f, alpha));
                            Line(o + dir * (gd - 10f), o + dir * (gd + 10f), 4.8f * sc, HalberdLeather.WithAlpha(alpha));
                        }
                        Circle(but, 4.4f * sc, new Rgba(0.1f, 0.13f, 0.17f, alpha), 10);
                        Circle(but, 3.4f * sc, new Rgba(0.6f, 0.65f, 0.72f, alpha), 10);
                    }
                    else
                        Circle(but, 4.4f * sc, shc, 10);
                }
                DrawHalberdHead(sh, ghost, alpha, shc);
            }
            if (!ghost)
                DrawHalberdHands(o, dir, alpha);
        }

        void DrawHalberdHead(bool sh, bool ghost, float alpha, Rgba shc)
        {
            Rgba hi = HalberdSteelHi.WithAlpha(alpha), lo = HalberdSteelLo.WithAlpha(alpha), outl = new Rgba(0.03f, 0.05f, 0.07f, 0.85f * alpha);
            if (sh)
            {
                hi = lo = outl = shc;
            }
            // langets up the shaft, the spike, the fluke on the far side
            SetColor(sh ? shc : new Rgba(0.49f, 0.53f, 0.6f, alpha));
            Quad(HP(-62f, -1.8f), HP(-14f, -1.8f), HP(-14f, 1.8f), HP(-62f, 1.8f));
            SetColor(lo);
            Tri(HP(-27f, -2.5f), HP(-41f, -18f), HP(-51f, -3f));
            SetColor(hi);
            Tri(HP(1.5f, 0f), HP(-17f, -4f), HP(-17f, 4f));
            // the axe blade: a dark rim, the steel, a lighter middle
            float cu = -35f, cv = 9f;
            V2 c0 = HP(cu, cv);
            if (!sh)
                for (int i = 0; i < BladeU.Length; i++)
                {
                    int j = (i + 1) % BladeU.Length;
                    SetColor(outl);
                    Tri(c0, HP(cu + (BladeU[i] - cu) * 1.07f, cv + (BladeV[i] - cv) * 1.07f), HP(cu + (BladeU[j] - cu) * 1.07f, cv + (BladeV[j] - cv) * 1.07f));
                }
            for (int i = 0; i < BladeU.Length; i++)
            {
                int j = (i + 1) % BladeU.Length;
                SetColor(lo);
                Tri(c0, HP(BladeU[i], BladeV[i]), HP(BladeU[j], BladeV[j]));
            }
            if (!sh)
            {
                for (int i = 0; i < BladeU.Length; i++)  // the lit middle, toward the spike
                {
                    int j = (i + 1) % BladeU.Length;
                    SetColor(hi);
                    V2 m0 = HP(cu + 8f + (BladeU[i] - cu) * 0.55f, cv + (BladeV[i] - cv) * 0.55f), m1 = HP(cu + 8f + (BladeU[j] - cu) * 0.55f, cv + (BladeV[j] - cv) * 0.55f);
                    Tri(HP(cu + 8f, cv), m0, m1);
                }
                for (int i = 1; i < 11; i++)  // the cutting edge, hot
                    Line(HP(BladeU[i - 1] * 0.98f - 0.3f, BladeV[i - 1] - 0.8f), HP(BladeU[i] * 0.98f - 0.3f, BladeV[i] - 0.8f), 1.7f * hSc, HalberdEdge.WithAlpha(0.95f * alpha));
            }
            SetColor(sh ? shc : HalberdBrass.WithAlpha(alpha));  // the socket's brass band
            Quad(HP(-63f, -4.8f), HP(-51f, -4.8f), HP(-51f, 4.8f), HP(-63f, 4.8f));
        }

        /// Both arms, from a pauldron on each shoulder (turned with the swing) out to the gloves on the pole.
        void DrawHalberdArms(V2 o, float aim, float a, float tw, Rgba armCol, float alpha)
        {
            V2 dir = V2.FromAngle(a);
            Rgba armc = armCol.A > 0f ? armCol : new Rgba(0.29f, 0.66f, 0.61f);
            for (int k = 0; k < 2; k++)
            {
                V2 s = o + HalberdRot(new V2(2f, k == 0 ? -11.5f : 11.5f), aim + tw), hand = o + dir * (k == 0 ? 40f : 16f);
                V2 d = hand - s, n = new V2(-d.Y, d.X).Normalized();
                V2 sOff = s - o;
                if (n.X * sOff.X + n.Y * sOff.Y < 0f)
                    n = n * -1f;
                V2 elbow = (s + hand) * 0.5f + n * 6f;
                Line(s, elbow, 8.2f, new Rgba(0.05f, 0.18f, 0.17f, alpha));
                Line(elbow, hand, 8.2f, new Rgba(0.05f, 0.18f, 0.17f, alpha));
                Line(s, elbow, 5.6f, armc.WithAlpha(alpha));
                Line(elbow, hand, 5.6f, armc.WithAlpha(alpha));
                Circle(s, 7.2f, new Rgba(0.05f, 0.18f, 0.17f, alpha), 12);
                Circle(s, 6f, new Rgba(0.66f, 0.72f, 0.8f, alpha), 12);
                Circle(s - new V2(0.8f, 1.2f), 2.6f, new Rgba(0.84f, 0.88f, 0.93f, alpha), 8);
            }
        }

        void DrawHalberdHands(V2 o, V2 dir, float alpha)
        {
            for (int k = 0; k < 2; k++)
            {
                V2 h = o + dir * (k == 0 ? 40f : 16f);
                float r = k == 0 ? 6f : 5.4f;
                Circle(h, r + 1.3f, new Rgba(0.16f, 0.11f, 0.06f, alpha), 14);
                Circle(h, r, new Rgba(0.72f, 0.56f, 0.34f, alpha), 14);
                Circle(h - new V2(1.4f, 1.8f), 1.8f, new Rgba(1f, 0.93f, 0.78f, 0.45f * alpha), 8);
            }
        }

        static V2 HalberdRot(V2 p, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new V2(p.X * c - p.Y * s, p.X * s + p.Y * c);
        }
    }
}
