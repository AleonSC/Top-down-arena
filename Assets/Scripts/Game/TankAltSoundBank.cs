namespace Arena
{
    /// The Tank Soldier's Alt fires (cues raised in Core/TankAlts.cs):
    ///   TankDouble     Double Barrel: both barrels a hair apart, a deep shove of air, then the gun breaking open (a clack),
    ///                  the spent shells tinkling out, and snapping shut.
    ///   TankShellLoad  a shell thumbed into the breech: a short slide and a click.
    ///   TankMineLaunch the Proximity Shell leaving the cannon: a hollow mortar thoomp and a short rising whistle.
    ///   TankMineArm    it lands (a heavy clunk of steel on the floor), then two quick beeps: armed.
    ///   TankMineBurst  it goes off: a sharp crack and shrapnel rattling out (on top of the flak burst's own boom).
    ///   TankGasLaunch  the canister popped from the launcher: a hollow thunk with a tube ring.
    ///   TankGasHiss    the canister clattering down, then the long hiss of the gas venting.
    /// The launches and the blast use the Weapons volume (IsTankAltWeaponSfx).
    public partial class SoundBank
    {
        static bool IsTankAltWeaponSfx(Sfx s) => s == Sfx.TankDouble || s == Sfx.TankMineLaunch || s == Sfx.TankGasLaunch;

        void BuildTankAlt()
        {
            const Osc S = Osc.Sine;
            var barrel = new Gun
            {
                Crack = 0.3f, CrackDecay = 0.003f, CrackHighpass = 1600f,
                Body = 1f, BodyDecay = 0.09f, BodyLowpass = 1000f,
                Thump = 1f, ThumpFrom = 95f, ThumpTo = 38f, ThumpDecay = 0.16f,
                Tail = 0.35f, TailDecay = 0.5f, TailLowpass = 550f,
                Reverb = 0.28f, Duration = 0.9f, Peak = 0.85f,
            };
            // Double Barrel: two blasts 35 ms apart, then the break-open at 0.3 s, the shells, and the snap shut at 0.5 s.
            Set(Sfx.TankDouble, Finish(Mix(
                Gunshot(barrel, 11),
                Delay(Gunshot(barrel, 12), 0.035f),
                Delay(Tone(0.06f, Osc.Square, 900f, 500f, 0.07f, noise: 0.5f, lowpass: 0.5f, decay: 3f), 0.3f),        // the clack of the action opening
                Delay(Tone(0.08f, S, 180f, 120f, 0.1f, lowpass: 0.3f, decay: 3f), 0.3f),
                Delay(Bell(0.18f, 3200f, 0.03f), 0.38f),                                                              // shells tinkling out
                Delay(Bell(0.16f, 3700f, 0.025f), 0.43f),
                Delay(Tone(0.05f, Osc.Square, 1300f, 700f, 0.08f, noise: 0.4f, lowpass: 0.6f, decay: 3.5f), 0.52f),  // and the snap shut
                Delay(Tone(0.06f, S, 220f, 140f, 0.1f, lowpass: 0.3f, decay: 3f), 0.52f)), 0.95f), 0.25f);
            // A shell loaded: a slide of brass and a click.
            Set(Sfx.TankShellLoad, Finish(Mix(
                Tone(0.07f, S, 1200f, 2400f, 0.06f, noise: 0.9f, lowpass: 0.55f, attack: 0.01f, decay: 2f),
                Delay(Tone(0.03f, Osc.Square, 2200f, 1600f, 0.07f, lowpass: 0.6f, decay: 3f), 0.06f),
                Delay(Tone(0.04f, S, 300f, 200f, 0.06f, lowpass: 0.4f, decay: 3f), 0.06f)), 0.3f), 0.1f);
            // The Proximity Shell fired: a mortar's hollow thoomp, a puff, and a short whistle as it climbs.
            Set(Sfx.TankMineLaunch, Finish(Room(Mix(
                Tone(0.3f, S, 140f, 55f, 0.5f, noise: 0.15f, lowpass: 0.3f, decay: 2.2f),
                Tone(0.1f, S, 600f, 300f, 0.15f, noise: 1f, lowpass: 0.4f, decay: 3f),
                Delay(Tone(0.3f, S, 900f, 1700f, 0.05f, noise: 0.2f, lowpass: 0.8f, attack: 0.08f, decay: 1.5f), 0.05f)), 0.2f), 0.75f), 0.2f);
            // It lands: a heavy clunk, a short ring of steel, then two beeps as it arms.
            Set(Sfx.TankMineArm, Finish(Mix(
                Tone(0.12f, S, 160f, 70f, 0.3f, noise: 0.4f, lowpass: 0.3f, decay: 2.5f),
                Bell(0.25f, 1100f, 0.04f),
                Delay(Tone(0.05f, S, 1800f, 1800f, 0.08f, attack: 0.002f, decay: 4f), 0.18f),
                Delay(Tone(0.05f, S, 2400f, 2400f, 0.08f, attack: 0.002f, decay: 4f), 0.26f)), 0.5f), 0.1f);
            // It goes off: a hard crack and shrapnel rattling away.
            Set(Sfx.TankMineBurst, Finish(Mix(
                Tone(0.05f, S, 3000f, 900f, 0.3f, noise: 1f, lowpass: 0.95f, attack: 0.001f, decay: 3.5f),
                Delay(Crackle(0.35f, 14), 0.02f),
                Tone(0.25f, S, 110f, 45f, 0.3f, lowpass: 0.25f, decay: 2.5f)), 0.7f), 0.1f);
            // Tear Gas fired: the launcher's hollow thunk and the tube ringing.
            Set(Sfx.TankGasLaunch, Finish(Mix(
                Tone(0.16f, S, 240f, 110f, 0.35f, noise: 0.25f, lowpass: 0.35f, decay: 2.5f),
                Tone(0.05f, S, 900f, 400f, 0.12f, noise: 1f, lowpass: 0.5f, decay: 3f),
                Bell(0.2f, 520f, 0.05f)), 0.55f), 0.2f);
            // The canister lands: it clatters twice, then vents in a long hiss that fades.
            Set(Sfx.TankGasHiss, Finish(Mix(
                Tone(0.04f, Osc.Square, 1400f, 900f, 0.08f, noise: 0.5f, lowpass: 0.5f, decay: 3.5f),
                Delay(Tone(0.03f, Osc.Square, 1600f, 1000f, 0.05f, noise: 0.5f, lowpass: 0.5f, decay: 3.5f), 0.1f),
                Delay(Tone(1.3f, S, 4000f, 3000f, 0.14f, noise: 1f, lowpass: 0.75f, attack: 0.06f, decay: 1.2f), 0.06f),
                Delay(Tone(0.9f, S, 1500f, 1200f, 0.05f, noise: 1f, lowpass: 0.4f, attack: 0.1f, decay: 1.5f), 0.1f)), 0.45f), 0.15f);
        }
    }
}
