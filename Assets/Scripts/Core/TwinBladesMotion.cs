using System;

namespace Arena
{
    /// The Twin Blades' Crescent Waltz as motion (Docs/Mockups/twin-blades-rework.html, Crescent Waltz): the five chain steps and
    /// Twin Fang as key poses for both hands, timed from the click. The host times each step's hits from these numbers
    /// (TwinBlades.cs) and each screen draws the pose from them (Game/TwinBladesLook.cs), so the sabres are where the hits say.
    ///   1 Rising Cut     right hand, a diagonal cut           4 Pirouette       a reverse turn, both arms out
    ///   2 Falling Cut    left hand, the mirror cut            5 Crescent Shear  both raised behind, a 200 degree crossing sweep
    ///   3 Double Jab     a thrust with each hand              Twin Fang (alt)   both sabres thrown, crossing at the cursor and back
    /// A hand's pose is its angle off the aim and distance from the body's centre (positive angles turn clockwise on screen, so the
    /// right hand sits at +), and its blade's angle off the aim. Tw twists the shoulders; Sp turns the whole body (the Pirouette).
    public static class TwinBladesMotion
    {
        public struct Pose { public float Ra, Rr, Rb, La, Lr, Lb, Tw, Sp; }

        internal struct Key { public float T; public Pose P; public byte E; }  // E: 0 ease in-out, 1 ease out, 2 linear

        public class Step
        {
            public string Name;
            public float Dur;    // seconds the move plays
            public float Next;   // the share of Dur after which the next press counts (1: the chain ends here)
            public float Fwd;    // px the swordsman steps forward with it (their own game moves them)
            public float HotR0, HotR1, HotL0, HotL1;  // the shares of Dur when each blade is cutting (its trail is drawn)
            internal Key[] Keys;
            internal Pose Entry, Exit;  // where it starts (the step before's end), and its end turned back into one turn
        }

        public const float BladeLen = 48f, Recover = 0.25f;
        public const float FangRelease = 0.25f, FangFlight = 0.61f, FangTotal = 0.95f, FangSide = 95f;

        public static readonly Pose Ready = P(0.75f, 18f, 0.35f, -0.75f, 18f, -0.35f);

        static Pose P(float ra, float rr, float rb, float la, float lr, float lb, float tw = 0f, float sp = 0f)
            => new Pose { Ra = ra, Rr = rr, Rb = rb, La = la, Lr = lr, Lb = lb, Tw = tw, Sp = sp };
        static Key K(float t, Pose p, byte e = 0) => new Key { T = t, P = p, E = e };

        public static readonly Step[] Steps =
        {
            new Step { Name = "Rising Cut", Dur = 0.26f, Next = 0.7f, Fwd = 12f, HotR0 = 0.3f, HotR1 = 0.7f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                K(0.3f, P(1.25f, 16f, 2.1f, -0.8f, 16f, -0.55f, 0.3f)), K(0.66f, P(-1.1f, 24f, -1.75f, -0.75f, 16f, -0.5f, -0.3f)), K(1f, P(-0.95f, 22f, -1.4f, -0.8f, 16f, -0.55f, -0.25f), 1) } },
            new Step { Name = "Falling Cut", Dur = 0.26f, Next = 0.7f, Fwd = 12f, HotR0 = 2f, HotR1 = 2f, HotL0 = 0.3f, HotL1 = 0.7f, Keys = new[] {
                K(0.3f, P(0.8f, 16f, 0.55f, -1.25f, 16f, -2.1f, -0.3f)), K(0.66f, P(0.75f, 16f, 0.5f, 1.1f, 24f, 1.75f, 0.3f)), K(1f, P(0.8f, 16f, 0.55f, 0.95f, 22f, 1.4f, 0.25f), 1) } },
            new Step { Name = "Double Jab", Dur = 0.34f, Next = 0.8f, Fwd = 15f, HotR0 = 0.16f, HotR1 = 0.36f, HotL0 = 0.48f, HotL1 = 0.68f, Keys = new[] {
                K(0.12f, P(0.55f, 13f, 0.12f, -0.55f, 13f, -0.12f)), K(0.3f, P(0.1f, 37f, 0.02f, -0.55f, 13f, -0.12f, -0.25f), 1), K(0.44f, P(0.55f, 14f, 0.15f, -0.45f, 14f, -0.12f)),
                K(0.62f, P(0.55f, 14f, 0.15f, -0.1f, 37f, -0.02f, 0.25f), 1), K(1f, P(0.6f, 16f, 0.25f, -0.45f, 18f, -0.2f)) } },
            new Step { Name = "Pirouette", Dur = 0.42f, Next = 0.85f, Fwd = 24f, HotR0 = 0.18f, HotR1 = 0.85f, HotL0 = 0.18f, HotL1 = 0.85f, Keys = new[] {
                K(0.15f, P(1.4f, 21f, 1.5f, -1.4f, 21f, -1.5f, 0f, 0.4f)), K(0.85f, P(1.4f, 23f, 1.55f, -1.4f, 23f, -1.55f, 0f, -MathX.Tau), 2), K(1f, P(1.15f, 20f, 1.25f, -1.15f, 20f, -1.25f, 0f, -MathX.Tau - 0.12f), 1) } },
            new Step { Name = "Crescent Shear", Dur = 0.52f, Next = 1f, Fwd = 18f, HotR0 = 0.42f, HotR1 = 0.64f, HotL0 = 0.42f, HotL1 = 0.64f, Keys = new[] {
                K(0.4f, P(2.25f, 16f, 2.85f, -2.25f, 16f, -2.85f)), K(0.62f, P(-0.85f, 24f, -1.35f, 0.85f, 24f, 1.35f), 1), K(1f, P(-0.7f, 22f, -1.1f, 0.7f, 22f, 1.1f), 1) } },
        };

