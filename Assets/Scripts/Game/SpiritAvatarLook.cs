using UnityEngine;

namespace Arena
{
    /// Spirit Avatar (Core/SpiritAvatar.cs; Docs/Mockups/spirit-avatar.html look C, the Sumi Giant): a giant brushed in ink round the
    /// Ninja, a red seal on its back and a paper mask, its two arms on a huge calligraphy brush. Drawn from the snapshot: Player.SpT
    /// (out; it shudders in its last second), SpAge (the rise), SpWind (the cleave: the wind-up with its floor arc, then the sweep) and
    /// SpAng (the cleave's heading). The body is under the model (DrawSpiritBack) and the arms and brush over it (DrawSpiritFront).
    /// FxKind.SpiritInk: the cleave's stroke and the rise's and release's stains on the floor (DrawSpiritFloor), the release's
    /// splatter and the swallowed shots over everything (DrawSpiritInkFx).
    public partial class ArenaApp
    {
        static readonly Rgba SpInkC = Game.SpInk, SpSealC = Game.SpSeal, SpPaperC = Game.SpPaper, SpWood = new Rgba(0.42f, 0.29f, 0.18f);
        const float SpIdleBlade = 0.55f, SpRecoverTime = 0.6f;

        readonly System.Collections.Generic.Dictionary<int, float> spSweptAt = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.Dictionary<int, float> spLastWind = new System.Collections.Generic.Dictionary<int, float>();

        static float SpEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
        static float SpEaseInOut(float k) { k = Mathf.Clamp01(k); return k < 0.5f ? 2f * k * k : 1f - (-2f * k + 2f) * (-2f * k + 2f) / 2f; }

        /// How far the spirit has formed (0..1), and its alpha with the last second's shudder.
        static float SpForm(Player p) => SpEase(p.SpAge / Game.SpRise);

        struct SpFrame { public V2 O, X, Y; public float S; public V2 At(float x, float y) => O + X * (x * S) + Y * (y * S); }

        SpFrame SpFrameOf(Player p, float t)
        {
            float h = p.SpWind > 0f ? p.SpAng : p.Aim, form = SpForm(p);
            V2 o = p.Disp;
            if (p.SpT < 1f)  // the last second: it shudders before it bursts
            {
                float k = 1f - p.SpT;
                o += new V2(Mathf.Sin(t * 61f), Mathf.Cos(t * 53f)) * (3f * k);
            }
            return new SpFrame { O = o, X = V2.FromAngle(h), Y = V2.FromAngle(h + MathX.Pi / 2f), S = 0.55f + 0.45f * form };
        }

        /// The brush's angle from the heading: resting, drawn back through the wind-up, swept across, eased back (this game's clock).
        float SpBlade(Player p, float t)
        {
            spLastWind.TryGetValue(p.Id, out float last);
            spLastWind[p.Id] = p.SpWind;
            if (last > 1f && p.SpWind <= 0f)
                spSweptAt[p.Id] = t;
            if (p.SpWind > 1f)
                return Game.SpBladeAt(p.SpWind - 1f);
            if (p.SpWind > 0f)
                return Mathf.Lerp(SpIdleBlade, Game.SpBladeFrom, SpEaseInOut(p.SpWind));
            if (spSweptAt.TryGetValue(p.Id, out float at) && t - at < SpRecoverTime)
                return Mathf.Lerp(Game.SpBladeTo, SpIdleBlade, SpEaseInOut((t - at) / SpRecoverTime));
            return SpIdleBlade;
        }

        /// A brushed ellipse outline: a band of quads whose width swells and thins round it (a0..a1, radians round the ellipse).
        void SpInkLoop(SpFrame f, float cx, float cy, float rx, float ry, float w, Rgba col, float a0, float a1, int segs)
        {
            SetColor(col);
            V2 pi = V2.Zero, po = V2.Zero;
            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs, a = Mathf.Lerp(a0, a1, u), ww = w * (0.35f + 0.65f * Mathf.Sin(MathX.Pi * u));
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                V2 ni = f.At(cx + c * (rx - ww / 2f), cy + s * (ry - ww / 2f)), no = f.At(cx + c * (rx + ww / 2f), cy + s * (ry + ww / 2f));
                if (i > 0)
                    Quad(pi, po, no, ni);
                pi = ni; po = no;
            }
        }

