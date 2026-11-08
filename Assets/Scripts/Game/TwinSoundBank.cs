namespace Arena
{
    /// The Twin Blades' sounds (cues raised in Core/TwinBlades.cs and Core/Knight.cs), the Crescent Waltz's voice: two light sabres,
    /// so quick airy cuts with a high, clean "shing" ring, lighter and brighter than the Greatsword's; the Shear and Twin Fang's burst
    /// are the heavy ones. Everything but the catch uses the Weapons volume (IsTwinWeaponSfx).
    public partial class SoundBank
    {
        const float TwinRing = 3100f, TwinRingVol = 0.06f, TwinRoom = 0.2f;

        static bool IsTwinWeaponSfx(Sfx s)
        {
            return s == Sfx.TwinCut1 || s == Sfx.TwinCut2 || s == Sfx.TwinJab || s == Sfx.TwinSpin || s == Sfx.TwinShear
                || s == Sfx.TwinHit || s == Sfx.TwinHitHeavy || s == Sfx.TwinThrow || s == Sfx.TwinCross;
        }

        void BuildTwin()
        {
            const Osc S = Osc.Sine;
            // Rising Cut: a quick rush sweeping up, a ring that rises with it. Falling Cut sweeps down and rings a touch lower.
            Set(Sfx.TwinCut1, Finish(Mix(
                Tone(0.14f, S, 380f, 1100f, 0.15f, noise: 0.85f, lowpass: 0.6f, attack: 0.015f, decay: 1.8f),
                Tone(0.2f, S, TwinRing * 0.95f, TwinRing * 1.04f, TwinRingVol, lowpass: 0.65f, decay: 4.5f)), 0.42f), 0.04f);
            Set(Sfx.TwinCut2, Finish(Mix(
                Tone(0.14f, S, 1100f, 380f, 0.15f, noise: 0.85f, lowpass: 0.6f, attack: 0.015f, decay: 1.8f),
                Tone(0.2f, S, TwinRing * 0.9f, TwinRing * 0.84f, TwinRingVol, lowpass: 0.65f, decay: 4.5f)), 0.42f), 0.04f);
            // A jab: a short dry hiss with a tick at the point.
            Set(Sfx.TwinJab, Finish(Mix(
                Tone(0.08f, S, 900f, 1600f, 0.13f, noise: 0.9f, lowpass: 0.7f, attack: 0.005f, decay: 2.5f),
                Tone(0.02f, Osc.Square, 2600f, 2200f, 0.05f, lowpass: 0.6f, decay: 3f)), 0.36f), 0.03f);
            // The Pirouette: a whirl that swells and falls away (0.4 s), two rings beating as the blades go round.
            Set(Sfx.TwinSpin, Finish(Mix(
                Tone(0.4f, S, 500f, 900f, 0.13f, noise: 0.85f, lowpass: 0.5f, attack: 0.12f, decay: 1.2f),
                Tone(0.38f, S, TwinRing, TwinRing * 0.97f, TwinRingVol * 0.8f, lowpass: 0.6f, attack: 0.08f, decay: 2f),
                Tone(0.38f, S, TwinRing * 1.02f, TwinRing * 0.99f, TwinRingVol * 0.8f, lowpass: 0.6f, attack: 0.08f, decay: 2f)), 0.45f), 0.1f);
            // The Crescent Shear: the blades drawn up behind (a rising breath), a wide tearing sweep, a bright double ring as they cross.
            Set(Sfx.TwinShear, Finish(Room(Mix(
                Reverse(Tone(0.2f, S, 300f, 800f, 0.08f, noise: 0.7f, lowpass: 0.4f, decay: 1f)),
                Delay(Tone(0.26f, S, 2400f, 420f, 0.26f, noise: 0.85f, lowpass: 0.85f, attack: 0.003f, decay: 1.8f), 0.2f),
                Delay(Tone(0.18f, S, 140f, 60f, 0.24f, lowpass: 0.25f, decay: 2.2f), 0.22f),
                Delay(Bell(0.5f, TwinRing, 0.07f), 0.24f),
                Delay(Bell(0.45f, TwinRing * 1.26f, 0.05f), 0.26f)), TwinRoom), 0.62f), 0.15f);
            // A cut lands: a slice and a light thud. The heavy one (the Shear, Twin Fang's burst) has a deeper body and a ring.
            Set(Sfx.TwinHit, Finish(Mix(
                Tone(0.06f, S, 2200f, 800f, 0.18f, noise: 0.9f, lowpass: 0.85f, decay: 3f),
                Tone(0.1f, S, 260f, 120f, 0.2f, noise: 0.3f, lowpass: 0.3f, decay: 3f)), 0.36f), 0.035f);
            Set(Sfx.TwinHitHeavy, Finish(Room(Mix(
                Tone(0.08f, S, 2000f, 600f, 0.22f, noise: 0.9f, lowpass: 0.85f, decay: 3f),
                Tone(0.2f, S, 180f, 60f, 0.32f, noise: 0.25f, lowpass: 0.25f, decay: 2.2f),
                Bell(0.4f, TwinRing * 0.8f, 0.06f)), TwinRoom * 0.6f), 0.55f), 0.06f);
            // Twin Fang thrown: a hard flick, then a spinning whirr that fades as the sabres fly off.
            var throwLayers = new float[9][];
            throwLayers[0] = Tone(0.07f, S, 600f, 1800f, 0.18f, noise: 0.85f, lowpass: 0.7f, attack: 0.004f, decay: 2.5f);
            for (int k = 1; k < throwLayers.Length; k++)  // one whoosh per turn of the blades, fading as they fly off
                throwLayers[k] = Delay(Tone(0.05f, S, 700f, 450f, 0.1f * (1f - k / 10f), noise: 0.85f, lowpass: 0.45f, attack: 0.01f, decay: 2f), 0.04f + 0.055f * k);
            Set(Sfx.TwinThrow, Finish(Mix(throwLayers), 0.45f), 0.15f);
            // Where they cross: a bright clash, a burst of air, a ring that hangs.
            Set(Sfx.TwinCross, Finish(Room(Mix(
                Tone(0.05f, S, 3400f, 1800f, 0.24f, noise: 1f, lowpass: 0.95f, decay: 3f),
                Tone(0.22f, S, 900f, 200f, 0.18f, noise: 0.8f, lowpass: 0.5f, decay: 2f),
                Bell(0.6f, TwinRing * 1.1f, 0.08f),
                Bell(0.5f, TwinRing * 1.4f, 0.04f)), TwinRoom), 0.55f), 0.1f);
            // Caught: two quick clicks of the hilts landing in the gloves.
            Set(Sfx.TwinCatch, Finish(Mix(
                Tone(0.03f, Osc.Square, 1500f, 1200f, 0.08f, lowpass: 0.45f, decay: 3f),
                Delay(Tone(0.03f, Osc.Square, 1700f, 1350f, 0.08f, lowpass: 0.45f, decay: 3f), 0.05f),
                Delay(Bell(0.25f, TwinRing * 0.7f, 0.03f), 0.05f)), 0.3f), 0.1f);
        }
    }
}
