namespace Arena
{
    /// Siege Bell's sound (Core/SiegeBell.cs), sent with the cast (FxKind.SiegeBell). One take, timed to Game.BellTolls:
    ///   tolls 1 and 2: a church bell (the hum, prime, minor third, fifth and octave partials of a real bell), each dying away
    ///   toll 3: the same bell a fourth lower and louder, with a low thump and a breath of air as the shockwave goes out
    /// On the Effects volume. Render: Tools/SoundCheck/render.sh SiegeBell
    public partial class SoundBank
    {
        void BuildSiegeBell()
        {
            const Osc S = Osc.Sine;
            // A church bell's partials over its strike note: hum (0.5), prime (1), minor third (1.183), fifth (1.506),
            // nominal (2), then the upper ones; the lower ones ring longest.
            var ratio = new[] { 0.5f, 1f, 1.183f, 1.506f, 2f, 2.514f, 2.662f, 3.011f };
            var amp = new[] { 1f, 0.8f, 0.5f, 0.45f, 0.35f, 0.25f, 0.2f, 0.15f };
            var life = new[] { 1f, 0.75f, 0.55f, 0.45f, 0.35f, 0.25f, 0.22f, 0.18f };
            float[] small = Partials(1.6f, 196f, 0.32f, ratio, amp, life, 0.004f);
            float[] big = Mix(
                Partials(2.6f, 147f, 0.5f, ratio, amp, life, 0.004f),
                Tone(0.5f, S, 90f, 35f, 0.6f, attack: 0.002f, decay: 3f),                                 // the thump
                Tone(0.6f, S, 700f, 700f, 0.08f, noise: 1f, lowpass: 0.12f, attack: 0.02f, decay: 1.5f)); // air pushed out
            var take = Mix(Delay(small, Game.BellTolls[0]), Delay(small, Game.BellTolls[1]), Delay(big, Game.BellTolls[2]));
            Set(Sfx.SiegeBell, Finish(Room(take, 0.35f), 0.8f), 0.5f);
        }
    }
}
