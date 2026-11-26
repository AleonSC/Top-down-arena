using UnityEngine;

namespace Arena
{
    /// Blood Ritual (the Tank Hemomancer's fourth ultimate, Core/BloodRitual.cs), from Docs/Mockups/blood-ritual.html.
    /// Everything comes from the replicated zone (`ZoneKind.BloodRitual`: Pos, Radius, its age the phase) and the imps (minion sync:
    /// Pos, Aim, Scale, Stacks the imp's state, Wind a claw's flash), so it costs nothing extra on the network:
    ///   the inscribe: a rope of blood from the Hemomancer to a pen point that draws the outer circle, then the five strokes of
    ///     the star, then the inner ring and its runes; every stroke a dark under-stroke, the blood, and a wet highlight;
    ///   the open: the sigil flares and the floor inside caves into a portal (dark in the middle, red at the rim) with swirling
    ///     arms and a pulse rolling out, embers rising off it;
    ///   the close: the portal shrinks shut and the sigil fades;
    ///   a blood imp: seen from above, a red body with a horned head, flapping bat wings, a barbed tail and yellow eyes; a dark
    ///     ripple under it as it climbs out; white claw marks as it claws (Wind).
    /// Visual effects off: a plain disc, the star as thin lines, and the imps as plain triangles.
    public partial class ArenaApp
    {
        static readonly Rgba RitBlood = new Rgba(0.70f, 0.07f, 0.17f), RitDeep = new Rgba(0.29f, 0.02f, 0.07f), RitWet = new Rgba(1f, 0.43f, 0.49f);
        static readonly Rgba RitImpBody = new Rgba(0.77f, 0.09f, 0.18f), RitImpWing = new Rgba(0.48f, 0.05f, 0.12f), RitImpHorn = new Rgba(0.16f, 0.01f, 0.03f);
        static readonly Rgba RitEye = new Rgba(1f, 0.83f, 0.3f);

        /// The star's five points, in drawing order (round to the start again).
        static V2 RitStar(Zone z, int k) => z.Pos + V2.FromAngle(-MathX.Pi / 2f + (k * 2 % 5) * MathX.Tau / 5f) * (z.Radius * 0.86f);

        /// A stroke of blood from a to b: dark under, blood, wet highlight.
        void RitStroke(V2 a, V2 b, float w, float alpha)
        {
            Line(a, b, w + 3f, RitDeep.WithAlpha(alpha));
            Line(a, b, w, RitBlood.WithAlpha(alpha));
            Line(a, b, Mathf.Max(1f, w * 0.3f), RitWet.WithAlpha(0.55f * alpha));
        }

        /// Part of a circle of blood, from the top round by `frac`.
        void RitRing(V2 c, float r, float w, float frac, float alpha)
        {
            if (frac <= 0f)
                return;
            float start = -MathX.Pi / 2f, sweep = MathX.Tau * frac;
            Arc(c, r, w + 3f, RitDeep.WithAlpha(alpha), 56, start, sweep);
            Arc(c, r, w, RitBlood.WithAlpha(alpha), 56, start, sweep);
            Arc(c, r, Mathf.Max(1f, w * 0.3f), RitWet.WithAlpha(0.55f * alpha), 56, start, sweep);
        }

        /// The point the pen is at, `prog` 0..1 through the drawing (circle to 0.35, star to 0.9, runes after).
        static V2 RitPen(Zone z, float prog)
        {
            if (prog < 0.35f)
                return z.Pos + V2.FromAngle(-MathX.Pi / 2f + prog / 0.35f * MathX.Tau) * z.Radius;
            float q = Mathf.Clamp01((prog - 0.35f) / 0.55f) * 5f;
            int i = Mathf.Min(4, (int)q);
            return V2.Lerp(RitStar(z, i), RitStar(z, i + 1), q - i);
        }

