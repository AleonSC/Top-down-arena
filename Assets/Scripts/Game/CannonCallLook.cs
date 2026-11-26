using UnityEngine;

namespace Arena
{
    /// Cannon Call (Core/CannonCall.cs), the Iron Round look of Docs/Mockups/cannon-call.html, drawn from each FxKind.CannonCall
    /// effect's age alone (Pos = where the knight stood, Pos2 = where the ball lands, Radius = their id * 1000 + the blast radius,
    /// Color.A = the landing time / 4 s):
    ///   DrawCannonGround (the effects pass): the mark (a red disc, the dashed blast ring, the core ring closing in as the countdown,
    ///     a white countdown arc and four ticks), the ball's shadow growing on it; at the slam a white flash, a shock ring, a dust
    ///     ring, stone chunks thrown up and out, sparks; then the crater (cracks, a dark pit, the spent ball, smoke) for Game.CcCraterT s
    ///   DrawCannonSky (over the arena's contents): the flare streaking from the knight to the mark, and the iron ball dropping in
    ///     from behind and above on its smoke trail, growing smaller as it comes down, its fuse sputtering
    ///   DrawCannonGlow (the glow pass): the flare, the fuse, the slam's flash
    public partial class ArenaApp
    {
        static readonly Rgba CcMark = new Rgba(0.9f, 0.27f, 0.24f), CcFlash = new Rgba(1f, 0.93f, 0.78f), CcShock = new Rgba(0.9f, 0.86f, 0.78f);
        static readonly Rgba CcDust = new Rgba(0.59f, 0.55f, 0.5f), CcSmoke = new Rgba(0.45f, 0.44f, 0.43f), CcPit = new Rgba(0.09f, 0.08f, 0.08f);
        static readonly Rgba CcIron = new Rgba(0.16f, 0.16f, 0.19f), CcIronLit = new Rgba(0.54f, 0.56f, 0.6f), CcBand = new Rgba(0.35f, 0.38f, 0.42f);
        static readonly Rgba CcEmber = new Rgba(1f, 0.67f, 0.31f), CcStone = new Rgba(0.36f, 0.34f, 0.31f), CcInk = new Rgba(0f, 0f, 0f);
        const float CcSkyBack = 260f, CcSkyUp = 300f, CcBallR = 11f;

        static float CcLandOf(Effect fx) => fx.Color.A * 4f;
        static float CcRadiusOf(Effect fx) => fx.Radius - (int)(fx.Radius / 1000f + 0.001f) * 1000f;
        static int CcSeed(Effect fx) => ((int)(fx.Pos2.X * 7.1f) ^ (int)(fx.Pos2.Y * 13.3f)) & 0xffff;

        /// Where the falling ball is at this age, how big it's drawn (2.6 high up, 1 on the floor) and how far through its fall (0-1).
        static V2 CcBallAt(Effect fx, float age, out float scale, out float f)
        {
            float land = CcLandOf(fx);
            f = Mathf.Clamp01((age - (land - Game.CcFallT)) / Game.CcFallT);
            V2 d = fx.Pos2 - fx.Pos;
            d = d.Length < 1f ? new V2(1f, 0f) : d.Normalized();
            V2 sky = fx.Pos2 - d * CcSkyBack + new V2(0f, -CcSkyUp);  // (screen up is -y)
            float g = f * f * 0.85f + f * 0.15f;
            scale = Mathf.Lerp(2.6f, 1f, f * f);
            return sky + (fx.Pos2 - sky) * g;
        }

