using UnityEngine;

namespace Arena
{
    /// The Nova and Turret abilities' sounds (Core/SentryNova.cs). They aren't Sfx (that byte enum is nearly full): each game
    /// plays them when the effect arrives (SentryNovaLook.cs `UpdateSentryNovaLook`), quieter for an ally's cast like a sent cue.
    ///   NovaBurst     a deep whump under a bright crack, an outward rush and a scatter of high chimes (the 24 shots' own
    ///                 ShotNova pops ride on top)
    ///   TurretDeploy  a short whistle as the pod drops, the landing thud, the servo whir of the legs and head unfolding,
    ///                 three latch clicks and two rising boot beeps
    ///   TurretFold    two falling beeps, the servo winding down, a power-off drop and a soft clunk as it folds
    /// On the Effects volume.
    public partial class SoundBank
    {
        public enum AbilityCue { NovaBurst, TurretDeploy, TurretFold }

        AudioClip[] abilityCues;
        readonly float[] abilityCueLast = { -10f, -10f, -10f };

        void BuildSentryNova()
        {
            const Osc S = Osc.Sine, Q = Osc.Square, W = Osc.Saw, T = Osc.Triangle;
            var nova = Finish(Mix(
                Tone(0.4f, S, 150f, 42f, 0.6f, attack: 0.002f, decay: 2.4f),                                    // the whump
                Tone(0.12f, W, 2600f, 500f, 0.2f, noise: 0.5f, lowpass: 0.6f, attack: 0.001f, decay: 3f),       // the crack
                Tone(0.55f, S, 3200f, 650f, 0.16f, noise: 1f, lowpass: 0.22f, attack: 0.012f, decay: 1.6f),      // the rush outward
                Chime(0.9f, 784f, 0.12f), Chime(0.9f, 1175f, 0.08f),
                Delay(Sparkle(0.45f, 7, 0.05f, 2400f, 4800f), 0.04f)), 0.55f);
            var deploy = Finish(Mix(
                Tone(0.16f, S, 1500f, 520f, 0.05f, noise: 0.6f, lowpass: 0.3f, attack: 0.1f, decay: 0.4f),     // the drop
                Delay(Mix(
                    Tone(0.2f, S, 115f, 48f, 0.65f, attack: 0.002f, decay: 2.6f),                               // the thud
                    Tone(0.07f, Q, 720f, 280f, 0.12f, noise: 0.6f, lowpass: 0.4f, attack: 0.001f, decay: 4f)), Game.SentryLand),
                Delay(Tremolo(Tone(0.3f, W, 170f, 430f, 0.13f, lowpass: 0.2f, attack: 0.03f, decay: 0.7f), 38f, 0.35f), 0.2f),  // servo
                Delay(Tone(0.025f, Q, 2300f, 1800f, 0.08f, attack: 0.001f, decay: 3f), 0.42f),                  // three latches
                Delay(Tone(0.025f, Q, 2300f, 1800f, 0.08f, attack: 0.001f, decay: 3f), 0.46f),
                Delay(Tone(0.025f, Q, 2300f, 1800f, 0.08f, attack: 0.001f, decay: 3f), 0.5f),
                Delay(Tone(0.06f, T, 1760f, 1760f, 0.12f, attack: 0.003f, decay: 1f), 0.58f),                   // boot beeps
                Delay(Tone(0.09f, T, 2350f, 2350f, 0.12f, attack: 0.003f, decay: 1f), 0.66f)), 0.5f);
            var fold = Finish(Mix(
                Tone(0.07f, T, 2350f, 2350f, 0.11f, attack: 0.003f, decay: 1f),                                 // falling beeps
                Delay(Tone(0.1f, T, 1568f, 1568f, 0.11f, attack: 0.003f, decay: 1f), 0.09f),
                Delay(Tremolo(Tone(0.36f, W, 430f, 140f, 0.12f, lowpass: 0.2f, attack: 0.02f, decay: 0.9f), 34f, 0.35f), 0.12f),  // servo down
                Delay(Tone(0.28f, S, 900f, 110f, 0.1f, attack: 0.004f, decay: 2f), 0.16f),                     // power off
                Delay(Mix(
                    Tone(0.14f, S, 140f, 70f, 0.4f, attack: 0.002f, decay: 2.6f),                               // the clunk
                    Tone(0.2f, S, 900f, 300f, 0.06f, noise: 1f, lowpass: 0.15f, attack: 0.01f, decay: 2f)), 0.44f)), 0.42f);
            abilityCues = new AudioClip[3];
            float[][] data = { nova, deploy, fold };
            for (int i = 0; i < 3; i++)
            {
                abilityCues[i] = AudioClip.Create("ability_cue_" + i, data[i].Length, 1, Rate, false);
                abilityCues[i].SetData(data[i], 0);
            }
        }

        /// One of these cues (ally: someone else's cast, quieter; quiet 0..1: further away).
        public void PlayAbilityCue(AbilityCue cue, bool ally, float quiet, float now)
        {
            int i = (int)cue;
            if (Muted || abilityCues == null || now - abilityCueLast[i] < 0.05f)
                return;
            float volume = Master * Effects * (ally ? 0.45f : 1f) * (1f - quiet);
            if (volume <= 0.001f)
                return;
            abilityCueLast[i] = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = 1f + ((float)rng.NextDouble() - 0.5f) * 0.03f;
            voice.PlayOneShot(abilityCues[i], volume);
        }
    }
}
