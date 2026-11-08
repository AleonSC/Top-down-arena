using System;

namespace Arena
{
    /// The Lance's Breach chain as motion (Docs/Mockups/lance-rework.html, moveset Breach, alt Hook & Haul, the Winged War Lance):
    /// four steps and the hook as key poses, timed from the click. The host times each hit from these numbers (Lance.cs) and each
    /// screen draws the pose from them (Game/LanceLook.cs), so the lance is where the hits say.
    ///   1 Jab                a quick thrust from the hip         3 Wide Sweep        the shaft swung flat across 150 degrees
    ///   2 Second Jab         the same, a touch wider             4 Breaching Thrust  a wind-up, a step in, the big shove
    ///   Hook & Haul (alt)    the lance shoots out to an enemy, hooks it and hauls it back to the tip
    /// The pose is TwinBladesMotion's: R is the lance hand (Rb the lance's angle off the aim), L the shield arm (Lb the way the
    /// shield faces). Every move starts and ends at the guard (the mockup's), lance couched along your right side.
    public static class LanceMotion
    {
        /// The Winged War Lance: the tip is Len past the glove, the butt spike Butt behind it.
        public const float Len = 130f, Butt = 30f;
        public const float JabHit = 0.48f, BreachHit = 0.48f, SweepFrom = 0.22f, SweepTo = 0.62f, HookHit = 0.34f;  // the share of each move's time when it lands
        public const float Recover = TwinBladesMotion.Recover;

        public static readonly TwinBladesMotion.Pose Ready = P(0.62f, 17f, -0.04f, -0.95f, 15f, -0.35f);

        static TwinBladesMotion.Pose P(float ra, float rr, float rb, float la = -0.95f, float lr = 15f, float lb = -0.35f, float tw = 0f)
            => TwinBladesMotion.P(ra, rr, rb, la, lr, lb, tw);
        static TwinBladesMotion.Pose Jab(float ra, float rr, float lance, float tw) => P(ra, rr, lance, -0.95f, 15f, -0.35f, tw);

        /// Where each step moves you: from MoveFrom to MoveTo of its time, Fwd px along the aim (your own game moves you).
        public static readonly float[] MoveFrom = { 0.3f, 0.3f, 0.2f, 0.36f }, MoveTo = { 0.55f, 0.55f, 0.6f, 0.52f };

        public static readonly TwinBladesMotion.Step[] Steps =
        {
            new TwinBladesMotion.Step { Name = "Jab", Dur = 0.3f, Next = 0.8f, Fwd = 10f, HotR0 = 0.36f, HotR1 = 0.6f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.3f, Jab(0.95f, 10f, -0.03f, 0.18f)), TwinBladesMotion.K(0.52f, Jab(0.2f, 29f, -0.03f, -0.14f)), TwinBladesMotion.K(1f, Ready) } },
            new TwinBladesMotion.Step { Name = "Second Jab", Dur = 0.3f, Next = 0.8f, Fwd = 10f, HotR0 = 0.36f, HotR1 = 0.6f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.3f, Jab(0.95f, 10f, 0.05f, 0.18f)), TwinBladesMotion.K(0.52f, Jab(0.2f, 29f, 0.05f, -0.14f)), TwinBladesMotion.K(1f, Ready) } },
            new TwinBladesMotion.Step { Name = "Wide Sweep", Dur = 0.38f, Next = 0.8f, Fwd = 12f, HotR0 = 0.22f, HotR1 = 0.66f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.22f, P(1.35f, 15f, 1.25f, -1.2f, 15f, -0.35f, 0.35f)), TwinBladesMotion.K(0.62f, P(-0.5f, 21f, -1.3f, -1.3f, 15f, -0.35f, -0.4f)),
                TwinBladesMotion.K(1f, Ready) } },
            new TwinBladesMotion.Step { Name = "Breaching Thrust", Dur = 0.62f, Next = 0.9f, Fwd = 34f, HotR0 = 0.4f, HotR1 = 0.56f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.35f, P(1.5f, 8f, 0.05f, -0.6f, 18f, -0.35f, 0.55f)), TwinBladesMotion.K(0.5f, P(0.05f, 31f, 0f, -1.3f, 15f, -0.35f, -0.25f)),
                TwinBladesMotion.K(0.72f, P(0.05f, 30f, 0f, -1.3f, 15f, -0.35f, -0.2f)), TwinBladesMotion.K(1f, Ready) } },
        };

        /// Hook & Haul: the lance shoots out through the hand to the enemy (HookSlide), hooks it, and is hauled back.
        public static readonly TwinBladesMotion.Step Hook = new TwinBladesMotion.Step { Name = "Hook & Haul", Dur = 0.55f, Next = 0.92f, HotR0 = 2f, HotR1 = 2f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
            TwinBladesMotion.K(0.3f, Jab(0.15f, 27f, 0f, -0.15f)), TwinBladesMotion.K(0.38f, Jab(0.15f, 27f, 0f, -0.15f)), TwinBladesMotion.K(0.62f, Jab(1.1f, 11f, 0.02f, 0.3f)),
            TwinBladesMotion.K(1f, Ready) } };

        static LanceMotion()
        {
            foreach (var s in Steps)
                Home(s);
            Home(Hook);
        }

        static void Home(TwinBladesMotion.Step s)
        {
            s.Entry = Ready;  // every move starts from the guard
            s.Exit = TwinBladesMotion.Wrap(s.Keys[s.Keys.Length - 1].P);
            s.HasHome = true;
            s.Home = Ready;
        }

        /// The pose `c` seconds into a move, then back to the guard.
        public static TwinBladesMotion.Pose At(TwinBladesMotion.Step s, float c) => TwinBladesMotion.At(s, c);

        static float C01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// How far the lance slides out through the hand during the hook, `c` seconds in, for an enemy `dist` px away: out to it
        /// by HookHit, then back in as it is hauled.
        public static float HookSlide(float c, float dist)
        {
            float u = c / Hook.Dur, s = Math.Max(0f, Math.Min(110f, dist - 150f));
            if (u < 0.34f)
            {
                float k = C01((u - 0.08f) / 0.26f), m = 1f - k;
                return s * (1f - m * m * m);
            }
            float q = C01((u - 0.36f) / 0.2f);
            return s * (1f - (q < 0.5f ? 2f * q * q : 1f - (float)Math.Pow(-2f * q + 2f, 2) / 2f));
        }
    }
}
