using UnityEngine;

namespace Arena
{
    /// Proposed reworks of the Duelist's body (mockup, picked with DUELLOOK in previews; 0 keeps today's silver square):
    ///   1 Maestro: the Masked Court's master: a dark fencer with a steel rim, a full white mask with gold filigree, a
    ///     crimson plume, a long crimson cape with a gold hem, gold epaulettes and a swept-hilt rapier.
    ///   2 Masked Duke: baroque: a crimson doublet in a gold lattice, a white ruff, a black bicorne with a white plume,
    ///     a gold long-nosed mask, a steel half-cape over one shoulder, a rapier and a main-gauche.
    ///   3 Phantom: sleek: a steel diamond (the square it had, turned to point), a chrome mask with one crimson visor
    ///     slit, two afterimages and a ribbon trailing it, a rapier with a glowing crimson edge.
    /// From To the Death on, each is battle-worn: the mask cracks and the cape (or ribbon) tears.
    public partial class ArenaApp
    {
        public static int DuelistLookVariant;  // previews: 0 today's look, 1-3 the options above

        static readonly Rgba DuelInk = new Rgba(0.04f, 0.045f, 0.07f, 0.95f), DuelDark = new Rgba(0.1f, 0.11f, 0.16f),
            DuelMask = new Rgba(0.95f, 0.93f, 0.9f), DuelBlood = new Rgba(0.55f, 0.08f, 0.16f), DuelWhite = new Rgba(0.95f, 0.96f, 1f);

        void DrawDuelistBody(Enemy e, V2 pos, float r, float t)
        {
            var d = e.Duel;
            bool worn = d != null && d.Stage >= DuelStage.ToTheDeath;
            float face = e.Facing + (d != null && d.StaggerT > 0f ? Mathf.Sin(t * 14f) * 0.35f : 0f);
            DuelPose(e, face, out float bladeAng, out bool up);
            switch (DuelistLookVariant)
            {
                case 1: DrawMaestro(e, pos, r, face, bladeAng, up, worn, t); break;
                case 2: DrawDuke(e, pos, r, face, bladeAng, up, worn, t); break;
                default: DrawPhantom(e, pos, r, face, bladeAng, up, worn, t); break;
            }
        }

        /// Where the sword points: drawn back while the line shows, straight down the dash, held across the front on
        /// guard, straight up in salute, a little off the facing otherwise.
        static void DuelPose(Enemy e, float face, out float bladeAng, out bool up)
        {
            var d = e.Duel;
            up = d != null && d.SaluteT > 0f;
            bool warn = d != null && d.Dash != DuelDash.None && d.DashWarn > 0f, dash = d != null && d.Dash != DuelDash.None && d.DashWarn <= 0f;
            bladeAng = warn ? face + 1.2f : dash ? face : Game.DuelGuarding(e) ? face - 0.9f : face + 0.3f;
        }

        /// The sword hand: on the right side, a little forward.
        static V2 DuelHand(V2 pos, float r, float face, bool up) =>
            pos + V2.FromAngle(face) * (r * (up ? 0.2f : 0.35f)) + V2.FromAngle(face).Orthogonal() * (r * 0.8f);

        /// A rapier from `hand`: blade, ricasso, a gold cross-guard and, if `swept`, a gold knuckle bow round the hand.
        void DuelRapier(V2 hand, float ang, float len, bool swept, Rgba edge, float t)
        {
            V2 d = V2.FromAngle(ang), s = d.Orthogonal();
            if (edge.A > 0f)
                Line(hand + d * 6f, hand + d * len, 7f, edge);
            Line(hand + d * 4f, hand + d * len, 3f, DuelWhite);
            Line(hand + d * 4f, hand + d * 14f, 4.5f, new Rgba(0.7f, 0.74f, 0.85f));
            Line(hand - s * 9f + d * 4f, hand + s * 9f + d * 4f, 3.5f, DuelGold);
            Circle(hand - s * 9f + d * 4f, 2.4f, DuelGold, 8);
            Circle(hand + s * 9f + d * 4f, 2.4f, DuelGold, 8);
            if (swept)
                Arc(hand, 8f, 2.2f, DuelGold.WithAlpha(0.95f), 12, ang - 1.4f, 2.8f);
            Line(hand, hand - d * 7f, 3.5f, new Rgba(0.35f, 0.2f, 0.15f));
            Circle(hand - d * 8f, 3f, DuelGold, 8);
            Circle(hand + d * len, 1.6f + 0.8f * Mathf.Sin(t * 9f), DuelWhite.WithAlpha(0.8f), 6);  // a point of light at the tip
        }

