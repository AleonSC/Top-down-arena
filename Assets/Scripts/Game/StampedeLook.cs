using UnityEngine;

namespace Arena
{
    /// Stampede (the Tank Summoner's 4th choice, Core/Stampede.cs), from Docs/Mockups/stampede.html. Everything is drawn from the
    /// replicated zone (`ZoneKind.Stampede`: Pos, Aim, Radius, Marched, T, Duration), so it costs nothing on the network:
    ///   the gate    a bar of soul-green light across the aim where the herd came out, with rune arcs, opening over
    ///               Game.StampedeWind s and fading once the herd has stopped;
    ///   the herd    Game.StampedeCount spirit beasts side by side (a mixed herd: bison, boar, stag, boar, bison, ...),
    ///               each a glowing green body with a bone rim, spine runes, a head with white eyes, horns, tusks or antlers
    ///               and legs that stride by the distance marched, with a dust trail, afterimages and a pale wake;
    ///   the fade    the herd and its wake fade over Game.StampedeFade s after the last beast stops.
    /// (The shove, the damage numbers and the wall slam are the ordinary hit effects.) Visual effects off: a disc and a bar
    /// per beast, a plain line for the gate.
    public partial class ArenaApp
    {
        static readonly Rgba StampedeGreen = new Rgba(0.5f, 0.88f, 0.67f), StampedeDeep = new Rgba(0.16f, 0.47f, 0.35f), StampedeLight = new Rgba(0.78f, 1f, 0.88f),
            StampedeBone = C255(239, 233, 214), StampedeDust = C255(170, 160, 140);

        /// The kind of beast in lane i: 0 bison, 1 boar, 2 stag.
        static int StampedeKind(int i) => (i % 7) == 0 || (i % 7) == 4 ? 0 : (i % 7) == 2 || (i % 7) == 6 ? 2 : 1;

        void DrawStampede(Zone z, float t)
        {
            int n = Game.StampedeCount(z);
            bool cap = Game.StampedeBack2(z);
            float age = Game.StampedeAge(z), dist;
            int leg = Game.StampedeLeg(age, cap, out dist);
            float alpha = Game.StampedeAlpha(z);
            float out0 = leg == 1 ? z.Aim + Mathf.PI : z.Aim;  // the way it charges out of the gate
            V2 outDir = V2.FromAngle(out0), dir = V2.FromAngle(z.Aim), perp = new V2(-dir.Y, dir.X), gatePerp = new V2(-outDir.Y, outDir.X);
            V2 gate = z.Pos - outDir * z.Marched;
            DrawStampedeGate(gate, gatePerp, z.Radius, age, alpha);
            if (age < Game.StampedeWind)
                return;  // the beasts step out of the gate when it has opened
            float run = z.Marched / 22f;
            float a = alpha * Mathf.Clamp01((age - Game.StampedeWind) / 0.12f);
            if (fxOn && alpha > 0f)  // the wake: a pale wash behind the line
            {
                V2 back = z.Pos - dir * Mathf.Max(0f, Mathf.Min(z.Marched, 140f));
                for (int s = 0; s < 3; s++)
                    GlowDisc(back + (z.Pos - back) * (0.25f + 0.35f * s), z.Radius * (0.9f - 0.1f * s), StampedeGreen, 0.07f * a);
            }
            for (int i = 0; i < n; i++)
            {
                float lane = (i - (n - 1) * 0.5f) * Game.StampedeSpacing;
                float lag = (KHash(z.Id * 7f + i) - 0.5f) * 14f;
                V2 at = z.Pos + perp * lane + dir * lag;
                DrawStampedeBeast(at, z.Aim, StampedeKind(i), run + i * 1.3f, a, leg >= 0 && alpha > 0.5f, z.Id * 13f + i);
            }
        }

        void DrawStampedeGate(V2 gate, V2 perp, float half, float age, float alpha)
        {
            float open = Mathf.Clamp01(age / Game.StampedeWind), h = (half + 24f) * KEo(open);
            float fade = age < Game.StampedeWind + Game.StampedeReach / Game.StampedeSpeed ? 1f : alpha;
            if (fade <= 0f)
                return;
            if (!fxOn)
            {
                Line(gate - perp * h, gate + perp * h, 3f, StampedeGreen.WithAlpha(0.8f * fade));
                return;
            }
            Line(gate - perp * h, gate + perp * h, 14f, StampedeGreen.WithAlpha(0.14f * fade));
            Line(gate - perp * h, gate + perp * h, 5f, StampedeLight.WithAlpha(0.9f * fade));
            for (int r = 1; r <= 3; r++)  // rune arcs on the herd's side
                Arc(gate, r * 16f * open, 1.5f, StampedeGreen.WithAlpha(0.45f * fade), 18, perp.Angle - Mathf.PI / 2f - 1.4f, 2.8f);
            if (age < Game.StampedeWind + 0.3f)
                GlowDisc(gate, (half + 20f) * 0.5f, StampedeGreen, 0.25f * (1f - Mathf.Clamp01((age - Game.StampedeWind) / 0.3f)));
        }

