using UnityEngine;

namespace Arena
{
    /// The menu background dancing to the music: each frame reads the playing track's spectrum (SoundBank.MusicSpectrum) and turns
    /// it into what the Holo Floor (MenuHolo.cs BgHoloFloor) draws:
    ///   beatBands   BeatBandCount bands, bass first, 0..1 (eased; normalised to the track's own recent loudness, so quiet and
    ///               loud tracks, and any volume setting, move the same)
    ///   BeatPulse   1 on a kick or bass hit, fading over about half a second
    ///   beatWaves   when the last few beats landed (a bright line runs down the floor from each)
    /// Works on any track (the generated ones and your own); with no music, paused, muted, effects off or the background STILL,
    /// everything rests at 0 and the floor looks as it always did. Local only.
    public partial class ArenaApp
    {
        public const int BeatBandCount = 24;
        const int BeatFft = 512;          // bins of ~43 Hz at 44.1 kHz
        const float BeatWaveLife = 1.1f;  // seconds for a beat's line to reach you

        readonly float[] beatSpec = new float[BeatFft];
        readonly float[] beatBands = new float[BeatBandCount];
        readonly float[] beatRaw = new float[BeatBandCount];
        readonly float[] beatWaves = { -100f, -100f, -100f, -100f };  // birth times (Time.unscaledTime); long ago = none
        int beatWaveNext;
        float beatPulse, beatPeak = 1e-3f, bassAvg, bassPeak = 1e-3f, lastBeatAt = -10f;
        bool beatFrozen;  // previews: keep the values set from outside

        /// How strong the reaction is: 0 with effects off or the background STILL, half on Reduced effects.
        float BeatAmt => !fxOn || !menuBgAnimate ? 0f : fxReduced ? 0.5f : 1f;
        float BeatPulse => beatPulse * BeatAmt;
        float BeatBand(int i) => beatBands[i] * BeatAmt;

        /// Once a frame (from ReactTick): the spectrum into bands, and a beat when the bass jumps above its running average.
        void BeatTick()
        {
            if (beatFrozen)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f), now = Time.unscaledTime;
            beatPulse *= Mathf.Exp(-4.5f * dt);
            bool live = sounds != null && BeatAmt > 0f && sounds.MusicSpectrum(beatSpec);
            float fall = Mathf.Exp(-5f * dt);
            if (!live)
            {
                for (int i = 0; i < BeatBandCount; i++)
                    beatBands[i] *= fall;
                return;
            }

            // Bands spaced evenly in pitch from ~43 Hz to ~11 kHz (bins 1..256), each the mean of its bins.
            float loudest = 0f;
            for (int i = 0; i < BeatBandCount; i++)
            {
                int b0 = BandBin(i), b1 = Mathf.Max(b0 + 1, BandBin(i + 1));
                float sum = 0f;
                for (int b = b0; b < b1; b++)
                    sum += beatSpec[b];
                float v = sum / (b1 - b0) * (1f + i * 0.18f);  // tilt: highs carry less energy, lift them so the right side moves too
                beatRaw[i] = v;
                loudest = Mathf.Max(loudest, v);
            }
            beatPeak = Mathf.Max(1e-4f, Mathf.Max(loudest, beatPeak * Mathf.Exp(-0.25f * dt)));  // the track's recent peak, falling slowly
            for (int i = 0; i < BeatBandCount; i++)
            {
                float v = Mathf.Sqrt(Mathf.Clamp01(beatRaw[i] / beatPeak));
                beatBands[i] = v > beatBands[i] ? Mathf.Lerp(beatBands[i], v, 0.7f) : Mathf.Max(v, beatBands[i] * fall);
            }

            // A beat: the bass (bins 1..4, ~40-200 Hz) well above its last half second's average.
            float bass = beatSpec[1] + beatSpec[2] + beatSpec[3] + beatSpec[4];
            bassPeak = Mathf.Max(1e-4f, Mathf.Max(bass, bassPeak * Mathf.Exp(-0.3f * dt)));
            bool hit = bass > bassAvg * 1.35f && bass > bassPeak * 0.3f && now - lastBeatAt > 0.24f;
            bassAvg = Mathf.Lerp(bassAvg, bass, 1f - Mathf.Exp(-dt / 0.5f));
            if (hit)
            {
                lastBeatAt = now;
                beatPulse = Mathf.Max(beatPulse, Mathf.Clamp01(bass / bassPeak));
                beatWaves[beatWaveNext] = now;
                beatWaveNext = (beatWaveNext + 1) % beatWaves.Length;
            }
        }

        static int BandBin(int i) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(256f, i / (float)BeatBandCount)), 1, 256);

        /// Beat line k's age in seconds, false for an empty or finished one.
        bool BeatWave(int k, float now, out float age)
        {
            age = now - beatWaves[k];
            return age >= 0f && age < BeatWaveLife && BeatAmt > 0f;
        }
    }
}
