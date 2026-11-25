using System;

namespace Arena
{
    /// Orbital Laser's sound (Core/OrbitalLaser.cs), sent with the cast (FxKind.OrbitalLaser). One take, timed to the strike
    /// (Game.OrbLockT, OrbChargeT, OrbHitT, OrbBeamEndT):
    ///   uplink: a quick rising chirp as the spotting line drops
    ///   lock-on: three beeps, one a third of the way through each step of the countdown, each a little higher
    ///   charge: a whine climbing to the impact, with a low hum under it
    ///   strike: a deep boom with a crack on top, then the beam's roar (filtered noise and a buzzing saw) until it fades
    ///   scorch: the glass hissing and crackling as it cools
    /// On the Effects volume. Render: Tools/SoundCheck/render.sh Orbital
    public partial class SoundBank
    {
        void BuildOrbital()
        {
            const Osc S = Osc.Sine, W = Osc.Saw;
            float lockLen = Game.OrbHitT - Game.OrbLockT, beam = Game.OrbBeamT;
            var layers = new System.Collections.Generic.List<float[]>
            {
                // The uplink chirp.
                Tone(0.22f, S, 900f, 2600f, 0.18f, attack: 0.01f, decay: 1.2f),
                // The charge's whine and hum.
                Delay(Tone(Game.OrbHitT - Game.OrbChargeT + 0.05f, W, 220f, 1300f, 0.16f, lowpass: 0.2f, attack: 0.3f, decay: 0.05f), Game.OrbChargeT),
                Delay(Tone(Game.OrbHitT - Game.OrbLockT, S, 55f, 70f, 0.25f, attack: 0.8f, decay: 0.05f), Game.OrbLockT),
                // The impact: a deep boom, a noise crack and a thump.
                Delay(Tone(1.6f, S, 90f, 28f, 0.9f, attack: 0.002f, decay: 2.2f), Game.OrbHitT),
                Delay(Tone(0.5f, S, 3000f, 3000f, 0.55f, noise: 1f, lowpass: 0.5f, attack: 0.001f, decay: 4f), Game.OrbHitT),
                Delay(Tone(0.25f, Osc.Triangle, 160f, 50f, 0.5f, attack: 0.001f, decay: 3f), Game.OrbHitT),
                // The beam's roar while it fires.
                Delay(Tremolo(Tone(beam + 0.3f, S, 600f, 600f, 0.35f, noise: 1f, lowpass: 0.08f, attack: 0.04f, decay: 1.4f), 18f, 0.25f), Game.OrbHitT),
                Delay(Tone(beam + 0.2f, W, 82f, 62f, 0.22f, lowpass: 0.15f, attack: 0.03f, decay: 1.2f), Game.OrbHitT),
                // The glass cooling.
                Delay(Tone(2.4f, S, 5000f, 5000f, 0.08f, noise: 1f, lowpass: 0.35f, attack: 0.2f, decay: 1.5f), Game.OrbBeamEndT - 0.2f),
                Delay(Crackle(2.6f, 60), Game.OrbBeamEndT - 0.1f),
            };
            for (int k = 0; k < 3; k++)  // the lock-on's beeps
                layers.Add(Delay(Tone(0.09f, Osc.Square, 1250f + 180f * k, 1250f + 180f * k, 0.08f, lowpass: 0.4f, attack: 0.002f, decay: 0.6f),
                    Game.OrbLockT + lockLen * k / 3f));
            Set(Sfx.OrbitalLaser, Finish(Room(Mix(layers.ToArray()), 0.35f), 0.85f), 0.5f);
        }
    }
}
