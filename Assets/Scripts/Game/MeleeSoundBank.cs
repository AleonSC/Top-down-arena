using System;
using UnityEngine;

namespace Arena
{
    /// The melee rushers' sounds (cues made in Core/MeleeSounds.cs), from the recipes of Docs/Mockups/melee-enemy-sounds.html.
    /// Deaths are all different; the Buzzsaw has no touch tick, only the contact whirr (a loop: SetSawLoop).
    public partial class SoundBank
    {
        AudioSource sawVoice;

        /// True for the melee rushers' cues, so they use the Enemies volume.
        static bool IsMeleeEnemySfx(string name)
        {
            return name.StartsWith("Swarmer") || name.StartsWith("Spiker") || name.StartsWith("Brute") || name.StartsWith("Lunger")
                || name.StartsWith("Saw") || name.StartsWith("SpikeBlock");
        }

        static float[] Delay(float[] a, float seconds)
        {
            return Concat(new float[(int)(seconds * Rate)], a);
        }

        /// The sound plus a little of a small room's echo of it.
        static float[] Room(float[] a, float wet)
        {
            var padded = Concat(a, new float[(int)(0.3f * Rate)]);
            var echo = Reverb(padded);
            for (int i = 0; i < padded.Length; i++)
                padded[i] += echo[i] * wet;
            return padded;
        }

        float[][] Takes(int n, Func<float[]> make)
        {
            var t = new float[n][];
            for (int i = 0; i < n; i++)
                t[i] = make();
            return t;
        }

