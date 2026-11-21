namespace Arena
{
    /// The main menu's and the saved-runs screen's sounds in the holo look (the picks from Docs/Mockups/menu-sound-lab.html). All played
    /// by your own screen, never sent, on the Effects volume. Render: Tools/SoundCheck/render.sh Menu (and Bay, Run, Title, Neon, Disk).
    ///   MenuHover    a key under the mouse, and a saved run's row: a tiny glassy phosphor tick (replaces the tape-head tick)
    ///   MenuPlay     Play (and New run): a thunk, then a power chord that opens up
    ///   MenuKey      Join game and Settings: a two-step digital chirp
    ///   MenuQuit     Quit: the pop and fizz of a tube switching off (the game waits for it before closing)
    ///   MenuEject    the drive's eject key: three falling beeps (then DiskEject, now a whoosh, as the disk kicks out)
    ///   TitleKey/Space/Enter  each title letter landing: the clack of a split-flap board (Enter: with a low thock)
    ///   MenuRooms    the outline rooms drawing themselves: quiet random bleeps
    ///   NeonSpark    the title's neon flickering: two tiny electric ticks
    ///   BayVault     the Play move: four access beeps, the doors sliding on air, a chord as the screen seats (the old servo cues are not played)
    ///   BayBack      Back to the menu: a gentle falling interval
    ///   RunContinue  Continue a saved run: a warm major chord
    ///   RunDelArm    DEL's first click: one sharp dissonant blip
    ///   RunDelete    REALLY?: falling bleeps scattering away
    public partial class SoundBank
    {
        void BuildMenuHolo()
        {
            var hover = new float[3][];
            float[] hz = { 0.96f, 1f, 1.05f };
            for (int k = 0; k < 3; k++)
                hover[k] = Finish(Mix(Tone(0.035f, Osc.Sine, 3200f * hz[k], 2900f * hz[k], 0.14f, attack: 0.001f, decay: 3f),
                    Tone(0.02f, Osc.Sine, 6000f, 6000f, 0.05f, noise: 1f, lowpass: 0.8f, attack: 0.001f, decay: 3f)), 0.11f);
            Set(Sfx.MenuHover, hover, 0.04f);

            // a thunk and a click, then four saw voices (A2 E3 A3 E4) whose dark layer gives way to a brighter one: the filter opening
            float[] power = { 110f, 164.8f, 220f, 329.6f };
            var chord = new float[power.Length * 2][];
            for (int k = 0; k < power.Length; k++)
            {
                chord[2 * k] = Delay(Tone(0.5f, Osc.Saw, power[k], power[k], 0.06f, lowpass: 0.04f, attack: 0.04f, decay: 1.6f), 0.05f);
                chord[2 * k + 1] = Delay(Tone(0.5f, Osc.Saw, power[k], power[k], 0.05f, lowpass: 0.3f, attack: 0.25f, decay: 1.4f), 0.05f);
            }
            Set(Sfx.MenuPlay, Finish(Mix(Mix(chord), Tone(0.12f, Osc.Sine, 140f, 55f, 0.45f, attack: 0.002f, decay: 3f),
                Tone(0.03f, Osc.Sine, 3000f, 3000f, 0.15f, noise: 1f, lowpass: 0.5f, attack: 0.001f, decay: 3f)), 0.42f), 0.15f);

            Set(Sfx.MenuKey, Finish(Mix(Tone(0.04f, Osc.Square, 880f, 880f, 0.06f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f),
                Delay(Tone(0.05f, Osc.Square, 1320f, 1320f, 0.06f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f), 0.045f)), 0.17f), 0.05f);

            Set(Sfx.MenuQuit, Finish(Mix(Tone(0.04f, Osc.Sine, 300f, 300f, 0.3f, noise: 1f, lowpass: 0.1f, attack: 0.001f, decay: 3f),
                Tone(0.5f, Osc.Sine, 15000f, 8000f, 0.03f, attack: 0.002f, decay: 1.5f),
                Delay(Tone(0.5f, Osc.Sine, 5000f, 1500f, 0.06f, noise: 1f, lowpass: 0.3f, attack: 0.01f, decay: 2f), 0.03f)), 0.3f), 0.2f);

            Set(Sfx.MenuEject, Finish(Mix(Tone(0.05f, Osc.Square, 1320f, 1320f, 0.05f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f),
                Delay(Tone(0.05f, Osc.Square, 990f, 990f, 0.05f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f), 0.07f),
                Delay(Tone(0.05f, Osc.Square, 660f, 660f, 0.05f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f), 0.14f)), 0.2f), 0.1f);
            Set(Sfx.DiskEject, Finish(Tone(0.25f, Osc.Sine, 3000f, 600f, 0.1f, noise: 0.85f, lowpass: 0.3f, attack: 0.02f, decay: 1.5f), 0.2f), 0.1f);

            // the split-flap clack (four takes, played in turn); the space softer, the last letter with a low thock
            var flaps = new float[4][];
            float[] fhz = { 0.9f, 1f, 1.12f, 0.96f };
            for (int k = 0; k < 4; k++)
                flaps[k] = Finish(Mix(Tone(0.018f, Osc.Sine, 2400f * fhz[k], 2400f * fhz[k], 0.2f, noise: 1f, lowpass: 0.55f, attack: 0.001f, decay: 3f),
                    Tone(0.02f, Osc.Square, 600f * fhz[k], 600f * fhz[k], 0.03f, lowpass: 0.3f, attack: 0.001f, decay: 3f)), 0.18f);
            Set(Sfx.TitleKey, flaps, 0f);
            Set(Sfx.TitleSpace, Finish(Tone(0.018f, Osc.Sine, 1800f, 1800f, 0.15f, noise: 1f, lowpass: 0.4f, attack: 0.001f, decay: 3f), 0.1f), 0f);
            Set(Sfx.TitleEnter, Finish(Mix(flaps[1], Tone(0.1f, Osc.Sine, 110f, 55f, 0.35f, attack: 0.002f, decay: 3f)), 0.26f), 0f);

            var bleeps = new float[14][];
            for (int k = 0; k < bleeps.Length; k++)
            {
                float f = 1200f + (float)rng.NextDouble() * 1800f;
                bleeps[k] = Delay(Tone(0.03f, Osc.Square, f, f, 0.02f, lowpass: 0.5f, attack: 0.002f, decay: 1.5f), k * 0.09f);
            }
            Set(Sfx.MenuRooms, Finish(Mix(bleeps), 0.07f), 1f);

            Set(Sfx.NeonSpark, Finish(Mix(Tone(0.012f, Osc.Sine, 6000f, 6000f, 0.2f, noise: 1f, lowpass: 0.9f, attack: 0.001f, decay: 3f),
                Delay(Tone(0.012f, Osc.Sine, 6500f, 6500f, 0.13f, noise: 1f, lowpass: 0.9f, attack: 0.001f, decay: 3f), 0.12f)), 0.09f), 0.5f);

            // four rising access beeps, the doors sliding on air, a chord (C4 G4 C5) as the screen seats
            float[] access = { 1046f, 1318f, 1568f, 2093f };
            var vault = new float[access.Length + 4][];
            for (int k = 0; k < access.Length; k++)
                vault[k] = Delay(Tone(0.05f, Osc.Square, access[k], access[k], 0.03f, lowpass: 0.5f, attack: 0.002f, decay: 1.5f), k * 0.06f);
            vault[4] = Delay(Tone(0.5f, Osc.Sine, 600f, 2500f, 0.07f, noise: 0.9f, lowpass: 0.25f, attack: 0.15f, decay: 1.5f), 0.28f);
            vault[5] = Delay(Tone(0.4f, Osc.Triangle, 261.6f, 261.6f, 0.05f, attack: 0.004f, decay: 2f), 0.82f);
            vault[6] = Delay(Tone(0.4f, Osc.Triangle, 392f, 392f, 0.05f, attack: 0.004f, decay: 2f), 0.82f);
            vault[7] = Delay(Tone(0.4f, Osc.Triangle, 523.3f, 523.3f, 0.05f, attack: 0.004f, decay: 2f), 0.82f);
            Set(Sfx.BayVault, Finish(Mix(vault), 0.3f), 0.3f);

            Set(Sfx.BayBack, Finish(Mix(Tone(0.12f, Osc.Triangle, 659.3f, 659.3f, 0.1f, attack: 0.003f, decay: 2f),
                Delay(Tone(0.2f, Osc.Triangle, 440f, 440f, 0.1f, attack: 0.003f, decay: 2f), 0.09f)), 0.22f), 0.2f);

            float[] major = { 261.6f, 329.6f, 392f, 523.3f };
            var cont = new float[major.Length][];
            for (int k = 0; k < major.Length; k++)
                cont[k] = Delay(Tone(0.45f, Osc.Triangle, major[k], major[k], 0.06f, attack: 0.004f, decay: 2f), k * 0.02f);
            Set(Sfx.RunContinue, Finish(Mix(cont), 0.3f), 0.2f);

            Set(Sfx.RunDelArm, Finish(Mix(Tone(0.1f, Osc.Saw, 1100f, 1100f, 0.06f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f),
                Tone(0.1f, Osc.Saw, 1166f, 1166f, 0.06f, lowpass: 0.4f, attack: 0.002f, decay: 1.5f)), 0.2f), 0.1f);

            var dissolve = new float[10][];
            for (int k = 0; k < dissolve.Length; k++)
            {
                float f = 2000f - k * 170f + (float)rng.NextDouble() * 100f;
                dissolve[k] = Delay(Tone(0.03f, Osc.Square, f, f, 0.04f, lowpass: 0.6f, attack: 0.002f, decay: 1.5f), k * 0.025f);
            }
            Set(Sfx.RunDelete, Finish(Mix(dissolve), 0.22f), 0.2f);
        }
    }
}
