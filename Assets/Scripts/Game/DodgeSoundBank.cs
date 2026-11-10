namespace Arena
{
    /// The Dodge's sound (raised in Core/Dodge.cs, your own screen): Cloth Swish, a soft fabric rush with no tonal part, quiet enough to
    /// hear a hundred times a minute (option B of Docs/Mockups/dodge-sounds.html). On the Effects volume.
    public partial class SoundBank
    {
        void BuildDodge()
        {
            const Osc S = Osc.Sine;
            Set(Sfx.DodgeSwish, Finish(Mix(
                Tone(0.22f, S, 2000f, 900f, 0.2f, noise: 1f, lowpass: 0.5f, attack: 0.04f, decay: 1.8f),
                Tone(0.2f, S, 1200f, 300f, 0.2f, noise: 1f, lowpass: 0.14f, attack: 0.02f, decay: 1.8f)), 0.45f), 0.1f);
        }
    }
}