        /// Twin Fang's throw: the wind-up, the release, empty hands while the sabres fly, the catch.
        public static readonly Step Fang = new Step { Name = "Twin Fang", Dur = FangTotal, Next = 1f, HotR0 = 0.14f, HotR1 = 0.27f, HotL0 = 0.14f, HotL1 = 0.27f, Keys = new[] {
            K(0.14f, P(1.45f, 18f, 2.3f, -1.45f, 18f, -2.3f)), K(0.26f, P(0.45f, 25f, 0.25f, -0.45f, 25f, -0.25f), 1), K(0.82f, P(0.6f, 22f, 0.3f, -0.6f, 22f, -0.3f)), K(1f, Ready, 1) } };

        static TwinBladesMotion()
        {
            Pose prev = Ready;
            foreach (var s in Steps)
            {
                s.Entry = prev;
                s.Exit = Wrap(s.Keys[s.Keys.Length - 1].P);
                prev = s.Exit;
            }
            Steps[0].Entry = Ready;  // (the chain starts over from the guard)
            Fang.Entry = Ready;
            Fang.Exit = Ready;
        }

        static Pose Wrap(Pose p)
        {
            p.Sp = (float)(Math.IEEERemainder(p.Sp, MathX.Tau));
            return p;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float C01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        static float Ease(byte e, float t)
        {
            t = C01(t);
            if (e == 1) { float m = 1f - t; return 1f - m * m * m; }
            if (e == 2) return t;
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public static Pose Mix(Pose a, Pose b, float t) => new Pose
        {
            Ra = Lerp(a.Ra, b.Ra, t), Rr = Lerp(a.Rr, b.Rr, t), Rb = Lerp(a.Rb, b.Rb, t),
            La = Lerp(a.La, b.La, t), Lr = Lerp(a.Lr, b.Lr, t), Lb = Lerp(a.Lb, b.Lb, t), Tw = Lerp(a.Tw, b.Tw, t), Sp = Lerp(a.Sp, b.Sp, t)
        };

        /// The pose `c` seconds into a move: its keys, then back to the guard over Recover once it ends.
        public static Pose At(Step s, float c)
        {
            if (c >= s.Dur)
            {
                float u = C01((c - s.Dur) / Recover);
                return Mix(s.Exit, Ready, Ease(1, u));
            }
            float k = C01(c / s.Dur);
            float t0 = 0f;
            Pose from = s.Entry;
            foreach (var key in s.Keys)
            {
                if (k <= key.T)
                    return Mix(from, key.P, Ease(key.E, (k - t0) / Math.Max(0.0001f, key.T - t0)));
                t0 = key.T;
                from = key.P;
            }
            return from;
        }

        /// The chain step (1 to 5) out of a player's replicated Combo byte (the high bits count swings).
        public static int StepOf(int combo) => combo & 7;

        /// Where one thrown sabre is `q` (0 to 1) into its flight: out on a curve to `target` on its own side (`side` +1 for
        /// the right hand, -1 the left), crossing the other there, then back on the other side to `back` (where its owner is).
        public static V2 FangPos(V2 from, V2 target, V2 back, float q, int side)
        {
            V2 d = target - from;
            V2 n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
            if (q < 0.5f)
            {
                float u = q / 0.5f, t = Ease(1, u) * 0.6f + u * 0.4f;
                return Bez(from, (from + target) * 0.5f + n * (FangSide * side), target, t);
            }
            float w = Ease(0, (q - 0.5f) / 0.5f);
            return Bez(target, (back + target) * 0.5f - n * (FangSide * side * 0.8f), back, w);
        }

        static V2 Bez(V2 a, V2 b, V2 c, float t)
        {
            float m = 1f - t;
            return a * (m * m) + b * (2f * m * t) + c * (t * t);
        }
    }
}
