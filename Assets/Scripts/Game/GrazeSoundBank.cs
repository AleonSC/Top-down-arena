namespace Arena
{
    /// The Graze's sound (Core/Graze.cs, your own screen only): a quick airy "tsip", a falling glassy tick over a breath of
    /// high noise, three takes so a run of grazes doesn't buzz. Short and quiet: it can play several times a second. On the
    /// Effects volume. Render: Tools/SoundCheck/render.sh Graze.
    public partial class SoundBank
    {
        void BuildGraze()
        {
            var takes = new float[3][];
            float[] hz = { 0.95f, 1f, 1.06f };
            for (int k = 0; k < 3; k++)
                takes[k] = Finish(Mix(
                    Tone(0.05f, Osc.Sine, 4200f * hz[k], 2600f * hz[k], 0.1f, attack: 0.001f, decay: 3f),
                    Tone(0.08f, Osc.Sine, 7000f, 4000f, 0.12f, noise: 1f, lowpass: 0.7f, attack: 0.004f, decay: 2.4f)), 0.16f);
            Set(Sfx.Graze, takes, 0.04f);
        }
    }
}
