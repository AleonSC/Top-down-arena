using UnityEngine;

namespace Arena
{
    /// The Warden's prisoners (Docs/Approaches.md): the Thug moves like a Brute but has its own body (a hunched bruiser
    /// with taped fists); the Shiv moves like a Lunger (its lunge lane too) but is Hooded or Twin Shanks.
    public partial class ArenaApp
    {
        static readonly Rgba ThugJumpsuit = new Rgba(1f, 0.55f, 0.2f), ThugTattoo = new Rgba(0.25f, 0.35f, 0.55f, 0.75f),
            ThugTape = new Rgba(0.92f, 0.9f, 0.84f);

        /// The Thug, in the Rioter's top-down style (WardenLook.cs): a huge hunched back in a tank top (`col`, which reddens
        /// as it enrages) over an orange jumpsuit, bare tattooed arms, giant taped fists swinging as it lumbers, half a cuff.
        void DrawThugBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            float p = Mathf.Sin(t * 5f + e.Id * 1.7f);  // a slow, heavy stride
            float w = Game.PunchWindT(e), j = Game.PunchJabT(e);  // its charged punch (ThugPunch.cs), with its right fist
            float ext = j < 0f ? 0f : 1f - Mathf.Max(0f, j - 0.6f) * 1.5f;
            V2 body = pos - f * r * 0.1f;
            Oval(body, r * 0.75f + 1.6f, r * 1.15f + 1.6f, e.Facing, RioterInk);
            Oval(body, r * 0.75f, r * 1.15f, e.Facing, ThugJumpsuit);  // the jumpsuit, tied at the waist
            Oval(body + f * r * 0.08f, r * 0.64f, r * 0.92f, e.Facing, col);  // the tank top
            Circle(pos - f * r * 0.35f, r * 0.35f, col.Mix(RioterInk, 0.25f), 28);  // the hump of its back
            for (int k = -1; k <= 1; k += 2)
            {
                V2 shoulder = pos + s * (k * r * 0.95f);
                V2 elbow = shoulder + f * r * 0.35f + s * (k * r * 0.35f);
                V2 fist = elbow + f * r * (0.6f + 0.12f * k * p) - s * (k * r * 0.12f);
                if (k > 0 && (w > 0f || ext > 0f))
                {   // drawn back and out over the wind-up, then thrown straight ahead
                    elbow = shoulder + f * r * (0.35f - 0.6f * w + 0.45f * ext) + s * (r * (0.35f + 0.2f * w - 0.25f * ext));
                    fist = elbow + f * r * (0.6f - 0.3f * w + 0.6f * ext) - s * (r * (0.12f + 0.3f * ext));
                }
                Cap(shoulder, elbow, 7f + 2.6f, RioterInk);  // one outline under both halves: a round elbow
                Cap(elbow, fist, 6f + 2.6f, RioterInk);
                ThugArm(shoulder, elbow, 7f, k < 0);
                ThugArm(elbow, fist, 6f, false);
                Circle(fist, r * 0.34f, RioterInk, 28);
                Circle(fist, r * 0.27f, k > 0 && w > 0f ? ThugTape.Mix(PunchTell, w * (0.6f + 0.4f * Mathf.Sin(t * 30f))) : ThugTape, 28);  // the taped fist (glowing as it charges)
                Cap(fist - s * r * 0.18f, fist + s * r * 0.18f, 1.2f, new Rgba(0.6f, 0.58f, 0.52f));
                if (k > 0)
                    Arc(elbow + (fist - elbow) * 0.75f, r * 0.2f, 2.4f, RioterChain, 24);  // a cuff, its other half gone
            }
            RioterHead(pos + f * r * 0.4f, r * 0.3f, f, new Rgba(0.55f, 0.42f, 0.34f, 0.6f));  // shaved
            Cap(pos + f * r * 0.62f - s * r * 0.18f, pos + f * r * 0.62f + s * r * 0.18f, 2.4f, RioterInk);  // the brow
        }

        static readonly Rgba PunchTell = new Rgba(1f, 0.6f, 0.25f);

        /// Under everyone: a Thug's punch. Over the wind-up its narrow wedge is outlined on the floor ahead of it and fills
        /// out from its body to the rim as the fist comes back; the jab flashes the wedge white (ThugPunch.cs).
        void DrawThugFloor(float t)
        {
            foreach (var e in EnemiesOf(EnemyType.Thug))
            {
                if (e.Type != EnemyType.Thug || e.LarderStage == Game.LarderIdle || game.IsSpawning(e))
                    continue;
                V2 pos = e.Disp;
                float half = Game.PunchHalf, R = Game.PunchReach, jab = Game.PunchJabT(e);
                if (jab < 0f)
                {
                    float w = Game.PunchWindT(e);
                    Wedge(pos, R, e.Facing, half, PunchTell.WithAlpha(0.12f), 12);
                    Wedge(pos, R * (0.3f + 0.7f * w), e.Facing, half, PunchTell.WithAlpha(0.18f + 0.14f * w), 12);
                    Arc(pos, R, 2.4f, PunchTell.WithAlpha(0.6f + 0.35f * w), 12, e.Facing - half, half * 2f);
                    for (int k = -1; k <= 1; k += 2)
                        Cap(pos + V2.FromAngle(e.Facing + k * half) * e.Radius, pos + V2.FromAngle(e.Facing + k * half) * R, 1.6f, PunchTell.WithAlpha(0.55f));
                }
                else
                {
                    Wedge(pos, R, e.Facing, half, Rgba.White.WithAlpha(0.35f * (1f - jab)), 12);
                    Arc(pos, R, 4f, Rgba.White.WithAlpha(0.9f * (1f - jab)), 12, e.Facing - half, half * 2f);
                }
            }
        }

