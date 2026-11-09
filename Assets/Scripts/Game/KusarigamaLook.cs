using System;
using UnityEngine;

namespace Arena
{
    /// The Kusarigama's Ink Brush look (Docs/Mockups/kusarigama-rework.html, look B). Everything comes from the replicated Player
    /// state and the host's plain rings, so every screen sees the same thing (Core/Kusarigama.cs). Two layers:
    ///   - the models, drawn with Visual effects on or off (DrawKusarigama, the normal pass): a sickle on a wooden grip, a chain of
    ///     links that sags when slack and pulls straight when taut, and a small iron weight with a ring. The weight carries the
    ///     whirl's gauge (a ring round it that empties over KusariWhirlMax) and the combo's step (it glows lilac when the next press
    ///     throws it; MeleeLooks.cs and CharacterModels.cs draw it hanging);
    ///   - the ink, Visual effects on only (DrawKusariInk, the additive glow pass): the cut paints one lilac brush stroke along the
    ///     sickle's path that dries out into bristle gaps (KusariCutLook), the throw drags a dry-brush line behind the weight and
    ///     splashes where it bites, the whirl paints an ensō (a brush circle) behind the weight with ink wisps off the enemies it
    ///     pulls, and the snap bursts into a ring of splatter. The bite and the snap are the host's lilac rings (IsKusariRing).
    public partial class ArenaApp
    {
        static readonly Rgba KusariLilac = Game.KusariColor;
        static readonly Rgba KusariSteel = new Rgba(0.77f, 0.78f, 0.84f), KusariIron = new Rgba(0.23f, 0.235f, 0.3f), KusariWood = new Rgba(0.43f, 0.31f, 0.18f);

        public const float KusariSweepTime = 0.18f, KusariDryTime = 0.55f;  // the sickle's sweep across its arc; how long its stroke takes to dry out after it
        const float KusariSplashLife = 0.7f;  // how long a bite's or the snap's splash lasts (its ring effect is kept that long)

        // --- The models ---------------------------------------------------------------------------------------------------

        void DrawKusarigama(Player p)
        {
            if (p.Downed)
                return;
            V2 o = p.Disp;
            if (p.KusariWhirl)
            {
                float spin = Mathf.Clamp01(p.KusariHeld / Game.KusariSpinUp), orbit = Game.KusariOrbit(spin);
                V2 bw = o + V2.FromAngle(p.KusariAng) * orbit, hand = o + V2.FromAngle(p.KusariAng - 0.5f) * 11f;
                DrawKusariChain(hand, bw, 0f, 1f);
                DrawKusariWeight(bw, 1f, game.AnimT * 18f, 1f - p.KusariHeld / Game.KusariWhirlMax, 0f);
                return;
            }
            if (p.HookT > 0f)
            {
                V2 hand, pos;
                float sag;
                KusariThrowPose(p, out hand, out pos, out sag);
                DrawKusariChain(hand, pos, sag, 1f);
                DrawKusariWeight(pos, 1f, game.AnimT * 14f, -1f, 0f);
            }
        }