        /// WorldEffects.cs: FxKind.CannonCall.
        void DrawCannonGround(Effect fx)
        {
            float age = fx.T, land = CcLandOf(fx), R = CcRadiusOf(fx), core = R * Game.CcCore / Game.CcRadius, a = age - land;
            V2 at = fx.Pos2;
            int seed = CcSeed(fx);

            if (a >= 0f)
                DrawCannonCrater(at, R, a, seed);

            // The mark, from the flare's landing to the slam.
            if (age >= Game.CcFlareT && a < 0.05f)
            {
                float w = land - Game.CcFlareT, f = Mathf.Clamp01((age - Game.CcFlareT) / w), al = EaseOut3(Mathf.Clamp01((age - Game.CcFlareT) / 0.15f));
                Circle(at, R, CcMark.WithAlpha((0.06f + 0.1f * f) * al), 40);
                const int dashes = 18;
                for (int i = 0; i < dashes; i++)
                {
                    float s = MathX.Tau * i / dashes + age * 0.25f;
                    Arc(at, R, 2f, CcMark.WithAlpha(0.75f * al), 4, s, MathX.Tau / dashes * 0.55f);
                }
                Arc(at, Mathf.Lerp(R, core, f * f), 1.5f, CcMark.WithAlpha(0.9f * al), 40);
                Arc(at, core * 0.42f, 3f, Rgba.White.WithAlpha(0.85f * al), 24, -MathX.Pi / 2f, MathX.Tau * f);
                for (int i = 0; i < 4; i++)
                {
                    V2 d = V2.FromAngle(i * MathX.Pi / 2f);
                    Line(at + d * core * 0.55f, at + d * core * 0.8f, 2f, CcMark.WithAlpha(0.9f * al));
                }
                // The ball's shadow, growing as it falls.
                float sc, ff;
                CcBallAt(fx, age, out sc, out ff);
                if (ff > 0f)
                {
                    float sr = Mathf.Lerp(6f, CcBallR * 1.45f * R / Game.CcRadius, ff * ff);
                    Ellipse(at, sr * 1.15f, sr * 0.8f, CcInk.WithAlpha(0.15f + 0.45f * ff));
                }
            }

            // The slam.
            if (a >= 0f && a < 1.6f)
            {
                if (a < 0.1f)
                    Circle(at, R * 0.4f * (1f - a / 0.1f) + 8f, Rgba.White.WithAlpha(0.75f * (1f - a / 0.1f)), 28);
                if (a < 0.45f)
                {
                    float rf = EaseOut3(a / 0.4f);
                    Arc(at, 20f + R * 1.15f * rf, 5f * (1f - rf) + 1.5f, CcShock.WithAlpha(0.85f * (1f - a / 0.45f)), 48);
                }
                if (a < 0.7f)
                    Arc(at, 10f + R * 0.85f * EaseOut3(a / 0.6f), 10f * (1f - a / 0.7f) + 2f, CcDust.WithAlpha(0.5f * (1f - a / 0.7f)), 40);
                // Dust rolling out.
                for (int i = 0; i < 16; i++)
                {
                    float h1 = Hash01(seed + i * 1.7f), h2 = Hash01(seed + i * 3.1f), h3 = Hash01(seed + i * 5.3f), f = a / 1.5f;
                    if (f >= 1f)
                        break;
                    float an = h1 * MathX.Tau, sp = 150f * (0.4f + 0.6f * h2), d = sp * 1.5f * (f - f * f / 2f);
                    V2 p = at + V2.FromAngle(an) * d + new V2(0f, -20f * f);
                    Circle(p, 22f * (0.6f + 0.8f * h3) * (0.45f + f * 1.1f), CcDust.WithAlpha(0.3f * Mathf.Pow(1f - f, 1.4f)), 14);
                }
                // Stone chunks thrown up and out, with their shadows.
                for (int i = 0; i < 12; i++)
                {
                    float h = Hash01(seed + i * 4.4f), an = h * MathX.Tau, sp = 120f + Hash01(seed + i * 2.2f) * 160f, life = 0.6f + Hash01(seed + i * 6.6f) * 0.3f;
                    if (a > life)
                        continue;
                    float g = a / life, d = sp * a * (1f - g * 0.4f), hz = Mathf.Sin(Mathf.PI * g) * (24f + Hash01(seed + i * 1f) * 20f);
                    V2 p = at + V2.FromAngle(an) * d;
                    Ellipse(p, 3f, 2f, CcInk.WithAlpha(0.35f));
                    V2 c = p + new V2(0f, -hz);
                    float r = a * 12f + i;
                    CavTri(c + V2.FromAngle(r) * 4.5f, c + V2.FromAngle(r + 2.3f) * 4f, c + V2.FromAngle(r + 4.1f) * 4f, CcStone);
                }
                // Sparks.
                if (a < 0.5f)
                    for (int i = 0; i < 22; i++)
                    {
                        float h1 = Hash01(seed + i * 1.31f), h2 = Hash01(seed + i * 2.77f), h3 = Hash01(seed + i * 4.13f);
                        float life = 0.5f * (0.45f + 0.55f * h3);
                        if (a > life)
                            continue;
                        float g = a / life, an = h1 * MathX.Tau, sp = 520f * (0.35f + 0.65f * h2), d = sp * life * (g - g * g / 2f), v = sp * (1f - g);
                        V2 dir = V2.FromAngle(an), p = at + dir * d;
                        Line(p, p - dir * Mathf.Max(1.5f, 0.035f * v), 2f * (1f - g * 0.5f), CcEmber.WithAlpha(1f - g));
                    }
            }
        }

