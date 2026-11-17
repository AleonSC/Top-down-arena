namespace Arena
{
    /// Water Ward's sounds (the Healer Elementalist's 4th Space ability, Core/WaterWard.cs), sent by the host with the effect:
    ///   WwSplash  a drop splashing on an enemy: a round "bloop" rising, a short wet slap of spray, droplets pattering back down
    ///   WwBurst   a drop eating a shot: the shot hissing out to steam, a soft "pff" of mist billowing, a bubble pop and fine spray
    ///   WwCast    the call: a cool shimmer rising as the drops gather above, a falling whistle, then one plop per drop as it lands
    ///             (at Game.WwFall s and WwFallGap after), each with a ring of plips
    /// Small and bright, so a ring of drops going off one after another reads as a patter, not a wall of noise. On the Effects volume;
    /// a short gap stops a crowd of them in one tick stacking up.
    public partial class SoundBank
    {
        void BuildWaterWard()
        {
            const Osc S = Osc.Sine;
            var splash = Finish(Mix(
                Tone(0.11f, S, 240f, 820f, 0.55f, attack: 0.002f, decay: 3.2f),                                    // the bloop
                Tone(0.07f, Osc.Triangle, 520f, 1500f, 0.18f, attack: 0.001f, decay: 3.5f),                         // its bright top
                Tone(0.24f, S, 2600f, 900f, 0.4f, noise: 1f, lowpass: 0.3f, attack: 0.002f, decay: 3f),            // the slap of spray
                Delay(Plips(0.35f, 6, 0.1f, 900f, 2000f), 0.07f)), 0.5f);                                           // droplets falling back
            var burst = Finish(Mix(
                Tone(0.32f, S, 6000f, 3500f, 0.26f, noise: 1f, lowpass: 0.55f, attack: 0.002f, decay: 2.4f),       // the shot hissing out
                Delay(Tone(0.55f, S, 900f, 300f, 0.3f, noise: 1f, lowpass: 0.07f, attack: 0.09f, decay: 1.6f), 0.02f),  // the mist billowing
                Tone(0.08f, S, 420f, 1100f, 0.32f, attack: 0.002f, decay: 3.5f),                                    // the bubble's pop
                Delay(Plips(0.45f, 8, 0.07f, 1300f, 2600f), 0.05f)), 0.48f);                                        // fine spray
            float land = Game.WwFall, gap = Game.WwFallGap;
            var cast = Finish(Mix(
                Tone(0.4f, S, 520f, 1040f, 0.16f, attack: 0.12f, decay: 2.2f),                                     // the shimmer gathering
                Tremolo(Tone(0.4f, S, 1560f, 2080f, 0.07f, attack: 0.15f, decay: 2.4f), 11f, 0.6f),
                Delay(Tone(0.3f, S, 1900f, 900f, 0.06f, noise: 0.4f, lowpass: 0.4f, attack: 0.12f, decay: 1.2f), land - 0.3f),  // falling
                Delay(Tone(0.1f, S, 210f, 620f, 0.5f, attack: 0.002f, decay: 3.4f), land),                         // first drop lands
                Delay(Tone(0.14f, S, 2200f, 900f, 0.16f, noise: 1f, lowpass: 0.3f, attack: 0.002f, decay: 3.2f), land),
                Delay(Plips(0.2f, 3, 0.07f, 1100f, 2100f), land + 0.04f),
                Delay(Tone(0.1f, S, 250f, 720f, 0.45f, attack: 0.002f, decay: 3.4f), land + gap),                  // and the second
                Delay(Tone(0.14f, S, 2400f, 950f, 0.14f, noise: 1f, lowpass: 0.3f, attack: 0.002f, decay: 3.2f), land + gap),
                Delay(Plips(0.2f, 3, 0.06f, 1200f, 2300f), land + gap + 0.04f)), 0.5f);
            Set(Sfx.WwCast, cast, 0.05f);
            Set(Sfx.WwSplash, splash, 0.05f);
            Set(Sfx.WwBurst, burst, 0.05f);
            Jitter(0.12f, Sfx.WwSplash, Sfx.WwBurst);
            Jitter(0.04f, Sfx.WwCast);  // (kept close, so its plops stay on the landings)
        }
    }
}
