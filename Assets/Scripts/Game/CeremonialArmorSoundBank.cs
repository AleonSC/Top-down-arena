namespace Arena
{
    /// Ceremonial Armor's sounds (the tank Ninja's 1st ability, Core/CeremonialArmor.cs), sent by the host:
    ///   CereSummon  a plate summoned: three soft gold temple-bell notes rising (a pentatonic D, E, A) over a shimmer as the sigil opens,
    ///               a falling breath of air as the plate drops, then at Game.CereSummonTime the click into its slot: a dry lacquered
    ///               "tok", a low thump and a small bright ring of gold
    ///   CereBreak   a plate shattering: a sharp lacquer crack, a hollow wooden knock and thud, a dull gold ring, and shards tinkling
    ///               down for half a second (three takes)
    /// On the Effects volume; a short gap stops several breaks in one tick stacking up.
    public partial class SoundBank
    {
        void BuildCeremonialArmor()
        {
            const Osc S = Osc.Sine;
            float land = Game.CereSummonTime;
            var summon = Finish(Room(Mix(
                Bell(0.7f, 587f, 0.22f),                                                                              // D5
                Delay(Bell(0.65f, 659f, 0.2f), 0.07f),                                                                // E5
                Delay(Bell(0.75f, 880f, 0.22f), 0.14f),                                                               // A5
                Tremolo(Tone(0.5f, S, 1760f, 2640f, 0.05f, attack: 0.2f, decay: 1.8f), 12f, 0.7f),                    // the sigil's shimmer
                Delay(Tone(0.3f, S, 3000f, 700f, 0.1f, noise: 1f, lowpass: 0.2f, attack: 0.15f, decay: 1f), land - 0.3f),  // the drop
                Delay(Tone(0.05f, Osc.Triangle, 1900f, 1100f, 0.5f, attack: 0.001f, decay: 4f), land),                // the "tok"
                Delay(Tone(0.03f, S, 4000f, 2000f, 0.25f, noise: 1f, lowpass: 0.6f, attack: 0.001f, decay: 4f), land),
                Delay(Tone(0.12f, S, 170f, 90f, 0.5f, attack: 0.002f, decay: 3f), land),                              // the thump
                Delay(Bell(0.4f, 2349f, 0.1f), land + 0.01f)), 0.18f), 0.55f);                                        // the gold ring
            Set(Sfx.CereSummon, summon, 0.05f);
            Set(Sfx.CereBreak, Takes(3, () =>
            {
                var shards = new float[9][];
                for (int k = 0; k < shards.Length; k++)  // shards of lacquer and gold falling and tinkling
                    shards[k] = Delay(Bell(0.12f + (float)rng.NextDouble() * 0.12f, 2400f + (float)rng.NextDouble() * 2600f, 0.05f + (float)rng.NextDouble() * 0.05f),
                        0.04f + k * 0.05f + (float)rng.NextDouble() * 0.04f);
                return Finish(Room(Mix(
                    Tone(0.06f, S, 6000f, 2500f, 0.7f, noise: 1f, lowpass: 0.7f, attack: 0.001f, decay: 4f),           // the crack
                    Tone(0.14f, Osc.Triangle, 760f, 260f, 0.45f, attack: 0.001f, decay: 3f),                           // the hollow knock
                    Tone(0.2f, S, 140f, 55f, 0.6f, noise: 0.15f, lowpass: 0.3f, attack: 0.002f, decay: 2.5f),          // the thud
                    Delay(Bell(0.55f, 1175f, 0.09f), 0.01f),                                                           // a dull gold ring
                    Mix(shards)), 0.2f), 0.7f);
            }), 0.06f);
            Jitter(0.05f, Sfx.CereSummon);  // (kept close, so the click stays on the landing)
            Jitter(0.1f, Sfx.CereBreak);
        }
    }
}