        void SpEllipse(SpFrame f, float cx, float cy, float rx, float ry, Rgba col) =>
            Ellipse(f.At(cx, cy), rx * f.S, ry * f.S, f.X.Angle, col, 18);

        /// Under the model: the glow, the telegraphed arc, the brushed body, the seal and the shoulders.
        void DrawSpiritBack(Player p, float t)
        {
            if (!Game.SpOn(p))
                return;
            float form = SpForm(p), a = form;
            var f = SpFrameOf(p, t);
            GlowDisc(f.O, 120f * f.S, SpPaperC, 0.32f * a);
            GlowDisc(f.O, 105f * f.S, SpSealC, 0.12f * a);
            if (p.SpWind > 0f && p.SpWind <= 1f)
                DrawSpiritArc(p, f.O, p.SpAng, p.SpWind);
            // the brushed body: a few rough loops, then the ink fill and a paper edge
            for (int k = 0; k < 6; k++)
            {
                float ox = (TH(k * 3.1f) - 0.5f) * 6f, oy = (TH(k * 5.7f) - 0.5f) * 6f, rx = 30f + TH(k + 2f) * 8f, ry = 54f + TH(k + 7f) * 8f;
                float a0 = TH(k + 11f) * 0.5f + t * 0.15f * (k % 2 == 0 ? 1f : -1f);
                SpInkLoop(f, -6f + ox, oy, rx, ry, 6f + TH(k + 4f) * 8f, SpInkC.WithAlpha((0.55f + TH(k + 9f) * 0.3f) * a), a0, a0 + MathX.Tau - 0.3f - TH(k + 13f) * 0.6f, 28);
            }
            SpEllipse(f, -6f, 0f, 30f, 52f, SpInkC.WithAlpha(0.72f * a));
            SpInkLoop(f, -6f, 0f, 37f, 60f, 1.6f, SpPaperC.WithAlpha(0.55f * a), 0.2f, MathX.Tau - 0.2f, 32);
            // the seal on its back, rocking a little
            float sr = Mathf.Sin(t * 0.8f) * 0.1f;
            V2 sc = f.At(-8f, 0f), sx = V2.FromAngle(f.X.Angle + sr) * (11f * f.S), sy = V2.FromAngle(f.X.Angle + sr + MathX.Pi / 2f) * (11f * f.S);
            SetColor(SpSealC.WithAlpha(0.92f * a));
            Quad(sc - sx - sy, sc + sx - sy, sc + sx + sy, sc - sx + sy);
            var dark = SpInkC.WithAlpha(0.9f * a);
            float lw = 2f * f.S;
            V2 i0 = sc - sx * 0.62f - sy * 0.62f, i1 = sc + sx * 0.62f - sy * 0.62f, i2 = sc + sx * 0.62f + sy * 0.62f, i3 = sc - sx * 0.62f + sy * 0.62f;
            Line(i0, i1, lw, dark); Line(i1, i2, lw, dark); Line(i2, i3, lw, dark); Line(i3, i0, lw, dark);
            Line(sc - sx * 0.35f - sy * 0.35f, sc + sx * 0.35f + sy * 0.35f, lw, dark);
            Line(sc + sx * 0.35f - sy * 0.35f, sc, lw, dark);
            // the shoulders
            for (int s = -1; s <= 1; s += 2)
            {
                Circle(f.At(-4f, s * 52f), 17f * f.S, SpInkC.WithAlpha(0.86f * a), 16);
                Arc(f.At(-4f, s * 52f), 17f * f.S, 1.2f, SpPaperC.WithAlpha(0.5f * a), 16);
            }
            if (p.SpAge < Game.SpRise)  // the rise: ink drops pulling in
                for (int i = 0; i < 14; i++)
                {
                    float dly = TH(i * 4.7f) * 0.35f, ck = Mathf.Clamp01((p.SpAge - dly) / (Game.SpRise * 0.6f));
                    if (ck <= 0f || ck >= 1f)
                        continue;
                    float an = i / 14f * MathX.Tau + TH(i) * 0.5f + 0.8f * (1f - ck), dd = Mathf.Lerp(130f + TH(i + 9f) * 40f, 20f, SpEase(ck));
                    Circle(p.Disp + V2.FromAngle(an) * dd, 3f + 3f * TH(i + 3f), SpInkC.WithAlpha(0.9f), 8);
                }
        }

