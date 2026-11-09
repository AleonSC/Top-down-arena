namespace Arena
{
    /// A perfect parry's sound (raised in Core/Parry.cs): a bright two-note ping as the shield takes the shot, a glassy shimmer
    /// over it, and a thin high whip as the needle leaves (Docs/Mockups/parry-perfect-rework.html). On the Effects volume.
    public partial class SoundBank
    {
        void BuildParry()
        {
            const Osc S = Osc.Sine;
            Set(Sfx.PerfectParry, Finish(Room(Mix(
                Tone(0.02f, S, 5000f, 3000f, 0.18f, noise: 1f, lowpass: 0.95f, decay: 4f),
                Bell(0.55f, 2350f, 0.16f),
                Delay(Bell(0.4f, 3520f, 0.06f), 0.02f),
                Delay(Tone(0.09f, S, 2500f, 7000f, 0.08f, noise: 0.9f, lowpass: 0.9f, attack: 0.004f, decay: 2.4f), 0.03f)), 0.12f), 0.55f), 0.1f);
        }
    }
}
