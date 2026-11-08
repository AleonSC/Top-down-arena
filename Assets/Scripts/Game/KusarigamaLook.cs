using System;
using UnityEngine;

namespace Arena
{
    /// The Kusarigama's weight (Docs/Mockups/kusarigama-combo.html). Drawing only, with Visual effects on; everything comes from the
    /// replicated Player state, so every screen sees the same thing (Core/Kusarigama.cs).
    ///   - the throw (HookT counting down from KusariThrowTime, KusariAim, HookId): the weight whirls once over your hand, flies to its target
    ///     with a glow, sticks in it and comes back with it in tow;
    ///   - the whirl (KusariWhirl, KusariHeld, KusariAng): a speed-blur arc behind the weight, the chain, a dashed pull ring that turns,
    ///     chevrons running in along it, and a gauge ring that empties over KusariWhirlMax;
    ///   - two diamonds behind you show the combo's step (KusariStep).
    public partial class ArenaApp
    {
        static readonly Rgba KusariLilac = Game.KusariColor;

        void DrawKusarigama(Player p)
        {
            if (!fxOn || p.Downed)
                return;
            float t = Time.unscaledTime;
            V2 o = p.Disp;
            if (p.KusariWhirl)
            {
                DrawKusariWhirl(p, o, t);
                return;
            }
            if (p.HookT > 0f)
                DrawKusariThrow(p, o, t);
            if (p.KusariStep != 0)
                for (int d = 0; d < 2; d++)
                {
                    float da = p.Aim + MathX.Pi + (d - 0.5f) * 0.4f;
                    bool lit = d < p.KusariStep;
                    Polygon(o + V2.FromAngle(da) * 30f, lit ? 5f : 4.3f, 4, 0f, lit ? KusariLilac : new Rgba(1f, 1f, 1f, 0.14f));
                }
        }

        /// The chain: a glowing lilac underlay when it's taut and moving, steel links on top.
        void DrawKusariChain(V2 a, V2 b, float glow)
        {
            if (glow > 0f)
            {
                Line(a, b, 9f, KusariLilac.WithAlpha(0.14f * glow));
                Line(a, b, 4.5f, KusariLilac.WithAlpha(0.3f * glow));
            }
            V2 to = b - a;
            float len = to.Length;
            if (len < 1f)
                return;
            V2 dir = to / len;
            int n = Mathf.Max(2, (int)(len / 8f));
            for (int i = 0; i < n; i++)
            {
                V2 p0 = a + dir * (len * i / n), p1 = a + dir * (len * (i + 0.62f) / n);
                Line(p0, p1, i % 2 == 0 ? 3.4f : 2.2f, new Rgba(0.78f, 0.8f, 0.9f));
            }
            Line(a, b, 1f, Rgba.White.WithAlpha(0.35f + 0.4f * glow));
        }

        /// The weight: a spiked ball, with a glow when it's flying.
        void DrawKusariWeight(V2 at, float glow, float spin)
        {
            GlowDisc(at, 30f + 14f * glow, KusariLilac, 0.35f + 0.6f * glow);
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(spin + i * MathX.Tau / 6f);
                Line(at + d * 8f, at + d * 16f, 4.5f, new Rgba(0.75f, 0.77f, 0.86f));
            }
            Circle(at, 11f, new Rgba(0.85f, 0.87f, 0.95f), 14);
            Circle(at, 9f, new Rgba(0.27f, 0.28f, 0.38f), 14);
            Circle(at + new V2(-3f, -3f), 2.4f, Rgba.White.WithAlpha(0.7f), 6);
            Arc(at, 11f, 1.6f, KusariLilac.WithAlpha(0.5f + 0.5f * glow), 14);
        }

