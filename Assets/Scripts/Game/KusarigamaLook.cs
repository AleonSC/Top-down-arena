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

        /// The weight: a spiked ball, with a glow when it's flying.
        void DrawKusariWeight(V2 at, float glow, float spin)
        {
            if (glow > 0f)
                GlowDisc(at, 26f, KusariLilac, 0.5f * glow);
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(spin + i * MathX.Tau / 6f);
                Line(at + d * 7f, at + d * 13f, 3.5f, new Rgba(0.68f, 0.7f, 0.79f));
            }
            Circle(at, 8.5f, new Rgba(0.8f, 0.82f, 0.9f), 12);
            Circle(at, 6.8f, new Rgba(0.29f, 0.3f, 0.39f), 12);
            Circle(at + new V2(-2.5f, -2.5f), 1.8f, Rgba.White.WithAlpha(0.55f), 6);
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
            DrawChain(hand, pos, 0.9f);
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
            for (int i = 0; i < 10; i++)
                Arc(o, orbit, Mathf.Max(1f, (9f - i * 0.7f) * spin), KusariLilac.WithAlpha((1f - i / 10f) * 0.5f * spin), 6, ang - (i + 1) * 0.126f * spin, 0.126f * spin);
            V2 hand = o + V2.FromAngle(p.Aim) * 11f;
            V2 w = o + V2.FromAngle(ang) * orbit;
            DrawChain(hand, w, 0.9f);
            DrawKusariWeight(w, spin, t * 18f);
            // the gauge: it empties as the whirl runs out
            Arc(o, 30f, 3f, KusariLilac.WithAlpha(0.9f), 24, -MathX.Pi / 2f, MathX.Tau * (1f - p.KusariHeld / Game.KusariWhirlMax));
        }
    }
}
