using UnityEngine;

namespace Arena
{
    /// MOCKUP (owner Nov 19): how the node tiers (Docs/Nodes.md "Node tiers") could show on the ground outside the hub:
    /// the bronze / silver / gold zones 0-15, 15-30 and 30+ tiles off the hub's wall (`Expansion.TierMid`, `TierOuter`).
    /// `TierBandStyle` 0 is off (the game); 1 Metal bands, 2 Contours, 3 Range rings are the options to pick from.
    /// Drawn under the rooms by `DrawRoomsFloor`; preview `TIERBANDS=1|2|3 EXPANDMAP=0 ZOOM=0.2 shot.sh x hud`.
    public partial class ArenaApp
    {
        public static int TierBandStyle;

        const float TierReach = 4800f;  // how far out the outer zone is drawn
        const float Pi2 = Mathf.PI / 2f;

        void DrawTierBands(float t)
        {
            switch (TierBandStyle)
            {
                case 1: TierBandsMetal(t); break;
                case 2: TierBandsContours(t); break;
                case 3: TierBandsRings(t); break;
            }
        }

        static float TierEdge(int tier) => tier == 0 ? 0f : tier == 1 ? Expansion.TierMid : tier == 2 ? Expansion.TierOuter : TierReach;

        // --- the hub's box grown by d: four sides and four corner quarter-circles ---


        /// The ground between offsets d0 and d1 off the hub's wall, in `col`.
        void TierFill(float d0, float d1, Rgba col)
        {
            float W = Game.Width, H = Game.Height;
            FillRect(-d1, -d1, W + 2f * d1, d1 - d0, col);  // the box grown by d1, less the box grown by d0
            FillRect(-d1, H + d0, W + 2f * d1, d1 - d0, col);
            FillRect(-d1, -d0, d1 - d0, H + 2f * d0, col);
            FillRect(W + d0, -d0, d1 - d0, H + 2f * d0, col);
        }

        /// A fade: `steps` slices of the band, getting stronger toward `outer` (the far edge) or toward the near edge.
        void TierFade(float d0, float d1, Rgba col, float aNear, float aFar, int steps)
        {
            float w = (d1 - d0) / steps;
            for (int s = 0; s < steps; s++)
                TierFill(d0 + s * w, d0 + (s + 1) * w + 0.5f, col.WithAlpha(Mathf.Lerp(aNear, aFar, (s + 0.5f) / steps)));
        }

        /// One outline round the hub at offset d: solid or dashed.
        void TierRing(float d, float width, Rgba col, float dash, float gap, float phase)
        {
            float W = Game.Width, H = Game.Height;
            var side = new[] { (new V2(-d, -d), new V2(W + d, -d)), (new V2(W + d, -d), new V2(W + d, H + d)), (new V2(W + d, H + d), new V2(-d, H + d)), (new V2(-d, H + d), new V2(-d, -d)) };
            for (int i = 0; i < 4; i++)
            {
                if (dash <= 0f) Line(side[i].Item1, side[i].Item2, width, col);
                else PhosDash(side[i].Item1, side[i].Item2, dash, gap, phase, width, col);
            }
        }

        /// The tier's mark: a row of three square cells filled up to it (as under a prize node), at c, `k` times the size.
        void TierCells(V2 c, int tier, float k, float a)
        {
            var ink = TierInk[tier];
            float cell = 22f * k, gap = 6f * k, x0 = c.X - (3f * cell + 2f * gap) / 2f;
            for (int i = 0; i < 3; i++)
            {
                float x = x0 + i * (cell + gap);
                if (i <= tier) FillRect(x, c.Y - cell / 2f, cell, cell, ink.WithAlpha(a));
                else NodeFrame(x, c.Y - cell / 2f, cell, cell, 3f * k, ink.WithAlpha(a * 0.35f));
            }
        }

        /// The tier's mark at the middle of each of the hub's four sides at offset d, set on the line.
        void TierBadges(float d, int tier, float k, float a, bool plate)
        {
            float W = Game.Width, H = Game.Height;
            foreach (var p in new[] { new V2(W / 2f, -d), new V2(W / 2f, H + d), new V2(-d, H / 2f), new V2(W + d, H / 2f) })
            {
                if (plate) FillRect(p.X - 62f * k, p.Y - 20f * k, 124f * k, 40f * k, new Rgba(0.04f, 0.05f, 0.08f, 0.9f));
                if (plate) NodeFrame(p.X - 62f * k, p.Y - 20f * k, 124f * k, 40f * k, 3f * k, TierInk[tier].WithAlpha(a));
                TierCells(p, tier, k, a);
            }
        }