        /// The sigil and its portal, on the floor (DrawZone).
        void DrawBloodRitual(Zone z, float t)
        {
            float age = Game.RitAge(z), closeAt = Game.RitCloseAt(z);
            float prog = Mathf.Clamp01(age / Game.RitInscribe);
            float open = age < Game.RitOpenAt ? 0f : age < Game.RitFloodAt ? (age - Game.RitOpenAt) / Game.RitOpen
                : age < closeAt ? 1f : Mathf.Clamp01(1f - (age - closeAt) / Game.RitClose * 1.4f);
            float fade = age < closeAt ? 1f : Mathf.Clamp01(1f - (age - closeAt) / Game.RitClose);
            float flare = age >= Game.RitOpenAt && age < Game.RitFloodAt + 0.3f ? 1f - (age - Game.RitOpenAt) / (Game.RitOpen + 0.3f) : 0f;
            if (!fxOn)
            {
                Circle(z.Pos, z.Radius, RitDeep.WithAlpha(0.35f * fade), 40);
                Arc(z.Pos, z.Radius, 2f, RitBlood.WithAlpha(fade), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(prog / 0.35f));
                for (int k = 0; k < 5; k++)
                    if (prog > 0.35f + k * 0.11f)
                        Line(RitStar(z, k), RitStar(z, k + 1), 1.5f, RitBlood.WithAlpha(fade));
                return;
            }
            // The portal: a fan from the black middle out to a red rim, swirling arms, a pulse rolling out.
            if (open > 0f)
            {
                float r = z.Radius * 0.78f * open;
                Rgba core = new Rgba(0f, 0f, 0f, 0.96f), mid = new Rgba(0.16f, 0f, 0.03f, 0.92f), rim = new Rgba(0.6f, 0.04f, 0.14f, 0f);
                const int segs = 40;
                for (int k = 0; k < segs; k++)
                {
                    V2 d0 = V2.FromAngle(MathX.Tau * k / segs), d1 = V2.FromAngle(MathX.Tau * (k + 1) / segs);
                    VTri(z.Pos + d0 * r, rim, z.Pos + d1 * r, rim, z.Pos + d1 * (r * 0.62f), new Rgba(0.55f, 0.04f, 0.13f, 0.85f));
                    VTri(z.Pos + d0 * r, rim, z.Pos + d1 * (r * 0.62f), new Rgba(0.55f, 0.04f, 0.13f, 0.85f), z.Pos + d0 * (r * 0.62f), new Rgba(0.55f, 0.04f, 0.13f, 0.85f));
                    VTri(z.Pos + d0 * (r * 0.62f), mid, z.Pos + d1 * (r * 0.62f), mid, z.Pos, core);
                }
                int arms = fxReduced ? 3 : 4;
                for (int a = 0; a < arms; a++)
                {
                    V2 prev = z.Pos;
                    for (int s = 1; s <= 12; s++)
                    {
                        float f = s / 12f, ang = t * 1.4f + a * MathX.Tau / arms + f * 2.4f;
                        V2 at = z.Pos + V2.FromAngle(ang) * (r * (0.12f + 0.78f * f));
                        Line(prev, at, 2f, new Rgba(1f, 0.27f, 0.37f, 0.35f * open * (1f - f * 0.5f)));
                        prev = at;
                    }
                }
                float pr = Mathf.Repeat(t * 1.6f, 1f);
                Arc(z.Pos, r * (0.4f + 0.6f * pr), 2f, new Rgba(1f, 0.31f, 0.39f, 0.45f * (1f - pr) * open), 40);
                if (!fxReduced)
                    for (int k = 0; k < 6; k++)  // embers rising off it
                    {
                        float ph = Mathf.Repeat(t * 0.8f + k / 6f + z.Id * 0.13f, 1f);
                        V2 at = z.Pos + V2.FromAngle(k * 2.4f + z.Id) * (r * 0.6f * Hash01(z.Id + k)) + new V2(0f, -ph * 46f);
                        Circle(at, 1.8f * (1f - ph * 0.5f), new Rgba(1f, 0.35f, 0.43f, 0.8f * (1f - ph) * open), 6);
                    }
            }
            // The flare as it opens.
            if (flare > 0f)
                GlowDisc(z.Pos, z.Radius * 1.5f, new Rgba(1f, 0.16f, 0.27f), 0.45f * flare);
            // The sigil: the outer circle, the star, the inner ring and its runes, drawn as far as the pen has got.
            float glowA = (open > 0f ? 0.55f + 0.25f * Mathf.Sin(t * 6f) : 0f) * fade;
            if (glowA > 0f)
                Arc(z.Pos, z.Radius, 10f, new Rgba(1f, 0.16f, 0.27f, 0.18f * glowA), 56);
            RitRing(z.Pos, z.Radius, 5f, Mathf.Clamp01(prog / 0.35f), fade);
            float starP = Mathf.Clamp01((prog - 0.35f) / 0.55f) * 5f;
            for (int k = 0; k < 5; k++)
            {
                float f = Mathf.Clamp01(starP - k);
                if (f <= 0f)
                    break;
                RitStroke(RitStar(z, k), V2.Lerp(RitStar(z, k), RitStar(z, k + 1), f), 4f, fade);
            }
            float runeP = Mathf.Clamp01((prog - 0.9f) / 0.1f);
            RitRing(z.Pos, z.Radius * 0.86f, 2f, runeP, fade);
            for (int k = 0; k < 15; k++)
            {
                if (k / 15f >= runeP)
                    break;
                float a = -MathX.Pi / 2f + (k + 0.5f) * MathX.Tau / 15f;
                V2 c = z.Pos + V2.FromAngle(a) * (z.Radius * 0.93f), u = V2.FromAngle(a + MathX.Pi / 2f), n = V2.FromAngle(a);
                var col = RitBlood.WithAlpha(fade);
                switch (k % 3)  // three runes, round and round
                {
                    case 0:
                        Line(c - u * 3f - n * 3f, c + n * 3f, 1.6f, col);
                        Line(c + n * 3f, c + u * 3f - n * 3f, 1.6f, col);
                        break;
                    case 1:
                        Line(c - n * 3.5f, c + n * 3.5f, 1.6f, col);
                        Line(c - u * 3f, c + u * 3f + n * 2f, 1.6f, col);
                        break;
                    default:
                        Line(c - u * 3f + n * 3f, c - n * 3f, 1.6f, col);
                        Line(c - n * 3f, c + u * 3f + n * 3f, 1.6f, col);
                        Line(c - u * 1.5f, c + u * 1.5f, 1.6f, col);
                        break;
                }
            }
            if (starP > 1f)
                for (int k = 0; k < 5; k++)  // a bead of blood at each point
                    Circle(RitStar(z, k), 3.2f, RitBlood.WithAlpha(fade), 10);
            // The rope of blood from the Hemomancer to the pen while it's drawn.
            Player owner;
            if (prog < 1f && game.Players.TryGetValue(z.Owner, out owner) && !owner.Downed)
            {
                V2 pen = RitPen(z, prog), from = owner.Disp;
                V2 ctrl = (from + pen) * 0.5f + new V2(0f, -Mathf.Min(70f, from.DistanceTo(pen) * 0.3f) + Mathf.Sin(t * 14f) * 5f);
                V2 prev = from;
                for (int s = 1; s <= 14; s++)
                {
                    float f = s / 14f;
                    V2 at = (1f - f) * (1f - f) * from + 2f * (1f - f) * f * ctrl + f * f * pen;
                    Line(prev, at, 5f, RitDeep.WithAlpha(0.9f));
                    Line(prev, at, 3f, RitBlood);
                    prev = at;
                }
                GlowDisc(pen, 12f, new Rgba(1f, 0.3f, 0.4f), 0.6f);
                Circle(pen, 3.5f, RitWet, 10);
            }
        }

