namespace Arena
{
    /// The Fireball's sounds (Deep Roar of Docs/Mockups/fireball-sounds.html): ShotFireball, a low roar with a heavy thump and no highs (the
    /// weapon's Sound, 0.5 s apart), and ShotFireballBurn, a soft muffled flare when it sets an enemy burning (raised in Combat.cs).
    /// Both use the Weapons volume (their names start with Shot). Nothing is above about 1 kHz.
    public partial class SoundBank
    {
        void BuildFireball()
        {
            const Osc S = Osc.Sine;

            // Deep Roar: a falling sine thump under dark noise that swells in and rolls away.
            var roar = new float[4][];
            for (int k = 0; k < 4; k++)
                roar[k] = Finish(Mix(
                    Tone(0.3f, S, 85f - 3f * k, 35f, 0.8f, attack: 0.004f, decay: 2.2f),
                    Tone(0.85f, S, 160f, 110f, 0.55f, noise: 1f, lowpass: 0.05f + 0.004f * k, attack: 0.1f, decay: 3.2f)), 0.5f);
            Set(Sfx.ShotFireball, roar, 0.1f);

            // Burn: a soft low flare with a few dull pops, no crackle. Short gap: a rolling ball ignites several enemies at once.
            var burn = new float[4][];
            for (int k = 0; k < 4; k++)
                burn[k] = Finish(Mix(
                    Tone(0.5f, S, 320f + 20f * k, 220f, 0.4f, noise: 1f, lowpass: 0.07f, attack: 0.012f, decay: 3f),
                    Tone(0.12f, S, 180f, 90f, 0.2f, noise: 0.5f, lowpass: 0.1f, attack: 0.002f, decay: 3f)), 0.3f);
            Set(Sfx.ShotFireballBurn, burn, 0.12f);
        }
    }
}