        /// Where the thrown weight is now: it whirls once over your hand, flies to its target, sticks in it and comes back with it.
        void KusariThrowPose(Player p, out V2 hand, out V2 pos, out float sag)
        {
            float k = Game.KusariThrowTime - p.HookT;
            V2 o = p.Disp, dir = V2.FromAngle(p.KusariAim);
            hand = o + dir * 12f - dir.Orthogonal() * 10f;
            Enemy e = null;
            bool hooked = p.HookId != 0 && game.Enemies.TryGetValue(p.HookId, out e) && e.Hp > 0f;
            V2 end = hooked ? e.Disp - dir * e.Radius * 0.7f : o + dir * game.KusariReachOf(p);
            sag = 0f;
            if (k < Game.KusariWindUp)
            {
                pos = hand + V2.FromAngle(p.KusariAim - MathX.Tau * (1f - k / Game.KusariWindUp)) * 36f;  // one turn over your hand
                sag = 4f;
            }
            else if (k < Game.KusariImpactAt)
            {
                float u = Mathf.Clamp01((k - Game.KusariWindUp) / (Game.KusariImpactAt - Game.KusariWindUp));
                pos = V2.Lerp(hand + dir * 36f, end, EaseOut3(u));
            }
            else if (k < Game.KusariDragFrom)
                pos = end;
            else
            {
                float u = Mathf.Clamp01((k - Game.KusariDragFrom) / (Game.KusariDragTo - Game.KusariDragFrom)), s = u * u * (3f - 2f * u);
                V2 home = hooked ? hand + dir * (e.Radius + 20f) : hand;
                if (k < Game.KusariDragTo)
                    pos = hooked ? e.Disp - dir * e.Radius * 0.7f : V2.Lerp(end, home, s);  // stuck in it, wherever it is now
                else
                {
                    pos = V2.Lerp(home, hand, Mathf.Clamp01((k - Game.KusariDragTo) / (Game.KusariThrowTime - Game.KusariDragTo)));
                    sag = 6f;  // slack on the way home
                }
            }
        }

        /// The chain: links from a to b, alternating face-on (rings) and edge-on (bars), sagging `sag` px at the middle.
        void DrawKusariChain(V2 a, V2 b, float sag, float alpha)
        {
            V2 to = b - a;
            float len = to.Length;
            if (len < 2f || alpha <= 0f)
                return;
            V2 n = to.Orthogonal() / len;
            int count = Mathf.Max(3, Mathf.RoundToInt(len / 6.5f));
            var dark = new Rgba(0.08f, 0.08f, 0.12f, 0.75f * alpha);
            V2 prev = a;
            for (int i = 1; i <= count; i++)
            {
                float f = (float)i / count;
                V2 pt = a + to * f + n * (sag * 4f * f * (1f - f));
                V2 mid = (prev + pt) * 0.5f, d = (pt - prev).Normalized();
                if (i % 2 == 1)
                {
                    Arc(mid, 2.6f, 2.6f, dark, 8);
                    Arc(mid, 2.6f, 1.3f, KusariSteel.WithAlpha(alpha), 8);
                }
                else
                    Line(mid - d * 3.2f, mid + d * 3.2f, 1.7f, KusariSteel.WithAlpha(0.9f * alpha));
                prev = pt;
            }
        }

        /// The kama: a wooden grip pointing back along `ang`, the blade hooking off its end the way it sweeps (`s` = +1 for a sweep
        /// toward larger angles).
        void DrawKusariSickle(V2 at, float ang, float s, float alpha)
        {
            if (alpha <= 0f)
                return;
            V2 d = V2.FromAngle(ang), sd = d.Orthogonal() * -s;
            Line(at - d * 16f, at, 4f, KusariWood.WithAlpha(alpha));
            Line(at - d * 14f, at - d * 11f, 4.4f, new Rgba(0.24f, 0.17f, 0.12f, alpha));
            V2 b0 = at - sd * 1.5f, tip = at + sd * 18f - d * 5f, cOut = at + d * 11f + sd * 8f, cIn = at + d * 4f + sd * 7f;
            const int N = 8;
            SetColor(new Rgba(0.59f, 0.61f, 0.69f, alpha));
            V2 po = b0, pi = at;
            for (int i = 1; i <= N; i++)
            {
                float u = (float)i / N;
                V2 qo = Bez(b0, cOut, tip, u), qi = Bez(at, cIn, tip, u);
                Quad(po, qo, qi, pi);
                po = qo;
                pi = qi;
            }
            po = b0;
            for (int i = 1; i <= N; i++)
            {
                V2 qo = Bez(b0, cOut, tip, (float)i / N);
                Line(po, qo, 1.3f, Rgba.White.WithAlpha(0.85f * alpha));
                po = qo;
            }
            Circle(at, 2.2f, new Rgba(0.24f, 0.24f, 0.3f, alpha), 8);
        }

        static V2 Bez(V2 a, V2 c, V2 b, float u) => a * ((1f - u) * (1f - u)) + c * (2f * u * (1f - u)) + b * (u * u);

