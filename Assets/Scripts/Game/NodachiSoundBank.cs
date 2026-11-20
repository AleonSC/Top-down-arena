using UnityEngine;

namespace Arena
{
    /// The Nodachi's sounds: tamahagane steel. Deep, heavy air under every sweep, a thin edge-whistle riding on it, and the blade
    /// ringing after the cut; the Storm's armoured wind-ups creak and settle; the Guard's jabs are short hard whistles. Each step is
    /// one sound sent at the press (Core/Nodachi.cs) and built from NodachiMotion's own timings, so every whoosh lands as its edge does:
    ///   NodNuki, NodKesa, NodMoon, NodTenchi, NodNagi        Arashi's steps (Tenchi ends in the slam's boom).
    ///   NodKasumi, NodNidan, NodSenbon, NodTsuka, NodHiki    Mamori's steps.
    ///   NodHit                                               the blade biting an enemy (a slice of a sweep or a jab that hits).
    ///   NodBlock                                             the Guard's cone stopping a shot.
    ///   NodToStorm, NodToGuard                               the stance switch: the blade turned low with a heavy shing, or raised with a crisp one.
    /// The steps and the switch use the Weapons volume; NodHit and NodBlock the Effects volume.
    public partial class SoundBank
    {
        /// The heavy air a long blade pushes: a swell of dark noise with a low body under it and a thin whistle off the edge.
        float[] NodAir(float dur, float lo, float hi, float vol, bool rising = true)
        {
            float f0 = rising ? lo : hi, f1 = rising ? hi : lo;
            return Mix(
                Tone(dur, Osc.Sine, f0, f1, vol, noise: 0.92f, lowpass: 0.22f, attack: dur * 0.45f, decay: 1.5f),
                Tone(dur, Osc.Sine, f0 * 0.18f, f1 * 0.14f, vol * 0.5f, lowpass: 0.3f, attack: dur * 0.4f, decay: 1.4f),
                Tone(dur * 0.8f, Osc.Sine, f0 * 3f, f1 * 3.2f, vol * 0.2f, noise: 0.55f, lowpass: 0.65f, attack: dur * 0.5f, decay: 2f));
        }

        /// The blade ringing after a cut: a long steel bell, high and thin.
        static float[] NodRing(float dur, float vol, float f = 1480f) => Mix(Bell(dur, f, vol), Bell(dur * 0.7f, f * 1.51f, vol * 0.4f));

        /// The armoured wind-up: the grip tightening, plates settling, a low strain rising into the swing.
        float[] NodCreak(float dur, float vol) =>
            Mix(Reverse(Tone(dur, Osc.Saw, 62f, 74f, vol, lowpass: 0.08f, attack: 0.01f, decay: 1.4f)),
                Tone(dur * 0.6f, Osc.Triangle, 210f, 160f, vol * 0.35f, noise: 0.5f, lowpass: 0.3f, attack: 0.004f, decay: 3f),
                Delay(Tone(0.03f, Osc.Square, 900f, 600f, vol * 0.25f, noise: 0.5f, lowpass: 0.5f, attack: 0.001f, decay: 4f), dur * 0.35f));

        /// A jab: a short hard whistle of the point, a tick of steel at full stretch.
        float[] NodThrust(float vol, float pitch = 1f) =>
            Mix(Tone(0.11f, Osc.Sine, 1500f * pitch, 3200f * pitch, vol, noise: 0.7f, lowpass: 0.6f, attack: 0.012f, decay: 2.2f),
                Tone(0.09f, Osc.Sine, 240f * pitch, 140f * pitch, vol * 0.45f, noise: 0.3f, lowpass: 0.3f, attack: 0.01f, decay: 2f),
                Delay(Tone(0.02f, Osc.Triangle, 3800f * pitch, 3000f * pitch, vol * 0.35f, noise: 0.3f, lowpass: 0.85f, attack: 0.0005f, decay: 3f), 0.05f));

        float[] NodBoom(float vol) =>
            Mix(Tone(0.7f, Osc.Sine, 105f, 32f, vol, noise: 0.25f, lowpass: 0.2f, attack: 0.001f, decay: 2.2f),
                Tone(0.08f, Osc.Sine, 4200f, 1400f, vol * 0.45f, noise: 1f, lowpass: 0.9f, attack: 0.0005f, decay: 3f),
                Tone(0.5f, Osc.Sine, 2000f, 120f, vol * 0.25f, noise: 1f, lowpass: 0.4f, attack: 0.002f, decay: 1.8f),
                Crackle(0.35f, 14),
                NodRing(1.1f, vol * 0.12f, 392f));