        /// A cape hanging from the shoulders and flowing back: `cols` strips swaying, a hem along the bottom; torn: jagged.
        void DuelCape(V2 pos, float r, float face, float len, float width, Rgba col, Rgba hem, bool torn, float t, int id)
        {
            V2.Axes(face, out V2 f, out V2 s);
            const int cols = 8;
            var top = new V2[cols + 1];
            var bot = new V2[cols + 1];
            for (int i = 0; i <= cols; i++)
            {
                // The top wraps the back of the body; the strips fall back, gathering in a little toward the hem.
                float u = -1f + 2f * i / cols, a = face + MathX.Pi + u * MathX.Pi * 0.5f * width;
                float l = len * (1f - 0.25f * u * u) * (torn ? (i % 2 == 0 ? 0.55f : 0.85f) : 1f);
                float sway = Mathf.Sin(t * 4.5f + u * 2.2f + id) * r * 0.12f;
                top[i] = pos + V2.FromAngle(a) * (r * 0.85f);
                bot[i] = pos + V2.FromAngle(face + MathX.Pi + u * MathX.Pi * 0.32f * width) * (r * 0.85f + l) + s * sway;
            }
            SetColor(col);
            for (int i = 0; i < cols; i++)
                Quad(top[i], top[i + 1], bot[i + 1], bot[i]);
            SetColor(col.WithAlpha(col.A * 0.55f));  // folds: darker strips every other column
            for (int i = 1; i < cols; i += 2)
                Quad(Vec(top[i], bot[i], 0.25f), Vec(top[i + 1], bot[i + 1], 0.25f), bot[i + 1], bot[i]);
            for (int i = 0; i < cols; i++)
                Line(bot[i], bot[i + 1], 2.2f, hem);
        }

        /// A feather from `root` curling back along `back` toward `side`: a quill and barbs either side, tapering.
        void DuelPlume(V2 root, V2 back, V2 side, float r, Rgba col, float t)
        {
            const int n = 9;
            V2 prev = root;
            for (int k = 1; k <= n; k++)
            {
                float u = k / (float)n, sway = Mathf.Sin(t * 5f + u * 3f) * 0.06f;
                V2 at = root + back * (r * 1.3f * u) + side * (r * (0.5f * u * u + sway));
                V2 d = at - prev, o = d.Orthogonal() / Mathf.Max(0.01f, d.Length);
                float w = r * 0.26f * (1f - 0.75f * u) * (0.6f + 0.4f * Mathf.Sin(u * 3.1f));
                Line(at - o * w, at + o * w - d * 0.6f, 2f, col.WithAlpha(0.9f));
                Line(at + o * w, at - o * w - d * 0.6f, 2f, col.WithAlpha(0.9f));
                Line(prev, at, 1.6f, col);
                prev = at;
            }
        }

        static V2 Vec(V2 a, V2 b, float k) => a + (b - a) * k;

        /// A filled ellipse round `c` with half-axes `a` and `b`.
        void DuelEllipse(V2 c, V2 a, V2 b, Rgba col, int n)
        {
            SetColor(col);
            for (int i = 0; i < n; i++)
            {
                float a0 = MathX.Tau * i / n, a1 = MathX.Tau * (i + 1) / n;
                Tri(c, c + a * Mathf.Cos(a0) + b * Mathf.Sin(a0), c + a * Mathf.Cos(a1) + b * Mathf.Sin(a1));
            }
        }

