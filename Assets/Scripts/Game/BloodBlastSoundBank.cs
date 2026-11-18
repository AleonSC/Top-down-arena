using UnityEngine;

namespace Arena
{
    /// The Blood Blast's sounds (Core/BloodBlast.cs): blood shaped into a blade, so every cut is a whoosh with a wet, low slosh
    /// under it and a spatter of drops at its end.
    ///   BlastSlash, BlastFlurry, BlastWhirl, BlastCleave, BlastFlight   each step of the chain, sent at the press.
    ///   BlastHit                                                       a cut landing (once a press that hits something).
    ///   BlastTideGather, BlastTideWave                                 Crimson Tide gathering, then rolling out.
    ///   BlastReturn                                                    the Blood Vortex: a rising swirl, a heartbeat as it lands (0.97 s in).
    /// The steps and the Tide use the Weapons volume; BlastHit and BlastReturn the Effects volume.
    public partial class SoundBank
    {
        /// Air torn by a blade of liquid: filtered noise sliding f0 to f1.
        float[] BlbWhoosh(float dur, float f0, float f1, float vol, float lp = 0.45f, float att = 0.02f) =>
            Tone(dur, Osc.Sine, f0, f1, vol, noise: 0.85f, lowpass: lp, attack: att, decay: 1.6f);

        /// The wet body under a cut: a low gurgle that wobbles.
        float[] BlbSlosh(float dur, float f, float vol) => Tremolo(Tone(dur, Osc.Sine, f * 1.4f, f, vol, noise: 0.2f, lowpass: 0.2f, attack: 0.01f, decay: 1.4f), 22f, 0.5f);

        /// Drops spattering: a few tiny, bright ticks.
        float[] BlbSpatter(float vol, int n = 4, float over = 0.12f)
        {
            var mix = Tone(0.02f, Osc.Sine, 2600f, 1800f, vol, noise: 1f, lowpass: 0.7f, attack: 0.0005f, decay: 4f);
            for (int i = 1; i < n; i++)
                mix = Mix(mix, Delay(Tone(0.018f, Osc.Sine, 2200f + 400f * i, 1500f, vol * (1f - 0.15f * i), noise: 1f, lowpass: 0.65f, attack: 0.0005f, decay: 4f), over * i / n));
            return mix;
        }

        /// A wet splat: a soft low thump and a smack of noise.
        float[] BlbSplat(float vol, float f = 140f) =>
            Mix(Tone(0.14f, Osc.Sine, f, f * 0.45f, vol, noise: 0.3f, lowpass: 0.25f, attack: 0.001f, decay: 2.6f),
                Tone(0.06f, Osc.Sine, 900f, 300f, vol * 0.6f, noise: 1f, lowpass: 0.4f, attack: 0.0008f, decay: 3f));

        void BuildBloodBlast()
        {
            Set(Sfx.BlastSlash, Finish(Mix(BlbWhoosh(0.2f, 600f, 1500f, 0.22f), BlbSlosh(0.2f, 110f, 0.08f), Delay(BlbSpatter(0.05f), 0.12f)), 0.45f), 0.03f);
            Set(Sfx.BlastFlurry, Finish(Mix(BlbWhoosh(0.1f, 800f, 1700f, 0.18f, 0.5f, 0.01f), Delay(BlbWhoosh(0.1f, 1700f, 800f, 0.18f, 0.5f, 0.01f), 0.15f),
                Delay(BlbWhoosh(0.1f, 800f, 1800f, 0.2f, 0.5f, 0.01f), 0.3f), BlbSlosh(0.42f, 130f, 0.06f), Delay(BlbSpatter(0.05f, 6, 0.3f), 0.08f)), 0.45f), 0.03f);
            Set(Sfx.BlastWhirl, Finish(Mix(Tremolo(BlbWhoosh(0.38f, 400f, 1300f, 0.22f, 0.45f, 0.08f), 9f, 0.5f), BlbSlosh(0.4f, 90f, 0.09f), Delay(BlbSpatter(0.06f, 6, 0.2f), 0.25f)), 0.5f), 0.03f);
            Set(Sfx.BlastCleave, Finish(Room(Mix(Reverse(BlbSlosh(0.3f, 80f, 0.1f)), Delay(BlbWhoosh(0.1f, 1400f, 400f, 0.25f), 0.28f),
                Delay(Mix(BlbSplat(0.6f, 110f), Tone(0.35f, Osc.Sine, 70f, 35f, 0.45f, noise: 0.2f, lowpass: 0.2f, attack: 0.002f, decay: 2f), BlbSpatter(0.1f, 7, 0.25f)), 0.38f)), 0.25f), 0.7f), 0.03f);
            Set(Sfx.BlastFlight, Finish(Mix(BlbWhoosh(0.12f, 700f, 1600f, 0.2f), Delay(Tremolo(BlbWhoosh(0.45f, 1300f, 500f, 0.16f, 0.4f, 0.03f), 14f, 0.4f), 0.1f),
                BlbSlosh(0.3f, 120f, 0.05f)), 0.45f), 0.03f);
            Set(Sfx.BlastHit, Finish(Mix(BlbSplat(0.32f), Delay(BlbSpatter(0.04f, 3, 0.06f), 0.02f)), 0.4f), 0.03f);
            Set(Sfx.BlastTideGather, Finish(Mix(Reverse(BlbWhoosh(0.6f, 300f, 1100f, 0.18f, 0.35f, 0.05f)), Tremolo(Tone(0.62f, Osc.Saw, 55f, 82f, 0.08f, lowpass: 0.12f, attack: 0.3f, decay: 0.6f), 18f, 0.5f),
                BlbSlosh(0.6f, 70f, 0.08f)), 0.5f), 0.03f);
            Set(Sfx.BlastTideWave, Finish(Room(Mix(BlbWhoosh(0.6f, 900f, 250f, 0.3f, 0.35f, 0.01f), BlbSplat(0.5f, 90f), BlbSlosh(0.55f, 60f, 0.12f),
                Delay(BlbSpatter(0.07f, 8, 0.4f), 0.1f)), 0.3f), 0.7f), 0.03f);
            Set(Sfx.BlastReturn, Finish(Mix(Tremolo(Tone(0.9f, Osc.Sine, 300f, 900f, 0.07f, noise: 0.6f, lowpass: 0.4f, attack: 0.3f, decay: 0.8f), 12f, 0.6f),
                Delay(Mix(Tone(0.12f, Osc.Sine, 75f, 45f, 0.5f, lowpass: 0.2f, attack: 0.002f, decay: 2.5f), Delay(Tone(0.12f, Osc.Sine, 70f, 42f, 0.35f, lowpass: 0.2f, attack: 0.002f, decay: 2.5f), 0.16f)), 0.95f)), 0.5f), 0.03f);
            Jitter(0.05f, Sfx.BlastSlash, Sfx.BlastFlurry, Sfx.BlastWhirl, Sfx.BlastCleave, Sfx.BlastFlight, Sfx.BlastHit, Sfx.BlastTideGather, Sfx.BlastTideWave);
        }
    }
}
