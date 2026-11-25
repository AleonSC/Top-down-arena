using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Greatsword's ten-step chain, Moonwheel and Eclipse's Rend lance (Docs/Mockups/greatsword-chain-forge.html; the Moonlit
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
            float a = Time.unscaledTime - v.At, reach = Game.MeleeReach(p, p.Weapon.Reach);
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
                    float pr = Game.MeleeReach(p, Game.PommelReach);
                    if (a >= 0.02f)  // the hilt's wake: a short tapered streak out to where it strikes, fading after
                    {
                        float grow = EaseOut3(Mathf.Clamp01((a - 0.02f) / (d.Wind - 0.02f))), f = a < d.Wind + 0.03f ? 1f : 1f - Mathf.Clamp01((a - d.Wind - 0.03f) / GreatTrailFade);
                        DrawGreatStreak(o + dir * 10f, o + dir * (10f + (pr - 4f) * grow), 13f, 0.9f * f, GreatMoon);
                    }
                    DrawMeleeModel(WeaponId.Greatsword, o + dir * push, ang, reach * 0.9f, 1f, GreatMoon, false, 1f, arm);
                    if (a >= d.Wind)
                    {
                        float k = Mathf.Clamp01((a - d.Wind) / 0.15f);
                        V2 at = o + dir * (pr * 0.85f);
                        GlowDisc(at, 20f * (1f - k * 0.3f), Rgba.White, 0.7f * (1f - k));
                        DrawGreatCrescent(at - dir * 14f, p.Aim - 0.75f, p.Aim + 0.75f, 10f + 8f * k, 20f + 10f * EaseOut3(k), 0.9f * (1f - k), GreatMoon, false);  // the knock, a small moon
                    }
                    return true;
                }
                case Game.GreatMove.Leap:
                {
                    // In the air the blade rises over the shoulder; it sweeps down into the landing (GreatSweepsOf) as the ring lands.
                    var lp = GreatSweepsOf(m);
                    float end = lp[0].T0 + lp[0].W + lp[0].Act + GreatHold + GreatRecover;
                    if (a > end)
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
                    }
                    DrawGreatSweeps(p, v, lp, a, arm);
                    return true;
                }
                case Game.GreatMove.Eclipse:
                {
                    // Raised high behind (the blade foreshortened), then brought over and down along the aim with a gold crescent trail.
                    var es = SwEclipse;
                    if (a > es[0].T0 + es[0].W + es[0].Act + 0.25f + GreatRecover)
                        return false;
                    if (!draw)
                        return true;
                    if (a < es[0].W + es[0].Act)
                    {
                        float u = Mathf.Clamp01(a / es[0].W), e = u * u * (3f - 2f * u);
                        Arc(o, 64f - 36f * e, 3f, GreatGold.WithAlpha(0.2f + 0.6f * e), 28);
                        GlowDisc(o, 80f, GreatGold, 0.22f * e);
                    }
                    else
                    {
                        float k = Mathf.Clamp01((a - es[0].W - es[0].Act) / 0.4f);
                        GlowDisc(o + dir * reach, 30f * (1f - k), GreatGold, 0.6f * (1f - k));
                    }
                    DrawGreatSweeps(p, v, es, a, arm);
                    return true;
                }
                default:  // a sweep (or two): the whole motion from the click (DrawGreatSweeps)
                {
                    var sw = GreatSweepsOf(m);
                    var last = sw[sw.Length - 1];
                    if (a > last.T0 + last.W + last.Act + GreatHold + GreatRecover)
                        return false;
                    if (!draw)
                        return true;
                    float u = Mathf.Clamp01(a / Mathf.Max(0.01f, sw[0].W)), e = u * u * (3f - 2f * u);
                    if (m == Game.GreatMove.Undertow && a < sw[0].W + sw[0].Act)  // the current gathering: arcs drifting in from its reach
                        for (int i = 0; i < 3; i++)
                        {
                            float r = Game.MeleeReach(p, Game.UndertowReach) * (1f - 0.35f * Mathf.Repeat(a * 4f + i / 3f, 1f));
                            Arc(o, r, 2f, GreatMoon.WithAlpha(0.3f * e * (1f - Mathf.Clamp01((a - sw[0].W) / sw[0].Act))), 20, p.Aim - 1.4f, 2.8f);
                        }
                    if (m == Game.GreatMove.FullMoon && a < sw[0].W)
                        Arc(o, 60f - 30f * e, 2.5f, GreatMoon.WithAlpha(0.2f + 0.5f * e), 24);
                    DrawGreatSweeps(p, v, sw, a, arm);
                    return true;
                }
            }
        }

        /// One sweep of a step, as in the mockup: from the click, T0 until it starts, W drawing the blade back to A0 (degrees off
        /// the aim), Act sweeping it to A1 (eased out). The host's hit lands W + 0.55 Act in (GreatswordChain.cs GreatMoves.Wind).
        struct GreatSweep { public float T0, W, Act, A0, A1, Reach, Rin, Lift, Hold; public bool Gold; }  // Lift: how much shorter the blade looks raised; Hold: its own pause at the end

        static GreatSweep Sw(float t0, float w, float act, float a0, float a1, float reach, float rin = 22f)
            => new GreatSweep { T0 = t0, W = w, Act = act, A0 = a0, A1 = a1, Reach = reach, Rin = rin, Hold = GreatHold };

        static readonly GreatSweep[] SwUndertow = { Sw(0f, 0.14f, 0.16f, -105f, 105f, Game.UndertowReach, 40f) };
        static readonly GreatSweep[] SwCross = { Sw(0f, 0.06f, 0.08f, -75f, 35f, Game.CrossReach), Sw(0.2f, 0.02f, 0.08f, 75f, -35f, Game.CrossReach) };
        static readonly GreatSweep[] SwBackstep = { Sw(0f, 0.05f, 0.16f, 95f, -95f, Game.BackstepReach) };
        static readonly GreatSweep[] SwMoon = { Sw(0f, 0.12f, 0.32f, -180f, 180f, Game.MoonReach) };
        static readonly GreatSweep[] SwLeap = { Sw(0f, 0.28f, 0.1f, -95f, 0f, 112f) };
        static readonly GreatSweep[] SwEclipse = { new GreatSweep { T0 = 0f, W = 0.37f, Act = 0.09f, A0 = 165f, A1 = 0f, Reach = 135f, Rin = 30f, Lift = 0.35f, Hold = 0.25f, Gold = true } };

        static GreatSweep[] GreatSweepsOf(Game.GreatMove m)
        {
            switch (m)
            {
                case Game.GreatMove.Undertow: return SwUndertow;
                case Game.GreatMove.Cross: return SwCross;
                case Game.GreatMove.Backstep: return SwBackstep;
                case Game.GreatMove.FullMoon: return SwMoon;
                default: return SwLeap;
            }
        }

        const float GreatHold = 0.06f, GreatRecover = 0.22f, GreatTrailFade = 0.14f, GreatPull = 12f * MathX.Pi / 180f;
        readonly Dictionary<int, float> greatBladeAng = new Dictionary<int, float>(), greatBladeAt = new Dictionary<int, float>();

        /// The blade through a step's sweeps, with each one's tapered crescent trail, then back to the held pose. A new step picks up
        /// from where the last left the blade, so a chain flows.
        void DrawGreatSweeps(Player p, GreatMoveView v, GreatSweep[] sw, float a, Rgba arm)
        {
            float now = Time.unscaledTime, aim = p.Aim, rest = -GreatRestOff, D = MathX.Pi / 180f;
            V2 o = p.Disp;
            // where the blade was when this step began (the last step's end, if it was still drawing), as an angle off the aim
            float from = rest, was, wasAt;
            if (greatBladeAng.TryGetValue(p.Id, out was) && greatBladeAt.TryGetValue(p.Id, out wasAt) && wasAt >= v.At - 0.05f && wasAt <= v.At + 0.05f)
                from = was;
            int cur = 0;
            for (int i = 0; i < sw.Length; i++)
                if (a >= sw[i].T0)
                    cur = i;
            // trails first, under the blade
            for (int i = 0; i <= cur; i++)
            {
                var q = sw[i];
                float t = a - q.T0 - q.W;
                if (t < 0f)
                    continue;
                float fade = t < q.Act ? 1f : 1f - Mathf.Clamp01((t - q.Act) / GreatTrailFade);
                if (fade <= 0f)
                    continue;
                float at = Mathf.Lerp(q.A0, q.A1, EaseOut3(Mathf.Clamp01(t / q.Act)));
                float r1 = Game.MeleeReach(p, q.Reach) + 2f;
                DrawGreatCrescent(o, aim + q.A0 * D, aim + at * D, q.Rin + 34f, r1, 0.85f * fade, q.Gold ? GreatGold : GreatMoon, true);
            }
            var c = sw[cur];
            float lt = a - c.T0, prev = cur == 0 ? from : sw[cur - 1].A1 * D, ang, sg = Mathf.Sign(c.A1 - c.A0);
            float start = c.A0 * D - sg * GreatPull, endA = c.A1 * D, reach = Game.MeleeReach(p, c.Reach) - 4f;
            bool bright = false;
            if (lt < c.W)
            {
                float u = lt / c.W, e = u * u * (3f - 2f * u);
                ang = Mathf.Lerp(prev, start, e);
                reach *= 1f - c.Lift * e;
                bright = u > 0.6f;
            }
            else if (lt < c.W + c.Act)
            {
                float k = EaseOut3((lt - c.W) / c.Act);
                ang = Mathf.Lerp(start, endA, k);
                reach *= 1f - c.Lift * (1f - k);
                bright = true;
            }
            else
            {
                float r = cur < sw.Length - 1 ? 0f : Mathf.Clamp01((lt - c.W - c.Act - c.Hold) / GreatRecover);
                bright = c.Gold && lt < c.W + c.Act + c.Hold;
                float back = Mathf.Repeat(endA - rest + MathX.Pi, MathX.Tau) - MathX.Pi;  // the short way back round to the held pose
                ang = endA - back * (r * r * (3f - 2f * r));
                reach = Mathf.Lerp(reach, Game.MeleeReach(p, p.Weapon.Reach), r);
            }
            greatBladeAng[p.Id] = ang;
            greatBladeAt[p.Id] = now;
            Rgba col = c.Gold && lt < c.W + c.Act + c.Hold ? GreatGold : GreatMoon;
            DrawMeleeModel(WeaponId.Greatsword, o, aim + ang, reach, 1f, col, bright, 1f, arm);
            if (lt >= c.W && lt < c.W + c.Act)
                GlowDisc(o + V2.FromAngle(aim + ang) * reach, 18f, col, 0.45f);  // the tip's light as it cuts
        }

        /// A thrust's wake from `a` to `b`: a narrow wedge, a point at `a` widening toward `b` (hw either side), clear at its
        /// edges and bright down the middle, with a white core line toward the tip.
        void DrawGreatStreak(V2 a, V2 b, float hw, float alpha, Rgba col)
        {
            V2 to = b - a;
            float len = to.Length;
            if (alpha <= 0.01f || len < 2f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            var core = col.WithAlpha(0.8f * alpha);
            var none = col.WithAlpha(0f);
            V2 m = a + dir * (len * 0.82f);
            VTri(m, core, a, none, m + side * hw * 0.35f, none);  // the bright middle (the preview colours a triangle by its first vertex)
            VTri(m, core, m - side * hw * 0.35f, none, a, none);
            VTri(a, none, m + side * hw, none, m, core);       // the soft edges (clear in the preview)
            VTri(a, none, m, core, m - side * hw, none);
            VTri(b + side * hw * 0.4f, none, m + side * hw, none, m, core);
            VTri(b - side * hw * 0.4f, none, m, core, m - side * hw, none);
            VTri(m, core, b + side * hw * 0.4f, none, b - side * hw * 0.4f, none);
            Line(a + dir * (len * 0.35f), b, 1.6f, Rgba.White.WithAlpha(0.85f * alpha));
        }

        /// The mockup's crescent: a band from angle a0 to a1 between r0 and r1, filled with light that brightens to its outer edge,
        /// with a white rim. `trail`: thin where it began, full at a1 (a cut's wake); otherwise thickest in the middle (a hanging moon).
        void DrawGreatCrescent(V2 c, float a0, float a1, float r0, float r1, float alpha, Rgba col, bool trail)
        {
            if (alpha <= 0.01f || Mathf.Abs(a1 - a0) < 0.01f)
                return;
            int n = Mathf.Clamp((int)(Mathf.Abs(a1 - a0) * 9f), 6, 40);
            var outer = col.WithAlpha(0.85f * alpha);
            var mid = col.WithAlpha(0.32f * alpha);
            var none = col.WithAlpha(0f);
            V2 po = V2.Zero, pm = V2.Zero, pi = V2.Zero;
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n, an = Mathf.Lerp(a0, a1, u);
                float th = trail ? Mathf.Pow(u, 0.8f) : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(MathX.Pi * u)), 0.7f);
                float ri = r1 - (r1 - r0) * th, rm = r1 - (r1 - ri) * 0.3f;
                V2 d = V2.FromAngle(an), qo = c + d * r1, qm = c + d * rm, qi = c + d * ri;
                if (i > 0)
                {
                    VTri(qo, outer, po, outer, pm, mid);
                    VTri(qo, outer, pm, mid, qm, mid);
                    VTri(pi, none, qm, mid, pm, mid);  // (the clear vertex first: the preview colours a triangle by its first)
                    VTri(qi, none, qm, mid, pi, none);
                }
                po = qo; pm = qm; pi = qi;
            }
            float sweep = a1 - a0;
            Arc(c, r1, 1.8f, Rgba.White.WithAlpha(0.9f * alpha), n, a0, sweep);
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
            V2.Axes(ang, out V2 dir, out V2 side);
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
        /// rend's wind-up each starts from this pose and comes back to it, and it waits while it draws the sword.
        void DrawGreatswordIdle(Player p, float t)
        {
            float ang, reach, bend;
            if (!MeleeRest(p, WeaponId.Greatsword, out ang, out reach, out bend) || MeleeSwingShowing(p) || GreatPoseShowing(p))
                return;
            DrawMeleeModel(WeaponId.Greatsword, p.Disp, ang, reach, 1f, GreatMoon, false, bend, ArmColor(p.Id));
        }

        /// True while a step's own pose, or the Moonwheel draws the sword.
        bool GreatPoseShowing(Player p)
        {
            if (p.FangOut != 0 || DrawGreatMove(p, false))
                return true;
            return false;
        }

        const float GreatRestOff = 0.45f;  // the held pose's angle off the aim (MeleeRest)

        /// The chain's ten diamonds on an arc behind you.
        void DrawGreatPips(Player p)
        {
            if (!fxOn || p.Downed)
                return;
            float t = Time.unscaledTime;
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

        /// Eclipse's Rend: a lance of light flying out along the aim with a crescent head (Power, in the colour's alpha, sets its width; gold at full).
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
