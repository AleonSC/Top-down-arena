namespace Arena
{
    /// The Arc Knuckles' sounds (cues raised in Core/ArcKnuckles.cs and Core/Parry.cs), in the Arc Coil's voice: short dry punches
    /// with a crackle of static on top, a deep thump and a zap for the slams, the coils whining up when a perfect parry arms the
    /// Counter Hammer. All of them use the Weapons volume (IsKnuckleWeaponSfx).
    public partial class SoundBank
    {
        const float KnuckleZap = 2400f;

        static bool IsKnuckleWeaponSfx(Sfx s)
        {
            return s == Sfx.KnuckleJab || s == Sfx.KnuckleHook || s == Sfx.KnuckleUpper || s == Sfx.KnuckleSlam || s == Sfx.KnuckleCounter
                || s == Sfx.KnuckleArm || s == Sfx.KnuckleRocket || s == Sfx.KnuckleRocketHit;
        }

        void BuildKnuckles()
        {
            const Osc S = Osc.Sine;
            // A jab or cross: a quick dry whoosh, a thick knock, a tick of static.
            Set(Sfx.KnuckleJab, Finish(Mix(
                Tone(0.07f, S, 700f, 1300f, 0.14f, noise: 0.9f, lowpass: 0.6f, attack: 0.004f, decay: 2.6f),
                Delay(Tone(0.07f, S, 190f, 90f, 0.22f, noise: 0.25f, lowpass: 0.3f, decay: 3f), 0.06f),
                Delay(Tone(0.03f, Osc.Square, KnuckleZap, KnuckleZap * 0.7f, 0.04f, noise: 0.5f, lowpass: 0.7f, decay: 3f), 0.07f)), 0.4f), 0.03f);
            // The hook: a wider swing round, then the knock.
            Set(Sfx.KnuckleHook, Finish(Mix(
                Tone(0.16f, S, 450f, 1000f, 0.15f, noise: 0.85f, lowpass: 0.5f, attack: 0.05f, decay: 1.6f),
                Delay(Tone(0.08f, S, 170f, 80f, 0.26f, noise: 0.25f, lowpass: 0.3f, decay: 3f), 0.15f),
                Delay(Tone(0.04f, Osc.Square, KnuckleZap * 1.1f, KnuckleZap * 0.6f, 0.05f, noise: 0.5f, lowpass: 0.7f, decay: 3f), 0.16f)), 0.45f), 0.04f);
            // The uppercut: a low scoop rising, a hard knock, a zap that climbs.
            Set(Sfx.KnuckleUpper, Finish(Mix(
                Tone(0.14f, S, 250f, 900f, 0.14f, noise: 0.8f, lowpass: 0.45f, attack: 0.04f, decay: 1.6f),
                Delay(Tone(0.09f, S, 220f, 70f, 0.28f, noise: 0.3f, lowpass: 0.3f, decay: 2.6f), 0.13f),
                Delay(Tone(0.12f, Osc.Square, KnuckleZap * 0.6f, KnuckleZap * 1.4f, 0.05f, noise: 0.4f, lowpass: 0.6f, decay: 2.5f), 0.14f)), 0.48f), 0.05f);
            // The Twin Hammer lands: a deep thump in the floor, stone cracking, a wide crackle of static.
            Set(Sfx.KnuckleSlam, Finish(Room(Mix(
                Tone(0.3f, S, 110f, 40f, 0.4f, noise: 0.2f, lowpass: 0.2f, decay: 2f),
                Tone(0.18f, S, 1400f, 300f, 0.2f, noise: 1f, lowpass: 0.7f, decay: 2.4f),
                Tone(0.22f, Osc.Square, KnuckleZap, KnuckleZap * 0.4f, 0.05f, noise: 0.6f, lowpass: 0.7f, decay: 2f)), 0.25f), 0.62f), 0.12f);
            // The Counter Hammer: the same slam, heavier, with a bright bell over it.
            Set(Sfx.KnuckleCounter, Finish(Room(Mix(
                Tone(0.36f, S, 100f, 34f, 0.46f, noise: 0.2f, lowpass: 0.2f, decay: 1.8f),
                Tone(0.2f, S, 1600f, 280f, 0.22f, noise: 1f, lowpass: 0.75f, decay: 2.2f),
                Tone(0.26f, Osc.Square, KnuckleZap * 1.2f, KnuckleZap * 0.4f, 0.06f, noise: 0.6f, lowpass: 0.7f, decay: 2f),
                Bell(0.7f, 1320f, 0.08f),
                Bell(0.6f, 1980f, 0.05f)), 0.3f), 0.68f), 0.15f);
            // The Rocket Dash: a spark catches, the thruster roars and falls away as you fly.
            Set(Sfx.KnuckleRocket, Finish(Mix(
                Tone(0.03f, Osc.Square, KnuckleZap, KnuckleZap * 0.8f, 0.05f, noise: 0.6f, lowpass: 0.7f, decay: 3f),
                Delay(Tone(0.32f, S, 160f, 90f, 0.22f, noise: 0.95f, lowpass: 0.35f, attack: 0.02f, decay: 1.4f), 0.02f),
                Delay(Tone(0.26f, S, 900f, 2200f, 0.08f, noise: 0.9f, lowpass: 0.6f, attack: 0.03f, decay: 1.8f), 0.02f)), 0.5f), 0.1f);
            // The fist lands at the end of it: a heavy knock with a burst of static.
            Set(Sfx.KnuckleRocketHit, Finish(Room(Mix(
                Tone(0.12f, S, 200f, 60f, 0.36f, noise: 0.3f, lowpass: 0.3f, decay: 2.4f),
                Tone(0.06f, S, 2400f, 700f, 0.18f, noise: 0.9f, lowpass: 0.85f, decay: 3f),
                Tone(0.14f, Osc.Square, KnuckleZap * 1.2f, KnuckleZap * 0.5f, 0.05f, noise: 0.6f, lowpass: 0.7f, decay: 2.2f)), 0.2f), 0.55f), 0.06f);
            // Armed: the coils whine up and click.
            Set(Sfx.KnuckleArm, Finish(Mix(
                Tone(0.22f, S, 600f, 2200f, 0.08f, lowpass: 0.6f, attack: 0.08f, decay: 1.4f),
                Tone(0.22f, Osc.Square, 900f, 3000f, 0.03f, noise: 0.3f, lowpass: 0.5f, attack: 0.08f, decay: 1.4f),
                Delay(Tone(0.03f, Osc.Square, 2600f, 2200f, 0.06f, lowpass: 0.6f, decay: 3f), 0.22f)), 0.4f), 0.08f);
        }
    }
}