        void DuelEllipseEdge(V2 c, V2 a, V2 b, float w, Rgba col, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = MathX.Tau * i / n, a1 = MathX.Tau * (i + 1) / n;
                Line(c + a * Mathf.Cos(a0) + b * Mathf.Sin(a0), c + a * Mathf.Cos(a1) + b * Mathf.Sin(a1), w, col);
            }
        }

        /// A crack across a mask: three ink zigzags from `at` along `along`.
        void DuelCrack(V2 at, V2 along, V2 side, float len)
        {
            V2 p1 = at + along * (len * 0.35f) + side * (len * 0.18f), p2 = at + along * (len * 0.65f) - side * (len * 0.12f), p3 = at + along * len + side * (len * 0.1f);
            Line(at, p1, 1.8f, DuelInk);
            Line(p1, p2, 1.8f, DuelInk);
            Line(p2, p3, 1.5f, DuelInk);
            Line(p1, p1 + side * (len * 0.25f), 1.2f, DuelInk);
        }

        // ---- 1 Maestro ----

        void DrawMaestro(Enemy e, V2 pos, float r, float face, float bladeAng, bool up, bool worn, float t)
        {
            V2.Axes(face, out V2 f, out V2 s);
            Rgba rim = worn ? DuelCrimson : DuelSteel;
            DuelCape(pos, r, face, r * 1.35f, 0.85f, DuelCrimson.WithAlpha(0.9f), DuelGold, worn, t, e.Id);
            // Body: ink edge, rim, dark coat, a gold sash across it.
            Circle(pos, r + 3f, DuelInk, 28);
            Circle(pos, r, rim, 28);
            Circle(pos, r - 3f, DuelDark, 28);
            Line(pos - s * (r * 0.75f) - f * (r * 0.35f), pos + s * (r * 0.55f) + f * (r * 0.55f), 4f, DuelGold.WithAlpha(0.9f));
            // Epaulettes: gold discs with a fringe on each shoulder.
            for (int k = -1; k <= 1; k += 2)
            {
                V2 sh = pos + s * (k * r * 0.78f) - f * (r * 0.05f);
                for (int j = -2; j <= 2; j++)
                    Line(sh, sh + s * (k * r * 0.28f) + f * (j * r * 0.09f), 1.5f, DuelGold.WithAlpha(0.8f));
                Circle(sh, r * 0.2f, DuelGold, 12);
                Circle(sh, r * 0.11f, DuelDark, 10);
            }
            // The plume: a crimson feather curling back over the left shoulder.
            // The mask: a full white face toward the front, gold filigree over the brow, two slits.
            V2 mc = pos + f * (r * 0.2f);
            DuelEllipse(mc, f * (r * 0.58f), s * (r * 0.62f), DuelMask, 18);
            DuelEllipseEdge(mc, f * (r * 0.58f), s * (r * 0.62f), 1.4f, DuelInk.WithAlpha(0.6f), 18);
            Arc(mc, r * 0.48f, 1.8f, DuelGold, 12, face - 1.2f, 2.4f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 eye = mc + f * (r * 0.18f) + s * (k * r * 0.25f);
                DuelEllipse(eye, s * (r * 0.14f), f * (r * 0.06f), DuelInk, 10);
            }
            if (worn)
                DuelCrack(mc + f * (r * 0.5f) - s * (r * 0.1f), -f, s, r * 0.6f);
            DuelRapier(DuelHand(pos, r, face, up), up ? -MathX.Pi / 2f : bladeAng, r * 2.5f, true, new Rgba(0f, 0f, 0f, 0f), t);
        }

        // ---- 2 Masked Duke ----

