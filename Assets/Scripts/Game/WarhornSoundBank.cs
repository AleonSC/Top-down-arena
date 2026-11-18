using System;

namespace Arena
{
    /// Warhorn Volley's sound (Core/WarhornVolley.cs), ported from the Fire Arrows synth of Docs/Mockups/warhorn-volley.html and
    /// sent with the cast (FxKind.WarhornVolley). One take, timed to the volley (Game.VolBlastT, VolLandT, VolEndT):
    ///   the war horn: one long, low, rasping blast that slides up a fifth, with breath in it and a thump as it starts
    ///   the volley: a hiss of shafts swelling in, whistles of arrows coming down, thuds as they land, and the fire crackling
    /// On the Effects volume. Render: Tools/SoundCheck/render.sh Warhorn
    public partial class SoundBank
    {
        void BuildWarhorn()
        {
            const Osc S = Osc.Sine, W = Osc.Saw;
            const float hornLen = 1.06f;
            // The horn: two detuned saws and a square an octave down, through a low-pass, saturated, with a slow wobble.
            var horn = Mix(
                Tone(hornLen, W, 58f, 87f, 0.5f, lowpass: 0.12f, attack: 0.06f, decay: 0.35f),
                Tone(hornLen, W, 58.3f, 87.4f, 0.4f, lowpass: 0.12f, attack: 0.06f, decay: 0.35f),
                Tone(hornLen, Osc.Square, 29f, 43.5f, 0.3f, lowpass: 0.08f, attack: 0.06f, decay: 0.35f),
                Tone(hornLen, W, 87f, 130f, 0.1f, lowpass: 0.12f, attack: 0.08f, decay: 0.4f));
            for (int i = 0; i < horn.Length; i++)
                horn[i] = (float)Math.Tanh(horn[i] * 4.5f) / (float)Math.Tanh(4.5f);
            horn = Tremolo(horn, 5.3f, 0.18f);
            var blast = Mix(
                horn,
                Tone(hornLen, S, 900f, 900f, 0.07f, noise: 1f, lowpass: 0.15f, attack: 0.05f, decay: 0.5f),   // breath
                Tone(0.4f, S, 70f, 38f, 0.5f, attack: 0.002f, decay: 3f));                                      // the thump

            float sweep = Game.VolEndT - Game.VolLandT;
            var layers = new System.Collections.Generic.List<float[]>
            {
                Delay(blast, Game.VolBlastT),
                // The shafts hissing overhead, from just before the first lands until the front leaves the field.
                Delay(Tone(sweep + 0.6f, S, 1700f, 1700f, 0.16f, noise: 1f, lowpass: 0.25f, attack: 0.45f, decay: 0.7f), Game.VolLandT - Game.VolFallT),
                // The fire catching behind the front.
                Delay(Crackle(sweep + 0.8f, 90), Game.VolLandT + 0.1f),
            };
            for (int k = 0; k < 14; k++)  // whistles of arrows coming down
            {
                float at = Game.VolLandT - Game.VolFallT + sweep * (k + (float)rng.NextDouble()) / 14f;
                layers.Add(Delay(Tone(0.38f, S, 2600f + (float)rng.NextDouble() * 500f, 1100f, 0.04f, attack: 0.1f, decay: 1f), at));
            }
            for (int k = 0; k < 44; k++)  // thuds as they land, a few heavier ones in enemies
            {
                float at = Game.VolLandT + sweep * (k + (float)rng.NextDouble()) / 44f;
                bool big = k % 5 == 2;
                layers.Add(Delay(Mix(
                    Tone(big ? 0.09f : 0.05f, S, big ? 900f : 1400f, big ? 900f : 1400f, big ? 0.3f : 0.12f, noise: 1f, lowpass: big ? 0.12f : 0.2f, attack: 0.001f, decay: 3f),
                    Tone(0.08f, Osc.Triangle, big ? 150f : 230f, big ? 70f : 120f, big ? 0.3f : 0.09f, attack: 0.001f, decay: 3f)), at));
            }
            Set(Sfx.Warhorn, Finish(Room(Mix(layers.ToArray()), 0.3f), 0.8f), 0.5f);
        }
    }
}
