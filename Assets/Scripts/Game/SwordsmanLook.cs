using UnityEngine;

namespace Arena
{
    /// The Swordsman and the Shield Swordsman (Core/Swordsman.cs), drawn as in Docs/Mockups/swordsman.html: a crimson body with a
    /// cloak, steel pauldrons and a crested helmet, a long tapered blade, and (the Shield Swordsman) a blue-steel shield held
    /// in front. On the floor, its cone: a curved band from the hand to the tip that is exactly what the blade covers. It
    /// fills red as the wind-up runs, then the bright wedge shows where the cut has passed. A blocked shot flashes the plate.
    /// Timed from the age of the phase this peer sees (Enemy.SeenT), since PhaseT only arrives with each snapshot.
    public partial class ArenaApp
    {
        static readonly Rgba SwordAmber = new Rgba(1f, 0.69f, 0.18f), SwordRed = new Rgba(1f, 0.28f, 0.2f), SwordCream = new Rgba(1f, 0.95f, 0.85f);
        static readonly Rgba SteelLight = new Rgba(0.82f, 0.86f, 0.89f), SteelDark = new Rgba(0.35f, 0.4f, 0.44f), ShieldDark = new Rgba(0.15f, 0.26f, 0.37f), ShieldLight = new Rgba(0.44f, 0.61f, 0.77f);

