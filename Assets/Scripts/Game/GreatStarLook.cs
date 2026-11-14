using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Shuriken's Great Star stance (Core/GreatStar.cs; Docs/Mockups/shuriken-great-star.html, concept B):
    ///   - DrawGreatStarHeld (CharacterModels.cs): the huge star over the Ninja's shoulder while it's in the hand.
    ///   - DrawGreatStarRoute (WorldRenderer.cs, your own Ninja only): the ink route you're drawing, from you to the cursor, and
    ///     how much ink is left.
    ///   - DrawGreatStarShot (WorldShots.cs): the thrown star, spinning, with an ink-violet trail.
    public partial class ArenaApp
    {
        static readonly Rgba GsSteel = new Rgba(0.82f, 0.82f, 0.9f), GsShade = new Rgba(0.45f, 0.43f, 0.56f), GsEdge = new Rgba(0.97f, 0.96f, 1f), GsBore = new Rgba(0.08f, 0.07f, 0.12f);
        const int GsTrailN = 14;

        readonly Dictionary<int, List<V2>> gsTrails = new Dictionary<int, List<V2>>();
        readonly List<int> gsGone = new List<int>();

        /// A four-point star with curved-in edges, R to a tip, turned rot, with a bore in the middle. glow 0-1: the ink halo.
        void DrawGreatStarBody(V2 c, float R, float rot, float alpha, float glow)
        {
            if (glow > 0f)
            {
                Circle(c, R * 1.45f, GreatInk(0.12f * glow * alpha), 20);
                Circle(c, R * 1.1f, GreatInk(0.16f * glow * alpha), 20);
            }
            for (int k = 0; k < 4; k++)
            {
                float a = rot + k * MathX.Pi / 2f, mid = a + MathX.Pi / 4f;
                V2 tip = c + V2.FromAngle(a) * R, next = c + V2.FromAngle(a + MathX.Pi / 2f) * R, ctl = c + V2.FromAngle(mid) * (R * 0.16f);
                V2 prev = tip;
                for (int s = 1; s <= 6; s++)  // the curved-in edge from this tip to the next, lit on the leading half
                {
                    float u = s / 6f, w = 1f - u;
                    V2 q = tip * (w * w) + ctl * (2f * u * w) + next * (u * u);
                    SetColor((u < 0.5f ? GsSteel : GsShade).WithAlpha(alpha));
                    Tri(c, prev, q);
                    prev = q;
                }
            }
            for (int k = 0; k < 4; k++)  // the ridges
            {
                V2 d = V2.FromAngle(rot + k * MathX.Pi / 2f);
                Line(c + d * (R * 0.2f), c + d * (R * 0.9f), Mathf.Max(1f, R * 0.05f), GsEdge.WithAlpha(0.7f * alpha));
            }
            Circle(c, R * 0.15f, GsBore.WithAlpha(alpha), 12);
            Arc(c, R * 0.15f, Mathf.Max(1f, R * 0.05f), GsEdge.WithAlpha(0.8f * alpha), 12);
        }

        static Rgba GreatInk(float a) => Game.GreatStarInk.WithAlpha(a);

        /// The thrown Great Star: a trail of where it has been, faint ghosts behind it, the star.
        void DrawGreatStarShot(Bullet b, float t)
        {
            List<V2> trail;
            if (!gsTrails.TryGetValue(b.Id, out trail))
            {
                if (gsTrails.Count > 8)
                    PruneGreatTrails();
                gsTrails[b.Id] = trail = new List<V2>();
            }
            if (trail.Count == 0 || (trail[trail.Count - 1] - b.Pos).LengthSq > 16f)
            {
                trail.Add(b.Pos);
                if (trail.Count > GsTrailN)
                    trail.RemoveAt(0);
            }
            for (int k = trail.Count - 1; k > 0; k--)
            {
                float f = k / (float)trail.Count;
                Line(trail[k], trail[k - 1], 34f * f, GreatInk(0.28f * f));
            }
            float R = b.Radius + 6f, spin = t * 24f + b.Id;
            V2 back = b.Vel.IsZero ? V2.Zero : b.Vel.Normalized();
            DrawGreatStarBody(b.Pos - back * 10f, R, spin - 0.5f, 0.18f, 0f);
            DrawGreatStarBody(b.Pos, R, spin, 1f, 1f);
        }

        void PruneGreatTrails()
        {
            gsGone.Clear();
            foreach (var id in gsTrails.Keys)
                if (!game.Bullets.ContainsKey(id))
                    gsGone.Add(id);
            foreach (var id in gsGone)
                gsTrails.Remove(id);
        }

        /// The route you're drawing: a soft ink stroke with bright dashes along it, a ring where it ends, and the ink left.
        void DrawGreatStarRoute(Player p, float t)
        {
            var route = game.GreatStarRoute;
            if (route.Count == 0)
                return;
            V2 prev = p.Disp;
            foreach (var q in route)
            {
                Line(prev, q, 14f, GreatInk(0.2f));
                prev = q;
            }
            float creep = MathX.Fmod(t * 60f, 18f), along = 0f;  // the dashes creep along toward the end
            prev = p.Disp;
            foreach (var q in route)
            {
                float seg = prev.DistanceTo(q);
                V2 d = seg > 0f ? (q - prev) / seg : V2.Zero;
                for (float s = -MathX.Fmod(along - creep, 18f); s < seg; s += 18f)
                {
                    float a = Mathf.Max(0f, s), b = Mathf.Min(seg, s + 7f);
                    if (b > a)
                        Line(prev + d * a, prev + d * b, 3f, GreatInk(0.95f));
                }
                along += seg;
                prev = q;
            }
            V2 end = route[route.Count - 1];
            float left = Mathf.Clamp01(1f - Game.InkLength(p.Pos, route) / Game.GreatStarInkMax);
            Arc(end, 9f + 2f * Mathf.Sin(t * 10f), 2f, GreatInk(0.9f), 16);
            V2 bar = end + new V2(14f, -18f);  // the ink left
            Line(bar, bar + new V2(60f, 0f), 4f, new Rgba(0.05f, 0.04f, 0.1f, 0.75f));
            Line(bar, bar + new V2(60f, 0f), 2.5f, GreatInk(0.25f));
            if (left > 0f)
                Line(bar, bar + new V2(60f * left, 0f), 2.5f, left < 0.15f ? new Rgba(1f, 0.55f, 0.6f) : GreatInk(1f));
        }

        /// Is this Ninja's Great Star in their hand (so drawn on them)?
        bool GreatStarHeld(Player p) => Game.InGreatStar(p) && !game.GreatStarOut(p.Id);
    }
}