        void DrawKusariThrow(Player p, V2 o, float t)
        {
            float k = Game.KusariThrowTime - p.HookT;
            V2 dir = V2.FromAngle(p.KusariAim);
            V2 hand = o + dir * 12f - dir.Orthogonal() * 10f;
            Enemy e = null;
            bool hooked = p.HookId != 0 && game.Enemies.TryGetValue(p.HookId, out e) && e.Hp > 0f;
            V2 end = hooked ? e.Disp - dir * e.Radius * 0.7f : o + dir * game.KusariReachOf(p);
            V2 pos;
            float glow = 1f, spin = t * 14f;
            if (k < Game.KusariWindUp)
            {
                float u = k / Game.KusariWindUp;
                pos = hand + V2.FromAngle(p.KusariAim - MathX.Tau * (1f - u)) * 36f;  // one turn over your hand
                glow = u * 0.5f;
            }
            else if (k < Game.KusariImpactAt)
            {
                float u = Mathf.Clamp01((k - Game.KusariWindUp) / (Game.KusariImpactAt - Game.KusariWindUp)), eo = 1f - (1f - u) * (1f - u) * (1f - u);
                pos = V2.Lerp(hand + dir * 36f, end, eo);
            }
            else if (k < Game.KusariDragFrom)
                pos = end;
            else
            {
                float u = Mathf.Clamp01((k - Game.KusariDragFrom) / (Game.KusariDragTo - Game.KusariDragFrom)), s = u * u * (3f - 2f * u);
                V2 home = hooked ? hand + dir * (e.Radius + 20f) : hand;
                pos = k < Game.KusariDragTo ? V2.Lerp(end, home, s) : V2.Lerp(home, hand, Mathf.Clamp01((k - Game.KusariDragTo) / (Game.KusariThrowTime - Game.KusariDragTo)));
                glow = 1f - 0.7f * u;
                if (hooked && k < Game.KusariDragTo)
                    pos = e.Disp - dir * e.Radius * 0.7f;  // stuck in it, wherever it is now
            }
            DrawKusariChain(hand, pos, glow);
            DrawKusariWeight(pos, glow, spin);
        }

        void DrawKusariWhirl(Player p, V2 o, float t)
        {
            float spin = Mathf.Clamp01(p.KusariHeld / Game.KusariSpinUp), orbit = Game.KusariOrbit(spin), ang = p.KusariAng;
            float pull = Game.KusariPull;
            // the pull ring, and chevrons running in along it
            Arc(o, pull, 2f, KusariLilac.WithAlpha(0.28f * spin), 48);
            for (int i = 0; i < 10; i++)
            {
                float u = Mathf.Repeat(t * 1.1f + i * 0.17f, 1f), r = pull - u * (pull - 60f), a = i * MathX.Tau / 10f + t * 0.5f + u * 0.5f;
                V2 at = o + V2.FromAngle(a) * r, inward = V2.FromAngle(a) * (r - 12f) + o, side = V2.FromAngle(a + MathX.Pi / 2f) * 5f;
                Rgba c = KusariLilac.WithAlpha(0.55f * spin * (1f - u));
                Line(at - side, inward, 2f, c);
                Line(at + side, inward, 2f, c);
            }
            for (int i = 0; i < 6; i++)
                Arc(o, 80f + i * 20f, 2f, KusariLilac.WithAlpha(0.12f * spin), 12, ang * 0.6f + i * 1.1f, 0.7f);
            // the speed blur behind the weight
            Arc(o, orbit, 2f, KusariLilac.WithAlpha(0.22f * spin), 48);
            for (int i = 0; i < 12; i++)
            {
                float w = Mathf.Max(1f, (16f - i * 1.2f) * spin), al = (1f - i / 12f) * spin;
                Arc(o, orbit, w * 2f, KusariLilac.WithAlpha(0.12f * al), 6, ang - (i + 1) * 0.14f * spin, 0.14f * spin);
                Arc(o, orbit, w, KusariLilac.WithAlpha(0.6f * al), 6, ang - (i + 1) * 0.14f * spin, 0.14f * spin);
                Arc(o, orbit, Mathf.Max(1f, w * 0.3f), Rgba.White.WithAlpha(0.7f * al), 6, ang - (i + 1) * 0.14f * spin, 0.14f * spin);
            }
            V2 hand = o + V2.FromAngle(p.Aim) * 11f;
            V2 bw = o + V2.FromAngle(ang) * orbit;
            DrawKusariChain(hand, bw, spin);
            DrawKusariWeight(bw, spin, t * 18f);
            // the gauge: it empties as the whirl runs out
            Arc(o, 30f, 3f, KusariLilac.WithAlpha(0.9f), 24, -MathX.Pi / 2f, MathX.Tau * (1f - p.KusariHeld / Game.KusariWhirlMax));
        }

