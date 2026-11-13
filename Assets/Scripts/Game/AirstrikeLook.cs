using UnityEngine;

namespace Arena
{
    /// Airstrike (Core/Airstrike.cs), the Carpet Wall look of Docs/Mockups/airstrike-rework.html, drawn from one FxKind.Airstrike's age:
    /// the red smoke flare arcing from the thrower to the cursor and smoking there; the lane (red band, marching dashed edges and
    /// chevrons pointing the jet's way) growing out from the flare to both walls; the jet's shadow sweeping the floor ahead of the jet,
    /// with two contrails and a hot engine; each bomb falling with its shadow growing under it (its blast is the grenade's Frag Burst
    /// Boom, sent by the host); the craters it leaves; and the burning strip, flickering flames along the lane until it goes out.
    /// The mastered second wall (colour alpha 0.5) has no flare. Timing: Game.AirFlareT, AirJetT, AirBombTime, AirFireEnd.
    public partial class ArenaApp
    {
        static readonly Rgba AirRed = new Rgba(1f, 0.36f, 0.26f), AirSmoke = new Rgba(0.86f, 0.24f, 0.18f), AirJet = new Rgba(0.55f, 0.59f, 0.62f),
            AirJetLight = new Rgba(0.79f, 0.82f, 0.84f), AirCockpit = new Rgba(0.17f, 0.21f, 0.25f), AirEngine = new Rgba(1f, 0.67f, 0.31f),
            AirBomb = new Rgba(0.23f, 0.27f, 0.19f), AirCrater = new Rgba(0.04f, 0.03f, 0.02f), AirFlame = new Rgba(1f, 0.47f, 0.16f), AirFlameHot = new Rgba(1f, 0.82f, 0.4f);

