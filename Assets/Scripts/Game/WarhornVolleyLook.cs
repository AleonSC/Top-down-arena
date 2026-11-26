using UnityEngine;

namespace Arena
{
    /// Warhorn Volley (Core/WarhornVolley.cs), the Fire Arrows look of Docs/Mockups/warhorn-volley.html, drawn from the
    /// FxKind.WarhornVolley effect's age alone (Pos = where the knight stood, Pos2 = the lane's far end, Radius = their id,
    /// colour alpha = the lane's half width / 1000). Every arrow is picked from the effect's own numbers, so every screen draws the same volley.
    ///   DrawWarhornGround (in the effects pass): the volley's shadow riding over the lane, each arrow's shadow racing in,
    ///     the shafts stuck in the floor (quivering as they land, fading after ~2 s) and the little fires at their feet
    ///   DrawWarhornSky (over the arena's contents): the black war horn raised to the knight's helm, the sound rolling out of
    ///     its bell, and the burning arrows arcing in from behind and above
    ///   DrawWarhornGlow (the glow pass): the bell's flare, the arrows' burning heads and the fires on the floor
    public partial class ArenaApp
    {
        static readonly Rgba VolShaft = new Rgba(0.35f, 0.23f, 0.15f), VolFletch = new Rgba(0.19f, 0.17f, 0.15f), VolHead = new Rgba(1f, 0.7f, 0.28f);
        static readonly Rgba VolFire = new Rgba(1f, 0.47f, 0.16f), VolFireCore = new Rgba(1f, 0.9f, 0.6f), VolDust = new Rgba(0.47f, 0.4f, 0.35f);
        static readonly Rgba VolHornDark = new Rgba(0.11f, 0.09f, 0.07f), VolHornLight = new Rgba(0.33f, 0.26f, 0.2f), VolHornBand = new Rgba(1f, 0.6f, 0.24f);
        static readonly Rgba VolInk = new Rgba(0f, 0f, 0f), VolGlove = new Rgba(0.56f, 0.61f, 0.67f);
        const int VolArrowCount = 640;
        const float VolHornScale = 1.5f, VolSkyBack = 140f, VolSkyUp = 230f, VolArrowLen = 30f, VolStuckLen = 19f;

        /// The frame a volley is drawn in: along the aim, to the left of it, up the screen, and the way its arrows fall.
        struct VolFrame
        {
            public V2 Origin, Dir, Left, Fall, Sky;
            public float Half;
            public int Seed;
        }

        static VolFrame VolFrameOf(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            V2 dir = path.Length < 1f ? new V2(1f, 0f) : path.Normalized();
            V2 sky = dir * -VolSkyBack + new V2(0f, -VolSkyUp);  // (screen up is -y)  // where an arrow starts its fall, from where it lands
            return new VolFrame
            {
                Origin = fx.Pos, Dir = dir, Left = new V2(-dir.Y, dir.X), Sky = sky, Fall = (sky * -1f).Normalized(),
                Half = fx.Color.A > 0.05f ? fx.Color.A * 1000f : Game.VolHalf, Seed = ((int)(fx.Pos.X * 7.1f) ^ (int)(fx.Pos.Y * 13.3f)) & 0xffff,
            };
        }

        /// Arrow i: where it lands and when. False for the few that would land on the knight.
        static bool VolArrow(VolFrame f, int i, out V2 at, out float land)
        {
            float h1 = CavHash(f.Seed + i * 3), h2 = CavHash(f.Seed + i * 7 + 1), h3 = CavHash(f.Seed + i * 11 + 2);
            land = Game.VolLandT + (i + h1) / VolArrowCount * (Game.VolEndT - Game.VolLandT);
            float u = Game.VolFront(land) + (h2 - 0.5f) * 90f, v = (h3 * 2f - 1f) * f.Half;
            at = f.Origin + f.Dir * u + f.Left * v;
            return u * u + v * v > 55f * 55f;
        }