        public const float KusariSweepTime = 0.18f, KusariHangTime = 0.5f;  // the sickle's sweep across its arc; how long the crescent it leaves hangs after it

        /// The sickle cut's energy (the light layer of FxKind.Slash; MeleeLooks.cs): a lilac ribbon thick at the blade that thins
        /// to nothing behind it, with a white edge, and the crescent the blade leaves hanging where it passed (drawn at the spot
        /// you swung from, so it stays put as you move), glassy, with a glint running along it, fading over KusariHangTime.
        void KusariCutLook(Effect fx, V2 o, float reach, float start, float at, float T, Rgba col)
        {
            float sweep = at - start;
            if (Mathf.Abs(sweep) < 0.01f)
                return;
            float sweepT = KusariSweepTime, s = sweep >= 0f ? 1f : -1f;
            float fade = T < sweepT ? 1f : 1f - Mathf.Clamp01((T - sweepT) / (KusariHangTime + 0.1f));
            if (fade <= 0f)
                return;
            // the ribbon: the blade's last third of its arc, thickest at the head
            float tail = at - s * Mathf.Min(Mathf.Abs(sweep), 1.3f), inner = reach * 0.42f;
            int n = Mathf.Max(10, Mathf.CeilToInt(Mathf.Abs(at - tail) * 36f));
            float ribbonFade = T < sweepT ? 1f : 1f - Mathf.Clamp01((T - sweepT) / 0.18f);
            if (ribbonFade > 0f)
                for (int i = 0; i < n; i++)
                {
                    float f0 = (float)i / n, f1 = (float)(i + 1) / n;
                    float a0 = tail + (at - tail) * f0, a1 = tail + (at - tail) * f1;
                    float k0 = Mathf.Pow(f0, 1.4f) * ribbonFade, k1 = Mathf.Pow(f1, 1.4f) * ribbonFade;
                    V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                    V2 i0 = o + d0 * (inner + (reach - inner) * 0.3f * (1f - f0)), i1 = o + d1 * (inner + (reach - inner) * 0.3f * (1f - f1));
                    V2 e0 = o + d0 * reach, e1 = o + d1 * reach;
                    VTri(i0, col.WithAlpha(0f), e0, col.WithAlpha(0.6f * k0), e1, col.WithAlpha(0.6f * k1));
                    VTri(i0, col.WithAlpha(0f), e1, col.WithAlpha(0.6f * k1), i1, col.WithAlpha(0f));
                    V2 w0 = o + d0 * (reach - 5f * f0 - 1f), w1 = o + d1 * (reach - 5f * f1 - 1f);
                    VTri(w0, Rgba.White.WithAlpha(0.15f * k0), e0, Rgba.White.WithAlpha(0.95f * k0), e1, Rgba.White.WithAlpha(0.95f * k1));
                    VTri(w0, Rgba.White.WithAlpha(0.15f * k0), e1, Rgba.White.WithAlpha(0.95f * k1), w1, Rgba.White.WithAlpha(0.15f * k1));
                }
            // the hanging crescent, left where the blade passed
            V2 h = fx.Pos;
            float hang = T < sweepT ? 1f : 1f - Mathf.Pow(Mathf.Clamp01((T - sweepT) / KusariHangTime), 2f);
            float run = Mathf.Abs(sweep) * (T < sweepT ? 1f : 1f);
            float from = start;
            Arc(h, reach, 28f, col.WithAlpha(0.13f * hang), 24, from, s * run);
            Arc(h, reach, 7f, col.WithAlpha(0.42f * hang), 24, from, s * run);
            Arc(h, reach, 2.4f, Rgba.White.WithAlpha(0.6f * hang), 24, from, s * run);
            float gl = Mathf.Clamp01((T - sweepT) / (KusariHangTime * 0.8f));
            if (T > sweepT && gl < 1f)
            {
                float ga = from + s * Mathf.Abs(sweep) * gl;
                GlowDisc(h + V2.FromAngle(ga) * reach, 24f, Rgba.White, 0.6f * hang);
                Arc(h, reach, 2.4f, Rgba.White.WithAlpha(0.9f * hang), 6, ga - 0.08f, 0.16f);
            }
        }
    }
}