        /// The crater left by a slam `a` s ago: cracks, a dark pit, the spent ball and smoke, fading over its last second.
        void DrawCannonCrater(V2 at, float R, float a, int seed)
        {
            float fade = 1f - Mathf.Clamp01((a - (Game.CcCraterT - 1f)) / 1f);
            if (fade <= 0f)
                return;
            float rr = R * 0.42f, grow = EaseOut3(Mathf.Clamp01(a / 0.12f));
            for (int i = 0; i < 9; i++)
            {
                float an = Hash01(seed + i * 3.7f) * MathX.Tau, l = rr * (1.3f + Hash01(seed + i * 9.1f) * 1.3f) * grow;
                V2 s = at + V2.FromAngle(an) * rr * 0.7f, m = at + V2.FromAngle(an + 0.18f) * l * 0.6f, e = at + V2.FromAngle(an - 0.05f) * l;
                Line(s, m, 2f, CcInk.WithAlpha(0.55f * fade));
                Line(m, e, 1.6f, CcInk.WithAlpha(0.45f * fade));
            }
            Ellipse(at, rr * 1.25f, rr * 1.1f, CcPit.WithAlpha(0.45f * fade));
            Ellipse(at, rr, rr * 0.86f, CcPit.WithAlpha(0.8f * fade));
            Arc(at, rr * 0.95f, 2f, CcDust.WithAlpha(0.45f * fade), 28);
            float br = 8f * R / Game.CcRadius;
            Circle(at, br, CcIron.WithAlpha(fade), 16);
            Circle(at + new V2(-br * 0.3f, -br * 0.3f), br * 0.4f, CcIronLit.WithAlpha(0.6f * fade), 10);
            // Smoke lifting off the pit.
            for (int i = 0; i < 6; i++)
            {
                float f = (a - 0.3f) / 1.3f;
                if (f < 0f || f >= 1f)
                    break;
                float an = Hash01(seed + i * 1.9f) * MathX.Tau, d = 20f * (0.4f + 0.6f * Hash01(seed + i * 2.9f)) * f;
                V2 p = at + V2.FromAngle(an) * d + new V2(0f, -55f * f);
                Circle(p, 26f * (0.6f + 0.8f * Hash01(seed + i * 4.3f)) * (0.45f + f * 1.1f), CcSmoke.WithAlpha(0.32f * Mathf.Pow(1f - f, 1.4f) * fade), 14);
            }
        }

