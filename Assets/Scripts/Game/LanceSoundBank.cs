namespace Arena
{
    /// The Lance's sounds (cues raised in Core/Lance.cs and Core/Knight.cs), the Breach's voice: a long ash shaft and a steel head,
    /// so heavier and woodier than the sabres: a short whoosh and a thump for the jabs, a wide swoosh with the pennant fluttering
    /// for the sweep, a breath drawn in and a driving thrust for the Breaching Thrust, dull steel-on-body hits, a crunch when a
    /// shoved enemy hits another (Impact) and stone cracking at a wall, the hook rattling out and catching. The swings and hits
    /// use the Weapons volume (IsLanceWeaponSfx).
    public partial class SoundBank
    {
        const float LanceRing = 1500f, LanceRoom = 0.18f;

        static bool IsLanceWeaponSfx(Sfx s)
        {
            return s == Sfx.LanceJab || s == Sfx.LanceSweep || s == Sfx.LanceBreach || s == Sfx.LanceHitSfx || s == Sfx.LanceHitHeavy || s == Sfx.LanceHookOut;
        }

        void BuildLance()
        {
            const Osc S = Osc.Sine;
            // A jab: a short whoosh of the shaft going out, a low thump behind it, a small tick of steel at the point.
            Set(Sfx.LanceJab, Finish(Mix(
                Tone(0.12f, S, 300f, 900f, 0.17f, noise: 0.85f, lowpass: 0.5f, attack: 0.01f, decay: 2.2f),
                Tone(0.07f, S, 160f, 90f, 0.12f, lowpass: 0.3f, decay: 3f),
                Delay(Tone(0.02f, Osc.Square, 2000f, 1700f, 0.04f, lowpass: 0.6f, decay: 3f), 0.05f)), 0.4f), 0.04f);
            // The Wide Sweep: a wide, low swoosh, the pennant fluttering through it.
            Set(Sfx.LanceSweep, Finish(Mix(
                Tone(0.3f, S, 260f, 520f, 0.17f, noise: 0.9f, lowpass: 0.45f, attack: 0.06f, decay: 1.4f),
                Tremolo(Tone(0.28f, S, 700f, 500f, 0.07f, noise: 1f, lowpass: 0.5f, attack: 0.04f, decay: 1.6f), 28f, 0.8f)), 0.45f), 0.08f);
            // The Breaching Thrust, from the click: a breath drawn in through the wind-up (0.28 s), then the thrust's drive and a
            // heavy thump as it lands.
            Set(Sfx.LanceBreach, Finish(Room(Mix(
                Reverse(Tone(0.26f, S, 200f, 600f, 0.08f, noise: 0.7f, lowpass: 0.35f, decay: 1f)),
                Delay(Tone(0.16f, S, 400f, 1400f, 0.24f, noise: 0.85f, lowpass: 0.7f, attack: 0.004f, decay: 2f), 0.24f),
                Delay(Tone(0.24f, S, 110f, 50f, 0.28f, lowpass: 0.25f, decay: 2.2f), 0.28f),
                Delay(Bell(0.35f, LanceRing * 0.8f, 0.03f), 0.29f)), LanceRoom), 0.6f), 0.15f);
            // A hit lands: a dull clank of steel on a body. The Breaching Thrust's is deeper, with a ring.
            Set(Sfx.LanceHitSfx, Finish(Mix(
                Tone(0.08f, S, 1500f, 500f, 0.14f, noise: 0.8f, lowpass: 0.7f, decay: 3f),
                Tone(0.12f, S, 200f, 90f, 0.24f, noise: 0.3f, lowpass: 0.3f, decay: 3f)), 0.38f), 0.035f);
            Set(Sfx.LanceHitHeavy, Finish(Room(Mix(
                Tone(0.1f, S, 1300f, 400f, 0.2f, noise: 0.85f, lowpass: 0.7f, decay: 3f),
                Tone(0.24f, S, 150f, 55f, 0.34f, noise: 0.25f, lowpass: 0.25f, decay: 2.2f),
                Bell(0.35f, LanceRing, 0.05f)), LanceRoom * 0.6f), 0.58f), 0.06f);
            // Impact: two bodies colliding, a crunch with a low knock.
            Set(Sfx.LanceImpactSfx, Finish(Mix(
                Tone(0.11f, S, 120f, 60f, 0.3f, noise: 0.5f, lowpass: 0.3f, decay: 2.5f),
                Tone(0.05f, S, 900f, 300f, 0.15f, noise: 1f, lowpass: 0.6f, decay: 3f),
                Delay(Tone(0.04f, S, 700f, 250f, 0.08f, noise: 1f, lowpass: 0.5f, decay: 3f), 0.03f)), 0.5f), 0.05f);
            // Into a wall: stone cracking, a deep thud, grit falling after.
            Set(Sfx.LanceWall, Finish(Room(Mix(
                Tone(0.22f, S, 90f, 40f, 0.32f, noise: 0.4f, lowpass: 0.25f, decay: 2f),
                Tone(0.12f, S, 2500f, 800f, 0.14f, noise: 1f, lowpass: 0.9f, decay: 3f),
                Delay(Tone(0.25f, S, 1200f, 600f, 0.05f, noise: 1f, lowpass: 0.5f, attack: 0.02f, decay: 2.5f), 0.08f)), LanceRoom), 0.55f), 0.06f);
            // Hook & Haul thrown: the shaft shooting out through the glove, the lugs rattling.
            var outLayers = new float[5][];
            outLayers[0] = Tone(0.14f, S, 500f, 1600f, 0.14f, noise: 0.85f, lowpass: 0.6f, attack: 0.005f, decay: 2.2f);
            for (int k = 1; k < outLayers.Length; k++)
                outLayers[k] = Delay(Tone(0.02f, Osc.Square, 1800f + 150f * k, 1500f, 0.035f, lowpass: 0.5f, decay: 3f), 0.03f + 0.035f * k);
            Set(Sfx.LanceHookOut, Finish(Mix(outLayers), 0.42f), 0.1f);
            // It catches: a clank as the hook bites, then the drag back in.
            Set(Sfx.LanceHaul, Finish(Mix(
                Tone(0.03f, Osc.Square, 1400f, 1100f, 0.08f, lowpass: 0.45f, decay: 3f),
                Bell(0.3f, LanceRing * 1.15f, 0.05f),
                Delay(Tone(0.2f, S, 900f, 300f, 0.13f, noise: 0.9f, lowpass: 0.5f, attack: 0.02f, decay: 1.8f), 0.03f)), 0.42f), 0.1f);
        }
    }
}
