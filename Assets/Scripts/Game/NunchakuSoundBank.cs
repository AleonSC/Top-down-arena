using UnityEngine;

namespace Arena
{
    /// The Nunchaku's sounds, the Storm set of Docs/Mockups/nunchaku-sounds.html (the Twin Storm's crimson bands in sound: airy whooshes with a
    /// low hum under them, an electric crack where a stick snaps, a boom under the Twin Crush). Raised in Core/Nunchaku.cs and Knight.cs:
    ///   NunSnap, NunBack, NunFig8, NunCrush   each combo step, sent at the press and timed so its crack lands with the hit.
    ///   NunCounter                             the Twin Counter's double lash (Fire in the guard).
    ///   NunHit                                 a stick landing on an enemy (once per swing that hits something).
    ///   NunGuard                               the windmill spinning up: raising the guard, and again after a counter.
    ///   NunSwat                                a disc batting a shot back.
    /// The steps, the counter and the windmill use the Weapons volume; NunHit and NunSwat the Effects volume.
    public partial class SoundBank
    {
        float[] NunWhoosh(float dur, float f0, float f1, float vol, float lp = 0.5f, float att = 0.03f) =>
            Tone(dur, Osc.Sine, f0, f1, vol, noise: 0.85f, lowpass: lp, attack: att, decay: 1.6f);

        /// A hard knock: the stick's wood on a body or a shot.
        float[] NunTok(float f, float vol, float dur = 0.045f) =>
            Mix(Tone(dur, Osc.Triangle, f, f * 0.8f, vol, noise: 0.15f, lowpass: 0.6f, attack: 0.0008f, decay: 4f),
                Tone(0.012f, Osc.Sine, 4000f, 2500f, vol * 0.5f, noise: 1f, lowpass: 0.9f, attack: 0.0005f, decay: 3f));

        float[] NunCrack(float vol) =>
            Mix(Tone(0.02f, Osc.Sine, 5000f, 1800f, vol, noise: 1f, lowpass: 0.95f, attack: 0.0004f, decay: 3f),
                Tone(0.05f, Osc.Square, 1400f, 700f, vol * 0.35f, noise: 0.4f, lowpass: 0.7f, attack: 0.0004f, decay: 4f));

        float[] NunThump(float vol, float f0 = 120f, float f1 = 50f, float dur = 0.16f) =>
            Tone(dur, Osc.Sine, f0, f1, vol, noise: 0.25f, lowpass: 0.25f, attack: 0.001f, decay: 2.5f);

        /// Spinning sticks: a whoosh beating `hz` times a second.
        float[] NunRotor(float dur, float hz, float f0, float f1, float vol) =>
            Tremolo(Tone(dur, Osc.Sine, f0, f1, vol, noise: 0.85f, lowpass: 0.45f, attack: dur * 0.5f, decay: 0.7f), hz, 0.75f);

        /// The crimson hum under a swing.
        float[] NunHum(float dur, float f, float vol) => Tremolo(Tone(dur, Osc.Saw, f, f, vol, lowpass: 0.15f, attack: 0.02f, decay: 1.2f), 30f, 0.4f);

        /// An electric crack: the stick snapping out.
        float[] NunZap(float vol) => Mix(NunCrack(vol), Tone(0.08f, Osc.Saw, 2400f, 600f, vol * 0.35f, noise: 0.3f, lowpass: 0.5f, attack: 0.0005f, decay: 3f));

        void BuildNunchaku()
        {
            Set(Sfx.NunSnap, Finish(Mix(NunWhoosh(0.16f, 500f, 1600f, 0.2f, 0.55f, 0.02f), NunHum(0.16f, 220f, 0.05f), Delay(NunZap(0.35f), 0.1f)), 0.5f), 0.03f);
            Set(Sfx.NunBack, Finish(Mix(NunWhoosh(0.22f, 1300f, 400f, 0.22f), NunHum(0.22f, 196f, 0.05f), Delay(NunTok(800f, 0.08f), 0.12f)), 0.45f), 0.03f);
            Set(Sfx.NunFig8, Finish(Mix(NunWhoosh(0.13f, 500f, 1400f, 0.2f, 0.55f, 0.015f), Delay(NunWhoosh(0.13f, 1400f, 500f, 0.2f, 0.55f, 0.015f), 0.12f),
                NunHum(0.26f, 247f, 0.05f), Delay(NunZap(0.12f), 0.065f), Delay(NunZap(0.12f), 0.185f)), 0.5f), 0.03f);
            Set(Sfx.NunCrush, Finish(Room(Mix(NunRotor(0.36f, 16f, 300f, 1000f, 0.2f), Reverse(NunHum(0.36f, 110f, 0.08f)),
                Delay(Mix(NunZap(0.4f), NunThump(0.9f, 90f, 30f, 0.55f), Tone(0.45f, Osc.Sine, 2500f, 150f, 0.22f, noise: 1f, lowpass: 0.45f, attack: 0.002f, decay: 1.8f),
                    NunHum(0.5f, 82f, 0.1f)), 0.36f)), 0.3f), 0.75f), 0.03f);
            Set(Sfx.NunHit, Finish(Mix(NunTok(650f, 0.25f, 0.06f), NunThump(0.25f, 170f, 70f, 0.1f),
                Delay(Tone(0.12f, Osc.Sine, 3000f, 5000f, 0.04f, noise: 0.9f, lowpass: 0.8f, decay: 2f), 0.01f)), 0.45f), 0.03f);
            Set(Sfx.NunGuard, Finish(Mix(NunRotor(0.5f, 20f, 350f, 1000f, 0.2f), Reverse(NunHum(0.45f, 165f, 0.07f))), 0.45f), 0.03f);
            Set(Sfx.NunSwat, Finish(Mix(NunTok(1200f, 0.2f, 0.035f), NunZap(0.15f),
                Delay(Tone(0.16f, Osc.Sine, 1500f, 3500f, 0.08f, noise: 0.5f, lowpass: 0.7f, decay: 2f), 0.01f)), 0.4f), 0.03f);
            Set(Sfx.NunCounter, Finish(Room(Mix(NunWhoosh(0.2f, 600f, 2200f, 0.22f, 0.6f, 0.02f), Reverse(NunHum(0.2f, 220f, 0.06f)),
                Delay(Mix(NunZap(0.45f), Delay(NunZap(0.3f), 0.03f), NunThump(0.45f, 120f, 40f, 0.25f), NunHum(0.3f, 110f, 0.06f)), 0.2f)), 0.25f), 0.65f), 0.03f);
            Jitter(0.05f, Sfx.NunSnap, Sfx.NunBack, Sfx.NunFig8, Sfx.NunCrush, Sfx.NunHit, Sfx.NunGuard, Sfx.NunSwat, Sfx.NunCounter);
        }
    }
}
