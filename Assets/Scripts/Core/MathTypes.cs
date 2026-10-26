using System;

namespace Arena
{
    // Small math and color types so the core game has no UnityEngine dependency
    // and can be compiled and tested outside Unity (see Tools/SimTest).

    public struct V2
    {
        public float X, Y;

        public V2(float x, float y) { X = x; Y = y; }

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

        public float DistanceTo(V2 o) => (this - o).Length;
        public float DistanceSq(V2 o) => (this - o).LengthSq;

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

        public static readonly Rgba White = new Rgba(1f, 1f, 1f);
    }

    public static class MathX
    {
        public const float Pi = (float)Math.PI;
        public const float Tau = (float)(Math.PI * 2.0);

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Fmod(float a, float b) => a - b * (float)Math.Floor(a / b);
    }
}