        void BuildMelee(GameObject owner)
        {
            // Swarmer: small, wet and quiet, because they come four at a time.
            Set(Sfx.SwarmerChomp, Takes(3, () => Finish(Mix(
                Tone(0.03f, Osc.Square, 1500f, 700f, 0.2f, noise: 0.5f, lowpass: 0.7f, decay: 3f),
                Delay(Tone(0.03f, Osc.Square, 1100f, 500f, 0.16f, noise: 0.5f, lowpass: 0.7f, decay: 3f), 0.045f),
                Tone(0.09f, Osc.Sine, 380f, 150f, 0.2f, noise: 0.35f, lowpass: 0.3f, decay: 2.5f)), 0.3f)), 0.08f);
            Set(Sfx.SwarmerDie, Finish(Mix(
                Tone(0.07f, Osc.Sine, 900f, 2200f, 0.18f, decay: 2f),
                Tone(0.04f, Osc.Sine, 2400f, 900f, 0.12f, noise: 0.9f, lowpass: 0.8f, decay: 3f)), 0.24f), 0.04f);

            // Spiker: high and dry, so it sits above the Brutes' thuds.
            Set(Sfx.SpikerFlip, Takes(3, () => Finish(Mix(
                Tone(0.035f, Osc.Square, 2600f, 3400f, 0.1f, lowpass: 0.6f, decay: 3f),
                Tone(0.05f, Osc.Triangle, 1500f, 900f, 0.1f, noise: 0.4f, lowpass: 0.5f, decay: 3f)), 0.2f)), 0.1f);
            Set(Sfx.SpikerStab, Finish(Mix(
                Tone(0.06f, Osc.Sine, 4200f, 2200f, 0.16f, noise: 0.9f, lowpass: 0.9f, decay: 3f),
                Tone(0.1f, Osc.Triangle, 320f, 160f, 0.2f, noise: 0.3f, lowpass: 0.3f, decay: 2.5f),
                Bell(0.28f, 3100f, 0.045f)), 0.32f), 0.1f);
            Set(Sfx.SpikerDie, Finish(Mix(
                Crackle(0.16f, 13),
                Tone(0.12f, Osc.Sine, 3600f, 1100f, 0.1f, noise: 0.5f, lowpass: 0.9f, decay: 2.5f),
                Bell(0.35f, 2400f, 0.06f)), 0.32f), 0.05f);

            // Brute: low and slow; you feel it before you see it.
            Set(Sfx.BruteStep, Takes(2, () => Finish(Mix(
                Tone(0.12f, Osc.Sine, 110f, 48f, 0.5f, lowpass: 0.25f, decay: 2.2f),
                Tone(0.05f, Osc.Sine, 500f, 180f, 0.14f, noise: 0.9f, lowpass: 0.35f, decay: 3f)), 0.4f)), 0.25f);
            Set(Sfx.BruteGrunt, Finish(Mix(
                Tone(0.22f, Osc.Saw, 100f, 70f, 0.3f, noise: 0.12f, lowpass: 0.18f, attack: 0.02f, decay: 1.4f),
                Tone(0.22f, Osc.Saw, 104f, 74f, 0.2f, lowpass: 0.25f, attack: 0.03f, decay: 1.6f)), 0.4f), 0.5f);
            Set(Sfx.BruteBash, Finish(Room(Mix(
                Tone(0.28f, Osc.Sine, 130f, 36f, 0.6f, noise: 0.2f, lowpass: 0.2f, decay: 2f),
                Tone(0.05f, Osc.Sine, 1400f, 500f, 0.25f, noise: 1f, lowpass: 0.8f, decay: 3f),
                Crackle(0.18f, 8)), 0.3f), 0.55f), 0.12f);
            Set(Sfx.BruteDie, Finish(Room(Mix(
                Tone(0.4f, Osc.Saw, 130f, 45f, 0.28f, noise: 0.1f, lowpass: 0.15f, attack: 0.02f, decay: 1.3f),
                Delay(Tone(0.3f, Osc.Sine, 90f, 28f, 0.55f, noise: 0.3f, lowpass: 0.15f, decay: 2f), 0.28f),
                Delay(Crackle(0.14f, 6), 0.28f)), 0.35f), 0.5f), 0.2f);

            // Lunger: the pink line's tell must be heard under gunfire; the death is a yowl that drops into a thud.
            Set(Sfx.LungerTell, Finish(Mix(
                Tone(0.22f, Osc.Saw, 600f, 2400f, 0.1f, lowpass: 0.5f, attack: 0.01f, decay: 0.8f),
                Delay(Bell(0.35f, 1760f, 0.12f), 0.2f),
                Delay(Tone(0.04f, Osc.Square, 2400f, 2400f, 0.1f, lowpass: 0.6f, decay: 3f), 0.2f)), 0.45f), 0.3f);
            Set(Sfx.LungerLeap, Finish(Mix(
                Tone(0.3f, Osc.Sine, 2200f, 300f, 0.22f, noise: 0.85f, lowpass: 0.6f, attack: 0.004f, decay: 1.4f),
                Tone(0.1f, Osc.Sine, 140f, 60f, 0.4f, lowpass: 0.25f, decay: 2.5f)), 0.5f), 0.2f);
            Set(Sfx.LungerLand, Finish(Mix(
                Tone(0.14f, Osc.Sine, 160f, 50f, 0.5f, noise: 0.25f, lowpass: 0.25f, decay: 2.5f),
                Tone(0.14f, Osc.Saw, 260f, 150f, 0.12f, lowpass: 0.25f, decay: 2f),
                Tone(0.04f, Osc.Sine, 1600f, 600f, 0.15f, noise: 1f, lowpass: 0.8f, decay: 3f)), 0.5f), 0.15f);
            Set(Sfx.LungerDie, Finish(Mix(
                Tone(0.25f, Osc.Saw, 520f, 160f, 0.18f, noise: 0.2f, lowpass: 0.35f, decay: 1.5f),
                Tone(0.1f, Osc.Sine, 1800f, 500f, 0.14f, noise: 0.9f, lowpass: 0.7f, decay: 3f),
                Delay(Tone(0.12f, Osc.Sine, 140f, 50f, 0.3f, lowpass: 0.25f, decay: 2.5f), 0.1f)), 0.4f), 0.1f);

            // Buzzsaw: kept quiet. No ticks and no swoop; the contact whirr is a loop (SetSawLoop), and the death is a short wind-down.
            // Whole cycles in the loop (127 of the saw, 20 of the flutter) so it joins up without a click.
            const float loopLen = 20f / 33f;
            var whirr = Tremolo(Mix(
                Tone(loopLen, Osc.Saw, 127f / loopLen, 127f / loopLen, 0.1f, noise: 0.15f, lowpass: 0.3f, attack: 0.0005f, decay: 0f),
                Tone(loopLen, Osc.Saw, 254f / loopLen, 254f / loopLen, 0.05f, lowpass: 0.4f, attack: 0.0005f, decay: 0f)), 33f, 0.55f);
            Set(Sfx.SawWhirr, whirr, 0f);
            Set(Sfx.SawDie, Finish(Mix(
                Tremolo(Tone(0.7f, Osc.Saw, 230f, 40f, 0.25f, noise: 0.15f, lowpass: 0.3f, decay: 0.5f), 18f, 0.6f),
                Delay(Tone(0.12f, Osc.Sine, 2800f, 1400f, 0.12f, noise: 0.9f, lowpass: 0.8f, decay: 3f), 0.05f),
                Delay(Bell(0.4f, 1500f, 0.04f), 0.1f)), 0.2f), 0.2f);

            // Spike Block: rock, shake (the tell), lunge, and an audible rest, which is the cue to shoot it.
            Set(Sfx.SpikeBlockRock, Finish(Mix(
                Tone(0.1f, Osc.Square, 150f, 90f, 0.22f, lowpass: 0.2f, decay: 2.5f),
                Delay(Tone(0.09f, Osc.Square, 130f, 80f, 0.2f, lowpass: 0.2f, decay: 2.5f), 0.24f),
                Delay(Tone(0.04f, Osc.Sine, 2200f, 1400f, 0.04f, noise: 0.6f, decay: 3f), 0.01f)), 0.3f), 0.4f);
            Set(Sfx.SpikeBlockShake, Finish(Tremolo(Mix(
                Tone(0.55f, Osc.Square, 150f, 260f, 0.12f, noise: 0.3f, lowpass: 0.3f, attack: 0.1f, decay: 0.4f),
                Crackle(0.55f, 38),
                Tone(0.55f, Osc.Saw, 60f, 110f, 0.2f, lowpass: 0.15f, attack: 0.15f, decay: 0.3f)), 24f, 0.55f), 0.45f), 0.6f);
            Set(Sfx.SpikeBlockLunge, Finish(Room(Mix(
                Tone(0.3f, Osc.Saw, 90f, 200f, 0.3f, noise: 0.25f, lowpass: 0.2f, attack: 0.01f, decay: 1.1f),
                Tone(0.25f, Osc.Sine, 1800f, 500f, 0.1f, noise: 0.9f, lowpass: 0.55f, decay: 1.5f),
                Delay(Tone(0.12f, Osc.Sine, 90f, 32f, 0.55f, lowpass: 0.2f, decay: 2f), 0.22f),
                Delay(Bell(0.5f, 760f, 0.1f), 0.22f)), 0.25f), 0.6f), 0.3f);
            Set(Sfx.SpikeBlockRest, Finish(Mix(
                Tone(0.4f, Osc.Sine, 700f, 2200f, 0.07f, noise: 0.9f, lowpass: 0.25f, attack: 0.05f, decay: 1f),
                Delay(Tone(0.12f, Osc.Square, 120f, 70f, 0.22f, lowpass: 0.2f, decay: 2.5f), 0.05f),
                Delay(Notes(new[] { 600f, 450f }, 0.1f, Osc.Triangle, 0.09f, 1f), 0.3f)), 0.35f), 0.5f);
            Set(Sfx.SpikeBlockDie, Finish(Room(Mix(
                Crackle(0.25f, 16),
                Tone(0.3f, Osc.Sine, 110f, 35f, 0.5f, noise: 0.3f, lowpass: 0.2f, decay: 2f),
                Bell(0.4f, 700f, 0.07f),
                Tone(0.06f, Osc.Sine, 1500f, 600f, 0.2f, noise: 1f, lowpass: 0.8f, decay: 3f)), 0.2f), 0.5f), 0.15f);

            // The Buzzsaw's contact whirr plays on its own looping voice.
            sawVoice = owner.AddComponent<AudioSource>();
            sawVoice.playOnAwake = false;
            sawVoice.spatialBlend = 0f;
            sawVoice.loop = true;
            sawVoice.clip = clips[(int)Sfx.SawWhirr][0];
        }

        /// Fade the Buzzsaw's contact whirr to `level` (0 = off, 1 = full), from Game.SawWhirrLevel each frame.
        public void SetSawLoop(float level)
        {
            if (sawVoice == null)
                return;
            float v = Muted ? 0f : level * Master * Enemies;
            if (v <= 0.002f)
            {
                if (sawVoice.isPlaying)
                    sawVoice.Stop();
                return;
            }
            sawVoice.volume = Mathf.Min(1f, v);
            if (!sawVoice.isPlaying)
                sawVoice.Play();
        }
    }
}
