using System;

namespace Arena
{
    /// The Nunchaku's Twin Storm as motion (Docs/Mockups/ninja-nunchaku.html, Twin Storm): a chuck in each hand, the four combo steps,
    /// the Twin Counter and the Twin Windmill guard as key poses, timed from the press. The host times each step's hits from these
    /// steps' Dur (Nunchaku.cs) and each screen draws the pose from them (Game/NunchakuLook.cs).
    ///   1 Snap               the right chuck whips straight ahead      3 Double Figure-8   both chucks cross twice in front
    ///   2 Backhand           the left chuck sweeps back across         4 Twin Crush        both spin overhead and slam together
    ///   5 Twin Counter       both lash straight ahead (from the guard)   the guard          both spin a disc at each side, in front
    /// A hand is a point in the aim's frame (X ahead, Y toward the right hand) and its handle's angle off the aim (positive turns
    /// clockwise on screen, toward the right). The swinging stick trails the handle: it points where the handle pointed Lag seconds
    /// before (FreeAngle), which is what gives the whip.
    public static class NunchakuMotion
    {
        public struct Hand { public float X, Y, Th; }
        public struct Pose { public Hand R, L; }

        internal struct Key { public float T, X, Y, Th; public byte E; }  // E: 0 ease in-out, 1 ease out, 2 linear, 3 ease in

        public class Step
        {
            public string Name;
            public float Dur;   // seconds the move plays
            public float Next;  // the share of Dur after which the next press counts
            internal Key[] R, L;  // null: that hand rests
        }

        public const float Handle = 24f, Link = 10f, Stick = 24f;  // the held stick, the chain, the swinging stick (px)
        public const float Lag = 0.045f, Whip = 1.15f, Recover = 0.12f;
        public const float Rest = 2.6f;  // the handle's angle at rest: tucked back along the forearm
        const float S = 1.15f;           // the mockup's units to the arena's
        public const float GuardTime = 2.4f, GuardSpin = MathX.Tau * 10f / GuardTime;  // the guard's discs: turns a second (rad/s)
        public const float GuardSpinUp = 0.2f;  // seconds the discs take to come up to speed (raising the guard, or back from the counter)
        public const float GuardX = 18f * S, GuardY = 12f * S;  // where each disc spins, in the aim's frame
        public static float Reach => Handle + Link + Stick;

        /// A key from the mockup: u, the handle's angle, the hand (mockup units) and the body's lunge (added to the hand here).
        static Key K(float t, float th, float x, float y, float bx = 0f, byte e = 0) => new Key { T = t, X = (x + bx) * S, Y = y * S, Th = th };
        static Key K(float t, float th, float x, float y, float bx, char e) => new Key { T = t, X = (x + bx) * S, Y = y * S, Th = th, E = (byte)(e == 'o' ? 1 : e == 'l' ? 2 : e == 'i' ? 3 : 0) };

        static Key[] Mirror(Key[] k)
        {
            var m = new Key[k.Length];
            for (int i = 0; i < k.Length; i++)
                m[i] = new Key { T = k[i].T, X = k[i].X, Y = -k[i].Y, Th = -k[i].Th, E = k[i].E };
            return m;
        }

        const float R = Rest, Tau = MathX.Tau, Pi4 = 2f * MathX.Tau;
        static readonly Key[] Snap = { K(0f, R, 6, 13), K(0.24f, 3.5f, 0, 15, -3), K(0.52f, 0.05f, 22, 5, 14, 'o'), K(0.7f, 0.05f, 20, 5, 12), K(1f, R, 6, 13) };
        static readonly Key[] Backhand = { K(0f, R, 6, 13), K(0.22f, 1.6f, 10, 15, 4), K(0.55f, -1.4f, 15, -8, 10, 'o'), K(1f, R - Tau, 6, 13) };
        static readonly Key[] Fig8 = { K(0f, R, 6, 13), K(0.14f, 1.3f, 14, 7, 6), K(0.38f, -1.3f, 16, -4, 12, 'o'), K(0.62f, 1.3f, 16, 5, 12), K(0.8f, 0.4f, 14, 6, 8), K(1f, R, 6, 13) };
        static readonly Key[] Crush = { K(0f, R, 6, 13), K(0.36f, 9.2f, 2, 2, -6, 'i'), K(0.5f, Pi4, 24, 2, 18, 'o'), K(0.64f, Pi4, 24, 2, 18), K(1f, Pi4 + R, 6, 13) };
        static readonly Key[] Lash = { K(0f, R, 6, 13), K(0.2f, 3.6f, 0, 15, -4), K(0.45f, 0f, 26, 4, 20, 'o'), K(0.7f, 0f, 24, 4, 16), K(1f, R, 6, 13) };