        static float When(float t) => t < 0f ? 0f : t;

        void BuildNodachi()
        {
            var st = NodachiMotion.Storm;
            var gd = NodachiMotion.Guard;
            // Arashi: each sweep's air swells into its edge, the blade rings after.
            {
                var a = st[0].Segs[0];
                Set(Sfx.NodNuki, Finish(Room(Mix(NodCreak(a.T0, 0.12f), Delay(NodAir(a.T1 - a.T0 + 0.18f, 260f, 900f, 0.32f), When(a.T0 - 0.1f)),
                    Delay(NodRing(0.7f, 0.05f), a.T1)), 0.2f), 0.55f), 0.03f);
            }
            {
                var a = st[1].Segs[0];
                Set(Sfx.NodKesa, Finish(Room(Mix(NodCreak(a.T0, 0.12f), Delay(NodAir(a.T1 - a.T0 + 0.18f, 240f, 820f, 0.32f, false), When(a.T0 - 0.1f)),
                    Delay(NodRing(0.7f, 0.05f, 1320f), a.T1)), 0.2f), 0.55f), 0.03f);
            }
            {
                var a = st[2].Segs[0];  // Engetsu: one long circle, the air beating as it comes round, a long ring
                float d = a.T1 - a.T0 + 0.25f;
                Set(Sfx.NodMoon, Finish(Room(Mix(NodCreak(a.T0, 0.14f), Delay(Tremolo(NodAir(d, 200f, 760f, 0.34f), 7f, 0.35f), When(a.T0 - 0.12f)),
                    Delay(NodRing(1.2f, 0.07f, 1175f), a.T1)), 0.3f), 0.6f), 0.03f);
            }
            {
                var spin = st[3].Segs[0];
                var slam = st[3].Segs[1];  // Tenchi: the spin, the blade hauled up overhead (an in-rush), the slam's boom and ring
                Set(Sfx.NodTenchi, Finish(Room(Mix(NodCreak(spin.T0, 0.14f),
                    Delay(Tremolo(NodAir(spin.T1 - spin.T0 + 0.15f, 220f, 700f, 0.3f), 6f, 0.3f), When(spin.T0 - 0.1f)),
                    Delay(Reverse(NodAir(slam.T0 - spin.T1 + 0.04f, 300f, 1100f, 0.28f)), spin.T1 - 0.02f),
                    Delay(NodBoom(0.9f), slam.T0)), 0.35f), 0.8f), 0.03f);
            }
            {
                var a = st[4].Segs[1];
                var b = st[4].Segs[2];  // Nagi: there and back, a footfall under each
                Set(Sfx.NodNagi, Finish(Room(Mix(NodCreak(a.T0, 0.1f),
                    Delay(NodAir(a.T1 - a.T0 + 0.16f, 260f, 880f, 0.3f), When(a.T0 - 0.1f)),
                    Delay(NodAir(b.T1 - b.T0 + 0.16f, 260f, 880f, 0.3f, false), When(b.T0 - 0.1f)),
                    Delay(Tone(0.09f, Osc.Sine, 140f, 70f, 0.14f, noise: 0.4f, lowpass: 0.25f, attack: 0.002f, decay: 3f), a.T0),
                    Delay(Tone(0.09f, Osc.Sine, 130f, 65f, 0.14f, noise: 0.4f, lowpass: 0.25f, attack: 0.002f, decay: 3f), b.T0),
                    Delay(NodRing(0.6f, 0.04f), b.T1)), 0.2f), 0.55f), 0.03f);
            }
            // Mamori: hard whistles timed to each stretch.
            Set(Sfx.NodKasumi, Finish(Delay(NodThrust(0.35f), When(NodachiMotion.HitAt(gd[0].Segs[0]) - 0.035f)), 0.45f), 0.02f);
            Set(Sfx.NodNidan, Finish(Mix(Delay(NodThrust(0.3f, 0.92f), When(NodachiMotion.HitAt(gd[1].Segs[0]) - 0.035f)), Delay(NodThrust(0.35f, 1.06f), When(NodachiMotion.HitAt(gd[1].Segs[1]) - 0.035f))), 0.48f), 0.02f);
            {
                var layers = new System.Collections.Generic.List<float[]>();
                var segs = gd[2].Segs;
                for (int i = 0; i < segs.Length; i++)
                    layers.Add(Delay(NodThrust(0.22f + 0.02f * i, 0.9f + 0.05f * ((i * 3) % 5)), When(NodachiMotion.HitAt(segs[i]) - 0.035f)));
                layers.Add(Delay(NodRing(0.7f, 0.05f, 1660f), segs[segs.Length - 1].T1));
                Set(Sfx.NodSenbon, Finish(Room(Mix(layers.ToArray()), 0.15f), 0.55f), 0.02f);
            }
            {
                float hit = NodachiMotion.HitAt(gd[3].Segs[0]);  // Tsuka-ate: a short drawn breath of air, then the wooden butt's knock
                Set(Sfx.NodTsuka, Finish(Mix(Reverse(Tone(hit, Osc.Sine, 300f, 700f, 0.12f, noise: 0.9f, lowpass: 0.3f, attack: 0.01f, decay: 1.5f)),
                    Delay(Mix(Tone(0.07f, Osc.Triangle, 330f, 200f, 0.4f, noise: 0.2f, lowpass: 0.45f, attack: 0.0008f, decay: 4f),
                        Tone(0.14f, Osc.Sine, 140f, 60f, 0.4f, noise: 0.2f, lowpass: 0.25f, attack: 0.001f, decay: 2.5f)), hit)), 0.55f), 0.02f);
            }
            {
                var hop = gd[4].Segs[0];  // Hiki-zuki: a scuff and swish of the hop back, then the jab
                Set(Sfx.NodHiki, Finish(Mix(Tone(hop.T1 + 0.05f, Osc.Sine, 900f, 300f, 0.16f, noise: 0.9f, lowpass: 0.35f, attack: 0.01f, decay: 1.6f),
                    Delay(Tone(0.06f, Osc.Sine, 160f, 80f, 0.15f, noise: 0.5f, lowpass: 0.25f, attack: 0.002f, decay: 3f), hop.T1),
                    Delay(NodThrust(0.38f, 1.04f), When(NodachiMotion.HitAt(gd[4].Segs[1]) - 0.035f))), 0.5f), 0.02f);
            }
            // A cut landing: a wet slice, a low thump and a short ring.
            Set(Sfx.NodHit, Finish(Mix(Tone(0.08f, Osc.Sine, 2600f, 900f, 0.3f, noise: 0.95f, lowpass: 0.55f, attack: 0.001f, decay: 2.5f),
                Tone(0.12f, Osc.Sine, 170f, 70f, 0.35f, noise: 0.2f, lowpass: 0.25f, attack: 0.001f, decay: 2.5f),
                Delay(NodRing(0.25f, 0.06f, 1760f), 0.01f)), 0.45f), 0.03f);
            // A shot stopped on the blade: a bright tink.
            Set(Sfx.NodBlock, Finish(Mix(Tone(0.02f, Osc.Triangle, 4200f, 3400f, 0.3f, noise: 0.2f, lowpass: 0.9f, attack: 0.0005f, decay: 3f),
                NodRing(0.3f, 0.12f, 2490f)), 0.4f), 0.03f);
            // The stance switch: the blade turned in the grip, a slide of steel (low and heavy into the Storm, crisp and high into the Guard).
            Set(Sfx.NodToStorm, Finish(Room(Mix(Tone(0.3f, Osc.Sine, 1800f, 700f, 0.22f, noise: 0.85f, lowpass: 0.55f, attack: 0.02f, decay: 1.4f),
                Tone(0.25f, Osc.Saw, 90f, 62f, 0.1f, lowpass: 0.1f, attack: 0.01f, decay: 1.5f),
                Delay(NodRing(0.6f, 0.06f, 988f), 0.2f)), 0.25f), 0.5f), 0.02f);
            Set(Sfx.NodToGuard, Finish(Room(Mix(Tone(0.22f, Osc.Sine, 2200f, 6000f, 0.2f, noise: 0.85f, lowpass: 0.7f, attack: 0.015f, decay: 1.3f),
                Delay(Tone(0.02f, Osc.Triangle, 3000f, 2400f, 0.2f, noise: 0.3f, lowpass: 0.85f, attack: 0.0005f, decay: 3f), 0.17f),
                Delay(NodRing(0.5f, 0.07f, 1976f), 0.18f)), 0.2f), 0.5f), 0.02f);
            Jitter(0.04f, Sfx.NodNuki, Sfx.NodKesa, Sfx.NodMoon, Sfx.NodTenchi, Sfx.NodNagi, Sfx.NodKasumi, Sfx.NodNidan, Sfx.NodSenbon, Sfx.NodTsuka, Sfx.NodHiki,
                Sfx.NodHit, Sfx.NodBlock, Sfx.NodToStorm, Sfx.NodToGuard);
        }
    }
}