        /// One beast at `at` heading `ang`. kind: 0 bison, 1 boar, 2 stag; run: stride phase; a: alpha; trail: draws its dust.
        void DrawStampedeBeast(V2 at, float ang, int kind, float run, float a, bool trail, float seed)
        {
            if (a <= 0f)
                return;
            V2 u = V2.FromAngle(ang), v = new V2(-u.Y, u.X);
            float L = kind == 0 ? 22f : kind == 2 ? 18f : 15f, W = kind == 0 ? 13f : kind == 2 ? 8.5f : 9f;
            if (!fxOn)
            {
                Ellipse(at, L, W, ang, StampedeGreen.WithAlpha(0.85f * a), 10);
                Line(at, at + u * (L + 8f), 2.5f, StampedeBone.WithAlpha(a));
                return;
            }
            if (trail)
                for (int d = 1; d <= 5; d++)  // dust
                    GlowDisc(at - u * (d * 14f + L) + v * Mathf.Sin(d * 2f + seed) * 6f, 5f + d * 2f, StampedeDust, 0.12f * a / d);
            float sw = Mathf.Sin(run * 1.6f) * L * 0.22f;
            for (int leg = 0; leg < 4; leg++)  // legs
            {
                float side = leg % 2 == 0 ? -1f : 1f, fore = leg < 2 ? -0.55f : 0.35f, ph = (leg % 2 == 0 ? -1f : 1f) * sw;
                Line(at + u * (fore * L) + v * (side * W * 0.6f), at + u * (fore * L + ph) + v * (side * W * 1.1f), 2.4f, StampedeGreen.WithAlpha(0.85f * a));
            }
            GlowDisc(at, L * 1.5f, StampedeGreen, 0.22f * a);
            Ellipse(at, L, W, ang, StampedeBone.WithAlpha(a), 14);                   // the bone rim
            Ellipse(at, L - 1.4f, W - 1.4f, ang, StampedeDeep.WithAlpha(a), 14);
            Ellipse(at + u * (L * 0.2f), L * 0.78f, W * 0.8f, ang, StampedeGreen.WithAlpha(0.95f * a), 14);
            for (float q = -0.6f; q <= 0.4f; q += 0.3f)  // spine runes
            {
                V2 c = at + u * (q * L);
                Line(c + v * (-W * 0.3f), c + u * (L * 0.1f), 1.2f, StampedeBone.WithAlpha(0.7f * a));
                Line(c + u * (L * 0.1f), c + v * (W * 0.3f), 1.2f, StampedeBone.WithAlpha(0.7f * a));
            }
            V2 head = at + u * (L * 0.95f);  // the head
            Ellipse(head, L * 0.42f + 1.2f, W * 0.55f + 1.2f, ang, StampedeBone.WithAlpha(a), 12);
            Ellipse(head, L * 0.42f, W * 0.55f, ang, StampedeLight.WithAlpha(a), 12);
            for (int s = -1; s <= 1; s += 2)
            {
                if (kind == 0)       // bison: curved horns
                    BoneCurve(at + u * (L * 0.95f) + v * (s * W * 0.45f), at + u * (L * 1.45f) + v * (s * W * 0.95f), at + u * (L * 1.25f) + v * (s * W * 1.45f), 2.6f, StampedeBone.WithAlpha(a));
                else if (kind == 1)  // boar: tusks
                    BoneCurve(at + u * (L * 1.2f) + v * (s * W * 0.3f), at + u * (L * 1.5f) + v * (s * W * 0.4f), at + u * (L * 1.55f) + v * (s * W * 0.95f), 2.4f, StampedeBone.WithAlpha(a));
                else                 // stag: antlers
                {
                    V2 b = at + u * (L * 0.95f), k1 = b + u * (L * 0.2f) + v * (s * W * 1.2f), k2 = b + u * (L * 0.65f) + v * (s * W * 1.5f);
                    Line(b + v * (s * W * 0.3f), k1, 2.2f, StampedeBone.WithAlpha(a));
                    Line(k1, k2, 2.2f, StampedeBone.WithAlpha(a));
                    Line(k1, b + u * (L * 0.45f) + v * (s * W * 2f), 2f, StampedeBone.WithAlpha(a));
                }
                GlowDisc(at + u * (L * 1.08f) + v * (s * W * 0.28f), 2.6f, new Rgba(1f, 1f, 1f), 0.95f * a);  // eyes
            }
        }
    }
}
