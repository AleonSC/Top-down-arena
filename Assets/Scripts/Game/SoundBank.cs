using System;
using UnityEngine;

namespace Arena
{
    /// Retro sound effects synthesized at startup, so the project needs no audio files.
    /// Tweak the recipes in Build() to change how things sound.
    public class SoundBank
    {
        const int Rate = 44100;

        enum Osc { Sine, Square, Triangle, Saw }

        readonly int count = Enum.GetValues(typeof(Sfx)).Length;
        readonly AudioClip[] clips;
        readonly float[] volume, minGap, lastPlayed;
        readonly AudioSource[] voices;
        readonly System.Random rng = new System.Random(7);
        int nextVoice;

        public bool Muted;

        public SoundBank(GameObject owner)
        {
            clips = new AudioClip[count];
            volume = new float[count];
            minGap = new float[count];
            lastPlayed = new float[count];
            for (int i = 0; i < count; i++)
            {
                volume[i] = 1f;
                lastPlayed[i] = -10f;
            }
            voices = new AudioSource[12];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = owner.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
            Build();
        }

        void Build()
        {
            // Rapid-fire sounds get a minimum gap so a bullet storm doesn't turn into noise.
            Set(Sfx.PlayerShot, Tone(0.07f, Osc.Square, 880f, 330f, 0.18f, lowpass: 0.5f), gap: 0.06f);
            Set(Sfx.AllyShot, Tone(0.07f, Osc.Square, 700f, 280f, 0.1f, lowpass: 0.4f), gap: 0.09f);
            Set(Sfx.EnemyShot, Tone(0.06f, Osc.Triangle, 520f, 380f, 0.1f), gap: 0.08f);
            Set(Sfx.Hit, Tone(0.05f, Osc.Sine, 300f, 200f, 0.14f, noise: 1f, lowpass: 0.6f, decay: 3f), gap: 0.045f);
            Set(Sfx.Hurt, Tone(0.18f, Osc.Square, 220f, 90f, 0.3f, lowpass: 0.4f), gap: 0.1f);
            Set(Sfx.Explode, Tone(0.4f, Osc.Sine, 120f, 40f, 0.45f, noise: 0.8f, lowpass: 0.2f), gap: 0.05f);
            Set(Sfx.BigExplode, Tone(1.2f, Osc.Sine, 80f, 30f, 0.7f, noise: 0.7f, lowpass: 0.1f, decay: 1.5f));
            Set(Sfx.Ability, Tone(0.25f, Osc.Sine, 300f, 900f, 0.3f, attack: 0.02f));
            Set(Sfx.Shockwave, Tone(0.5f, Osc.Saw, 200f, 50f, 0.4f, noise: 0.4f, lowpass: 0.3f));
            Set(Sfx.Blink, Tone(0.15f, Osc.Sine, 1200f, 400f, 0.25f));
            Set(Sfx.Pick, Tone(0.08f, Osc.Sine, 660f, 990f, 0.25f));
            Set(Sfx.LevelUp, Notes(new[] { 523f, 659f, 784f, 1047f }, 0.08f, Osc.Square, 0.2f, 0.5f));
            Set(Sfx.WaveStart, Notes(new[] { 392f, 523f }, 0.15f, Osc.Saw, 0.25f, 0.3f));
            Set(Sfx.WaveClear, Notes(new[] { 523f, 659f, 784f }, 0.1f, Osc.Triangle, 0.3f, 1f));
            Set(Sfx.GameOver, Notes(new[] { 392f, 330f, 262f, 196f }, 0.22f, Osc.Square, 0.3f, 0.3f));
            Set(Sfx.Downed, Tone(0.4f, Osc.Square, 400f, 100f, 0.3f, lowpass: 0.3f));
            Set(Sfx.Revive, Tone(0.3f, Osc.Sine, 300f, 800f, 0.3f, attack: 0.02f));
        }

        void Set(Sfx s, float[] samples, float gap = 0f)
        {
            var clip = AudioClip.Create(s.ToString(), samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            clips[(int)s] = clip;
            minGap[(int)s] = gap;
        }

        public void Play(Sfx s, float now)
        {
            int i = (int)s;
            if (Muted || i <= 0 || i >= count || clips[i] == null || now - lastPlayed[i] < minGap[i])
                return;
            lastPlayed[i] = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.1f;
            voice.PlayOneShot(clips[i], volume[i]);
        }

        // --- Synthesis ---------------------------------------------------------

        /// A single sweep from f0 to f1 Hz with an attack/decay envelope.
        /// noise mixes in white noise (0..1); lowpass smooths the result (1 = off).
        float[] Tone(float dur, Osc osc, float f0, float f1, float vol,
                     float noise = 0f, float lowpass = 1f, float attack = 0.003f, float decay = 2f)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            double phase = 0;
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x;
                switch (osc)
                {
                    case Osc.Square: x = p < 0.5f ? 1f : -1f; break;
                    case Osc.Triangle: x = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                    case Osc.Saw: x = 2f * p - 1f; break;
                    default: x = Mathf.Sin(2f * Mathf.PI * p); break;
                }
                if (noise > 0f)
                    x = x * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                y += lowpass * (x - y);
                float env = Mathf.Min(1f, t / attack) * Mathf.Pow(1f - u, decay);
                data[i] = y * env * vol;
            }
            return data;
        }

        float[] Notes(float[] freqs, float noteDur, Osc osc, float vol, float lowpass)
        {
            int per = (int)(noteDur * Rate);
            var data = new float[per * freqs.Length];
            for (int k = 0; k < freqs.Length; k++)
            {
                var note = Tone(noteDur, osc, freqs[k], freqs[k], vol, lowpass: lowpass, decay: 1f);
                Array.Copy(note, 0, data, k * per, Math.Min(note.Length, per));
            }
            return data;
        }
    }
}
