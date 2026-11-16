using UnityEngine;

namespace Arena
{
    /// Bullet Rain's sounds (BulletRainLook.cs `UpdateBulletRainSound`). Not Sfx (that byte enum is nearly full): each game
    /// plays them from the zone's clock, so nothing is sent.
    ///   RainCast    a rising rush and a low thunder thump as the downpour is called
    ///   RainPatter  half a second of needles hitting the ground: a hiss with a scatter of tiny ticks, repeated while it falls
    ///   RainEnd     the hiss thinning out, a few last drops
    /// On the Effects volume; quieter for an ally's rain and with distance.
    public partial class SoundBank
    {
        public enum RainCue { RainCast, RainPatter, RainEnd }

        AudioClip[] rainCues;
        readonly float[] rainCueLast = { -10f, -10f, -10f };

        void BuildBulletRain()
        {
            const Osc S = Osc.Sine, W = Osc.Saw;
            var cast = Finish(Mix(
                Tone(0.45f, S, 400f, 2400f, 0.2f, noise: 1f, lowpass: 0.3f, attack: 0.35f, decay: 0.8f),         // rising rush
                Delay(Tone(0.5f, S, 90f, 38f, 0.6f, attack: 0.003f, decay: 2.2f), 0.3f),                         // thunder thump
                Delay(Tone(0.4f, W, 1800f, 300f, 0.1f, noise: 0.7f, lowpass: 0.25f, attack: 0.002f, decay: 2.5f), 0.3f)), 0.6f);
            var patter = Finish(Mix(
                Tone(0.5f, S, 5000f, 4000f, 0.14f, noise: 1f, lowpass: 0.55f, attack: 0.05f, decay: 0.3f),       // the hiss
                Sparkle(0.5f, 14, 0.03f, 3000f, 7000f)), 0.4f);
            var end = Finish(Mix(
                Tone(0.6f, S, 4500f, 2500f, 0.12f, noise: 1f, lowpass: 0.4f, attack: 0.02f, decay: 1.6f),
                Sparkle(0.6f, 5, 0.03f, 3000f, 6000f)), 0.35f);
            rainCues = new AudioClip[3];
            float[][] data = { cast, patter, end };
            for (int i = 0; i < 3; i++)
            {
                rainCues[i] = AudioClip.Create("rain_cue_" + i, data[i].Length, 1, Rate, false);
                rainCues[i].SetData(data[i], 0);
            }
        }

        /// One of these cues (ally: someone else's rain, quieter; quiet 0..1: further away).
        public void PlayRainCue(RainCue cue, bool ally, float quiet, float now)
        {
            int i = (int)cue;
            if (Muted || rainCues == null || now - rainCueLast[i] < 0.05f)
                return;
            float volume = Master * Effects * (ally ? 0.5f : 1f) * (1f - quiet);
            if (volume <= 0.001f)
                return;
            rainCueLast[i] = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.04f;
            voice.PlayOneShot(rainCues[i], volume);
        }
    }
}
