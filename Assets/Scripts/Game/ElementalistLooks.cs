using UnityEngine;

namespace Arena
{
    /// The Elementalist DPS's three abilities, drawn (look A of each in Docs/Mockups/elementalist-dps-abilities.html):
    ///   Meteor:   a spinning-tick warning circle, a fireball streaking in with a long tail (DrawMeteorWarning, from DrawZone),
    ///             then a white-hot flash, shockwave, sparks and a scorch mark (FxKind.Meteor, DrawMeteorFx).
    ///   Blizzard: a fogged-out disc full of fast wind-driven snow streaks (DrawBlizzard, from DrawZone).
    ///   Tsunami:  a curling crest with foam, ripple lines and spray (FxKind.Tsunami, DrawTsunamiFx), and wet enemies
    ///             dripping (DrawWet, from DrawEnemyStatus). Game logic: Core/Tsunami.cs.
    public partial class ArenaApp
    {
        static readonly Rgba MeteorFire = new Rgba(1f, 0.59f, 0.24f), MeteorHot = new Rgba(1f, 0.86f, 0.59f), WaveColor = new Rgba(0.31f, 0.82f, 0.91f);
        public const float MeteorFxTime = 2.8f;

        // --- Meteor ----------------------------------------------------------------------------------------------------

        /// While the zone counts down (z.T is what's left): the warning circle fills in, and the rock comes down the sky.
        void DrawMeteorWarning(Zone z, float t)
        {
            var c = z.Def.Color;
            float f = 1f - Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));  // 0 as it's called, 1 as it lands
            float r = z.Radius;
            Circle(z.Pos, r * f, c.WithAlpha(0.22f), 40);
            Arc(z.Pos, r, 3f, c.WithAlpha(MathX.Fmod(t, 0.2f) < 0.1f ? 0.9f : 0.5f), 48);
            for (int k = 0; k < 8; k++)  // ticks turning round the rim
            {
                V2 d = V2.FromAngle(k * MathX.Tau / 8f + t * 1.5f);
                Line(z.Pos + d * (r - 8f), z.Pos + d * (r + 12f), 2.5f, c.WithAlpha(0.8f));
            }
            Line(z.Pos - new V2(12f, 0f), z.Pos + new V2(12f, 0f), 2f, c.WithAlpha(0.8f));
            Line(z.Pos - new V2(0f, 12f), z.Pos + new V2(0f, 12f), 2f, c.WithAlpha(0.8f));
            // The rock: in from the upper right, faster as it falls, with a tail of fading glow.
            V2 sky = z.Pos + new V2(430f, -500f);
            float size = 11f + 18f * f * f;
            for (int k = 18; k >= 1; k--)
            {
                float ff = Mathf.Max(0f, f - k * 0.018f), a = 1f - k / 18f;
                GlowDisc(V2.Lerp(sky, z.Pos, ff * ff), size * (1.2f + a), MeteorFire, 0.55f * a);
            }
            V2 head = V2.Lerp(sky, z.Pos, f * f);
            GlowDisc(head, size * 3f, MeteorFire, 0.8f);
            Circle(head, size, new Rgba(0.35f, 0.16f, 0.1f), 14);
            Circle(head - new V2(size * 0.2f, size * 0.2f), size * 0.6f, MeteorFire.WithAlpha(0.95f), 12);
            Circle(head - new V2(size * 0.25f, size * 0.25f), size * 0.3f, Rgba.White.WithAlpha(0.95f), 8);
        }

        /// FxKind.Meteor: it lands (Pos, Radius = its blast). A flash, a ring, sparks, and a scorch mark that fades over MeteorFxTime.
        void DrawMeteorFx(Effect fx)
        {
            float ti = fx.T, r = fx.Radius;
            float scorch = Mathf.Clamp01(1f - ti / MeteorFxTime);
            Circle(fx.Pos, r * 0.8f, new Rgba(0.1f, 0.055f, 0.04f, 0.55f * scorch), 32);
            if (ti < 0.35f)
                GlowDisc(fx.Pos, r * 3f * (1f - ti / 0.35f), MeteorHot, 0.95f);
            if (ti < 0.5f)
            {
                float k = 1f - Mathf.Pow(1f - ti / 0.5f, 3f);
                Arc(fx.Pos, r * 1.25f * k, 5f * (1f - ti / 0.5f) + 0.5f, MeteorHot.WithAlpha(1f - ti / 0.5f), 40);
            }
            if (ti < 0.9f)
                for (int k = 0; k < 20; k++)
                {
                    float a = Hash01(k) * MathX.Tau, v = 200f + Hash01(k + 5f) * 330f;
                    V2 at = fx.Pos + new V2(Mathf.Cos(a) * v * ti, Mathf.Sin(a) * v * ti * 0.7f + 250f * ti * ti);
                    Circle(at, 3.5f * (1f - ti / 0.9f), MeteorHot.WithAlpha(1f - ti / 0.9f), 6);
                }
        }

        // --- Blizzard --------------------------------------------------------------------------------------------------

        /// The storm: a fog of white that thickens toward the rim, an icy edge and snow streaking across on the wind.
        void DrawBlizzard(Zone z, float t)
        {
            var c = z.Def.Color;
            float R = z.Radius, age = z.Duration - z.T;
            float fade = Mathf.Clamp01(z.T / 0.6f) * Mathf.Clamp01(age / 0.4f);  // in over 0.4 s, out over the last 0.6 s
            FanGradient(z.Pos, R, Rgba.White.WithAlpha(0.1f * fade), Rgba.White.WithAlpha(0.3f * fade), 32);
            const float slant = 0.27f;
            V2 ex = V2.FromAngle(slant), ey = ex.Orthogonal();
            for (int k = 0; k < 44; k++)  // each streak runs along a chord of the circle, so none leaves it
            {
                float y = (Hash01(k + 2f) - 0.5f) * 2f * R * 0.96f, w = Mathf.Sqrt(Mathf.Max(0f, R * R - y * y));
                float len = 28f + 40f * Hash01(k + 3f), speed = 420f + 460f * Hash01(k), span = Mathf.Max(1f, 2f * w - len);
                float u = -w + MathX.Fmod(Hash01(k + 1f) * span + t * speed, span);
                V2 p0 = z.Pos + ex * u + ey * y, p1 = p0 + ex * len;
                Line(p0, p1, 1.6f + 1.8f * Hash01(k + 4f), Rgba.White.WithAlpha((0.4f + 0.5f * Hash01(k + 5f)) * fade));
            }
            Arc(z.Pos, R, 2.5f, c.WithAlpha(0.6f * fade), 48);
        }

        // --- Tsunami ---------------------------------------------------------------------------------------------------

        /// FxKind.Tsunami: a wave from Pos to Pos2 (Radius = half its width). It travels at Game.TsunamiSpeed from the moment it
        /// appears (so everyone sees it where the host has it), leaving a wet shine on the ground behind it.
        void DrawTsunamiFx(Effect fx, float t)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len, side = dir.Orthogonal();
            float half = fx.Radius, front = Mathf.Min(len + Game.TsunamiThick, fx.T * Game.TsunamiSpeed);
            float a = Mathf.Clamp01(fx.T / 0.15f) * Mathf.Clamp01((len + Game.TsunamiThick + 30f - front) / 140f);
            if (a <= 0.01f)
                return;
            System.Func<float, float> bulge = y => 55f * (1f - (y / half) * (y / half));
            System.Func<float, float, V2> at = (s, y) => fx.Pos + dir * s + side * y;
            const int n = 16;
            // The shine it leaves, then the glassy body: clear at the back, bright at the crest.
            var clear = WaveColor.WithAlpha(0f);
            var shine = WaveColor.WithAlpha(0.1f * a);
            var deep = new Rgba(0.12f, 0.5f, 0.74f, 0.5f * a);
            var crest = WaveColor.WithAlpha(0.78f * a);
            float backS = Mathf.Max(0f, front - Game.TsunamiThick * 1.6f);
            for (int i = 0; i < n; i++)
            {
                float y0 = -half + 2f * half * i / n, y1 = -half + 2f * half * (i + 1) / n;
                float tail = Mathf.Max(0f, front - 40f);
                VTri(at(0f, y0), clear, at(0f, y1), clear, at(tail, y0), shine);
                VTri(at(0f, y1), clear, at(tail, y1), shine, at(tail, y0), shine);
                float f0 = front + bulge(y0), f1 = front + bulge(y1);
                VTri(at(backS, y0), clear, at(backS, y1), clear, at(f0 - 34f, y0), deep);
                VTri(at(backS, y1), clear, at(f1 - 34f, y1), deep, at(f0 - 34f, y0), deep);
                VTri(at(f0 - 34f, y0), deep, at(f1 - 34f, y1), deep, at(f0, y0), crest);
                VTri(at(f1 - 34f, y1), deep, at(f1, y1), crest, at(f0, y0), crest);
            }
            for (int k = 1; k < 5; k++)  // ripple lines parallel to the crest
            {
                V2 prev = at(front - k * 26f + bulge(-half + 8f) * 0.8f, -half + 8f);
                for (int i = 1; i <= 12; i++)
                {
                    float y = -half + 8f + (2f * half - 16f) * i / 12f;
                    V2 cur = at(front - k * 26f + bulge(y) * 0.8f + 3f * Mathf.Sin(t * 7f + y * 0.05f + k), y);
                    Line(prev, cur, 1.8f, Rgba.White.WithAlpha(0.15f * a));
                    prev = cur;
                }
            }
            for (int k = 0; k < 30; k++)  // foam along the crest
            {
                float y = -half + 10f + (2f * half - 20f) * k / 29f;
                Circle(at(front + bulge(y) - 4f + 5f * Mathf.Sin(t * 11f + k * 1.7f), y), 7f + 4f * Hash01(k + (float)System.Math.Floor(t * 6f)), Rgba.White.WithAlpha(0.85f * a), 8);
            }
            for (int k = 0; k < 24; k++)  // spray flung back off it
            {
                float ph = MathX.Fmod(t * 1.6f + Hash01(k), 1f), y = (Hash01(k + 9f) - 0.5f) * 2f * half * 0.95f;
                V2 p = at(front + bulge(y) - ph * 70f + 8f, y) + new V2(0f, -12f - 40f * Mathf.Sin(ph * MathX.Pi));
                Circle(p, 2.2f, Rgba.White.WithAlpha(0.8f * (1f - ph) * a), 6);
            }
            GlowDisc(at(front + 24f, 0f), half * 0.9f, WaveColor, 0.18f * a);
        }

        /// A wet enemy: a blue cast, a sheen round it and drops running off it. Returns the tinted body colour.
        Rgba DrawWet(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            GlowDisc(pos, r + 18f, WaveColor, 0.22f);
            for (int k = 0; k < 4; k++)
            {
                float f = MathX.Fmod(t * 1.4f + k * 0.27f + e.Id * 0.19f, 1f);
                Circle(pos + new V2((k - 1.5f) * r * 0.4f, r * 0.3f + f * (r + 14f)), (2f + r * 0.08f) * (1f - f * 0.5f), WaveColor.WithAlpha(0.9f * (1f - f)), 6);
            }
            return col.Mix(new Rgba(0.2f, 0.45f, 0.65f), 0.35f);
        }
    }
}