        /// The flare and the falling balls, over the arena's contents (WorldRenderer.cs DrawArenaOver).
        void DrawCannonSky()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.CannonCall)
                    continue;
                float age = fx.T;
                if (age > 0.08f && age < Game.CcFlareT + 0.05f)
                {
                    V2 from = CcKnightAt(fx);
                    float f = EaseOut3(Mathf.Clamp01((age - 0.08f) / (Game.CcFlareT - 0.08f)));
                    V2 head = from + (fx.Pos2 - from) * f, tail = from + (fx.Pos2 - from) * Mathf.Max(0f, f - 0.3f);
                    Line(tail, head, 2.5f, CcMark.WithAlpha(0.5f));
                    Circle(head, 2.6f, Rgba.White, 8);
                }
                float sc, ff;
                V2 p = CcBallAt(fx, age, out sc, out ff);
                if (ff <= 0f || ff >= 1f)
                    continue;
                float size = sc * Mathf.Sqrt(CcRadiusOf(fx) / Game.CcRadius);
                for (int i = 13; i >= 1; i--)
                {
                    float qs, qf;
                    V2 q = CcBallAt(fx, age - i * 0.035f, out qs, out qf);
                    if (qf <= 0f)
                        continue;
                    float al = 1f - i / 14f;
                    Circle(q, 9f * qs * (0.7f + 0.3f * al), CcSmoke.WithAlpha(0.3f * al), 12);
                }
                float r = CcBallR * size;
                Circle(p, r + 1.5f, CcInk.WithAlpha(0.7f), 20);
                Circle(p, r, CcIron, 20);
                Circle(p + new V2(-r * 0.35f, -r * 0.35f), r * 0.45f, CcIronLit.WithAlpha(0.55f), 12);
                Ellipse(p, r, r * 0.3f, 0.6f, CcBand.WithAlpha(0.9f), 16);
                Ellipse(p, r * 0.8f, r * 0.15f, 0.6f, CcIron, 14);
                V2 fuse = p + new V2(r * 0.65f, -r * 0.75f);
                Line(p + new V2(r * 0.45f, -r * 0.55f), fuse, 2f, new Rgba(0.23f, 0.16f, 0.1f));
                Circle(fuse, 1.8f + 0.6f * Mathf.Sin(game.AnimT * 50f), CcEmber, 8);
            }
        }

        V2 CcKnightAt(Effect fx)
        {
            Player p;
            return game.Players.TryGetValue((int)(fx.Radius / 1000f + 0.001f), out p) ? p.Disp : fx.Pos;
        }

        /// The glow pass (Juice.cs DrawGlowPass): the flare, the fuse, the slam's flash.
        void DrawCannonGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.CannonCall)
                    continue;
                float age = fx.T, a = age - CcLandOf(fx), R = CcRadiusOf(fx);
                if (age < Game.CcFlareT + 0.1f)
                {
                    float fa = Mathf.Clamp01(age / 0.08f) * (1f - Mathf.Clamp01((age - Game.CcFlareT) / 0.1f));
                    V2 from = CcKnightAt(fx);
                    float f = EaseOut3(Mathf.Clamp01((age - 0.08f) / (Game.CcFlareT - 0.08f)));
                    GlowDisc(from, 26f, CcMark, 0.6f * fa);
                    GlowDisc(from + (fx.Pos2 - from) * f, 18f, CcMark, 0.8f * fa);
                }
                float sc, ff;
                V2 p = CcBallAt(fx, age, out sc, out ff);
                if (ff > 0f && ff < 1f)
                {
                    float r = CcBallR * sc;
                    GlowDisc(p + new V2(r * 0.65f, -r * 0.75f), 8f + 3f * Mathf.Sin(game.AnimT * 50f), CcEmber, 0.9f);
                }
                if (a >= 0f && a < 0.18f)
                    GlowDisc(fx.Pos2, R * 0.85f, CcFlash, 0.6f * (1f - a / 0.18f));
                if (a >= 0f && a < 0.5f)
                    GlowDisc(fx.Pos2, R * 0.5f, CcEmber, 0.35f * (1f - a / 0.5f));
            }
        }
    }
}
