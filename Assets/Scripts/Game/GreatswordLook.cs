using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Greatsword's ten-step chain, Moonwheel, and its old guard and Rend (Docs/Mockups/greatsword-chain-forge.html; the Moonlit
    /// Crescent look). Drawing only, with Visual effects on.
    ///   - the sweeps (Undertow, Cross Cut, Backstep Reap, Full Moon) are ordinary Slash effects drawn by MeleeLooks.cs; GreatSlashSign
    ///     says which way each goes (Backstep Reap and Cross Cut's second cut come back), and DrawSteelWallFx (WorldEffects.cs) hangs
    ///     each crescent the same way (GreatWallSign);
    ///   - DrawGreatMove: each step as it starts (seen as the owner's swing count changing): the blade drawn back to where its sweep
    ///     starts, the hilt driven out for a Pommel Bash, the blade raised through Comet Leap's jump with a ring where you land, and
    ///     Eclipse's golden wind-up and slam (its lance is FxKind.GreatRend, drawn by DrawGreatRendFx, gold);
    ///   - ten diamonds behind you for the chain (DrawGreatPips), the tenth gold (Eclipse);
    ///   - the Moonwheel (DrawGreatWheel): the sword spinning where the host says it is (Player.FangOut, FangAt), smoothed, and a ring
    ///     under your own feet while it comes back;
    ///   - the old guard (DrawGreatGuard, DrawGreatGuardPose) and Rend, unused since the Moonwheel (Greatsword.cs).
    public partial class ArenaApp
    {
        static readonly Rgba GreatMoon = new Rgba(0.78f, 0.88f, 1f), GreatGold = new Rgba(1f, 0.84f, 0.51f);

        readonly Dictionary<Effect, float> greatWallSign = new Dictionary<Effect, float>();

        /// Which way a crescent hangs: the way its cut swept (the return cut, step 2, sweeps back). Fixed when first drawn.
        float GreatWallSign(Effect fx, V2 center)
        {
            float sign;
            if (greatWallSign.TryGetValue(fx, out sign))
                return sign;
            Player best = null;
            float bd = 44f;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.Greatsword)
                    continue;
                float d = Mathf.Min(p.Disp.DistanceTo(center), p.Pos.DistanceTo(center));
                if (d < bd)
                {
                    bd = d;
                    best = p;
                }
            }
            sign = best != null ? Game.GreatSweepSign(Game.GreatMoveOf(best.GreatStep), 0) : 1f;
            if (greatWallSign.Count > 24)
            {
                var live = new HashSet<Effect>(game.Effects);
                var gone = DrawScratch.FreshFx();
                foreach (var k in greatWallSign.Keys)
                    if (!live.Contains(k))
                        gone.Add(k);
                foreach (var k in gone)
                    greatWallSign.Remove(k);
            }
            greatWallSign[fx] = sign;
            return sign;
        }

        readonly Dictionary<int, int> greatSlashCount = new Dictionary<int, int>(), greatSlashN = new Dictionary<int, int>();

        /// Which way a new Greatsword sweep goes (MeleeLooks.cs, as it first sees the Slash): its step's, the second cut of a
        /// Cross Cut the other way.
        public float GreatSlashSign(Player p)
        {
            int c = p.Combo >> 3, seen, n = 0;
            if (greatSlashCount.TryGetValue(p.Id, out seen) && seen == c)
                greatSlashN.TryGetValue(p.Id, out n);
            greatSlashCount[p.Id] = c;
            greatSlashN[p.Id] = n + 1;
            return Game.GreatSweepSign(Game.GreatMoveOf(p.GreatStep), n);
        }

        class GreatMoveView { public int Count; public float At = -99f; public byte Step; public V2 From; public float Aim; }
        public static float GreatPreviewAge = -1f;  // Tools/Preview: >= 0 shows the step a player is on as started this long ago
        readonly Dictionary<int, GreatMoveView> greatMoveViews = new Dictionary<int, GreatMoveView>();

        /// The step this player started last and how long ago (a new one is a change in their swing count).
        GreatMoveView WatchGreatMove(Player p)
        {
            GreatMoveView v;
            int c = p.Combo >> 3;
            if (!greatMoveViews.TryGetValue(p.Id, out v))
                greatMoveViews[p.Id] = v = GreatPreviewAge >= 0f
                    ? new GreatMoveView { Count = c, At = Time.unscaledTime - GreatPreviewAge, Step = p.GreatStep, From = p.Disp, Aim = p.Aim }
                    : new GreatMoveView { Count = c };  // (no move on first sight)
            if (v.Count != c)
            {
                v.Count = c;
                v.At = Time.unscaledTime;
                v.Step = p.GreatStep;
                v.From = p.Disp;
                v.Aim = p.Aim;
            }
            return v;
        }

        /// Where a step's sweep starts, as an angle off the aim (MeleeLooks.cs sweeps from aim - sign * half).
        static float GreatSweepStart(Game.GreatMove m)
        {
            switch (m)
            {
                case Game.GreatMove.Undertow: return -Game.UndertowArc * MathX.Pi / 360f;
                case Game.GreatMove.Cross: return -0.35f - Game.CrossArc * MathX.Pi / 360f;
                case Game.GreatMove.Backstep: return Game.BackstepArc * MathX.Pi / 360f;
                case Game.GreatMove.FullMoon: return -MathX.Pi;
                default: return 0f;
            }
        }

        void DrawGreatWindup(Player p) => DrawGreatMove(p, true);

        /// Each step's own pose before its hit (and the moves with no Slash of their own): see the class notes. True while it draws the
        /// sword (the held pose waits).
        bool DrawGreatMove(Player p, bool draw)
        {
            var v = WatchGreatMove(p);
            if (!fxOn || p.Downed || p.FangOut != 0)
                return false;
            var m = Game.GreatMoveOf(v.Step);
            if (m == Game.GreatMove.None)
                return false;
            var d = Game.GreatMoves[(int)m];
            float a = Time.unscaledTime - v.At, reach = Game.MeleeReach(p, p.Weapon.Reach), rest = p.Aim - GreatRestOff;
            V2 o = p.Disp, dir = V2.FromAngle(p.Aim);
            Rgba arm = ArmColor(p.Id);
            switch (m)
            {
                case Game.GreatMove.Pommel:
                {
                    if (a > d.Dur)
                        return false;
                    if (!draw)
                        return true;
                    float q = Mathf.Clamp01(a / 0.14f), push = 20f * Mathf.Sin(MathX.Pi * q);
                    float ang = p.Aim + MathX.Pi - 0.35f + 0.25f * q;  // the blade back over the shoulder, the hilt leading
                    DrawMeleeModel(WeaponId.Greatsword, o + dir * push, ang, reach * 0.9f, 1f, GreatMoon, false, 1f, arm);
                    if (a >= d.Wind)
                    {
                        float k = Mathf.Clamp01((a - d.Wind) / 0.15f);
                        V2 at = o + dir * (Game.PommelReach * 0.75f);
                        GlowDisc(at, 20f * (1f - k * 0.3f), Rgba.White, 0.7f * (1f - k));
                        Arc(at, 8f + 18f * EaseOut3(k), 2.5f, GreatMoon.WithAlpha(0.8f * (1f - k)), 16);
                    }
                    return true;
                }
                case Game.GreatMove.Leap:
                {
                    if (a > d.Wind + 0.2f)
                        return false;
                    if (!draw)
                        return true;
                    V2 vdir = V2.FromAngle(v.Aim);
                    V2 land = v.From + vdir * d.Fwd;
                    if (a < d.Wind)
                    {
                        float u = a / d.Wind, e = u * u * (3f - 2f * u);
                        float lr = Game.MeleeReach(p, Game.LeapRadius);
                        Arc(land, lr * (1.25f - 0.25f * e), 2f, GreatMoon.WithAlpha(0.15f + 0.4f * e), 32);  // where you come down
                        GlowDisc(land, 30f, GreatMoon, 0.12f + 0.2f * e);
                        DrawMeleeModel(WeaponId.Greatsword, o, p.Aim - 1.7f * (1f - e), reach * (0.8f + 0.2f * e), 1f, GreatMoon, u > 0.6f, 1f, arm);
                    }
                    else
                    {
                        float k = (a - d.Wind) / 0.2f;
                        DrawMeleeModel(WeaponId.Greatsword, o, p.Aim, reach, 1f, GreatMoon, k < 0.5f, 1f, arm);
                    }
                    return true;
                }
                case Game.GreatMove.Eclipse:
                {
                    if (a > d.Wind + 0.4f)
                        return false;
                    if (!draw)
                        return true;
                    if (a < d.Wind)
                    {
                        float u = a / d.Wind, e = u * u * (3f - 2f * u);
                        Arc(o, 64f - 36f * e, 3f, GreatGold.WithAlpha(0.2f + 0.6f * e), 28);
                        GlowDisc(o, 80f, GreatGold, 0.22f * e);
                        DrawMeleeModel(WeaponId.Greatsword, o, Mathf.Lerp(rest, p.Aim + MathX.Pi - 0.3f, e), reach * (1f - 0.35f * e), 1f, GreatGold, u > 0.5f, 1f, arm);
                    }
                    else
                    {
                        float k = (a - d.Wind) / 0.4f;
                        DrawMeleeModel(WeaponId.Greatsword, o, p.Aim, reach, 1f, k < 0.6f ? GreatGold : GreatMoon, k < 0.6f, 1f, arm);
                        GlowDisc(o + dir * reach, 30f * (1f - k), GreatGold, 0.6f * (1f - k));
                    }
                    return true;
                }
                default:  // a sweep: the blade drawn back to where it starts, then its Slash takes the sword
                {
                    if (a > d.Wind + 0.08f || (a >= d.Wind && MeleeSwingShowing(p)))
                        return false;
                    if (!draw)
                        return true;
                    float u = Mathf.Clamp01(a / d.Wind), e = u * u * (3f - 2f * u);
                    float start = p.Aim + GreatSweepStart(m);
                    if (m == Game.GreatMove.Undertow)  // the current gathering: arcs drifting in from its reach
                        for (int i = 0; i < 3; i++)
                        {
                            float r = Game.MeleeReach(p, Game.UndertowReach) * (1f - 0.35f * Mathf.Repeat(u + i / 3f, 1f));
                            Arc(o, r, 2f, GreatMoon.WithAlpha(0.35f * e), 20, p.Aim - 1.4f, 2.8f);
                        }
                    if (m == Game.GreatMove.FullMoon)
                        Arc(o, 60f - 30f * e, 2.5f, GreatMoon.WithAlpha(0.2f + 0.5f * e), 24);
                    DrawMeleeModel(WeaponId.Greatsword, o, Mathf.Lerp(rest, start, e), reach, 1f, GreatMoon, u > 0.7f, 1f, arm);
                    return true;
                }
            }
        }

        readonly Dictionary<int, V2> greatWheelShown = new Dictionary<int, V2>();

        /// The Moonwheel: the sword spinning about its middle where the host says it is, with a pale wheel of light; drawn back
        /// before the throw; a ring under your own feet while its cooldown runs.
        void DrawGreatWheel(Player p)
        {
            if (p.Id == game.MyId && game.TwinFangCd > 0f && !p.Downed)
            {
                float k = 1f - game.TwinFangCd / Game.WheelCooldown;
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, p.Def.BodyR + 7f, 2f, GreatMoon.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * k);
            }
            if (!fxOn || p.Downed || p.FangOut == 0)
            {
                greatWheelShown.Remove(p.Id);
                return;
            }
            float reach = Game.MeleeReach(p, p.Weapon.Reach);
            if (p.FangOut == 1)
            {
                DrawMeleeModel(WeaponId.Greatsword, p.Disp, p.Aim + MathX.Pi * 0.7f, reach, 1f, GreatMoon, true, 1f, ArmColor(p.Id));
                return;
            }
            V2 c;
            if (!greatWheelShown.TryGetValue(p.Id, out c))
                c = p.FangAt;
            c = c + (p.FangAt - c) * (1f - Mathf.Exp(-Time.unscaledDeltaTime * 20f));
            greatWheelShown[p.Id] = c;
            float spin = -Time.unscaledTime * Game.WheelSpinRate, L = reach * 0.98f;
            Arc(c, L * 0.5f, 7f, GreatMoon.WithAlpha(0.18f), 32);
            Arc(c, L * 0.5f, 2f, Rgba.White.WithAlpha(0.35f), 32);
            for (int k = 1; k <= 3; k++)
                DrawGreatBlade(c - V2.FromAngle(spin + k * 0.35f) * (L * 0.45f), spin + k * 0.35f, L, 0.22f / k, false);
            DrawGreatBlade(c - V2.FromAngle(spin) * (L * 0.45f), spin, L, 1f, true);
            GlowDisc(c, 26f, GreatMoon, 0.3f);
        }

        /// The Greatsword alone (no arms), its pommel at `o`: the thrown one.
        void DrawGreatBlade(V2 o, float ang, float reach, float alpha, bool bright)
        {
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            const float gx = 36f;
            float L = reach, w = 7.6f;
            Line(o - dir * 6f, o + dir * gx, 5f, new Rgba(0.23f, 0.17f, 0.12f, alpha));
            Line(o - dir * 6f, o + dir * gx, 3f, MeleeHandle.WithAlpha(alpha));
            Circle(o - dir * 7f, 3.4f, new Rgba(0.72f, 0.6f, 0.29f, alpha), 8);
            V2 g0 = o + dir * (gx - 3f), g1 = o + dir * (gx + 3f);
            SetColor(MeleeGuard.WithAlpha(alpha));
            Quad(g0 - side * 12f, g1 - side * 10f, g1 + side * 10f, g0 + side * 12f);
            V2 b0 = o + dir * (gx + 1f);
            float bl = L - gx - 1f;
            V2 q0 = b0 - side * w, q1 = b0 + dir * (bl - 18f) - side * w * 0.82f, tip = b0 + dir * bl, q3 = b0 + dir * (bl - 18f) + side * w * 0.5f, q4 = b0 + side * w * 0.62f;
            SetColor((bright ? Rgba.White : new Rgba(0.88f, 0.93f, 0.99f)).WithAlpha(alpha));
            Quad(q0, q1, q3, q4);
            Tri(q1, tip, q3);
            var edge = Rgba.White.WithAlpha(0.95f * alpha);
            Line(q0, q1, 1.1f, edge);
            Line(q1, tip, 1.1f, edge);
            Line(tip, q3, 1.1f, edge);
            Line(q3, q4, 1.1f, edge);
        }

        /// The Greatsword held at the ready in both gloved hands, drawn over the body in place of the model's own blade. A cut, the
        /// rend's wind-up and the guard pose each start from this pose and come back to it, and it waits while they draw the sword.
        void DrawGreatswordIdle(Player p, float t)
        {
            float ang, reach, bend;
            if (!MeleeRest(p, WeaponId.Greatsword, out ang, out reach, out bend) || MeleeSwingShowing(p) || GreatPoseShowing(p))
                return;
            DrawMeleeModel(WeaponId.Greatsword, p.Disp, ang, reach, 1f, GreatMoon, false, bend, ArmColor(p.Id));
        }

        /// True while a step's own pose, the Moonwheel or the old guard pose (with its thrust) draws the sword.
        bool GreatPoseShowing(Player p)
        {
            float at;
            if (p.FangOut != 0 || DrawGreatMove(p, false))
                return true;
            return p.GreatGuard || (greatKickAt.TryGetValue(p.Id, out at) && Time.unscaledTime - at < GreatKickTime);
        }

        const float GreatKickTime = 0.22f, GreatRaiseTime = 0.08f, GreatRestOff = 0.45f;  // the guard's thrust back to the ready; raising it; the held pose's angle off the aim (MeleeRest)
        readonly Dictionary<int, bool> greatGuardSeen = new Dictionary<int, bool>();
        readonly Dictionary<int, float> greatKickAt = new Dictionary<int, float>(), greatRaiseAt = new Dictionary<int, float>();

        /// The sword while you guard: braced out along the aim and trembling a little, brighter with every point of Temper, then thrust
        /// forward for GreatKickTime when you let go (the Rend). The held sword phases out while this one is drawn.
        void DrawGreatGuardPose(Player p)
        {
            bool prev;
            greatGuardSeen.TryGetValue(p.Id, out prev);
            greatGuardSeen[p.Id] = p.GreatGuard;
            float now = Time.unscaledTime, kick;
            if (prev && !p.GreatGuard && !p.Downed)
                greatKickAt[p.Id] = now;
            if (!prev && p.GreatGuard)
                greatRaiseAt[p.Id] = now;
            float since = greatKickAt.TryGetValue(p.Id, out kick) ? now - kick : 99f;
            bool kicking = !p.GreatGuard && since >= 0f && since < GreatKickTime;
            if (!fxOn || p.Downed || !(p.GreatGuard || kicking))
                return;
            float fill = Mathf.Clamp01((float)p.GreatTemper / Game.GreatTemperMax);
            bool full = p.GreatTemper >= Game.GreatTemperMax;
            V2 o = p.Disp;
            V2 dir = V2.FromAngle(p.Aim);
            float reach = Game.MeleeReach(p, p.Weapon.Reach), t = now;
            float push = 0f, ang = p.Aim + (p.GreatGuard ? 0.015f * Mathf.Sin(t * 38f) * (0.4f + fill) : 0f);
            float rest = p.Aim - GreatRestOff + 0.02f * Mathf.Sin(game.AnimT * 2.6f), raise;
            if (kicking)
            {
                float q = since / GreatKickTime;
                push = 16f * (1f - q) * (1f - q);  // a thrust that eases back to the ready, turning down into the held pose
                ang += (rest - ang) * q * q * (3f - 2f * q);
            }
            else if (greatRaiseAt.TryGetValue(p.Id, out raise) && now - raise < GreatRaiseTime)
            {
                float q = (now - raise) / GreatRaiseTime;  // raised from the held pose
                ang = rest + (ang - rest) * q * (2f - q);
            }
            Rgba col = full ? GreatGold : GreatMoon;
            DrawMeleeModel(WeaponId.Greatsword, o + dir * push, ang, reach, 1f, col, full || kicking || fill > 0.5f, 1f, ArmColor(p.Id));
            if (fill > 0f && p.GreatGuard)  // the edge lights along its length as Temper builds
            {
                V2 a = o + dir * 40f, b = o + dir * (40f + (reach - 46f) * fill);
                Line(a, b, 4f, col.WithAlpha(0.35f + 0.2f * fill));
                GlowDisc(b, 12f + 10f * fill, col, 0.35f + 0.25f * fill);
            }
            if (kicking)
                GlowDisc(o + dir * (reach + push), 22f * (1f - since / GreatKickTime), col, 0.6f * (1f - since / GreatKickTime));
        }

        /// The chain's three diamonds, or the Temper's six, on an arc behind you.
        void DrawGreatPips(Player p)
        {
            if (!fxOn || p.Downed)
                return;
            float t = Time.unscaledTime;
            bool temper = p.GreatGuard || p.GreatTemper > 0 || p.GreatPrimed;
            if (temper)
            {
                int n = p.GreatPrimed && !p.GreatGuard ? Game.GreatTemperMax : Mathf.Min(Game.GreatTemperMax, (int)p.GreatTemper);
                bool gold = n >= Game.GreatTemperMax;
                for (int d = 0; d < Game.GreatTemperMax; d++)
                {
                    float da = p.Aim + MathX.Pi + (d - 2.5f) * 0.34f;
                    V2 c = p.Disp + V2.FromAngle(da) * 30f;
                    Polygon(c, 4.3f + (gold ? 0.8f * Mathf.Sin(t * 10f) + 0.8f : 0f), 4, 0f, d < n ? (gold ? GreatGold : GreatMoon) : new Rgba(1f, 1f, 1f, 0.14f));
                }
                if (gold)
                    Arc(p.Disp, 26f, 3f, GreatGold.WithAlpha(0.25f + 0.1f * Mathf.Sin(t * 9f)), 32);
                return;
            }
            if (p.GreatStep == 0)
                return;
            int steps = Game.GreatChain.Length;
            for (int d = 0; d < steps; d++)  // the chain's ten diamonds on an arc behind you, Eclipse's gold
            {
                float da = p.Aim + MathX.Pi + (d - (steps - 1) / 2f) * 0.2f;
                V2 c = p.Disp + V2.FromAngle(da) * 31f;
                bool lit = d < p.GreatStep, last = d == steps - 1;
                Rgba on = last ? GreatGold : GreatMoon;
                Polygon(c, (last ? 4.4f : 3.3f) + (lit ? 0.5f : 0f), 4, 0f, lit ? on : new Rgba(1f, 1f, 1f, 0.14f));
                if (d == p.GreatStep && last)  // Eclipse is next
                    Polygon(c, 6.5f + 1.5f * Mathf.Sin(t * 10f), 4, 0f, GreatGold.WithAlpha(0.4f));
            }
        }

        /// The guard arc while the weapon-action key is held.
        void DrawGreatGuard(Player p)
        {
            if (!fxOn || p.Downed || !p.GreatGuard)
                return;
            float t = Time.unscaledTime, r = Game.GreatGuardRadius, half = Game.GreatGuardHalfArc;
            float fill = Mathf.Clamp01((float)p.GreatTemper / Game.GreatTemperMax);
            bool full = p.GreatTemper >= Game.GreatTemperMax;
            Rgba col = full ? GreatGold : GreatMoon;
            V2 o = p.Disp;
            float a0 = p.Aim - half, sweep = 2f * half;
            Arc(o, r, Game.GreatGuardBand * 2f, col.WithAlpha(0.13f), 24, a0, sweep);
            Arc(o, r, 7f, col.WithAlpha(0.4f + 0.15f * fill), 24, a0, sweep);
            Arc(o, r, 2.4f, Rgba.White.WithAlpha(0.7f), 24, a0, sweep);
            float gl = Mathf.Repeat(t * 1.1f, 1.3f) - 0.15f;
            if (gl > 0f && gl < 1f)
            {
                float ga = a0 + sweep * gl;
                GlowDisc(o + V2.FromAngle(ga) * r, 22f, Rgba.White, 0.55f);
                Arc(o, r, 2.4f, Rgba.White.WithAlpha(0.9f), 6, ga - 0.07f, 0.14f);
            }
            var hitCol = Defs.BulletColor(Defs.KnightSlash);
            foreach (var sp in game.Effects)  // the host's spark where a bullet died: the arc brightens there
            {
                if (sp.Kind != FxKind.Spark || sp.Radius != 8f || sp.Color.R != hitCol.R || sp.Color.G != hitCol.G || sp.Color.B != hitCol.B)
                    continue;
                V2 rel = sp.Pos - o;
                float d = rel.Length, an = rel.Angle;
                float diff = Mathf.Repeat(an - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (Mathf.Abs(d - r) > Game.GreatGuardBand + 12f || Mathf.Abs(diff) > half + 0.1f)
                    continue;
                float q = Mathf.Clamp01(sp.T / 0.4f);
                if (q >= 1f)
                    continue;
                GlowDisc(o + V2.FromAngle(an) * r, 20f * (1f - q * 0.4f), Rgba.White, 0.8f * (1f - q));
                float run = 0.5f * EaseOut3(q);
                Arc(o, r, 2f, Rgba.White.WithAlpha(0.55f * (1f - q)), 12, an - run, 2f * run);
            }
        }

        /// The Rend: a lance of light flying out along the aim with a crescent head, wider for every point of Temper, gold at six.
        void DrawGreatRendFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length;
            if (len < 1f)
                return;
            V2 dir = to / len;
            int n = Mathf.Clamp(Mathf.RoundToInt((fx.Color.A - 0.2f) / 0.8f * Game.GreatTemperMax), 0, Game.GreatTemperMax);
            Rgba col = n >= Game.GreatTemperMax ? GreatGold : GreatMoon;
            float T = fx.T, fa = 1f - Mathf.Clamp01((T - 0.3f) / 0.2f);
            float head = Mathf.Min(len, 60f + 1500f * T), tl = Mathf.Min(head, 420f);
            V2 a = fx.Pos + dir * (head - tl), b = fx.Pos + dir * head;
            Line(a, b, fx.Radius * 1.4f, col.WithAlpha(0.16f * fa));
            Line(a, b, Mathf.Max(4f, fx.Radius * 0.5f), col.WithAlpha(0.45f * fa));
            Line(fx.Pos + dir * (head - tl * 0.7f), b, 2f + n * 0.4f, Rgba.White.WithAlpha(0.9f * fa));
            float cr = 34f + n * 5f, ang = dir.Angle;
            for (int k = 9; k >= 0; k--)
            {
                V2 c = b - dir * (18f + 12f * k);
                Arc(c, cr, 3f * (1f - k / 12f) + 1f, col.WithAlpha((1f - k / 10f) * 0.5f * fa), 12, ang - 0.95f, 1.9f);
            }
            Arc(b - dir * 18f, cr, 7f + n * 0.6f, col.WithAlpha(0.8f * fa), 14, ang - 0.95f, 1.9f);
            Arc(b - dir * 18f, cr, 2.4f, Rgba.White.WithAlpha(fa), 14, ang - 0.95f, 1.9f);
            GlowDisc(b, 24f + n * 3f, col, 0.5f * fa);
        }
    }
}
