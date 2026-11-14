using UnityEngine;

namespace Arena
{
    /// The Warden's prisoners (Docs/Approaches.md): the Thug moves like a Brute but has its own body (a hunched bruiser
    /// with taped fists); the Shiv is a Lunger with a taped shank held out front, keeping its body, spikes and lunge lane.
    public partial class ArenaApp
    {
        static readonly Rgba PrisonInk = new Rgba(0.15f, 0.12f, 0.16f, 0.9f), PrisonSteel = new Rgba(0.82f, 0.86f, 0.9f);

        static readonly Rgba ThugJumpsuit = new Rgba(1f, 0.55f, 0.2f), ThugTattoo = new Rgba(0.25f, 0.35f, 0.55f, 0.75f),
            ThugTape = new Rgba(0.92f, 0.9f, 0.84f);

        /// The Thug, in the Rioter's top-down style (WardenLook.cs): a huge hunched back in a tank top (`col`, which reddens
        /// as it enrages) over an orange jumpsuit, bare tattooed arms, giant taped fists swinging as it lumbers, half a cuff.
        void DrawThugBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float p = Mathf.Sin(t * 5f + e.Id * 1.7f);  // a slow, heavy stride
            V2 body = pos - f * r * 0.1f;
            Ellipse(body, r * 0.75f + 1.6f, r * 1.15f + 1.6f, e.Facing, RioterInk, 16);
            Ellipse(body, r * 0.75f, r * 1.15f, e.Facing, ThugJumpsuit, 16);  // the jumpsuit, tied at the waist
            Ellipse(body + f * r * 0.08f, r * 0.64f, r * 0.92f, e.Facing, col, 16);  // the tank top
            Circle(pos - f * r * 0.35f, r * 0.35f, col.Mix(RioterInk, 0.25f), 12);  // the hump of its back
            for (int k = -1; k <= 1; k += 2)
            {
                V2 shoulder = pos + s * (k * r * 0.95f);
                V2 elbow = shoulder + f * r * 0.35f + s * (k * r * 0.35f);
                V2 fist = elbow + f * r * (0.6f + 0.12f * k * p) - s * (k * r * 0.12f);
                ThugArm(shoulder, elbow, 7f, k < 0);
                ThugArm(elbow, fist, 6f, false);
                Circle(fist, r * 0.34f, RioterInk, 12);
                Circle(fist, r * 0.27f, ThugTape, 12);  // the taped fist
                Line(fist - s * r * 0.2f, fist + s * r * 0.2f, 1.2f, new Rgba(0.6f, 0.58f, 0.52f));
                if (k > 0)
                    Arc(elbow + (fist - elbow) * 0.75f, r * 0.2f, 2.4f, RioterChain, 10);  // a cuff, its other half gone
            }
            RioterHead(pos + f * r * 0.4f, r * 0.3f, f, new Rgba(0.55f, 0.42f, 0.34f, 0.6f));  // shaved
            Line(pos + f * r * 0.62f - s * r * 0.2f, pos + f * r * 0.62f + s * r * 0.2f, 2.4f, RioterInk);  // the brow
        }

        /// A bare arm (a tank top), with a blue tattoo on the upper arm if asked.
        void ThugArm(V2 a, V2 b, float w, bool tattoo)
        {
            Line(a, b, w + 2.6f, RioterInk);
            Line(a, b, w, RioterSkin);
            if (tattoo)
                Line(a + (b - a) * 0.25f, a + (b - a) * 0.5f, w * 0.55f, ThugTattoo);
        }

        void DrawShivDetails(Enemy e, V2 pos, float r, Rgba col)
        {
            V2 f = V2.FromAngle(e.Facing);
            V2 tip = pos + f * r * 2.3f;
            Line(pos + f * r * 0.4f, tip, 3f, PrisonSteel);  // the blade
            Line(pos + f * r * 0.4f, pos + f * r * 1.0f, 5f, PrisonInk);  // the taped grip
            Circle(tip, 2.5f, new Rgba(1f, 1f, 1f, 0.9f), 6);
        }
    }
}