        void DrawDuke(Enemy e, V2 pos, float r, float face, float bladeAng, bool up, bool worn, float t)
        {
            V2.Axes(face, out V2 f, out V2 s);
            DuelCape(pos, r, face, r * 1.25f, 0.8f, new Rgba(0.42f, 0.46f, 0.62f, 0.95f), DuelGold, worn, t, e.Id);
            // Main-gauche in the left hand, held low.
            V2 off = pos - s * (r * 0.85f) + f * (r * 0.3f);
            Line(off, off + V2.FromAngle(face - 0.45f) * (r * 0.9f), 3f, DuelWhite);
            Arc(off, 6f, 2.5f, DuelGold, 10, face - 0.45f - 1.5f, 3f);
            // Doublet: crimson in a gold lattice.
            Circle(pos, r + 3f, DuelInk, 28);
            Circle(pos, r, DuelGold, 28);
            Circle(pos, r - 2.5f, worn ? DuelBlood : new Rgba(0.6f, 0.1f, 0.2f), 28);
            float q = r * 0.42f;
            for (int i = -3; i <= 3; i++)
            {
                V2 a = pos + f * (i * q) - s * r, b = pos + f * (i * q) + s * r;
                ClipLine(pos, r - 3f, a + f * r, b - f * r, 1.2f, DuelGold.WithAlpha(0.7f));
                ClipLine(pos, r - 3f, a - f * r, b + f * r, 1.2f, DuelGold.WithAlpha(0.7f));
            }
            // The ruff: a white scalloped collar.
            V2 hc = pos + f * (r * 0.05f);
            Circle(hc, r * 0.66f, DuelInk, 20);
            Circle(hc, r * 0.62f, DuelMask, 20);
            for (int k = 0; k < 12; k++)  // the ruff's pleats
            {
                V2 dk = V2.FromAngle(MathX.Tau * k / 12f);
                Line(hc + dk * (r * 0.42f), hc + dk * (r * 0.6f), 1.2f, new Rgba(0.6f, 0.6f, 0.65f));
            }
            // The bicorne: black, across the head, gold-edged, a white plume along its crown.
            V2 hat = hc - f * (r * 0.1f);
            DuelEllipse(hat, s * (r * 0.8f), f * (r * 0.3f), new Rgba(0.06f, 0.06f, 0.09f), 22);
            DuelEllipseEdge(hat, s * (r * 0.8f), f * (r * 0.3f), 2f, DuelGold, 22);
            Line(hat - s * (r * 0.62f), hat + s * (r * 0.62f), 3f, DuelWhite.WithAlpha(0.9f));  // a white trim along its crown
            Circle(hat + s * (r * 0.5f), 4.5f, DuelCrimson, 10);  // a cockade
            // The gold bauta: a long-nosed mask out the front.
            V2 mc = hc + f * (r * 0.3f);
            DuelEllipse(mc, f * (r * 0.24f), s * (r * 0.34f), DuelGold, 14);
            SetColor(DuelGold);
            Tri(mc + s * (r * 0.12f), mc - s * (r * 0.12f), mc + f * (r * 0.62f));
            Line(mc, mc + f * (r * 0.6f), 1.2f, new Rgba(0.7f, 0.5f, 0.15f));
            for (int k = -1; k <= 1; k += 2)
                Circle(mc + f * (r * 0.08f) + s * (k * r * 0.17f), 2.4f, DuelInk, 8);
            if (worn)
                DuelCrack(mc + f * (r * 0.3f) + s * (r * 0.05f), -f + s * 0.4f, s, r * 0.4f);
            DuelRapier(DuelHand(pos, r, face, up), up ? -MathX.Pi / 2f : bladeAng, r * 2.4f, true, new Rgba(0f, 0f, 0f, 0f), t);
        }

        /// A line from a to b, kept to inside the circle round c (for patterns on a round body).
        void ClipLine(V2 c, float rad, V2 a, V2 b, float w, Rgba col)
        {
            V2 d = b - a;
            float len = d.Length;
            if (len < 0.01f)
                return;
            V2 u = d / len;
            float proj = (c - a).Dot(u);
            V2 near = a + u * proj;
            float h = rad * rad - (near - c).LengthSq;
            if (h <= 0f)
                return;
            float k = Mathf.Sqrt(h);
            float t0 = Mathf.Max(0f, proj - k), t1 = Mathf.Min(len, proj + k);
            if (t1 > t0)
                Line(a + u * t0, a + u * t1, w, col);
        }

