using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Knight's Halberd, held, swung and set (Docs/Mockups/halberd-swing.html for the Cleaver look; halberd-rework.html for
    /// Reaper's Wheel and Set the Pike). Drawing only, with Visual effects on; the motion's numbers are Core/HalberdMotion.cs (the
    /// host times each step's hit from them) and the rules Core/Halberd.cs.
    ///   - held: both gloved hands on the pole, each joined by an arm to a pauldron that turns with the swing; the blade is
    ///     carried raised and has a shadow on the floor (DrawHalberdIdle, from the player loop: the model leaves its stick out);
    ///   - Reaper's Wheel: each step's effect (FxKind.HalberdStep, Dur = HalberdMotion.StepTotal) plays the step from its age:
    ///     the Sweep, the Return Sweep (the head turns over first), the Overhead Chop (raised high, edge down, dropped along the
    ///     aim: a crack in the floor, dust and a heavy kick) and the Reaping Wheel (a full turn round you, a ring of dust);
    ///   - Cleaver: a short hot trail, three lagging ghosts of the head, dust kicked up along the tip's path, orange sparks,
    ///     a dust ring and a cut mark on each enemy the edge reaches, and a small shake on your own hits;
    ///   - Set the Pike: the pole held level, point out, turning after HbPikeA (eased on screen), a faint arc at the point's
    ///     reach, and each poke (FxKind.HalberdPoke) jabbing the point out with a streak and sparks.
    public partial class ArenaApp
    {
        static readonly Rgba HalberdSteelHi = new Rgba(0.82f, 0.85f, 0.9f), HalberdSteelLo = new Rgba(0.43f, 0.48f, 0.57f), HalberdEdge = new Rgba(1f, 0.81f, 0.59f),
            HalberdOrange = new Rgba(1f, 0.67f, 0.36f), HalberdHot = new Rgba(1f, 0.93f, 0.78f), HalberdWood = new Rgba(0.18f, 0.14f, 0.09f),
            HalberdLeather = new Rgba(0.36f, 0.25f, 0.16f), HalberdBrass = new Rgba(0.72f, 0.59f, 0.35f), HalberdDust = new Rgba(0.69f, 0.59f, 0.47f);

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
        readonly HashSet<Effect> halberdKicked = new HashSet<Effect>();  // your own steps whose first cut (or the chop's landing) has kicked the screen
        readonly Dictionary<int, float> pikeShown = new Dictionary<int, float>();  // the set pike's angle as drawn, eased after HbPikeA
        readonly V2[] trTip = new V2[TrailN + 1], trDir = new V2[TrailN + 1];
        readonly float[] trR = new float[TrailN + 1];
        readonly bool[] trOk = new bool[TrailN + 1];

        static int HalberdStepOf(Effect fx) => Mathf.Clamp(Mathf.RoundToInt(fx.Radius) % 10, 1, 4);
        static int HalberdOwnerOf(Effect fx) => Mathf.FloorToInt(fx.Radius / 10f + 0.001f);

        /// True while this player has a Reaper's Wheel step playing (the idle pose waits).
        bool HalberdBusy(Player p)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.HalberdStep && fx.T < fx.Dur - 0.001f && HalberdOwnerOf(fx) == p.Id)
                    return true;
            return false;
        }

        /// The newest poke of player `id` still playing, or null.
        Effect HalberdPokeOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.HalberdPoke && fx.T < fx.Dur && Mathf.RoundToInt(fx.Radius) == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// The Halberd held at the ready (no swing playing), or set as a pike: drawn over the body, in place of the model's own stick.
        void DrawHalberdIdle(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.Halberd)
                return;
            if (p.HbSet && !p.Downed)
            {
                DrawPikeSet(p, t);
                return;
            }
            pikeShown.Remove(p.Id);
            if (HalberdBusy(p))
                return;
            var m = new HalberdMotion.Pose { A = HalberdMotion.RestA + 0.02f * Mathf.Sin(t * 2.6f), K = HalberdMotion.RestK + 0.008f * Mathf.Sin(t * 2.6f), Lift = 0.5f };
            DrawHalberd(p.Disp, p.Aim, m, Game.MeleeReach(p, p.Weapon.Reach), p.Def.Color, 1f, false);
        }

        /// The pike set: held level, point out at the replicated HbPikeA (eased here so 20 snapshots a second turn smoothly),
        /// a faint arc where the point reaches, and the point jabbing out while a poke plays.
        void DrawPikeSet(Player p, float t)
        {
            float shown;
            if (!pikeShown.TryGetValue(p.Id, out shown))
                shown = p.HbPikeA;
            shown += (Mathf.Repeat(p.HbPikeA - shown + MathX.Pi, MathX.Tau) - MathX.Pi) * 0.35f;
            pikeShown[p.Id] = shown;
            float baseReach = Game.MeleeReach(p, p.Weapon.Reach) * p.Stats.HalberdReachMult, reach = baseReach * Game.PikeReachK;
            float k = Game.PikeReachK * 0.86f;  // drawn back a little, ready to jab
            var poke = HalberdPokeOf(p.Id);
            if (poke != null)
            {
                reach = (poke.Pos2 - poke.Pos).Length;
                float u = poke.T / poke.Dur, kk = reach / baseReach;
                k = u < 0.22f ? Mathf.Lerp(k, kk, 1f - Mathf.Pow(1f - u / 0.22f, 3f)) : Mathf.Lerp(kk, k, Mathf.SmoothStep(0f, 1f, (u - 0.22f) / 0.78f));
            }
            // the point's reach: a thin arc either side of the pike, breathing
            float a0 = shown - 0.3f, pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            Arc(p.Disp, reach, 1.6f, Game.HbGlow.WithAlpha(0.16f + 0.08f * pulse), 16, a0, 0.6f);
            Arc(p.Disp, reach - 18f, 1f, Game.HbGlow.WithAlpha(0.08f), 16, a0 + 0.08f, 0.44f);
            var m = new HalberdMotion.Pose { A = 0f, K = k + 0.006f * Mathf.Sin(t * 3.1f), Roll = 0f, Lift = 0.12f };
            DrawHalberd(p.Disp, shown, m, baseReach, p.Def.Color, 1f, false);
        }

        /// A Reaper's Wheel step in the Cleaver look: the pose now, plus the trail and the hits. `light` is the additive pass;
        /// with Visual effects off, a plain arc or bar.
        void DrawHalberdStepFx(Effect fx, bool light)
        {
            int step = HalberdStepOf(fx), id = HalberdOwnerOf(fx);
            float c = fx.T, aim = (fx.Pos2 - fx.Pos).Angle, reach = (fx.Pos2 - fx.Pos).Length;
            Player owner;
            game.Players.TryGetValue(id, out owner);
            V2 o = owner != null ? owner.Disp : fx.Pos;
            if (!fxOn)
            {
                DrawHalberdStepPlain(fx, step, o, aim, reach);
                return;
            }
            if (owner != null && (owner.Parrying || owner.HbSet))
                return;  // (the parry took it back, or the pike went down)
            Effect cur;
            if (!halberdNewest.TryGetValue(id, out cur) || cur == fx || cur.T >= cur.Dur || fx.T < cur.T || !game.Effects.Contains(cur))
                halberdNewest[id] = fx;
            else
                return;  // a newer step by the same player has taken over
            float end = HalberdMotion.StepTotal(step) - HalberdMotion.Recover;
            if (owner != null && c > end)  // coming back to the ready: turn to where you are aiming now
            {
                float diff = Mathf.Repeat(owner.Aim - aim + MathX.Pi, MathX.Tau) - MathX.Pi, u = Mathf.Clamp01((c - end) / HalberdMotion.Recover);
                aim += diff * u * u * (3f - 2f * u);
            }
            var m = HalberdMotion.StepAt(step, c);
            Rgba arm = owner != null ? owner.Def.Color : default(Rgba);
            int seed = (int)(fx.Pos.X * 13f + fx.Pos.Y * 7f) + step * 101;

            SampleHalberdTrail(o, aim, reach, c, step);
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
                if (step == 3)
                    ChopGlow(o, aim, reach, c);
                if (step == 4)
                    WheelGlow(o, reach, c);
                HalberdHits(o, aim, reach, c, step, id, seed, true);
                return;
            }

            HalberdDustTrail(o, aim, reach, c, step);
            if (step == 3)
                ChopCrack(o, aim, reach, c, seed);
            if (step == 4)
                WheelDust(o, reach, c, seed);
            for (int i = 0; i < TrailN; i++)  // the tip's own bright line
                if (trOk[i] && trOk[i + 1])
                    Line(trTip[i], trTip[i + 1], 2.4f, new Rgba(1f, 0.94f, 0.86f, 0.85f * (1f - (float)i / TrailN)));
            if (m.Pass > 0)  // three lagging ghosts of the head
                for (int g = 1; g <= 3; g++)
                {
                    var gp = HalberdMotion.StepAt(step, c - 0.03f * g);
                    if (gp.Pass > 0)
                        DrawHalberd(o, aim, gp, reach, arm, 0.34f - 0.1f * (g - 1), true);
                }
            DrawHalberd(o, aim, m, reach, arm, 1f, false);
            HalberdHits(o, aim, reach, c, step, id, seed, false);
            if (step == 3 && id == game.MyId && c >= HalberdMotion.ChopHit && halberdKicked.Add(fx))
            {
                halberdKicked.RemoveWhere(e => e.T >= e.Dur || !game.Effects.Contains(e));
                Hold(0.05f, 0.15f);  // the chop lands whether or not it hits: the floor takes it
                Shake(0.2f);
            }
        }

        /// Visual effects off: a plain arc for the sweeps and the wheel, a bar for the chop, while the edge moves.
        void DrawHalberdStepPlain(Effect fx, int step, V2 o, float aim, float reach)
        {
            float a, b;
            HalberdPassWindow(step, out a, out b);
            float f = (fx.T - a) / Mathf.Max(0.05f, b - a + 0.12f);
            if (f < 0f || f >= 1f)
                return;
            Rgba col = Game.HbGlow;
            if (step == 3)
            {
                V2 tip = o + V2.FromAngle(aim) * reach * HalberdMotion.ChopK;
                Line(o, tip, 2f * Game.HbChopHalfW * (1f - f), col.WithAlpha(0.3f * (1f - f)));
                Line(o, tip, 2.5f, Rgba.White.WithAlpha(0.9f * (1f - f)));
                return;
            }
            float r = step == 4 ? reach * HalberdMotion.WheelK : reach, half = HalberdMotion.Sweep;
            float from = step == 4 ? aim + HalberdMotion.WindA : step == 1 ? aim - half : aim + half;
            float sweep = (step == 4 ? HalberdMotion.WheelTurn : 2f * half) * Mathf.Min(1f, f * 2f) * (step == 2 ? -1f : 1f);
            Arc(o, r * 0.78f, r * 0.4f, col.WithAlpha(0.28f * (1f - f)), 24, sweep < 0f ? from + sweep : from, Mathf.Abs(sweep));
            Arc(o, r, 3f, Rgba.White.WithAlpha(0.9f * (1f - f)), 24, sweep < 0f ? from + sweep : from, Mathf.Abs(sweep));
        }

        /// When step `step`'s edge is moving (cutting): from a to b s after its click.
        static void HalberdPassWindow(int step, out float a, out float b)
        {
            switch (step)
            {
                case 2: a = HalberdMotion.Start2 - HalberdMotion.End1; b = HalberdMotion.End2 - HalberdMotion.End1; break;
                case 3: a = HalberdMotion.ChopRaise; b = HalberdMotion.ChopHit; break;
                case 4: a = HalberdMotion.WheelWind; b = HalberdMotion.WheelWind + HalberdMotion.WheelSpin; break;
                default: a = HalberdMotion.Start1; b = HalberdMotion.End1; break;
            }
        }

        void SampleHalberdTrail(V2 o, float aim, float reach, float c, int step)
        {
            for (int i = 0; i <= TrailN; i++)
            {
                float t = c - TrailSpan * i / TrailN;
                var m = t > 0f ? HalberdMotion.StepAt(step, t) : new HalberdMotion.Pose();
                trOk[i] = t > 0f && m.Pass > 0;
                trDir[i] = V2.FromAngle(aim + m.A);
                trR[i] = reach * m.K;
                trTip[i] = o + trDir[i] * trR[i];
            }
        }

        /// Dust kicked up along the tip's path, drifting out the way the blade went.
        void HalberdDustTrail(V2 o, float aim, float reach, float c, int step)
        {
            float ps, pe;
            HalberdPassWindow(step, out ps, out pe);
            int n = step == 4 ? 28 : 16;
            float sd = step == 2 ? -1f : 1f;
            for (int j = 0; j < n; j++)
            {
                float tj = ps + j * (pe - ps) / n, age = c - tj;
                if (age <= 0f || age > 0.6f)
                    continue;
                var mj = HalberdMotion.StepAt(step, tj);
                uint r = (uint)(j * 977 + step * 131);
                float jit = (Rand01(ref r) - 0.5f) * 8f, spread = 10f + Rand01(ref r) * 22f;
                V2 d = V2.FromAngle(aim + mj.A), side = new V2(-d.Y, d.X);
                V2 at = o + d * (reach * mj.K + jit + age * 26f) + side * (sd * age * spread);
                float u = age / 0.6f;
                Circle(at, 3f + u * 11f, HalberdDust.WithAlpha(0.24f * (1f - u) * (1f - u)), 10);
            }
        }

        /// The Overhead Chop's landing: a jagged crack along the floor out to the blade, dust thrown up both sides of it.
        void ChopCrack(V2 o, float aim, float reach, float c, int seed)
        {
            float q = c - HalberdMotion.ChopHit;
            if (q < 0f || q > 0.55f)
                return;
            float u = q / 0.55f, len = reach * HalberdMotion.ChopK;
            V2 d = V2.FromAngle(aim), n = new V2(-d.Y, d.X);
            uint r = (uint)seed;
            V2 prev = o + d * 26f;
            float grow = Mathf.Min(1f, q / 0.06f);
            for (int i = 1; i <= 12; i++)  // the crack, opening out from you
            {
                float along = 26f + (len - 26f) * i / 12f * grow, off = (Rand01(ref r) - 0.5f) * (5f + i * 0.7f);
                V2 at = o + d * along + n * off;
                Line(prev, at, 3.4f * (1f - u * 0.6f), new Rgba(0.08f, 0.06f, 0.05f, 0.7f * (1f - u)));
                Line(prev, at, 1.3f, HalberdHot.WithAlpha(0.8f * (1f - u) * (1f - u)));
                prev = at;
            }
            for (int i = 0; i < 10; i++)  // dust thrown out either side of the line
            {
                float along = 30f + (len - 30f) * Rand01(ref r), sd = i % 2 == 0 ? 1f : -1f, sp = 30f + 60f * Rand01(ref r);
                V2 at = o + d * along + n * (sd * (6f + sp * q));
                Circle(at, 4f + u * 13f, HalberdDust.WithAlpha(0.3f * (1f - u) * (1f - u)), 10);
            }
        }

        /// The chop's flash where it lands (glow pass).
        void ChopGlow(V2 o, float aim, float reach, float c)
        {
            float q = c - HalberdMotion.ChopHit;
            if (q < 0f || q > 0.18f)
                return;
            float u = q / 0.18f;
            V2 d = V2.FromAngle(aim);
            float len = reach * HalberdMotion.ChopK;
            Line(o + d * 20f, o + d * len, 2f * Game.HbChopHalfW * (1f - 0.5f * u), HalberdOrange.WithAlpha(0.32f * (1f - u)));
            GlowDisc(o + d * len, 34f, HalberdHot, 0.8f * (1f - u));
        }

        /// The Reaping Wheel: a faint ring at the blade's reach while it turns (glow pass).
        void WheelGlow(V2 o, float reach, float c)
        {
            float s = c - HalberdMotion.WheelWind, len = HalberdMotion.WheelSpin + 0.2f;
            if (s < 0f || s > len)
                return;
            float u = s / len, fade = u < 0.2f ? u / 0.2f : 1f - (u - 0.2f) / 0.8f;
            Arc(o, reach * HalberdMotion.WheelK, 10f, HalberdOrange.WithAlpha(0.16f * fade), 40);
        }

        /// The Reaping Wheel's ring of dust, thrown out as it finishes.
        void WheelDust(V2 o, float reach, float c, int seed)
        {
            float q = c - HalberdMotion.WheelWind - HalberdMotion.WheelSpin * 0.6f;
            if (q < 0f || q > 0.6f)
                return;
            float u = q / 0.6f, rr = reach * HalberdMotion.WheelK * (0.8f + 0.35f * u);
            Arc(o, rr, 6f * (1f - u) + 1f, HalberdDust.WithAlpha(0.32f * (1f - u)), 40);
        }

        /// What the edge does to each enemy it reaches, timed to the blade getting there (worked out from the motion and
        /// where the enemies are drawn: the host's hits are its own): a white flash, orange sparks, a dust ring and a cut
        /// mark across it. Your own first cut of each step holds the picture a moment and shakes the screen.
        void HalberdHits(V2 o, float aim, float reach, float c, int step, int id, int seed, bool light)
        {
            float first = 99f;
            Effect self = null;
            halberdNewest.TryGetValue(id, out self);
            bool big = step != 2;
            float sc = step == 2 ? 0.7f : 1f;
            V2 ad = V2.FromAngle(aim);
            foreach (var e in game.Enemies.Values)
            {
                V2 rel = e.Disp - o;
                float d = rel.Length;
                if (d < 1f)
                    continue;
                float diff = Mathf.Repeat(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (step == 3)
                {
                    float along = rel.X * ad.X + rel.Y * ad.Y, across = Mathf.Abs(rel.X * -ad.Y + rel.Y * ad.X);
                    if (along < 0f || along > reach * HalberdMotion.ChopK + e.Radius || across > Game.HbChopHalfW + e.Radius)
                        continue;
                }
                else if (step == 4)
                {
                    if (d - e.Radius > reach * HalberdMotion.WheelK)
                        continue;
                }
                else if (d - e.Radius > reach || Mathf.Abs(diff) > HalberdMotion.Sweep + 0.2f)
                    continue;
                float hitT = HalberdMotion.StepCross(step, diff), q = c - hitT;
                first = Mathf.Min(first, hitT);
                if (q < 0f || q > 0.55f)
                    continue;
                V2 at = e.Disp;
                if (light)
                {
                    if (q < 0.06f)
                        GlowDisc(at, 30f * sc, HalberdOrange, 0.9f * (1f - q / 0.06f));
                    continue;
                }
                uint r = (uint)(seed + e.Id * 31);
                if (q < 0.4f)
                    for (int i = 0; i < (big ? 14 : 8); i++)  // sparks thrown off the cut
                    {
                        float th = aim + diff + (Rand01(ref r) - 0.5f) * 2.2f, sp = (130f + 240f * Rand01(ref r)) * sc, rr = sp * q, l = 7f + 9f * Rand01(ref r);
                        V2 dd = V2.FromAngle(th);
                        Line(at + dd * rr, at + dd * (rr + l), 2f, (i % 3 == 0 ? HalberdHot : HalberdOrange).WithAlpha(1f - q / 0.4f));
                    }
                if (q < 0.45f)
                    Arc(at, 6f + q * 95f * sc, 5f * (1f - q / 0.45f) + 1f, HalberdDust.WithAlpha(0.4f * (1f - q / 0.45f)), 18);
                if (q < 0.45f)  // the cut mark, along the way the blade travelled (across the chop's line)
                {
                    float al = 1f - q / 0.45f, ba = step == 3 ? aim + MathX.Pi / 2f : aim + HalberdMotion.StepAt(step, hitT).A;
                    V2 n = new V2(-Mathf.Sin(ba), Mathf.Cos(ba));
                    Line(at - n * 20f, at + n * 20f, 3.2f, HalberdOrange.WithAlpha(0.7f * al));
                    Line(at - n * 20f, at + n * 20f, 1.3f, Rgba.White.WithAlpha(al));
                }
            }
            if (!light && step != 3 && self != null && first < 90f && c >= first && id == game.MyId && halberdKicked.Add(self))
            {
                halberdKicked.RemoveWhere(e => e.T >= e.Dur || !game.Effects.Contains(e));
                HalberdOwnCut(step);
            }
        }

        /// The weight of your own cut: a hit-stop and a kick, a little less on the return sweep.
        void HalberdOwnCut(int step)
        {
            Hold(step == 2 ? 0.03f : 0.04f, 0.15f);
            Shake(step == 2 ? 0.08f : step == 4 ? 0.14f : 0.12f);
        }

        /// A poke of the set pike: a streak out along the line and sparks at the point (`light`: the glow pass). With Visual
        /// effects off, a plain line.
        void DrawHalberdPokeFx(Effect fx, bool light)
        {
            float u = fx.T / fx.Dur;
            if (u >= 1f)
                return;
            Player owner;
            V2 o = game.Players.TryGetValue(Mathf.RoundToInt(fx.Radius), out owner) ? owner.Disp : fx.Pos;
            V2 d = (fx.Pos2 - fx.Pos).Normalized();
            float reach = (fx.Pos2 - fx.Pos).Length;
            V2 tip = o + d * reach;
            if (!fxOn)
            {
                Line(o, tip, 6f * (1f - u), Game.HbGlow.WithAlpha(0.35f * (1f - u)));
                Line(o, tip, 2f, Rgba.White.WithAlpha(0.9f * (1f - u)));
                return;
            }
            if (light)
            {
                if (u < 0.45f)
                {
                    float k = 1f - u / 0.45f;
                    Line(o + d * 40f, tip, 9f * k, HalberdOrange.WithAlpha(0.22f * k));
                    GlowDisc(tip, 22f, HalberdHot, 0.7f * k);
                }
                return;
            }
            if (u < 0.35f)  // the jab's streak along the shaft
            {
                float k = 1f - u / 0.35f;
                Line(o + d * (reach * 0.45f), tip, 2f, HalberdHot.WithAlpha(0.8f * k));
            }
            if (u > 0.15f && u < 0.75f)  // sparks off the point as it lands
            {
                float q = (u - 0.15f) / 0.6f;
                uint r = (uint)(fx.Pos.X * 7f + fx.Pos.Y * 3f);
                for (int i = 0; i < 9; i++)
                {
                    float th = d.Angle + (Rand01(ref r) - 0.5f) * 1.8f, rr = (40f + 80f * Rand01(ref r)) * q;
                    V2 dd = V2.FromAngle(th);
                    Line(tip + dd * rr, tip + dd * (rr + 7f), 1.8f, (i % 3 == 0 ? HalberdHot : HalberdOrange).WithAlpha(1f - q));
                }
                Arc(tip, 6f + q * 30f, 3f * (1f - q) + 0.8f, HalberdDust.WithAlpha(0.35f * (1f - q)), 14);
            }
        }

        /// The Halberd itself at a pose: shadow on the floor, arms and gloves, the pole and its axe head. `ghost`: just a
        /// faint copy of the head.
        void DrawHalberd(V2 o, float aim, HalberdMotion.Pose m, float reach, Rgba armCol, float alpha, bool ghost)
        {
            if (alpha <= 0f)
                return;
            float a = aim + m.A, tw = 0.3f * m.A;
            V2 dir = V2.FromAngle(a), side = new V2(-dir.Y, dir.X);
            float tipR = reach * m.K, sc = 1f + 0.2f * m.Lift, cr = Mathf.Cos(m.Roll), pole = Mathf.Max(reach * 0.95f, tipR - 6f);  // (a long reach slides the pole out through the hands)
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
