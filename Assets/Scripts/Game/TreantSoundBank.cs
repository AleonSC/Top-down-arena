namespace Arena
{
    /// The Treant Rod's sounds (Docs/Mockups/treant-rod-sounds-seeds.html): the root lash's Leaf Rustle (SwingRoot, raised when the weapon
    /// fires), the Treant's seed shot as Pod Pop (ShotSeed, raised when a seed spawns) and a seed landing as Tick (ShotSeedHit, raised when
    /// it is removed). All three use the Weapons volume (their names start with Swing/Shot) and are kept quiet: seeds fire every 0.4 s all wave.
    public partial class SoundBank
    {
        void BuildTreant()
        {
            const Osc S = Osc.Sine;

            // Leaf Rustle: bright noise fluttering like leaves, with a soft low swish under it.
            var rustle = new float[4][];
            for (int k = 0; k < 4; k++)
                rustle[k] = Finish(Mix(
                    Tremolo(Tone(0.4f, S, 3600f + 250f * k, 5600f, 0.16f, noise: 1f, lowpass: 0.65f, attack: 0.04f, decay: 1.8f), 22f + 3f * k, 0.55f),
                    Tone(0.3f, S, 500f, 1400f, 0.1f, noise: 1f, lowpass: 0.12f, attack: 0.05f, decay: 1.6f)), 0.42f);
            Set(Sfx.SwingRoot, rustle, 0.1f);

            // Pod Pop: a small bubbly pop that falls in pitch, with a click of noise on the front.
            var pop = new float[4][];
            for (int k = 0; k < 4; k++)
                pop[k] = Finish(Mix(
                    Tone(0.12f, S, 620f + 25f * k, 210f, 0.24f, attack: 0.002f, decay: 3f),
                    Tone(0.02f, S, 2200f, 2200f, 0.12f, noise: 1f, lowpass: 0.6f, attack: 0.001f, decay: 3f)), 0.24f);
            Set(Sfx.ShotSeed, pop, 0.05f);

            // Tick: a dry tap, like a seed hitting a wooden board. Very short, so a volley never smears.
            var tick = new float[3][];
            for (int k = 0; k < 3; k++)
                tick[k] = Finish(Mix(
                    Tone(0.07f, S, 420f - 20f * k, 260f, 0.2f, attack: 0.001f, decay: 3f),
                    Tone(0.015f, S, 3000f, 3000f, 0.14f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 3f)), 0.22f);
            Set(Sfx.ShotSeedHit, tick, 0.03f);
        }
    }
}
