using UnityEngine;

namespace Arena
{
    /// Flame Wall as a "Ring of fire" (Docs/Mockups/flame-wall-ring.html, style C "Spiral fuse"), with Visual effects on: a
    /// spark lands at the centre and spirals out to the edge (0.22 s), then a fuse head runs once round the ring (0.53 s),
    /// lighting a band of scorched ground and a ring of flame tongues that burst up behind it, overshoot and settle. When the
    /// head meets its tail it flashes, pops embers and throws a ring outward. Tongues lean outward and up-screen for height,
    /// embers and smoke rise, the rock flares where an enemy shot is about to hit, and the last 0.6 s the flames sink and
    /// smoke. Once the guard is spent the flames drop to 45% (as before). Drawing only: everything comes from the zone and
    /// the bullets this screen has; sizes are the mockup's, scaled from its 78 px ring to the zone's radius.
    public partial class ArenaApp
    {
        static readonly Rgba FireRed = new Rgba(200 / 255f, 40 / 255f, 16 / 255f), FireOrange = new Rgba(1f, 110 / 255f, 24 / 255f),
            FireYellow = new Rgba(1f, 196 / 255f, 64 / 255f), FireWhite = new Rgba(1f, 246 / 255f, 200 / 255f), FireSmoke = new Rgba(60 / 255f, 48 / 255f, 44 / 255f);
        const float RingPre = 0.22f, RingSweep = 0.53f;  // spiral out, then once round

        static float FlameBurst(float f)  // ease-out with overshoot (the tongues' burst)
        {
            f = Mathf.Clamp01(f) - 1f;
            return 1f + 2.9f * f * f * f + 1.9f * f * f;
        }

        /// How long ago the fuse passed angle a (0 = not reached yet), going round from angle 0 (the right).
        static float RingLit(float a, float age)
        {
            float k = (age - RingPre) / RingSweep;
            if (k < 0f)
                return 0f;
            float d = MathX.Fmod(MathX.Fmod(a, MathX.Tau) + MathX.Tau, MathX.Tau) / MathX.Tau;
            float reach = k >= 1f ? 1f : EaseOut3(k);
            if (d > reach + 1e-4f)
                return 0f;
            return Mathf.Max(1e-4f, (reach - d) * RingSweep + (k > 1f ? (k - 1f) * RingSweep : 0f));
        }

        /// Where the spark is: spiralling out from the centre, then running round the edge; false once it has met its tail.
        static bool RingHead(V2 c, float R, float age, out V2 at, out float ang)
        {
            if (age < RingPre)
            {
                float q = age / RingPre;
                ang = -MathX.Tau * 0.75f * (1f - q);
                at = c + V2.FromAngle(ang) * (R * EaseOut3(q));
                return true;
            }
            float k = (age - RingPre) / RingSweep;
            ang = MathX.Tau * EaseOut3(k);
            at = c + V2.FromAngle(ang) * R;
            return k < 1f;
        }

        /// One flame tongue: four layered kites (red, orange, yellow, white core) leaning outward and up-screen.
        void FlameTongue(V2 b, float a, float h, float w, float t, int i)
        {
            V2 o = V2.FromAngle(a), dir = new V2(o.X * 0.45f, o.Y * 0.45f - 1f);
            dir = dir * (1f / Mathf.Max(0.001f, dir.Length));
            V2 n = new V2(-dir.Y, dir.X);
            float sway = Mathf.Sin(t * 9f + i * 1.7f) * 0.35f;
            for (int j = 0; j < 4; j++)
            {
                float hk = j == 0 ? 1f : j == 1 ? 0.78f : j == 2 ? 0.52f : 0.26f, wk = j == 0 ? 1f : j == 1 ? 0.74f : j == 2 ? 0.48f : 0.24f;
                Rgba col = j == 0 ? FireRed.WithAlpha(0.85f) : j == 1 ? FireOrange.WithAlpha(0.9f) : j == 2 ? FireYellow.WithAlpha(0.95f) : FireWhite.WithAlpha(0.9f);
                float hh = h * hk, ww = w * wk;
                V2 tip = b + dir * hh + n * (sway * ww * 1.6f), mid = b + dir * (hh * 0.38f);
                V2 bl = b + n * (ww * 0.55f), br = b - n * (ww * 0.55f), ml = mid + n * ww, mr = mid - n * ww;
                SetColor(col);
                Tri(tip, ml, bl);
                Tri(tip, bl, br);
                Tri(tip, br, mr);
            }
        }