        static float SmoothStep(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        /// The blade's angle relative to where it faces, in the phase this peer sees.
        static float SwordRel(Enemy e)
        {
            const float rest = 0.8f, H = Game.SwordHalf;
            switch (e.Phase)
            {
                case 1: return Mathf.Lerp(rest, -H, SmoothStep(e.SeenT / Game.SwordWind));
                case 2: return Mathf.Lerp(-H, H, Mathf.Clamp01(e.SeenT / Game.SwordCut));
                case 3: return Mathf.Lerp(H, rest, SmoothStep(e.SeenT / Game.SwordRecoverOf(e.Type)));
            }
            return rest;
        }

        static V2 SwordPt(V2 pos, V2 fwd, V2 side, float s, float x, float y) { return pos + fwd * (x * s) + side * (y * s); }

        /// A curved band between two radii and two angles.
        void SwordBand(V2 pos, float r0, float r1, float a0, float sweep, Rgba c)
        {
            if (r1 - r0 < 0.5f || sweep <= 0f)
                return;
            Arc(pos, (r0 + r1) * 0.5f, r1 - r0, c, Mathf.Max(6, (int)(sweep * 14f)), a0, sweep);
        }

        /// On the floor under it: the cone.
        void DrawSwordsmanUnder(Enemy e, V2 pos, float r, float t)
        {
            float R = Game.SwordReach, I = Game.SwordInner, H = Game.SwordHalf, f = e.Facing;
            const int rings = 6;
            float step = (R - I) / rings;
            for (int k = 0; k < rings; k++)  // the reach: faint, a little stronger toward the tip
                SwordBand(pos, I + step * k, I + step * (k + 1), f - H, H * 2f, SwordAmber.WithAlpha(0.025f + 0.02f * k));
            Rgba edge = SwordAmber.WithAlpha(0.45f);
            float w = 1.5f;
            if (e.Phase == 1)
            {
                float fill = SmoothStep(e.SeenT / Game.SwordWind), top = I + (R - I) * fill;
                for (int k = 0; k < rings; k++)
                    SwordBand(pos, I + step * k, Mathf.Min(I + step * (k + 1), top), f - H, H * 2f, SwordRed.WithAlpha(0.14f + 0.07f * k));
                edge = SwordRed.WithAlpha(0.95f);
                w = 2.5f;
            }
            else if (e.Phase == 2)
            {
                float u = Mathf.Clamp01(e.SeenT / Game.SwordCut), swept = (u * 2f) * H;
                SwordBand(pos, I, R, f - H, H * 2f, SwordRed.WithAlpha(0.14f));
                for (int k = 0; k < rings; k++)  // where the blade has already passed
                    SwordBand(pos, I + step * k, I + step * (k + 1), f - H, swept, SwordCream.WithAlpha(0.09f + 0.05f * k));
                Arc(pos, R, 4f, SwordCream, 28, f - H, swept);
                edge = SwordCream;
                w = 2.5f;
            }
            else if (e.Phase == 3)
            {
                float fade = 1f - Mathf.Clamp01(e.SeenT / Game.SwordRecoverOf(e.Type));
                SwordBand(pos, I, R, f - H, H * 2f, SwordCream.WithAlpha(0.1f * fade));
                edge = SwordAmber.WithAlpha(0.35f);
            }
            Arc(pos, R, w, edge, 28, f - H, H * 2f);
            Line(pos + V2.FromAngle(f - H) * I, pos + V2.FromAngle(f - H) * R, w, edge);
            Line(pos + V2.FromAngle(f + H) * I, pos + V2.FromAngle(f + H) * R, w, edge);
        }

        /// The body, in place of the usual polygon. col is already tinted by status and the hit flash.
        void DrawSwordsmanBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float f = e.Facing, s = r / 15f, R = Game.SwordReach, I = Game.SwordInner;
            V2 fwd = V2.FromAngle(f), side = fwd.Orthogonal();

            // the cloak trailing behind
            Rgba cloak = col.Darkened(0.5f);
            SetColor(cloak);
            Tri(SwordPt(pos, fwd, side, s, -6f, -13f), SwordPt(pos, fwd, side, s, -31f, -4f), SwordPt(pos, fwd, side, s, -31f, 4f));
            Tri(SwordPt(pos, fwd, side, s, -6f, -13f), SwordPt(pos, fwd, side, s, -31f, 4f), SwordPt(pos, fwd, side, s, -6f, 13f));
            Circle(pos, r, col.Darkened(0.45f), 24);
            Circle(pos, r - 1.8f, col, 24);

            // the shield, up in front, or lowered at its side
            if (e.Type == EnemyType.ShieldSwordsman)
            {
                float g = Game.SwordGuardAt(e.Type, e.Phase, e.SeenT);
                float sc = f + Mathf.Lerp(-1.5f, 0f, g), hw = Mathf.Lerp(0.6f, Game.GuardHalf, g), rr = Mathf.Lerp(25f, 35f, g);
                Line(SwordPt(pos, fwd, side, s, 0f, -11f), pos + V2.FromAngle(sc) * (rr - 3f), 7f * s, col.Darkened(0.4f));
                if (g > 0.4f)
                    Arc(pos, rr, 20f, ShieldLight.WithAlpha(0.1f * g), 24, sc - hw, hw * 2f);
                Arc(pos, rr, 14f, ShieldDark, 24, sc - hw, hw * 2f);
                Arc(pos, rr, 9f, ShieldLight, 24, sc - hw, hw * 2f);
                Arc(pos, rr + 3f, 1.5f, Rgba.White.WithAlpha(0.6f), 24, sc - hw, hw * 2f);
                for (int k = -1; k <= 1; k++)
                    Circle(pos + V2.FromAngle(sc + k * hw * 0.62f) * rr, 1.8f, new Rgba(0.86f, 0.91f, 0.96f), 6);
                float fl = Mathf.Clamp01(e.T2 / Game.SwordBlockFlash);  // a blocked shot: the whole plate flashes
                if (fl > 0f)
                {
                    fl = Mathf.Pow(fl, 1.5f);
                    Arc(pos, rr, 26f, new Rgba(0.63f, 0.84f, 1f, 0.28f * fl), 24, sc - hw, hw * 2f);
                    Arc(pos, rr, 14f, new Rgba(0.94f, 0.98f, 1f, 0.9f * fl), 24, sc - hw, hw * 2f);
                }
            }

            // the sword arm and the blade: the tip sits exactly on the cone's rim
            float a = f + SwordRel(e);
            V2 dir = V2.FromAngle(a), perp = dir.Orthogonal(), hand = pos + dir * I;
            Line(SwordPt(pos, fwd, side, s, 0f, 11f), hand, 7f * s, col.Darkened(0.4f));
            float g0 = I + 5f, bw = 4.6f;
            V2 bl = pos + dir * g0 + perp * bw, br = pos + dir * g0 - perp * bw;
            V2 sl = pos + dir * (R - 16f) + perp * bw * 0.9f, sr = pos + dir * (R - 16f) - perp * bw * 0.9f, tip = pos + dir * R;
            if (e.Phase == 2)
                Line(pos + dir * g0, tip, 12f, SwordCream.WithAlpha(0.25f));
            SetColor(SteelLight);
            Tri(bl, sl, sr);
            Tri(bl, sr, br);
            Tri(sl, tip, sr);
            Line(pos + dir * (g0 + 4f), pos + dir * (R - 22f), 1.5f, SteelDark.WithAlpha(0.7f));
            Line(pos + dir * (I - 6f), pos + dir * g0, 4.5f, new Rgba(0.23f, 0.14f, 0.09f));
            Line(pos + dir * g0 + perp * 9f, pos + dir * g0 - perp * 9f, 4f, new Rgba(0.82f, 0.66f, 0.3f));
            Circle(hand, 4f, new Rgba(0.6f, 0.65f, 0.68f), 8);

            // pauldrons and a crested helmet
            Circle(SwordPt(pos, fwd, side, s, 0f, -13f), 7f * s, SteelDark, 12);
            Circle(SwordPt(pos, fwd, side, s, 0f, -13f), 5.6f * s, SteelLight, 12);
            Circle(SwordPt(pos, fwd, side, s, 0f, 13f), 7f * s, SteelDark, 12);
            Circle(SwordPt(pos, fwd, side, s, 0f, 13f), 5.6f * s, SteelLight, 12);
            Circle(pos, 9.5f * s, SteelDark, 16);
            Circle(pos, 8.2f * s, new Rgba(0.78f, 0.83f, 0.86f), 16);
            Line(SwordPt(pos, fwd, side, s, -9f, 0f), SwordPt(pos, fwd, side, s, 6f, 0f), 3.5f * s, new Rgba(0.75f, 0.22f, 0.2f));
            Line(SwordPt(pos, fwd, side, s, 7f, -3.5f), SwordPt(pos, fwd, side, s, 7f, 3.5f), 2.5f * s, new Rgba(0.1f, 0.07f, 0.06f));
        }
    }
}