        static float VolStuckAlpha(float since) => 1f - Mathf.Clamp01((since - 1.9f) / 0.7f);

        /// How far the horn is raised (0 at the hip, 1 at the helm) at this age.
        static float VolRaise(float age) =>
            age < Game.VolBlastT ? CavEaseOut(age / Game.VolBlastT) : age < Game.VolHornT ? 1f : 1f - Mathf.Clamp01((age - Game.VolHornT) / 0.3f);

        void VolArrowShape(V2 tip, V2 fall, float len, float alpha, bool head)
        {
            V2 tail = tip - fall * len, side = new V2(-fall.Y, fall.X);
            Line(tail, tip, 3.4f, VolInk.WithAlpha(0.5f * alpha));
            Line(tail, tip, 2f, VolShaft.WithAlpha(alpha));
            if (head)
                CavTri(tip + side * 3.2f, tip + fall * 7f, tip - side * 3.2f, VolHead.WithAlpha(alpha));
            V2 f0 = tail + fall * 1.5f, f1 = tail + fall * 9f;
            CavTri(f0, f1, tail - fall * 2f + side * 5f, VolFletch.WithAlpha(alpha));
            CavTri(f0, f1, tail - fall * 2f - side * 5f, VolFletch.WithAlpha(alpha));
        }

        void VolFlame(V2 at, float t, int seed, float alpha)
        {
            if (alpha <= 0.01f)
                return;
            float fl = 0.75f + 0.25f * Mathf.Sin(t * 31f + seed * 7f) + 0.12f * Mathf.Sin(t * 53f + seed);
            V2 up = new V2(0f, -1f), x = new V2(1f, 0f);
            CavTri(at - x * 4f, at + up * 15f * fl + x * 0.6f, at + x * 4f, VolFire.WithAlpha(0.85f * alpha));
            CavTri(at - x * 2f, at + up * 8f * fl, at + x * 2f, VolFireCore.WithAlpha(0.9f * alpha));
        }

        void DrawWarhornGround(Effect fx)
        {
            var f = VolFrameOf(fx);
            float age = fx.T, t = game.AnimT;

            // The volley's shadow: a dark band riding just ahead of where the arrows land.
            if (age > Game.VolLandT - Game.VolFallT && age < Game.VolEndT + 0.2f)
            {
                float a = Mathf.Clamp01((age - (Game.VolLandT - Game.VolFallT)) / 0.3f) * Mathf.Clamp01((Game.VolEndT + 0.2f - age) / 0.3f);
                float fr = Game.VolFront(age);
                V2 l = f.Left * f.Half;
                V2 p0 = f.Origin + f.Dir * (fr - 60f), p1 = f.Origin + f.Dir * (fr + 50f), p2 = f.Origin + f.Dir * (fr + 320f);
                Rgba clear = VolInk.WithAlpha(0f), dark = VolInk.WithAlpha(0.26f * a);
                VQuad(p0 + l, clear, p0 - l, clear, p1 - l, dark, p1 + l, dark);
                VQuad(p1 + l, dark, p1 - l, dark, p2 - l, clear, p2 + l, clear);
            }

            // The blast kicks dust up round the knight's feet.
            if (age > Game.VolBlastT && age < Game.VolBlastT + 0.9f)
            {
                float g = (age - Game.VolBlastT) / 0.9f;
                for (int k = 0; k < 12; k++)
                {
                    float an = CavHash(f.Seed + k * 5) * MathX.Tau, r = 18f + CavEaseOut(g) * (60f + CavHash(k * 9) * 50f);
                    Circle(VolKnightAt(fx) + V2.FromAngle(an) * r, 9f + g * 14f, VolDust.WithAlpha(0.3f * (1f - g)), 10);
                }
            }

            V2 at;
            float land;
            for (int i = 0; i < VolArrowCount; i++)
            {
                if (!VolArrow(f, i, out at, out land))
                    continue;
                float since = age - land;
                if (since < -Game.VolFallT)
                    break;  // (the rest land later still)
                if (since < 0f)
                {
                    // In the air: its shadow races in along the floor under it.
                    float g = 1f + since / Game.VolFallT;
                    V2 sh = at - f.Dir * (VolSkyBack * (1f - g));
                    Line(sh - f.Dir * 11f, sh + f.Dir * 6f, 2.2f, VolInk.WithAlpha(0.1f + 0.28f * g));
                    continue;
                }
                float al = VolStuckAlpha(since);
                if (al <= 0f)
                    continue;
                if (since < 0.3f)
                    Circle(at, 4f + since * 40f, VolDust.WithAlpha(0.4f * (1f - since / 0.3f)), 8);
                // The shaft in the floor, leaning back the way it came, quivering as it lands.
                float wob = Mathf.Sin(since * 46f) * 0.22f * Mathf.Exp(-since * 7f) + (CavHash(f.Seed + i) - 0.5f) * 0.25f;
                V2 fall = f.Fall.Rotated(wob);
                Line(at, at - f.Dir * VolStuckLen * 0.9f + new V2(0f, 1.5f), 2f, VolInk.WithAlpha(0.25f * al));  // its shadow
                VolArrowShape(at, fall, VolStuckLen, al, false);
                Circle(at, 1.8f, VolInk.WithAlpha(0.6f * al), 6);
                if (i % 3 == 0 && since < 0.9f)
                    VolFlame(at, t, i, 0.85f * (1f - since / 0.9f));
            }
        }

