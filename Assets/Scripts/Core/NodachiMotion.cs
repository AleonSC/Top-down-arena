using System;

namespace Arena
{
    /// The Nodachi's moves as motion (Docs/Nodachi.md; mockup https://claude.ai/artifact/7jmWYi6U9oafkebSdAuSnc): each step is a list of
    /// strikes timed from the press, and a pose of the one long blade at any moment. The host lands the hits from the strikes
    /// (Nodachi.cs) and every screen draws the blade from the pose (Game/NodachiLook.cs), so what you see is what lands.
    ///   Arashi (Storm, stance 0): Nukitsuke, Kesagiri, Engetsu, Tenchi (finisher), Nagi: long sweeps; every wind-up is armoured.
    ///   Mamori (Guard, stance 1): Kasumi-zuki, Nidan-zuki, Senbon-zuki (finisher), Tsuka-ate, Hiki-zuki: quick jabs; while one
    ///   plays, small shots in a cone in front are blocked.
    /// The steps are ordered so each one starts near where the last one ended (Kesagiri picks up on Nukitsuke's side, Engetsu winds
    /// from Kesagiri's end, Tenchi's spin carries on Engetsu's turn and its slam comes round to the front, Nagi pulls back from the
    /// slam), and every step eases from wherever the blade was into its wind-up and back into the stance's ready pose after.
    public static class NodachiMotion
    {
        public enum Kind : byte { Arc, Jab, Pommel, Slam, Hop, Walk }

        /// One strike. Angles are degrees off the aim (positive turns clockwise on screen, toward the right hand).
        public struct Seg
        {
            public Kind K;
            public float T0, T1;       // when it starts and ends, seconds after the press (a Slam lands at T0)
            public float A0, A1;       // an Arc's sweep; a Jab's lane is A0
            public float Reach, Half;  // how far (an Arc's radius, a Jab's length, a Slam's ring), a Jab's half width
            public float Mult;         // damage against the weapon's Damage
            public float Stun, Knock;  // seconds stunned (not bosses), px shoved
        }

        public class Step
        {
            public string Name;
            public float Dur, Next;    // seconds it plays; when the next press counts
            public bool Finisher;
            public Seg[] Segs;
            internal Key[] Keys;
            public float Windup => Segs[0].K == Kind.Hop || Segs[0].K == Kind.Walk ? Segs[1].T0 : Segs[0].T0;
            /// When its last strike is over (the Guard's cone drops, the recovery starts).
            public float StrikesEnd { get { float e = 0f; foreach (var s in Segs) e = Math.Max(e, s.K == Kind.Slam ? s.T0 : s.T1); return e; } }
        }

        /// The blade's pose: its angle off the aim (radians), the grip pushed out along it (px), the grip moved along the aim (px),
        /// how foreshortened it is (1 flat on the floor, less raised overhead), and how far its sweep reaches (the trail; 0 none).
        public struct Pose { public float Th, Ext, Fwd, Len, R; }

        internal struct Key { public float T; public Pose P; public byte E; }  // E: 0 smooth, 1 ease out, 2 ease in

        public const float Grip = 13f, Handle = 22f, Blade = 104f;  // px: the grip's distance from the body, the handle behind it, the blade past it
        public static float Tip => Grip + Blade;
        public const float Pull = 28f;  // degrees a sweep winds back past where it starts

        public static readonly Pose StormReady = new Pose { Th = D(150f), Len = 1f };      // wakigamae: low, trailing behind on the right
        public static readonly Pose GuardReady = new Pose { Th = D(14f), Ext = -4f, Fwd = 4f, Len = 1f };  // seigan: the point at their eyes
        public static Pose Ready(int stance) => stance == 0 ? StormReady : GuardReady;

        static float D(float deg) => deg * MathX.Pi / 180f;

        static Seg Arc(float t0, float t1, float a0, float a1, float reach, float mult) => new Seg { K = Kind.Arc, T0 = t0, T1 = t1, A0 = a0, A1 = a1, Reach = reach, Mult = mult };
        static Seg Jab(float t0, float t1, float ang, float len, float half, float mult) => new Seg { K = Kind.Jab, T0 = t0, T1 = t1, A0 = ang, Reach = len, Half = half, Mult = mult };

