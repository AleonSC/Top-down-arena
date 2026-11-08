using System;

namespace Arena
{
    /// The Knight's Halberd swing as one motion, timed from the click (Docs/Mockups/halberd-swing.html, Cleaver look with the
    /// 0.12 s wind-up). The host times its six steps' hits from it (Knight.cs) and each screen draws the pose from it
    /// (Game/HalberdLook.cs), so the blade is where the hit says it is.
    ///   0 - 0.05 s    pull back: the pole draws in through the hands, the blade rises, the shoulders turn away
    ///   0.05 - 0.26   the sweep: the pole runs out to full reach, 80 degrees in 0.21 s (it eases in from behind the first step)
    ///   0.26 - 0.34   with the return sweep: the head turns over (a pull to the end of the arc, a half roll)
    ///   0.34 - 0.55   the return sweep, the other way
    ///   then          a short follow-through and the pole eases back to the ready.
    /// Angles are relative to the aim, positive turning clockwise on screen (the sweep runs from -0.7 to +0.7).
    public static class HalberdMotion
    {
        public const float Sweep = 0.7f;                         // half the sweep, radians (80 degrees across)
        public const float Pull = 0.05f, SweepTime = 0.21f, Gap = 0.08f, Follow = 0.10f, Recover = 0.22f;
        public const float WindA = -1.3f, RestA = -0.95f, Over = 0.14f, FollowA = 0.22f;
        public const float RestK = 0.72f, CoilK = 0.55f;         // how far out the blade is, as a share of the reach
        public const float Start1 = Pull, End1 = Pull + SweepTime, Start2 = End1 + Gap, End2 = Start2 + SweepTime;

        /// Where the pole is: its angle off the aim, the blade's reach as a share of the weapon's, the head's roll
        /// about the pole (radians), how high the blade is held (0 low, 1 raised), and 1 / 2 while a sweep is cutting.
        public struct Pose { public float A, K, Roll, Lift; public int Pass; }

        public static float End(bool up) => up ? End2 : End1;
        public static float Total(bool up) => End(up) + Follow + Recover;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float C01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        static float Eo(float t) { t = 1f - C01(t); return 1f - t * t * t; }
        static float Eio(float t) { t = C01(t); return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f; }

        /// How far one sweep has turned `s` seconds in: it eases in from `aStart` to the first step's angle, then holds the
        /// speed that finishes the whole thing in SweepTime. dir +1 turns up, -1 down.
        public static float PassAngle(float s, float aStart, float edge, float dir)
        {
            float d = Math.Abs(edge - aStart), v = (2f * d + 2f * Sweep) / SweepTime, tau = d > 0f ? 2f * d / v : 0f;
            s = s < 0f ? 0f : (s > SweepTime ? SweepTime : s);
            float p = s < tau ? 0.5f * (v / tau) * s * s : d + v * (s - tau);
            return aStart + dir * p;
        }

        public static Pose At(float c, bool up)
        {
            var o = new Pose { A = RestA, K = RestK, Roll = 0f, Lift = 0.5f };
            if (c <= 0f || c >= Total(up))
                return o;
            float end = End(up);
            if (c < Start1)
            {
                float u = Eio(c / Pull);
                o.A = Lerp(RestA, WindA, u); o.K = Lerp(RestK, CoilK, u); o.Lift = Lerp(0.5f, 1f, u);
            }
            else if (c < End1)
            {
                float s = c - Start1, u = s / SweepTime;
                o.A = PassAngle(s, WindA, -Sweep, 1f); o.K = Lerp(CoilK, 1f, Eo(u / 0.28f)); o.Lift = Lerp(1f, 0.25f, Eio(u / 0.6f)); o.Pass = 1;
            }
            else if (up && c < Start2)
            {
                float u = (c - End1) / Gap;
                o.A = Sweep + Over * Eo(u / 0.6f); o.K = Lerp(1f, 0.97f, u); o.Roll = MathX.Pi * Eio(u); o.Lift = Lerp(0.25f, 0.5f, u);
            }
            else if (up && c < End2)
            {
                float s = c - Start2, u = s / SweepTime;
                o.A = PassAngle(s, Sweep + Over, Sweep, -1f); o.K = 0.97f; o.Roll = MathX.Pi; o.Lift = Lerp(0.5f, 0.25f, Eio(u / 0.4f)); o.Pass = 2;
            }
            else if (c < end + Follow)
            {
                float u = (c - end) / Follow, dir = up ? -1f : 1f, a0 = up ? -Sweep : Sweep;
                o.A = a0 + dir * FollowA * Eo(u); o.K = Lerp(up ? 0.97f : 1f, 0.9f, u); o.Roll = up ? MathX.Pi : 0f; o.Lift = Lerp(0.25f, 0.45f, u);
            }
            else
            {
                float u = Eio((c - end - Follow) / Recover), dir = up ? -1f : 1f, a0 = (up ? -Sweep : Sweep) + dir * FollowA;
                o.A = Lerp(a0, RestA, u); o.K = Lerp(0.9f, RestK, u); o.Roll = up ? MathX.Pi * (1f + u) : 0f; o.Lift = Lerp(0.45f, 0.5f, u);
            }
            return o;
        }

        /// Seconds after the click when the sweep's edge crosses the bearing `rel` (radians off the aim, -Sweep to Sweep).
        /// pass 1 is the first sweep, 2 the return.
        public static float CrossTime(float rel, int pass)
        {
            float aStart = pass == 1 ? WindA : Sweep + Over, edge = pass == 1 ? -Sweep : Sweep;
            float d = Math.Abs(edge - aStart), v = (2f * d + 2f * Sweep) / SweepTime, tau = d > 0f ? 2f * d / v : 0f;
            float p = Math.Abs(rel - aStart);
            float s = p < d ? (tau > 0f ? (float)Math.Sqrt(2f * p * tau / v) : 0f) : tau + (p - d) / v;
            if (s > SweepTime) s = SweepTime;
            return (pass == 1 ? Start1 : Start2) + s;
        }

        /// When step `i` (0..5) of a sweep lands: the edge reaches its bearing. pass 1 sweeps up, 2 back down.
        public static float StepTime(int pass, int i, int steps)
        {
            float f = (float)i / (steps - 1) * 2f - 1f;
            return CrossTime((pass == 1 ? f : -f) * Sweep, pass);
        }
    }
}
