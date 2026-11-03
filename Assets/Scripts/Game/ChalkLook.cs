using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Spirit Chalk's ink (Docs/Mockups/totem-staff-rework.html, concept 5): a glowing line of pale ink with a bright head where
    /// it is being drawn; a closed loop turns gold and fills faintly; enemies held in by one wear a ring. Drawing only; what the
    /// ink does is in Chalk.cs (the host sends the dots in every snapshot).
    public partial class ArenaApp
    {
        static readonly Rgba InkCore = new Rgba(0.82f, 0.93f, 1f), InkGlow = new Rgba(0.45f, 0.7f, 1f), InkSealed = new Rgba(1f, 0.9f, 0.6f);
        readonly Dictionary<int, List<V2>> inkRings = new Dictionary<int, List<V2>>();

        struct InkVert { public V2 P; public float Fade; public bool Sealed; }
        readonly List<InkVert> inkRun = new List<InkVert>(), inkSmooth = new List<InkVert>();
        readonly Dictionary<int, int> inkLast = new Dictionary<int, int>();

        void DrawInk(float t)
        {
            var ink = game.Ink;
            if (ink.Count == 0)
                return;
            inkRings.Clear();
            inkLast.Clear();
            for (int k = 0; k < ink.Count; k++)
            {
                var d = ink[k];
                inkLast[d.Owner] = k;  // the newest dot of each drawer: the line their chalk is still on
                if (d.Pen == 0)
                    continue;
                List<V2> ring;
                if (!inkRings.TryGetValue(d.Pen, out ring))
                    inkRings[d.Pen] = ring = new List<V2>();
                ring.Add(d.Pos);
            }
            // A loop's inside, faintly, so you can see what it holds.
            foreach (var ring in inkRings.Values)
            {
                if (ring.Count < 3)
                    continue;
                V2 c = V2.Zero;
                foreach (var q in ring)
                    c += q;
                c = c / ring.Count;
                SetColor(InkSealed.WithAlpha(0.09f + 0.03f * Mathf.Sin(t * 4f)));
                for (int k = 0; k < ring.Count; k++)
                    Tri(c, ring[k], ring[(k + 1) % ring.Count]);
            }
            // Each stroke is one smooth ribbon through its dots (a run of the same owner and stroke), growing out to the live chalk tip.
            int i = 0;
            while (i < ink.Count)
            {
                inkRun.Clear();
                int owner = ink[i].Owner, stroke = ink[i].Stroke, j = i;
                while (j < ink.Count && ink[j].Owner == owner && ink[j].Stroke == stroke && (j == i || ink[j - 1].Pos.DistanceTo(ink[j].Pos) < 40f))
                {
                    var d = ink[j];
                    inkRun.Add(new InkVert { P = d.Pos, Fade = Mathf.Clamp01(d.T / 0.8f) * (0.93f + 0.07f * Mathf.Sin(t * 7f + d.Pos.X * 0.05f)), Sealed = d.Pen != 0 });
                    j++;
                }
                V2 tip;
                bool live = inkLast.ContainsKey(owner) && inkLast[owner] == j - 1 && game.InkTipDisp.TryGetValue(owner, out tip);
                if (live)
                {
                    game.InkTipDisp.TryGetValue(owner, out tip);
                    if (tip.DistanceTo(inkRun[inkRun.Count - 1].P) > 0.5f && tip.DistanceTo(inkRun[inkRun.Count - 1].P) < 60f)
                        inkRun.Add(new InkVert { P = tip, Fade = 1f, Sealed = false });
                }
                else if (inkRun.Count >= 3 && ink[i].Pen != 0 && ink[i].Pen == ink[j - 1].Pen && ink[i].Pos.DistanceTo(ink[j - 1].Pos) < 40f)
                    inkRun.Add(inkRun[0]);  // a sealed loop closes on itself
                Player who;
                game.Players.TryGetValue(owner, out who);
                DrawInkRun(inkRun, Game.ChalkDotR * 2f * (who != null ? Game.AreaMult(who) : 1f));
                i = j;
            }
            // The chalk itself, where a line is being drawn.
            foreach (var kv in game.InkTipDisp)
            {
                Circle(kv.Value, 11f, new Rgba(0.85f, 0.95f, 1f, 0.28f), 14);
                Circle(kv.Value, 4f, Rgba.White, 10);
            }
        }

        /// One stroke as a ribbon: a soft glow, a body, and a bright thread, joined with no seams (one strip, colour per vertex).
        void DrawInkRun(List<InkVert> run, float width)
        {
            if (run.Count == 0)
                return;
            if (run.Count == 1)
            {
                var only = run[0];
                Circle(only.P, width * 0.5f + 5f, (only.Sealed ? InkSealed : InkGlow).WithAlpha(0.12f * only.Fade), 14);
                Circle(only.P, width * 0.35f, (only.Sealed ? InkSealed : InkCore).WithAlpha(0.6f * only.Fade), 12);
                return;
            }
            // Curve through the dots (Catmull-Rom, three steps a gap) so a circle is a circle and not a polygon.
            inkSmooth.Clear();
            int n = run.Count;
            for (int k = 0; k < n - 1; k++)
            {
                V2 p0 = run[Mathf.Max(k - 1, 0)].P, p1 = run[k].P, p2 = run[k + 1].P, p3 = run[Mathf.Min(k + 2, n - 1)].P;
                for (int step = 0; step < 3; step++)
                {
                    float u = step / 3f, u2 = u * u, u3 = u2 * u;
                    V2 at = (p1 * 2f + (p2 - p0) * u + (p0 * 2f - p1 * 5f + p2 * 4f - p3) * u2 + (p1 * 3f - p0 - p2 * 3f + p3) * u3) * 0.5f;
                    inkSmooth.Add(new InkVert { P = at, Fade = Mathf.Lerp(run[k].Fade, run[k + 1].Fade, u), Sealed = u < 0.5f ? run[k].Sealed : run[k + 1].Sealed });
                }
            }
            inkSmooth.Add(run[n - 1]);
            InkRibbon(inkSmooth, width * 0.5f + 6f, InkGlow, new Rgba(1f, 0.78f, 0.35f), 0.17f, true);
            InkRibbon(inkSmooth, width * 0.36f, InkCore, InkSealed, 0.6f, false);
            InkRibbon(inkSmooth, 1.1f, Rgba.White, Rgba.White, 0.9f, false);
            var a = inkSmooth[0]; var z = inkSmooth[inkSmooth.Count - 1];
            Circle(a.P, width * 0.36f, (a.Sealed ? InkSealed : InkCore).WithAlpha(0.6f * a.Fade), 10);
            Circle(z.P, width * 0.36f, (z.Sealed ? InkSealed : InkCore).WithAlpha(0.6f * z.Fade), 10);
        }

        static V2 InkTangent(List<InkVert> v, int j)
        {
            V2 d = v[Mathf.Min(j + 1, v.Count - 1)].P - v[Mathf.Max(j - 1, 0)].P;
            return d.Length < 0.001f ? new V2(1f, 0f) : d.Normalized();
        }

        /// A strip along the run, `half` either side. soft: bright in the middle and clear at the edges (the glow).
        void InkRibbon(List<InkVert> v, float half, Rgba pale, Rgba gold, float alpha, bool soft)
        {
            for (int j = 0; j + 1 < v.Count; j++)
            {
                V2 a = v[j].P, b = v[j + 1].P, na = InkTangent(v, j).Orthogonal() * half, nb = InkTangent(v, j + 1).Orthogonal() * half;
                Rgba ca = (v[j].Sealed ? gold : pale).WithAlpha(alpha * v[j].Fade), cb = (v[j + 1].Sealed ? gold : pale).WithAlpha(alpha * v[j + 1].Fade);
                if (soft)
                {
                    Rgba ea = ca.WithAlpha(0f), eb = cb.WithAlpha(0f);
                    VTri(a, ca, a + na, ea, b, cb); VTri(a + na, ea, b + nb, eb, b, cb);
                    VTri(a, ca, a - na, ea, b, cb); VTri(a - na, ea, b - nb, eb, b, cb);
                }
                else
                {
                    VTri(a + na, ca, a - na, ca, b + nb, cb);
                    VTri(a - na, ca, b - nb, cb, b + nb, cb);
                }
            }
        }

        /// A ring and four turning ticks round each enemy a loop is holding.
        void DrawPennedMarks(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Penned || e.Hp <= 0f)
                    continue;
                float r = e.Radius + 8f;
                Arc(e.Disp, r, 2f, InkSealed.WithAlpha(0.8f), 24);
                for (int k = 0; k < 4; k++)
                {
                    float a = t * 1.5f + k * MathX.Pi / 2f;
                    V2 u = V2.FromAngle(a);
                    Line(e.Disp + u * (r - 3f), e.Disp + u * (r + 4f), 2f, InkSealed.WithAlpha(0.9f));
                }
            }
        }
    }
}
