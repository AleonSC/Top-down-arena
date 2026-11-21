using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Three reactive menu backgrounds drawn to sit behind every arena (dark navy, neon cyan, a square or hex lattice, the same
    /// look as the Pressure grid and the arena walls): Lattice, Circuit Run and Hex Scan. They read the pointer and clicks
    /// through BackgroundReact.cs and look alive with nobody touching them. Mockups for now: not in the unlock table yet.
    public partial class ArenaApp
    {
        static readonly Rgba ArenaInkDim = new Rgba(0.22f, 0.4f, 0.85f);   // the faint blue of the arena grid
        static readonly Rgba ArenaInkLit = new Rgba(0.35f, 0.92f, 1f);     // the neon cyan of its walls

        void ArenaBackdrop()
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.016f, 0.024f, 0.06f), new Rgba(0.05f, 0.065f, 0.15f));
        }

        /// The pointer, or (with nobody over it) a point wandering on its own, and how hard to lean on it (1 for the real pointer).
        V2 ArenaProbe(float t, out float strength)
        {
            if (ReactPointer(out var p))
            {
                strength = 1f;
                return p;
            }
            strength = 0.6f;
            return new V2(W * (0.5f + 0.32f * Mathf.Sin(t * 0.37f)), H * (0.5f + 0.26f * Mathf.Sin(t * 0.51f + 1f)));
        }

        /// How strongly click ripple `i` is lit at distance `dist` from where it was made: a travelling band that fades as it ages.
        float ArenaWave(float age, float dist, float speed, float width, float power = 1f)
        {
            float life = 1f - age / BgRippleLife;
            float d = dist - age * speed;
            return Mathf.Exp(-d * d / (2f * width * width)) * life * life * power;
        }

        // --- Lattice -------------------------------------------------------------------------------------------------------

        static V2[] latPos = new V2[0];
        static float[] latHot = new float[0];

        /// A square lattice like the arena's own grid. The pointer pulls it in like a gravity well and lights the lines it bends;
        /// a click sends a shockwave through it that lights every row and column it crosses.
        void BgLattice(float t)
        {
            ArenaBackdrop();
            const float cell = 40f;
            int cols = (int)(W / cell) + 2, rows = (int)(H / cell) + 2, n = cols * rows;
            if (latPos.Length != n)
            {
                latPos = new V2[n];
                latHot = new float[n];
            }
            var p = ArenaProbe(t, out float k);
            float pull = (44f + 46f * ReactEnergy) * k;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    var b = new V2(i * cell, j * cell);
                    var d = p - b;
                    float dist = Mathf.Max(1f, d.Length);
                    float g = Mathf.Exp(-dist * dist / (2f * 190f * 190f));
                    var disp = d / dist * Mathf.Min(g * pull, dist * 0.5f);  // (never past the pointer, so it bunches instead of folding)
                    float hot = g * 0.85f * k;
                    disp += new V2(Mathf.Sin(t * 0.8f + i * 0.35f + j * 0.2f), Mathf.Cos(t * 0.7f + j * 0.4f - i * 0.1f)) * 2.5f;
                    for (int r = 0; r < BgRippleSlots; r++)
                        if (ReactRipple(r, t, out var at, out float age))
                        {
                            var o = b - at;
                            float w = ArenaWave(age, o.Length, 380f, 42f, bgRipples[r].Power);
                            disp += o.Normalized() * (w * 24f);
                            hot += w;
                        }
                    latPos[j * cols + i] = b + disp;
                    latHot[j * cols + i] = Mathf.Min(1f, hot);
                }
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                {
                    int a = j * cols + i;
                    if (i + 1 < cols)
                        LatticeEdge(a, a + 1, j % 4 == 0);
                    if (j + 1 < rows)
                        LatticeEdge(a, a + cols, i % 4 == 0);
                }
            for (int a = 0; a < n; a++)
            {
                float h = latHot[a];
                if (h < 0.18f)
                    continue;
                float s = 1.5f + 3.5f * h;
                FillRect(latPos[a].X - s / 2f, latPos[a].Y - s / 2f, s, s, Rgba.White.Mix(ArenaInkLit, 0.35f).WithAlpha(0.35f + 0.6f * h));
            }
            SoftGlow(p, 150f + 130f * ReactEnergy, ArenaInkLit.WithAlpha((0.07f + 0.12f * ReactEnergy) * k));
            ArenaReticle(p, t, k);
        }

        void LatticeEdge(int a, int b, bool major)
        {
            float h = Mathf.Max(latHot[a], latHot[b]);
            var col = ArenaInkDim.Mix(ArenaInkLit, Mathf.Min(1f, h * 1.2f));
            Line(latPos[a], latPos[b], (major ? 1.4f : 1f) + h * 1.3f, col.WithAlpha((major ? 0.16f : 0.1f) + 0.6f * h));
        }

        /// Four small corner brackets and a dot: the arena HUD's own frame, around the pointer.
        void ArenaReticle(V2 p, float t, float k)
        {
            float s = 15f + 2f * Mathf.Sin(t * 3f), L = 6f, a = 0.55f * k;
            var c = ArenaInkLit.WithAlpha(a);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var corner = p + new V2(sx * s, sy * s);
                    Line(corner, corner - new V2(sx * L, 0f), 1.5f, c);
                    Line(corner, corner - new V2(0f, sy * L), 1.5f, c);
                }
            Circle(p, 2f, Rgba.White.WithAlpha(0.8f * k), 8);
        }

        // --- Circuit Run ---------------------------------------------------------------------------------------------------

        sealed class RunRoute
        {
            public V2[] Pts;
            public float[] Cum;   // length up to each point
            public float Len;
            public float Speed, Phase;
        }

        static List<RunRoute> runRoutes;

        const float RunCell = 48f;

        static void BuildRunRoutes()
        {
            runRoutes = new List<RunRoute>();
            int cols = (int)(W / RunCell), rows = (int)(H / RunCell);
            for (int r = 0; r < 18; r++)
            {
                int gx = 1 + (int)(Hash(r * 7 + 1) * (cols - 2)), gy = 1 + (int)(Hash(r * 7 + 2) * (rows - 2));
                bool horiz = Hash(r * 7 + 3) < 0.5f;
                int dir = Hash(r * 7 + 4) < 0.5f ? -1 : 1;
                var pts = new List<V2> { new V2(gx * RunCell, gy * RunCell) };
                int segs = 4 + (int)(Hash(r * 7 + 5) * 4f);
                for (int s = 0; s < segs; s++)
                {
                    int len = 2 + (int)(Hash(r * 31 + s * 5 + 6) * 5f);
                    for (int tries = 0; tries < 2; tries++)
                    {
                        int nx = gx + (horiz ? dir * len : 0), ny = gy + (horiz ? 0 : dir * len);
                        if (nx >= 0 && nx <= cols && ny >= 0 && ny <= rows)
                        {
                            gx = nx; gy = ny;
                            break;
                        }
                        dir = -dir;
                    }
                    pts.Add(new V2(gx * RunCell, gy * RunCell));
                    horiz = !horiz;
                    dir = Hash(r * 31 + s * 5 + 9) < 0.5f ? -1 : 1;
                }
                var arr = pts.ToArray();
                var cum = new float[arr.Length];
                for (int i = 1; i < arr.Length; i++)
                    cum[i] = cum[i - 1] + (arr[i] - arr[i - 1]).Length;
                runRoutes.Add(new RunRoute { Pts = arr, Cum = cum, Len = cum[arr.Length - 1], Speed = 55f + 70f * Hash(r * 13), Phase = Hash(r * 17) });
            }
        }

        static V2 RunAt(RunRoute r, float s)
        {
            s = Mathf.Clamp(s, 0f, r.Len);
            int i = 1;
            while (i < r.Pts.Length - 1 && r.Cum[i] < s)
                i++;
            float seg = r.Cum[i] - r.Cum[i - 1];
            return r.Pts[i - 1] + (r.Pts[i] - r.Pts[i - 1]) * (seg > 0f ? (s - r.Cum[i - 1]) / seg : 0f);
        }

        /// The closest point of route r to p: its distance along the route, and how far away it is.
        static float RunNearest(RunRoute r, V2 p, out float dist)
        {
            float best = float.MaxValue, bs = 0f;
            for (int i = 1; i < r.Pts.Length; i++)
            {
                var a = r.Pts[i - 1];
                var ab = r.Pts[i] - a;
                float l2 = ab.LengthSq, u = l2 > 0f ? Mathf.Clamp01(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / l2) : 0f;
                float d = (a + ab * u - p).Length;
                if (d < best)
                {
                    best = d;
                    bs = r.Cum[i - 1] + u * Mathf.Sqrt(l2);
                }
            }
            dist = best;
            return bs;
        }

        /// Draws the stretch [s0, s1] of a route as lines.
        void RunRange(RunRoute r, float s0, float s1, float width, Rgba col)
        {
            s0 = Mathf.Max(0f, s0);
            s1 = Mathf.Min(r.Len, s1);
            if (s1 <= s0)
                return;
            for (int i = 1; i < r.Pts.Length; i++)
            {
                float a = Mathf.Max(s0, r.Cum[i - 1]), b = Mathf.Min(s1, r.Cum[i]);
                if (b > a)
                    Line(RunAt(r, a), RunAt(r, b), width, col);
            }
        }

        /// Circuit traces across the dark: data packets run along them and quicken near the pointer, the traces it passes light up
        /// and a tap line reaches from it to the nearest one. A click sends a pulse down the trace nearest to it, both ways.
        void BgCircuitRun(float t)
        {
            ArenaBackdrop();
            if (runRoutes == null)
                BuildRunRoutes();
            var p = ArenaProbe(t, out float k);
            // The board's faint dot grid.
            for (float y = RunCell; y < H; y += RunCell)
                for (float x = RunCell; x < W; x += RunCell)
                    FillRect(x - 0.75f, y - 0.75f, 1.5f, 1.5f, ArenaInkDim.WithAlpha(0.22f));
            V2 tapAt = V2.Zero;
            float tapDist = 1e9f;
            for (int ri = 0; ri < runRoutes.Count; ri++)
            {
                var r = runRoutes[ri];
                float s0 = RunNearest(r, p, out float dist);
                float prox = Mathf.Clamp01(1f - dist / 190f);
                prox = prox * prox * k;
                if (dist < tapDist)
                {
                    tapDist = dist;
                    tapAt = RunAt(r, s0);
                }
                // The trace, lit by how close the pointer is (the lit part is the stretch within reach of it).
                RunRange(r, 0f, r.Len, 2f, ArenaInkDim.WithAlpha(0.2f));
                if (prox > 0.01f)
                    RunRange(r, s0 - 260f * prox, s0 + 260f * prox, 2f + 1.5f * prox, ArenaInkLit.WithAlpha(0.15f + 0.55f * prox));
                // Click pulses: from the point nearest each click, both ways down the trace.
                for (int c = 0; c < BgRippleSlots; c++)
                    if (ReactRipple(c, t, out var at, out float age))
                    {
                        float sc = RunNearest(r, at, out float cd);
                        float reach = Mathf.Clamp01(1f - cd / 520f), front = Mathf.Max(0f, age - cd / 900f) * 520f, life = 1f - age / BgRippleLife;
                        if (reach <= 0f || front <= 0f)
                            continue;
                        float a = life * life * (0.35f + 0.65f * reach) * bgRipples[c].Power;
                        for (int dir = -1; dir <= 1; dir += 2)
                        {
                            float hs = sc + dir * front;
                            if (hs < 0f || hs > r.Len)
                                continue;
                            RunRange(r, Mathf.Min(hs, hs - dir * 110f), Mathf.Max(hs, hs - dir * 110f), 3f, ArenaInkLit.WithAlpha(0.55f * a));
                            var head = RunAt(r, hs);
                            SoftGlow(head, 34f, ArenaInkLit.WithAlpha(0.35f * a));
                            Circle(head, 3f, Rgba.White.WithAlpha(0.9f * a), 8);
                        }
                    }
                // Vias at the corners and ends.
                for (int i = 0; i < r.Pts.Length; i++)
                {
                    bool end = i == 0 || i == r.Pts.Length - 1;
                    float near = Mathf.Clamp01(1f - (r.Pts[i] - p).Length / 160f) * k;
                    Arc(r.Pts[i], end ? 5f : 2.4f, end ? 1.5f : 2.4f, (near > 0.01f ? ArenaInkLit : ArenaInkDim).WithAlpha((end ? 0.45f : 0.3f) + 0.5f * near), 12);
                }
                // Packets: a few per trace, the head bright with a short trail, faster and bigger near the pointer.
                for (int q = 0; q < 2; q++)
                {
                    float speed = r.Speed * (1f + 2.4f * prox);
                    float s = MathX.Fmod(t * r.Speed + (r.Phase + q * 0.5f) * r.Len + 140f * prox * Mathf.Sin(t * 2f), r.Len);
                    for (int tr = 5; tr >= 0; tr--)
                    {
                        float f = 1f - tr / 6f;
                        var at = RunAt(r, s - tr * (6f + 0.05f * speed));
                        float sz = (2f + 2.2f * prox) * f;
                        FillRect(at.X - sz / 2f, at.Y - sz / 2f, sz, sz, (tr == 0 ? Rgba.White : ArenaInkLit).WithAlpha((0.25f + 0.65f * f) * (0.55f + 0.45f * prox + (tr == 0 ? 0.2f : 0f))));
                    }
                }
            }
            if (tapDist < 190f)
            {
                float a = (1f - tapDist / 190f) * k;
                var d = (tapAt - p).Normalized();
                for (float s = 0f; s < tapDist - 6f; s += 9f)
                    Line(p + d * s, p + d * Mathf.Min(tapDist, s + 4.5f), 1.2f, ArenaInkLit.WithAlpha(0.5f * a));
                Arc(tapAt, 8f + 2f * Mathf.Sin(t * 5f), 1.5f, ArenaInkLit.WithAlpha(0.8f * a), 16);
            }
            SoftGlow(p, 130f + 100f * ReactEnergy, ArenaInkLit.WithAlpha((0.05f + 0.1f * ReactEnergy) * k));
            ArenaReticle(p, t, k);
        }

        // --- Hex Scan ------------------------------------------------------------------------------------------------------

        /// A hex field under a scanner that follows the pointer: cells inside its ring light up and show scraps of data, a sweep arm
        /// turns round it. A click sends a ring through the field that flashes each cell it crosses.
        void BgHexScan(float t)
        {
            ArenaBackdrop();
            const float R = 36f, dx = R * 1.5f, dy = R * 1.7320508f;
            var p = ArenaProbe(t, out float k);
            float scanR = 250f + 40f * ReactEnergy;
            int cols = (int)(W / dx) + 2, rows = (int)(H / dy) + 2;
            float band = MathX.Fmod(t * 150f, W * 0.5f + H + 500f) - 250f;  // a slow diagonal glint across the field
            for (int i = -1; i < cols; i++)
                for (int j = -1; j < rows; j++)
                {
                    var c = new V2(i * dx, j * dy + ((i & 1) != 0 ? dy / 2f : 0f));
                    float h = Hash(i * 131 + j * 17 + 5);
                    float dp = (c - p).Length;
                    float lit = Mathf.Clamp01(1f - dp / scanR);
                    lit = lit * lit * (3f - 2f * lit) * k;
                    float glint = Mathf.Exp(-Mathf.Pow(c.X * 0.5f + c.Y - band, 2f) / (2f * 36f * 36f)) * 0.3f;
                    float wave = 0f;
                    for (int r = 0; r < BgRippleSlots; r++)
                        if (ReactRipple(r, t, out var at, out float age))
                            wave += ArenaWave(age, (c - at).Length, 520f, 58f, bgRipples[r].Power) * 1.3f;
                    float v = Mathf.Min(1f, Mathf.Max(lit, Mathf.Max(glint, wave)));
                    var col = ArenaInkDim.Mix(ArenaInkLit, v);
                    if (v > 0.2f)  // a lit cell is filled, a little different each (the data)
                    {
                        SetColor(col.WithAlpha(v * (0.06f + 0.14f * h)));
                        for (int e = 0; e < 6; e++)
                            Tri(c, HexCorner(c, R - 2f, e), HexCorner(c, R - 2f, e + 1));
                    }
                    if (v > 0.05f)
                        for (int e = 0; e < 6; e++)
                            Line(HexCorner(c, R - 1f, e), HexCorner(c, R - 1f, e + 1), 1f + 1.2f * v, col.WithAlpha(0.1f + 0.7f * v));
                    else
                        for (int e = 0; e < 3; e++)  // three edges each: the neighbours draw the other three
                            Line(HexCorner(c, R, e), HexCorner(c, R, e + 1), 1f, col.WithAlpha(0.11f));
                    if (v > 0.35f)
                    {
                        if (h > 0.62f)
                            Circle(c, 2f + 2f * v, Rgba.White.Mix(ArenaInkLit, 0.4f).WithAlpha(0.8f * v), 8);
                        else if (h > 0.3f)
                            Line(c - new V2(7f, 0f), c + new V2(7f * (0.3f + h), 0f), 1.5f, ArenaInkLit.WithAlpha(0.5f * v));
                    }
                }
            // The scanner: its ring, a few ticks, and the sweep arm with a fading wake.
            Arc(p, scanR, 1.2f, ArenaInkLit.WithAlpha(0.28f * k), 72);
            Arc(p, scanR * 0.5f, 1f, ArenaInkLit.WithAlpha(0.14f * k), 48);
            for (int tk = 0; tk < 24; tk++)
            {
                var d = V2.FromAngle(tk * MathX.Tau / 24f);
                Line(p + d * (scanR - (tk % 6 == 0 ? 10f : 5f)), p + d * scanR, 1.2f, ArenaInkLit.WithAlpha(0.4f * k));
            }
            float ang = t * 1.7f;
            for (int w = 0; w < 14; w++)
                Arc(p, scanR - 3f, 3f, ArenaInkLit.WithAlpha(0.3f * k * (1f - w / 14f)), 6, ang - (w + 1) * 0.09f, 0.092f);
            Line(p, p + V2.FromAngle(ang) * (scanR - 3f), 1.8f, ArenaInkLit.WithAlpha(0.85f * k));
            ArenaReticle(p, t, k);
        }

        static V2 HexCorner(V2 c, float r, int e) => c + V2.FromAngle(e * MathX.Tau / 6f) * r;
    }
}
