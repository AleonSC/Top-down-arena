using UnityEngine;

namespace Arena
{
    /// The Lightbringer's sounds (cues raised in Core/Lightbringer.cs and Core/Combat.cs), a healer's sword: warm airy cuts with a
    /// clean major-key chime instead of a steel ring, a sunrise swell into a struck chord for Noon, sparkles when a crescent heals,
    /// a glassy three-note arpeggio when it wards, and the Sunlance's gathering shimmer and bright release. Built like the Twin
    /// Blades' bank (TwinSoundBank.cs). The cuts, a cut landing, Noon and the release use the Weapons volume (IsLightWeaponSfx).
    public partial class SoundBank
    {
        // A major chord on C6 (and its octave): the sword's voice.
        const float LightC = 1046.5f, LightE = 1318.5f, LightG = 1568f, LightRoom = 0.25f;

        static bool IsLightWeaponSfx(Sfx s)
        {
            return s == Sfx.LightCut1 || s == Sfx.LightCut2 || s == Sfx.LightNoon || s == Sfx.LightRadiant || s == Sfx.LightHit || s == Sfx.LanceRelease;
        }

        /// A struck chime: harmonic partials (unlike Bell's inharmonic ones), so it rings sweet rather than churchy.
        static float[] Chime(float dur, float f, float vol)
        {
            float[] ratio = { 1f, 2f, 3f, 4.01f }, amp = { 1f, 0.35f, 0.14f, 0.06f }, life = { 1f, 0.55f, 0.3f, 0.18f };
            int n = (int)(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = (float)i / n, x = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    x += amp[k] * Mathf.Sin(2f * Mathf.PI * f * ratio[k] * t) * Mathf.Pow(Mathf.Max(0f, 1f - u / life[k]), 2f);
                data[i] = x * vol * Mathf.Min(1f, t / 0.003f);
            }
            return data;
        }

        /// Sparkle: `count` tiny high chimes scattered over `dur`, fading as they go (light motes).
        float[] Sparkle(float dur, int count, float vol, float lo = 2600f, float hi = 5200f)
        {
            var layers = new float[count][];
            for (int k = 0; k < count; k++)
            {
                float at = dur * k / count + (float)rng.NextDouble() * dur / count * 0.8f;
                float f = lo + (float)rng.NextDouble() * (hi - lo);
                layers[k] = Delay(Chime(0.12f, f, vol * (1f - 0.7f * k / count)), at);
            }
            return Mix(layers);
        }

        /// A soft pad on one note: three slightly detuned sines that swell in (a choir-like breath under the chord).
        float[] Pad(float dur, float f, float vol, float attack)
        {
            const Osc S = Osc.Sine;
            return Mix(
                Tone(dur, S, f, f, vol, attack: attack, decay: 1.2f),
                Tone(dur, S, f * 1.004f, f * 1.004f, vol * 0.7f, attack: attack, decay: 1.2f),
                Tone(dur, S, f * 0.996f, f * 0.996f, vol * 0.7f, attack: attack, decay: 1.2f));
        }

