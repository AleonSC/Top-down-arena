using UnityEngine;

namespace Arena
{
    /// Blood Garden (the Healer Hemomancer's fourth ultimate, Core/BloodGarden.cs), from the mockup (claude.ai artifact
    /// "Blood Garden Ult"). Everything comes from the replicated sapling zone (`ZoneKind.BloodSapling`: Pos, Radius, its age,
    /// Aim the age it lands at, GuardFrac the bulb) and the enemies, so it costs nothing extra on the network:
    ///   a seed (FxKind.BloodSeed): a blood drop arcing from the Hemomancer to where it lands, its shadow on the floor under it;
    ///   the aura: a faint red wash, a dashed ring turning round it, thorns on the rim;
    ///   the siphon: a curving tether from each enemy in the aura to the bulb, drops riding it;
    ///   the sapling: a stem with two leaves and a bulb that fills with blood, pulsing gold near full; a thin ring round its foot
    ///     counts down its life; it sprouts in over BgSprout s and wilts over its last 1.5 s.
    /// Visual effects off: a plain ring, a dot and the bulb's fill as an arc.
    public partial class ArenaApp
    {
        static readonly Rgba BgStem = new Rgba(0.35f, 0.1f, 0.14f), BgLeaf = new Rgba(0.62f, 0.13f, 0.21f), BgDeep = new Rgba(0.16f, 0.03f, 0.07f);
        static readonly Rgba BgWet = new Rgba(1f, 0.6f, 0.66f), BgFull = new Rgba(1f, 0.27f, 0.38f);

        /// Where the bulb sits over a sapling at `pos`, its size `k` (0 unsprouted .. 1).
        static V2 BgBulbAt(V2 pos, float k) => pos + new V2(0f, -21f * k);

        /// A sapling and its aura, on the floor (DrawZone).
        void DrawBloodSapling(Zone z, float t)
        {
            float age = Game.BgAge(z) - z.Aim;
            if (age < 0f)
                return;  // its seed is still flying (DrawBloodSeedFx)
            float grow = Mathf.Clamp01(age / Game.BgSprout), wilt = Mathf.Clamp01(z.T / 1.5f), fill = Mathf.Clamp01(z.GuardFrac);
            float k = grow * (0.6f + 0.4f * wilt) * 1.4f, fade = wilt;  // (1.4: the sapling's size)
            if (!fxOn)
            {
                Arc(z.Pos, z.Radius * grow, 1.5f, Game.BgRed.WithAlpha(0.6f * fade), 32);
                Circle(z.Pos, 5f, BgLeaf.WithAlpha(fade), 10);
                Arc(z.Pos, 9f, 3f, Game.BgRed.WithAlpha(fade), 16, -MathX.Pi / 2f, MathX.Tau * fill);
                return;
            }
            float R = z.Radius * grow;
            GlowDisc(z.Pos, R, Game.BgRed, 0.12f * fade);
            DashRing(z.Pos, R, 7f, 9f, t * 14f + z.Id * 5f, 1.6f, Game.BgRed.WithAlpha(0.45f * fade));
            for (int i = 0; i < 10; i++)  // thorns on the rim
            {
                float a = i * MathX.Tau / 10f + z.Id * 0.7f;
                Line(z.Pos + V2.FromAngle(a) * (R * 0.92f), z.Pos + V2.FromAngle(a + 0.08f) * (R * 1.03f), 2f, BgStem.WithAlpha(0.9f * fade));
            }
            V2 bulb = BgBulbAt(z.Pos, k);
            if (grow >= 1f && z.T > 0.05f)  // the siphon: a tether from each enemy in the aura into the bulb
            {
                int shown = 0;
                foreach (var e in game.Enemies.Values)
                {
                    if (e.Hp <= 0f || e.Disp.FartherThan(z.Pos, z.Radius + e.Radius * 0.5f) || shown++ >= 6)
                        continue;
                    V2 a0 = e.Disp, along = bulb - a0;
                    float len = along.Length;
                    if (len < 8f)
                        continue;
                    V2 side = along.Orthogonal() / len;
                    float bow = (14f + Mathf.Sin(t * 5f + e.Id) * 8f) * ((e.Id & 1) == 0 ? 1f : -1f);
                    V2 prev = a0;
                    for (int s = 1; s <= 12; s++)
                    {
                        float u = s / 12f;
                        V2 pt = a0 + along * u + side * (bow * Mathf.Sin(u * MathX.Pi));
                        Line(prev, pt, 2.6f - 1.2f * u, Game.BgRed.WithAlpha(0.55f * fade));
                        prev = pt;
                    }
                    for (int d = 0; d < 3; d++)
                    {
                        float u = Mathf.Repeat(t * 1.5f + d / 3f + e.Id * 0.17f, 1f);
                        V2 pt = a0 + along * u + side * (bow * Mathf.Sin(u * MathX.Pi));
                        DrawDrop(pt, 3f, along / len, BgFull.WithAlpha(fade));
                    }
                    Arc(e.Disp, e.Radius + 4f, 2f, Game.BgRed.WithAlpha(0.7f * fade), 10, t * 2f + e.Id, 0.9f);  // the slow, curling round it
                    Arc(e.Disp, e.Radius + 4f, 2f, Game.BgRed.WithAlpha(0.7f * fade), 10, t * 2f + e.Id + 2.1f, 0.9f);
                }
            }
            // The sapling: shadow, stem, two leaves, the bulb.
            Circle(z.Pos + new V2(0f, 2f), 9f * k, new Rgba(0f, 0f, 0f, 0.35f * fade), 14);
            V2 foot = z.Pos + new V2(0f, 2f * k), mid = z.Pos + new V2(-3f * k, -7f * k), top = z.Pos + new V2(0f, -14f * k);
            Line(foot, mid, 3.5f * k, BgStem.WithAlpha(fade));
            Line(mid, top, 3f * k, BgStem.WithAlpha(fade));
            BgLeafAt(mid + new V2(-1f, 0f) * k, new V2(-1f, -0.55f), 7f * k, fade);
            BgLeafAt(mid + new V2(2f, -2f) * k, new V2(1f, -0.6f), 7f * k, fade);
            float br = (8f + 2f * fill) * k + (fill > 0.85f ? Mathf.Sin(t * 20f) : 0f);
            Circle(bulb, br, BgDeep.WithAlpha(fade), 18);
            if (fill > 0f)  // the blood in it, risen to its level
            {
                float lvl = br * (1f - 2f * fill);
                const int rows = 8;
                for (int r = 0; r < rows; r++)
                {
                    float y0 = Mathf.Lerp(br, lvl, r / (float)rows), y1 = Mathf.Lerp(br, lvl, (r + 1) / (float)rows);
                    float w0 = Mathf.Sqrt(Mathf.Max(0f, br * br - y0 * y0)), w1 = Mathf.Sqrt(Mathf.Max(0f, br * br - y1 * y1));
                    Rgba c = (fill > 0.85f ? BgFull : Game.BgRed).WithAlpha(fade);
                    VTri(bulb + new V2(-w0, y0), c, bulb + new V2(w0, y0), c, bulb + new V2(w1, y1), c);
                    VTri(bulb + new V2(-w0, y0), c, bulb + new V2(w1, y1), c, bulb + new V2(-w1, y1), c);
                }
            }
            Arc(bulb, br, 1.6f, (fill > 0.85f ? Rgba.Gold : BgWet).WithAlpha(fade), 18);
            Circle(bulb + new V2(-br * 0.35f, -br * 0.4f), 1.6f * k, BgWet.WithAlpha(0.8f * fade), 6);
            if (fill > 0.85f)
                GlowDisc(bulb, br * 2.2f, BgFull, 0.3f * fade);
            float life = Mathf.Clamp01(z.T / Game.BgLife);  // its life, round its foot
            Arc(z.Pos + new V2(0f, 2f), 17f, 1.6f, new Rgba(0.76f, 0.8f, 0.91f, 0.3f * fade), 24, -MathX.Pi / 2f, MathX.Tau * life);
        }