        // ---- 3 Phantom ----

        void DrawPhantom(Enemy e, V2 pos, float r, float face, float bladeAng, bool up, bool worn, float t)
        {
            V2.Axes(face, out V2 f, out V2 s);
            // The ribbon: a crimson sash trailing in a wave (torn: shorter, in two pieces).
            int seg = worn ? 9 : 14;
            V2 prev = pos - f * (r * 0.4f);
            for (int k = 1; k <= seg; k++)
            {
                if (worn && k == 6)
                {
                    prev = pos - f * (r * (0.4f + 0.18f * (k + 1))) + s * (Mathf.Sin(t * 6f - (k + 1) * 0.6f) * r * 0.35f);
                    continue;
                }
                V2 at = pos - f * (r * (0.4f + 0.18f * k)) + s * (Mathf.Sin(t * 6f - k * 0.6f) * r * 0.35f * (k / (float)seg));
                Line(prev, at, 6f - 3.5f * k / seg, DuelCrimson.WithAlpha(0.85f - 0.5f * k / seg));
                prev = at;
            }
            // Afterimages: two fading copies a step behind.
            for (int k = 2; k >= 1; k--)
            {
                V2 c = pos - f * (r * 0.6f * k) + s * (Mathf.Sin(t * 3f + k) * 3f);
                PhantomDiamond(c, r * (1f - 0.12f * k), f, s, (k == 1 ? DuelSteel : DuelCrimson).WithAlpha(0.22f / k));
            }
            // Body: an ink diamond, a steel one, a dark core, a sheen down one face.
            PhantomDiamond(pos, r + 3.5f, f, s, DuelInk);
            PhantomDiamond(pos, r, f, s, worn ? DuelCrimson : DuelSteel);
            PhantomDiamond(pos, r * 0.78f, f, s, DuelDark);
            Line(pos + f * (r * 0.95f) - s * (r * 0.08f), pos - s * (r * 0.75f), 2f, DuelWhite.WithAlpha(0.45f));
            // The chrome mask: a pale oval with a glowing crimson slit.
            V2 mc = pos + f * (r * 0.22f);
            DuelEllipse(mc, f * (r * 0.38f), s * (r * 0.44f), new Rgba(0.78f, 0.82f, 0.92f), 16);
            DuelEllipse(mc - s * (r * 0.1f) - f * (r * 0.06f), f * (r * 0.2f), s * (r * 0.2f), DuelWhite.WithAlpha(0.6f), 12);  // the shine
            float glow = 0.6f + 0.4f * Mathf.Sin(t * 7f);
            V2 slit = mc + f * (r * 0.16f);
            Line(slit - s * (r * 0.34f), slit + s * (r * 0.34f), 7f, DuelCrimson.WithAlpha(0.3f * glow));
            Line(slit - s * (r * 0.3f), slit + s * (r * 0.3f), 2.6f, DuelCrimson);
            if (worn)
                DuelCrack(mc + f * (r * 0.36f) - s * (r * 0.15f), -f + s * 0.3f, s, r * 0.5f);
            DuelRapier(DuelHand(pos, r, face, up), up ? -MathX.Pi / 2f : bladeAng, r * 2.5f, false, DuelCrimson.WithAlpha(0.35f + 0.15f * glow), t);
        }

        /// A diamond pointing along f: long in front, short behind.
        void PhantomDiamond(V2 c, float r, V2 f, V2 s, Rgba col)
        {
            SetColor(col);
            V2 a = c + f * (r * 1.5f), b = c + s * (r * 0.66f), d = c - f * (r * 0.9f), e = c - s * (r * 0.66f);
            Tri(a, b, d);
            Tri(a, d, e);
        }
    }
}