        public static readonly Step[] Storm =
        {
            new Step { Name = "Nukitsuke", Dur = 0.72f, Next = 0.52f, Segs = new[] { Arc(0.24f, 0.40f, -100f, 100f, 150f, 1f) } },
            new Step { Name = "Kesagiri", Dur = 0.72f, Next = 0.50f, Segs = new[] { Arc(0.26f, 0.40f, 75f, -75f, 165f, 1.25f) } },
            new Step { Name = "Engetsu", Dur = 1.05f, Next = 0.85f, Segs = new[] { Arc(0.40f, 0.74f, -180f, 180f, 210f, 1.5f) } },
            new Step { Name = "Tenchi", Dur = 1.40f, Next = 1.12f, Finisher = true, Segs = new[]
            {
                Arc(0.34f, 0.64f, 180f, 540f, 150f, 1.1f),
                new Seg { K = Kind.Slam, T0 = 0.82f, Reach = 190f, Mult = 2.9f, Stun = 0.8f, Knock = 40f },
            } },
            new Step { Name = "Nagi", Dur = 1.0f, Next = 0.84f, Segs = new[]
            {
                new Seg { K = Kind.Walk, T0 = 0.18f, T1 = 0.80f, Reach = 100f },
                Arc(0.24f, 0.46f, -150f, 150f, 185f, 0.9f),
                Arc(0.54f, 0.76f, 150f, -150f, 185f, 0.9f),
            } },
        };

        static readonly float[] SenbonLanes = { -14f, 8f, -4f, 16f, -18f, 4f, 0f };

        public static readonly Step[] Guard =
        {
            new Step { Name = "Kasumi-zuki", Dur = 0.38f, Next = 0.24f, Segs = new[] { Jab(0.08f, 0.20f, 0f, 175f, 13f, 0.6f) } },
            new Step { Name = "Nidan-zuki", Dur = 0.58f, Next = 0.42f, Segs = new[] { Jab(0.07f, 0.17f, 0f, 150f, 12f, 0.45f), Jab(0.25f, 0.36f, 0f, 190f, 12f, 0.45f) } },
            new Step { Name = "Senbon-zuki", Dur = 1.0f, Next = 0.84f, Finisher = true, Segs = Senbon() },
            new Step { Name = "Tsuka-ate", Dur = 0.46f, Next = 0.32f, Segs = new[]
            {
                new Seg { K = Kind.Pommel, T0 = 0.08f, T1 = 0.20f, Reach = 70f, Half = 28f, Mult = 0.75f, Stun = 0.5f, Knock = 50f },
            } },
            new Step { Name = "Hiki-zuki", Dur = 0.54f, Next = 0.38f, Segs = new[]
            {
                new Seg { K = Kind.Hop, T0 = 0f, T1 = 0.14f, Reach = -70f },
                Jab(0.18f, 0.30f, 0f, 190f, 13f, 0.85f),
            } },
        };

        static Seg[] Senbon()
        {
            var s = new Seg[SenbonLanes.Length];
            for (int i = 0; i < s.Length; i++)
                s[i] = Jab(0.14f + 0.075f * i, 0.14f + 0.075f * i + 0.09f, SenbonLanes[i], 185f, 9f, 0.35f);
            return s;
        }

        public const int Steps = 5;  // a combo's length, in both stances
        public static int Code(int stance, int idx) => stance * 8 + idx;
        public static int StanceOf(int code) => code >= 8 ? 1 : 0;

        /// The step a code (stance * 8 + index) plays, or null.
        public static Step StepOf(int code)
        {
            int i = code & 7;
            if (i >= Steps)
                return null;
            return code >= 8 && code < 16 ? Guard[i] : code < 8 ? Storm[i] : null;
        }

        /// When a jab or pommel strike is at full stretch: its hit lands then.
        public static float HitAt(Seg s) => s.K == Kind.Slam ? s.T0 : s.T0 + (s.T1 - s.T0) * 0.45f;

        /// When an Arc's edge passes `deg` (between A0 and A1): the sweep eases out from its wind-back, so the edge is fast early.
        public static float ArcTimeAt(Seg s, float deg)
        {
            float sg = Math.Sign(s.A1 - s.A0), from = s.A0 - sg * Pull;
            float u = (deg - from) / (s.A1 - from);
            u = u < 0f ? 0f : u > 1f ? 1f : u;
            float x = 1f - (float)Math.Pow(1f - u, 1f / 3f);  // the inverse of the ease out
            return s.T0 + x * (s.T1 - s.T0);
        }

        /// Whether the Guard's cone is up `c` seconds into a step (from the press until its last jab is back).
        public static bool Guarding(Step s, float c) => c >= 0f && c <= s.StrikesEnd + 0.05f;

        /// Whether the Storm's armour is up `c` seconds into a step (its wind-up, until the first edge moves).
        public static bool Armoured(Step s, float c) => c >= 0f && c < s.Windup;

        static NodachiMotion()
        {
            foreach (var s in Storm)
                s.Keys = Build(s);
            foreach (var s in Guard)
                s.Keys = Build(s);
        }

