using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The arena floor's other patterns, picked in Settings > Visual effects > Arena floor (pref "floor_style"; preview
    /// FLOORSTYLE=<n>). Each reacts like the Pressure grid (FloorGridLook.cs): it bends round the players (unless the floor
    /// is set to Static), takes their class colour near them, and kill / blast ripples and your Dodge burst run through it.
    /// All are world-anchored, so they line up across the rooms of any arena, and purely cosmetic (never synced).
    ///   Flux     a needle every 24 px combed along a slow drifting current; needles swirl round players, swing into a
    ///            runner's wake, and a ring combs them outward
    ///   Seismo   horizontal phosphor lines every 16 px (every 4th brighter) with a slow refresh band rolling down; they
    ///            lift into a bump round players that leans behind them, and shake like a seismograph as a ring passes
    ///   Plates   32 px bevelled plates that sink under and round players and flip like a split-flap board as a ring passes
    ///   Circuit  seeded traces on a 32 px grid (straight runs, 45 degree bends, pads); they bow like the grid, and kills,
    ///            dodges and footsteps send pulses racing along them
    public partial class ArenaApp
    {
        public static readonly string[] FloorStyleNames = { "Grid", "Flux", "Seismo", "Plates", "Circuit" };
        int floorStyle;  // pref "floor_style": 0 the Pressure grid, then FloorStyleNames' order
        public static int PreviewFloorStyle = -1;  // the preview's FLOORSTYLE=<n>
        int FloorStyle => PreviewFloorStyle >= 0 ? PreviewFloorStyle : floorStyle;

        /// The pattern keys in Settings > Visual effects (WinChoices' keys, smaller text so five fit in half the panel).
        int FloorStyleChoices(Rect r, int value, bool dim)
        {
            float gap = 4f, w = (r.width - gap * (FloorStyleNames.Length - 1)) / FloorStyleNames.Length;
            int pick = value;
            for (int i = 0; i < FloorStyleNames.Length; i++)
                if (WinKey(new Rect(r.x + i * (w + gap), r.y, w, r.height), FloorStyleNames[i].ToUpperInvariant(), i == value, 14, dim ? CrtUi.Dim : (Color?)null))
                    pick = i;
            return pick;
        }

        /// The chosen pattern over one area (areaLo..areaHi; lo..hi is the part in view).
        void DrawFloorPattern(V2 areaLo, V2 areaHi, V2 lo, V2 hi, Rgba grid)
        {
            switch (FloorStyle)
            {
                case 1: DrawFluxFloor(lo, hi, grid); break;
                case 2: DrawSeismoFloor(lo, hi, grid); break;
                case 3: DrawPlateFloor(lo, hi, grid); break;
                default: DrawCircuitFloor(areaLo, areaHi, lo, hi, grid); break;
            }
        }

        // ---------- shared ----------

        /// How lit a point is by the nearest player (the grid's falloff), and in whose colour.
        float FloorLit(float x, float y, out Rgba col)
        {
            float best = 0f;
            col = new Rgba(1f, 1f, 1f, 1f);
            foreach (var f in floorPulls)
            {
                float k = FloorFalloff(new V2(x, y), f.Pos);
                if (k > best) { best = k; col = f.Col; }
            }
            return best;
        }

        /// The ripples' fronts at a point: their strength (0 when none passes), the outward direction, a signed wave for
        /// shaking, and whether the strongest is a Dodge burst (its colour in dash).
        float FloorRing(float x, float y, out V2 dir, out float wave, out Rgba dash)
        {
            float s = 0f, best = 0f;
            dir = V2.Zero; wave = 0f; dash = new Rgba(0f, 0f, 0f, 0f);
            foreach (var r in ripples)
            {
                float life = r.Life > 0f ? r.Life : RippleLife, fade = 1f - r.T / life;
                if (fade <= 0f)
                    continue;
                float dx = x - r.Pos.X, dy = y - r.Pos.Y, d = Mathf.Sqrt(dx * dx + dy * dy), off = d - r.T * 700f;
                if (off > 120f || off < -120f)
                    continue;
                float band = Mathf.Exp(-off * off / 1600f) * fade * fade * Mathf.Min(1.5f, r.Power + 0.3f);
                if (band < 0.003f)
                    continue;
                s += band;
                if (d > 0.5f)
                    dir += new V2(dx / d, dy / d) * band;
                wave += Mathf.Sin(off * 0.09f) * band;
                if (band > best) { best = band; dash = r.Col; }
            }
            return s;
        }

        bool FloorRingNear(float x, float y, float pad)
        {
            foreach (var r in ripples)
            {
                float life = r.Life > 0f ? r.Life : RippleLife;
                if (r.T >= life)
                    continue;
                float d = (new V2(x, y) - r.Pos).Length;
                if (Mathf.Abs(d - r.T * 700f) < 120f + pad)
                    return true;
            }
            return false;
        }

        bool FloorPullNear(float x, float y, float reach)
        {
            foreach (var f in floorPulls)
                if (Mathf.Abs(f.Pos.X - x) < reach && Mathf.Abs(f.Pos.Y - y) < reach)
                    return true;
            return false;
        }

        /// The colour of a mark: the floor's ink, toward a player's colour near them and toward white (or the dodger's
        /// colour) where a ring passes.
        static Rgba FloorInk(Rgba ink, float a, float lit, Rgba pcol, float ring, Rgba dash, float litA, float ringA)
        {
            var c = ink.WithAlpha(a);
            if (lit > 0.03f)
                c = c.Mix(pcol, Mathf.Min(1f, lit * 1.6f) * 0.75f).WithAlpha(a + lit * litA);
            if (ring > 0.03f)
            {
                var rc = dash.A > 0f ? dash : new Rgba(1f, 1f, 1f, 1f).Mix(ink, 0.35f);
                float k = Mathf.Min(1f, ring);
                c = c.Mix(rc, k).WithAlpha(c.A + k * ringA);
            }
            return c;
        }

        static float Snap(float v, float step, float offset) => Mathf.Ceil((v - offset) / step) * step + offset;

        // ---------- Flux ----------

        const float FluxStep = 24f, FluxLen = 7f;

        static float FluxBase(float x, float y) =>
            Mathf.Sin(x * 0.0023f + y * 0.0011f) * 1.4f + Mathf.Sin(y * 0.0031f - x * 0.0007f) * 1.1f + 0.6f;

        void DrawFluxFloor(V2 lo, V2 hi, Rgba grid)
        {
            bool bend = FloorBends;
            var ink = new Rgba(grid.R, grid.G, grid.B, 1f);
            float baseA = Mathf.Min(0.6f, grid.A * 4.5f), drift = Time.time * 0.04f;
            for (float x = Snap(lo.X + FluxStep / 2f, FluxStep, FluxStep / 2f); x < hi.X; x += FluxStep)
                for (float y = Snap(lo.Y + FluxStep / 2f, FluxStep, FluxStep / 2f); y < hi.Y; y += FluxStep)
                {
                    // the angle in doubled form: a needle has no head, so 0 and 180 degrees are the same
                    float a0 = FluxBase(x, y) + drift, c = Mathf.Cos(2f * a0) * 0.35f, s = Mathf.Sin(2f * a0) * 0.35f, len = 1f;
                    V2 dir;
                    float wave;
                    Rgba dash;
                    float ring = FloorRing(x, y, out dir, out wave, out dash);
                    if (bend)
                    {
                        foreach (var f in floorPulls)
                        {
                            float dx = x - f.Pos.X, dy = y - f.Pos.Y, d2 = dx * dx + dy * dy;
                            if (d2 > 6f * FloorR * FloorR)
                                continue;
                            float k = Mathf.Exp(-d2 / (1.4f * FloorR * FloorR)) * 2f, ta = Mathf.Atan2(dy, dx) + MathX.Pi / 2f;
                            c += Mathf.Cos(2f * ta) * k;  // round the player, like a field round a wire
                            s += Mathf.Sin(2f * ta) * k;
                            float sp = f.Vel.Length;
                            if (sp > 40f)  // and into the wake behind a runner
                            {
                                float wa = Mathf.Atan2(f.Vel.Y, f.Vel.X), m = Mathf.Min(1f, sp / 400f) * 0.7f;
                                c += Mathf.Cos(2f * wa) * k * m;
                                s += Mathf.Sin(2f * wa) * k * m;
                            }
                        }
                        if (ring > 0.01f)  // a ring combs them outward
                        {
                            float ra = Mathf.Atan2(dir.Y, dir.X);
                            c += Mathf.Cos(2f * ra) * ring * 6f;
                            s += Mathf.Sin(2f * ra) * ring * 6f;
                            len += Mathf.Min(1.5f, ring * 1.2f);
                        }
                    }
                    Rgba pcol;
                    float lit = FloorLit(x, y, out pcol), a = Mathf.Atan2(s, c) / 2f, l = FluxLen * len * (1f + lit * 0.5f);
                    float ex = Mathf.Cos(a) * l, ey = Mathf.Sin(a) * l;
                    Line(new V2(x - ex, y - ey), new V2(x + ex, y + ey), 1.4f, FloorInk(ink, baseA, lit, pcol, ring, dash, 0.7f, 0.6f));
                }
        }

        // ---------- Seismo ----------

        const float SeisGap = 16f, SeisSeg = 8f, SeisChunk = 64f;

        void DrawSeismoFloor(V2 lo, V2 hi, Rgba grid)
        {
            bool bend = FloorBends;
            var ink = new Rgba(grid.R, grid.G, grid.B, 1f);
            float t = Time.time;
            for (float y = Snap(lo.Y + SeisGap / 2f, SeisGap, SeisGap / 2f); y < hi.Y; y += SeisGap)
            {
                int n = Mathf.RoundToInt((y - SeisGap / 2f) / SeisGap);
                bool major = (n % 4 + 4) % 4 == 0;
                float phase = Mathf.Repeat(y - t * 160f, 1400f);  // the refresh band rolling down
                float scan = phase < 120f ? phase / 120f : 0f;
                float a = (major ? grid.A * 2.4f : grid.A * 1.2f) + scan * 0.12f;
                var plain = ink.WithAlpha(a);
                float w = major ? 1.2f : 1f;
                float runFrom = lo.X;
                bool inRun = false;
                for (float x = lo.X; x < hi.X; )
                {
                    float end = Mathf.Min(hi.X, Mathf.Floor(x / SeisChunk) * SeisChunk + SeisChunk), mid = (x + end) / 2f;
                    bool detail = FloorPullNear(mid, y, FloorR * 2.6f) || FloorRingNear(mid, y, SeisChunk);
                    if (!detail)
                    {
                        if (!inRun) { runFrom = x; inRun = true; }
                    }
                    else
                    {
                        if (inRun) { Line(new V2(runFrom, y), new V2(x, y), w, plain); inRun = false; }
                        for (float sx = x; sx < end; sx += SeisSeg)
                        {
                            float ex = Mathf.Min(end, sx + SeisSeg), mx = (sx + ex) / 2f;
                            V2 dir;
                            float wave;
                            Rgba dash, pcol;
                            float ring = FloorRing(mx, y, out dir, out wave, out dash), lit = FloorLit(mx, y, out pcol);
                            Line(SeismoAt(sx, y, bend), SeismoAt(ex, y, bend), w, FloorInk(ink, a, lit, pcol, ring, dash, 0.6f, 0.5f));
                        }
                    }
                    x = end;
                }
                if (inRun)
                    Line(new V2(runFrom, y), new V2(hi.X, y), w, plain);
            }
        }

        /// A point on a Seismo line: lifted away from each player (up above them, down below), leaning behind a runner,
        /// and shaken where a ring passes.
        V2 SeismoAt(float x, float y, bool bend)
        {
            if (!bend)
                return new V2(x, y);
            float oy = 0f;
            foreach (var f in floorPulls)
            {
                float dx = x - f.Pos.X + f.Vel.X * 0.09f, dy = y - f.Pos.Y, d2 = dx * dx + dy * dy;
                if (d2 > 9f * FloorR * FloorR)
                    continue;
                oy += (dy >= 0f ? 1f : -1f) * 11f * Mathf.Exp(-d2 / (FloorR * FloorR));
            }
            V2 dir;
            float wave;
            Rgba dash;
            FloorRing(x, y, out dir, out wave, out dash);
            return new V2(x, y + oy + wave * 14f);
        }

        // ---------- Plates ----------

        const float PlateSize = 32f, PlateGap = 3f, PlateFlip = 110f;

        void DrawPlateFloor(V2 lo, V2 hi, Rgba grid)
        {
            bool bend = FloorBends;
            var ink = new Rgba(grid.R, grid.G, grid.B, 1f);
            var lightEdge = new Rgba(1f, 1f, 1f, 1f).Mix(ink, 0.7f).WithAlpha(Mathf.Min(0.45f, grid.A * 3.2f));
            var shadeEdge = ink.WithAlpha(Mathf.Min(0.3f, grid.A * 1.5f));
            for (float px = Snap(lo.X, PlateSize, 0f); px + PlateSize <= hi.X + 0.5f; px += PlateSize)
                for (float py = Snap(lo.Y, PlateSize, 0f); py + PlateSize <= hi.Y + 0.5f; py += PlateSize)
                {
                    float cx = px + PlateSize / 2f, cy = py + PlateSize / 2f;
                    // weight: the nearest player, felt a moment behind them as they move
                    float press = 0f;
                    Rgba pcol = ink;
                    foreach (var f in floorPulls)
                    {
                        float k = Mathf.Max(FloorFalloff(new V2(cx, cy), f.Pos), FloorFalloff(new V2(cx, cy), f.Pos - f.Vel * 0.08f));
                        if (k > press) { press = k; pcol = f.Col; }
                    }
                    float depth = bend ? Mathf.Min(1f, press * 1.8f) : 0f;
                    // a ring flips each plate it passes
                    float flip = 0f, flash = 0f;
                    var fcol = new Rgba(0f, 0f, 0f, 0f);
                    foreach (var r in ripples)
                    {
                        float life = r.Life > 0f ? r.Life : RippleLife, fade = 1f - r.T / life;
                        if (fade <= 0f)
                            continue;
                        float u = (r.T * 700f - (new V2(cx, cy) - r.Pos).Length) / PlateFlip;
                        if (u <= 0f || u >= 1f)
                            continue;
                        float s = Mathf.Sin(u * MathX.Pi) * fade * Mathf.Min(1f, r.Power + 0.4f);
                        if (s > flash) { flash = s; flip = u; fcol = r.Col; }
                    }
                    float inset = PlateGap + depth * 4f, off = depth * 1.5f, w = PlateSize - 2f * inset, h = w;
                    float x = px + inset + off, y = py + inset + off;
                    if (flash > 0f && bend)
                    {
                        float sx = Mathf.Max(0.06f, Mathf.Abs(Mathf.Cos(flip * MathX.Pi)));
                        x += w * (1f - sx) / 2f;
                        w *= sx;
                    }
                    // the bevel: light on the top and left, shade on the bottom and right; a sunk plate the other way round
                    bool sunk = depth > 0.15f;
                    var top = sunk ? shadeEdge : lightEdge;
                    var bottom = sunk ? lightEdge : shadeEdge;
                    float glow = Mathf.Max(press * (bend ? 1f : 1.2f), depth * 0.8f);
                    if (glow > 0.06f)
                    {
                        var pc = ink.Mix(pcol, 0.75f);
                        FillRect(x, y, w, h, pc.WithAlpha(0.11f * glow));
                        top = top.Mix(pc, glow).WithAlpha(Mathf.Max(top.A, 0.55f * glow));
                        bottom = bottom.Mix(pc, glow).WithAlpha(Mathf.Max(bottom.A, 0.4f * glow));
                    }
                    if (flash > 0.02f)
                    {
                        var fc = fcol.A > 0f ? fcol : new Rgba(1f, 1f, 1f, 1f).Mix(ink, 0.4f);
                        FillRect(x, y, w, h, fc.WithAlpha(0.3f * flash));
                        top = top.Mix(fc, flash).WithAlpha(Mathf.Max(top.A, 0.75f * flash));
                        bottom = bottom.Mix(fc, flash).WithAlpha(Mathf.Max(bottom.A, 0.75f * flash));
                    }
                    Line(new V2(x, y), new V2(x + w, y), 1f, top);
                    Line(new V2(x, y), new V2(x, y + h), 1f, top);
                    if (sunk || glow > 0.06f || flash > 0.02f)  // (at rest the shaded side is left out: it's faint, and half the cost)
                    {
                        Line(new V2(x, y + h), new V2(x + w, y + h), 1f, bottom);
                        Line(new V2(x + w, y), new V2(x + w, y + h), 1f, bottom);
                    }
                }
        }

        // ---------- Circuit ----------

        const float TraceStep = 32f;

        class Trace { public V2[] P; public float[] Cum; public float Len; public V2 Min, Max; }
        readonly Dictionary<long, List<Trace>> circuits = new Dictionary<long, List<Trace>>();
        struct FootPulse { public V2 Pos; public float T; public Rgba Col; }
        readonly List<FootPulse> floorSteps = new List<FootPulse>();
        readonly Dictionary<int, float> stepDue = new Dictionary<int, float>();
        int stepFrame = -1;

        static readonly int[] TraceDx = { 1, 1, 0, -1, -1, -1, 0, 1 }, TraceDy = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// An area's traces, laid once from its corners (the same board on every machine, every visit).
        List<Trace> CircuitFor(V2 lo, V2 hi)
        {
            int ax = Mathf.RoundToInt(lo.X), ay = Mathf.RoundToInt(lo.Y), bx = Mathf.RoundToInt(hi.X), by = Mathf.RoundToInt(hi.Y);
            long key = ((long)(ax & 0xffff) << 48) | ((long)(ay & 0xffff) << 32) | ((long)(bx & 0xffff) << 16) | (long)(by & 0xffff);
            List<Trace> list;
            if (circuits.TryGetValue(key, out list))
                return list;
            list = new List<Trace>();
            circuits[key] = list;
            uint seed = ((uint)ax * 73856093u ^ (uint)ay * 19349663u ^ (uint)bx * 83492791u ^ (uint)by * 2654435761u) | 1u;
            System.Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xffffff) / 16777216f; };
            int i0 = Mathf.CeilToInt((lo.X + 16f) / TraceStep), i1 = Mathf.FloorToInt((hi.X - 16f) / TraceStep);
            int j0 = Mathf.CeilToInt((lo.Y + 16f) / TraceStep), j1 = Mathf.FloorToInt((hi.Y - 16f) / TraceStep);
            if (i1 <= i0 || j1 <= j0)
                return list;
            var taken = new HashSet<long>();
            System.Func<int, int, long> cell = (i, j) => ((long)i << 32) ^ (uint)j;
            System.Func<int, int, bool> free = (i, j) => i >= i0 && i <= i1 && j >= j0 && j <= j1 && !taken.Contains(cell(i, j));
            int tries = Mathf.RoundToInt((hi.X - lo.X) * (hi.Y - lo.Y) / 2600f);
            var pts = new List<V2>();
            for (int t = 0; t < tries; t++)
            {
                int i = i0 + (int)(rnd() * (i1 - i0 + 1)), j = j0 + (int)(rnd() * (j1 - j0 + 1)), d = (int)(rnd() * 8f) & 7;
                if (!free(i, j))
                    continue;
                pts.Clear();
                pts.Add(new V2(i * TraceStep, j * TraceStep));
                taken.Add(cell(i, j));
                var cells = new List<long> { cell(i, j) };
                int len = 3 + (int)(rnd() * 12f);
                for (int s = 0; s < len; s++)
                {
                    if (rnd() < 0.22f)
                        d = (d + (rnd() < 0.5f ? 1 : 7)) & 7;
                    int ni = i + TraceDx[d], nj = j + TraceDy[d];
                    if (!free(ni, nj))
                        break;
                    i = ni; j = nj;
                    pts.Add(new V2(i * TraceStep, j * TraceStep));
                    taken.Add(cell(i, j));
                    cells.Add(cell(i, j));
                }
                if (pts.Count < 3)
                    continue;
                foreach (var c in cells)  // keep some room round it, so traces run in loose bundles
                {
                    int ci = (int)(c >> 32), cj = (int)(uint)c;
                    for (int k = 0; k < 8; k++)
                        if (rnd() < 0.5f)
                            taken.Add(cell(ci + TraceDx[k], cj + TraceDy[k]));
                }
                var tr = new Trace { P = pts.ToArray(), Cum = new float[pts.Count] };
                tr.Min = tr.Max = tr.P[0];
                for (int k = 1; k < tr.P.Length; k++)
                {
                    tr.Cum[k] = tr.Cum[k - 1] + (tr.P[k] - tr.P[k - 1]).Length;
                    tr.Min = new V2(Mathf.Min(tr.Min.X, tr.P[k].X), Mathf.Min(tr.Min.Y, tr.P[k].Y));
                    tr.Max = new V2(Mathf.Max(tr.Max.X, tr.P[k].X), Mathf.Max(tr.Max.Y, tr.P[k].Y));
                }
                tr.Len = tr.Cum[tr.Cum.Length - 1];
                list.Add(tr);
            }
            return list;
        }

        static V2 TraceAt(Trace tr, float s)
        {
            s = Mathf.Clamp(s, 0f, tr.Len);
            int k = 1;
            while (k < tr.Cum.Length - 1 && tr.Cum[k] < s)
                k++;
            float seg = tr.Cum[k] - tr.Cum[k - 1], u = seg > 0f ? (s - tr.Cum[k - 1]) / seg : 0f;
            return tr.P[k - 1] + (tr.P[k] - tr.P[k - 1]) * u;
        }

        /// Footsteps: while a player moves, a small pulse leaves their feet about every 0.22 s.
        void TickFloorSteps()
        {
            if (stepFrame == Time.frameCount)
                return;
            stepFrame = Time.frameCount;
            float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            for (int i = floorSteps.Count - 1; i >= 0; i--)
            {
                var s = floorSteps[i];
                s.T += dt;
                if (s.T > 0.9f) floorSteps.RemoveAt(i); else floorSteps[i] = s;
            }
            if (game.Paused)
                return;
            foreach (var p in game.Players.Values)
            {
                V2 v;
                if (!floorVel.TryGetValue(p.Id, out v))
                    continue;
                float due;
                stepDue.TryGetValue(p.Id, out due);
                due -= dt * Mathf.Min(1f, v.Length / 300f);
                if (due <= 0f)
                {
                    due = 0.22f;
                    floorSteps.Add(new FootPulse { Pos = p.Disp, Col = p.Def.Color });
                }
                stepDue[p.Id] = due;
            }
        }

        void DrawCircuitFloor(V2 areaLo, V2 areaHi, V2 lo, V2 hi, Rgba grid)
        {
            TickFloorSteps();
            bool bend = FloorBends;
            var ink = new Rgba(grid.R, grid.G, grid.B, 1f);
            float baseA = Mathf.Min(0.5f, grid.A * 2.6f);
            var traces = CircuitFor(areaLo, areaHi);
            foreach (var tr in traces)
            {
                if (tr.Max.X < lo.X - 8f || tr.Min.X > hi.X + 8f || tr.Max.Y < lo.Y - 8f || tr.Min.Y > hi.Y + 8f)
                    continue;
                for (int k = 1; k < tr.P.Length; k++)
                {
                    V2 a = tr.P[k - 1], b = tr.P[k], m = (a + b) * 0.5f;
                    Rgba pcol;
                    float lit = FloorLit(m.X, m.Y, out pcol);
                    var col = FloorInk(ink, baseA, lit, pcol, 0f, pcol, 0.6f, 0f);
                    if (bend && (FloorPullNear(m.X, m.Y, FloorR * 2.6f) || FloorRingNear(m.X, m.Y, 40f)))
                        for (int q = 0; q < 4; q++)  // cut up, so the bow reads as a curve
                            Line(FloorWarp(a.X + (b.X - a.X) * q / 4f, a.Y + (b.Y - a.Y) * q / 4f),
                                FloorWarp(a.X + (b.X - a.X) * (q + 1) / 4f, a.Y + (b.Y - a.Y) * (q + 1) / 4f), 2f, col);
                    else
                        Line(a, b, 2f, col);
                }
                var padCol = ink.WithAlpha(Mathf.Min(0.6f, baseA * 1.8f));
                foreach (var e in new[] { tr.P[0], tr.P[tr.P.Length - 1] })
                {
                    V2 w = bend ? FloorWarp(e.X, e.Y) : e;
                    FillRect(w.X - 3f, w.Y - 3f, 6f, 6f, padCol);
                }
            }
            // pulses: kills (white, fast, wide), dodges (the dodger's colour) and footsteps (small)
            var white = new Rgba(1f, 1f, 1f, 1f).Mix(ink, 0.35f);
            foreach (var r in ripples)
            {
                float life = r.Life > 0f ? r.Life : RippleLife;
                if (r.T >= life)
                    continue;
                bool dash = r.Col.A > 0f;
                DrawPulses(traces, r.Pos, r.T, dash ? r.Col : white, dash ? 620f : 520f, dash ? 90f : 170f, dash ? 6 : 14, bend, lo, hi);
            }
            foreach (var s in floorSteps)
                DrawPulses(traces, s.Pos, s.T, s.Col, 420f, 40f, 2, bend, lo, hi);
        }

        /// Pulses from the trace points nearest a spot, running both ways along every trace within reach.
        void DrawPulses(List<Trace> traces, V2 from, float age, Rgba col, float speed, float reach, int max, bool bend, V2 lo, V2 hi)
        {
            int n = 0;
            foreach (var tr in traces)
            {
                if (tr.Max.X < from.X - reach || tr.Min.X > from.X + reach || tr.Max.Y < from.Y - reach || tr.Min.Y > from.Y + reach)
                    continue;
                float best = float.MaxValue, s0 = 0f;
                for (int k = 0; k < tr.P.Length; k++)
                {
                    float d = (tr.P[k] - from).Length;
                    if (d < best) { best = d; s0 = tr.Cum[k]; }
                }
                if (best > reach)
                    continue;
                if (++n > max)
                    return;
                float t = age - best / 900f, life = 1f - t * 1.4f;
                if (t <= 0f || life <= 0f)
                    continue;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float s = s0 + dir * speed * t;
                    if (s < 0f || s > tr.Len)
                    {
                        float over = s < 0f ? -s : s - tr.Len;
                        if (over < speed * 0.12f && (dir < 0 ? s0 > 0f : s0 < tr.Len))  // just arrived: the pad flashes
                        {
                            V2 e = TraceAt(tr, s);
                            if (bend) e = FloorWarp(e.X, e.Y);
                            FillRect(e.X - 4f, e.Y - 4f, 8f, 8f, col.WithAlpha(0.9f * life));
                        }
                        continue;
                    }
                    V2 head = TraceAt(tr, s), tail = TraceAt(tr, s - dir * 40f);
                    if (head.X < lo.X - 40f || head.X > hi.X + 40f || head.Y < lo.Y - 40f || head.Y > hi.Y + 40f)
                        continue;
                    if (bend) { head = FloorWarp(head.X, head.Y); tail = FloorWarp(tail.X, tail.Y); }
                    Line(tail, head, 3f, col.WithAlpha(0.35f * life));
                    FillRect(head.X - 2f, head.Y - 2f, 4f, 4f, col.WithAlpha(life));
                }
            }
        }
    }
}
