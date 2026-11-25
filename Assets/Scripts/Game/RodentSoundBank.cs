using System;

namespace Arena
{
    /// Rodent Minigun's sounds (Core/RodentMinigun.cs):
    ///   RodentSqueak  one rodent out of the portal: a short, high chirp that bends up then down, over a soft pop. Played with
    ///                 every shot, so it is quiet, has four takes and won't repeat faster than every 45 ms (a buzzing chatter at full fire).
    ///   RodentPortal  the portal tearing open: a rising, wobbling whoosh with a low hum and a tiny squeak at the end. Sent with the cast.
    /// On the Weapons volume (the squeak) and Effects (the portal). Render: Tools/SoundCheck/render.sh Rodent
    public partial class SoundBank
    {
        void BuildRodent()
        {
            var takes = new float[4][];
            for (int v = 0; v < takes.Length; v++)
            {
                float f = 2600f + 420f * v;
                takes[v] = Finish(Mix(
                    Tone(0.035f, Osc.Sine, f, f * 1.35f, 0.5f, attack: 0.002f, decay: 0.6f),
                    Delay(Tone(0.045f, Osc.Sine, f * 1.35f, f * 0.8f, 0.4f, attack: 0.002f, decay: 1.5f), 0.03f),
                    Tone(0.03f, Osc.Sine, 900f, 900f, 0.25f, noise: 1f, lowpass: 0.3f, attack: 0.001f, decay: 3f)), 0.22f);
            }
            Set(Sfx.RodentSqueak, takes, 0.045f);

            var swirl = Tremolo(Tone(0.42f, Osc.Sine, 500f, 1600f, 0.5f, noise: 0.85f, lowpass: 0.12f, attack: 0.08f, decay: 0.9f), 14f, 0.5f);
            Set(Sfx.RodentPortal, Finish(Room(Mix(
                swirl,
                Tone(0.45f, Osc.Triangle, 70f, 110f, 0.45f, attack: 0.05f, decay: 1.2f),
                Delay(Tone(0.08f, Osc.Sine, 2400f, 3600f, 0.25f, attack: 0.003f, decay: 1f), 0.36f)), 0.2f), 0.6f));
        }
    }
}