        static Key K(float t, float th, float ext, float fwd, float len, float r, byte e) => new Key { T = t, P = new Pose { Th = th, Ext = ext, Fwd = fwd, Len = len, R = r }, E = e };

        /// The step's key poses: each strike's wind-up, its sweep or stretch, the last one held a moment.
        static Key[] Build(Step s)
        {
            var k = new System.Collections.Generic.List<Key>();
            float lastTh = 0f;
            foreach (var g in s.Segs)
            {
                switch (g.K)
                {
                    case Kind.Arc:
                    {
                        float sg = Math.Sign(g.A1 - g.A0), ext = Math.Max(0f, Math.Min(22f, g.Reach - Tip));
                        k.Add(K(g.T0, D(g.A0 - sg * Pull), 0f, 0f, 1f, 0f, 0));
                        k.Add(K(g.T1, D(g.A1), ext, 0f, 1f, g.Reach, 1));
                        lastTh = D(g.A1);
                        break;
                    }
                    case Kind.Slam:  // raised overhead behind you, then brought round and down in front
                        k.Add(K(g.T0 - 0.1f, lastTh, -10f, -4f, 0.4f, 0f, 0));
                        k.Add(K(g.T0, lastTh + D(180f), 20f, 6f, 1f, 0f, 2));
                        k.Add(K(g.T0 + 0.12f, lastTh + D(180f), 14f, 4f, 1f, 0f, 1));
                        break;
                    case Kind.Jab:
                    {
                        float mid = HitAt(g);
                        k.Add(K(g.T0, D(g.A0), -24f, 0f, 1f, 0f, 0));
                        k.Add(K(mid, D(g.A0), 30f, 6f, 1f, 0f, 1));
                        k.Add(K(g.T1, D(g.A0), 10f, 2f, 1f, 0f, 0));
                        break;
                    }
                    case Kind.Pommel:  // the blade turned back, the butt of the handle driven out in front
                    {
                        float mid = HitAt(g);
                        k.Add(K(g.T0, D(180f), -Grip, -6f, 1f, 0f, 0));
                        k.Add(K(mid, D(180f), -Grip, 28f, 1f, 0f, 1));
                        k.Add(K(g.T1, D(180f), -Grip, 14f, 1f, 0f, 0));
                        break;
                    }
                }
            }
            var last = k[k.Count - 1];
            float hold = Math.Min(s.Dur, last.T + 0.08f);
            last.T = hold;
            last.P.R = 0f;
            last.E = 0;
            k.Add(last);
            return k.ToArray();
        }

        static float Ease(byte e, float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            if (e == 1) { float m = 1f - t; return 1f - m * m * m; }
            if (e == 2) return t * t * t;
            return t * t * (3f - 2f * t);
        }

        /// The shortest turn from a to b.
        public static float Wrap(float a) => (float)Math.IEEERemainder(a, MathX.Tau);

        /// Between two poses (`turn`: the angle the shortest way round; otherwise as given, so a spin keeps spinning).
        public static Pose Mix(Pose a, Pose b, float k, bool turn = true) => new Pose
        {
            Th = a.Th + (turn ? Wrap(b.Th - a.Th) : b.Th - a.Th) * k,
            Ext = a.Ext + (b.Ext - a.Ext) * k,
            Fwd = a.Fwd + (b.Fwd - a.Fwd) * k,
            Len = a.Len + (b.Len - a.Len) * k,
            R = a.R + (b.R - a.R) * k,
        };

        /// The pose `c` seconds into a step that started with the blade at `from`: eased from there into the first wind-up, through
        /// the strikes, held, then eased into the stance's ready pose by the step's end (and the ready pose after it).
        public static Pose At(Step s, int stance, float c, Pose from)
        {
            var ready = Ready(stance);
            var k = s.Keys;
            if (c <= 0f)
                return from;
            if (c < k[0].T)
                return Mix(from, k[0].P, Ease(0, c / k[0].T));
            var end = k[k.Length - 1];
            if (c >= end.T)
                return c >= s.Dur ? ready : Mix(end.P, ready, Ease(0, (c - end.T) / Math.Max(0.001f, s.Dur - end.T)));
            int i = 0;
            while (i < k.Length - 2 && c > k[i + 1].T)
                i++;
            Key a = k[i], b = k[i + 1];
            float u = Ease(b.E, (c - a.T) / Math.Max(0.0001f, b.T - a.T));
            var p = Mix(a.P, b.P, u, false);
            if (b.P.R > 0f)
                p.R = b.P.R;  // a sweep's trail reaches its full radius all the way through
            return p;
        }
    }
}
