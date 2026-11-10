using UnityEngine;

namespace Arena
{
    /// The Taser Guard (WardenFaction.cs) and its Fork Bolt (Defs.TaserBolt): a white lightning glyph along its flight that
    /// re-rolls its zigzag 16 times a second, with a fork; on a hit, a flash and six bolts web out from you
    /// (FxKind.TaserHit). Every number is the one in `Docs/Mockups/taser-guard-stun.html`, option B (Fork Bolt), in world
    /// pixels, so the game matches it. The guard itself: a visor, a stubby taser out front with two prongs, and while it
    /// charges (Enemy.Winding, Game.TaserWindUp) a crackle between the prongs that grows into the shot.
    public partial class ArenaApp
    {
        static readonly Rgba ForkGlow = new Rgba(0.56f, 0.83f, 1f);      // #8fd4ff: the glow and the outer stroke
        static readonly Rgba ForkMid = new Rgba(0.875f, 0.957f, 1f);     // #dff4ff: the middle stroke
        static readonly Rgba ForkHalo = new Rgba(0.62f, 0.86f, 1f);      // #9fdcff: the soft halo
        static readonly Rgba ForkWhite = new Rgba(1f, 1f, 1f);
        static readonly Rgba TaserCharge = new Rgba(0.75f, 0.91f, 1f);   // #bfe9ff: the prongs' crackle

        /// The mockup's hash: the same value for the same seed, so a flicker frame looks the same as there.
        static float TaserRnd(float n)
        {
            double x = System.Math.Sin(n * 127.1 + 311.7) * 43758.5453;
            return (float)(x - System.Math.Floor(x));
        }

        /// A zigzag from a to b: `n` segments, the points between them pushed sideways by up to `amp` (the mockup's zig).
        void TaserZig(V2 a, V2 b, float amp, float seed, int n, float width, Rgba col)
        {
            V2 d = b - a;
            float len = Mathf.Max(0.0001f, d.Length);
            V2 nrm = new V2(-d.Y / len, d.X / len), prev = a;
            for (int i = 1; i <= n; i++)
            {
                V2 next = i == n ? b : a + d * ((float)i / n) + nrm * ((TaserRnd(seed + i * 3.1f) - 0.5f) * 2f * amp);
                Line(prev, next, width, col);
                prev = next;
            }
        }

        /// A bolt: a wide faint glow stroke under a white core, along the same zigzag (the mockup's bolt()).
        void TaserBoltLine(V2 a, V2 b, float amp, float seed, int n, Rgba glow, float w, float alpha)
        {
            TaserZig(a, b, amp, seed, n, w * 3f, glow.WithAlpha(alpha * 0.3f));
            TaserZig(a, b, amp, seed, n, w, ForkWhite.WithAlpha(alpha));
        }

        /// A soft glow standing in for the mockup's radial gradient: 14 discs, each faint enough that the edge doesn't show
        /// and stacked so the strength falls off in a straight line from `alpha` in the middle to nothing at `r`.
        void TaserHalo(V2 at, float r, Rgba col, float alpha)
        {
            if (alpha <= 0.001f)
                return;
            const int steps = 14;
            float each = 1f - Mathf.Pow(1f - Mathf.Min(0.95f, alpha), 1f / steps);
            for (int i = 0; i < steps; i++)
                Circle(at, r * (1f - (float)i / steps), col.WithAlpha(each), 18);
        }

        /// A stroke through points: segments with a round disc at each corner, so thick strokes have no notches.
        void TaserStroke(V2[] pts, float width, Rgba col, bool joints)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
                Line(pts[i], pts[i + 1], width, col);
            if (joints)
                foreach (var p in pts)
                    Circle(p, width / 2f, col, 8);
        }

        /// The Fork Bolt in flight (WorldShots.cs): 36 px along its path in five zigzag segments, a fork off the middle.
        void DrawTaserBolt(Bullet b, float t)
        {
            V2 u = b.Vel.Length > 0.01f ? b.Vel.Normalized() : V2.FromAngle(0f), n = new V2(-u.Y, u.X);
            float sd = Mathf.Floor(t * 16f) * 4.3f + b.Id * 9f;
            const int N = 5;
            const float len = 36f;
            var pts = new V2[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float k = (float)i / N - 0.5f;
                float o = i == 0 || i == N ? 0f : (i % 2 == 1 ? 1f : -1f) * (5f + TaserRnd(sd + i) * 6f);
                pts[i] = b.Pos + u * (len * k) + n * o;
            }
            TaserHalo(b.Pos, 24f, ForkHalo, 0.55f);
            TaserStroke(pts, 10f, ForkGlow.WithAlpha(0.4f), false);
            TaserStroke(pts, 4.2f, ForkMid, true);
            TaserStroke(pts, 1.8f, ForkWhite, true);
            V2 f = pts[2];
            float fa = TaserRnd(sd + 7f) > 0.5f ? 1f : -1f;
            TaserBoltLine(f, f + n * (fa * 13f) + u * 6f, 2f, sd, 3, ForkGlow, 1.3f, 0.95f);
        }

        /// FxKind.TaserHit: a flash and six bolts webbing out from the player, over 0.7 s (Pos = the player).
        void DrawTaserHitFx(Effect fx)
        {
            float k = Mathf.Clamp01(fx.T / fx.Dur);
            V2 c = fx.Pos;
            Circle(c, 10f + k * 16f, ForkMid.WithAlpha((1f - k) * 0.7f), 20);
            float q = Mathf.Floor(k * 18f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * MathX.Tau + 0.5f;
                V2 d = V2.FromAngle(a);
                TaserBoltLine(c + d * 8f, c + d * (16f + k * 40f), 5f, q * 5f + i, 5, ForkGlow, 2f, 1f - k);
            }
        }

        /// A Taser Guard's details over its body: visor, the taser out front, and its charge.
        void DrawTaserGuardDetails(Enemy e, V2 pos, float r, float t)
        {
            V2 f = V2.FromAngle(e.Facing), side = new V2(-f.Y, f.X);
            Line(pos + f * 2f - side * 6f, pos + f * 2f + side * 6f, 3.5f, new Rgba(0.04f, 0.08f, 0.16f, 0.95f));  // visor
            Line(pos + f * 11f, pos + f * 23f, 5f, new Rgba(0.81f, 0.85f, 0.93f));                                // the taser
            V2 pa = pos + f * 27f + side * 3f, pb = pos + f * 27f - side * 3f;
            Line(pos + f * 23f + side * 3f, pa, 1.5f, ForkWhite);                                                  // its prongs
            Line(pos + f * 23f - side * 3f, pb, 1.5f, ForkWhite);
            if (!e.Winding)
                return;
            float k = 1f;
            if (aimSince.TryGetValue(e.Id, out float since))
                k = MathX.Clamp01((t - since) / Game.WindUpOf(e.Type));
            V2 muzzle = pos + f * 26f, gap = side * 4f;
            TaserHalo(muzzle, 8f + 16f * k, TaserCharge, 0.55f * k);
            TaserBoltLine(muzzle + gap, muzzle - gap, 1f + 4f * k, Mathf.Floor(t * 30f), 3, TaserCharge, 1.3f, 0.5f + 0.5f * k);
        }
    }
}