        /// A bare arm (a tank top), with a blue tattoo on the upper arm if asked.
        void ThugArm(V2 a, V2 b, float w, bool tattoo)
        {
            Cap(a, b, w, RioterSkin);  // (its outline is drawn first by DrawThugBody)
            if (tattoo)
                Cap(a + (b - a) * 0.25f, a + (b - a) * 0.5f, w * 0.55f, ThugTattoo);
        }

        static readonly Rgba ShivBlade = new Rgba(0.85f, 0.9f, 0.95f), ShivRed = new Rgba(0.8f, 0.15f, 0.15f);

        /// The Shiv, in the Rioter's top-down style, two kinds by id: Hooded (a lean hoodie in `col`, hood up, the shank held
        /// low) and Twin Shanks (wiry, shirtless, a red headband, a shank in each hand, one in reverse grip). The shank
        /// arm draws back as it winds up (phase 1) and thrusts out on the lunge (phase 2).
        void DrawShivBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            float p = Mathf.Sin(t * 11f + e.Id * 1.7f);  // a quick, light step
            float draw = e.Phase == 1 ? 1f : 0f, jab = e.Phase == 2 ? 1f : 0f;
            if (e.Id % 2 == 0)
                DrawShivHooded(e, pos, r, col, f, s, p, draw, jab);
            else
                DrawShivTwin(e, pos, r, f, s, p, draw, jab);
        }

        void DrawShivHooded(Enemy e, V2 pos, float r, Rgba col, V2 f, V2 s, float p, float draw, float jab)
        {
            Oval(pos, r * 0.5f + RioterInkW, r * 0.85f + RioterInkW, e.Facing, RioterInk);
            Oval(pos, r * 0.5f, r * 0.85f, e.Facing, col);  // the hoodie, narrow shoulders
            V2 free = pos + f * r * (0.7f + 0.15f * p) - s * r * 0.55f;
            RioterArm(pos - s * r * 0.7f, free, 3.4f, col);
            ShivHand(free, r);
            V2 knife = pos + f * r * (0.85f - 0.9f * draw + 0.8f * jab) + s * r * (0.6f + 0.25f * draw - 0.35f * jab);
            RioterArm(pos + s * r * 0.7f, knife, 3.4f, col);
            ShivShank(knife, f, r * 1.1f);
            ShivHand(knife, r);
            V2 head = pos + f * r * 0.05f;
            Circle(head, r * 0.4f + RioterInkW, RioterInk, 24);
            Circle(head, r * 0.4f, col.Mix(RioterInk, 0.3f), 24);  // the hood, up
            Circle(head + f * r * 0.12f, r * 0.24f, new Rgba(0.08f, 0.07f, 0.08f, 0.95f), 24);  // the face in shadow
        }

        void DrawShivTwin(Enemy e, V2 pos, float r, V2 f, V2 s, float p, float draw, float jab)
        {
            Oval(pos - f * r * 0.15f, r * 0.45f + RioterInkW, r * 0.78f + RioterInkW, e.Facing, RioterInk);
            Oval(pos - f * r * 0.15f, r * 0.45f, r * 0.78f, e.Facing, ThugJumpsuit);  // the jumpsuit, tied off
            Oval(pos, r * 0.38f, r * 0.68f, e.Facing, RioterSkin);  // the bare back
            Cap(pos - f * r * 0.15f - s * r * 0.3f, pos + f * r * 0.1f + s * r * 0.25f, 2f, ThugTattoo);
            for (int k = -1; k <= 1; k += 2)
            {
                bool lead = k > 0;  // the lead hand winds up and lunges; the other keeps its blade along the forearm
                float d = lead ? draw : 0f, j = lead ? jab : 0f;
                V2 h = pos + f * r * (0.75f + 0.12f * k * p - 0.8f * d + 0.8f * j) + s * (k * r * (0.6f + 0.2f * d - 0.3f * j));
                RioterArm(pos + s * (k * r * 0.62f), h, 3.4f, RioterSkin);
                ShivShank(h, lead ? f : (-f * 0.4f - s * 0.9f).Normalized(), lead ? r : r * 0.8f);
                ShivHand(h, r);
            }
            RioterHead(pos + f * r * 0.1f, r * 0.36f, f, new Rgba(0.15f, 0.12f, 0.1f, 0.95f));  // a buzz cut
            Arc(pos + f * r * 0.1f, r * 0.38f, 2.2f, ShivRed, 24, e.Facing - 0.9f + MathX.Pi, 1.8f);  // the headband
        }

        /// A sharpened shank with a taped grip, pointing along `dir`.
        void ShivShank(V2 grip, V2 dir, float len)
        {
            V2 tip = grip + dir * len;
            V2 n = dir.Orthogonal();
            SetColor(RioterInk);  // a tapered blade to a point
            Tri(grip + n * 2.8f, grip - n * 2.8f, tip + dir * 1.8f);
            SetColor(ShivBlade);
            Tri(grip + n * 1.8f, grip - n * 1.8f, tip);
            Cap(grip - dir * 3f, grip + dir * 2f, 4.5f, ThugTape);
        }

        void ShivHand(V2 h, float r)
        {
            Circle(h, r * 0.17f + RioterInkW, RioterInk, 24);
            Circle(h, r * 0.17f, RioterSkin, 24);
        }
    }
}
