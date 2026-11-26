using UnityEngine;

namespace Arena
{
    /// The Bone Guard's Marrow Tether threads (FxKind.MarrowLink) and its Alt, the Ossuary Wall (ZoneKind.OssuaryWall)
    /// (Core/BoneGuard.cs; mockup Docs/Mockups/bone-guard-rework.html, concept B).
    public partial class ArenaApp
    {
        static readonly Rgba MarrowThread = new Rgba(0.82f, 0.28f, 0.37f), MarrowBead = new Rgba(1f, 0.6f, 0.66f);
        static readonly Rgba FemurShade = new Rgba(0.66f, 0.6f, 0.48f), WallGlow = new Rgba(0.62f, 0.95f, 0.84f);

        /// A marrow thread between two enemies: a sagging red strand with beads running along it while it lasts, or
        /// snapped taut and bright for 0.45 s as one is caged and the other reeled in.
        void DrawMarrowLinkFx(Effect fx, float f, float t)
        {
            Enemy a, b;
            int ia = (int)fx.Pos2.X, ib = (int)fx.Pos2.Y;
            if (!game.Enemies.TryGetValue(ia, out a) || !game.Enemies.TryGetValue(ib, out b))
                return;
            V2 pa = a.Disp, pb = b.Disp;
            if (fx.Radius >= 0.5f)  // taut: the yank
            {
                if (fx.T > 0.45f)
                    return;  // (the flash is over; the effect stays only to hide the spent loose thread)
                float al = 1f - fx.T / 0.45f;
                Line(pa, pb, 4.5f, MarrowThread.WithAlpha(0.3f * al));
                Line(pa, pb, 2.2f, MarrowBead.WithAlpha(al));
                return;
            }
            foreach (var o in FxOf(FxKind.MarrowLink))  // a loose thread gives way to a taut one for the same pair
                if (o.Kind == FxKind.MarrowLink && o.Radius >= 0.5f && o.T < o.Dur
                    && ((int)o.Pos2.X == ia && (int)o.Pos2.Y == ib || (int)o.Pos2.X == ib && (int)o.Pos2.Y == ia))
                    return;
            float fade = Mathf.Clamp01(fx.T / 0.12f) * Mathf.Clamp01((fx.Dur - fx.T) / 0.4f);
            V2 d = pb - pa;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 side = d.Orthogonal() / len, mid = (pa + pb) * 0.5f + side * (6f + len * 0.06f) * (1f + 0.25f * Mathf.Sin(t * 3f + ia));
            V2 prev = pa;  // (Bez: KusarigamaLook.cs)
            const int segs = 10;
            for (int k = 1; k <= segs; k++)
            {
                V2 p = Bez(pa, mid, pb, k / (float)segs);
                Line(prev, p, 1.8f, MarrowThread.WithAlpha(0.85f * fade));
                prev = p;
            }
            if (fxOn)
                for (int k = 0; k < 3; k++)  // marrow beads running along it
                    Circle(Bez(pa, mid, pb, Mathf.Repeat(t * 0.8f + k / 3f, 1f)), 1.6f, MarrowBead.WithAlpha(fade), 6);
        }

        /// The Ossuary Wall: a bone tumbling in (with the wall's line marked where it lands), then a palisade of femurs
        /// punching up from the middle out, end to end along the wall; they topple and fade in its last 0.4 s.
        void DrawOssuaryWall(Zone z, float t)
        {
            var bone = Defs.BulletColor(Defs.BoneShot);
            V2 a, b;
            Game.OssuaryEnds(z, out a, out b);
            float age = z.Duration - z.T;
            if (age < Game.BoneWallFlight)
            {
                float k = age / Game.BoneWallFlight, lift;
                V2 at = LobbedAt(z, k, out lift);
                Line(a, b, 1.5f, WallGlow.WithAlpha(0.2f + 0.3f * k));
                Circle(at, 4f, new Rgba(0f, 0f, 0f, 0.25f), 8);
                V2 u = V2.FromAngle(t * 14f) * 6f, c = at + new V2(0f, -lift);
                Line(c - u, c + u, 3f, bone);
                Circle(c - u, 2.2f, bone, 6);
                Circle(c + u, 2.2f, bone, 6);
                return;
            }
            float up = age - Game.BoneWallFlight, fall = Mathf.Clamp01(1f - z.T / 0.4f);
            V2 along = (b - a).Normalized(), out1 = V2.FromAngle(z.Aim);
            Line(a, b, 3f, WallGlow.WithAlpha(0.3f * (1f - fall)));
            int n = Mathf.Max(5, Mathf.RoundToInt((b - a).Length / 12f) + 1);
            float half = (n - 1) / 2f;
            for (int i = 0; i < n; i++)
            {
                float delay = Mathf.Abs(i - half) * 0.03f;
                float s = 1f - Sq(1f - Mathf.Clamp01((up - delay) / 0.16f));
                if (s <= 0f)
                    continue;
                if (!fxOn)
                    s = 1f;
                float side = i % 2 == 0 ? 1f : -1f, tilt = 0.12f * Mathf.Sin(i * 2.3f) + fall * 1.2f * side;
                V2 c = a + (b - a) * (i / (float)(n - 1)) + out1 * (fall * 10f * side);
                V2 dir = V2.FromAngle(along.Angle + tilt) * (6.5f * s), nrm = dir.Orthogonal().Normalized() * (2.2f * s);
                Rgba col = bone.WithAlpha(1f - fall), shade = FemurShade.WithAlpha(1f - fall);
                Line(c - dir + new V2(2f, 3f), c + dir + new V2(2f, 3f), 5f * s, new Rgba(0f, 0f, 0f, 0.3f * (1f - fall)));  // shadow
                Line(c - dir, c + dir, 5f * s, shade);
                Line(c - dir - nrm * 0.3f, c + dir - nrm * 0.3f, 3f * s, col);
                Circle(c - dir + nrm, 2.6f * s, col, 6);  // the knobbed ends
                Circle(c - dir - nrm, 2.6f * s, col, 6);
                Circle(c + dir + nrm, 2.6f * s, col, 6);
                Circle(c + dir - nrm, 2.6f * s, col, 6);
            }
        }
    }
}
