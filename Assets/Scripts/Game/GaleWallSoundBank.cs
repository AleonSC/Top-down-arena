using System;
using UnityEngine;

namespace Arena
{
    /// Gale Wall's sounds (Core/GaleWall.cs): a wall of wind. Each screen plays them from what it sees
    /// (GaleWallLook.cs `TickGaleWallSounds`), so nothing is sent. On the Effects volume; an ally's are quieter.
    ///   KwCast     the wall rising: a gust swelling in, with a thin whistle sliding up over it
    ///   KwHum      the wall's howl (a loop): wind noise gusting once a loop, a faint whistle on top; higher and louder as it fills
    ///   KwCatch    a shot caught in the wind: a short puff of air and a soft low thud (three takes, rate-limited)
    ///   KwFull     a breathy two-note whistle when it's full
    ///   KwUnleash  air sucked in, then a gale bursting out: a deep whoosh, a soft boom and a fluttering tail
    public partial class SoundBank
    {
        AudioSource kwHumVoice;  // (made in the constructor)

        void BuildGaleWall()
        {
            const Osc S = Osc.Sine;
            Set(Sfx.KwCast, Finish(Mix(
                Rush(0.75f, 300f, 1800f, 0.6f, 0.3f),                                                                   // the gust
                Tone(0.7f, S, 90f, 140f, 0.18f, noise: 1f, lowpass: 0.03f, attack: 0.25f, decay: 1.6f),                 // its body
                Delay(Tremolo(Tone(0.55f, S, 620f, 1150f, 0.08f, noise: 0.15f, attack: 0.15f, decay: 1.6f), 6f, 0.4f), 0.1f)), 0.5f), 0.1f);  // the whistle
            Set(Sfx.KwCatch, Takes(3, () => Finish(Mix(
                Tone(0.14f, S, 1, 1, 0.6f, noise: 1f, lowpass: 0.22f, attack: 0.006f, decay: 2.6f),                    // the puff
                Tone(0.12f, S, 210f, 110f, 0.35f, attack: 0.004f, decay: 2.4f)), 0.3f)), 0.035f);                       // the thud
            Set(Sfx.KwFull, Finish(Mix(
                Tone(0.45f, S, 988f, 988f, 0.3f, noise: 0.12f, attack: 0.03f, decay: 2.2f),
                Delay(Tone(0.5f, S, 1318f, 1318f, 0.3f, noise: 0.12f, attack: 0.03f, decay: 2.2f), 0.12f),
                Rush(0.6f, 900f, 2400f, 0.15f, 0.05f)), 0.4f), 0.2f);
            Set(Sfx.KwUnleash, Finish(Room(Mix(
                Reverse(Rush(0.18f, 2000f, 600f, 0.35f, 0.01f)),                                                       // sucked in
                Delay(Tone(1.1f, S, 1, 1, 0.9f, noise: 1f, lowpass: 0.09f, attack: 0.01f, decay: 1.4f), 0.16f),        // the gale
                Delay(Tone(0.6f, S, 90f, 38f, 0.6f, attack: 0.008f, decay: 1.8f), 0.16f),                              // the boom
                Delay(Tremolo(Rush(0.9f, 2600f, 500f, 0.3f, 0.02f), 13f, 0.7f), 0.22f)), 0.15f), 0.7f), 0.15f);        // flutter
            // The howl: wind noise and a faint 440 Hz whistle (264 cycles in the 0.6 s loop), gusting once a loop (1/0.6 Hz),
            // made seamless by fading the take's extra tail back over its start.
            const float len = 0.6f, over = 0.15f;
            var take = Mix(
                Tone(len + over, S, 1, 1, 0.6f, noise: 1f, lowpass: 0.05f, attack: 0.0005f, decay: 0f),
                Tone(len + over, S, 440f, 440f, 0.05f, attack: 0.0005f, decay: 0f));
            int n = (int)(len * Rate), m = take.Length - n;
            var hum = new float[n];
            for (int i = 0; i < n; i++)
            {
                hum[i] = take[i];
                if (i < m)
                {
                    float w = (float)i / m;
                    hum[i] = take[i] * w + take[n + i] * (1f - w);
                }
            }
            hum = Tremolo(hum, 1f / len, 0.35f);
            float max = 0f;
            foreach (float v in hum)
                max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < hum.Length && max > 0f; i++)
                hum[i] *= 0.2f / max;
            Set(Sfx.KwHum, hum, 0f);
            Jitter(0.06f, Sfx.KwCast, Sfx.KwFull, Sfx.KwUnleash);
            Jitter(0.2f, Sfx.KwCatch);
        }

        /// A Gale Wall sound: yours at full volume, an ally's quieter, both quieter with distance.
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