        /// The weight: a small iron fundo with a ring. `gauge` (0-1, or -1 for none) is the whirl left, a lilac ring round it that
        /// empties; `primed` (0-1) is the combo's step, a lilac glow when the next press throws it.
        void DrawKusariWeight(V2 at, float alpha, float spin, float gauge, float primed)
        {
            if (primed > 0f)
            {
                GlowDisc(at, 22f, KusariLilac, 0.45f * primed * alpha);
                Arc(at, 9.5f, 1.6f, KusariLilac.WithAlpha(0.9f * primed * alpha), 16);
            }
            Circle(at + new V2(2f, 3f), 7.5f, new Rgba(0f, 0f, 0f, 0.35f * alpha), 12);
            Circle(at, 7f, new Rgba(0.78f, 0.8f, 0.87f, alpha), 14);
            Circle(at, 5.8f, KusariIron.WithAlpha(alpha), 14);
            Circle(at + new V2(-2.2f, -2.2f), 1.8f, Rgba.White.WithAlpha(0.75f * alpha), 6);
            Arc(at + V2.FromAngle(spin) * 7f, 2.2f, 1.4f, KusariSteel.WithAlpha(alpha), 8);
            if (gauge >= 0f)
            {
                gauge = Mathf.Clamp01(gauge);
                Arc(at, 12f, 2.2f, new Rgba(1f, 1f, 1f, 0.12f * alpha), 20);
                Arc(at, 12f, 2.2f, KusariLilac.WithAlpha((0.45f + 0.5f * gauge) * alpha), 20, -MathX.Pi / 2f, MathX.Tau * gauge);  // dims as it runs out
            }
        }

        // --- The ink (the additive glow pass, Visual effects on) ---------------------------------------------------------------

        /// A brush stroke along an arc from a0 to a1: seven bristles across `width` (scaled along it by `profile`), breaking up into
        /// gaps as `dry` goes 0 to 1 (the outer bristles first).
        void KusariBrushArc(V2 o, float r, float a0, float a1, float width, float dry, float alpha, int seed, Func<float, float> profile)
        {
            if (alpha <= 0f || Mathf.Abs(a1 - a0) < 0.005f)
                return;
            int n = Mathf.Max(10, Mathf.CeilToInt(Mathf.Abs(a1 - a0) * 34f));
            const int B = 7;
            for (int b = 0; b < B; b++)
            {
                float off = (float)b / (B - 1) - 0.5f, edge = (1f - Mathf.Abs(off) * 2f) * 0.25f, jit = (Hash01(seed, b) - 0.5f) * 1.5f;
                Rgba col = b == 3 ? Rgba.White.WithAlpha(0.3f * alpha) : KusariLilac.WithAlpha(0.4f * alpha);
                for (int i = 0; i < n; i++)
                {
                    float f = (float)i / n, w = width * profile(f);
                    if (w < 0.3f || Hash01(seed + b * 31, (int)(f * 18f)) < dry * 1.15f - edge)
                        continue;
                    float s0 = Mathf.Lerp(a0, a1, f), s1 = Mathf.Lerp(a0, a1, (i + 1f) / n);
                    Arc(o, r + off * w + jit, Mathf.Max(0.8f, w / B * 1.7f), col, 1, s0, s1 - s0);
                }
            }
        }

        /// A straight brush stroke from a to b (the same bristles as KusariBrushArc).
        void KusariBrushLine(V2 a, V2 b, float width, float dry, float alpha, int seed, Func<float, float> profile)
        {
            V2 to = b - a;
            float len = to.Length;
            if (len < 2f || alpha <= 0f)
                return;
            V2 d = to / len, sd = d.Orthogonal();
            int n = Mathf.CeilToInt(len / 5f);
            const int B = 7;
            for (int k = 0; k < B; k++)
            {
                float off = (float)k / (B - 1) - 0.5f, edge = (1f - Mathf.Abs(off) * 2f) * 0.25f;
                Rgba col = k == 3 ? Rgba.White.WithAlpha(0.3f * alpha) : KusariLilac.WithAlpha(0.4f * alpha);
                for (int i = 0; i < n; i++)
                {
                    float f = (float)i / n, f1 = (i + 1f) / n, w = width * profile(f);
                    if (Hash01(seed + k * 17, (int)(f * 14f)) < dry * 1.15f - edge)
                        continue;
                    Line(a + to * f + sd * (off * w), a + to * f1 + sd * (off * width * profile(f1)), Mathf.Max(0.8f, w / B * 1.6f), col);
                }
            }
        }

