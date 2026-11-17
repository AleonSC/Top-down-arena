using UnityEngine;

namespace Arena
{
    /// Deployable Cover's sounds (the Tank Soldier's 4th Space ability, Core/DeployableCover.cs). Each game plays them itself
    /// (DeployableCoverLook.cs `UpdateCoverSound`), so nothing is sent, timed to the look's Stake Drop:
    ///   CoverDeploy  the throw and the build: a short whoosh as the canister flies (0 - 0.22 s), a heavy thump as it lands,
    ///                a steel clang as the wall springs up, two clunks as the arms swing back, a quick ratchet of four bolts
    ///                driven in (0.52 - 0.61 s), and a faint electric buzz as the light strip flickers on
    ///   CoverBreak   the cover bursting: a sharp crack, a big ringing crash of plate with a low thud under it, then plates
    ///                clattering down round it and a little grit
    ///   CoverFold    folding away: two light clunks as the arms swing in, a hiss as the wall sinks, a soft click at the end
    ///   CoverTing    a shot ringing off the plate: a short bright ping (a few takes, so a volley doesn't repeat one note)
    /// On the Effects volume; quieter for an ally's cover and with distance.
    public partial class SoundBank
    {
        /// Struck steel plate: inharmonic partials that die away fast, the high ones first.
        static float[] Plate(float dur, float f, float vol) =>
            Partials(dur, f, vol, new[] { 1f, 1.47f, 2.09f, 2.56f, 3.91f }, new[] { 1f, 0.7f, 0.45f, 0.3f, 0.18f }, new[] { 1f, 0.7f, 0.5f, 0.35f, 0.2f }, 0.002f);

        void BuildCover()
        {
            const Osc S = Osc.Sine;
            var deploy = Finish(Mix(
                Reverse(Tone(0.2f, S, 900f, 2400f, 0.16f, noise: 1f, lowpass: 0.25f, attack: 0.002f, decay: 2.2f)),  // the canister's whoosh
                Delay(Tone(0.32f, S, 120f, 42f, 0.75f, attack: 0.002f, decay: 3f), 0.22f),                           // the landing thump
                Delay(Tone(0.12f, S, 900f, 200f, 0.3f, noise: 1f, lowpass: 0.2f, attack: 0.001f, decay: 3f), 0.22f),  // grit off it
                Delay(Plate(0.55f, 310f, 0.42f), 0.28f),                                                               // the wall springing up
                Delay(Mix(Plate(0.2f, 205f, 0.3f), Tone(0.12f, S, 140f, 70f, 0.35f, decay: 3f)), 0.40f),              // an arm swinging back
                Delay(Mix(Plate(0.2f, 190f, 0.3f), Tone(0.12f, S, 130f, 65f, 0.35f, decay: 3f)), 0.47f),              // the other arm
                Delay(Mix(Tone(0.03f, Osc.Square, 2200f, 1500f, 0.12f, noise: 0.5f, lowpass: 0.5f, decay: 3f), Plate(0.08f, 1250f, 0.12f)), 0.52f),  // bolts
                Delay(Mix(Tone(0.03f, Osc.Square, 2100f, 1450f, 0.12f, noise: 0.5f, lowpass: 0.5f, decay: 3f), Plate(0.08f, 1180f, 0.12f)), 0.55f),
                Delay(Mix(Tone(0.03f, Osc.Square, 2300f, 1550f, 0.12f, noise: 0.5f, lowpass: 0.5f, decay: 3f), Plate(0.08f, 1320f, 0.12f)), 0.58f),
                Delay(Mix(Tone(0.03f, Osc.Square, 2000f, 1400f, 0.12f, noise: 0.5f, lowpass: 0.5f, decay: 3f), Plate(0.08f, 1100f, 0.12f)), 0.61f),
                Delay(Tremolo(Tone(0.18f, Osc.Saw, 120f, 120f, 0.06f, lowpass: 0.15f, attack: 0.01f, decay: 1.2f), 28f, 0.9f), 0.56f)),  // the light's buzz
                0.7f);
            var clatter = new float[7][];
            for (int k = 0; k < clatter.Length; k++)
            {
                float at = 0.14f + 0.7f * k / clatter.Length + (float)rng.NextDouble() * 0.07f;
                float f = 480f + (float)rng.NextDouble() * 700f;
                clatter[k] = Delay(Mix(Plate(0.16f, f, 0.16f * (1f - 0.08f * k)), Tone(0.06f, S, 160f, 90f, 0.12f, decay: 3f)), at);
            }
            var brk = Finish(Mix(
                Tone(0.06f, S, 4000f, 1200f, 0.5f, noise: 1f, lowpass: 0.6f, attack: 0.001f, decay: 3f),            // the crack
                Plate(0.9f, 180f, 0.5f), Plate(0.7f, 415f, 0.3f),                                                   // the crash of plate
                Tone(0.45f, S, 95f, 38f, 0.7f, attack: 0.002f, decay: 2.6f),                                        // the thud under it
                Delay(Tone(0.6f, S, 1400f, 300f, 0.12f, noise: 1f, lowpass: 0.15f, attack: 0.01f, decay: 2f), 0.1f),  // grit
                Mix(clatter)), 0.75f);                                                                               // plates coming down
            var fold = Finish(Mix(
                Mix(Plate(0.14f, 260f, 0.22f), Tone(0.08f, S, 150f, 80f, 0.25f, decay: 3f)),                        // an arm folding in
                Delay(Mix(Plate(0.14f, 240f, 0.22f), Tone(0.08f, S, 140f, 75f, 0.25f, decay: 3f)), 0.07f),          // the other
                Delay(Tone(0.26f, S, 1800f, 500f, 0.12f, noise: 1f, lowpass: 0.2f, attack: 0.04f, decay: 1.6f), 0.16f),  // the wall sinking
                Delay(Mix(Tone(0.025f, Osc.Square, 1600f, 1100f, 0.1f, noise: 0.4f, lowpass: 0.5f, decay: 3f), Plate(0.06f, 900f, 0.08f)), 0.42f)),  // latched
                0.45f);
            var tings = new float[4][];
            for (int k = 0; k < tings.Length; k++)
            {
                float f = 1300f + 260f * k + (float)rng.NextDouble() * 120f;
                tings[k] = Finish(Mix(Plate(0.14f, f, 0.3f), Tone(0.012f, S, 5000f, 2500f, 0.2f, noise: 1f, lowpass: 0.7f, attack: 0.0005f, decay: 3f)), 0.22f);
            }
            Set(Sfx.CoverDeploy, deploy, 0.05f);
            Set(Sfx.CoverFold, fold, 0.05f);
            Set(Sfx.CoverTing, tings, 0.07f);  // (at most one every 0.07 s: a volley stays a patter, not a roar)
            Set(Sfx.CoverBreak, brk, 0.05f);
            Jitter(0.04f, Sfx.CoverDeploy);  // (kept close, so its hits stay on the look's beats)
            Jitter(0.06f, Sfx.CoverBreak, Sfx.CoverFold, Sfx.CoverTing);
        }
    }
}
