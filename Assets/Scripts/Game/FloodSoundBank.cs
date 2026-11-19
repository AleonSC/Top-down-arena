using UnityEngine;

namespace Arena
{
    /// Flood's sounds (the Tank Elementalist's 4th choice, Core/Flood.cs). Each game plays them from the pool's own clock
    /// (FloodLook.cs `UpdateFloodSound`), so nothing is sent:
    ///   FloodCast   the stamp: a low thud, a splash bursting off it, the wave crashing outward and spray falling back
    ///   FloodLap    water lapping while it floods: a soft low slosh with a couple of drips, repeated
    ///   FloodDrain  the pool running away: a throaty gurgle sinking in pitch, with bubbles
    /// On the Effects volume; quieter for an ally's flood and with distance.
    public partial class SoundBank
    {
        /// Water drops: `count` short sine "plips" (a fast upward sweep) scattered over `dur`.
        float[] Plips(float dur, int count, float vol, float lo, float hi)
        {
            var layers = new float[count][];
            for (int k = 0; k < count; k++)
            {
                float at = dur * k / count + (float)rng.NextDouble() * dur / count * 0.8f;
                float f = lo + (float)rng.NextDouble() * (hi - lo);
                layers[k] = Delay(Tone(0.06f, Osc.Sine, f, f * 2.6f, vol * (1f - 0.5f * k / count), attack: 0.002f, decay: 2.5f), at);
            }
            return Mix(layers);
        }

        void BuildFlood()
        {
            const Osc S = Osc.Sine;
            var cast = Finish(Mix(
                Tone(0.35f, S, 110f, 42f, 0.7f, attack: 0.002f, decay: 3f),                                       // the stamp
                Tone(0.5f, S, 3200f, 700f, 0.35f, noise: 1f, lowpass: 0.35f, attack: 0.004f, decay: 2.6f),        // splash
                Delay(Tone(0.95f, S, 700f, 180f, 0.32f, noise: 1f, lowpass: 0.1f, attack: 0.22f, decay: 1.4f), 0.08f),  // the wave crashing out
                Delay(Plips(0.6f, 9, 0.1f, 700f, 1600f), 0.25f)), 0.62f);                                          // spray falling back
            var lap = Finish(Mix(
                Tone(0.8f, S, 320f, 170f, 0.2f, noise: 1f, lowpass: 0.05f, attack: 0.25f, decay: 1.6f),            // slosh
                Delay(Plips(0.5f, 2, 0.08f, 500f, 1100f), 0.15f)), 0.22f);
            var drain = Finish(Mix(
                Tremolo(Tone(0.9f, S, 380f, 120f, 0.3f, noise: 0.35f, lowpass: 0.18f, attack: 0.03f, decay: 1.3f), 13f, 0.75f),  // gurgle
                Plips(0.75f, 6, 0.09f, 260f, 700f)), 0.4f);                                                         // bubbles
            Set(Sfx.FloodCast, cast, 0.05f);
            Set(Sfx.FloodLap, lap, 0.05f);
            Set(Sfx.FloodDrain, drain, 0.05f);
            Jitter(0.06f, Sfx.FloodCast, Sfx.FloodLap, Sfx.FloodDrain);
        }
    }
}