        /// Where the knight who blew the horn is drawn (or where they stood, if they've gone).
        V2 VolKnightAt(Effect fx)
        {
            Player p;
            return game.Players.TryGetValue((int)fx.Radius, out p) ? p.Disp : fx.Pos;
        }

        void DrawWarhornSky()
        {
            foreach (var fx in FxOf(FxKind.WarhornVolley))
                if (fx.Kind == FxKind.WarhornVolley)
                    DrawWarhornSkyFx(fx);
        }

        void DrawWarhornSkyFx(Effect fx)
        {
            var f = VolFrameOf(fx);
            float age = fx.T;

            // The horn, in the knight's hand from the hip up to the helm.
            float raise = VolRaise(age);
            Player kn;
            if (raise > 0f && game.Players.TryGetValue((int)fx.Radius, out kn) && !kn.Downed)
            {
                V2 fwd = V2.FromAngle(kn.Aim) * VolHornScale, left = new V2(-fwd.Y, fwd.X), c = kn.Disp;
                V2 bell = DrawVolHorn(c, fwd, left, raise, age);
                // The sound rolling out of the bell while it's blown.
                for (float s = Game.VolBlastT; s < Game.VolHornT; s += 0.11f)
                {
                    float a = age - s;
                    if (a < 0f || a > 0.8f)
                        continue;
                    float g = a / 0.8f, r = 16f + 330f * CavEaseOut(g);
                    Arc(bell, r, 1f + 3.5f * (1f - g), VolHornBand.WithAlpha(0.45f * (1f - g)), 40, kn.Aim - 1.05f, 2.1f);
                }
            }

            // The arrows in the air: they arc in from behind and above, growing smaller as they come down.
            V2 at;
            float land;
            for (int i = 0; i < VolArrowCount; i++)
            {
                if (!VolArrow(f, i, out at, out land))
                    continue;
                float since = age - land;
                if (since < -Game.VolFallT)
                    break;
                if (since >= 0f)
                    continue;
                float g = 1f + since / Game.VolFallT, sc = 1f + 0.55f * (1f - g), al = Mathf.Clamp01(g / 0.12f);
                V2 tip = at + f.Sky * (1f - g);
                Line(tip - f.Fall * 50f * sc, tip - f.Fall * 18f, 1.8f, VolFire.WithAlpha(0.18f * al));  // its ember trail
                VolArrowShape(tip, f.Fall, VolArrowLen * sc, al, true);
            }
        }

