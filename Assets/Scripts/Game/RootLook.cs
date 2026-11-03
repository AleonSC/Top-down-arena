using UnityEngine;

namespace Arena
{
    /// The Treant Rod's roots (Docs/Mockups/healer-summoner-attacks.html, Root Lash): the lash that whips out along your aim,
    /// and the root that ties you to your Treant. Both are the same gnarled bark with thorns; the lash is quick and bare, the
    /// tether keeps a few green leaves. Drawing only; reach, hits and healing are in Knight.cs and KitWeapons.cs.
    public partial class ArenaApp
    {
        const float RootOut = 0.12f, RootHold = 0.10f, RootBack = 0.20f;  // seconds into the lash: out, held, snapping back

        static readonly Rgba RootBark = new Rgba(0.54f, 0.35f, 0.17f), RootBarkHi = new Rgba(0.77f, 0.54f, 0.28f),
            RootKnot = new Rgba(0.33f, 0.21f, 0.1f), RootThorn = new Rgba(0.85f, 0.79f, 0.63f), RootLeaf = new Rgba(0.35f, 0.77f, 0.42f);

        /// A gnarled root from `from` along `dir`, `len` long: a bark body that tapers to a third of `thick`, a light edge, knots,
        /// alternating thorns, and (if `leafy`) small leaves. `wave` is how far it snakes to the side, `phase` moves the snake.
        /// `tip`: a leaf bud at the end (the lash). Fades with `a`.
        void DrawRoot(V2 from, V2 dir, float len, float thick, float wave, float phase, float a, bool leafy, bool tip)
        {
            if (len < 2f || a <= 0f)
                return;
            V2 side = dir.Orthogonal(), prev = from;
            int n = Mathf.Max(4, (int)(len / 14f));
            for (int i = 1; i <= n; i++)
            {
                float k = (float)i / n, w = Mathf.Lerp(thick, thick * 0.3f, k);
                V2 at = from + dir * (len * k) + side * (Mathf.Sin(k * 9f - phase) * wave * Mathf.Sin(k * Mathf.PI));  // still at both ends
                Line(prev, at, w, RootBark.WithAlpha(a));
                Line(prev, at, w * 0.35f, RootBarkHi.WithAlpha(a));
                if (i % 4 == 2)
                    Circle(at, w * 0.6f, RootKnot.WithAlpha(a), 6);  // a knot
                if (i % 3 == 0)
                {
                    float sd = (i / 3) % 2 == 0 ? 1f : -1f;  // thorns on alternate sides
                    Line(at, at + side * (sd * (w * 0.9f + 3f)) + dir * (w * 0.6f), 2f, RootThorn.WithAlpha(a));
                }
                else if (leafy && i % 3 == 1)
                    Circle(at + side * (((i / 3) % 2 == 0 ? 1f : -1f) * (w * 0.5f + 3f)), 2.5f, RootLeaf.WithAlpha(0.85f * a), 6);
                prev = at;
            }
            if (tip)
            {
                // A bud at the point: a small leaf, so it reads as something growing, not a stick.
                V2 end = prev;
                SetColor(RootLeaf.WithAlpha(a));
                Tri(end + dir * 9f, end + side * 5f - dir * 2f, end - side * 5f - dir * 2f);
                Line(end - dir * 2f, end + dir * 7f, 1f, new Rgba(0.85f, 1f, 0.8f, 0.7f * a));
            }
        }

