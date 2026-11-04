using UnityEngine;

namespace Arena
{
    /// The Healer's new pair (Docs/Mockups/soul-lantern.html look B, Docs/Mockups/deployable-medkit.html look A).
    /// Soul Lantern: a small iron cage round a cold teal flame, a ghostly ribbon to the player it is linked to, and
    /// flames that are twisting wisps (the shots themselves are drawn by the wisp shot). Deployable Medkit: a white
    /// first-aid case with a red cross tumbling through the air, and the healing orbs it bursts into. Drawing only;
    /// the link, heal and flames are SoulLantern.cs and the case and orbs are Medkit.cs.
    public partial class ArenaApp
    {
        static readonly Rgba LanternTeal = new Rgba(0.47f, 1f, 0.88f), LanternIron = new Rgba(0.36f, 0.42f, 0.46f),
            KitWhite = new Rgba(0.95f, 0.97f, 0.98f), KitRed = new Rgba(0.9f, 0.28f, 0.25f), KitGreen = new Rgba(0.35f, 1f, 0.55f);

        void DrawSoulLantern(Minion m, float t)
        {
            float fade = m.T >= 0f && m.T < 0.7f ? m.T / 0.7f : 1f;  // fades as the link ends
            Player linked;
            if (game.Players.TryGetValue(m.LinkTo, out linked))
            {
                // The link: a ghostly ribbon that undulates, with a green mote carrying each heal across.
                V2 a = m.Disp, b = linked.Disp, n = (b - a).Normalized().Orthogonal();
                V2 prev = a, prev2 = a;
                for (int k = 1; k <= 18; k++)
                {
                    float f = k / 18f;
                    V2 pt = V2.Lerp(a, b, f) + n * (Mathf.Sin(f * 7f - t * 5f) * 9f * Mathf.Sin(f * MathX.Pi));
                    Line(prev, pt, 7f, LanternTeal.WithAlpha(0.2f * fade));
                    Line(prev, pt, 2f, new Rgba(0.82f, 1f, 0.97f, 0.9f * fade));
                    prev = pt;
                }
                float mote = MathX.Fmod(t, 0.75f) / 0.75f;
                Circle(V2.Lerp(a, b, mote), 4f, KitGreen.WithAlpha(0.95f * fade), 10);
                Arc(linked.Disp, linked.Def.BodyR + 9f + 2f * Mathf.Sin(t * 4f), 2f, LanternTeal.WithAlpha(0.35f * fade), 28);
            }
            V2 at = m.Disp + new V2(0f, 3f * Mathf.Sin(t * 2.3f + m.Id));
            Circle(at, 62f, LanternTeal.WithAlpha(0.07f * fade), 24);
            Circle(at, 34f, LanternTeal.WithAlpha(0.12f * fade), 20);
            // The flame, leaning with a wobble, inside the cage.
            float lean = Mathf.Sin(t * 6f + m.Id) * 2f;
            SetColor(new Rgba(0.8f, 1f, 0.97f, 0.95f * fade));
            Tri(at + new V2(lean, -16f), at + new V2(-8f, 10f), at + new V2(8f, 10f));
            Circle(at + new V2(0f, 5f), 6.5f, LanternTeal.WithAlpha(0.9f * fade), 12);
            // The cage: iron bars, a base and a ring on top.
            var iron = LanternIron.WithAlpha(fade);
            Line(at + new V2(-11f, 14f), at + new V2(-13f, -8f), 2f, iron);
            Line(at + new V2(11f, 14f), at + new V2(13f, -8f), 2f, iron);
            Line(at + new V2(-13f, -8f), at + new V2(0f, -19f), 2f, iron);
            Line(at + new V2(13f, -8f), at + new V2(0f, -19f), 2f, iron);
            Line(at + new V2(0f, 15f), at + new V2(0f, -18f), 1.3f, iron);
            Line(at + new V2(-6f, 15f), at + new V2(-4f, -17f), 1.3f, iron);
            Line(at + new V2(6f, 15f), at + new V2(4f, -17f), 1.3f, iron);
            Line(at + new V2(-13f, 15f), at + new V2(13f, 15f), 3f, iron);
            Arc(at + new V2(0f, -22f), 3f, 1.6f, iron, 10);
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, 24f, 2f, LanternTeal.WithAlpha(0.45f * fade), 28, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }

        /// The Deployable Medkit in flight: a first-aid case tumbling over its shadow.
        void DrawMedkitShot(Bullet b, float t)
        {
            float flight = Mathf.Max(0.01f, b.Life0), k = 1f - MathX.Clamp01(b.Life / flight);
            float lift = Mathf.Sin(k * MathX.Pi) * 70f;
            V2 at = b.Pos + new V2(0f, -lift);
            Circle(b.Pos + new V2(0f, 6f), 12f - 4f * lift / 70f, new Rgba(0f, 0f, 0f, 0.35f), 14);
            float spin = k * 9f;
            V2 dx = V2.FromAngle(spin), dy = dx.Orthogonal();
            SetColor(KitWhite);
            Quad(at - dx * 13f - dy * 10f, at + dx * 13f - dy * 10f, at + dx * 13f + dy * 10f, at - dx * 13f + dy * 10f);
            Line(at - dx * 13f - dy * 9f, at + dx * 13f - dy * 9f, 2.5f, new Rgba(0.55f, 0.6f, 0.64f));
            Line(at - dx * 7f, at + dx * 7f, 5f, KitRed);
            Line(at - dy * 7f, at + dy * 7f, 5f, KitRed);
        }

        /// A medkit's orb: the usual green orb plus a white-ringed first-aid cross, so it reads as a pickup you can walk to.
        void DrawMedOrb(Orb o, float t)
        {
            if (o.Life < 1.5f && MathX.Fmod(t, 0.12f) < 0.06f)
                return;  // flickers for its last 1.5 s
            float pulse = 1f + 0.12f * Mathf.Sin(t * 6f + o.Id);
            float r = o.Radius * pulse;
            V2 at = o.Disp + new V2(0f, 2.5f * Mathf.Sin(t * 3f + o.Id));
            Circle(at, r * 2.4f, KitGreen.WithAlpha(0.16f), 20);
            Circle(at, r, KitGreen, 16);
            Arc(at, r + 1.5f, 1.5f, Rgba.White.WithAlpha(0.8f), 16);
            Line(at - new V2(r * 0.6f, 0f), at + new V2(r * 0.6f, 0f), 2.5f, Rgba.White);
            Line(at - new V2(0f, r * 0.6f), at + new V2(0f, r * 0.6f), 2.5f, Rgba.White);
        }
    }
}
