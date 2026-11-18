namespace Arena
{
    /// The Frost Lance's sounds (Core/FrostLance.cs), sent by the host with the effects. One cold voice for all of them: glassy chimes
    /// on an E minor chord (E6, G6, B6) over airy rushes, with ice that crackles, crunches and shatters.
    ///   FrostCrescent  Shiver Crescent: an airy rush sweeping up, a glassy shimmer riding it, a chime (E then B) as it cuts
    ///   FrostBackhand  Backhand Rime: the rush sweeping down, a softer chime (G, E), a hiss of rime settling on the floor
    ///   FrostWaltz     Frost Waltz: a rush that wheels round (tremolo), a rising run of three chimes, glitter flung off the end
    ///   FrostFan       Crystal Fan: the point striking the floor (a low thump), five crystals cracking up out of it one after another,
    ///                  each a crunch and a high ping, the centre first
    ///   FrostSlamWind  Avalanche Slam, its swing: a deep rush rising as the lance goes up and over (it lands FlHitAt later)
    ///   FrostSlam      the slam landing: a heavy thud, ice crashing, a ring of spikes cracking up, glitter raining back
    ///   FrostFreeze    an enemy freezing: a creaking crackle tightening up in pitch, a glassy "tink" as it locks
    ///   FrostShatter   a shatter: a bright crack, a burst of glass shards tinkling out, a low knock under it
    ///   FrostBreak     a parry breaking the lance: a short brittle snap and a spray of small shards
    ///   ZeroCharge     Absolute Zero's charge: a cold breath swelling, a shimmer of chimes gathering, a choir-like pad on E rising
    ///                  to the wave (ZeroWaveAt in)
    ///   ZeroWave       the white wave: a soft boom, a wide hiss sweeping out, the whole chord struck at once, frost crackling after
    ///   ZeroShatter    the second wave: a cascade of shattering glass over a falling chime run
    /// The chain's steps, the slam and the break are on the Weapons volume; the freezes, shatters and Absolute Zero on Effects.
    public partial class SoundBank
    {
        const float FrE = 1318.5f, FrG = 1568f, FrB = 1975.5f;

        /// Glass breaking: `count` tiny bright pings (triangle, falling a little) scattered over `dur`, fading as they go.
        float[] Glass(float dur, int count, float vol, float lo = 2400f, float hi = 7000f)
        {
            var layers = new float[count][];
            for (int k = 0; k < count; k++)
            {
                float at = dur * k / count * (0.4f + 0.6f * (float)rng.NextDouble());
                float f = lo + (float)rng.NextDouble() * (hi - lo);
                layers[k] = Delay(Tone(0.07f + 0.08f * (float)rng.NextDouble(), Osc.Triangle, f, f * 0.93f, vol * (1f - 0.6f * k / count), attack: 0.001f, decay: 4f), at);
            }
            return Mix(layers);
        }

        /// Ice creaking: short noisy clicks getting quicker and higher over `dur`.
        float[] Crackle(float dur, int count, float vol, float lo, float hi)
        {
            var layers = new float[count][];
            for (int k = 0; k < count; k++)
            {
                float u = k / (float)count, at = dur * (1f - (1f - u) * (1f - u));
                float f = lo + (hi - lo) * u;
                layers[k] = Delay(Tone(0.018f, Osc.Sine, f, f * 0.7f, vol * (0.6f + 0.4f * (float)rng.NextDouble()), noise: 0.8f, lowpass: 0.6f, attack: 0.001f, decay: 3f), at);
            }
            return Mix(layers);
        }

        /// An airy rush: filtered noise swelling and sweeping from f0 to f1.
        float[] Rush(float dur, float f0, float f1, float vol, float attack) =>
            Tone(dur, Osc.Sine, f0, f1, vol, noise: 1f, lowpass: 0.12f, attack: attack, decay: 1.8f);

        void BuildFrost()
        {
            const Osc S = Osc.Sine;
            var crescent = Finish(Mix(
                Rush(0.32f, 700f, 2600f, 0.42f, 0.05f),                                                            // the sweep
                Tremolo(Tone(0.3f, S, FrB * 1.5f, FrB * 2f, 0.05f, attack: 0.04f, decay: 2f), 17f, 0.6f),          // shimmer riding it
                Delay(Chime(0.35f, FrE, 0.16f), 0.07f), Delay(Chime(0.3f, FrB, 0.12f), 0.11f),                     // the cut's chime
                Delay(Glass(0.2f, 5, 0.05f), 0.09f)), 0.5f);
            var backhand = Finish(Mix(
                Rush(0.32f, 2400f, 650f, 0.42f, 0.04f),
                Delay(Chime(0.35f, FrG, 0.13f), 0.07f), Delay(Chime(0.32f, FrE, 0.1f), 0.11f),
                Delay(Tone(0.5f, S, 5000f, 3000f, 0.06f, noise: 1f, lowpass: 0.5f, attack: 0.08f, decay: 2f), 0.1f)), 0.48f);  // rime settling
            var waltz = Finish(Mix(
                Tremolo(Rush(0.42f, 600f, 1900f, 0.48f, 0.06f), 9f, 0.55f),                                        // wheeling round
                Delay(Chime(0.3f, FrE, 0.11f), 0.08f), Delay(Chime(0.3f, FrG, 0.11f), 0.15f), Delay(Chime(0.4f, FrB, 0.13f), 0.22f),
                Delay(Glass(0.3f, 9, 0.05f), 0.3f)), 0.52f);                                                      // glitter flung off
            var fanLayers = new float[12][];
            fanLayers[0] = Tone(0.18f, S, 140f, 60f, 0.55f, attack: 0.002f, decay: 3f);                               // the point strikes the floor
            fanLayers[1] = Tone(0.12f, S, 2500f, 900f, 0.2f, noise: 1f, lowpass: 0.4f, attack: 0.002f, decay: 3f);
            for (int i = 0; i < 5; i++)
            {
                float at = 0.06f + System.Math.Abs(i - 2) * 0.03f + i * 0.004f, f = new[] { FrE, FrG, FrB, FrG * 2f, FrE * 2f }[i];
                fanLayers[2 + i] = Delay(Tone(0.09f, S, 1800f, 500f, 0.22f, noise: 0.9f, lowpass: 0.35f, attack: 0.002f, decay: 3f), at);  // crunch
                fanLayers[7 + i] = Delay(Chime(0.25f, f, 0.07f), at + 0.01f);                                      // ping
            }
            var fan = Finish(Mix(fanLayers), 0.55f);
            float land = Game.FlHitAt(4, 0);
            var slamWind = Finish(Mix(
                Rush(land + 0.05f, 220f, 1400f, 0.5f, land * 0.8f),                                               // up and over
                Tone(land, S, 90f, 180f, 0.12f, attack: land * 0.7f, decay: 0.6f)), 0.5f);
            var slam = Finish(Mix(
                Tone(0.45f, S, 95f, 38f, 0.8f, attack: 0.002f, decay: 2.6f),                                       // the thud
                Tone(0.4f, S, 3800f, 900f, 0.4f, noise: 1f, lowpass: 0.45f, attack: 0.002f, decay: 2.8f),          // ice crashing
                Delay(Crackle(0.12f, 9, 0.18f, 900f, 2400f), 0.01f),                                              // spikes cracking up
                Delay(Chime(0.6f, FrE * 0.5f, 0.14f), 0.01f), Delay(Chime(0.5f, FrB * 0.5f, 0.1f), 0.02f),
                Delay(Glass(0.6f, 14, 0.06f), 0.08f)), 0.66f);                                                     // glitter raining back
            var freeze = Finish(Mix(
                Crackle(0.26f, 12, 0.2f, 700f, 2600f),                                                            // creaking tighter
                Tone(0.3f, S, 4500f, 6500f, 0.07f, noise: 1f, lowpass: 0.5f, attack: 0.15f, decay: 1.5f),
                Delay(Chime(0.35f, FrB * 2f, 0.1f), 0.25f)), 0.45f);                                               // locked: tink
            var shatter = Finish(Mix(
                Tone(0.05f, S, 6000f, 2000f, 0.5f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 3f),          // the crack
                Tone(0.25f, S, 180f, 70f, 0.4f, attack: 0.002f, decay: 3f),                                        // the knock
                Glass(0.45f, 18, 0.11f),                                                                           // shards
                Delay(Chime(0.4f, FrE * 2f, 0.07f), 0.02f)), 0.6f);
            var brk = Finish(Mix(
                Tone(0.04f, S, 5000f, 1800f, 0.4f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 3f),
                Glass(0.22f, 8, 0.08f, 2000f, 5500f)), 0.42f);
            float wave = Game.ZeroWaveAt, sh = Game.ZeroShatterAt - Game.ZeroWaveAt;
            var charge = Finish(Mix(
                Rush(wave, 300f, 1500f, 0.3f, wave * 0.85f),                                                       // a cold breath swelling
                Pad(wave, FrE * 0.25f, 0.1f, wave * 0.8f), Pad(wave, FrB * 0.25f, 0.07f, wave * 0.9f),             // the choir rising
                Sparkle(wave * 0.9f, 10, 0.05f, 3000f, 6000f)), 0.48f);
            var zeroWave = Finish(Mix(
                Tone(0.6f, S, 120f, 45f, 0.6f, attack: 0.004f, decay: 2.2f),                                       // the boom
                Tone(0.9f, S, 5000f, 1200f, 0.32f, noise: 1f, lowpass: 0.3f, attack: 0.01f, decay: 1.6f),          // the wave hissing out
                Chime(1.1f, FrE, 0.12f), Chime(1.1f, FrG, 0.1f), Chime(1.1f, FrB, 0.1f), Chime(1.2f, FrE * 0.5f, 0.1f),  // the chord struck
                Delay(Crackle(0.5f, 14, 0.12f, 600f, 2000f), 0.15f)), 0.62f);                                      // frost crackling after
            var zeroShatter = Finish(Mix(
                Tone(0.06f, S, 6000f, 2000f, 0.5f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 3f),
                Tone(0.4f, S, 150f, 55f, 0.45f, attack: 0.002f, decay: 2.6f),
                Glass(0.9f, 30, 0.1f),
                Delay(Chime(0.35f, FrB * 2f, 0.08f), 0.02f), Delay(Chime(0.35f, FrG * 2f, 0.08f), 0.1f),            // a falling run
                Delay(Chime(0.4f, FrE * 2f, 0.08f), 0.18f), Delay(Chime(0.6f, FrB, 0.08f), 0.26f)), 0.64f);
            Set(Sfx.FrostCrescent, crescent, 0.05f);
            Set(Sfx.FrostBackhand, backhand, 0.05f);
            Set(Sfx.FrostWaltz, waltz, 0.05f);
            Set(Sfx.FrostFan, fan, 0.05f);
            Set(Sfx.FrostSlamWind, slamWind, 0.05f);
            Set(Sfx.FrostSlam, slam, 0.05f);
            Set(Sfx.FrostFreeze, freeze, 0.06f);
            Set(Sfx.FrostShatter, shatter, 0.05f);
            Set(Sfx.FrostBreak, brk, 0.05f);
            Set(Sfx.ZeroCharge, charge, 0.1f);
            Set(Sfx.ZeroWave, zeroWave, 0.1f);
            Set(Sfx.ZeroShatter, zeroShatter, 0.1f);
            Jitter(0.06f, Sfx.FrostCrescent, Sfx.FrostBackhand, Sfx.FrostWaltz, Sfx.FrostFan, Sfx.FrostFreeze, Sfx.FrostShatter, Sfx.FrostBreak);
            Jitter(0.03f, Sfx.FrostSlamWind, Sfx.FrostSlam, Sfx.ZeroCharge, Sfx.ZeroWave, Sfx.ZeroShatter);  // (kept close: they're timed to the look)
        }
    }
}