        /// The wind-up's floor arc: a dashed outline and a red fill sweeping across the way the brush will (k 0..1).
        void DrawSpiritArc(Player p, V2 o, float h, float k)
        {
            float reach = Game.SpReachOf(p), half = Game.SpHalfArc;  // (SpMastered is the host's: other games show the base reach)
            for (int i = 0; i < 20; i++)  // the dashed rim
                if (i % 2 == 0)
                    Arc(o, reach, 2f, SpSealC.WithAlpha(0.5f), 4, h - half + 2f * half * i / 20f, 2f * half / 20f);
            Line(o + V2.FromAngle(h - half) * 30f, o + V2.FromAngle(h - half) * reach, 1.5f, SpSealC.WithAlpha(0.4f));
            Line(o + V2.FromAngle(h + half) * 30f, o + V2.FromAngle(h + half) * reach, 1.5f, SpSealC.WithAlpha(0.4f));
            float e = SpEaseInOut(k), to = -half + 2f * half * e;
            int n = Mathf.Max(2, (int)(24 * e));
            Rgba rim = SpSealC.WithAlpha(0.3f + 0.2f * k), mid = SpSealC.WithAlpha(0f);
            for (int i = 0; i < n; i++)
            {
                float a0 = h - half + (to + half) * i / n, a1 = h - half + (to + half) * (i + 1) / n;
                VTri(o + V2.FromAngle(a0) * 40f, mid, o + V2.FromAngle(a0) * reach, rim, o + V2.FromAngle(a1) * reach, rim);
                VTri(o + V2.FromAngle(a0) * 40f, mid, o + V2.FromAngle(a1) * reach, rim, o + V2.FromAngle(a1) * 40f, mid);
            }
            Arc(o, reach, 3f, SpSealC.WithAlpha(0.9f), Mathf.Max(2, n), h - half, to + half);
        }