        /// The black war horn in the knight's off hand: mouthpiece at the helm, bell out ahead and to the left. Returns the bell.
        V2 DrawVolHorn(V2 c, V2 fwd, V2 left, float raise, float age)
        {
            // (the mockup's frame: x ahead, y to the right of the aim; screen y points down)
            V2 A = c + fwd * Mathf.Lerp(2f, 8f, raise) + left * Mathf.Lerp(-15f, -3f, raise);
            V2 C = c + fwd * Mathf.Lerp(4f, 20f, raise) + left * Mathf.Lerp(-22f, 4f, raise);
            V2 B = c + fwd * Mathf.Lerp(-2f, 31f, raise) + left * Mathf.Lerp(-30f, -14f, raise);
            float alpha = Mathf.Clamp01(raise * 1.5f);
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float s0 = (float)i / n, s1 = (float)(i + 1) / n;
                V2 a = VolBezier(A, C, B, s0), b = VolBezier(A, C, B, s1), nrm = (b - a).Normalized();
                V2 side = new V2(-nrm.Y, nrm.X);
                float w0 = (1.6f + 5.2f * Mathf.Pow(s0, 1.7f)) * VolHornScale, w1 = (1.6f + 5.2f * Mathf.Pow(s1, 1.7f)) * VolHornScale;
                Rgba col = new Rgba(Mathf.Lerp(VolHornDark.R, VolHornLight.R, s1), Mathf.Lerp(VolHornDark.G, VolHornLight.G, s1), Mathf.Lerp(VolHornDark.B, VolHornLight.B, s1), alpha);
                VQuad(a + side * w0, col, a - side * w0, col, b - side * w1, col, b + side * w1, col);
            }
            foreach (float s in new[] { 0.35f, 0.68f })
            {
                V2 p = VolBezier(A, C, B, s), nrm = (VolBezier(A, C, B, s + 0.02f) - p).Normalized(), side = new V2(-nrm.Y, nrm.X);
                float w = (1.9f + 5.2f * Mathf.Pow(s, 1.7f)) * VolHornScale;
                Line(p + side * w, p - side * w, 2.2f, VolHornBand.WithAlpha(alpha));
            }
            Arc(B, 6.8f * VolHornScale, 2.4f, VolHornBand.WithAlpha(alpha), 16);
            Circle(B, 5.4f * VolHornScale, VolInk.WithAlpha(0.6f * alpha), 12);
            Circle(A, 2.8f * VolHornScale, VolGlove.WithAlpha(alpha), 8);  // the gloved hand at the mouthpiece
            return B;
        }

        static V2 VolBezier(V2 a, V2 c, V2 b, float s)
        {
            float u = 1f - s;
            return a * (u * u) + c * (2f * u * s) + b * (s * s);
        }

        void DrawWarhornGlow()
        {
            foreach (var fx in FxOf(FxKind.WarhornVolley))
            {
                if (fx.Kind != FxKind.WarhornVolley)
                    continue;
                var f = VolFrameOf(fx);
                float age = fx.T;
                Player kn;
                if (age > Game.VolBlastT && age < Game.VolHornT && game.Players.TryGetValue((int)fx.Radius, out kn))
                    GlowDisc(kn.Disp + V2.FromAngle(kn.Aim) * 30f, 34f, VolFire, 0.45f);
                V2 at;
            float land;
            for (int i = 0; i < VolArrowCount; i++)
                {
                    if (!VolArrow(f, i, out at, out land))
                        continue;
                    float since = age - land;
                    if (since < -Game.VolFallT)
                        break;
                    if (since < 0f)
                    {
                        if (i % 2 == 0)
                        {
                            float g = 1f + since / Game.VolFallT;
                            GlowDisc(at + f.Sky * (1f - g), 16f, VolFire, 0.5f * Mathf.Clamp01(g / 0.12f));
                        }
                    }
                    else if (i % 3 == 0 && since < 0.9f)
                        GlowDisc(at + new V2(0f, -5f), 20f, VolFire, 0.4f * (1f - since / 0.9f));
                }
            }
        }
    }
}
