using UnityEngine;

namespace Arena
{
    /// Footsteps, the Servo Tap (option D of Docs/Mockups/footstep-sounds.html): a small digital tick over a short low pulse.
    /// Two layers so the tick can brighten with speed; not Sfx (local only, worked out from positions in Footsteps.cs).
    /// On the Effects volume.
    public partial class SoundBank
    {
        AudioClip[] stepThump, stepTick;
        const float StepLevel = 0.85f;  // overall footstep loudness (1 = the mockup's level)

        void BuildFootsteps()
        {
            stepThump = new AudioClip[3];
            stepTick = new AudioClip[3];
            for (int v = 0; v < 3; v++)
            {
                float d = 1f + (v - 1) * 0.03f;  // three slightly different takes
                stepThump[v] = Clip("step_thump_" + v, Tone(0.05f, Osc.Sine, 140f * d, 70f * d, 0.3f, attack: 0.002f, decay: 2f));
                stepTick[v] = Clip("step_tick_" + v, Tone(0.014f, Osc.Triangle, 2400f * d, 1600f * d, 0.09f, attack: 0.001f, decay: 1.5f));
            }
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// One footstep at `speed` px/s: louder and brighter the faster you go (DPS speed 240 = full). gain: 1 for your own
        /// player, less for an ally further away. foot: +1 / -1, the left foot a touch lower.
        public void PlayFootstep(float speed, float gain, int foot)
        {
            if (Muted || stepThump == null)
                return;
            float k = Mathf.Min(speed / 240f, 1.5f);
            float g = (0.18f + 0.82f * Mathf.Pow(Mathf.Min(k, 1.35f), 1.3f)) * gain * StepLevel * Master * Effects;
            if (g <= 0.001f)
                return;
            float bright = Mathf.Min(0.65f + 0.55f * k, 1.2f);
            float r = (foot > 0 ? 1f : 0.965f) * (0.97f + (float)rng.NextDouble() * 0.06f);
            int take = rng.Next(3);
            StepVoice(stepThump[take], r, g);
            StepVoice(stepTick[take], r * bright, g);
        }

        void StepVoice(AudioClip clip, float pitch, float volume)
        {
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }
    }
}