        /// Over the model: the two arms, the brush and the mask.
        void DrawSpiritFront(Player p, float t)
        {
            if (!Game.SpOn(p))
                return;
            float a = SpForm(p), wa = SpBlade(p, t);
            var f = SpFrameOf(p, t);
            float gx = Mathf.Cos(wa) * 46f, gy = Mathf.Sin(wa) * 46f;
            for (int s = -1; s <= 1; s += 2)
            {
                float sx = -4f, sy = s * 50f, mx = (sx + gx) / 2f, my = (sy + gy) / 2f, nx = -(gy - sy), ny = gx - sx, nl = Mathf.Max(0.01f, Mathf.Sqrt(nx * nx + ny * ny));
                float ex = mx + nx / nl * 14f * s, ey = my + ny / nl * 14f * s;
                Line(f.At(sx, sy), f.At(ex, ey), 13f * f.S, SpInkC.WithAlpha(0.86f * a));
                Line(f.At(ex, ey), f.At(gx, gy), 12f * f.S, SpInkC.WithAlpha(0.86f * a));
                Circle(f.At(ex, ey), 6.5f * f.S, SpInkC.WithAlpha(0.86f * a), 10);
            }
            float c = Mathf.Cos(wa), s2 = Mathf.Sin(wa), px = -s2, py = c;
            Line(f.At(c * 36f, s2 * 36f), f.At(c * 110f, s2 * 110f), 6f * f.S, SpWood.WithAlpha(a));
            Line(f.At(c * 103f, s2 * 103f), f.At(c * 113f, s2 * 113f), 7.5f * f.S, SpSealC.WithAlpha(a));
            // the bristles: a tapered tuft of ink to the tip
            SetColor(SpInkC.WithAlpha(0.95f * a));
            V2 prevL = f.At(c * 112f + px * 9f, s2 * 112f + py * 9f), prevR = f.At(c * 112f - px * 9f, s2 * 112f - py * 9f);
            for (int i = 1; i <= 8; i++)
            {
                float u = i / 8f, r = Mathf.Lerp(112f, 172f, u), w = 9f * (1f - u * u) + 1f * u * (1f - u) * 8f;
                V2 l = f.At(c * r + px * w, s2 * r + py * w), rr = f.At(c * r - px * w, s2 * r - py * w);
                Quad(prevL, l, rr, prevR);
                prevL = l; prevR = rr;
            }
            Line(f.At(c * 116f + px * 7f, s2 * 116f + py * 7f), f.At(c * 168f, s2 * 168f), 1f, SpPaperC.WithAlpha(0.45f * a));
            Line(f.At(c * 116f - px * 7f, s2 * 116f - py * 7f), f.At(c * 168f, s2 * 168f), 1f, SpPaperC.WithAlpha(0.45f * a));
            Circle(f.At(gx, gy), 8f * f.S, SpInkC.WithAlpha(0.92f * a), 10);
            // the paper mask over the Ninja's head: red brows, ink eyes
            SpEllipse(f, 34f, 0f, 13f, 15f, SpPaperC.WithAlpha(0.93f * a));
            for (int s = -1; s <= 1; s += 2)
            {
                Line(f.At(30f, s * 12f), f.At(38f, s * 9f), 2.6f * f.S, SpSealC.WithAlpha(a));
                Line(f.At(38f, s * 9f), f.At(45f, s * 2f), 2.2f * f.S, SpSealC.WithAlpha(a));
                SpEllipse(f, 40f, s * 6f, 3.5f, 1.8f, SpInkC.WithAlpha(a));
            }
        }

