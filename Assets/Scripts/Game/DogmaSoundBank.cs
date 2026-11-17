namespace Arena
{
    /// Dragon's Dogma's sounds (the Tank Ninja's second ultimate, Core/DragonsDogma.cs), sent by the host with what they go with:
    ///   DogmaRoar    the cast: a rush of wind swelling up out of the sigil into a deep, fluttering dragon roar (a growling
    ///                saw with a throat of breath over a sub rumble), with two spirit bells ringing out over it, in a big room
    ///   DogmaDevour  a regular enemy eaten: the jaws snapping shut (a hard click and a thump), a deep gulp sliding down, and a
    ///                thin shimmer of its soul rising
    ///   DogmaBite    a heavy or boss only bitten: the snap, a crunch of armour and a heavy thud, no gulp
    ///   DogmaHome    it coils back in: a rush of wind pulled into you, settling into a low hum and a soft bell
    /// The bites are short with a small gap and some jitter, so a string of them through a crowd reads as a feeding frenzy, not
    /// a wall of noise. On the Effects volume. Render: Tools/SoundCheck/render.sh Dogma
    public partial class SoundBank
    {
        void BuildDogma()
        {
            const Osc S = Osc.Sine;
            var roarDry = Mix(
                Reverse(Tone(0.55f, S, 3200f, 700f, 0.22f, noise: 1f, lowpass: 0.25f, decay: 2.5f)),                    // wind swelling up
                Delay(Mix(
                    Tremolo(Tone(1.5f, Osc.Saw, 125f, 60f, 0.5f, noise: 0.25f, lowpass: 0.12f, attack: 0.12f, decay: 1.4f), 26f, 0.5f),  // the growl
                    Tone(1.4f, S, 750f, 280f, 0.32f, noise: 1f, lowpass: 0.1f, attack: 0.15f, decay: 1.6f),               // breath in the throat
                    Tone(1.3f, S, 58f, 38f, 0.45f, attack: 0.1f, decay: 1.2f)), 0.42f),                                   // sub rumble
                Delay(Bell(1.5f, 880f, 0.11f), 0.55f),                                                                   // spirit bells
                Delay(Bell(1.3f, 1318f, 0.07f), 0.75f));
            Set(Sfx.DogmaRoar, Finish(Room(roarDry, 0.4f), 0.75f), 0.3f);

            var snap = Mix(
                Tone(0.045f, Osc.Square, 2400f, 900f, 0.3f, noise: 0.7f, lowpass: 0.5f, attack: 0.001f, decay: 3f),
                Tone(0.12f, S, 170f, 60f, 0.7f, attack: 0.001f, decay: 3f));
            var devour = Mix(
                snap,
                Delay(Tone(0.2f, S, 330f, 105f, 0.45f, attack: 0.02f, decay: 1.5f), 0.07f),                              // the gulp
                Delay(Tremolo(Tone(0.42f, S, 1500f, 2500f, 0.08f, attack: 0.05f, decay: 2f), 14f, 0.6f), 0.12f));      // its soul rising
            Set(Sfx.DogmaDevour, Finish(Room(devour, 0.18f), 0.55f), 0.05f);

            var bite = Mix(
                snap,
                Tone(0.18f, S, 1800f, 480f, 0.5f, noise: 1f, lowpass: 0.35f, attack: 0.001f, decay: 3f),                // armour crunching
                Delay(Tone(0.22f, S, 110f, 44f, 0.85f, attack: 0.002f, decay: 2.5f), 0.02f));                          // a heavy thud
            Set(Sfx.DogmaBite, Finish(Room(bite, 0.15f), 0.6f), 0.08f);

            var home = Mix(
                Reverse(Tone(0.6f, S, 2400f, 500f, 0.2f, noise: 1f, lowpass: 0.2f, decay: 2.5f)),                      // pulled in
                Delay(Tone(0.5f, S, 72f, 46f, 0.5f, attack: 0.01f, decay: 2f), 0.58f),                                  // settling
                Delay(Bell(0.9f, 660f, 0.1f), 0.6f));
            Set(Sfx.DogmaHome, Finish(Room(home, 0.3f), 0.5f), 0.2f);

            Jitter(0.1f, Sfx.DogmaDevour, Sfx.DogmaBite);
        }
    }
}
