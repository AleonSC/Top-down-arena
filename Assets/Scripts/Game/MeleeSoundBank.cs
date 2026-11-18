using System;
using UnityEngine;

namespace Arena
{
    /// The melee rushers' sounds (cues made in Core/MeleeSounds.cs), from the recipes of Docs/Mockups/melee-enemy-sounds.html.
    /// Deaths are all different.
    public partial class SoundBank
    {
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

        void BuildMelee()
        {
            // Swarmer: small, wet and quiet, because they come four at a time.
            Set(Sfx.SwarmerChomp, Takes(3, () => Finish(Mix(
                Tone(0.03f, Osc.Square, 1500f, 700f, 0.2f, noise: 0.5f, lowpass: 0.7f, decay: 3f),
                Delay(Tone(0.03f, Osc.Square, 1100f, 500f, 0.16f, noise: 0.5f, lowpass: 0.7f, decay: 3f), 0.045f),
                Tone(0.09f, Osc.Sine, 380f, 150f, 0.2f, noise: 0.35f, lowpass: 0.3f, decay: 2.5f)), 0.3f)), 0.08f);
            Set(Sfx.SwarmerDie, Finish(Mix(
                Tone(0.07f, Osc.Sine, 900f, 2200f, 0.18f, decay: 2f),
                Tone(0.04f, Osc.Sine, 2400f, 900f, 0.12f, noise: 0.9f, lowpass: 0.8f, decay: 3f)), 0.24f), 0.04f);

            // Brute: low and slow; you feel it before you see it.
            // A stomp with a scuff on top: the low thump alone is lost on small speakers.
            Set(Sfx.BruteStep, Takes(2, () => Finish(Mix(
                Tone(0.14f, Osc.Sine, 120f, 50f, 0.5f, lowpass: 0.25f, decay: 2.2f),
                Tone(0.1f, Osc.Triangle, 320f, 140f, 0.3f, noise: 0.5f, lowpass: 0.4f, decay: 2.5f),
                Tone(0.06f, Osc.Sine, 1200f, 400f, 0.2f, noise: 1f, lowpass: 0.5f, decay: 3f)), 0.55f)), 0.2f);
            // The small ones: a Swarmer's quick soft patter, a Lunger's padding step.
            Set(Sfx.SwarmerPatter, Takes(3, () => Finish(Mix(
                Tone(0.03f, Osc.Triangle, 700f, 380f, 0.2f, noise: 0.6f, lowpass: 0.3f, decay: 3f),
                Delay(Tone(0.03f, Osc.Triangle, 800f, 400f, 0.14f, noise: 0.6f, lowpass: 0.3f, decay: 3f), 0.06f)), 0.07f)), 0.15f);
            Set(Sfx.LungerPad, Takes(2, () => Finish(Mix(
                Tone(0.08f, Osc.Sine, 260f, 110f, 0.3f, noise: 0.4f, lowpass: 0.3f, decay: 2.5f),
                Tone(0.04f, Osc.Sine, 900f, 400f, 0.1f, noise: 1f, lowpass: 0.5f, decay: 3f)), 0.3f)), 0.2f);
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
        }
    }
}
