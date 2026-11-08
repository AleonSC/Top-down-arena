namespace Arena
{
    /// The Lightbringer's chain as motion (Docs/Mockups/lightbringer-rework.html; design in Docs/LightbringerRework.md): Dawn Cut,
    /// Mercy Cut, Noon Sunburst and the Sunlance as key poses, timed from the click. The host times each hit from these numbers
    /// (Lightbringer.cs) and each screen draws the pose from them (Game/LightbringerLook.cs), so the sword is where the hits say.
    /// The pose is TwinBladesMotion's: R is the sword hand, L the off hand (open, it gathers the light for the Sunlance).
    ///   1 Dawn Cut       a rising cut, right to left                3 Noon Sunburst  raised in both hands, planted point-first
    ///   2 Mercy Cut      the backhand return cut                    Sunlance (alt)   light gathered in the off hand, then a thrust
    public static class LightbringerMotion
    {
        public const float BladeLen = 54f;
        public const float CutHit = 0.52f, NoonHit = 0.56f, LanceHit = 0.58f;  // the share of each move's time when it lands

        public static readonly TwinBladesMotion.Pose Ready = TwinBladesMotion.P(0.7f, 18f, 0.45f, -0.95f, 15f, -0.4f);

        static TwinBladesMotion.Pose P(float ra, float rr, float rb, float la, float lr, float lb, float tw = 0f) => TwinBladesMotion.P(ra, rr, rb, la, lr, lb, tw);

        public static readonly TwinBladesMotion.Step[] Steps =
        {
            new TwinBladesMotion.Step { Name = "Dawn Cut", Dur = 0.4f, Next = 0.85f, Fwd = 10f, HotR0 = 0.36f, HotR1 = 0.66f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.3f, P(1.5f, 16f, 2.3f, -1.1f, 13f, -0.4f, 0.38f)), TwinBladesMotion.K(0.64f, P(-1.05f, 24f, -1.55f, -0.8f, 15f, -0.4f, -0.32f)),
                TwinBladesMotion.K(1f, P(-0.85f, 21f, -1.15f, -0.85f, 15f, -0.4f, -0.24f), 1) } },
            new TwinBladesMotion.Step { Name = "Mercy Cut", Dur = 0.4f, Next = 0.85f, Fwd = 10f, HotR0 = 0.36f, HotR1 = 0.66f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.3f, P(-1.55f, 16f, -2.35f, -0.7f, 14f, -0.2f, -0.38f)), TwinBladesMotion.K(0.64f, P(1.1f, 24f, 1.6f, -0.95f, 15f, -0.4f, 0.32f)),
                TwinBladesMotion.K(1f, P(0.9f, 21f, 1.2f, -0.95f, 15f, -0.4f, 0.24f), 1) } },
            new TwinBladesMotion.Step { Name = "Noon Sunburst", Dur = 0.62f, Next = 1f, Fwd = 14f, HotR0 = 0.32f, HotR1 = 0.58f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
                TwinBladesMotion.K(0.38f, P(0.35f, 8f, 2.9f, -0.35f, 8f, 2.9f), 1), TwinBladesMotion.K(0.56f, P(0.12f, 24f, 0.03f, -0.12f, 22f, 0.03f), 3),
                TwinBladesMotion.K(1f, P(0.15f, 23f, 0.06f, -0.15f, 21f, 0.06f), 1) } },
        };

        /// The Sunlance: light gathers in the off hand while the sword draws back, then the thrust that throws the lance.
        public static readonly TwinBladesMotion.Step Lance = new TwinBladesMotion.Step { Name = "Sunlance", Dur = 0.62f, Next = 1f, HotR0 = 0.5f, HotR1 = 0.62f, HotL0 = 2f, HotL1 = 2f, Keys = new[] {
            TwinBladesMotion.K(0.1f, P(1.6f, 13f, 2.7f, -0.2f, 20f, -0.1f, 0.25f)), TwinBladesMotion.K(0.45f, P(1.9f, 11f, 0.15f, -0.05f, 24f, -0.05f, 0.35f)),
            TwinBladesMotion.K(0.6f, P(0.08f, 36f, 0.02f, -0.6f, 16f, -0.3f, -0.1f), 1), TwinBladesMotion.K(1f, P(0.15f, 30f, 0.05f, -0.85f, 15f, -0.4f), 1) } };

        public const float Recover = TwinBladesMotion.Recover;
        public const float NoonLightTail = 1.05f, LanceLightTail = 0.7f;  // how long Noon's ring and the Sunlance's light play after the blow / the release (their looks)
        public static float LanceRelease => Lance.Dur * LanceHit;

        static LightbringerMotion()
        {
            var prev = Ready;
            foreach (var s in Steps)
            {
                s.Entry = prev;
                s.Exit = TwinBladesMotion.Wrap(s.Keys[s.Keys.Length - 1].P);
                s.HasHome = true;
                s.Home = Ready;
                prev = s.Exit;
            }
            Steps[0].Entry = Ready;
            Lance.Entry = Ready;
            Lance.Exit = TwinBladesMotion.Wrap(Lance.Keys[Lance.Keys.Length - 1].P);
            Lance.HasHome = true;
            Lance.Home = Ready;
        }

        /// The pose `c` seconds into a move, then back to the guard.
        public static TwinBladesMotion.Pose At(TwinBladesMotion.Step s, float c) => TwinBladesMotion.At(s, c);
    }
}