        /// Ink droplets flung from `at` toward `dirA` (spread over `spread` radians), `u` seconds after.
        void KusariDroplets(V2 at, float u, int seed, int n, float reach, float dirA, float spread)
        {
            float fade = 1f - Mathf.Clamp01((u - 0.3f) / 0.4f), out01 = EaseOut3(Mathf.Clamp01(u / 0.2f));
            if (fade <= 0f)
                return;
            for (int i = 0; i < n; i++)
            {
                float a = dirA + (Hash01(seed, i) - 0.5f) * spread, r = reach * (0.35f + 0.65f * Hash01(seed + 7, i)) * out01;
                Circle(at + V2.FromAngle(a) * r, (1.2f + 2.6f * Hash01(seed + 13, i)) * (1f - 0.4f * Mathf.Clamp01(u)), KusariLilac.WithAlpha(0.85f * fade), 8);
            }
        }

        static float KusariCutProfile(float f) => (f < 0.12f ? 0.4f + 5f * f : 1f) * (1f - 0.85f * f * f);  // pressed at the start, flicking thin
        static float KusariRising(float f) => 0.25f + 0.75f * f;
        static float KusariEnsoProfile(float f) => 0.25f + 0.75f * Mathf.Pow(f, 0.6f);
        static float KusariTaper(float f) => 1f - f;
        static float KusariFlat(float f) => 1f;

        /// The sickle cut's ink (the light layer of FxKind.Slash; MeleeLooks.cs): one brush stroke along the blade's path, painted
        /// where you swung from (so it stays put as you move), drying out over KusariDryTime, with droplets flicked off its end.
        void KusariCutLook(Effect fx, V2 o, float reach, float start, float at, float T, Rgba col)
        {
            float sweep = at - start;
            if (Mathf.Abs(sweep) < 0.01f)
                return;
            float s = sweep >= 0f ? 1f : -1f, dry = T < KusariSweepTime ? 0f : Mathf.Clamp01((T - KusariSweepTime) / KusariDryTime);
            if (dry >= 1f)
                return;
            int seed = MeleeSeed(fx);
            KusariBrushArc(fx.Pos, reach - 2f, start, at, 15f, dry, 1f - dry * dry, seed, KusariCutProfile);
            float u = T - KusariSweepTime * 0.9f;
            if (u > 0f)
                KusariDroplets(fx.Pos + V2.FromAngle(at) * reach, u, seed + 5, 6, 26f, at + s * MathX.Pi / 2f, 1f);
        }

