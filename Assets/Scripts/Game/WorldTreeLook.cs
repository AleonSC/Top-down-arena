using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// How the World Tree and its butterflies look (look A, "Heartwood", of Docs/Mockups/world-tree.html). The tree grows in over
    /// 1.4 s: a soft green disc with a bright rim, a ring of turning leaf runes and a dashed ring, roots spreading out, and a
    /// canopy of layered lobes with gold blooms twinkling in it and leaves drifting down. A pulse ring leaves it every 2 s. The last
    /// 1.2 s it fades. Butterflies are gold, green and cream, flap their wings as they fly and leave a thin glowing trail.
    /// Drawing only: everything is worked out from the zone's replicated timer and the minions' replicated position and heading.
    /// The mockup's additive glow is built from stacked soft discs, and its trails are drawn from where the butterfly has been
    /// on this screen.
    public partial class ArenaApp
    {
        static readonly Rgba TreeAura = new Rgba(0.47f, 0.9f, 0.43f), TreeGlow = new Rgba(0.75f, 1f, 0.59f), TreeRoot = new Rgba(0.27f, 0.2f, 0.13f),
            TreeLo = new Rgba(0.11f, 0.36f, 0.19f), TreeMid = new Rgba(0.2f, 0.55f, 0.25f), TreeHi = new Rgba(0.43f, 0.77f, 0.34f),
            TreeBloom = new Rgba(1f, 0.93f, 0.59f), TreeLeaf = new Rgba(0.63f, 0.9f, 0.43f), WingOrange = new Rgba(1f, 0.63f, 0.27f);
        static readonly Rgba[] WingColors = { new Rgba(1f, 0.84f, 0.43f), new Rgba(0.67f, 1f, 0.59f), new Rgba(1f, 1f, 0.78f) };

        static float TreeHash(float n)
        {
            float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f;
            return x - (float)System.Math.Floor(x);
        }

        /// A filled ellipse (centre, radii, tilt), drawn as a fan.
        void Ellipse(V2 c, float rx, float ry, float rot, Rgba col, int segs = 10)
        {
            SetColor(col);
            V2 ux = V2.FromAngle(rot), uy = new V2(-ux.Y, ux.X);
            V2 prev = c + ux * rx;
            for (int i = 1; i <= segs; i++)
            {
                float a = MathX.Tau * i / segs;
                V2 next = c + ux * (rx * Mathf.Cos(a)) + uy * (ry * Mathf.Sin(a));
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// A soft glow: discs stacked from wide and faint to small and strong.
        void SoftGlow(V2 c, float r, Rgba col, float alpha, int steps = 4)
        {
            for (int i = 0; i < steps; i++)
            {
                float k = (float)(i + 1) / steps;
                Circle(c, r * (1.05f - 0.75f * k), col.WithAlpha(alpha * k * k / steps * 1.6f), 20);
            }
        }

        void DrawWorldTree(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float gr = MathX.Clamp01(age / Game.WorldTreeGrow);
            gr = 1f - (1f - gr) * (1f - gr) * (1f - gr);
            float fade = z.T >= Game.WorldTreeFade ? 1f : MathX.Clamp01(z.T / Game.WorldTreeFade);
            float R = z.Radius * gr, a = fade;
            if (R < 2f)
                return;
            V2 c = z.Pos;
            float sc = gr * (0.85f + 0.15f * fade) * 1.25f;  // the mockup's tree, a little bigger: the arena is drawn smaller than the mockup's stage

            // the ground: a soft glow beyond the rim, the disc, then the rim, the dashed ring, leaf runes and the pulse ring
            SoftGlow(c, R * 1.35f, TreeAura, 0.22f * a, 5);
            Circle(c, R, TreeAura.WithAlpha(0.1f * a), 56);
            Circle(c, R * 0.55f, TreeAura.WithAlpha(0.08f * a), 40);
            Arc(c, R - 1f, 2.4f, TreeGlow.WithAlpha(0.75f * a), 64);
            float spin = age * 0.35f;
            for (int k = 0; k < 28; k++)
                Arc(c, R * 0.86f, 1.4f, TreeGlow.WithAlpha(0.35f * a), 2, spin + MathX.Tau * k / 28f, MathX.Tau / 28f * 0.3f);
            for (int k = 0; k < 8; k++)  // leaf runes
            {
                V2 u = V2.FromAngle(spin + MathX.Tau * k / 8f), s2 = u.Orthogonal();
                V2 p0 = c + u * R * 0.66f, p1 = c + u * R * 0.76f + s2 * 6f, p2 = c + u * R * 0.86f, p3 = c + u * R * 0.76f - s2 * 6f;
                SetColor(TreeGlow.WithAlpha(0.55f * a));
                Quad(p0, p1, p2, p3);
                Line(p0, p2, 1f, TreeGlow.WithAlpha(0.7f * a));
            }
            float pr = MathX.Fmod(age, 2f) / 2f;
            Arc(c, Mathf.Max(2f, R * pr), 2f, TreeGlow.WithAlpha((1f - pr) * 0.45f * a), 56);

            // roots
            for (int i = 0; i < 9; i++)
            {
                float ang = MathX.Tau * i / 9f + 0.3f, len = (34f + (i * 37 % 17)) * sc;
                V2 end = c + V2.FromAngle(ang) * len, ctl = c + V2.FromAngle(ang + 0.5f) * len * 0.6f;
                V2 prev = c;
                for (int s = 1; s <= 5; s++)
                {
                    float u = s / 5f;
                    V2 q = c * ((1f - u) * (1f - u)) + ctl * (2f * (1f - u) * u) + end * (u * u);
                    Line(prev, q, (5f - i % 3) * sc * (1.1f - 0.4f * u), TreeRoot.WithAlpha(0.9f * a));
                    Line(prev, q, 1f, TreeGlow.WithAlpha(0.5f * a));
                    prev = q;
                }
            }

            // the canopy: three layers of lobes, swaying a little, lighter toward the top
            V2 top = c + new V2(0f, -4f * sc);
            for (int layer = 0; layer < 3; layer++)
            {
                float s = layer == 0 ? 1f : layer == 1 ? 0.78f : 0.52f, oy = layer == 0 ? 0f : layer == 1 ? -3f : -6f;
                Rgba body = layer == 0 ? TreeLo : layer == 1 ? TreeMid : TreeHi;
                for (int i = 0; i < 14; i++)
                {
                    float la = MathX.Tau * i / 14f, lr = i % 2 == 1 ? 30f : 36f, rad = (20f + (i % 3) * 4f) * s * sc;
                    V2 lp = new V2(Mathf.Cos(la) * lr * 0.9f + Mathf.Sin(t * 0.8f + i) * 1.6f, Mathf.Sin(la) * lr * 0.9f + Mathf.Cos(t * 0.7f + i) * 1.6f) * s * sc + c + new V2(0f, oy * sc);
                    Circle(lp, rad, body.WithAlpha(a), 14);
                    Circle(lp + new V2(-2.5f, -3f) * sc, rad * 0.62f, body.Mix(Rgba.White, 0.18f).WithAlpha(a), 12);  // a lit side
                }
            }
            Circle(top + new V2(0f, -2f * sc), 17f * sc, TreeHi.WithAlpha(a), 20);
            Circle(top + new V2(-3f * sc, -4f * sc), 10f * sc, TreeHi.Mix(Rgba.White, 0.22f).WithAlpha(a), 16);

            // gold blooms twinkling in it, and the glow of its heart
            int blooms = fxReduced ? 12 : 26;
            for (int i = 0; i < blooms; i++)
            {
                float ba = i * 2.399f, br = Mathf.Sqrt((float)i / blooms) * 50f * sc;
                float tw = 0.55f + 0.45f * Mathf.Sin(t * 2.2f + i * 1.7f);
                Circle(top + V2.FromAngle(ba) * br, (2.1f + tw) * sc, TreeBloom.WithAlpha(0.85f * tw * a), 6);
            }
            SoftGlow(top, 46f * sc, TreeGlow, 0.2f * a, 4);

            // leaves drifting down off the canopy
            if (!fxReduced)
                for (int i = 0; i < 16; i++)
                {
                    float life = MathX.Fmod(age * (0.35f + 0.2f * TreeHash(i)) + TreeHash(i + 40f), 1f);
                    float la = TreeHash(i + 7f) * MathX.Tau, lr = (10f + TreeHash(i + 21f) * R * 0.7f) * gr;
                    V2 lp = c + V2.FromAngle(la) * lr + new V2(Mathf.Sin(life * 5f + i) * 8f, -6f + life * 34f);
                    float al = Mathf.Sin(life * MathX.Pi) * 0.9f * a;
                    Ellipse(lp, 3.4f, 1.9f, i + life * 6f, TreeLeaf.WithAlpha(al), 6);
                }

            // time left
            Arc(c, R + 7f, 2f, new Rgba(1f, 1f, 1f, 0.12f * a), 48);
            Arc(c, R + 7f, 2f, z.Def.Color.WithAlpha(0.85f * a), 48, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)));
        }

        // --- the butterflies -------------------------------------------------------

        readonly Dictionary<int, List<V2>> flyTrails = new Dictionary<int, List<V2>>();
        readonly Dictionary<int, int> flyTrailFrame = new Dictionary<int, int>();

        void DrawButterfly(Minion m, float t)
        {
            Rgba col = WingColors[m.Id % WingColors.Length];
            float born = m.Duration - m.T;
            float fadeIn = MathX.Clamp01(born * 3f);
            V2 at = m.Disp;

            // trail: where it has been, on this screen
            List<V2> tr;
            if (!flyTrails.TryGetValue(m.Id, out tr))
                flyTrails[m.Id] = tr = new List<V2>();
            int frame = Time.frameCount, seen;
            if (!flyTrailFrame.TryGetValue(m.Id, out seen) || seen != frame)
            {
                flyTrailFrame[m.Id] = frame;
                if (tr.Count == 0 || tr[tr.Count - 1].DistanceTo(at) > 2f)
                    tr.Add(at);
                if (tr.Count > 14)
                    tr.RemoveAt(0);
                if (flyTrails.Count > 80)
                    PruneTrails(frame);
            }
            if (!fxReduced)
                for (int i = 1; i < tr.Count; i++)
                {
                    float k = (float)i / tr.Count;
                    Line(tr[i - 1], tr[i], k * 3.4f, col.WithAlpha(k * 0.42f * fadeIn));
                }

            // glow, wings, body
            float s = 1.6f;  // big enough to read at the arena's zoom
            SoftGlow(at, 15f * s, col, 0.5f * fadeIn, 3);
            float flap = t * (20f + (m.Id % 5)) + m.Id * 1.3f;
            float f = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(flap));
            float ang = m.Aim + MathX.Pi / 2f;
            V2 ux = V2.FromAngle(ang), uy = new V2(-ux.Y, ux.X);
            for (int side = -1; side <= 1; side += 2)
            {
                V2 upper = at + (ux * (5f * side * f) + uy * -3f) * s, lower = at + (ux * (4f * side * f) + uy * 5f) * s;
                Ellipse(upper, 6.2f * f * s, 8f * s, ang + (side > 0 ? 1f : -1f) * 0.5f + MathX.Pi / 2f, col.WithAlpha(0.92f * fadeIn));
                Ellipse(lower, 4f * f * s, 5.4f * s, ang - (side > 0 ? 1f : -1f) * 0.3f + MathX.Pi / 2f, WingOrange.WithAlpha(0.82f * fadeIn));
                Circle(at + (ux * (6f * side * f) + uy * -4f) * s, 1.2f * s, new Rgba(1f, 1f, 1f, 0.7f * fadeIn), 5);
            }
            Line(at - uy * (6f * s), at + uy * (6f * s), 1.8f * s, new Rgba(1f, 1f, 1f, 0.95f * fadeIn));
        }

        void PruneTrails(int frame)
        {
            var dead = new List<int>();
            foreach (var kv in flyTrailFrame)
                if (frame - kv.Value > 120)
                    dead.Add(kv.Key);
            foreach (int id in dead)
            {
                flyTrails.Remove(id);
                flyTrailFrame.Remove(id);
            }
        }
    }
}
