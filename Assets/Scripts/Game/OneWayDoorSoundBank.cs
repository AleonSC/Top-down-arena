namespace Arena
{
    /// A One-way piece's door (Core/OneWayDoors.cs) refusing you: played by your own screen when it holds you back (OneWayDoorLook.cs), never sent.
    ///   OneWayBump  a soft padded thunk on the field, then a short two-note "nope" buzz dropping a step
    /// On the Effects volume. Render: Tools/SoundCheck/render.sh OneWay
    public partial class SoundBank
    {
        void BuildOneWayDoor()
        {
            Set(Sfx.OneWayBump, Finish(Mix(
                Tone(0.12f, Osc.Sine, 190f, 80f, 0.55f, attack: 0.002f, decay: 4f),                                  // the thunk
                Tone(0.06f, Osc.Square, 1100f, 500f, 0.07f, noise: 0.4f, lowpass: 0.35f, attack: 0.001f, decay: 6f),  // the field's crackle
                Delay(Tone(0.07f, Osc.Square, 330f, 330f, 0.09f, lowpass: 0.3f, attack: 0.004f, decay: 3f), 0.03f),   // "no-
                Delay(Tone(0.09f, Osc.Square, 247f, 240f, 0.09f, lowpass: 0.3f, attack: 0.004f, decay: 3f), 0.11f)), 0.5f), 0.04f);  // -pe"
        }
    }
}