        /// A leaf from `at` toward `dir`, `len` long.
        void BgLeafAt(V2 at, V2 dir, float len, float fade)
        {
            V2 d = dir.Normalized(), side = d.Orthogonal() * (len * 0.38f), tip = at + d * len, mid = at + d * (len * 0.5f);
            VTri(at, BgLeaf.WithAlpha(fade), mid + side, BgLeaf.WithAlpha(fade), tip, BgLeaf.WithAlpha(fade));
            VTri(at, BgLeaf.WithAlpha(fade), tip, BgLeaf.WithAlpha(fade), mid - side, BgStem.WithAlpha(fade));
        }

        /// A seed in flight (FxKind.BloodSeed): Pos where it was flung from, Pos2 where it lands, `f` 0..1 along.
        void DrawBloodSeedFx(Effect fx, float f)
        {
            V2 along = fx.Pos2 - fx.Pos;
            V2 ground = fx.Pos + along * f;
            float hop = Mathf.Sin(f * MathX.Pi), h = hop * Mathf.Min(70f, 30f + along.Length * 0.2f);
            V2 at = ground + new V2(0f, -h), dir = along.Length > 1f ? along / along.Length : new V2(1f, 0f);
            Circle(ground, 4f * (1f - 0.5f * hop), new Rgba(0f, 0f, 0f, 0.4f), 10);
            if (!fxOn)
            {
                Circle(at, 4f, Game.BgRed, 8);
                return;
            }
            for (int k = 1; k <= 3; k++)  // a short trail behind it
            {
                float g = Mathf.Max(0f, f - k * 0.04f), hp = Mathf.Sin(g * MathX.Pi);
                V2 back = fx.Pos + along * g + new V2(0f, -hp * Mathf.Min(70f, 30f + along.Length * 0.2f));
                Circle(back, 3.5f - k * 0.8f, Game.BgRed.WithAlpha(0.45f - k * 0.12f), 8);
            }
            DrawDrop(at, 5f, dir, Game.BgRed);
            Circle(at + new V2(-1.2f, -1.4f), 1.3f, BgWet.WithAlpha(0.8f), 6);
        }
    }
}
