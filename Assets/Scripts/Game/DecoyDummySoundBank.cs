namespace Arena
{
    /// Decoy Dummy's sounds (the Healer Soldier's 4th choice, Core/DecoyDummy.cs). Each game plays them itself
    /// (DecoyDummyLook.cs `UpdateDecoySound`), so nothing is sent, timed to the look:
    ///   DecoyThrow   a short whoosh as the canister flies (0 - 0.3 s), a soft thump as it lands, a rising hiss of air as it
    ///                puffs up, and a rubbery boing as it springs to full size
    ///   DecoySqueak  a shot or a bite landing on it: a short squeak of rubber (a few takes, so a volley doesn't repeat one note)
    ///   DecoyPop     a loud pop with a confetti rustle, then a raspberry fluttering off as the empty skin zips away
    ///   DecoyFold    deflating: a long sighing hiss of air, dropping, with a low flubber under it
    /// On the Effects volume; quieter for an ally's dummy and with distance.
    public partial class SoundBank
    {
        void BuildDecoy()
        {
            const Osc S = Osc.Sine;
            var thr = Finish(Mix(
                Reverse(Tone(0.26f, S, 900f, 2200f, 0.14f, noise: 1f, lowpass: 0.25f, attack: 0.002f, decay: 2.2f)),  // the canister's whoosh
                Delay(Tone(0.2f, S, 150f, 55f, 0.55f, attack: 0.002f, decay: 3f), 0.3f),                               // the landing thump
                Delay(Tremolo(Tone(0.38f, S, 300f, 1100f, 0.2f, noise: 1f, lowpass: 0.18f, attack: 0.03f, decay: 0.6f), 22f, 0.35f), 0.32f),  // air rushing in
                Delay(Tremolo(Tone(0.16f, Osc.Triangle, 380f, 640f, 0.32f, lowpass: 0.5f, attack: 0.004f, decay: 2.4f), 34f, 0.5f), 0.66f)),  // the boing
                0.65f);
            var squeaks = new float[4][];
            for (int k = 0; k < squeaks.Length; k++)
            {
                float f = 480f + 90f * k + (float)rng.NextDouble() * 60f;
                squeaks[k] = Finish(Mix(
                    Tremolo(Tone(0.09f, Osc.Square, f, f * 1.35f, 0.22f, lowpass: 0.3f, attack: 0.004f, decay: 2.5f), 45f, 0.6f),
                    Tone(0.05f, S, 180f, 110f, 0.25f, decay: 3f)), 0.35f);  // the dull smack under it
            }
            var flutter = new float[9][];
            for (int k = 0; k < flutter.Length; k++)
            {
                float f = 150f - 9f * k + (k % 2 == 0 ? 25f : -15f);
                flutter[k] = Tone(0.06f, Osc.Saw, f, f * 0.92f, 0.22f * (1f - 0.06f * k), lowpass: 0.25f, attack: 0.004f, decay: 1.4f);
            }
            var pop = Finish(Mix(
                Tone(0.035f, S, 3000f, 900f, 0.8f, noise: 1f, lowpass: 0.7f, attack: 0.0005f, decay: 3f),  // the pop
                Tone(0.18f, S, 260f, 60f, 0.7f, attack: 0.001f, decay: 3f),                                 // its body
                Delay(Tone(0.4f, S, 6000f, 3000f, 0.1f, noise: 1f, lowpass: 0.6f, attack: 0.01f, decay: 2f), 0.03f),  // confetti rustle
                Delay(Tremolo(Concat(flutter), 30f, 0.7f), 0.08f)),                                         // the raspberry as it zips off
                0.8f);
            var fold = Finish(Mix(
                Tone(0.55f, S, 2400f, 700f, 0.18f, noise: 1f, lowpass: 0.22f, attack: 0.02f, decay: 1.1f),  // the hiss, dropping
                Tremolo(Tone(0.45f, Osc.Saw, 120f, 62f, 0.16f, lowpass: 0.12f, attack: 0.03f, decay: 1.4f), 18f, 0.6f)),  // the flubber
                0.45f);
            Set(Sfx.DecoyThrow, thr, 0.05f);
            Set(Sfx.DecoySqueak, squeaks, 0.08f);  // (at most one every 0.08 s: a volley stays a patter)
            Set(Sfx.DecoyPop, pop, 0.05f);
            Set(Sfx.DecoyFold, fold, 0.05f);
            Jitter(0.04f, Sfx.DecoyThrow);
            Jitter(0.08f, Sfx.DecoySqueak, Sfx.DecoyPop, Sfx.DecoyFold);
        }
    }
}
