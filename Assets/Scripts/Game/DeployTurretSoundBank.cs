using UnityEngine;

namespace Arena
{
    /// The Deployable Turret's sounds (Core/DeployableTurret.cs). Each game plays them from the zone and its owner's trigger
    /// (DeployTurretLook.cs), so nothing is sent:
    ///   DtDrop      the crate landing: a hard slam, a metal clank, then the legs unfolding on servos
    ///   DtShot      one heavy round: a flat crack over a deep chesty thump (it fires up to 30 a second, so it is short)
    ///   DtSpool     the barrels spinning up from rest: a motor whine climbing under a rattle
    ///   DtHeat      the overheat: a rising hiss with four alarm beeps
    ///   DtBlast     the blast: a deep boom, a noise burst and rubble
    ///   DtLatch     the owner stepping out early: the seat's latch clunking and the servos winding down
    /// DtShot and DtSpool are on the Weapons volume, the rest on Effects (the Categories() table in SoundBank.cs).
    /// Render: Tools/SoundCheck/render.sh Dt
    public partial class SoundBank
    {
        void BuildDeployTurret()
        {
            const Osc S = Osc.Sine;
            Set(Sfx.DtDrop, Finish(Mix(
                Tone(0.3f, S, 120f, 38f, 0.8f, attack: 0.002f, decay: 3.5f),                                       // the slam
                Tone(0.25f, Osc.Square, 900f, 380f, 0.16f, noise: 0.5f, lowpass: 0.4f, attack: 0.001f, decay: 5f),  // metal clank
                Delay(Tone(0.5f, Osc.Saw, 160f, 420f, 0.07f, lowpass: 0.2f, attack: 0.05f, decay: 1.2f), 0.4f),   // the legs unfolding
                Delay(Tremolo(Tone(0.35f, Osc.Square, 500f, 700f, 0.05f, noise: 0.5f, lowpass: 0.3f, attack: 0.02f, decay: 1.6f), 22f, 0.7f), 0.45f),
                Delay(Tone(0.12f, Osc.Square, 1400f, 700f, 0.12f, noise: 0.3f, lowpass: 0.5f, decay: 4f), 0.95f)), 0.8f), 0.04f);   // the final lock

            SetGun(Sfx.DtShot, 0.025f, new Gun
            {
                Crack = 0.9f, CrackDecay = 0.0022f, CrackHighpass = 1700f,
                Body = 0.9f, BodyDecay = 0.045f, BodyLowpass = 1800f,
                Thump = 1f, ThumpFrom = 140f, ThumpTo = 48f, ThumpDecay = 0.07f,
                Tail = 0.18f, TailDecay = 0.2f, TailLowpass = 800f,
                Reverb = 0.14f, Duration = 0.26f, Peak = 0.62f,
            });

            Set(Sfx.DtSpool, Finish(Mix(
                Tone(0.6f, Osc.Saw, 110f, 780f, 0.1f, lowpass: 0.25f, attack: 0.08f, decay: 0.5f),
                Tremolo(Tone(0.6f, Osc.Square, 400f, 1300f, 0.05f, noise: 0.6f, lowpass: 0.4f, attack: 0.08f, decay: 0.6f), 26f, 0.7f),
                Delay(Tone(0.04f, Osc.Square, 1800f, 1200f, 0.07f, lowpass: 0.6f, decay: 3f), 0.55f)), 0.4f), 0.2f);

            Set(Sfx.DtHeat, Finish(Mix(
                Tone(0.8f, S, 2600f, 5200f, 0.07f, noise: 1f, lowpass: 0.6f, attack: 0.3f, decay: 1.2f),            // a rising hiss
                Delay(Tone(0.08f, Osc.Square, 1500f, 1500f, 0.1f, lowpass: 0.4f, attack: 0.002f, decay: 2.5f), 0.05f),
                Delay(Tone(0.08f, Osc.Square, 1500f, 1500f, 0.1f, lowpass: 0.4f, attack: 0.002f, decay: 2.5f), 0.25f),
                Delay(Tone(0.08f, Osc.Square, 1800f, 1800f, 0.11f, lowpass: 0.4f, attack: 0.002f, decay: 2.5f), 0.45f),
                Delay(Tone(0.08f, Osc.Square, 2100f, 2100f, 0.12f, lowpass: 0.4f, attack: 0.002f, decay: 2.5f), 0.65f)), 0.4f), 0.05f);

            Set(Sfx.DtBlast, Finish(Mix(
                Tone(1f, S, 90f, 28f, 0.95f, attack: 0.002f, decay: 2.2f),                                         // the boom
                Tone(0.8f, S, 4000f, 300f, 0.5f, noise: 1f, lowpass: 0.3f, attack: 0.002f, decay: 2.4f),           // the burst
                Delay(Tone(0.7f, S, 900f, 120f, 0.25f, noise: 1f, lowpass: 0.08f, attack: 0.05f, decay: 2f), 0.12f)), 0.9f), 0.06f);   // rubble

            Set(Sfx.DtLatch, Finish(Mix(
                Tone(0.1f, Osc.Square, 700f, 300f, 0.2f, noise: 0.4f, lowpass: 0.4f, attack: 0.001f, decay: 5f),
                Delay(Tone(0.3f, Osc.Saw, 520f, 140f, 0.08f, lowpass: 0.2f, attack: 0.02f, decay: 1.4f), 0.08f)), 0.5f), 0.05f);
        }
    }
}
