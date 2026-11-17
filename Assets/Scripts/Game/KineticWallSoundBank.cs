using System;
using UnityEngine;

namespace Arena
{
    /// Kinetic Wall's sounds (Core/KineticWall.cs), ported from the synth of Docs/Mockups/kinetic-wall-fx.html. Each screen plays them from
    /// what it sees (KineticWallLook.cs `TickKineticWallSounds`), so nothing is sent. On the Effects volume; an ally's are quieter.
    ///   KwCast     the field powering on: a rising sine sweep, a glassy shimmer opening up and a rush of noise
    ///   KwHum      the field's drone (a loop): two tones throbbing through a low-pass; brighter, higher and faster as it fills
    ///   KwCatch    a shot dragged to a stop: a falling "thwip" and a soft click (three takes, rate-limited)
    ///   KwFull     a bright two-note chime when it's full
    ///   KwUnleash  a short suck-in, then a deep boom with a crackling tail
    public partial class SoundBank
    {
        AudioSource kwHumVoice;  // (made in the constructor)

        void BuildKineticWall()
        {
            const Osc S = Osc.Sine, W = Osc.Saw, T = Osc.Triangle;
            Set(Sfx.KwCast, Finish(Mix(
                Tone(0.6f, S, 110f, 520f, 0.5f, attack: 0.04f, decay: 1.6f),                                         // the sweep
                Delay(Tone(0.7f, W, 880f, 2600f, 0.07f, lowpass: 0.35f, attack: 0.12f, decay: 1.8f), 0.1f),         // the shimmer
                Delay(Tone(0.7f, W, 887f, 2620f, 0.06f, lowpass: 0.35f, attack: 0.12f, decay: 1.8f), 0.1f),
                Tone(0.5f, S, 400f, 4000f, 0.22f, noise: 1f, lowpass: 0.3f, attack: 0.05f, decay: 1.5f)), 0.55f), 0.1f);  // the rush
            Set(Sfx.KwCatch, Takes(3, () => Finish(Mix(
                Tone(0.16f, T, 1100f, 240f, 0.5f, attack: 0.004f, decay: 2.2f),                                       // the thwip
                Tone(0.05f, S, 5000f, 4500f, 0.25f, noise: 1f, lowpass: 1f, attack: 0.002f, decay: 3f)), 0.3f)), 0.035f);  // the click
            Set(Sfx.KwFull, Finish(Mix(
                Tone(0.7f, S, 1318f, 1318f, 0.4f, attack: 0.01f, decay: 2.5f),
                Delay(Tone(0.7f, S, 1976f, 1976f, 0.35f, attack: 0.01f, decay: 2.5f), 0.09f)), 0.4f), 0.2f);
            var crackle = new float[14][];
            for (int i = 0; i < crackle.Length; i++)
                crackle[i] = Delay(Tone(0.03f, S, 2000f + (float)rng.NextDouble() * 4000f, 2500f, 0.3f, noise: 0.9f, lowpass: 0.6f, attack: 0.001f, decay: 2f),
                    0.15f + (float)rng.NextDouble() * 0.6f);
            Set(Sfx.KwUnleash, Finish(Room(Mix(
                Tone(0.06f, S, 300f, 5000f, 0.35f, noise: 1f, lowpass: 0.3f, attack: 0.05f, decay: 0.2f),            // suck-in
                Delay(Tone(0.8f, S, 110f, 32f, 0.9f, attack: 0.006f, decay: 1.6f), 0.06f),                           // boom
                Delay(Tone(1f, S, 6000f, 200f, 0.5f, noise: 1f, lowpass: 0.15f, attack: 0.004f, decay: 2.2f), 0.06f), // blast noise
                Mix(crackle)), 0.15f), 0.7f), 0.15f);
            // The hum: a 55 Hz saw and a 110 Hz sine (whole cycles in the 0.6 s loop: 33 and 66) through a low-pass, throbbing 5 times a second.
            const float len = 0.6f;
            var hum = Tremolo(Mix(
                Tone(len, W, 55f, 55f, 0.5f, lowpass: 0.08f, attack: 0.0005f, decay: 0f),
                Tone(len, S, 110f, 110f, 0.3f, attack: 0.0005f, decay: 0f)), 5f, 0.35f);
            float max = 0f;
            foreach (float v in hum)
                max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < hum.Length && max > 0f; i++)
                hum[i] *= 0.2f / max;
            Set(Sfx.KwHum, hum, 0f);
            Jitter(0.06f, Sfx.KwCast, Sfx.KwFull, Sfx.KwUnleash);
            Jitter(0.2f, Sfx.KwCatch);
        }

        /// A Kinetic Wall sound: yours at full volume, an ally's quieter, both quieter with distance.
        public void PlayKw(Sfx s, float now, bool mine, float quiet) => Play(new SoundCue { Sfx = s, Ally = !mine, Quiet = quiet }, now);

        public void PlayKwCatch(float now, bool mine, float quiet) => Play(Sfx.KwCatch, (mine ? 0.8f : 0.35f) * (1f - quiet), now);

        /// The hum: `level` 0 (off) to 1; `charge` 0..1 raises its pitch and volume.
        public void SetKwHum(float level, float charge)
        {
            if (kwHumVoice == null)
                return;
            if (kwHumVoice.clip == null)
            {
                var c = ClipsOf(Sfx.KwHum);
                if (c == null)
                    return;
                kwHumVoice.clip = c[0];
            }
            float v = Muted ? 0f : level * Master * Effects;
            if (v <= 0.002f)
            {
                if (kwHumVoice.isPlaying)
                    kwHumVoice.Stop();
                return;
            }
            kwHumVoice.volume = Mathf.Min(1f, v * (0.7f + 0.6f * charge));
            kwHumVoice.pitch = 1f + 0.5f * charge;
            if (!kwHumVoice.isPlaying)
                kwHumVoice.Play();
        }
    }
}