        /// A blood imp (DrawMinion), from above: Stacks 0 climbing out, 1 flying, 2 clawing, 3 going home.
        void DrawBloodImp(Minion m, float t)
        {
            float s = 1.6f * Mathf.Max(0.2f, m.Scale);
            V2 dir = V2.FromAngle(m.Aim), side = new V2(-dir.Y, dir.X), c = m.Disp;
            if (!fxOn)
            {
                Polygon(c, 7f * s, 3, m.Aim, RitImpBody);
                return;
            }
            if (m.Stacks == 0)  // a dark ripple where it climbs out
                Arc(c + new V2(0f, 6f), 9f * (2f - m.Scale), 1.5f, RitDeep.WithAlpha(0.7f * (1f - m.Scale)), 16);
            Ellipse(c + new V2(0f, 7f * s), 6f * s, 2.2f * s, 0f, new Rgba(0f, 0f, 0f, 0.3f), 10);  // its shadow
            float flap = Mathf.Sin(t * 26f + m.Id * 1.9f);
            for (int k = -1; k <= 1; k += 2)  // bat wings: a root, a swept-back tip and a notched trailing edge
            {
                V2 root = c - dir * (1f * s) + side * (k * 2.5f * s);
                V2 tip = root + side * (k * (10f + 3f * flap) * s) + dir * ((2f - 3f * flap) * s);
                V2 back = root + side * (k * 8f * s) - dir * (5f * s);
                V2 notch = root + side * (k * 4.5f * s) - dir * (2.5f * s);
                VTri(root, RitImpWing, tip, RitImpWing, back, RitImpWing);
                VTri(root, RitImpWing, back, RitImpWing, notch, RitImpWing);
                Line(root, tip, 1.4f, RitWet);  // the wing's bone
                Line(tip, back, 1f, RitImpHorn);
            }
            // The tail, a barb at its end.
            V2 t0 = c - dir * (4f * s), t1 = t0 - dir * (5f * s) + side * (Mathf.Sin(t * 9f + m.Id) * 3f * s), t2 = t1 - dir * (3f * s);
            Line(t0, t1, 1.5f * s, RitBlood);
            Line(t1, t2, 1.2f * s, RitBlood);
            VTri(t2 + side * (2f * s), RitBlood, t2 - side * (2f * s), RitBlood, t2 - dir * (3f * s), RitBlood);
            // Body and head.
            V2 head = c + dir * (4.5f * s);
            Ellipse(c, 4.5f * s + 1.2f, 5.5f * s + 1.2f, m.Aim + MathX.Pi / 2f, RitImpHorn, 12);  // a dark outline, so it reads on the red sigil
            Circle(head, 3.2f * s + 1.2f, RitImpHorn, 10);
            Ellipse(c, 4.5f * s, 5.5f * s, m.Aim + MathX.Pi / 2f, RitImpBody, 12);
            Circle(head, 3.2f * s, RitImpBody, 10);
            Ellipse(c - dir * (0.8f * s) + side * (1.2f * s), 1.6f * s, 2.4f * s, m.Aim + MathX.Pi / 2f, new Rgba(1f, 0.5f, 0.55f, 0.45f), 8);  // a sheen on its back
            for (int k = -1; k <= 1; k += 2)
                VTri(head + side * (k * 1.6f * s), RitImpHorn, head + side * (k * 3.2f * s), RitImpHorn, head + dir * (3.5f * s) + side * (k * 4f * s), RitImpHorn);
            Circle(head + dir * (1.2f * s) + side * (1.3f * s), 1.1f * s, RitEye, 6);
            Circle(head + dir * (1.2f * s) - side * (1.3f * s), 1.1f * s, RitEye, 6);
            if (m.Wind > 0f)  // a claw: three white marks across its front
                for (int k = -1; k <= 1; k++)
                {
                    V2 a = head + dir * (3f * s) + side * (k * 2.2f * s);
                    Line(a, a + dir * (4f * s) + side * (k * 1f * s), 1.2f, new Rgba(1f, 0.9f, 0.92f, m.Wind));
                }
        }
    }
}
