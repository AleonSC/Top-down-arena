namespace Arena
{
    /// The Twin Rifle grenade tubes' sounds (cues raised in Core/TwinGrenades.cs and Core/Combat.cs): a pump-style clack-clack
    /// as a hit loads a tube, a hollow 40 mm "thoonk" as Alt fires one, and a crack, deep boom and rumble where it bursts.
    /// All on the Weapons volume (the Categories() table in SoundBank.cs).
    public partial class SoundBank
    {
        void BuildTwinRifle()
        {
            const Osc S = Osc.Sine, Q = Osc.Square;
            // A tube loaded: two quick clicks, each with a small metal body.
            Set(Sfx.TwinNadeLoad, Finish(Mix(
                Tone(0.03f, Q, 1300f, 1000f, 0.18f, noise: 0.4f, lowpass: 0.5f, decay: 3f),
                Tone(0.05f, S, 240f, 150f, 0.18f, lowpass: 0.3f, decay: 2.5f),
                Delay(Tone(0.03f, Q, 1700f, 1300f, 0.2f, noise: 0.4f, lowpass: 0.55f, decay: 3f), 0.07f),
                Delay(Tone(0.06f, S, 280f, 170f, 0.2f, lowpass: 0.3f, decay: 2.5f), 0.07f)), 0.38f), 0.05f);
            // A grenade fired: a hollow pop with an air puff.
            Set(Sfx.TwinNadeLaunch, Finish(Room(Mix(
                Tone(0.12f, S, 260f, 90f, 0.4f, lowpass: 0.35f, attack: 0.002f, decay: 2.2f),
                Tone(0.05f, Osc.Triangle, 520f, 300f, 0.15f, lowpass: 0.4f, decay: 2.5f),
                Tone(0.18f, S, 1200f, 500f, 0.14f, noise: 0.95f, lowpass: 0.3f, attack: 0.004f, decay: 2f)), 0.15f), 0.55f), 0.05f);
            // Its blast: a crack, a deep boom and a long rumble.
            Set(Sfx.TwinNadeBoom, Finish(Room(Mix(
                Tone(0.04f, S, 3500f, 1500f, 0.25f, noise: 1f, lowpass: 0.9f, decay: 3f),
                Tone(0.35f, S, 110f, 38f, 0.5f, noise: 0.3f, lowpass: 0.2f, decay: 1.8f),
                Tone(0.7f, S, 220f, 60f, 0.25f, noise: 1f, lowpass: 0.08f, attack: 0.01f, decay: 1.6f)), 0.2f), 0.7f), 0.05f);
        }
    }
}