        void BuildLight()
        {
            const Osc S = Osc.Sine;
            // Dawn Cut: an airy rush sweeping up and a rising two-note chime (G then C). Mercy Cut sweeps down and chimes softer (E, C).
            Set(Sfx.LightCut1, Finish(Mix(
                Tone(0.16f, S, 340f, 1000f, 0.15f, noise: 0.8f, lowpass: 0.5f, attack: 0.02f, decay: 1.7f),
                Delay(Chime(0.3f, LightG, 0.05f), 0.03f),
                Delay(Chime(0.3f, LightC * 2f, 0.04f), 0.08f)), 0.42f), 0.04f);
            Set(Sfx.LightCut2, Finish(Mix(
                Tone(0.16f, S, 1000f, 340f, 0.15f, noise: 0.8f, lowpass: 0.5f, attack: 0.02f, decay: 1.7f),
                Delay(Chime(0.3f, LightE, 0.045f), 0.03f),
                Delay(Chime(0.32f, LightC, 0.045f), 0.08f)), 0.4f), 0.04f);
            // Noon: a rising swell while the sword goes up (0.34 s, up to the blow), then a deep thump, the struck chord and a
            // shimmer that falls away. Radiant: the same with an octave below, the high C on top and a longer, wider room.
            Set(Sfx.LightNoon, Finish(Room(Mix(
                Reverse(Tone(0.34f, S, 900f, 300f, 0.07f, noise: 0.6f, lowpass: 0.35f, decay: 1.4f)),
                Pad(0.34f, LightC * 0.5f, 0.02f, 0.3f),
                Delay(Tone(0.22f, S, 150f, 55f, 0.32f, noise: 0.15f, lowpass: 0.25f, decay: 2.2f), 0.34f),
                Delay(Tone(0.06f, S, 2600f, 900f, 0.14f, noise: 0.9f, lowpass: 0.8f, decay: 3f), 0.34f),
                Delay(Chime(0.9f, LightC, 0.06f), 0.345f),
                Delay(Chime(0.85f, LightE, 0.045f), 0.35f),
                Delay(Chime(0.8f, LightG, 0.04f), 0.355f),
                Delay(Sparkle(0.5f, 7, 0.018f), 0.38f)), LightRoom), 0.62f), 0.2f);
            Set(Sfx.LightRadiant, Finish(Room(Mix(
                Reverse(Tone(0.34f, S, 1200f, 300f, 0.09f, noise: 0.6f, lowpass: 0.35f, decay: 1.2f)),
                Pad(0.34f, LightC * 0.5f, 0.03f, 0.25f),
                Pad(0.34f, LightG * 0.5f, 0.02f, 0.25f),
                Delay(Tone(0.3f, S, 120f, 40f, 0.38f, noise: 0.15f, lowpass: 0.22f, decay: 2f), 0.34f),
                Delay(Tone(0.08f, S, 3200f, 900f, 0.16f, noise: 0.9f, lowpass: 0.85f, decay: 3f), 0.34f),
                Delay(Chime(1.3f, LightC * 0.5f, 0.05f), 0.345f),
                Delay(Chime(1.2f, LightC, 0.06f), 0.345f),
                Delay(Chime(1.1f, LightE, 0.05f), 0.35f),
                Delay(Chime(1.1f, LightG, 0.045f), 0.355f),
                Delay(Chime(1f, LightC * 2f, 0.035f), 0.36f),
                Delay(Pad(0.9f, LightC, 0.015f, 0.05f), 0.36f),
                Delay(Sparkle(0.9f, 14, 0.02f), 0.38f)), LightRoom * 1.6f), 0.7f), 0.2f);
            // A cut lands: a soft slice and a small high chime, so it reads as light rather than steel.
            Set(Sfx.LightHit, Finish(Mix(
                Tone(0.06f, S, 2000f, 700f, 0.16f, noise: 0.9f, lowpass: 0.8f, decay: 3f),
                Tone(0.09f, S, 240f, 120f, 0.16f, noise: 0.3f, lowpass: 0.3f, decay: 3f),
                Chime(0.2f, LightG * 2f, 0.03f)), 0.32f), 0.04f);
            // A crescent heals an ally: two quick rising sparkly notes. Wards them: a glassy three-note arpeggio over a hush.
            Set(Sfx.LightHeal, Finish(Mix(
                Chime(0.25f, LightE * 2f, 0.05f),
                Delay(Chime(0.3f, LightG * 2f, 0.05f), 0.06f),
                Sparkle(0.25f, 4, 0.015f, 3500f, 6000f)), 0.3f), 0.08f);
            Set(Sfx.LightWard, Finish(Room(Mix(
                Tone(0.3f, S, 2000f, 3000f, 0.03f, noise: 0.9f, lowpass: 0.3f, attack: 0.08f, decay: 1.5f),
                Chime(0.4f, LightC * 2f, 0.05f),
                Delay(Chime(0.4f, LightE * 2f, 0.045f), 0.05f),
                Delay(Chime(0.45f, LightG * 2f, 0.045f), 0.1f)), LightRoom * 0.6f), 0.32f), 0.08f);
            // The Sunlance: light gathering in the off hand (a rising shimmer up to the thrust, 0.36 s), then the release: a bright
            // zap, a rush down the line and the chord, with motes trailing after.
            Set(Sfx.LanceGather, Finish(Mix(
                Reverse(Tone(0.36f, S, 1600f, 500f, 0.06f, noise: 0.5f, lowpass: 0.4f, decay: 1.4f)),
                Tone(0.36f, S, 520f, 1040f, 0.04f, attack: 0.25f, decay: 0.4f),
                Sparkle(0.34f, 8, 0.016f)), 0.38f), 0.2f);
            Set(Sfx.LanceRelease, Finish(Room(Mix(
                Tone(0.1f, Osc.Saw, 2600f, 900f, 0.08f, lowpass: 0.35f, decay: 2.5f),
                Tone(0.3f, S, 1800f, 300f, 0.14f, noise: 0.85f, lowpass: 0.6f, attack: 0.004f, decay: 1.8f),
                Delay(Chime(0.7f, LightC, 0.05f), 0.02f),
                Delay(Chime(0.65f, LightG, 0.04f), 0.03f),
                Delay(Chime(0.6f, LightE * 2f, 0.03f), 0.05f),
                Delay(Sparkle(0.45f, 9, 0.018f), 0.08f)), LightRoom), 0.55f), 0.15f);
            // Your own key with too little light, or no ally in reach: a muted, falling blip.
            Set(Sfx.LanceFizzle, Finish(Tone(0.1f, Osc.Square, 320f, 180f, 0.06f, lowpass: 0.25f, decay: 2.5f), 0.22f), 0.15f);
        }
    }
}