        /// The glow pass (DrawMeleeGlow): the throw's line, the whirl's ensō and the splashes of the bites and snaps.
        void DrawKusariInk()
        {
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.Kusarigama || p.Downed)
                    continue;
                if (p.KusariWhirl)
                    DrawKusariEnso(p);
                else if (p.HookT > 0f)
                    DrawKusariThrowInk(p);
            }
            foreach (var fx in game.Effects)
                if (IsKusariRing(fx))
                    DrawKusariSplash(fx);
        }

        void DrawKusariThrowInk(Player p)
        {
            float k = Game.KusariThrowTime - p.HookT;
            if (k < Game.KusariWindUp)
                return;
            V2 hand, pos;
            float sag;
            KusariThrowPose(p, out hand, out pos, out sag);
            V2 from = hand + V2.FromAngle(p.KusariAim) * 36f;  // (the line runs to the weight, so it pulls in with the drag)
            float dry = Mathf.Clamp01((k - Game.KusariImpactAt) / 0.45f);
            KusariBrushLine(from, pos, 9f, dry, 1f - dry * dry, p.Id * 11 + 3, KusariRising);
        }

        void DrawKusariEnso(Player p)
        {
            V2 o = p.Disp;
            float spin = Mathf.Clamp01(p.KusariHeld / Game.KusariSpinUp), orbit = Game.KusariOrbit(spin), ang = p.KusariAng;
            float len = (0.6f + 5.2f * Mathf.Clamp01(p.KusariHeld / Game.KusariWhirlMax)) * spin;  // the brush circle grows as the whirl runs
            KusariBrushArc(o, orbit, ang - len, ang, 14f * spin, 0.15f, 0.75f, p.Id * 37 + 900, KusariEnsoProfile);
            Arc(o, Game.KusariPull, 1f, KusariLilac.WithAlpha(0.1f * spin), 64);  // the pull's edge
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f || e.SpawnT > 0f || e.Def.IsBoss || e.Def.Heavy)
                    continue;
                V2 rel = e.Disp - o;
                float d = rel.Length;
                if (d < 1f || d > Game.KusariPull || d < e.Radius + 40f)
                    continue;
                V2 back = rel / d;  // an ink wisp left behind as it's drawn in
                KusariBrushLine(e.Disp + back * (e.Radius + 2f), e.Disp + back * (e.Radius + 22f), 5f, 0.3f, 0.5f * spin, e.Id * 7, KusariTaper);
            }
        }

        /// The host's lilac rings for the Kusarigama: the weight biting (at the enemy, KusariColor) and the snap (KusariSnapRadius
        /// * 0.8, KusariColor), plus the snap's white ring, which the ink replaces. Off with Visual effects off: the plain rings show.
        bool IsKusariRing(Effect fx)
        {
            if (!fxOn || fx.Kind != FxKind.Ring)
                return false;
            if (SameRgb(fx.Color, KusariLilac))
                return true;
            if (!SameRgb(fx.Color, Rgba.White) || Mathf.Abs(fx.Radius - Game.KusariSnapRadius) > 1f || Mathf.Abs(fx.Dur - 0.3f) > 0.01f)
                return false;
            foreach (var p in game.Players.Values)
                if (p.Weapon != null && p.Weapon.Id == WeaponId.Kusarigama && p.Pos.DistanceTo(fx.Pos) < 40f)
                    return true;
            return false;
        }

        static bool IsKusariSnapRing(Effect fx) => SameRgb(fx.Color, Game.KusariColor) && Mathf.Abs(fx.Radius - Game.KusariSnapRadius * 0.8f) < 1f;

        /// A bite's splash (droplets flung on past the enemy) or the snap's burst (a ring of splatter and droplets all round).
        void DrawKusariSplash(Effect fx)
        {
            if (!SameRgb(fx.Color, KusariLilac))
                return;  // the snap's white ring: the lilac one draws the burst
            if (fx.Dur < KusariSplashLife)
                fx.Dur = KusariSplashLife;  // (front end only: the effect is drawing, nothing reads it)
            float u = fx.T;
            int seed = (int)(fx.Pos.X * 13f + fx.Pos.Y * 7f);
            if (IsKusariSnapRing(fx))
            {
                float k = Mathf.Clamp01(u / 0.5f);
                KusariBrushArc(fx.Pos, 24f + (Game.KusariSnapRadius - 24f) * EaseOut3(Mathf.Clamp01(u / 0.2f)), 0f, MathX.Tau, 10f, k, 1f - k, seed, KusariFlat);
                KusariDroplets(fx.Pos, u, seed + 3, 30, Game.KusariSnapRadius + 30f, 0f, MathX.Tau);
                return;
            }
            float away = 0f, best = float.MaxValue;
            foreach (var p in game.Players.Values)
            {
                float d = p.Disp.DistanceTo(fx.Pos);
                if (p.Weapon != null && p.Weapon.Id == WeaponId.Kusarigama && d < best)
                {
                    best = d;
                    away = (fx.Pos - p.Disp).Angle;
                }
            }
            KusariDroplets(fx.Pos, u, seed, 14, 34f, away, 2.6f);
        }
    }
}
