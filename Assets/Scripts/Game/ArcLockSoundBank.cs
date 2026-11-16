using System;
using UnityEngine;

namespace Arena
{
    /// Arc Lightning's lock-on sounds (Core/ArcLock.cs), ported from the mockup's synth: a charge hum that rises with every tag (a loop,
    /// your own draw only), an electric crackle and a low zap per tag climbing a step each time, a thunder crack when you let go, a
    /// snap per hop rising along the chain, a thin zap per Forked Volley bolt and a fizzle when nothing was tagged. They play from the
    /// effects the host sends (FxKind.ArcTag, ArcLink, ArcFork; Effect.Heard), at the Weapons volume; someone else's are quieter.
    public partial class SoundBank
    {
        AudioSource arcHumVoice;  // (made in the constructor)

        void BuildArcLock()
        {
            const Osc S = Osc.Sine;
            // A tag: a burst of high crackle, a second ping of it, and a low sawtooth zap falling away.
            Set(Sfx.ArcTag, Finish(Mix(
                Tone(0.09f, S, 3200f, 3000f, 0.35f, noise: 1f, lowpass: 0.95f, decay: 2.5f),
                Delay(Tone(0.05f, S, 1800f, 1700f, 0.2f, noise: 0.85f, lowpass: 0.6f, decay: 3f), 0.03f),
                Tone(0.16f, Osc.Saw, 160f, 90f, 0.12f, lowpass: 0.3f, decay: 2f)), 0.42f), 0.03f);
            // A hop: a snap of filtered noise with a falling buzz.
            Set(Sfx.ArcHop, Finish(Mix(
                Tone(0.09f, S, 1300f, 1100f, 0.35f, noise: 0.95f, lowpass: 0.55f, decay: 3f),
                Tone(0.07f, Osc.Saw, 1900f, 240f, 0.08f, lowpass: 0.5f, decay: 2.2f)), 0.4f), 0.025f);
            // Letting go: a white crack, a rolling rumble under it and a low drop.
            Set(Sfx.ArcCrack, Finish(Room(Mix(
                Tone(0.06f, S, 4200f, 3800f, 0.45f, noise: 1f, lowpass: 1f, decay: 3f),
                Tone(0.7f, S, 900f, 120f, 0.5f, noise: 1f, lowpass: 0.08f, attack: 0.01f, decay: 1.6f),
                Tone(0.35f, S, 120f, 40f, 0.3f, decay: 2f)), 0.2f), 0.6f), 0.1f);
            // A Forked Volley bolt: thinner and brighter than a hop.
            Set(Sfx.ArcFork, Finish(Mix(
                Tone(0.07f, S, 2200f, 1800f, 0.3f, noise: 0.9f, lowpass: 0.7f, decay: 3f),
                Tone(0.08f, Osc.Saw, 2600f, 600f, 0.07f, lowpass: 0.6f, decay: 2.4f)), 0.34f), 0.03f);
            // Nothing tagged: a short dull fizz.
            Set(Sfx.ArcFizzle, Finish(Tone(0.14f, S, 700f, 300f, 0.3f, noise: 0.95f, lowpass: 0.25f, decay: 2f), 0.2f), 0.1f);
            // The charge hum: two detuned saws near 55 Hz through a low-pass, throbbing 10 times a second. Whole cycles in the
            // 0.6 s loop (33 and 34 of the saws, 66 of the octave, 6 throbs) so it joins up without a click.
            const float len = 0.6f;
            var hum = Tremolo(Mix(
                Tone(len, Osc.Saw, 55f, 55f, 0.5f, lowpass: 0.06f, attack: 0.0005f, decay: 0f),
                Tone(len, Osc.Saw, 56.67f, 56.67f, 0.5f, lowpass: 0.06f, attack: 0.0005f, decay: 0f),
                Tone(len, S, 110f, 110f, 0.2f, attack: 0.0005f, decay: 0f)), 10f, 0.4f);
            float max = 0f;
            foreach (float v in hum)
                max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < hum.Length && max > 0f; i++)
                hum[i] *= 0.2f / max;
            Set(Sfx.ArcHum, hum, 0f);
        }

        void PlayArc(Sfx s, float now, float volume, float pitch) => Play(s, volume, now, -1, pitch * (1f + ((float)playRng.NextDouble() - 0.5f) * 0.03f));

        /// Tag number `index` (from 0): each a step higher (a semitone), so ten tags climb.
        public void PlayArcTag(float now, int index, bool mine) => PlayArc(Sfx.ArcTag, now, mine ? 1f : 0.45f, Mathf.Pow(2f, Mathf.Clamp(index, 0, 15) / 12f));

        /// A hop of the chain: the first carries the crack of letting go; each later one snaps a little higher.
        public void PlayArcHop(float now, int index, bool mine)
        {
            float v = mine ? 1f : 0.45f;
            if (index <= 0)
                PlayArc(Sfx.ArcCrack, now, v, 1f);
            PlayArc(Sfx.ArcHop, now, v * 0.9f, 1f + 0.06f * Mathf.Clamp(index, 0, 15));
        }

        public void PlayArcFork(float now, int bolt, bool mine) => PlayArc(Sfx.ArcFork, now, mine ? 0.9f : 0.4f, 1f + 0.08f * bolt);

        public void PlayArcFizzle(float now, bool mine) => PlayArc(Sfx.ArcFizzle, now, mine ? 0.8f : 0.35f, 1f);

        /// The charge hum: `level` 0 (off) to 1, rising in pitch and getting brighter with each tag.
        public void SetArcHum(float level, int tags)
        {
            if (arcHumVoice == null)
                return;
            if (arcHumVoice.clip == null)
            {
                var c = ClipsOf(Sfx.ArcHum);
                if (c == null)
                    return;
                arcHumVoice.clip = c[0];
            }
            float v = Muted ? 0f : level * Master * Weapons;
            if (v <= 0.002f)
            {
                if (arcHumVoice.isPlaying)
                    arcHumVoice.Stop();
                return;
            }
            arcHumVoice.volume = Mathf.Min(1f, v * (1f + 0.05f * tags));
            arcHumVoice.pitch = 1f + 0.09f * tags;
            if (!arcHumVoice.isPlaying)
                arcHumVoice.Play();
        }
    }
}
