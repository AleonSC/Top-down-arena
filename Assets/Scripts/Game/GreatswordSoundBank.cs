namespace Arena
{
    /// The Greatsword's sounds (cues raised in Core/Greatsword.cs and Core/Knight.cs), in the Moonlit Crescent's voice: steel and a thin
    /// ring, like the Katana's set but heavier. The overhead rend's wind-up and the Rend use the Weapons volume; the guard's are Effects.
    public partial class SoundBank
    {
        const float GreatRing = 2100f, GreatRoom = 0.25f;

        void BuildGreatsword()
        {
            const Osc S = Osc.Sine;
            // The overhead rend's blade lifting (0.25 s, before the cut): a low swell rising, a rush of air, a scrape of steel.
            Set(Sfx.GreatWindup, Finish(Mix(
                Reverse(Tone(0.25f, Osc.Saw, 90f, 250f, 0.1f, lowpass: 0.3f, decay: 1f)),
                Tone(0.25f, S, 400f, 1100f, 0.09f, noise: 0.85f, lowpass: 0.55f, attack: 0.15f, decay: 1f),
                Delay(Bell(0.3f, GreatRing * 0.8f, 0.05f), 0.12f)), 0.4f), 0.2f);
            // The guard going up: the blade scraping across, a thud as it sets, a ring.
            Set(Sfx.GreatRaise, Finish(Room(Mix(
                Tone(0.2f, S, 500f, 1500f, 0.12f, noise: 0.85f, lowpass: 0.6f, attack: 0.03f, decay: 1.4f),
                Tone(0.14f, S, 150f, 70f, 0.3f, lowpass: 0.3f, decay: 2.5f),
                Delay(Bell(0.5f, GreatRing * 0.9f, 0.08f), 0.05f)), GreatRoom * 0.6f), 0.45f), 0.15f);
            // A bullet stopped on the arc: a hard clang with a short ring.
            Set(Sfx.GreatBlock, Finish(Mix(
                Tone(0.05f, S, 3000f, 1500f, 0.22f, noise: 1f, lowpass: 0.9f, decay: 3f),
                Bell(0.3f, GreatRing, 0.1f),
                Bell(0.22f, GreatRing * 1.5f, 0.04f),
                Tone(0.08f, S, 260f, 120f, 0.16f, lowpass: 0.3f, decay: 3f)), 0.35f), 0.05f);
            // Temper full: a rising chime and a low bell.
            Set(Sfx.GreatFull, Finish(Room(Mix(
                Notes(new[] { 1047f, 1319f, 1568f, 2093f }, 0.06f, S, 0.2f, 1f),
                Delay(Bell(0.8f, 1047f, 0.12f), 0.05f),
                Tone(0.2f, S, 260f, 200f, 0.1f, lowpass: 0.3f, decay: 2f)), GreatRoom), 0.45f), 0.3f);
            // The Rend: a tearing rush, a boom under it, a bell that outlasts it.
            Set(Sfx.GreatRend, Finish(Room(Mix(
                Tone(0.3f, S, 2800f, 380f, 0.3f, noise: 0.85f, lowpass: 0.9f, attack: 0.001f, decay: 2f),
                Tone(0.45f, S, 110f, 35f, 0.45f, noise: 0.2f, lowpass: 0.15f, decay: 1.6f),
                Tone(0.05f, S, 3300f, 1700f, 0.25f, noise: 1f, lowpass: 0.95f, decay: 3f),
                Delay(Bell(0.8f, GreatRing * 1.2f, 0.1f), 0.05f)), GreatRoom * 0.9f), 0.7f), 0.2f);
        }
    }
}
