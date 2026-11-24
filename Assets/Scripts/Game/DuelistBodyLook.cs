using UnityEngine;

namespace Arena
{
    /// The Duelist's body, the Maestro (the Masked Court's master, CourtLook.cs): a dark fencer with a steel rim, a full
    /// white mask with gold filigree and two eye slits, gold epaulettes, a crimson cape with a gold hem swaying behind,
    /// and a swept-hilt rapier posed for each move (DuelPose). From To the Death on it's battle-worn: a crimson rim, a
    /// cracked mask, a torn cape. Its decoys look the same. The guard arc, salute glint and stagger: DuelistLook.cs.
    /// Its 2 s phase changes (DuelShiftPose, from the replicated TransT/TransTo) are set pieces of their own:
    ///   into the Masquerade: it runs to the middle, sweeps a deep bow (sword out behind, cape spread), then lifts its
    ///     mask aside (a dark face, violet eyes) while three violet silhouettes flicker round it, and snaps it back on;
    ///   into To the Death: it runs to the middle, points its sword at the floor and spins, its tip carving a crimson
    ///     circle, then raises the sword to its face and cuts down: a crimson flash, and it's battle-worn from there.
    public partial class ArenaApp
    {
        static readonly Rgba DuelInk = new Rgba(0.04f, 0.045f, 0.07f, 0.95f), DuelDark = new Rgba(0.1f, 0.11f, 0.16f),
            DuelMask = new Rgba(0.95f, 0.93f, 0.9f), DuelWhite = new Rgba(0.95f, 0.96f, 1f);

        void DrawDuelistBody(Enemy e, V2 pos, float r, float t)
        {
            var d = e.Duel;
            var m = new MaestroPose { Worn = d != null && d.Stage >= DuelStage.ToTheDeath, CapeLen = 1.35f, CapeWidth = 0.85f };
            m.Face = e.Facing + (d != null && d.StaggerT > 0f ? Mathf.Sin(t * 14f) * 0.35f : 0f);
            DuelPose(e, m.Face, out m.BladeAng, out m.Up);
            if (d != null && d.TransT > 0f)
                DuelShiftPose(d, ref m, t);
            DrawMaestro(e, pos, r, m, t);
        }

        /// How the Maestro is drawn this frame: its facing, the sword, the cape, and the phase changes' extras.
        struct MaestroPose
        {
            public float Face, BladeAng, CapeLen, CapeWidth;
            public bool Up, Worn;
            public float Bow;        // 0-1: dipping into a bow (the body squashes a little, the mask tips away)
            public float MaskOff;    // 0-1: the mask lifted aside, showing the face under it
            public float Ghosts;     // 0-1: the Masquerade's violet silhouettes flickering round it
            public float Carve;      // 0-1: how much of the crimson circle its tip has carved
            public float Flash;      // 0-1: the crimson flash of the cut down its mask
        }

        static readonly Rgba DuelViolet = new Rgba(0.71f, 0.61f, 1f);

