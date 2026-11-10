namespace Arena
{
    /// The Katana's sounds (cues raised in Core/Katana.cs, Core/Ninja.cs and Core/Knight.cs), the Steel set of
    /// Docs/Mockups/katana-sounds.html: airy whooshes with a metallic ring on top, hard cracks, bell tails.
    /// Senbon's swipes, Hayate's dash-cut (KatanaLunge) and a cut landing use the Weapons volume (IsKatanaWeaponSfx); the rest are
    /// Effects. The two stances (Katana.cs) reuse the old strike's sounds: KatanaStance as a Kage Chain or Zanshin begins, a chain
    /// chime per hop, the sheathe and KatanaEmpStrikeCut as Zanshin's cuts fall.
    public partial class SoundBank
    {
        const float KatanaRing = 2600f, KatanaRingVol = 0.07f, KatanaRoom = 0.25f, KatanaChime = 0.2f;
        const float KatanaAfterBlow = 0.4f;  // the chain, empowered and lost cues are raised as the blow lands and start this much later
        static readonly float[] KatanaChainPitch = { 523.25f, 587.33f, 659.25f, 783.99f, 880f };

        static bool IsKatanaWeaponSfx(Sfx s)
        {
            return s == Sfx.KatanaSwipe1 || s == Sfx.KatanaSwipe2 || s == Sfx.KatanaWindup || s == Sfx.KatanaLunge || s == Sfx.KatanaHit;
        }

        void BuildKatana()
        {
            const Osc S = Osc.Sine;

            // --- The chain ---
            // Swipe 1: a 110 degree slash, left to right; swipe 2 sweeps back up and rings a little higher.
            Set(Sfx.KatanaSwipe1, Finish(Mix(
                Tone(0.18f, S, 700f, 260f, 0.16f, noise: 0.85f, lowpass: 0.55f, attack: 0.02f, decay: 1.6f),
                Tone(0.22f, S, KatanaRing, KatanaRing * 0.92f, KatanaRingVol, lowpass: 0.6f, decay: 4f)), 0.45f), 0.05f);
            Set(Sfx.KatanaSwipe2, Finish(Mix(
                Tone(0.18f, S, 300f, 820f, 0.16f, noise: 0.85f, lowpass: 0.55f, attack: 0.02f, decay: 1.6f),
                Tone(0.22f, S, KatanaRing * 1.12f, KatanaRing * 1.03f, KatanaRingVol, lowpass: 0.6f, decay: 4f)), 0.45f), 0.05f);
            // Lunge wind-up: the blade drawn back, a click as it sets (it is 0.15 s long, the pull-back before the thrust).
            Set(Sfx.KatanaWindup, Finish(Mix(
                Tone(0.15f, Osc.Triangle, 180f, 120f, 0.1f, noise: 0.3f, lowpass: 0.25f, attack: 0.06f),
                Delay(Tone(0.025f, Osc.Square, 1800f, 1500f, 0.07f, lowpass: 0.5f, decay: 3f), 0.12f)), 0.35f), 0.1f);
            // Lunge: a sharp rising rush, a crack at the front, a low thump as you push off.
            Set(Sfx.KatanaLunge, Finish(Room(Mix(
                Tone(0.24f, S, 500f, 2300f, 0.2f, noise: 0.85f * 0.85f, lowpass: 0.65f, attack: 0.004f, decay: 1.2f),
                Tone(0.05f, S, 3000f, 1500f, 0.2f, noise: 1f, lowpass: 0.9f, decay: 3f),
                Tone(0.14f, S, 150f, 55f, 0.3f, lowpass: 0.3f, decay: 2.5f),
                Delay(Bell(0.35f, KatanaRing * 0.9f, 0.05f), 0.04f)), KatanaRoom * 0.5f), 0.6f), 0.1f);
            // A cut lands: a slice, a thud and a thin ring.
            Set(Sfx.KatanaHit, Finish(Mix(
                Tone(0.07f, S, 1900f, 650f, 0.2f, noise: 0.9f, lowpass: 0.8f, decay: 3f),
                Tone(0.13f, S, 230f, 90f, 0.26f, noise: 0.3f, lowpass: 0.3f, decay: 3f),
                Bell(0.28f, KatanaRing * 0.75f, 0.045f)), 0.4f), 0.04f);

            // The stance switch: the blade turned over (a quick swish), a click as it sets in its new guard, a soft thump of the burst
            // and a short ring.
            Set(Sfx.KatanaStanceSwap, Finish(Room(Mix(
                Tone(0.14f, S, 400f, 1400f, 0.14f, noise: 0.85f, lowpass: 0.6f, attack: 0.01f, decay: 1.8f),
                Delay(Tone(0.025f, Osc.Square, 2100f, 1700f, 0.07f, lowpass: 0.55f, decay: 3f), 0.1f),
                Delay(Tone(0.18f, S, 160f, 60f, 0.22f, noise: 0.3f, lowpass: 0.3f, decay: 2.4f), 0.1f),
                Delay(Bell(0.4f, KatanaRing * 0.8f, 0.05f), 0.11f)), KatanaRoom * 0.6f), 0.5f), 0.1f);

            // The Momentum Burst: the blade driven into the ground (a crack), a deep thump and a rolling shockwave with a ring on top.
            Set(Sfx.KatanaBurstSfx, Finish(Room(Mix(
                Tone(0.04f, S, 3000f, 1200f, 0.22f, noise: 1f, lowpass: 0.9f, decay: 3f),
                Tone(0.45f, S, 70f, 30f, 0.5f, lowpass: 0.3f, decay: 1.6f),
                Tone(0.45f, S, 2500f, 120f, 0.3f, noise: 1f, lowpass: 0.5f, attack: 0.01f, decay: 1.4f),
                Delay(Bell(0.5f, 990f, 0.06f), 0.02f)), KatanaRoom), 0.65f), 0.1f);
            // Caltrops thrown: a handful of iron spilling out, little ticks scattering over a short whoosh.
            var tl = new float[6][];
            tl[0] = Tone(0.18f, S, 700f, 2400f, 0.16f, noise: 0.85f, lowpass: 0.6f, attack: 0.01f, decay: 1.6f);
            for (int k = 1; k < tl.Length; k++)
                tl[k] = Delay(Tone(0.03f, Osc.Square, 3200f + 260f * k, 2600f, 0.05f, lowpass: 0.6f, decay: 3f), 0.06f + 0.045f * k);
            Set(Sfx.KatanaCaltrops, Finish(Mix(tl), 0.45f), 0.1f);
            // Caltrops flung or seeking: a rising whistle of many small blades.
            Set(Sfx.KatanaFling, Finish(Room(Mix(
                Tone(0.22f, S, 1500f, 5000f, 0.18f, noise: 0.9f, lowpass: 0.8f, attack: 0.01f, decay: 1.5f),
                Delay(Bell(0.3f, 1760f, 0.07f), 0.04f)), KatanaRoom * 0.5f), 0.5f), 0.1f);

            // --- The meter (your own screen) ---
            Set(Sfx.KatanaReady, Finish(Room(Mix(
                Notes(new[] { 1319f, 1760f, 2349f }, 0.05f, S, KatanaChime, 1f),
                Delay(Bell(0.55f, 1568f, 0.1f), 0.05f)), KatanaRoom), 0.4f), 0.2f);
            Set(Sfx.KatanaEmpty, Finish(Tone(0.07f, Osc.Square, 230f, 190f, 0.18f, lowpass: 0.35f, decay: 3f), 0.3f), 0.15f);

            // --- The Alt strike ---
            // Aim begins: a breath in and a heartbeat.
            Set(Sfx.KatanaAim, Finish(Room(Mix(
                Reverse(Tone(0.38f, S, 520f, 160f, 0.12f, noise: 0.9f, lowpass: 0.3f, decay: 1.2f)),
                Tone(0.14f, S, 72f, 44f, 0.4f, lowpass: 0.2f, decay: 2f),
                Delay(Tone(0.14f, S, 68f, 42f, 0.3f, lowpass: 0.2f, decay: 2f), 0.2f),
                Tone(0.5f, S, 240f, 110f, 0.06f, lowpass: 0.4f, attack: 0.1f, decay: 1.5f)), KatanaRoom * 0.6f), 0.5f), 0.3f);
            Set(Sfx.KatanaSwitch, Finish(Tone(0.045f, Osc.Triangle, 1900f, 1500f, 0.12f, lowpass: 0.6f, decay: 3f), 0.3f), 0.06f);
            // Stance: the blade slides half out, tension rising over the 0.35 s before the cut.
            Set(Sfx.KatanaStance, Finish(Room(Mix(
                Tremolo(Tone(0.36f, Osc.Saw, 300f, 950f, 0.08f, lowpass: 0.3f, attack: 0.25f, decay: 0.6f), 14f, 0.4f),
                Tone(0.3f, S, 2100f, 3600f, 0.05f, noise: 0.5f, lowpass: 0.8f, attack: 0.1f, decay: 0.8f),
                Delay(Bell(0.3f, KatanaRing * 1.6f, 0.04f), 0.2f)), KatanaRoom * 0.5f), 0.5f), 0.3f);
            // The cut: a tearing rush, a boom underneath, a ring that outlasts it.
            Set(Sfx.KatanaStrikeCut, Finish(Room(Mix(
                Tone(0.32f, S, 3300f, 320f, 0.35f, noise: 0.85f, lowpass: 0.9f, attack: 0.001f, decay: 2f),
                Tone(0.55f, S, 95f, 30f, 0.5f, noise: 0.2f, lowpass: 0.15f, decay: 1.5f),
                Tone(0.06f, S, 3500f, 1800f, 0.3f, noise: 1f, lowpass: 0.95f, decay: 3f),
                Delay(Bell(0.9f, KatanaRing * 1.5f, 0.1f), 0.05f)), KatanaRoom), 0.75f), 0.3f);
            // The empowered cut: a rising swell first (KatanaEmpLead, so it is raised that much before the cut), then a lower boom and two bells.
            Set(Sfx.KatanaEmpStrikeCut, Finish(Room(Mix(
                Reverse(Tone(0.3f, Osc.Saw, 110f, 330f, 0.1f, lowpass: 0.3f, decay: 1f)),
                Delay(Tone(0.34f, S, 3600f, 300f, 0.38f, noise: 0.85f, lowpass: 0.92f, attack: 0.001f, decay: 2f), 0.28f),
                Delay(Tone(0.7f, S, 80f, 24f, 0.6f, noise: 0.2f, lowpass: 0.15f, decay: 1.4f), 0.28f),
                Delay(Tone(0.06f, S, 3600f, 1900f, 0.3f, noise: 1f, lowpass: 0.95f, decay: 3f), 0.28f),
                Delay(Bell(1.2f, KatanaRing * 0.7f, 0.16f), 0.3f),
                Delay(Bell(1.2f, KatanaRing * 1.4f, 0.1f), 0.32f)), KatanaRoom * 1.2f), 0.85f), 0.3f);
            // Sheathe: a click as the blade goes home, then quiet until the blow.
            Set(Sfx.KatanaSheathe, Finish(Mix(
                Tone(0.03f, Osc.Square, 900f, 700f, 0.3f, lowpass: 0.7f, decay: 3f),
                Delay(Tone(0.05f, S, 1400f, 1200f, 0.15f, decay: 4f), 0.02f),
                Delay(Bell(0.45f, 2200f, 0.06f), 0.03f)), 0.4f), 0.3f);
            // The blow: a heavy slice, a body thud, a stone-crack tail.
            Set(Sfx.KatanaBlow, Finish(Room(Mix(
                Tone(0.1f, S, 2300f, 600f, 0.3f, noise: 0.9f, lowpass: 0.85f, decay: 3f),
                Tone(0.38f, S, 100f, 34f, 0.5f, noise: 0.4f, lowpass: 0.15f, decay: 2f),
                Crackle(0.22f, 14),
                Delay(Tone(0.16f, S, 200f, 70f, 0.3f, noise: 0.3f, lowpass: 0.25f, decay: 3f), 0.05f)), KatanaRoom * 0.8f), 0.7f), 0.3f);

            // --- The streak (each starts KatanaAfterBlow in, so the blow is heard first) ---
            Sfx[] chains = { Sfx.KatanaChain1, Sfx.KatanaChain2, Sfx.KatanaChain3, Sfx.KatanaChain4, Sfx.KatanaChain5 };
            for (int i = 0; i < chains.Length; i++)
            {
                float q = KatanaChainPitch[i];  // up a pentatonic line
                Set(chains[i], Finish(Delay(Room(Mix(
                    Notes(new[] { q, q * 2f }, 0.07f, S, KatanaChime, 1f),
                    Bell(0.5f, q * 2f, 0.08f),
                    Tone(0.12f, S, q / 4f, q / 5f, 0.2f, lowpass: 0.3f, decay: 3f)), KatanaRoom * 0.8f), KatanaAfterBlow), 0.4f), 0.1f);
            }
            Set(Sfx.KatanaEmpReady, Finish(Delay(Room(Mix(
                Notes(new[] { 784f, 988f, 1175f, 1568f }, 0.07f, S, KatanaChime * 1.2f, 1f),
                Delay(Bell(1f, 784f, 0.14f), 0.05f),
                Reverse(Tone(0.45f, Osc.Saw, 110f, 230f, 0.09f, lowpass: 0.3f, decay: 1f))), KatanaRoom * 1.1f), KatanaAfterBlow + 0.05f), 0.5f), 0.4f);
            Set(Sfx.KatanaLost, Finish(Delay(Mix(
                Tone(0.4f, S, 330f, 110f, 0.2f, lowpass: 0.4f, decay: 2f),
                Delay(Tone(0.1f, Osc.Square, 160f, 90f, 0.12f, lowpass: 0.3f, decay: 3f), 0.05f)), KatanaAfterBlow), 0.4f), 0.3f);
        }
    }
}
