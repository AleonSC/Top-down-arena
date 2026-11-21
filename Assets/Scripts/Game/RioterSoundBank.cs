using System;
using UnityEngine;

namespace Arena
{
    /// The Rioter's sounds, the Mob voice: every yell is two or three rough voices at once (detuned, a few ms apart), so a
    /// Rioter sounds like part of a riot. Played by each game from the Rioter's replicated state (Core/MeleeSounds.cs), never sent,
    /// except RiotWhoosh, sent with the bottle's throw (RiotBrains.cs). Render with `Tools/SoundCheck/render.sh Riot`.
    ///   RiotYell     "HEY! / OI!" when one first runs within earshot (three takes, rate-limited so a crowd isn't a wall of noise)
    ///   RiotAttack   "RAAH!" and a slap when it hits you
    ///   RiotHup      "HUP!" as it cocks the bottle
    ///   RiotWhoosh   the bottle tumbling away
    ///   RiotShatter  the bottle breaking where it stops
    ///   RiotDie      a falling "AAGH" that chokes off, and the body landing
    ///   RiotStep     a sneaker slap: running feet
    /// Kept dark (a lowpass at RiotTop, a quiet top formant): yells are midrange, not fizz.
    public partial class SoundBank
    {
        const float RiotTop = 2400f;  // lowpass on every voice, Hz

        // Shouted vowels (male formants F1-F3, F1 a little high as in a shout).
        static readonly float[] VowAA = { 800, 1150, 2600 }, VowEH = { 600, 1850, 2550 }, VowEE = { 330, 2300, 3000 },
            VowOH = { 600, 900, 2450 }, VowUH = { 620, 1200, 2450 };

        float riotWob, riotWobT;

        /// Smoothed random -1..1, a new value `rate` times a second (the voice's jitter and growl).
        float RiotWobble(float rate)
        {
            riotWobT += rate / Rate;
            if (riotWobT >= 1f)
            {
                riotWobT -= 1f;
                riotWob = (float)rng.NextDouble() * 2f - 1f;
            }
            return riotWob;
        }

