using System;
using UnityEngine;

namespace Arena
{
    /// The Rubber Chicken skin's sound pack (Expertise.cs), one sound for each of the Twin Blades' cues (TwinSoundBank.cs; the host
    /// picks these by the swinger's skin, Game.TwinSfx). A rubber
    /// chicken is air squeezed through a reed: a buzzy, nasal pitch that jumps up, wavers, and sags as it runs out of air.
    /// Squeak builds that voice (a narrow pulse through two nasal formants, with a pitch contour, vibrato and breath).
    public partial class SoundBank
    {
        /// One squeal: the pitch eases from f0 up to peak over `rise` of the length, sags toward fEnd, then drops in the last
        /// 12% as the air runs out. vib: vibrato depth (share of the pitch) at vibHz; breath: noise mixed in (0..1).
        float[] Squeak(float dur, float f0, float peak, float fEnd, float vol, float vib = 0.03f, float vibHz = 8f, float breath = 0.08f, float rise = 0.15f)
        {
            int n = (int)(dur * Rate);
            var raw = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n, t = (float)i / Rate;
                float f = u < rise ? f0 + (peak - f0) * (1f - (1f - u / rise) * (1f - u / rise)) : peak + (fEnd - peak) * ((u - rise) / (1f - rise));
                if (u > 0.88f)
                    f *= 1f - 0.35f * (u - 0.88f) / 0.12f;
                f *= 1f + vib * Mathf.Sin(2f * Mathf.PI * vibHz * t) + 0.012f * Mathf.Sin(2f * Mathf.PI * 23f * t);
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = (p < 0.28f ? 1f : -0.39f) + ((float)rng.NextDouble() * 2f - 1f) * breath;  // a narrow pulse: the reed
                float env = Mathf.Min(1f, t / 0.012f) * Mathf.Min(1f, (1f - u) * n / (0.05f * Rate)) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 13f * t));
                raw[i] = x * env;
            }
            var a = Resonate(raw, 1350f, 5f);
            var b = Resonate(raw, 2900f, 7f);
            var outp = new float[n];
            for (int i = 0; i < n; i++)
                outp[i] = (a[i] * 0.9f + b[i] * 0.6f + raw[i] * 0.12f) * vol;
            return outp;
        }

        /// A band-pass resonance (a state-variable filter) at f Hz: the nose of the squeak.
        static float[] Resonate(float[] x, float f, float q)
        {
            var y = new float[x.Length];
            float k = 2f * Mathf.Sin(Mathf.PI * f / Rate), low = 0f, band = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                low += k * band;
                float high = x[i] - low - band / q;
                band += k * high;
                y[i] = band;
            }
            return y;
        }

        /// A rubbery thwap: a soft low thump with a little wobble (a rubber body hitting something).
        float[] Thwap(float vol) => Mix(
            Tone(0.09f, Osc.Sine, 220f, 90f, vol, noise: 0.25f, lowpass: 0.3f, decay: 2.5f),
            Tremolo(Tone(0.16f, Osc.Triangle, 160f, 120f, vol * 0.5f, lowpass: 0.4f, decay: 2f), 28f, 0.6f));

        void BuildChicken()
        {
            // Rising Cut: a quick "eek!" that jumps up. Falling Cut: the same squeak, sagging down.
            Set(Sfx.ChickCut1, Finish(Mix(Squeak(0.22f, 420f, 980f, 1040f, 0.5f, rise: 0.25f),
                Tone(0.12f, Osc.Sine, 400f, 1000f, 0.08f, noise: 0.85f, lowpass: 0.5f, attack: 0.01f)), 0.45f), 0.04f);
            Set(Sfx.ChickCut2, Finish(Mix(Squeak(0.22f, 900f, 940f, 560f, 0.5f, rise: 0.08f),
                Tone(0.12f, Osc.Sine, 1000f, 400f, 0.08f, noise: 0.85f, lowpass: 0.5f, attack: 0.01f)), 0.45f), 0.04f);
            // Double Jab: two short pecks, "bwek bwek".
            Set(Sfx.ChickJab, Finish(Concat(Squeak(0.075f, 520f, 760f, 700f, 0.5f, vib: 0f, rise: 0.4f), new float[(int)(0.07f * Rate)],
                Squeak(0.075f, 560f, 820f, 760f, 0.5f, vib: 0f, rise: 0.4f)), 0.4f), 0.03f);
            // Pirouette: a long warbling squeal, the pitch swinging as it goes round (a chicken helicopter).
            Set(Sfx.ChickSpin, Finish(Mix(Squeak(0.48f, 600f, 900f, 780f, 0.5f, vib: 0.14f, vibHz: 6.5f, breath: 0.12f),
                Tremolo(Tone(0.45f, Osc.Sine, 500f, 800f, 0.08f, noise: 0.9f, lowpass: 0.4f, attack: 0.1f, decay: 1.2f), 6.5f, 0.7f)), 0.48f), 0.1f);
            // Crescent Shear: a wheezing in-breath, then the full scream "AAAAAAGH" as both chickens come down.
            Set(Sfx.ChickShear, Finish(Room(Mix(
                Reverse(Tone(0.22f, Osc.Sine, 300f, 700f, 0.12f, noise: 0.95f, lowpass: 0.35f, decay: 1f)),
                Delay(Squeak(0.62f, 380f, 1150f, 860f, 0.55f, vib: 0.05f, vibHz: 9f, breath: 0.1f, rise: 0.1f), 0.2f),
                Delay(Squeak(0.6f, 300f, 1150f * 0.75f, 640f, 0.3f, vib: 0.05f, vibHz: 8.3f, rise: 0.12f), 0.21f)), 0.15f), 0.6f), 0.15f);
            // A cut lands: a rubber thwap and a tiny honk. The heavy one squashes the air out: thwap and a big honk.
            Set(Sfx.ChickHit, Finish(Mix(Thwap(0.3f), Squeak(0.09f, 700f, 760f, 620f, 0.18f, vib: 0f, rise: 0.2f)), 0.4f), 0.035f);
            Set(Sfx.ChickHitHeavy, Finish(Mix(Thwap(0.45f), Squeak(0.26f, 460f, 620f, 380f, 0.4f, vib: 0.06f, vibHz: 11f, breath: 0.15f, rise: 0.1f)), 0.55f), 0.06f);
            // Twin Fang thrown: a squeal that fades off as the pair spins away (the pitch wobbling with each turn).
            Set(Sfx.ChickThrow, Finish(Mix(
                Tone(0.07f, Osc.Sine, 600f, 1800f, 0.15f, noise: 0.85f, lowpass: 0.7f, attack: 0.004f, decay: 2.5f),
                Delay(Tremolo(Squeak(0.5f, 820f, 900f, 520f, 0.4f, vib: 0.1f, vibHz: 18f), 18f, 0.5f), 0.03f)), 0.45f), 0.15f);
            // Where they cross: two chickens collide, a double honk and a puff of feathers (a soft noise burst).
            Set(Sfx.ChickCross, Finish(Room(Mix(
                Squeak(0.2f, 600f, 1000f, 700f, 0.4f, rise: 0.15f),
                Delay(Squeak(0.22f, 500f, 800f, 560f, 0.4f, rise: 0.15f), 0.04f),
                Tone(0.25f, Osc.Sine, 2000f, 800f, 0.1f, noise: 1f, lowpass: 0.3f, attack: 0.01f, decay: 1.5f),
                Thwap(0.3f)), 0.15f), 0.55f), 0.1f);
            // Caught: two tiny squeezes.
            Set(Sfx.ChickCatch, Finish(Concat(Squeak(0.05f, 900f, 1100f, 1000f, 0.4f, vib: 0f), new float[(int)(0.04f * Rate)], Squeak(0.05f, 1000f, 1200f, 1100f, 0.4f, vib: 0f)), 0.3f), 0.1f);
        }
    }
}
