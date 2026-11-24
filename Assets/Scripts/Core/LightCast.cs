using System;

namespace Arena
{
    /// The frame's shadow casters for the dynamic lights (Game/LightingLook.cs): wall edges as segments, round bodies as
    /// discs. Filled once a frame, read by every light's `LightCast.Cast`.
    public sealed class ShadowScene
    {
        public float[] Segs = new float[512];   // x0, y0, x1, y1 per segment
        public float[] Discs = new float[384];  // x, y, r per disc
        public int SegN, DiscN;

        public void Clear() { SegN = DiscN = 0; }

        public void AddSeg(V2 a, V2 b)
        {
            if (SegN * 4 + 4 > Segs.Length)
                Array.Resize(ref Segs, Segs.Length * 2);
            int i = SegN++ * 4;
            Segs[i] = a.X; Segs[i + 1] = a.Y; Segs[i + 2] = b.X; Segs[i + 3] = b.Y;
        }

        /// A box's four edges (a crate, a chest).
        public void AddBox(V2 c, float hw, float hh)
        {
            V2 a = new V2(c.X - hw, c.Y - hh), b = new V2(c.X + hw, c.Y - hh), d = new V2(c.X + hw, c.Y + hh), e = new V2(c.X - hw, c.Y + hh);
            AddSeg(a, b); AddSeg(b, d); AddSeg(d, e); AddSeg(e, a);
        }

        public void AddDisc(V2 c, float r)
        {
            if (r <= 0f)
                return;
            if (DiscN * 3 + 3 > Discs.Length)
                Array.Resize(ref Discs, Discs.Length * 2);
            int i = DiscN++ * 3;
            Discs[i] = c.X; Discs[i + 1] = c.Y; Discs[i + 2] = r;
        }
    }

    /// One light's visibility fan: N rays sorted by angle, each running Dist out (Reach, or less where something stops it).
    /// The lit floor is the polygon through the rays' ends; everything outside it is in shadow.
    public sealed class LightFan
    {
        public V2 At;
        public float Reach;
        public float[] Ang = new float[256], Dist = new float[256];
        public int N;
        public bool Shadowed;  // something stopped at least one ray
    }

    /// Lights casting shadows: rays from the light to the edges of every caster in reach (a hair either side of each
    /// corner, so the shadow's edge runs straight out from it) plus an even ring for the round rim, each stopped at the
    /// nearest caster. A disc casts as its diameter square to the light (its shadow is as wide as the body); a disc the
    /// light sits in (the shooter's own body, the enemy a blast is on) casts nothing.
    public static class LightCast
    {
        public const int RimRays = 20;
        const float Eps = 0.0012f;

        static float[] lx0 = new float[256], ly0 = new float[256], lx1 = new float[256], ly1 = new float[256];
        static int ln;

        public static void Cast(ShadowScene s, V2 at, float reach, LightFan fan, int rimRays = RimRays)
        {
            fan.At = at;
            fan.Reach = reach;
            fan.N = 0;
            fan.Shadowed = false;
            ln = 0;
            float r2 = reach * reach;
            for (int i = 0; i < s.SegN; i++)
            {
                int k = i * 4;
                float x0 = s.Segs[k], y0 = s.Segs[k + 1], x1 = s.Segs[k + 2], y1 = s.Segs[k + 3];
                if (Math.Max(x0, x1) < at.X - reach || Math.Min(x0, x1) > at.X + reach || Math.Max(y0, y1) < at.Y - reach || Math.Min(y0, y1) > at.Y + reach)
                    continue;
                AddLocal(at, r2, x0, y0, x1, y1);
            }
            for (int i = 0; i < s.DiscN; i++)
            {
                int k = i * 3;
                float cx = s.Discs[k], cy = s.Discs[k + 1], r = s.Discs[k + 2];
                float dx = cx - at.X, dy = cy - at.Y, d2 = dx * dx + dy * dy;
                if (d2 <= (r + 1f) * (r + 1f) || d2 >= (reach + r) * (reach + r))
                    continue;
                float d = (float)Math.Sqrt(d2), nx = -dy / d * r, ny = dx / d * r;
                AddLocal(at, r2, cx + nx, cy + ny, cx - nx, cy - ny);
            }

            int n = 0;
            int need = rimRays + ln * 4;
            if (fan.Ang.Length < need)
            {
                fan.Ang = new float[need * 2];
                fan.Dist = new float[need * 2];
            }
            for (int i = 0; i < rimRays; i++)
                fan.Ang[n++] = MathX.Tau * i / rimRays;
            for (int i = 0; i < ln; i++)
            {
                float a0 = Angle(lx0[i] - at.X, ly0[i] - at.Y), a1 = Angle(lx1[i] - at.X, ly1[i] - at.Y);
                fan.Ang[n++] = Wrap(a0 - Eps); fan.Ang[n++] = Wrap(a0 + Eps);
                fan.Ang[n++] = Wrap(a1 - Eps); fan.Ang[n++] = Wrap(a1 + Eps);
            }
            Array.Sort(fan.Ang, 0, n);
            for (int i = 0; i < n; i++)
            {
                float dx = (float)Math.Cos(fan.Ang[i]), dy = (float)Math.Sin(fan.Ang[i]);
                float best = reach;
                for (int j = 0; j < ln; j++)
                {
                    float ex = lx1[j] - lx0[j], ey = ly1[j] - ly0[j];
                    float den = dx * ey - dy * ex;
                    if (den > -1e-9f && den < 1e-9f)
                        continue;
                    float wx = lx0[j] - at.X, wy = ly0[j] - at.Y;
                    float t = (wx * ey - wy * ex) / den, u = (wx * dy - wy * dx) / den;
                    if (t > 0f && t < best && u >= -1e-4f && u <= 1f + 1e-4f)
                        best = t;
                }
                if (best < reach)
                    fan.Shadowed = true;
                fan.Dist[i] = best;
            }
            fan.N = n;
        }