        void DrawAirstrikeFx(Effect fx)
        {
            V2 from = fx.Pos, lane = fx.Pos2 - fx.Pos;
            float len = lane.Length;
            if (len < 1f)
                return;
            V2 dir = lane / len, side = dir.Orthogonal();
            float age = fx.T;
            bool flare = fx.Color.A > 0.75f;
            int owner = Mathf.FloorToInt(fx.Radius / 10000f);
            float flareAlong = fx.Radius - owner * 10000f;
            V2 flareAt = from + dir * flareAlong;
            int bombs = Game.AirBombCount(len);
            float lastBomb = bombs > 0 ? Game.AirBombTime(bombs - 1) : Game.AirJetT, fireEnd = Game.AirFireEnd(len);
            float half = Game.AirFireHalf, R = Game.AirBombRadius;

            // Craters, under everything else.
            for (int i = 0; i < bombs; i++)
            {
                float a = age - Game.AirBombTime(i);
                if (a < 0f)
                    continue;
                float k = Mathf.Clamp01(1f - (a - 2.2f) / 2f) * Mathf.Clamp01(a / 0.1f);
                if (k <= 0f)
                    continue;
                V2 c = from + dir * Game.AirBombAlong(i);
                FanGradient(c, R * 0.55f, AirCrater.WithAlpha(0.6f * k), AirCrater.WithAlpha(0f), 18);
                for (int s = 0; s < 7; s++)
                {
                    V2 d = V2.FromAngle(s * 0.9f + i * 1.7f);
                    Line(c + d * (R * 0.28f), c + d * (R * (0.55f + 0.15f * CavHash(i * 7 + s))), 3f, new Rgba(0.17f, 0.14f, 0.11f, 0.7f * k));
                }
            }

            // The lane: grows out from the flare, pulses, and goes once the last bomb has fallen.
            float laneT0 = flare ? Game.AirFlareT : 0f, la = age < laneT0 ? 0f : Mathf.Clamp01((age - laneT0) / 0.15f) * (1f - Mathf.Clamp01((age - lastBomb) / 0.35f));
            if (la > 0.01f)
            {
                float g = CavEaseOut((age - laneT0) / 0.35f), s0 = flareAlong * (1f - g), s1 = flareAlong + (len - flareAlong) * g;
                float pulse = 0.5f + 0.5f * Mathf.Sin(age * 14f);
                V2 p0 = from + dir * s0, p1 = from + dir * s1;
                var fill = AirRed.WithAlpha((0.07f + 0.05f * pulse) * la);
                VQuad(p0 + side * half, fill, p1 + side * half, fill, p1 - side * half, fill, p0 - side * half, fill);
                for (float s = Mathf.Repeat(age * 160f, 40f) + s0; s < s1; s += 40f)  // dashed edges marching the jet's way
                {
                    float e = Mathf.Min(s1, s + 22f);
                    Line(from + dir * s + side * half, from + dir * e + side * half, 3f, AirRed.WithAlpha(0.8f * la));
                    Line(from + dir * s - side * half, from + dir * e - side * half, 3f, AirRed.WithAlpha(0.8f * la));
                }
                for (float s = Mathf.Repeat(age * 260f, 120f) + s0; s < s1; s += 120f)  // chevrons pointing the way the jet flies
                {
                    V2 c = from + dir * s;
                    Line(c - dir * 12f + side * (half * 0.4f), c + dir * 8f, 4f, AirRed.WithAlpha(0.45f * la));
                    Line(c - dir * 12f - side * (half * 0.4f), c + dir * 8f, 4f, AirRed.WithAlpha(0.45f * la));
                }
            }

            // The burning strip, after the last bomb.
            float fa = age - lastBomb;
            if (fa > 0f && age < fireEnd)
            {
                float k = Mathf.Clamp01(fa / 0.2f) * Mathf.Clamp01((fireEnd - age) / 0.5f);
                var band = AirFlame.WithAlpha(0.1f * k);
                VQuad(from + side * half, band, fx.Pos2 + side * half, band, fx.Pos2 - side * half, band, from - side * half, band);
                int n = 0;
                for (float s = 0f; s < len; s += 34f, n++)
                    for (int j = 0; j < 2; j++)
                    {
                        float h = CavHash(n * 2 + j), f = Mathf.Repeat(age * 2.2f + h + j * 0.5f, 1f);
                        V2 c = from + dir * (s + 10f * h) + side * ((CavHash(n * 5 + j * 3) - 0.5f) * half * 1.4f) + new V2(0f, -26f * f);
                        float r = 22f + 14f * (1f - f);
                        FanGradient(c, r, AirFlame.Mix(AirFlameHot, 1f - f).WithAlpha(0.55f * k * (1f - f)), AirFlame.WithAlpha(0f), 12);
                    }
            }

            // The flare: arcing in, then smoking where it landed.
            if (flare)
            {
                Player thrower;
                if (age < Game.AirFlareT && game.Players.TryGetValue(owner, out thrower))
                {
                    float u = age / Game.AirFlareT;
                    V2 ground = V2.Lerp(thrower.Disp, flareAt, u), at = ground + new V2(0f, -90f * Mathf.Sin(MathX.Pi * u));
                    Circle(ground, 5f, new Rgba(0f, 0f, 0f, 0.3f), 10);
                    FanGradient(at, 26f, AirRed.WithAlpha(0.5f), AirRed.WithAlpha(0f), 14);
                    Circle(at, 7f, AirRed, 12);
                }
                float sa = age - Game.AirFlareT;
                if (sa >= 0f)
                {
                    float k = Mathf.Clamp01(1f - (age - lastBomb - 0.6f) / 1f);
                    for (int i = 0; i < 9; i++)
                    {
                        float p = Mathf.Repeat(sa * 0.45f + CavHash(i), 1f), d = 14f + 60f * p + 26f * CavHash(i + 3), a = -2.2f + CavHash(i + 9) * 0.9f;
                        Circle(flareAt + V2.FromAngle(a) * d, 14f + 34f * p, AirSmoke.WithAlpha(0.3f * (1f - p) * k), 14);
                    }
                    if (k > 0f)
                    {
                        FanGradient(flareAt, 36f + 6f * Mathf.Sin(age * 20f), AirRed.WithAlpha(0.55f * k), AirRed.WithAlpha(0f), 16);
                        Circle(flareAt, 7f, AirRed.WithAlpha(k), 12);
                        Circle(flareAt, 3f, Rgba.White.WithAlpha(0.9f * k), 8);
                    }
                }
            }

            // Bombs falling, their shadows growing under them.
            for (int i = 0; i < bombs; i++)
            {
                float u = (age - (Game.AirBombTime(i) - Game.AirFall)) / Game.AirFall;
                if (u < 0f || u >= 1f)
                    continue;
                V2 g = from + dir * Game.AirBombAlong(i);
                Circle(g, 5f + 8f * u, new Rgba(0f, 0f, 0f, 0.3f + 0.3f * u), 12);
                V2 b = g - dir * (50f * (1f - u)) + new V2(0f, -55f * (1f - u));
                Line(b - dir * (10f - 2f * u), b + dir * (10f - 2f * u), 10f - 2f * u, AirBomb);
                Line(b - dir * 14f + side * 5f, b - dir * 14f - side * 5f, 4f, AirBomb.Darkened(0.4f));
            }

            // The jet: its shadow on the floor, contrails, then the jet above.
            float js = (age - Game.AirJetT) * Game.AirJetSpeed - Game.AirLead;
            if (age >= Game.AirJetT && js < len + 500f)
            {
                V2 c = from + dir * js;
                DrawJetShape(c + new V2(55f, 75f), dir, side, 1.15f, new Rgba(0f, 0f, 0f, 0.38f), new Rgba(0f, 0f, 0f, 0.38f), new Rgba(0f, 0f, 0f, 0f));
                for (int k = 1; k < 24; k++)
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        V2 q = c - dir * (20f + k * 34f) + side * (sgn * 40f);
                        Circle(q, 4f + k * 0.5f, new Rgba(0.9f, 0.92f, 0.94f, 0.26f * (1f - k / 24f)), 10);
                    }
                FanGradient(c - dir * 44f, 30f, AirEngine.WithAlpha(0.7f), AirEngine.WithAlpha(0f), 12);
                DrawJetShape(c, dir, side, 1f, AirJet, AirJetLight, AirCockpit);
            }
        }