        public static readonly Step[] Steps =
        {
            new Step { Name = "Snap", Dur = 0.30f, Next = 0.75f, R = Snap },
            new Step { Name = "Backhand", Dur = 0.32f, Next = 0.75f, L = Mirror(Backhand) },
            new Step { Name = "Double Figure-8", Dur = 0.50f, Next = 0.85f, R = Fig8, L = Mirror(Fig8) },
            new Step { Name = "Twin Crush", Dur = 0.75f, Next = 1f, R = Crush, L = Mirror(Crush) },
        };

        /// The Twin Counter, Fire from the guard.
        public static readonly Step Counter = new Step { Name = "Twin Counter", Dur = 0.55f, Next = 1f, R = Lash, L = Mirror(Lash) };

        /// The step a code (1-4 the combo, 5 the counter) plays.
        public static Step StepOf(int code) => code == 5 ? Counter : code >= 1 && code <= 4 ? Steps[code - 1] : null;

        /// Both chucks tucked back along the forearms, breathing with `t`.
        public static Pose Ready(float t)
        {
            float b = 0.08f * (float)Math.Sin(t * 3f);
            return new Pose { R = new Hand { X = 6f * S, Y = 13f * S, Th = R + b }, L = new Hand { X = 6f * S, Y = -13f * S, Th = -R - b } };
        }

        /// The guard `g` seconds after it went up: a disc spinning at each side in front, the right one one way and the left the other.
        public static Pose Guard(float g)
        {
            float a = g < GuardSpinUp ? GuardSpin * g * g / (2f * GuardSpinUp) : GuardSpin * (g - GuardSpinUp / 2f);  // (it spins up over GuardSpinUp)
            return new Pose { R = new Hand { X = GuardX, Y = GuardY, Th = R - a }, L = new Hand { X = GuardX, Y = -GuardY, Th = -R + a } };
        }

        static float Ease(byte e, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            if (e == 1) { float m = 1f - t; return 1f - m * m * m; }
            if (e == 2) return t;
            if (e == 3) return t * t * t;
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        static Hand Track(Key[] k, float u)
        {
            int i = 0;
            while (i < k.Length - 2 && u > k[i + 1].T)
                i++;
            Key a = k[i], b = k[i + 1];
            float t = Ease(b.E, (u - a.T) / Math.Max(0.0001f, b.T - a.T));
            return new Hand { X = a.X + (b.X - a.X) * t, Y = a.Y + (b.Y - a.Y) * t, Th = a.Th + (b.Th - a.Th) * t };
        }

        /// The shortest turn from a to b.
        public static float Wrap(float a) => (float)Math.IEEERemainder(a, MathX.Tau);

        static Hand Mix(Hand a, Hand b, float k) => new Hand { X = a.X + (b.X - a.X) * k, Y = a.Y + (b.Y - a.Y) * k, Th = a.Th + Wrap(b.Th - a.Th) * k };

        public static Pose Mix(Pose a, Pose b, float k) => new Pose { R = Mix(a.R, b.R, k), L = Mix(a.L, b.L, k) };

        /// The pose `c` seconds into a step (`t` breathes the resting hand); after Dur it is back at Ready.
        public static Pose At(Step s, float c, float t)
        {
            var p = Ready(t);
            if (c < 0f || c >= s.Dur)
                return p;
            float u = c / s.Dur;
            if (s.R != null) p.R = Track(s.R, u);
            if (s.L != null) p.L = Track(s.L, u);
            return p;
        }

        /// The swinging stick's angle: it trails the handle (`th` now, `thLag` Lag seconds ago), a little past where the handle pointed.
        public static float FreeAngle(float th, float thLag) => th - Wrap(th - thLag) * Whip;
    }
}