        void DrawFlameRing(Zone z, float t)
        {
            float R = z.Radius, s = R / 78f, age = z.Duration - z.T;
            float end = Mathf.Clamp01(z.T / 0.6f), lowK = z.GuardFrac > 0f ? 1f : 0.45f;
            V2 c = z.Pos;
            bool met = age >= RingPre + RingSweep;

            // warm light on the floor inside
            if (met)
            {
                Circle(c, R - 8f * s, new Rgba(1f, 0.43f, 0.12f, 0.07f * end), 40);
                Circle(c, R - 30f * s, new Rgba(1f, 0.43f, 0.12f, 0.05f * end), 36);
            }

            // smoke, more of it as the fire dies
            int puffs = end < 1f ? 12 : 5;
            for (int k = 0; k < puffs; k++)
            {
                float p = MathX.Fmod(t * 0.8f + Hash01(k + 40f), 1f), a = Hash01(k + 44f) * MathX.Tau;
                if (RingLit(a, age) <= 0f)
                    continue;
                V2 at = c + V2.FromAngle(a) * R + new V2(Mathf.Sin(t + k) * 4f, (-10f - p * 30f) * s);
                Circle(at, (7f + Hash01(k + 48f) * 5f) * s * (0.6f + p * 1.3f), FireSmoke.WithAlpha(0.35f * (1f - p) * (met ? 1f : 0.5f)), 10);
            }

            // scorched ground, the red band and the hot line, lit as the fuse passes
            const int N = 44;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * MathX.Tau, lk = RingLit(a + MathX.Tau / N / 2f, age);
                if (lk <= 0f)
                    continue;
                float g = EaseOut3(lk / 0.25f), sw = MathX.Tau / N + 0.01f;
                Arc(c, R, 20f * s, new Rgba(24 / 255f, 10 / 255f, 6 / 255f, 0.75f * g), 3, a, sw);
                Arc(c, R, 9f * s, FireRed.WithAlpha((0.55f + 0.15f * Mathf.Sin(t * 6f + i)) * g * (0.4f + 0.6f * end)), 3, a, sw);
                Arc(c, R, 2.5f * s, FireYellow.WithAlpha((0.7f + 0.3f * Mathf.Sin(t * 11f + i * 2f)) * g * end), 3, a, sw);
            }

            // where an enemy shot is about to hit the fire
            if (z.GuardFrac > 0f)
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && Mathf.Abs(b.Pos.DistanceTo(c) - R) < 20f * s)
                    {
                        V2 off = b.Pos - c;
                        float ba = Mathf.Atan2(off.Y, off.X);
                        if (RingLit(ba, age) <= 0f)
                            continue;
                        Arc(c, R, 6f * s, FireWhite.WithAlpha(0.8f), 8, ba - 0.3f, 0.6f);
                        Circle(c + V2.FromAngle(ba) * R, 12f * s, FireYellow.WithAlpha(0.4f), 12);
                    }

            // the flame tongues
            const int M = 56;
            for (int i = 0; i < M; i++)
            {
                float a = i / (float)M * MathX.Tau + Hash01(i) * 0.05f, lk = RingLit(a, age);
                if (lk <= 0f)
                    continue;
                float grow = FlameBurst(lk / 0.32f);
                float h = (17f + 7f * Mathf.Sin(t * 11f + i * 2.3f) + 4f * Mathf.Sin(t * 17.3f + i)) * grow * (0.25f + 0.75f * end) * (0.85f + Hash01(i + 3f) * 0.35f) * lowK * s;
                FlameTongue(c + V2.FromAngle(a) * R, a, h, 5.2f * (0.8f + 0.4f * Hash01(i + 7f)) * Mathf.Min(1f, grow + 0.2f) * s, t, i);
            }

