using System;

namespace Arena
{
    /// The main menu theme, synthesized in code like the sound effects: a slow synthwave loop
    /// in A minor (pads, bass, a plucked arpeggio with echoes, soft drums). Render() returns
    /// mono samples that loop seamlessly. Plain C# (no Unity), so it can run on a background
    /// thread and be tested outside the editor.
    public static class MenuMusic
    {
        public const int Rate = 44100;
        const float Bpm = 92f;
        const int Bars = 16;            // two passes through the 8-chord progression
        const float Beat = 60f / Bpm;
        const float BarLen = Beat * 4f;

        // Chord roots (MIDI) and qualities for each bar of one pass: Am F C G | Am F Dm E.
        static readonly int[] Roots = { 57, 53, 60, 55, 57, 53, 50, 52 };
        static readonly bool[] Minor = { true, false, false, false, true, false, true, false };

        internal static float Hz(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69f) / 12.0);

        static int[] Chord(int bar)
        {
            int i = bar % Roots.Length;
            int root = Roots[i];
            return new[] { root, root + (Minor[i] ? 3 : 4), root + 7 };
        }

        public static float[] Render()
        {
            float length = BarLen * Bars;
            int n = (int)(length * Rate);
            int tail = Rate * 3;                 // echoes and reverb ring past the end...
            var dry = new float[n + tail];
            var wet = new float[n + tail];       // sent to the reverb
            var echo = new float[n + tail];      // sent to the delay
            var rng = new Random(11);

            for (int bar = 0; bar < Bars; bar++)
            {
                float t0 = bar * BarLen;
                int[] chord = Chord(bar);
                bool secondPass = bar >= 8;

                // Pad: two detuned saws per chord note, slow attack, overlapping the next chord.
                foreach (int note in chord)
                    for (int d = -1; d <= 1; d += 2)
                        AddSaw(dry, wet, t0, BarLen + 0.6f, Hz(note - 12) * (1f + d * 0.0035f), 0.045f, 0.7f, 0.9f, 900f + 500f * (bar % 4) / 3f);

                // Bass: root eighth notes that duck after each beat (a gentle pump).
                float rootHz = Hz(chord[0] - 24);
                for (int e = 0; e < 8; e++)
                {
                    float vol = e % 2 == 0 ? 0.32f : 0.22f;
                    AddBass(dry, t0 + e * Beat / 2f, Beat / 2f * 0.95f, rootHz, vol);
                }

                // Arpeggio: 16th notes climbing the chord over two octaves; echoes add space.
                int[] pattern = { 0, 1, 2, 3, 4, 3, 2, 1 };
                for (int s = 0; s < 16; s++)
                {
                    if (!secondPass && bar < 2 && s % 2 == 1)
                        continue;  // starts sparse, fills in
                    int step = pattern[(s + bar) % pattern.Length];
                    int note = chord[step % 3] + 12 * (step / 3) + 12;
                    float vol = s % 4 == 0 ? 0.11f : 0.075f;
                    AddPluck(dry, echo, t0 + s * Beat / 4f, 0.35f, Hz(note), vol);
                }

                // Drums: kick on 1 and 3, closed hats on 8ths, a soft clap on 2 and 4 in the second pass.
                for (int b = 0; b < 4; b++)
                {
                    if (b % 2 == 0)
                        AddKick(dry, t0 + b * Beat, 0.55f);
                    if (secondPass && b % 2 == 1)
                        AddClap(dry, wet, t0 + b * Beat, 0.16f, rng);
                }
                for (int h = 0; h < 8; h++)
                    AddHat(dry, t0 + h * Beat / 2f, h % 2 == 1 ? 0.05f : 0.03f, rng);
            }

            // Dotted-eighth echo on the arpeggio, then a big reverb on the pads, claps and echoes.
            int delay = (int)(Beat * 0.75f * Rate);
            for (int i = delay; i < echo.Length; i++)
                echo[i] += echo[i - delay] * 0.42f;
            var lp = new OnePole(2500f);
            for (int i = 0; i < echo.Length; i++)
            {
                float e = lp.Process(echo[i]);
                dry[i] += e;
                wet[i] += e * 0.6f;
            }
            var verb = Reverb(wet);
            for (int i = 0; i < dry.Length; i++)
                dry[i] += verb[i] * 0.5f + wet[i] * 0.35f;

            // Fold the tail back onto the start so the loop has no seam.
            var outp = new float[n];
            Array.Copy(dry, outp, n);
            for (int i = 0; i < tail; i++)
                outp[i] += dry[n + i];

            // Soft-clip and normalize.
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                outp[i] = (float)Math.Tanh(outp[i] * 1.2f);
                peak = Math.Max(peak, Math.Abs(outp[i]));
            }
            if (peak > 0f)
                for (int i = 0; i < n; i++)
                    outp[i] *= 0.8f / peak;
            return outp;
        }

        // --- Instruments ---------------------------------------------------------

        internal static void AddSaw(float[] dry, float[] wet, float start, float dur, float hz, float vol, float attack, float release, float cutoff)
        {
            int i0 = (int)(start * Rate), len = (int)((dur + release) * Rate);
            var lp1 = new OnePole(cutoff);
            var lp2 = new OnePole(cutoff);
            double phase = (hz * 0.37) % 1.0;  // vary the start phase so voices don't line up
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float x = (float)(2.0 * (phase - Math.Floor(phase)) - 1.0);
                float env = Math.Min(1f, t / attack) * (t > dur ? Math.Max(0f, 1f - (t - dur) / release) : 1f);
                float y = lp2.Process(lp1.Process(x)) * env * vol;
                dry[i0 + k] += y * 0.6f;
                wet[i0 + k] += y * 0.8f;
            }
        }

        internal static void AddBass(float[] dry, float start, float dur, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(dur * Rate);
            double phase = 0;
            var lp = new OnePole(400f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float x = (float)Math.Sin(2.0 * Math.PI * p) * 0.8f + (4f * Math.Abs(p - 0.5f) - 1f) * 0.4f;
                float env = Math.Min(1f, t / 0.008f) * (float)Math.Exp(-t / (dur * 0.8f)) * Math.Min(1f, (dur - t) / 0.01f);
                dry[i0 + k] += lp.Process(x) * env * vol;
            }
        }

        internal static void AddPluck(float[] dry, float[] echo, float start, float dur, float hz, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(dur * Rate);
            double phase = 0;
            var lp = new OnePole(3200f);
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                phase += hz / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float sq = p < 0.5f ? 1f : -1f;
                float tri = 4f * Math.Abs(p - 0.5f) - 1f;
                float env = Math.Min(1f, t / 0.003f) * (float)Math.Exp(-t / 0.09f);
                float y = lp.Process(sq * 0.35f + tri * 0.65f) * env * vol;
                dry[i0 + k] += y;
                echo[i0 + k] += y * 0.7f;
            }
        }

        internal static void AddKick(float[] dry, float start, float vol)
        {
            int i0 = (int)(start * Rate), len = (int)(0.35f * Rate);
            double phase = 0;
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                float hz = 45f + 85f * (float)Math.Exp(-t / 0.03f);
                phase += hz / Rate;
                float env = (float)Math.Exp(-t / 0.12f);
                dry[i0 + k] += (float)Math.Sin(2.0 * Math.PI * phase) * env * vol;
            }
        }

        internal static void AddHat(float[] dry, float start, float vol, Random rng)
        {
            int i0 = (int)(start * Rate), len = (int)(0.05f * Rate);
            float prev = 0f;
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                float x = (float)rng.NextDouble() * 2f - 1f;
                float hp = x - prev;  // crude highpass: just the bright part of the noise
                prev = x;
                dry[i0 + k] += hp * (float)Math.Exp(-t / 0.012f) * vol;
            }
        }

        internal static void AddClap(float[] dry, float[] wet, float start, float vol, Random rng)
        {
            int i0 = (int)(start * Rate), len = (int)(0.25f * Rate);
            var lp = new OnePole(3000f);
            float prev = 0f;
            for (int k = 0; k < len && i0 + k < dry.Length; k++)
            {
                float t = (float)k / Rate;
                float x = (float)rng.NextDouble() * 2f - 1f;
                float band = lp.Process(x - prev);
                prev = x;
                // Three quick bursts, then a decaying body.
                float bursts = t < 0.03f ? (float)Math.Exp(-(t % 0.01f) / 0.003f) : (float)Math.Exp(-(t - 0.03f) / 0.06f);
                float y = band * bursts * vol;
                dry[i0 + k] += y;
                wet[i0 + k] += y * 1.2f;
            }
        }

        // --- Effects -------------------------------------------------------------

        internal class OnePole
        {
            readonly float a;
            float y;
            public OnePole(float hz) { a = 1f - (float)Math.Exp(-2.0 * Math.PI * hz / Rate); }
            public float Process(float x) { y += a * (x - y); return y; }
        }

        /// Large hall: eight damped combs into four allpasses (Freeverb-style).
        internal static float[] Reverb(float[] input)
        {
            int n = input.Length;
            var outp = new float[n];
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            foreach (int c in combs)
            {
                int d = c * Rate / 44100;
                var buf = new float[d];
                float store = 0f;
                for (int i = 0; i < n; i++)
                {
                    float y = buf[i % d];
                    store = y * 0.6f + store * 0.4f;
                    buf[i % d] = input[i] + store * 0.84f;
                    outp[i] += y * 0.125f;
                }
            }
            foreach (int a in new[] { 556, 441, 341, 225 })
            {
                var buf = new float[a];
                for (int i = 0; i < n; i++)
                {
                    float b = buf[i % a];
                    float x = outp[i];
                    buf[i % a] = x + b * 0.5f;
                    outp[i] = b - x;
                }
            }
            return outp;
        }
    }
}