        static V2 JetPt(V2 c, V2 dir, V2 side, float k, float x, float y) => c + dir * (x * k) + side * (y * k);

        /// A jet seen from above, nose along `dir`, about 140 px long at k = 1.
        void DrawJetShape(V2 c, V2 dir, V2 side, float k, Rgba body, Rgba top, Rgba cockpit)
        {
            SetColor(body);
            Tri(JetPt(c, dir, side, k, 70f, 0f), JetPt(c, dir, side, k, 30f, -10f), JetPt(c, dir, side, k, 30f, 10f));                                         // nose
            Quad(JetPt(c, dir, side, k, 30f, -10f), JetPt(c, dir, side, k, -52f, -8f), JetPt(c, dir, side, k, -52f, 8f), JetPt(c, dir, side, k, 30f, 10f));                         // fuselage
            Quad(JetPt(c, dir, side, k, 22f, -10f), JetPt(c, dir, side, k, 0f, -60f), JetPt(c, dir, side, k, -16f, -60f), JetPt(c, dir, side, k, -8f, -10f));                       // wings
            Quad(JetPt(c, dir, side, k, 22f, 10f), JetPt(c, dir, side, k, 0f, 60f), JetPt(c, dir, side, k, -16f, 60f), JetPt(c, dir, side, k, -8f, 10f));
            Quad(JetPt(c, dir, side, k, -36f, -8f), JetPt(c, dir, side, k, -50f, -30f), JetPt(c, dir, side, k, -60f, -30f), JetPt(c, dir, side, k, -52f, -6f));                     // tailplanes
            Quad(JetPt(c, dir, side, k, -36f, 8f), JetPt(c, dir, side, k, -50f, 30f), JetPt(c, dir, side, k, -60f, 30f), JetPt(c, dir, side, k, -52f, 6f));
            if (top.A > 0f)
            {
                SetColor(top);
                Quad(JetPt(c, dir, side, k, 56f, -4f), JetPt(c, dir, side, k, 10f, -6f), JetPt(c, dir, side, k, 10f, 6f), JetPt(c, dir, side, k, 56f, 4f));
            }
            if (cockpit.A > 0f)
            {
                SetColor(cockpit);
                Quad(JetPt(c, dir, side, k, 52f, -3.5f), JetPt(c, dir, side, k, 32f, -5f), JetPt(c, dir, side, k, 32f, 5f), JetPt(c, dir, side, k, 52f, 3.5f));
            }
        }
    }
}