            // embers rising off the ring
            for (int k = 0; k < 18; k++)
            {
                float p = MathX.Fmod(t * 1.1f + Hash01(k + 20f), 1f), a = Hash01(k + 24f) * MathX.Tau;
                if (!met || RingLit(a, age) <= 0f)
                    continue;
                float size = (1.4f + Hash01(k + 28f) * 1.2f) * (1f - p * 0.6f) * s;
                V2 at = c + V2.FromAngle(a) * R + new V2(Mathf.Sin(t * 3f + k) * 6f, (-8f - p * 55f) * s);
                Circle(at, size * 2f, FireOrange.WithAlpha(0.3f * (1f - p) * end), 8);
                Circle(at, size, FireWhite.WithAlpha((1f - p) * end), 6);
            }

            // the cast spark at the centre
            if (age < 0.18f)
            {
                float k = age / 0.18f;
                Circle(c, 26f * s * EaseOut3(k), FireWhite.WithAlpha(0.6f * (1f - k)), 24);
                Circle(c, 10f * s, FireYellow.WithAlpha(0.9f * (1f - k)), 12);
            }

            // the fuse head, trailing sparks
            if (RingHead(c, R, age, out V2 head, out float ha))
            {
                for (int j = 0; j < 8; j++)
                {
                    float p = MathX.Fmod(t * 4f + Hash01(j + 60f), 1f), sa = ha + MathX.Pi / 2f + (Hash01(j + 64f) - 0.5f) * 2.4f;
                    V2 at = head + V2.FromAngle(sa) * ((10f + 30f * Hash01(j + 68f)) * p * s) + new V2(0f, -8f * p * s);
                    Circle(at, 1.4f * s, FireYellow.WithAlpha(1f - p), 5);
                    Circle(at - V2.FromAngle(sa) * (3f * s), 1f * s, FireYellow.WithAlpha(0.6f * (1f - p)), 4);
                }
                float pul = 0.8f + 0.2f * Mathf.Sin(t * 60f);
                Circle(head, 13f * pul * s, FireOrange.WithAlpha(0.35f), 16);
                Circle(head, 8f * pul * s, FireYellow.WithAlpha(0.8f), 12);
                Circle(head, 4f * s, FireWhite, 8);
            }

            // head meets tail: a flash, an ember pop and a ring thrown outward
            float since = age - (RingPre + RingSweep);
            if (since >= 0f && since < 0.9f)
            {
                V2 m = c + new V2(R, 0f);
                if (since < 0.3f)
                {
                    float k = since / 0.3f;
                    Circle(m, 34f * s * EaseOut3(k), FireWhite.WithAlpha(0.7f * (1f - k)), 24);
                    Circle(m, 20f * s * EaseOut3(k), FireYellow.WithAlpha(0.9f * (1f - k)), 18);
                }
                if (since < 0.4f)
                {
                    float k = since / 0.4f;
                    Arc(c, R - 6f * s + 28f * s * EaseOut3(k), 6f * (1f - k) * s, FireOrange.WithAlpha(0.8f * (1f - k)), 64);
                }
                for (int j = 0; j < 26; j++)
                {
                    float life = 0.5f + 0.4f * Hash01(j + 80f), k = since / life;
                    if (k >= 1f)
                        continue;
                    float a = Hash01(j + 84f) * MathX.Tau, sp = 60f + 180f * Hash01(j + 88f), travel = sp * (1f - Mathf.Exp(-2.4f * since)) / 2.4f;
                    V2 at = m + V2.FromAngle(a) * (travel * s) + new V2(0f, -40f * since * s);
                    float size = (1.8f + 1.6f * Hash01(j + 92f)) * (1f - k * 0.6f) * s;
                    Circle(at, size * 2f, FireOrange.WithAlpha(0.3f * (1f - k)), 8);
                    Circle(at, size, FireWhite.WithAlpha(1f - k), 6);
                }
            }
        }
    }
}
