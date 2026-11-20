using UnityEngine;

namespace Arena
{
    /// How a health shrine looks (Shrines.cs, Docs/Nodes.md): on the ghost marker a green cross; once a room claims it, a stone plinth in its cell with
    /// corner posts, a floating green cross over a glow and motes rising, ringed where you walk up to it. Used (by you): grey and still.
    /// A player with health past max wears a gold ring (DrawOverHealth). Preview: EXPANDNODE=shrine:x,y [SHRINEUSED=1] [OVERHP=<n>].
    public partial class ArenaApp
    {
        static readonly Rgba ShrineInk = new Rgba(0.35f, 1f, 0.55f);
        static readonly Rgba ShrineStone = new Rgba(0.2f, 0.24f, 0.27f);
        static readonly Rgba ShrineUsedInk = new Rgba(0.5f, 0.56f, 0.6f);

        /// A health cross: two bars, a hot core.
        void ShrineCross(V2 c, float k, Rgba col)
        {
            float arm = 15f * k, w = 6f * k;
            FillRect(c.X - arm, c.Y - w, 2f * arm, 2f * w, col.WithAlpha(col.A * 0.85f));
            FillRect(c.X - w, c.Y - arm, 2f * w, 2f * arm, col.WithAlpha(col.A * 0.85f));
            var hot = col.Mix(new Rgba(1f, 1f, 1f), 0.6f).WithAlpha(col.A);
            FillRect(c.X - arm + 3f * k, c.Y - 1.5f * k, 2f * arm - 6f * k, 3f * k, hot);
            FillRect(c.X - 1.5f * k, c.Y - arm + 3f * k, 3f * k, 2f * arm - 6f * k, hot);
        }

        /// The claimed shrine standing at c; used: you've had yours.
        void DrawShrine(V2 c, bool used, float t)
        {
            float pulse = used ? 0f : 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            var ink = used ? ShrineUsedInk : ShrineInk;
            if (!used)
            {
                Circle(c, 96f, ink.WithAlpha(0.05f + 0.04f * pulse), 36);
                Circle(c, 70f, ink.WithAlpha(0.08f + 0.05f * pulse), 32);
                Arc(c, Shrines.Reach + 26f + 4f * pulse, 2f, ink.WithAlpha(0.35f + 0.25f * pulse), 36);  // walk up to it
                for (int k = 0; k < 4; k++)
                    Arc(c, Shrines.Reach + 38f, 3f, ink.WithAlpha(0.5f), 8, t * 0.5f + k * MathX.Tau / 4f, 0.5f);
            }
            // the plinth: an octagon of stone, a lighter top, a lit seam
            Polygon(c + new V2(0f, 4f), 42f, 8, MathX.Tau / 16f, new Rgba(0f, 0f, 0f, 0.35f));
            Polygon(c, 40f, 8, MathX.Tau / 16f, ShrineStone);
            Polygon(c, 31f, 8, MathX.Tau / 16f, ShrineStone.Mix(new Rgba(1f, 1f, 1f), 0.12f));
            Arc(c, 35f, 2f, ink.WithAlpha(used ? 0.3f : 0.55f + 0.3f * pulse), 8, MathX.Tau / 16f);
            for (int k = 0; k < 4; k++)  // corner posts with lit caps
            {
                float a = MathX.Tau / 8f + k * MathX.Tau / 4f;
                V2 q = c + new V2(Mathf.Cos(a) * 50f, Mathf.Sin(a) * 50f);
                FillRect(q.X - 6f, q.Y - 6f, 12f, 12f, ShrineStone);
                FillRect(q.X - 3f, q.Y - 3f, 6f, 6f, ink.WithAlpha(used ? 0.35f : 0.6f + 0.4f * pulse));
            }
            if (used)
            {
                ShrineCross(c, 0.9f, ink.WithAlpha(0.45f));
                return;
            }
            // the floating cross over its glow, and motes rising off the plinth
            float bob = Mathf.Sin(t * 2.6f) * 4f;
            V2 x = c + new V2(0f, -26f + bob);
            Circle(x, 30f, ink.WithAlpha(0.14f + 0.1f * pulse), 24);
            Circle(x, 18f, ink.WithAlpha(0.2f + 0.1f * pulse), 20);
            ShrineCross(x, 1.15f, ink);
            for (int k = 0; k < 6; k++)
            {
                float ph = (t * 0.5f + k / 6f) % 1f, a = k * 2.4f;
                V2 m = c + new V2(Mathf.Cos(a) * 26f, Mathf.Sin(a) * 14f - 70f * ph);
                Circle(m, 2.5f * (1f - ph) + 1f, ink.WithAlpha(0.8f * (1f - ph)), 8);
            }
        }

        /// A player carrying health past max: a gold ring and three turning ticks round them.
        void DrawOverHealth(Player p, float t)
        {
            float r = p.Def.BodyR + 12f, a = Mathf.Min(1f, 0.45f + p.OverHp / Mathf.Max(1f, p.MaxHp));
            var gold = new Rgba(1f, 0.82f, 0.32f);
            Arc(p.Disp, r, 2f, gold.WithAlpha(0.45f * a), 32);
            for (int k = 0; k < 3; k++)
                Arc(p.Disp, r + 4f, 3f, gold.WithAlpha(0.85f * a), 6, t * 1.6f + k * MathX.Tau / 3f, 0.55f);
        }
    }
}
