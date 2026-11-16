using UnityEngine;

namespace Arena
{
    /// Bullet Rain's sounds (BulletRainLook.cs `UpdateBulletRainSound`). Each game plays them from the zone's clock, so nothing is sent.
    ///   RainCast    a rising rush and a low thunder thump as the downpour is called
    ///   RainPatter  half a second of needles hitting the ground: a hiss with a scatter of tiny ticks, repeated while it falls
    ///   RainEnd     the hiss thinning out, a few last drops
    /// On the Effects volume; quieter for an ally's rain and with distance.
    public partial class SoundBank
    {
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
            Set(Sfx.RainCast, cast, 0.05f);
            Set(Sfx.RainPatter, patter, 0.05f);
            Set(Sfx.RainEnd, end, 0.05f);
            Jitter(0.04f, Sfx.RainCast, Sfx.RainPatter, Sfx.RainEnd);
        }
    }
}
