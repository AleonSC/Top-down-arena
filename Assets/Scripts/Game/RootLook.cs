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
