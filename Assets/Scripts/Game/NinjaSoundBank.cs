namespace Arena
{
    /// The Ninja DPS's sounds (cues raised in Core/Ninja.cs), the Shinobi set of Docs/Mockups/ninja-dps-sounds.html.
    /// ShotKunai uses the Weapons volume (it starts with Shot); the rest are Effects.
    public partial class SoundBank
    {
        void BuildNinja()
        {
            const Osc S = Osc.Sine;

            // Kunai throw: a quick whoosh, dark air first, brighter as it passes.
            Set(Sfx.ShotKunai, Finish(Mix(
                Tone(0.22f, S, 500f, 300f, 0.22f, noise: 1f, lowpass: 0.14f, attack: 0.045f, decay: 1.8f),
                Delay(Tone(0.2f, S, 500f, 300f, 0.2f, noise: 1f, lowpass: 0.4f, attack: 0.03f, decay: 2.2f), 0.04f),
                Delay(Tone(0.14f, S, 500f, 300f, 0.14f, noise: 1f, lowpass: 0.8f, attack: 0.02f, decay: 3f), 0.08f),
                Delay(Tone(0.12f, S, 900f, 350f, 0.05f, lowpass: 0.5f, attack: 0.02f, decay: 2f), 0.04f)), 0.5f), 0.12f);

            // Blink to the Kunai's mark: a reversed breath pulls in, a pop as you leave, a soft ring where you land.
            Set(Sfx.NinjaBlink, Finish(Room(Mix(
                Reverse(Tone(0.14f, S, 600f, 1800f, 0.14f, noise: 0.8f, lowpass: 0.6f, decay: 1.2f)),
                Delay(Tone(0.05f, S, 2600f, 900f, 0.2f, noise: 1f, lowpass: 0.9f, decay: 3f), 0.13f),
                Delay(Tone(0.12f, S, 140f, 60f, 0.28f, lowpass: 0.3f, decay: 2.5f), 0.13f),
                Delay(Bell(0.4f, 3640f, 0.06f), 0.15f)), 0.15f), 0.5f), 0.15f);

            // Shadow Step: a cloud of smoke, then two whooshes round the enemy's sides, one rising, one falling.
            Set(Sfx.NinjaStepCast, Finish(Room(Mix(
                Tone(0.3f, S, 400f, 150f, 0.18f, noise: 1f, lowpass: 0.25f, attack: 0.02f, decay: 1.4f),
                Tone(0.3f, S, 1200f, 2400f, 0.04f, noise: 0.4f, lowpass: 0.7f, attack: 0.1f, decay: 1f),
                Tone(0.14f, S, 110f, 50f, 0.3f, lowpass: 0.2f, decay: 2f),
                Delay(Tone(0.3f, S, 350f, 1500f, 0.12f, noise: 0.9f, lowpass: 0.5f, attack: 0.04f, decay: 1f), 0.1f),
                Delay(Tone(0.3f, S, 1500f, 350f, 0.1f, noise: 0.9f, lowpass: 0.5f, attack: 0.04f, decay: 1f), 0.15f)), 0.21f), 0.5f), 0.3f);

            // Shadow Step's strike: starts 0.3 s in, as the two halves meet behind the enemy: a pop of air, a slice, a clean ring.
            Set(Sfx.NinjaStepStrike, Finish(Room(Delay(Mix(
                Reverse(Tone(0.1f, S, 500f, 2000f, 0.12f, noise: 0.8f, lowpass: 0.5f, decay: 1f)),
                Delay(Tone(0.07f, S, 2400f, 700f, 0.26f, noise: 0.9f, lowpass: 0.85f, decay: 3f), 0.09f),
                Delay(Tone(0.2f, S, 200f, 70f, 0.36f, noise: 0.3f, lowpass: 0.25f, decay: 2.5f), 0.09f),
                Delay(Tone(0.04f, S, 3400f, 1800f, 0.22f, noise: 1f, lowpass: 0.95f, decay: 3f), 0.09f),
                Delay(Bell(0.6f, 3360f, 0.09f), 0.1f)), 0.3f), 0.15f), 0.6f), 0.3f);

            // Vanish: a dull crack, a puff of smoke and a fading breath as you slip from view.
            Set(Sfx.NinjaVanish, Finish(Room(Mix(
                Tone(0.05f, S, 1500f, 500f, 0.22f, noise: 1f, lowpass: 0.8f, decay: 3f),
                Tone(0.45f, S, 500f, 120f, 0.2f, noise: 1f, lowpass: 0.2f, attack: 0.015f, decay: 1.3f),
                Tone(0.2f, S, 130f, 48f, 0.34f, lowpass: 0.2f, decay: 2.2f),
                Reverse(Tone(0.3f, S, 900f, 200f, 0.06f, noise: 0.9f, lowpass: 0.4f, decay: 1f))), 0.24f), 0.5f), 0.3f);

            // Blade Fan opens: six rising ticks as the kunai fly out into orbit, a rush and a thump.
            var ticks = new float[6][];
            for (int i = 0; i < 6; i++)
                ticks[i] = Delay(Tone(0.05f, S, 1500f + i * 260f, 1300f + i * 220f, 0.16f, noise: 0.5f, lowpass: 0.8f, decay: 3f), i * 0.025f);
            Set(Sfx.NinjaFanOpen, Finish(Room(Mix(Mix(ticks),
                Tone(0.25f, S, 500f, 1100f, 0.1f, noise: 0.9f, lowpass: 0.5f, attack: 0.03f, decay: 1.2f),
                Tone(0.14f, S, 130f, 60f, 0.26f, lowpass: 0.25f, decay: 2.5f)), 0.12f), 0.5f), 0.3f);

            // Blade Fan flies out: a rising rush, a bright burst and ticks as the kunai scatter.
            var scatter = new float[5][];
            for (int i = 0; i < 5; i++)
                scatter[i] = Delay(Tone(0.04f, S, 2600f - i * 200f, 1800f - i * 150f, 0.13f, noise: 0.7f, lowpass: 0.85f, decay: 3f), 0.1f + i * 0.02f);
            Set(Sfx.NinjaFanBurst, Finish(Room(Mix(Mix(scatter),
                Reverse(Tone(0.16f, S, 400f, 1800f, 0.14f, noise: 0.8f, lowpass: 0.6f, decay: 1f)),
                Delay(Tone(0.3f, S, 2400f, 500f, 0.28f, noise: 0.9f, lowpass: 0.9f, attack: 0.002f, decay: 2f), 0.14f),
                Delay(Tone(0.2f, S, 150f, 50f, 0.4f, lowpass: 0.2f, decay: 2f), 0.14f),
                Delay(Bell(0.7f, 3360f, 0.1f), 0.15f)), 0.18f), 0.65f), 0.3f);

            // Death Lotus: the arena darkens: a low swell that pulls in, a held breath, a deep note and a faint bell.
            Set(Sfx.NinjaLotusCast, Finish(Room(Mix(
                Reverse(Tone(0.7f, S, 320f, 90f, 0.12f, noise: 0.8f, lowpass: 0.25f, decay: 1.2f)),
                Tone(0.9f, S, 70f, 48f, 0.34f, lowpass: 0.15f, attack: 0.3f, decay: 1.2f),
                Tone(0.7f, S, 110f, 150f, 0.05f, lowpass: 0.2f, attack: 0.3f, decay: 1f),
                Delay(Bell(0.9f, 880f, 0.05f), 0.5f)), 0.24f), 0.5f), 0.5f);
        }
    }
}