        /// The 2 s phase change, by how far in it is (p): run to the middle, then the set piece for the phase it's going to.
        static void DuelShiftPose(DuelState d, ref MaestroPose m, float t)
        {
            float p = Mathf.Clamp01(1f - d.TransT / 2f);
            float run = Mathf.Clamp01(1f - p / 0.3f);
            m.CapeLen = 1.35f + 0.5f * run;  // streaming out behind as it runs in
            m.BladeAng = m.Face + 0.3f + 0.6f * run;  // sword trailing low
            if (d.TransTo == DuelStage.Masquerade)
            {
                float bow = Mathf.Sin(Mathf.Clamp01((p - 0.25f) / 0.4f) * MathX.Pi);
                m.Bow = bow;
                m.BladeAng = Mathf.Lerp(m.BladeAng, m.Face + 2.5f, bow);  // swept out and back
                m.CapeWidth = 0.85f + 0.4f * bow;
                float lift = Mathf.Clamp01((p - 0.6f) / 0.12f) * Mathf.Clamp01((0.95f - p) / 0.06f);
                m.MaskOff = lift;
                m.Ghosts = Mathf.Clamp01((p - 0.6f) / 0.15f) * Mathf.Clamp01((1f - p) / 0.1f);
            }
            else
            {
                float spin = Mathf.Clamp01((p - 0.3f) / 0.4f);
                if (spin > 0f && spin < 1f)
                {
                    m.Face += spin * MathX.Tau;  // one full turn, sword to the floor
                    m.BladeAng = m.Face + 1.4f;
                }
                m.Carve = spin;
                if (p > 0.72f)
                {
                    float cut = Mathf.Clamp01((p - 0.72f) / 0.13f);
                    m.Up = cut < 0.75f;  // raised to its face, then cut down
                    if (!m.Up)
                        m.BladeAng = m.Face + 0.9f;
                    m.Flash = p > 0.83f ? Mathf.Clamp01(1f - (p - 0.83f) / 0.17f) : 0f;
                    m.Worn = p > 0.83f;
                }
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

        void DrawMaestro(Enemy e, V2 pos, float r, MaestroPose m, float t)
        {
            float face = m.Face;
            V2.Axes(face, out V2 f, out V2 s);
            // The Masquerade's silhouettes: three violet copies flickering round it, a beat apart.
            if (m.Ghosts > 0f)
                for (int k = 0; k < 3; k++)
                {
                    float a = face + MathX.Pi + (k - 1) * 1.9f, on = 0.5f + 0.5f * Mathf.Sin(t * 22f + k * 2.1f);
                    V2 g = pos + V2.FromAngle(a) * (r * (2.2f + 0.6f * m.Ghosts));
                    Circle(g, r, DuelViolet.WithAlpha(0.22f * m.Ghosts * on), 24);
                    Arc(g, r, 2.5f, DuelViolet.WithAlpha(0.9f * m.Ghosts * on), 24);
                    DuelEllipse(g + f * (r * 0.2f), f * (r * 0.5f), s * (r * 0.55f), DuelMask.WithAlpha(0.6f * m.Ghosts * on), 14);
                }
            // To the Death: the circle its tip carves round it.
            if (m.Carve > 0f)
            {
                float span = MathX.Tau * m.Carve;
                Arc(pos, r * 2.6f, 10f, DuelCrimson.WithAlpha(0.18f), 48, e.Facing, span);
                Arc(pos, r * 2.6f, 2.5f, DuelCrimson.WithAlpha(0.9f), 48, e.Facing, span);
                if (m.Carve < 1f)
                    Circle(pos + V2.FromAngle(e.Facing + span) * (r * 2.6f), 3f + 1.5f * Mathf.Sin(t * 40f), DuelGold, 8);  // sparks at the tip
            }
            float body = r * (1f - 0.08f * m.Bow);
            Rgba rim = m.Worn ? DuelCrimson : DuelSteel;
            DuelCape(pos, body, face, r * m.CapeLen, m.CapeWidth, DuelCrimson.WithAlpha(0.9f), DuelGold, m.Worn, t, e.Id);
            // Body: ink edge, rim, dark coat.
            Circle(pos, body + 3f, DuelInk, 28);
            Circle(pos, body, rim, 28);
            Circle(pos, body - 3f, DuelDark, 28);
            // Epaulettes: gold discs with a fringe on each shoulder.
            for (int k = -1; k <= 1; k += 2)
            {
                V2 sh = pos + s * (k * body * 0.78f) - f * (body * 0.05f);
                for (int j = -2; j <= 2; j++)
                    Line(sh, sh + s * (k * body * 0.28f) + f * (j * body * 0.09f), 1.5f, DuelGold.WithAlpha(0.8f));
                Circle(sh, body * 0.2f, DuelGold, 12);
                Circle(sh, body * 0.11f, DuelDark, 10);
            }
            // The mask: a full white face toward the front, gold filigree over the brow, two slits. Bowing, it tips
            // forward; lifted, it swings out to the sword side and the dark face under it looks out with violet eyes.
            V2 mc = pos + f * (body * (0.2f + 0.12f * m.Bow));
            if (m.MaskOff > 0f)
            {
                for (int k = -1; k <= 1; k += 2)
                    Circle(mc + f * (body * 0.18f) + s * (k * body * 0.22f), 3f, DuelViolet.WithAlpha(m.MaskOff), 8);
                mc += s * (body * 0.95f * m.MaskOff) + f * (body * 0.25f * m.MaskOff);
            }
            DuelEllipse(mc, f * (body * 0.58f), s * (body * 0.62f), DuelMask, 18);
            DuelEllipseEdge(mc, f * (body * 0.58f), s * (body * 0.62f), 1.4f, DuelInk.WithAlpha(0.6f), 18);
            Arc(mc, body * 0.48f, 1.8f, DuelGold, 12, face - 1.2f, 2.4f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 eye = mc + f * (body * 0.18f) + s * (k * body * 0.25f);
                DuelEllipse(eye, s * (body * 0.14f), f * (body * 0.06f), DuelInk, 10);
            }
            if (m.Worn)
                DuelCrack(mc + f * (body * 0.5f) - s * (body * 0.1f), -f, s, body * 0.6f);
            V2 hand = m.MaskOff > 0f ? Vec(DuelHand(pos, body, face, m.Up), mc + s * (body * 0.5f), m.MaskOff) : DuelHand(pos, body, face, m.Up);
            DuelRapier(hand, m.Up ? -MathX.Pi / 2f : m.BladeAng, r * 2.5f, t);
            if (m.Flash > 0f)
            {
                Circle(pos, r + 6f + 30f * (1f - m.Flash), DuelCrimson.WithAlpha(0.35f * m.Flash), 28);
                Line(mc + s * (body * 0.6f) + f * (body * 0.6f), mc - s * (body * 0.6f) - f * (body * 0.5f), 4f * m.Flash + 1f, DuelCrimson.WithAlpha(m.Flash));
            }
        }
    }
}
