using UnityEngine;

namespace Arena
{
    /// Footsteps, the Servo Tap (option D of Docs/Mockups/footstep-sounds.html): a small digital tick over a short low pulse.
    /// Two layers (StepThump, StepTick) so the tick can brighten with speed; never sent (worked out from positions in Footsteps.cs).
    /// On the Effects volume.
    public partial class SoundBank
    {
        const float StepLevel = 0.85f;  // overall footstep loudness (1 = the mockup's level)

        void BuildFootsteps()
        {
            var thump = new float[3][];
            var tick = new float[3][];
            for (int v = 0; v < 3; v++)
            {
                float d = 1f + (v - 1) * 0.03f;  // three slightly different takes
                thump[v] = Tone(0.05f, Osc.Sine, 140f * d, 70f * d, 0.3f, attack: 0.002f, decay: 2f);
                tick[v] = Tone(0.014f, Osc.Triangle, 2400f * d, 1600f * d, 0.09f, attack: 0.001f, decay: 1.5f);
            }
            Set(Sfx.StepThump, thump, 0f);
            Set(Sfx.StepTick, tick, 0f);
        }

        /// One footstep at `speed` px/s: louder and brighter the faster you go (DPS speed 240 = full). gain: 1 for your own
        /// player, less for an ally further away. foot: +1 / -1, the left foot a touch lower.
        public void PlayFootstep(float speed, float gain, int foot)
        {
            float k = Mathf.Min(speed / 240f, 1.5f);
            float g = (0.18f + 0.82f * Mathf.Pow(Mathf.Min(k, 1.35f), 1.3f)) * gain * StepLevel;
            float bright = Mathf.Min(0.65f + 0.55f * k, 1.2f);
            float r = (foot > 0 ? 1f : 0.965f) * (0.97f + (float)playRng.NextDouble() * 0.06f);
            int take = playRng.Next(3);
            float now = Time.unscaledTime;
            Play(Sfx.StepThump, g, now, take, r);
            Play(Sfx.StepTick, g, now, take, r * bright);
        }
    }
}