        /// One shouting voice: a glottal pulse whose pitch rises from f0 to peak over `rise` of the length and slides to fEnd,
        /// with jitter and a period-doubling growl (`rough`), through three formants moving from vowel v0 to v1 (over `morph`
        /// of the length), saturated by `grit`. h: an "h" of breath before the voice (s); scale: formants up or down (a smaller
        /// or bigger head); choke: the last share of it breaks into a gurgle.
        float[] Shout(float dur, float f0, float peak, float fEnd, float[] v0, float[] v1, float vol, float rise = 0.2f, float grit = 0.7f,
            float breath = 0.08f, float rough = 0.4f, float h = 0f, float morph = 0.5f, float scale = 1f, float choke = 0f)
        {
            int n = (int)(dur * Rate);
            var raw = new float[n];
            double phase = 0;
            float prevG = 0f, jit = 0f;
            var lo = new float[3];
            var bd = new float[3];
            float[] q = { 5f, 9f, 12f }, gain = { 1f, 0.55f, 0.15f };
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n, t = (float)i / Rate;
                float f = u < rise ? f0 + (peak - f0) * (1f - (1f - u / rise) * (1f - u / rise)) : peak + (fEnd - peak) * ((u - rise) / (1f - rise));
                jit += 0.02f * (RiotWobble(40f) - jit);
                f *= 1f + 0.04f * jit + 0.015f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float g = p < 0.4f ? 0.5f * (1f - Mathf.Cos(Mathf.PI * p / 0.4f)) : p < 0.56f ? Mathf.Cos(0.5f * Mathf.PI * (p - 0.4f) / 0.16f) : 0f;
                float src = (g - prevG) * Rate / f * 0.1f;  // the pulse's slope: the buzz of the folds
                prevG = g;
                float half = (float)(phase * 0.5 - Math.Floor(phase * 0.5));
                src *= 1f + rough * (half < 0.5f ? 1f : -0.6f) * (0.6f + 0.4f * RiotWobble(9f));  // every other period louder: the growl
                float voiced = h > 0f ? Mathf.Clamp01((t - h * 0.6f) / (h * 0.5f)) : 1f;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float x = src * voiced + noise * (breath + (h > 0f && t < h ? 0.9f * (1f - t / h) : 0f));
                if (choke > 0f && u > 1f - choke)
                    x *= 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 32f * t);
                float m = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / morph));
                float y = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float fk = (v0[k] + (v1[k] - v0[k]) * m) * scale;
                    float kk = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(fk, Rate / 6f) / Rate);
                    lo[k] += kk * bd[k];
                    float hi = x - lo[k] - bd[k] / q[k];
                    bd[k] += kk * hi;
                    y += bd[k] * gain[k];
                }
                raw[i] = y * Mathf.Min(1f, t / 0.025f) * Mathf.Min(1f, (1f - u) / 0.18f);
            }
            float max = 1e-6f;
            foreach (float v in raw)
                max = Math.Max(max, Math.Abs(v));
            float drive = 1f + grit * 7f, norm = (float)Math.Tanh(drive);
            for (int i = 0; i < n; i++)
                raw[i] = (float)Math.Tanh(raw[i] / max * drive) / norm * vol;
            return Dark(Dark(raw, RiotTop), RiotTop);
        }

        /// The Mob: a voice and two more a little higher and lower, a few ms behind.
        float[] Mob(float dur, float f0, float peak, float fEnd, float[] v0, float[] v1, float h, float morph, float rise = 0.2f, float choke = 0f) => Mix(
            Shout(dur, f0, peak, fEnd, v0, v1, 0.4f, rise: rise, h: h, morph: morph, choke: choke),
            Delay(Shout(dur, f0 * 1.19f, peak * 1.21f, fEnd * 1.18f, v0, v1, 0.3f, rise: rise, grit: 0.75f, rough: 0.3f, breath: 0.15f, h: h, morph: morph, scale: 1.08f, choke: choke), 0.025f),
            Delay(Shout(dur, f0 * 0.84f, peak * 0.82f, fEnd * 0.85f, v0, v1, 0.3f, rise: rise, rough: 0.5f, h: h, morph: morph, scale: 0.95f, choke: choke), 0.045f));

        /// A one-pole-pair lowpass at hz: takes the fizz off the top.
        static float[] Dark(float[] x, float hz)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate), y = 0f;
            var o = new float[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                y += a * (x[i] - y);
                o[i] = y;
            }
            return o;
        }

        /// A sneaker hitting concrete: a soft slap, a low thud, a little scuff.
        float[] RiotSlap(float vol) => Mix(
            Tone(0.05f, Osc.Sine, 700f, 250f, vol * 0.5f, noise: 0.85f, lowpass: 0.2f, decay: 3f),
            Tone(0.08f, Osc.Sine, 170f, 70f, vol, lowpass: 0.3f, decay: 2.5f),
            Delay(Tone(0.06f, Osc.Sine, 1500f, 800f, vol * 0.12f, noise: 1f, lowpass: 0.3f, attack: 0.01f, decay: 2f), 0.03f));

        float[] RiotGlass()
        {
            var layers = new float[11][];
            layers[0] = Tone(0.12f, Osc.Sine, 3000f, 1500f, 0.4f, noise: 1f, lowpass: 0.4f, attack: 0.001f, decay: 3f);
            layers[1] = Tone(0.07f, Osc.Sine, 300f, 120f, 0.4f, noise: 0.3f, lowpass: 0.3f, decay: 3f);
            for (int i = 2; i < layers.Length; i++)  // shards: short high rings scattered over the first 0.2 s
                layers[i] = Delay(Bell(0.12f + 0.15f * (float)rng.NextDouble(), 1600f + 1800f * (float)rng.NextDouble(), 0.12f),
                    0.01f + 0.22f * (float)rng.NextDouble() * (float)rng.NextDouble());
            return Room(Mix(layers), 0.2f);
        }

        void BuildRioter()
        {
            Set(Sfx.RiotYell, new[] {
                Finish(Room(Concat(Mob(0.28f, 170f, 230f, 190f, VowEH, VowEE, 0.05f, 0.8f), new float[(int)(0.12f * Rate)], Mob(0.28f, 180f, 240f, 200f, VowEH, VowEE, 0.05f, 0.8f)), 0.12f), 0.25f),
                Finish(Room(Mob(0.4f, 160f, 220f, 180f, VowOH, VowEE, 0f, 0.7f), 0.12f), 0.25f),
                Finish(Room(Mob(0.42f, 165f, 235f, 185f, VowEH, VowEE, 0.06f, 0.8f), 0.12f), 0.25f) }, 0.7f);
            Set(Sfx.RiotAttack, Takes(2, () => Finish(Room(Mix(Mob(0.5f, 130f, 210f, 160f, VowUH, VowAA, 0f, 0.3f, rise: 0.25f), Delay(RiotSlap(0.6f), 0.3f)), 0.12f), 0.25f)), 0.35f);
            Set(Sfx.RiotHup, Finish(Room(Mob(0.18f, 170f, 220f, 160f, VowUH, VowUH, 0.04f, 0.5f), 0.12f), 0.22f), 0.3f);
            Set(Sfx.RiotWhoosh, Finish(Tremolo(Tone(0.4f, Osc.Sine, 400f, 1100f, 0.35f, noise: 0.9f, lowpass: 0.2f, attack: 0.05f, decay: 1.2f), 13f, 0.85f), 0.3f), 0.1f);
            Set(Sfx.RiotShatter, Takes(2, () => Finish(RiotGlass(), 0.25f)), 0.1f);
            Set(Sfx.RiotDie, Takes(2, () => Finish(Room(Mix(
                Mob(0.7f, 230f, 250f, 100f, VowAA, VowUH, 0f, 1f, rise: 0.1f, choke: 0.35f),
                Delay(Tone(0.18f, Osc.Sine, 120f, 45f, 0.5f, noise: 0.25f, lowpass: 0.25f, decay: 2.2f), 0.56f)), 0.12f), 0.25f)), 0.2f);
            Set(Sfx.RiotStep, Takes(3, () => Finish(RiotSlap(0.5f), 0.15f)), 0.12f);
            Jitter(0.12f, Sfx.RiotYell, Sfx.RiotAttack, Sfx.RiotHup, Sfx.RiotDie);
        }
    }
}