        /// The floor pass: each cleave's stroke and the rise's and release's stains.
        void DrawSpiritFloor()
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.SpiritInk)
                {
                    int kind = (int)(fx.Pos2.X + 0.5f);
                    if (kind == Game.SpFxStroke)
                        DrawSpiritStroke(fx);
                    else if (kind == Game.SpFxRelease || kind == Game.SpFxRise)
                    {
                        float k = Mathf.Clamp01(fx.T / fx.Dur), fade = 1f - k;
                        for (int i = 0; i < 16; i++)
                        {
                            float an = i / 16f * MathX.Tau + TH(i + kind * 7f), d = fx.Radius * (0.4f + 0.6f * TH(i + 3f)) * (kind == Game.SpFxRise ? 1f : SpEase(k * 3f));
                            Circle(fx.Pos + V2.FromAngle(an) * d, 3f + 5f * TH(i + 5f), SpInkC.WithAlpha(0.7f * fade), 8);
                        }
                    }
                }
        }

        /// One cleave's stroke on the floor: it paints across with the brush, holds, then fades.
        void DrawSpiritStroke(Effect fx)
        {
            float reach = fx.Radius, h = fx.Pos2.Y, drawK = Mathf.Clamp01(fx.T / Game.SpSweep);
            float left = fx.Dur - fx.T, a = Mathf.Clamp01(left / 0.8f);
            float a1 = Game.SpBladeAt(drawK);
            SpBand(fx.Pos, h, reach * 0.56f, reach + 14f, Game.SpBladeFrom - 0.04f, a1 + 0.04f, SpPaperC.WithAlpha(0.3f * a));
            SpBand(fx.Pos, h, reach * 0.6f, reach + 6f, Game.SpBladeFrom, a1, SpInkC.WithAlpha(0.94f * a));
            int segs = 32;  // the red rim along its outer edge
            for (int i = 0; i < segs; i++)
            {
                float u0 = i / (float)segs, u1 = (i + 1) / (float)segs;
                V2 p0 = SpBandEdge(fx.Pos, h, reach * 0.6f, reach + 6f, Game.SpBladeFrom, a1, u0), p1 = SpBandEdge(fx.Pos, h, reach * 0.6f, reach + 6f, Game.SpBladeFrom, a1, u1);
                Line(p0, p1, 2f, SpSealC.WithAlpha(0.8f * a));
            }
            for (int k = 0; k < 5; k++)  // dry-brush streaks
            {
                float s0 = Game.SpBladeFrom + 0.3f + k * 0.05f, s1 = a1 - 0.2f - k * 0.04f;
                if (s1 > s0)
                    Arc(fx.Pos, reach * 0.66f + k * 12f * reach / Game.SpReach, 1f, SpPaperC.WithAlpha(0.22f * a), 20, h + s0, s1 - s0);
            }
        }

        static V2 SpBandEdge(V2 o, float h, float r0, float r1, float a0, float a1, float u)
        {
            float an = Mathf.Lerp(a0, a1, u), th = (r1 - r0) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(MathX.Pi * u)), 0.35f);
            return o + V2.FromAngle(h + an) * (r0 + th);
        }

        /// A crescent tapering to points at both ends, from a0 to a1 round the heading.
        void SpBand(V2 o, float h, float r0, float r1, float a0, float a1, Rgba col)
        {
            if (a1 <= a0 + 0.01f)
                return;
            SetColor(col);
            const int segs = 32;
            V2 pin = o + V2.FromAngle(h + a0) * r0, pout = SpBandEdge(o, h, r0, r1, a0, a1, 0f);
            for (int i = 1; i <= segs; i++)
            {
                float u = i / (float)segs;
                V2 nin = o + V2.FromAngle(h + Mathf.Lerp(a0, a1, u)) * r0, nout = SpBandEdge(o, h, r0, r1, a0, a1, u);
                Quad(pin, pout, nout, nin);
                pin = nin; pout = nout;
            }
        }

        /// FxKind.SpiritInk over everything: the rise's and release's rings and splatter, a swallowed shot.
        void DrawSpiritInkFx(Effect fx)
        {
            int kind = (int)(fx.Pos2.X + 0.5f);
            float k = Mathf.Clamp01(fx.T / fx.Dur);
            if (kind == Game.SpFxSwallow)
            {
                Circle(fx.Pos, Mathf.Max(2f, fx.Radius) * (1f + 1.5f * k), SpInkC.WithAlpha(0.8f * (1f - k)), 10);
                Arc(fx.Pos, Mathf.Max(3f, fx.Radius) * (1.5f + 2.5f * k), 1.5f, SpSealC.WithAlpha(0.9f * (1f - k)), 12);
            }
            else if (kind == Game.SpFxRise)
            {
                float rk = SpEase(Mathf.Clamp01((fx.T - Game.SpRise * 0.85f) / 0.35f));
                if (rk > 0f && rk < 1f)
                    Arc(fx.Pos, fx.Radius * (0.4f + 0.6f * rk), 4f * (1f - rk) + 1f, SpSealC.WithAlpha(1f - rk), 40);
            }
            else if (kind == Game.SpFxRelease)
            {
                float e = SpEase(k * 1.6f);
                Arc(fx.Pos, fx.Radius * e, 6f * (1f - k) + 1f, SpInkC.WithAlpha(0.9f * (1f - k)), 40);
                Arc(fx.Pos, fx.Radius * e + 4f, 2f, SpSealC.WithAlpha(0.9f * (1f - k)), 40);
                for (int i = 0; i < 24; i++)
                {
                    float an = i / 24f * MathX.Tau + TH(i * 1.7f), d = fx.Radius * (0.3f + 1.1f * TH(i + 8f)) * e;
                    Circle(fx.Pos + V2.FromAngle(an) * d, (2f + 4f * TH(i + 2f)) * (1f - k * 0.5f), (i % 4 == 0 ? SpSealC : SpInkC).WithAlpha(1f - k), 8);
                }
            }
        }
    }
}
