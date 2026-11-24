using UnityEngine;

namespace Arena
{
    /// A Literal Tank's sounds (Core/LiteralTank.cs; Game/LiteralTankLook.cs plays them):
    ///   LitTankEngine  a seamless one-second loop: a diesel chugging (eight firing pulses), the treads' clatter (sixteen links) and
    ///                  a faint gearbox whine, on a looping voice of its own (SetLitTankEngine): idling low, revving up as it drives.
    ///   LitTankCannon  the main gun: a deep boom with a hard crack, a chest-thumping low end and a long rolling tail. Each game
    ///                  plays it as the shell is made (Combat.cs, ShotSound), like any shot.
    ///   LitTankFall    the tank dropping out of the sky: a falling whistle (0.6 s, the drop). Sent with the cast.
    ///   LitTankLand    it lands on you: a heavy crunching thud with a clank of steel. Played by each screen when the drop ends.
    ///   LitTankWarn    the last 3 s: a short double beep every half second, your own tank only.
    ///   LitTankHatch   you pop the hatch and hop out: a metal clunk and a spring. Played by each screen as it ends (the wreck's
    ///                  BigExplode is sent by the host).
    /// All on the Weapons volume (the Categories() table in SoundBank.cs). Render: Tools/SoundCheck/render.sh LitTank
    public partial class SoundBank
    {
        AudioSource litTankVoice;  // (made in the constructor)

        void BuildLitTank()
        {
            // The engine: whole cycles in the loop (8 pulses, 16 links, 48 Hz drone, 320 Hz whine) so it joins up without a click.
            const float loopLen = 1f;
            var pulses = new float[8][];
            for (int i = 0; i < 8; i++)
                pulses[i] = Tone(loopLen / 8f, Osc.Saw, 70f, 52f, 0.5f, noise: 0.35f, lowpass: 0.06f, attack: 0.004f, decay: 2.4f);
            var links = new float[16][];
            for (int i = 0; i < 16; i++)
                links[i] = Mix(
                    Tone(0.012f, Osc.Square, 900f, 600f, 0.09f, noise: 0.7f, lowpass: 0.5f, attack: 0.001f, decay: 3f),
                    new float[(int)(loopLen / 16f * Rate)]);
            var engine = Mix(
                Concat(pulses),
                Concat(links),
                Tone(loopLen, Osc.Saw, 48f, 48f, 0.16f, noise: 0.3f, lowpass: 0.05f, attack: 0.0005f, decay: 0f),
                Tremolo(Tone(loopLen, Osc.Triangle, 320f, 320f, 0.018f, attack: 0.0005f, decay: 0f), 4f, 0.5f));
            Set(Sfx.LitTankEngine, Finish(engine, 0.7f), 0f);

            // The main gun: bigger and longer than the Flak Cannon's.
            SetGun(Sfx.LitTankCannon, 0.3f, new Gun
            {
                Crack = 0.6f, CrackDecay = 0.004f, CrackHighpass = 1200f,
                Body = 1f, BodyDecay = 0.2f, BodyLowpass = 900f,
                Thump = 1.2f, ThumpFrom = 75f, ThumpTo = 28f, ThumpDecay = 0.35f,
                Tail = 0.55f, TailDecay = 1.1f, TailLowpass = 380f,
                Reverb = 0.4f, Duration = 1.4f, Peak = 0.95f,
            });

            // The falling whistle: high to low over the drop, getting louder.
            Set(Sfx.LitTankFall, Finish(Mix(
                Tone(0.6f, Osc.Sine, 1800f, 520f, 0.35f, noise: 0.08f, lowpass: 0.6f, attack: 0.25f, decay: 0.15f),
                Tone(0.6f, Osc.Sine, 60f, 45f, 0.2f, noise: 0.8f, lowpass: 0.03f, attack: 0.4f, decay: 0.1f)), 0.55f), 0.3f);

            // Landing: a deep crunch, a steel clank a beat later, gravel settling.
            Set(Sfx.LitTankLand, Finish(Mix(
                Tone(0.5f, Osc.Sine, 90f, 30f, 1f, noise: 0.5f, lowpass: 0.06f, attack: 0.002f, decay: 2.5f),
                Tone(0.25f, Osc.Square, 300f, 120f, 0.25f, noise: 0.85f, lowpass: 0.2f, attack: 0.002f, decay: 4f),
                Delay(Tone(0.3f, Osc.Triangle, 520f, 500f, 0.22f, noise: 0.15f, lowpass: 0.7f, attack: 0.001f, decay: 5f), 0.05f),
                Delay(Tone(0.3f, Osc.Triangle, 760f, 740f, 0.12f, lowpass: 0.7f, attack: 0.001f, decay: 6f), 0.06f),
                Delay(Tone(0.5f, Osc.Sine, 2000f, 1500f, 0.08f, noise: 1f, lowpass: 0.3f, attack: 0.01f, decay: 3f), 0.1f)), 0.9f), 0.3f);

            // The warning: two short beeps.
            var beep = Tone(0.05f, Osc.Square, 1250f, 1250f, 0.12f, lowpass: 0.35f, attack: 0.002f, decay: 0.5f);
            Set(Sfx.LitTankWarn, Finish(Concat(beep, new float[(int)(0.04f * Rate)], beep), 0.35f), 0.2f);

            // The hatch: a clunk, a spring's twang.
            Set(Sfx.LitTankHatch, Finish(Mix(
                Tone(0.12f, Osc.Triangle, 420f, 300f, 0.5f, noise: 0.4f, lowpass: 0.4f, attack: 0.001f, decay: 4f),
                Delay(Tremolo(Tone(0.3f, Osc.Sine, 260f, 240f, 0.2f, attack: 0.002f, decay: 3f), 22f, 0.6f), 0.03f)), 0.5f), 0.2f);
        }

        /// The engine's loop at `level` (0 = off, 1 = full) and `pitch`, every frame (from ArenaApp.Update, via the look).
        public void SetLitTankEngine(float level, float pitch)
        {
            if (litTankVoice == null)
                return;
            if (litTankVoice.clip == null)
            {
                var c = ClipsOf(Sfx.LitTankEngine);
                if (c == null)
                    return;
                litTankVoice.clip = c[0];
            }
            float v = Muted ? 0f : level * Master * Weapons;
            if (v <= 0.002f)
            {
                if (litTankVoice.isPlaying)
                    litTankVoice.Stop();
                return;
            }
            litTankVoice.volume = Mathf.Min(1f, v);
            litTankVoice.pitch = pitch;
            if (!litTankVoice.isPlaying)
                litTankVoice.Play();
        }
    }
}
