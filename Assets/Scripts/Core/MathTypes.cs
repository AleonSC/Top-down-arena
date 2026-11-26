using System;

namespace Arena
{
    // Small math and color types so the core game has no UnityEngine dependency
    // and can be compiled and tested outside Unity (see Tools/SimTest).

    public struct V2
    {
        public float X, Y;

        public V2(float x, float y) { X = x; Y = y; }

        /// Neither part is NaN or infinite (check numbers that came over the network before using them).
        public bool IsFinite => !float.IsNaN(X) && !float.IsNaN(Y) && !float.IsInfinity(X) && !float.IsInfinity(Y);

        public static readonly V2 Zero = new V2(0f, 0f);

        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.X - b.X, a.Y - b.Y);
        public static V2 operator -(V2 a) => new V2(-a.X, -a.Y);
        public static V2 operator *(V2 a, float s) => new V2(a.X * s, a.Y * s);
        public static V2 operator *(float s, V2 a) => new V2(a.X * s, a.Y * s);
        public static V2 operator /(V2 a, float s) => new V2(a.X / s, a.Y / s);

        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float LengthSq => X * X + Y * Y;
        public bool IsZero => X == 0f && Y == 0f;
        public float Angle => (float)Math.Atan2(Y, X);

        public V2 Normalized()
        {
            float l = Length;
            return l > 1e-6f ? new V2(X / l, Y / l) : Zero;
        }

        public V2 LimitLength(float max)
        {
            float l = Length;
            return l > max && l > 0f ? this * (max / l) : this;
        }

        public V2 Rotated(float a)
        {
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new V2(X * c - Y * s, X * s + Y * c);
        }

        // Perpendicular vector (rotated -90 degrees).
        public V2 Orthogonal() => new V2(Y, -X);

        /// The unit vector at `angle` and its Orthogonal (the -90 degree side): `V2.Axes(e.Facing, out V2 f, out V2 s);`
        public static void Axes(float angle, out V2 forward, out V2 side)
        {
            forward = FromAngle(angle);
            side = forward.Orthogonal();
        }

        public float DistanceTo(V2 o) => (this - o).Length;
        public float DistanceSq(V2 o) => (this - o).LengthSq;

        // Distance tests without the square root (they match `DistanceTo(o) < r` and so on for any r, NaN included).
        public bool CloserThan(V2 o, float r) => r > 0f && DistanceSq(o) < r * r;

        /// Distance from this point to the segment a-b (a segment under 1 px long counts as the point a).
        public float DistanceToSegment(V2 a, V2 b)
        {
            V2 ab = b - a;
            float len2 = ab.LengthSq;
            if (len2 < 1f)
                return DistanceTo(a);
            float t = MathX.Clamp01((this - a).Dot(ab) / len2);
            return DistanceTo(a + ab * t);
        }
        public bool WithinReach(V2 o, float r) => r >= 0f && DistanceSq(o) <= r * r;
        public bool FartherThan(V2 o, float r) => r < 0f || DistanceSq(o) > r * r;
        public bool FartherOrAt(V2 o, float r) => r <= 0f || DistanceSq(o) >= r * r;
        public float Dot(V2 o) => X * o.X + Y * o.Y;

        public V2 Clamp(V2 min, V2 max) =>
            new V2(Math.Max(min.X, Math.Min(max.X, X)), Math.Max(min.Y, Math.Min(max.Y, Y)));

        public static V2 FromAngle(float a) => new V2((float)Math.Cos(a), (float)Math.Sin(a));
        public static V2 Lerp(V2 a, V2 b, float t) => a + (b - a) * t;
    }

    public struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);
        public Rgba Darkened(float f) => new Rgba(R * (1f - f), G * (1f - f), B * (1f - f), A);
        public Rgba Mix(Rgba o, float f) => new Rgba(R + (o.R - R) * f, G + (o.G - G) * f, B + (o.B - B) * f, A);

        public static readonly Rgba White = new Rgba(1f, 1f, 1f);
        public static readonly Rgba Gold = new Rgba(1f, 0.8f, 0.35f);  // the one gold (Game's CrtStyle.Gold is the same): coins, highlights, gilded trim
    }

    public static class MathX
    {
        public const float Pi = (float)Math.PI;
        public const float Tau = (float)(Math.PI * 2.0);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Fmod(float a, float b) => a - b * (float)Math.Floor(a / b);
        public static float MoveTowards(float v, float target, float step) => v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
        /// An angle wrapped into [-Pi, Pi): the signed turn from one heading to another is WrapPi(to - from).
        public static float WrapPi(float a) => Fmod(a + Pi, Tau) - Pi;
        /// Is heading `a` less than `half` (radians) either side of heading `b`?
        public static bool AngleWithin(float a, float b, float half) => Math.Abs(WrapPi(a - b)) < half;

        /// Is a body (centre `pos`, radius `radius`) in the cone from `apex` along `aim`, `half` either side, `reach` long?
        /// The body counts for the reach (out past reach + radius). Within `inner` of the apex it is in (-1: only right on it).
        /// `padBody` widens the angle by the body's size seen from the apex, asin(radius / distance);
        /// `strict` makes the inner and angle tests `<` (else `<=`).
        public static bool InCone(V2 apex, float aim, float half, float reach, V2 pos, float radius, float inner, bool padBody, bool strict = false)
        {
            V2 rel = pos - apex;
            float d = rel.Length;
            if (d > reach + radius)
                return false;
            if ((strict ? d < inner : d <= inner) || rel.IsZero)
                return true;
            float off = Math.Abs(WrapPi(rel.Angle - aim));
            if (padBody)
                return strict ? off < half + (float)Math.Asin(Math.Min(1f, radius / d)) : off <= half + (float)Math.Asin(Math.Min(1f, radius / d));
            return strict ? off < half : off <= half;
        }
    }
}