        // --- 1 Metal bands: each zone a wash of its metal, strongest at its far edge, with a solid line where the next begins ---

        void TierBandsMetal(float t)
        {
            for (int z = 0; z < 3; z++)
            {
                float d0 = TierEdge(z), d1 = TierEdge(z + 1);
                var ink = TierInk[z];
                if (z == 0) d0 = 40f;  // (the hub's own walls start the first one)
                TierFade(d0, d1, ink, 0.015f, 0.09f, 8);
                if (z < 2)
                {
                    TierRing(d1, 10f, ink.WithAlpha(0.12f), 0f, 0f, 0f);
                    TierRing(d1, 3f, ink.WithAlpha(0.75f), 0f, 0f, 0f);
                    TierBadges(d1, z + 1, 1.2f, 0.9f, true);
                }
            }
        }

        // --- 2 Contours: thin dashed lines like a map's, drifting slowly outward, with chevrons (one, two, three) pointing the way up a tier ---

        void TierBandsContours(float t)
        {
            float W = Game.Width, H = Game.Height;
            for (int z = 0; z < 3; z++)  // light shading: each zone a soft wash of its metal, a touch stronger toward its far edge
                TierFade(z == 0 ? 40f : TierEdge(z), TierEdge(z + 1), TierInk[z], 0.05f, 0.12f, 6);
            for (int z = 1; z <= 2; z++)
            {
                float d = TierEdge(z);
                var ink = TierInk[z];
                TierRing(d, 9f, ink.WithAlpha(0.8f), 70f, 40f, t * 18f);
                TierRing(d + 40f, 4f, ink.WithAlpha(0.35f), 20f, 30f, -t * 12f);
                TierRing(d - 40f, 4f, ink.WithAlpha(0.35f), 20f, 30f, -t * 12f);
            }
            for (int z = 0; z < 3; z++)  // chevrons in the middle of each zone, along the straight stretches, pointing out
            {
                float mid = z == 2 ? Expansion.TierOuter + 700f : (TierEdge(z) + TierEdge(z + 1)) / 2f;
                if (z == 0) mid = 700f;
                var ink = TierInk[z].WithAlpha(0.55f);
                for (int n = 0; n <= z; n++)
                {
                    float off = mid + (n - z / 2f) * 46f;
                    for (float x = 200f; x < W; x += 400f)
                    {
                        TierChevron(new V2(x, -off), new V2(0f, -1f), ink);
                        TierChevron(new V2(x, H + off), new V2(0f, 1f), ink);
                    }
                    for (float y = 150f; y < H; y += 300f)
                    {
                        TierChevron(new V2(-off, y), new V2(-1f, 0f), ink);
                        TierChevron(new V2(W + off, y), new V2(1f, 0f), ink);
                    }
                }
            }
        }

        void TierChevron(V2 at, V2 dir, Rgba col)
        {
            V2 side = dir.Orthogonal();
            Line(at - side * 30f - dir * 18f, at + dir * 18f, 9f, col);
            Line(at + side * 30f - dir * 18f, at + dir * 18f, 9f, col);
        }

        // --- 3 Range rings: a radar's rings, ticked every 100 px, a dark plate with the tier cells at each side, the far zone cross-hatched ---

        void TierBandsRings(float t)
        {
            for (float d = 100f; d < TierReach; d += 100f)  // fine ticks
            {
                int z = d >= Expansion.TierOuter ? 2 : d >= Expansion.TierMid ? 1 : 0;
                bool major = d % 500f == 0f;
                var ink = TierInk[z].WithAlpha(major ? 0.28f : 0.12f);
                TierRing(d, major ? 2f : 1f, ink, 0f, 0f, 0f);
            }
            for (int z = 1; z <= 2; z++)
            {
                float d = TierEdge(z);
                var ink = TierInk[z];
                TierFill(d - 6f, d + 6f, ink.WithAlpha(0.18f));
                TierRing(d, 4f, ink.WithAlpha(0.9f), 0f, 0f, 0f);
                TierBadges(d, z, 1.5f, 1f, true);
            }
            
        }
    }
}
