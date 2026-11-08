namespace Arena
{
    /// The Censer's sounds (cues raised in Core/Censer.cs), the Thurible Rite's voice: a bronze censer on a chain, so smoky
    /// low whooshes with a bright chain rattle on every swing, a dull bronze clonk and an ember crackle when the head lands, a
    /// soft rising chime when a heal goes out, a church bell for the Benediction, the chain paying out for Censer Cast and a
    /// gong with a hiss of incense where it lands. Built like the Twin Blades' bank (TwinSoundBank.cs). The swings and hits use the
    /// Weapons volume (IsCenserWeaponSfx).
    public partial class SoundBank
    {
        const float CenserClink = 4300f, CenserBronze = 690f, CenserRoom = 0.22f;

        static bool IsCenserWeaponSfx(Sfx s)
        {
            return s == Sfx.CenserSweep1 || s == Sfx.CenserSweep2 || s == Sfx.CenserWhirl || s == Sfx.CenserHit || s == Sfx.CenserHitHeavy;
        }

        /// `n` chain links clinking over `span` seconds from `start`, each a tiny metallic tick at a slightly different pitch;
        /// `accel` > 0 bunches them toward the end (the chain paying out faster).
        float[] ChainRattle(int n, float start, float span, float vol, float accel = 0f)
        {
            var layers = new float[n][];
            uint r = (uint)(n * 7919 + (int)(span * 1000f));
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                u = u * (1f - accel) + u * u * accel;
                r = r * 1664525u + 1013904223u;
                float jit = ((r >> 8) & 0xffff) / 65536f;
                float f = CenserClink * (0.82f + 0.36f * jit), v = vol * (0.6f + 0.4f * ((r >> 4) & 0xff) / 255f);
                layers[i] = Delay(Tone(0.022f, Osc.Square, f, f * 0.93f, v, lowpass: 0.55f, decay: 4f), start + span * u + 0.006f * jit);
            }
            return Mix(layers);
        }

        /// A crackle of embers: a few sharp grains of noise.
        float[] Crackle(int n, float start, float span, float vol)
        {
            var layers = new float[n][];
            for (int i = 0; i < n; i++)
                layers[i] = Delay(Tone(0.012f, Osc.Sine, 2600f, 1800f, vol * (1f - 0.5f * i / n), noise: 1f, lowpass: 0.9f, decay: 4f), start + span * i / n + 0.007f * (i % 3));
            return Mix(layers);
        }

        void BuildCenser()
        {
            const Osc S = Osc.Sine;
            // Sweep: a smoky whoosh rising across the front, the chain rattling with it. The Return Sweep falls.
            Set(Sfx.CenserSweep1, Finish(Mix(
                Tone(0.28f, S, 220f, 640f, 0.16f, noise: 0.9f, lowpass: 0.32f, attack: 0.06f, decay: 1.6f),
                Tone(0.24f, S, 150f, 210f, 0.05f, lowpass: 0.2f, attack: 0.05f, decay: 1.8f),
                ChainRattle(6, 0.02f, 0.22f, 0.05f)), 0.42f), 0.04f);
            Set(Sfx.CenserSweep2, Finish(Mix(
                Tone(0.28f, S, 640f, 220f, 0.16f, noise: 0.9f, lowpass: 0.32f, attack: 0.06f, decay: 1.6f),
                Tone(0.24f, S, 210f, 150f, 0.05f, lowpass: 0.2f, attack: 0.05f, decay: 1.8f),
                ChainRattle(6, 0.03f, 0.22f, 0.05f)), 0.42f), 0.04f);
            // The Benediction: the chain let out and the censer whirled once round, a swelling whoosh with a pulse as it passes
            // behind you, the chain singing all the way.
            Set(Sfx.CenserWhirl, Finish(Room(Mix(
                Tremolo(Tone(0.62f, S, 260f, 520f, 0.18f, noise: 0.9f, lowpass: 0.3f, attack: 0.18f, decay: 1.1f), 5f, 0.45f),
                Tone(0.6f, S, 110f, 150f, 0.06f, lowpass: 0.15f, attack: 0.15f, decay: 1.2f),
                ChainRattle(12, 0.05f, 0.5f, 0.05f)), CenserRoom * 0.6f), 0.5f), 0.1f);
            // The head lands: a dull bronze clonk, a thud, embers crackling. The heavy one (the Benediction) rings longer.
            Set(Sfx.CenserHit, Finish(Mix(
                Bell(0.3f, CenserBronze, 0.07f),
                Tone(0.1f, S, 220f, 90f, 0.22f, noise: 0.3f, lowpass: 0.3f, decay: 3f),
                Crackle(4, 0.02f, 0.12f, 0.06f)), 0.38f), 0.035f);
            Set(Sfx.CenserHitHeavy, Finish(Room(Mix(
                Bell(0.55f, CenserBronze * 0.8f, 0.09f),
                Tone(0.18f, S, 170f, 60f, 0.3f, noise: 0.25f, lowpass: 0.25f, decay: 2.4f),
                Crackle(7, 0.02f, 0.25f, 0.07f)), CenserRoom * 0.5f), 0.52f), 0.06f);
            // A heal goes out to an ally: a soft rising two-note chime with a breath of air.
            Set(Sfx.CenserBless, Finish(Mix(
                Chime(0.5f, 880f, 0.08f),
                Delay(Chime(0.55f, 1318.5f, 0.07f), 0.07f),
                Tone(0.3f, S, 3000f, 5000f, 0.02f, noise: 1f, lowpass: 0.4f, attack: 0.05f, decay: 2f)), 0.3f), 0.12f);
            // The Benediction's heal: a church bell, struck once, with its octave.
            Set(Sfx.CenserBell, Finish(Room(Mix(
                Bell(1.4f, 392f, 0.14f),
                Bell(1.1f, 784f, 0.05f),
                Chime(0.8f, 1174.7f, 0.03f)), CenserRoom), 0.5f), 0.3f);
            // Censer Cast thrown: a snap and a whoosh, the chain paying out faster and faster.
            Set(Sfx.CenserThrow, Finish(Mix(
                Tone(0.08f, S, 500f, 1500f, 0.18f, noise: 0.85f, lowpass: 0.6f, attack: 0.004f, decay: 2.5f),
                Delay(Tone(0.25f, S, 700f, 300f, 0.1f, noise: 0.9f, lowpass: 0.35f, attack: 0.02f, decay: 1.8f), 0.15f),
                ChainRattle(14, 0.12f, 0.3f, 0.05f, 0.6f)), 0.45f), 0.15f);
            // Where it lands: a gong of bronze, a burst of air and a long hiss of incense spilling out.
            Set(Sfx.CenserLand, Finish(Room(Mix(
                Bell(0.9f, CenserBronze * 0.5f, 0.16f),
                Tone(0.2f, S, 140f, 50f, 0.3f, noise: 0.4f, lowpass: 0.2f, decay: 2.2f),
                Tone(0.25f, S, 900f, 250f, 0.14f, noise: 0.9f, lowpass: 0.5f, decay: 2f),
                Delay(Tone(0.7f, S, 5000f, 3000f, 0.04f, noise: 1f, lowpass: 0.5f, attack: 0.08f, decay: 1.4f), 0.05f),
                Crackle(9, 0.03f, 0.4f, 0.06f)), CenserRoom), 0.58f), 0.12f);
            // Reeled back and caught: the chain rattling in, the head settling with a clink.
            Set(Sfx.CenserCatch, Finish(Mix(
                ChainRattle(8, 0f, 0.16f, 0.04f),
                Delay(Tone(0.04f, Osc.Square, 1600f, 1300f, 0.07f, lowpass: 0.45f, decay: 3f), 0.18f),
                Delay(Bell(0.25f, CenserBronze * 1.3f, 0.03f), 0.18f)), 0.3f), 0.1f);
        }
    }
}