        /// The Treant: a bark trunk with a light rim and knots, a leafy crown, two glowing eyes, and thorny roots gripping the ground
        /// (the same bark as the lash and the root tether). Dazed (its guard is down): the colours dim and the roots slacken.
        void DrawTreant(Minion m, float t, bool dazed)
        {
            float r = m.Def.Radius * m.Scale;
            V2 c = m.Disp;
            float dim = dazed ? 0.55f : 1f;
            Rgba bark = RootBark.Darkened(dazed ? 0.4f : 0f), hi = RootBarkHi.Darkened(dazed ? 0.4f : 0f), knot = RootKnot, leaf = new Rgba(0.2f, 0.5f, 0.28f).Darkened(dazed ? 0.35f : 0f);
            Circle(c + new V2(0f, r * 0.35f), r * 1.15f, new Rgba(0f, 0f, 0f, 0.22f), 20);  // a shadow
            for (int k = 0; k < 7; k++)  // roots reaching out and gripping, swaying a little
            {
                float a = MathX.Tau * k / 7f + 0.3f, sway = Mathf.Sin(t * 1.4f + k * 1.9f) * (dazed ? 0.02f : 0.09f);
                V2 dir = V2.FromAngle(a + sway), side = dir.Orthogonal();
                V2 mid = c + dir * (r * 1.25f) + side * (r * 0.18f), tip = c + dir * (r * 1.7f);
                Line(c + dir * (r * 0.7f), mid, 4.5f, bark);
                Line(mid, tip, 2.8f, bark);
                Line(mid, tip, 1f, hi);
                Line(tip, tip + dir * 4f + side * 3f, 1.6f, RootThorn.WithAlpha(0.9f));
            }
            Circle(c, r * 1.08f, hi, 22);  // a light rim
            Circle(c, r, bark, 22);
            Arc(c, r * 0.62f, 1.5f, knot, 18, 0.4f, 3.6f);  // rings in the wood
            Circle(c + new V2(r * 0.45f, r * 0.3f), r * 0.2f, knot, 8);  // knots
            Circle(c + new V2(-r * 0.5f, r * 0.45f), r * 0.14f, knot, 8);
            // The crown: three leafy puffs over the top of the trunk, lighter on top.
            float bob = Mathf.Sin(t * 1.6f) * 1.2f;
            Circle(c + new V2(-r * 0.55f, -r * 0.62f + bob), r * 0.66f, leaf, 14);
            Circle(c + new V2(r * 0.5f, -r * 0.68f - bob), r * 0.62f, leaf, 14);
            Circle(c + new V2(0f, -r * 0.98f + bob), r * 0.58f, leaf.Mix(new Rgba(0.45f, 0.8f, 0.4f), 0.35f), 14);
            for (int k = 0; k < 4; k++)  // a few bright leaves
                Circle(c + new V2(Mathf.Sin(k * 2.1f + 1f) * r * 0.7f, -r * (0.7f + 0.2f * Mathf.Cos(k * 1.7f)) + bob), 2.2f, new Rgba(0.6f, 0.9f, 0.45f, 0.9f * dim), 6);
            // Two glowing eyes.
            var eye = new Rgba(1f, 0.82f, 0.4f, dim);
            Circle(c + new V2(-r * 0.3f, r * 0.02f), r * 0.2f, eye.WithAlpha(0.3f * dim), 8);
            Circle(c + new V2(r * 0.3f, r * 0.02f), r * 0.2f, eye.WithAlpha(0.3f * dim), 8);
            Circle(c + new V2(-r * 0.3f, r * 0.02f), r * 0.1f, eye, 6);
            Circle(c + new V2(r * 0.3f, r * 0.02f), r * 0.1f, eye, 6);
        }

        /// FxKind.RootLash: a root whips out from you along your aim (Pos2 = its tip, Radius = half its width), is held for a
        /// beat, then snaps back. f is how far through the effect's life it is.
        void DrawRootLashFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length;
            if (reach < 1f)
                return;
            V2 dir = to / reach;
            float t = f * Game.RootFxTime, len;
            if (t < RootOut)
                len = reach * (1f - (1f - t / RootOut) * (1f - t / RootOut));  // fast, easing into its length
            else if (t < RootOut + RootHold)
                len = reach;
            else
                len = reach * Mathf.Clamp01(1f - (t - RootOut - RootHold) / RootBack);
            float wave = t < RootOut + RootHold ? 6f : 3f;
            // A pale flash where it breaks the ground, then the root itself starting a body-length out from you.
            if (t < 0.1f)
                Arc(fx.Pos + dir * 22f, 8f + 90f * t, 2f, RootThorn.WithAlpha(0.6f * (1f - t / 0.1f)), 10);
            V2 start = fx.Pos + dir * 20f;
            DrawRoot(start, dir, Mathf.Max(0f, len - 20f), fx.Radius * 0.6f, wave, t * 28f, 1f, false, true);
            if (t > RootOut && t < RootOut + RootHold)  // the width it hit: a faint bark-coloured strip, so the line you aimed along reads
                Line(start, start + dir * (reach - 20f), fx.Radius * 2f, RootBark.WithAlpha(0.1f));
        }
    }
}
