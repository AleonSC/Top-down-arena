using UnityEngine;

namespace Arena
{
    /// How the Warder and the bubbles it (and Shielded elites) hold up look. Built from the mockups in
    /// Docs/Mockups/warder-rework.html (option A, the Censer Priest) and Docs/Mockups/shield-tells.html (option A, Cells).
    ///
    /// The bubble is twelve cells round the rim. Each cell is a twelfth of the shield and wears down on its own: a hit
    /// flashes and weakens the cell facing the shooter, then breaks it into shards (Variety.cs works out which; this
    /// peer sees it as the Enemy.Cells mask and flashes whatever changed).
    public partial class ArenaApp
    {
        static readonly Rgba WardLite = new Rgba(0.9f, 0.86f, 1f);
        static readonly Rgba EliteLite = new Rgba(0.87f, 0.95f, 1f);
        static readonly Rgba WardInk = new Rgba(0.11f, 0.08f, 0.19f);
        const float CellFlashLen = 0.3f, CellBreakLen = 0.45f, SigilFlight = 0.5f;

        /// The Warder's body: a pointed diamond aimed at its target, a lit facet, an eye that goes white while it charges,
        /// three hex sigils orbiting it (one leaves for each ally it wards), and a chevron over it while a ward is up.
        void DrawWarderBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float k = e.Phase == 1 ? Mathf.Clamp01(e.SeenT / Game.WarderCharge) : 0f;  // charging up to its next ward
            float R = r * 1.25f, ang = e.Facing + Mathf.Sin(t * 0.8f + e.Id) * 0.08f;
            V2 f = V2.FromAngle(ang), s = f.Orthogonal();
            V2 tip = pos + f * (R * 1.15f), right = pos + s * (R * 0.8f), back = pos - f * (R * 0.95f), left = pos - s * (R * 0.8f);
            SetColor(col.Darkened(0.22f));
            Tri(tip, right, back);
            Tri(tip, back, left);
            SetColor(col);  // the lit facet
            Tri(tip, right, pos - f * (R * 0.1f));
            Line(tip, right, 2.2f, WardInk);
            Line(right, back, 2.2f, WardInk);
            Line(back, left, 2.2f, WardInk);
            Line(left, tip, 2.2f, WardInk);
            Circle(pos + f * (R * 0.18f), R * 0.22f + k * 2f, k > 0.05f ? Rgba.White : WardInk, 12);

            int warded = 0;
            foreach (var o in game.Enemies.Values)
                if (o.WardId == e.Id && o.BubbleFrac > 0f)
                    warded++;
            float spin = 1.1f + k * 5f;
            for (int i = 0; i < 3 - Mathf.Min(3, warded); i++)  // the ones not out holding a bubble
            {
                float a = t * spin + i * MathX.Tau / 3f + e.Id;
                V2 at = pos + V2.FromAngle(a) * (r + 15f + k * 4f);
                float sr = 4.5f + k * 1.5f;
                Polygon(at, sr + 1.4f, 6, a, WardInk);
                Polygon(at, sr, 6, a, k > 0.05f ? WardLite : col);
            }
            if (warded > 0 && !game.IsSpawning(e))  // a chevron: this is the one to shoot first
            {
                float dy = Mathf.Sin(t * 3f) * 2f;
                V2 m = pos + new V2(0f, -r - 22f + dy);
                SetColor(WardLite);
                Tri(m + new V2(-6f, -9f), m + new V2(6f, -9f), m);
                Line(m + new V2(-6f, -14f), m + new V2(0f, -9f), 2.2f, WardLite);
                Line(m + new V2(6f, -14f), m + new V2(0f, -9f), 2.2f, WardLite);
            }
        }

        /// The Warder's reach filling up while it charges, drawn on the floor under everything.
        void DrawWarderCharge(Enemy e, V2 pos)
        {
            if (e.Phase != 1)
                return;
            float k = Mathf.Clamp01(e.SeenT / Game.WarderCharge);
            Arc(pos, Game.WarderRange, 1.5f, WardViolet.WithAlpha(0.1f + 0.25f * k), 64);
            Circle(pos, Game.WarderRange * k, WardViolet.WithAlpha(0.06f * (1f - k)), 48);
        }

        /// A tether from the Warder to a warded enemy: a faint line with beads flowing along it.
        void DrawWardTether(V2 a, V2 b, float t)
        {
            Line(a, b, 1f, WardViolet.WithAlpha(0.3f));
            V2 d = b - a;
            float len = d.Length;
            if (len < 20f)
                return;
            V2 u = d / len;
            for (float s = (t * 55f) % 16f; s < len; s += 16f)
                Circle(a + u * s, 2f, WardLite.WithAlpha(0.9f), 6);
        }

        /// A bubble over an enemy: twelve cells round the rim, a honeycomb inside that only shows where its cell is
        /// still up, shards where a cell broke, and a sigil flying in from the Warder as a ward lands.
        void DrawBubble(Enemy e, V2 pos, float r, float t)
        {
            bool warded = e.WardId != 0;
            Rgba c = warded ? WardViolet : BubbleBlue, lite = warded ? WardLite : EliteLite;
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

            Enemy w;
            if (warded && t - e.BubbleBornT < SigilFlight && game.Enemies.TryGetValue(e.WardId, out w))  // the sigil flying in
            {
                float u = (t - e.BubbleBornT) / SigilFlight, ease = 1f - (1f - u) * (1f - u) * (1f - u);
                V2 d = pos - w.Disp, side = d.IsZero ? V2.Zero : d.Orthogonal().Normalized();
                V2 at = w.Disp + d * ease + side * (Mathf.Sin(ease * Mathf.PI) * 14f);
                Polygon(at, 5.4f, 6, t * 6f, WardInk);
                Polygon(at, 4f, 6, t * 6f, WardLite);
            }
        }

        /// Called for an enemy with no bubble: the next one that goes up is new, so it grows in and a sigil flies to it.
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
            Line(b, b + new V2(w * Mathf.Clamp01(e.BubbleFrac), 0f), 3f, e.WardId != 0 ? WardLite : EliteLite);
        }
    }
}
