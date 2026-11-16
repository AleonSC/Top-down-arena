using System;
using UnityEngine;

namespace Arena
{
    /// The hum of a player touching a wall (WallTouchLook.cs): a neon-tube 60 Hz hum with its harmonics and a slow
    /// throb, on its own looping voice, faded by SetWallHum each frame.
    public partial class SoundBank
    {
        AudioSource wallHumVoice;
        const float WallHumPeak = 0.22f;

        void BuildWallHum()
        {
            // Whole cycles in the loop (45 of 60 Hz, one throb of 4/3 Hz) so it joins up without a click.
            const float len = 0.75f;
            var hum = Tremolo(Mix(
                Tone(len, Osc.Sine, 60f, 60f, 1f, attack: 0.0005f, decay: 0f),
                Tone(len, Osc.Sine, 120f, 120f, 0.6f, attack: 0.0005f, decay: 0f),
                Tone(len, Osc.Sine, 180f, 180f, 0.25f, attack: 0.0005f, decay: 0f),
                Tone(len, Osc.Sine, 240f, 240f, 0.12f, attack: 0.0005f, decay: 0f)), 4f / 3f, 0.35f);
            float max = 0f;
            foreach (float v in hum)
                max = Math.Max(max, Math.Abs(v));
            for (int i = 0; i < hum.Length && max > 0f; i++)
                hum[i] *= WallHumPeak / max;
            Set(Sfx.WallHum, hum, 0f);
        }

        /// Fade the wall hum to `level` (0 = off, 1 = full), from ArenaApp.TickWallTouch each frame.
        public void SetWallHum(float level)
        {
            if (wallHumVoice == null)
                return;
            if (wallHumVoice.clip == null)
            {
                var c = ClipsOf(Sfx.WallHum);  // (on its own looping voice, made in the constructor)
                if (c == null)
                    return;
                wallHumVoice.clip = c[0];
            }
            float v = Muted ? 0f : level * Master * Effects;
            if (v <= 0.002f)
            {
                if (wallHumVoice.isPlaying)
                    wallHumVoice.Stop();
                return;
            }
            wallHumVoice.volume = Mathf.Min(1f, v);
            if (!wallHumVoice.isPlaying)
                wallHumVoice.Play();
        }
    }
}