        /// How far the light at `at` reaches toward `to` before a caster stops it (Reach when nothing does).
        public static float Reach(LightFan fan, V2 to)
        {
            if (fan.N == 0)
                return fan.Reach;
            float a = Angle(to.X - fan.At.X, to.Y - fan.At.Y);
            int hi = 0;
            while (hi < fan.N && fan.Ang[hi] < a)
                hi++;
            int lo = hi == 0 ? fan.N - 1 : hi - 1;
            hi %= fan.N;
            float span = fan.Ang[hi] - fan.Ang[lo];
            if (span <= 0f)
                span += MathX.Tau;
            float f = span < 1e-6f ? 0f : ((a - fan.Ang[lo] + MathX.Tau) % MathX.Tau) / span;
            // the ray-to-ray edge is straight: where the line from the light toward `to` crosses it
            V2 p = fan.At + V2.FromAngle(fan.Ang[lo]) * fan.Dist[lo], q = fan.At + V2.FromAngle(fan.Ang[hi]) * fan.Dist[hi];
            V2 dir = V2.FromAngle(a), e = q - p, w = p - fan.At;
            float den = dir.X * e.Y - dir.Y * e.X;
            if (den > -1e-9f && den < 1e-9f)
                return fan.Dist[lo] + (fan.Dist[hi] - fan.Dist[lo]) * f;
            return (w.X * e.Y - w.Y * e.X) / den;
        }

        /// Is `to` lit by this light (inside its fan and its reach)?
        public static bool Lit(LightFan fan, V2 to) => (to - fan.At).Length < Reach(fan, to) - 0.5f;

        // The part of a segment inside the light's disc, kept for this light's rays.
        static void AddLocal(V2 at, float r2, float x0, float y0, float x1, float y1)
        {
            float ex = x1 - x0, ey = y1 - y0, fx = x0 - at.X, fy = y0 - at.Y;
            float a = ex * ex + ey * ey;
            if (a < 1e-6f)
                return;
            float b = 2f * (fx * ex + fy * ey), c = fx * fx + fy * fy - r2;
            float disc = b * b - 4f * a * c;
            if (disc <= 0f)
                return;
            float sq = (float)Math.Sqrt(disc);
            float t0 = Math.Max(0f, (-b - sq) / (2f * a)), t1 = Math.Min(1f, (-b + sq) / (2f * a));
            if (t1 <= t0)
                return;
            if (ln == lx0.Length)
            {
                Array.Resize(ref lx0, ln * 2); Array.Resize(ref ly0, ln * 2);
                Array.Resize(ref lx1, ln * 2); Array.Resize(ref ly1, ln * 2);
            }
            lx0[ln] = x0 + ex * t0; ly0[ln] = y0 + ey * t0;
            lx1[ln] = x0 + ex * t1; ly1[ln] = y0 + ey * t1;
            ln++;
        }

        static float Angle(float x, float y) => Wrap((float)Math.Atan2(y, x));

        static float Wrap(float a)
        {
            while (a < 0f) a += MathX.Tau;
            while (a >= MathX.Tau) a -= MathX.Tau;
            return a;
        }
    }
}
