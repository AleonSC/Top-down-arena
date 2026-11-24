using UnityEngine;

namespace Arena
{
    /// The Duelist's body, the Maestro (the Masked Court's master, CourtLook.cs): a dark fencer with a steel rim, a full
    /// white mask with gold filigree and two eye slits, gold epaulettes, a crimson cape with a gold hem swaying behind,
    /// and a swept-hilt rapier posed for each move (DuelPose). From To the Death on it's battle-worn: a crimson rim, a
    /// cracked mask, a torn cape. Its decoys look the same. The guard arc, salute glint and stagger: DuelistLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba DuelInk = new Rgba(0.04f, 0.045f, 0.07f, 0.95f), DuelDark = new Rgba(0.1f, 0.11f, 0.16f),
            DuelMask = new Rgba(0.95f, 0.93f, 0.9f), DuelWhite = new Rgba(0.95f, 0.96f, 1f);

        void DrawDuelistBody(Enemy e, V2 pos, float r, float t)
        {
            var d = e.Duel;
            bool worn = d != null && d.Stage >= DuelStage.ToTheDeath;
            float face = e.Facing + (d != null && d.StaggerT > 0f ? Mathf.Sin(t * 14f) * 0.35f : 0f);
            DuelPose(e, face, out float bladeAng, out bool up);
            DrawMaestro(e, pos, r, face, bladeAng, up, worn, t);
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

        /// A rapier from `hand`: blade, ricasso, a gold cross-guard and a gold knuckle bow round the hand.
        void DuelRapier(V2 hand, float ang, float len, float t)
        {
            V2 d = V2.FromAngle(ang), s = d.Orthogonal();
            Line(hand + d * 4f, hand + d * len, 3f, DuelWhite);
            Line(hand + d * 4f, hand + d * 14f, 4.5f, new Rgba(0.7f, 0.74f, 0.85f));
            Line(hand - s * 9f + d * 4f, hand + s * 9f + d * 4f, 3.5f, DuelGold);
            Circle(hand - s * 9f + d * 4f, 2.4f, DuelGold, 8);
            Circle(hand + s * 9f + d * 4f, 2.4f, DuelGold, 8);
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

        void DrawMaestro(Enemy e, V2 pos, float r, float face, float bladeAng, bool up, bool worn, float t)
        {
            V2.Axes(face, out V2 f, out V2 s);
            Rgba rim = worn ? DuelCrimson : DuelSteel;
            DuelCape(pos, r, face, r * 1.35f, 0.85f, DuelCrimson.WithAlpha(0.9f), DuelGold, worn, t, e.Id);
            // Body: ink edge, rim, dark coat.
            Circle(pos, r + 3f, DuelInk, 28);
            Circle(pos, r, rim, 28);
            Circle(pos, r - 3f, DuelDark, 28);
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
            DuelRapier(DuelHand(pos, r, face, up), up ? -MathX.Pi / 2f : bladeAng, r * 2.5f, t);
        }
    }
}
