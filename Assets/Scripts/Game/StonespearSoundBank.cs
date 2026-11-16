using UnityEngine;

namespace Arena
{
    /// The Stonespear's sounds, the Basalt set of Docs/Mockups/stonespear-sounds.html (deep and heavy, no crackle: the rubble is a soft low rumble):
    /// They aren't Sfx (that byte enum is full): each screen plays them from what it sees (StonespearLook.cs SsSound), like the Nova's.
    ///   Throw  a spear thrown, every spear of a volley too: a soft airy whoosh and a low push.
    ///   Wind   the slam's 0.3 s wind-up: a low rumble and a hiss of dust swelling in.
    ///   Slam   the impact and the six spears tearing out of the ground behind it (35 ms apart, as they rise).
    ///   Hit    a spear striking an enemy and crumbling: a dull thud and rubble settling.
    ///   Stick  a missed spear driving into the ground: a short dull thump.
    /// Throw, Wind and Slam use the Weapons volume, Hit and Stick the Effects volume.
    public partial class SoundBank
    {
        public enum StoneCue { Throw, Wind, Slam, Hit, Stick }

        AudioClip[] stoneCues;
        readonly float[] stoneCueLast = { -10f, -10f, -10f, -10f, -10f };

        /// One of the Stonespear's sounds (ally: someone else's, quieter; quiet 0..1: further away).
        public void PlayStoneCue(StoneCue cue, bool ally, float quiet, float now)
        {
            int i = (int)cue;
            if (Muted || stoneCues == null || now - stoneCueLast[i] < 0.03f)
                return;
            float volume = Master * (cue <= StoneCue.Slam ? Weapons : Effects) * (ally ? 0.45f : 1f) * (1f - quiet);
            if (volume <= 0.001f)
                return;
            stoneCueLast[i] = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.06f;
            voice.PlayOneShot(stoneCues[i], volume);
        }

        /// Soft rubble: dark, low-passed noise settling (no clicks).
        float[] StoneRubble(float dur) => Tone(dur, Osc.Sine, 260f, 120f, 0.35f, noise: 1f, lowpass: 0.06f, attack: 0.01f, decay: 2f);

        void BuildStonespear()
        {
            const Osc S = Osc.Sine;
            var wind = Finish(Mix(
                Reverse(Tone(0.32f, S, 70f, 45f, 0.5f, noise: 0.5f, lowpass: 0.08f, decay: 2f)),
                Reverse(Tone(0.3f, S, 400f, 200f, 0.12f, noise: 1f, lowpass: 0.25f, decay: 1.5f)),
                Delay(StoneRubble(0.25f), 0.05f)), 0.6f);
            var slam = Room(Mix(
                Tone(0.55f, S, 90f, 32f, 1f, noise: 0.2f, lowpass: 0.15f, decay: 2.2f),
                Tone(0.12f, S, 900f, 300f, 0.35f, noise: 1f, lowpass: 0.5f, attack: 0.001f, decay: 3f),
                Delay(StoneRubble(0.6f), 0.02f),
                Delay(Tone(0.7f, S, 300f, 150f, 0.18f, noise: 1f, lowpass: 0.12f, attack: 0.05f, decay: 1.6f), 0.05f)), 0.25f);
            var rise = new float[6 * 2][];
            for (int k = 0; k < 6; k++)  // the spears tearing out, a low grind each
            {
                float f = 140f * (1f + 0.06f * (k % 3));
                rise[2 * k] = Delay(Tremolo(Tone(0.35f, S, f, f * 1.6f, 0.12f, noise: 0.5f, lowpass: 0.08f, attack: 0.03f, decay: 1.6f), 18f, 0.3f), 0.035f * k);
                rise[2 * k + 1] = Delay(StoneRubble(0.08f), 0.035f * k + 0.02f);
            }
            var riseMix = Finish(Room(Mix(rise), 0.15f), 0.6f);
            var slamAll = Finish(Mix(Finish(slam, 0.9f), riseMix), 0.9f);
            var thr = Finish(Mix(
                Tone(0.28f, S, 600f, 180f, 0.4f, noise: 1f, lowpass: 0.18f, attack: 0.04f, decay: 1.8f),
                Tone(0.08f, S, 160f, 90f, 0.4f, noise: 0.3f, lowpass: 0.2f, decay: 2.5f)), 0.55f);
            var hit = Finish(Mix(
                Tone(0.18f, S, 130f, 60f, 0.8f, noise: 0.4f, lowpass: 0.2f, attack: 0.001f, decay: 2.5f),
                Delay(StoneRubble(0.3f), 0.01f),
                Tone(0.25f, S, 500f, 250f, 0.2f, noise: 1f, lowpass: 0.15f, decay: 2f)), 0.7f);
            var stick = Finish(Mix(
                Tone(0.14f, S, 110f, 70f, 0.8f, noise: 0.5f, lowpass: 0.12f, attack: 0.001f, decay: 3f),
                Delay(StoneRubble(0.2f), 0.03f)), 0.45f);
            var data = new[] { thr, wind, slamAll, hit, stick };
            stoneCues = new AudioClip[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                stoneCues[i] = AudioClip.Create("stone_cue_" + i, data[i].Length, 1, Rate, false);
                stoneCues[i].SetData(data[i], 0);
            }
        }
    }
}
