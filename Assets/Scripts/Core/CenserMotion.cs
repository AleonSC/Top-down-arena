using System;

namespace Arena
{
    /// The Censer's Thurible Rite as motion (Docs/Mockups/censer-rework.html): the three chain steps and Censer Cast as key
    /// poses for the censer's head, timed from the click. The host times each step's hit and heal from these numbers
    /// (Censer.cs) and each screen draws the pose from them (Game/CenserLook.cs), so the head is where the hits say.
    ///   1 Sweep          200 degrees across the front, right to left
    ///   2 Return Sweep   the same back, left to right
    ///   3 Benediction    the chain let out, the head and the body all the way round
    ///   Censer Cast (alt) the head thrown out to the cursor, a burst where it lands, reeled back in
    /// A pose is the head's angle off the aim (positive turns clockwise on screen) and its distance from the body's centre in px
    /// at the base reach (ReachBase: screens and the host scale it to the swing's real reach), the shoulders' twist, the body's
    /// turn (the Benediction), and Bless: the left hand lifted off the haft in a blessing (0 to 1).
    public static class CenserMotion
    {
        public struct Pose { public float Ha, Hr, Tw, Sp, Bless; }

        internal struct Key { public float T; public Pose P; public byte E; }  // E: 0 ease in-out, 1 ease out, 2 linear

        public class Step
        {
            public string Name;
            public float Dur;          // seconds the move plays
            public float Next;         // the share of Dur after which the next press counts (1: the chain ends here)
            public float Hot0, Hot1;   // the shares of Dur when the head can hit (its trail is drawn)
            public float Land;         // the share of Dur when the host's swing lands: all its hits, the heal and the incense at once
            internal Key[] Keys;
            internal Pose Entry, Exit;
        }

        public const float ReachBase = 85f, Recover = 0.3f;
        public const float CastTotal = 0.9f, CastRelease = 0.2f, CastLand = 0.38f, CastReel = 0.5f;  // the throw: wind-up, out, lands, sits, reeled back by CastTotal

        public static readonly Pose Ready = P(1.05f, 30f);

        static Pose P(float ha, float hr, float tw = 0f, float sp = 0f, float bless = 0f) => new Pose { Ha = ha, Hr = hr, Tw = tw, Sp = sp, Bless = bless };
        static Key K(float t, Pose p, byte e = 0) => new Key { T = t, P = p, E = e };

        public static readonly Step[] Steps =
        {
            new Step { Name = "Sweep", Dur = 0.36f, Next = 0.72f, Hot0 = 0.3f, Hot1 = 0.7f, Land = 0.5f, Keys = new[] {
                K(0.28f, P(2.05f, 50f, 0.38f)), K(0.68f, P(-1.7f, 88f, -0.32f), 1), K(1f, P(-1.95f, 64f, -0.25f), 1) } },
            new Step { Name = "Return Sweep", Dur = 0.36f, Next = 0.72f, Hot0 = 0.3f, Hot1 = 0.7f, Land = 0.5f, Keys = new[] {
                K(0.28f, P(-2.05f, 50f, -0.38f)), K(0.68f, P(1.7f, 88f, 0.32f), 1), K(1f, P(1.95f, 64f, 0.25f), 1) } },
            new Step { Name = "Benediction", Dur = 0.74f, Next = 1f, Hot0 = 0.22f, Hot1 = 0.82f, Land = 0.6f, Keys = new[] {
                K(0.2f, P(2.5f, 44f, 0.55f, 0f, 0.5f)), K(0.82f, P(2.5f + MathX.Tau, 118f, 0.2f, MathX.Tau, 0.5f), 2), K(1f, P(2.8f + MathX.Tau, 66f, 0f, MathX.Tau, 0f), 1) } },
        };

        /// Censer Cast's body: the wind-up behind, the throw, the arms following through while the head is out.
        public static readonly Step Cast = new Step { Name = "Censer Cast", Dur = CastTotal, Next = 1f, Hot0 = 2f, Hot1 = 2f, Keys = new[] {
            K(0.22f, P(2.75f, 40f, 0.55f)), K(0.36f, P(-0.15f, 20f, -0.25f), 1), K(1f, P(0.7f, 26f, 0f), 1) } };

        static CenserMotion()
        {
            Pose prev = Ready;
            foreach (var s in Steps)
            {
                s.Entry = prev;
                s.Exit = Wrap(s.Keys[s.Keys.Length - 1].P);
                prev = s.Exit;
            }
            Steps[0].Entry = Ready;
            Cast.Entry = Ready;
            Cast.Exit = Cast.Keys[Cast.Keys.Length - 1].P;
        }

        static Pose Wrap(Pose p)
        {
            p.Sp = (float)Math.IEEERemainder(p.Sp, MathX.Tau);
            p.Ha = (float)Math.IEEERemainder(p.Ha, MathX.Tau);
            return p;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float C01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Ease(byte e, float t)
        {
            t = C01(t);
            if (e == 1) { float m = 1f - t; return 1f - m * m * m; }
            if (e == 2) return t;
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public static Pose Mix(Pose a, Pose b, float t) => new Pose
        {
            Ha = Lerp(a.Ha, b.Ha, t), Hr = Lerp(a.Hr, b.Hr, t), Tw = Lerp(a.Tw, b.Tw, t), Sp = Lerp(a.Sp, b.Sp, t), Bless = Lerp(a.Bless, b.Bless, t)
        };

        /// The pose `c` seconds into a move: its keys, then back to the ready over Recover once it ends.
        public static Pose At(Step s, float c)
        {
            if (c >= s.Dur)
                return Mix(s.Exit, Ready, Ease(1, (c - s.Dur) / Recover));
            float k = C01(c / s.Dur), t0 = 0f;
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

        /// Where the head is for a pose, around `o` facing `aim`, for a swing reaching `reach` px.
        public static V2 Head(V2 o, float aim, Pose m, float reach) => o + V2.FromAngle(aim + m.Ha) * (m.Hr * reach / ReachBase);

        /// Where the hands grip the haft: both together, a little toward the head (the Halberd's two-handed hold).
        public static V2 Grip(V2 o, float aim, Pose m)
        {
            float rel = (float)Math.IEEERemainder(m.Ha - m.Sp, MathX.Tau);
            rel = rel < -1.9f ? -1.9f : rel > 1.9f ? 1.9f : rel;
            float a = aim + m.Sp + m.Tw * 0.4f + rel * 0.55f + 0.35f, r = 16f + C01((m.Hr - 40f) / 80f) * 6f;
            return o + V2.FromAngle(a) * r;
        }

        /// The chain step (1 to 3) out of a player's replicated Combo byte (the high bits count swings).
        public static int StepOf(int combo) => combo & 7;

        /// Censer Cast's head `c` seconds in, thrown from `from` (where the head was) to `to`, and reeled back to `back`; z is its
        /// height above the floor while it flies (0 to 1).
        public static V2 CastHead(V2 from, V2 to, V2 back, float c, out float z)
        {
            z = 0f;
            if (c < CastRelease)
                return from;
            if (c < CastLand)
            {
                float q = Ease(1, (c - CastRelease) / (CastLand - CastRelease));
                z = (float)Math.Sin(Math.PI * q);
                return from + (to - from) * q;
            }
            if (c < CastReel)
                return to;
            float r = Ease(0, (c - CastReel) / (CastTotal - CastReel));
            return to + (back - to) * r;
        }
    }
}
