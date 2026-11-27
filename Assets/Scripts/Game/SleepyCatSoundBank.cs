using System;
using UnityEngine;

namespace Arena
{
    /// Sleepy Cat's sounds (Core/SleepyCat.cs), both sent by the host. On the Effects volume. Render: Tools/SoundCheck/render.sh Meow
    /// (and CatNap).
    ///   Meow: a little cat's "mi-aow", three takes a touch apart in pitch: a buzzy voice whose pitch rises then falls, through two
    ///   mouth resonances sliding from "ee" to "ow" as the mouth opens and closes, with a breath of air at the start.
    ///   CatNap: a long, falling, sleepy yawn (the same voice, the mouth opening wide), then a soft purr: a low rumble pulsing
    ///   about 24 times a second, fading out.
    public partial class SoundBank
    {
        void BuildSleepyCat()
        {
            var takes = new float[3][];
            float[] hz = { 1f, 1.09f, 0.93f };
            for (int k = 0; k < 3; k++)
                takes[k] = Finish(Mix(
                    CatVoice(0.42f + 0.04f * k, 560f * hz[k], 820f * hz[k], 520f * hz[k], 0.9f),
                    Tone(0.06f, Osc.Sine, 3000f, 2000f, 0.08f, noise: 1f, lowpass: 0.4f, attack: 0.004f, decay: 2f)), 0.42f);
            Set(Sfx.Meow, takes, 0.05f);
            float[] yawn = CatVoice(1.1f, 520f, 600f, 300f, 0.8f, wide: true);
            float[] purr = Tremolo(Tone(1.6f, Osc.Saw, 52f, 46f, 0.5f, noise: 0.35f, lowpass: 0.08f, attack: 0.25f, decay: 1.4f), 24f, 0.85f);
            Set(Sfx.CatNap, Finish(Mix(yawn, Delay(purr, 0.75f)), 0.38f), 0.5f);
        }

        /// A cat's voice: a buzzy pulse whose pitch goes f0 -> peak (a third of the way) -> f1, through two resonances that slide
        /// from "ee" (F1 400, F2 2100) to "ow" (F1 750, F2 1100) and back a little at the end, with a gentle vibrato. `wide`: the
        /// mouth opens further and stays open (a yawn).
        float[] CatVoice(float dur, float f0, float peak, float f1, float vol, bool wide = false)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            double phase = 0;
            float b1 = 0f, b1p = 0f, b2 = 0f, b2p = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n, t = (float)i / Rate;
                float f = u < 0.33f ? Mathf.Lerp(f0, peak, Mathf.SmoothStep(0f, 1f, u / 0.33f)) : Mathf.Lerp(peak, f1, Mathf.SmoothStep(0f, 1f, (u - 0.33f) / 0.67f));
                f *= 1f + 0.015f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = p < 0.18f ? 1f - p / 0.09f : -0.2f;  // a narrow glottal pulse: bright, buzzy
                float open = wide ? Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, u / 0.4f)) : Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.15f));
                float fa = Mathf.Lerp(400f, wide ? 850f : 750f, open), fb = Mathf.Lerp(2100f, wide ? 1000f : 1100f, open);
                Resonate(x, fa, 90f, ref b1, ref b1p, out float y1);
                Resonate(x, fb, 140f, ref b2, ref b2p, out float y2);
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Pow(1f - u, wide ? 1.2f : 0.7f);
                data[i] = (y1 * 0.9f + y2 * 0.55f) * env * vol * 0.12f;
            }
            return data;
        }

        /// One step of a two-pole resonance at `freq` Hz, `bw` Hz wide (its last two outputs in y1, y2).
        void Resonate(float x, float freq, float bw, ref float y1, ref float y2, out float y)
        {
            float r = Mathf.Exp(-Mathf.PI * bw / Rate);
            float c = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
            y = (1f - r) * x + c * y1 - r * r * y2;
            y2 = y1;
            y1 = y;
        }
    }
}
