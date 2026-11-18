using UnityEngine;

namespace Arena
{
    /// How a Shielded elite's bubble looks. Built from the mockup in Docs/Mockups/shield-tells.html (option A, Cells).
    ///
    /// The bubble is twelve cells round the rim. Each cell is a twelfth of the shield and wears down on its own: a hit
    /// flashes and weakens the cell facing the shooter, then breaks it into shards (Variety.cs works out which; this
    /// peer sees it as the Enemy.Cells mask and flashes whatever changed).
    public partial class ArenaApp
    {
        static readonly Rgba EliteLite = new Rgba(0.87f, 0.95f, 1f);
        const float CellFlashLen = 0.3f, CellBreakLen = 0.45f;

        /// A bubble over an enemy: twelve cells round the rim, a honeycomb inside that only shows where its cell is
        /// still up, and shards where a cell broke.
        void DrawBubble(Enemy e, V2 pos, float r, float t)
        {
            Rgba c = BubbleBlue, lite = EliteLite;
            int m = e.Cells;
            if (m == 0)  // an old peer's snapshot: the plain ring
            {
                Circle(pos, r + 11f, c.WithAlpha(0.07f + 0.1f * e.BubbleFrac), 28);
                Arc(pos, r + 11f, 2f, c.WithAlpha(0.35f + 0.5f * e.BubbleFrac), 28);
                return;
            }
            const int N = Game.BubbleCells;
            if (e.CellHitT == null)
            {
                e.CellHitT = new float[N];
                e.CellDieT = new float[N];
                for (int i = 0; i < N; i++)
                    e.CellHitT[i] = e.CellDieT[i] = -99f;
            }
            if (e.CellsSeen == 0)
                e.BubbleBornT = e.BubbleBornT > -98.5f ? t : -99f;
            else if (e.CellsSeen != m)
                for (int i = 0; i < N; i++)
                {
                    bool was = (e.CellsSeen >> i & 1) != 0, up = (m >> i & 1) != 0;
                    bool wasWorn = (e.CellsSeen >> (i + N) & 1) != 0, worn = (m >> (i + N) & 1) != 0;
                    if (was && !up)
                        e.CellDieT[i] = t;
                    else if (up && worn && !wasWorn)
                        e.CellHitT[i] = t;
                }
            e.CellsSeen = m;

            float born = Mathf.Clamp01((t - e.BubbleBornT) / 0.4f), grow = 1f - (1f - born) * (1f - born) * (1f - born);
            float R = (r + 12f) * (0.6f + 0.4f * grow), alpha = Mathf.Clamp01(born * 2f);
            Circle(pos, R, c.WithAlpha((0.05f + 0.09f * e.BubbleFrac) * alpha), 28);

            if (fxOn && born >= 1f)  // the honeycomb, lit only where its cell is still up
            {
                const float hr = 6f;
                for (float gy = -R; gy <= R; gy += 9f)
                {
                    float shift = (Mathf.RoundToInt(gy / 9f) & 1) != 0 ? 5.2f : 0f;
                    for (float gx = -R; gx <= R; gx += 10.4f)
                    {
                        V2 h = new V2(gx + shift, gy);
                        if (h.Length > R - hr)
                            continue;
                        int cell = Game.CellAt(h.Angle);
                        if ((m >> cell & 1) == 0)
                            continue;
                        float lit = Mathf.Clamp01(1f - (t - e.CellHitT[cell]) / CellFlashLen);
                        Rgba hc = c.WithAlpha(0.16f + 0.5f * lit);
                        for (int k = 0; k < 6; k++)
                        {
                            float a0 = MathX.Tau * k / 6f, a1 = MathX.Tau * (k + 1) / 6f;
                            Line(pos + h + V2.FromAngle(a0) * hr, pos + h + V2.FromAngle(a1) * hr, 1f, hc);
                        }
                    }
                }
            }

            float step = MathX.Tau / N;
            for (int i = 0; i < N; i++)
            {
                float centre = -MathX.Tau * 0.25f + i * step, a0 = centre - step * 0.5f + 0.05f, sweep = step - 0.1f;
                if ((m >> i & 1) != 0)
                {
                    bool worn = (m >> (i + N) & 1) != 0;
                    float flash = Mathf.Clamp01(1f - (t - e.CellHitT[i]) / CellFlashLen);
                    float shimmer = 0.8f + 0.2f * Mathf.Sin(t * 3f - i * 0.6f);
                    Rgba cc = worn ? c.WithAlpha((0.55f + 0.3f * Mathf.Sin(t * 10f + i)) * alpha) : lite.WithAlpha(shimmer * alpha);
                    if (flash > 0f)
                        cc = cc.Mix(Rgba.White, flash);
                    Arc(pos, R, 4.5f + 2f * flash, cc, 5, a0, sweep);
                }
                else
                {
                    float since = (t - e.CellDieT[i]) / CellBreakLen;
                    Arc(pos, R, 1.6f, c.WithAlpha(0.3f * alpha), 4, a0 + 0.12f, sweep - 0.24f);  // the empty slot
                    if (since >= 0f && since < 1f)  // shards flying off
                        for (int s = 0; s < 3; s++)
                        {
                            float da = (s - 1) * 0.12f;
                            V2 at = pos + V2.FromAngle(centre + da) * (R + 4f + since * 26f * (1f - Mathf.Abs(da)));
                            Polygon(at, 3f * (1f - since), 3, centre + since * 6f, lite.WithAlpha(1f - since));
                        }
                }
            }
        }

        /// Called for an enemy with no bubble: the next one that goes up is new, so it grows in.
        void NoBubble(Enemy e)
        {
            e.CellsSeen = 0;
            e.BubbleBornT = -98f;
        }

        /// A thin shield bar above the health bar: the bubble's whole health, in its colour.
        void DrawShieldBar(Enemy e, V2 pos, float r)
        {
            float w = Mathf.Max(30f, r * 2f);
            V2 b = pos + new V2(-w / 2f, -r - 17f);
            Line(b, b + new V2(w, 0f), 3f, new Rgba(0f, 0f, 0f, 0.6f));
            Line(b, b + new V2(w * Mathf.Clamp01(e.BubbleFrac), 0f), 3f, EliteLite);
        }
    }
}
