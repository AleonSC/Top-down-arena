using UnityEngine;

namespace Arena
{
    /// The walls casting light (Visual effects on, look 1 "Neon spill" of the lighting mockups): the neon of every wall
    /// and pillar throws a soft glow of the walls' colour (`Ink`, the coming boss's) across the floor next to it.
    /// Drawn in the additive glow pass (DrawWallSpill, from Juice.cs DrawGlowPass). Boxes glow only on their outside
    /// (each outside edge's open side is worked out once per wall shape and kept); pillars glow all round.
    public partial class ArenaApp
    {
        const float SpillWide = 70f, SpillWideA = 0.16f;    // the wide wash
        const float SpillNear = 22f, SpillNearA = 0.2f;     // the bright band hugging the wall
        const float PillarSpill = 2.6f, PillarSpillA = 0.28f, PillarCore = 1.4f, PillarCoreA = 0.22f;  // x the pillar's radius

        SolidUnion spillFor;
        float[] spillSide = new float[0];  // per outside edge of spillFor: +1 or -1, which way its normal points to open floor

        /// DrawGlowPass: the light of the floor plan's walls and pillars on the floor.
        void DrawWallSpill()
        {
            var d = game.ActiveLayout;
            if (d == null)
                return;
            float a = Mathf.Clamp01(game.LayoutAge / 0.9f);  // comes up with the walls as they rise
            if (a <= 0.01f)
                return;
            V2 lo = new V2(-1e9f, -1e9f), hi = new V2(1e9f, 1e9f);
            if (CamZoomed)  // a big room: only what's near the view
            {
                V2 vh = ViewHalf();  // (the turned screen's reach, CameraRotate.cs)
                float vx = vh.X + SpillWide, vy = vh.Y + SpillWide;
                lo = new V2(camFocus.X - vx, camFocus.Y - vy);
                hi = new V2(camFocus.X + vx, camFocus.Y + vy);
            }
            var u = SolidUnion.For(d.Solids, false, ref wallUnion);
            if (u != null && u.Edges.Length > 0)
            {
                SpillSides(u);
                var g = u.Edges;
                for (int i = 0, k = 0; i < g.Length; i += 4, k++)
                {
                    if (g[i + 2] < lo.X || g[i] > hi.X || g[i + 3] < lo.Y || g[i + 1] > hi.Y)
                        continue;
                    V2 p0 = new V2(g[i], g[i + 1]), p1 = new V2(g[i + 2], g[i + 3]);
                    V2 n = (p1 - p0).Normalized().Orthogonal() * spillSide[k];
                    SpillBand(p0, p1, n, SpillWide, Ink, SpillWideA * a);
                    SpillBand(p0, p1, n, SpillNear, Ink, SpillNearA * a);
                }
            }
            foreach (var s in d.Solids)
                if (s.Round && !s.Edge && s.Pos.X + s.R > lo.X && s.Pos.X - s.R < hi.X && s.Pos.Y + s.R > lo.Y && s.Pos.Y - s.R < hi.Y)
                {
                    GlowDisc(s.Pos, s.R * PillarSpill, Ink, PillarSpillA * a);
                    GlowDisc(s.Pos, s.R * PillarCore, Ink, PillarCoreA * a);
                }
        }

        /// Which side of each outside edge is open floor: the side whose point just off the edge's middle is in no box.
        void SpillSides(SolidUnion u)
        {
            if (spillFor == u && spillSide.Length * 4 == u.Edges.Length)
                return;
            spillFor = u;
            var g = u.Edges;
            spillSide = new float[g.Length / 4];
            for (int i = 0, k = 0; i < g.Length; i += 4, k++)
            {
                V2 p0 = new V2(g[i], g[i + 1]), p1 = new V2(g[i + 2], g[i + 3]);
                V2 n = (p1 - p0).Normalized().Orthogonal();
                spillSide[k] = InUnion(u, (p0 + p1) * 0.5f + n * 2f) ? -1f : 1f;
            }
        }

        static bool InUnion(SolidUnion u, V2 p)
        {
            var f = u.Fills;
            for (int i = 0; i < f.Length; i += 4)
                if (p.X > f[i] && p.X < f[i] + f[i + 2] && p.Y > f[i + 1] && p.Y < f[i + 1] + f[i + 3])
                    return true;
            return false;
        }

        /// Light on one side of a wall edge, fading out over `w` like a GlowDisc (full, 0.38 at 30%, none at the rim),
        /// with a quarter fan past each end at half strength, so the two edges meeting at a box's corner round it off.
        void SpillBand(V2 p0, V2 p1, V2 n, float w, Rgba col, float a)
        {
            Rgba on = col.WithAlpha(a), mid = col.WithAlpha(a * 0.38f), off = col.WithAlpha(0f);
            V2 m = n * (w * 0.3f), o = n * w;
            VTri(p0 + o, off, p1 + o, off, p1 + m, mid);
            VTri(p0 + o, off, p1 + m, mid, p0 + m, mid);
            VTri(p0 + m, mid, p1 + m, mid, p1, on);
            VTri(p0 + m, mid, p1, on, p0, on);
            V2 along = (p1 - p0).Normalized();
            SpillFan(p1, n, along, w, col, a * 0.5f);
            SpillFan(p0, n, -along, w, col, a * 0.5f);
        }

        /// A quarter of a GlowDisc at `c`, between the directions `n` and `e`.
        void SpillFan(V2 c, V2 n, V2 e, float w, Rgba col, float a)
        {
            const int segs = 3;
            Rgba on = col.WithAlpha(a), mid = col.WithAlpha(a * 0.38f), off = col.WithAlpha(0f);
            for (int i = 0; i < segs; i++)
            {
                float t0 = MathX.Pi / 2f * i / segs, t1 = MathX.Pi / 2f * (i + 1) / segs;
                V2 d0 = n * Mathf.Cos(t0) + e * Mathf.Sin(t0), d1 = n * Mathf.Cos(t1) + e * Mathf.Sin(t1);
                VTri(c + d0 * w, off, c + d1 * w, off, c + d1 * (w * 0.3f), mid);
                VTri(c + d0 * w, off, c + d1 * (w * 0.3f), mid, c + d0 * (w * 0.3f), mid);
                VTri(c + d0 * (w * 0.3f), mid, c + d1 * (w * 0.3f), mid, c, on);
            }
        }
    }
}
