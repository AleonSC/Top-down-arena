using UnityEngine;

namespace Arena
{
    /// The Attack Helicopter's sounds (Core/AttackHelicopter.cs; played by Game/AttackHelicopterLook.cs, not sent):
    ///   HeliRotor  a seamless one-second loop: six deep blade slaps (the "whop") over a low rumble and the turbine's whine,
    ///              on a looping voice of its own (SetHeliRotor), louder as it comes in low, fading as it climbs away.
    ///   HeliShot   one chaingun round: a hard, short crack with a chesty thump, tighter than the SMG; one a round (20 a second).
    ///   HeliSpin   the barrels spinning up on a new target: a rising motor whine with a rattle.
    /// All on the Weapons volume (the Categories() table in SoundBank.cs). Render: Tools/SoundCheck/render.sh Heli
    public partial class SoundBank
    {
        AudioSource heliVoice;  // (made in the constructor)

        void BuildHeli()
        {
            // The rotor: whole cycles in the loop (six slaps, 1100 Hz whine, 6 Hz wobble) so it joins up without a click.
            const float loopLen = 1f, slap = loopLen / 6f;
            var slaps = new float[6][];
            for (int i = 0; i < 6; i++)
                slaps[i] = Mix(
                    Tone(slap, Osc.Sine, 95f, 55f, 0.55f, noise: 0.55f, lowpass: 0.09f, attack: 0.006f, decay: 3.2f),
                    Tone(slap, Osc.Sine, 300f, 160f, 0.08f, noise: 0.9f, lowpass: 0.25f, attack: 0.004f, decay: 5f));
            var rotor = Mix(
                Concat(slaps),
                Tone(loopLen, Osc.Sine, 42f, 42f, 0.12f, noise: 0.6f, lowpass: 0.04f, attack: 0.0005f, decay: 0f),
                Tremolo(Tone(loopLen, Osc.Sine, 1100f, 1100f, 0.025f, attack: 0.0005f, decay: 0f), 6f, 0.4f),
                Tone(loopLen, Osc.Saw, 220f, 220f, 0.02f, lowpass: 0.2f, attack: 0.0005f, decay: 0f));
            Set(Sfx.HeliRotor, Finish(rotor, 0.7f), 0f);

            // A chaingun round: a hard crack, a chesty thump and a very short tail (they overlap at 20 a second).
            SetGun(Sfx.HeliShot, 0.035f, new Gun
            {
                Crack = 0.75f, CrackDecay = 0.0018f, CrackHighpass = 2200f,
                Body = 0.8f, BodyDecay = 0.035f, BodyLowpass = 2200f,
                Thump = 0.75f, ThumpFrom = 170f, ThumpTo = 70f, ThumpDecay = 0.05f,
                Tail = 0.14f, TailDecay = 0.16f, TailLowpass = 900f,
                Reverb = 0.12f, Duration = 0.24f, Peak = 0.5f,
            });

            // The barrels spinning up: a motor whine climbing, a rattle that speeds up, a click as it reaches speed.
            Set(Sfx.HeliSpin, Finish(Mix(
                Tone(0.4f, Osc.Saw, 180f, 900f, 0.1f, lowpass: 0.25f, attack: 0.05f, decay: 0.6f),
                Tremolo(Tone(0.4f, Osc.Square, 600f, 1400f, 0.05f, noise: 0.6f, lowpass: 0.4f, attack: 0.05f, decay: 0.8f), 30f, 0.7f),
                Delay(Tone(0.03f, Osc.Square, 2000f, 1400f, 0.06f, lowpass: 0.6f, decay: 3f), 0.36f)), 0.4f), 0.2f);
        }

        /// The rotor's loop at `level` (0 = off, 1 = full) and `pitch`, every frame (from ArenaApp.Update, via the look).
        public void SetHeliRotor(float level, float pitch)
        {
            if (heliVoice == null)
                return;
            if (heliVoice.clip == null)
            {
                var c = ClipsOf(Sfx.HeliRotor);
                if (c == null)
                    return;
                heliVoice.clip = c[0];
            }
            float v = Muted ? 0f : level * Master * Weapons;
            if (v <= 0.002f)
            {
                if (heliVoice.isPlaying)
                    heliVoice.Stop();
                return;
            }
            heliVoice.volume = Mathf.Min(1f, v);
            heliVoice.pitch = pitch;
            if (!heliVoice.isPlaying)
                heliVoice.Play();
        }
    }
}
