using UnityEngine;

namespace Arena
{
    /// Holy Lance Rain (Core/HolyLanceRain.cs), the look of Docs/Mockups/holy-lance-rain.html, drawn from the effects' ages alone so
    /// every screen draws the same rain:
    ///   FxKind.HolyLanceRain (Radius = the caster's id): a faint gold disc and a turning dashed rim round wherever the caster is
    ///   FxKind.HolyLanceSpear (Pos = the spot, Color.A = the glow's time / 10): the shrinking gold warning ring; the spear dropping
    ///     out of the sky with its trail (DrawLanceRainSky, over the arena's contents); the white flash, shock ring and gold cracks;
    ///     then the mint healing glow with its rim, rising motes and the spear left standing in it, fading
    ///   DrawLanceRainGlow (the glow pass): the falling spear's light, the flash and the glows' soft light
    /// Visual effects off drops the trail, motes and cracks; Reduced effects drops the flash.
    public partial class ArenaApp
    {
        static readonly Rgba LrGold = new Rgba(1f, 0.84f, 0.42f), LrPale = new Rgba(1f, 0.96f, 0.86f), LrMint = new Rgba(0.49f, 1f, 0.69f);
        const float LrFallT = 0.22f, LrSky = 300f, LrSpearLen = 46f, LrBurstT = 0.45f;

        static float LrGlowTime(Effect fx) => fx.Color.A > 0.01f ? fx.Color.A * 10f : Game.LrGlowT;
        static int LrSeed(Effect fx) => ((int)(fx.Pos.X * 7.1f) ^ (int)(fx.Pos.Y * 13.3f)) & 0xffff;

        /// The glow's strength: a quick rise after the landing, a fade at its end.
        static float LrGlowAlpha(Effect fx)
        {
            float a = fx.T - Game.LrWarn, life = LrGlowTime(fx);
            if (a < 0f)
                return 0f;
            return Mathf.Clamp01(a / 0.25f) * Mathf.Clamp01((life + Game.LrFade - a) / (Game.LrFade + 0.2f));
        }

        /// A spear of light, point down at `tip`, `s` times full size.
        void DrawLanceSpear(V2 tip, float s, float a)
        {
            if (a <= 0.01f)
                return;
            float L = LrSpearLen * s;
            Line(tip + new V2(0f, -10f * s), tip + new V2(0f, -L), 3.2f * s, LrPale.WithAlpha(a));
            Quad(tip, tip + new V2(-5f * s, -10f * s), tip + new V2(0f, -14f * s), tip + new V2(5f * s, -10f * s), new Rgba(1f, 0.91f, 0.64f, a));
            Line(tip + new V2(-6f * s, -L * 0.62f), tip + new V2(6f * s, -L * 0.62f), 2.5f * s, LrGold.WithAlpha(a));
            Circle(tip + new V2(0f, -L), 2.4f * s, LrGold.WithAlpha(a), 8);
        }

        void DrawLanceRainGround(Effect fx)
        {
            if (fx.Kind == FxKind.HolyLanceRain)
            {
                Player p;
                if (!game.Players.TryGetValue((int)fx.Radius, out p))
                    return;
                float r = fx.Color.A > 0.05f ? fx.Color.A * 1000f : Game.LrRadius;
                float a = Mathf.Clamp01(fx.T / 0.3f) * Mathf.Clamp01((fx.Dur - fx.T) / 0.4f);
                Circle(p.Disp, r, LrGold.WithAlpha(0.05f * a), 56);
                const int dashes = 36;
                float turn = game.AnimT * 0.25f;
                for (int i = 0; i < dashes; i += 1)
                    Arc(p.Disp, r, 2f, LrGold.WithAlpha(0.5f * a), 3, turn + MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
                return;
            }

            float age = fx.T;
            V2 c = fx.Pos;
            int seed = LrSeed(fx);
            if (age < Game.LrWarn)
            {
                float k = age / Game.LrWarn;
                Circle(c, Game.LrHit, LrGold.WithAlpha(0.12f * k), 24);
                Arc(c, Game.LrHit * (1.6f - 0.6f * k), 1.6f, LrGold.WithAlpha(0.3f + 0.6f * k), 28);
                return;
            }

            float land = age - Game.LrWarn, ga = LrGlowAlpha(fx), pulse = 1f + 0.08f * Mathf.Sin(land * 5f + seed);
            if (ga > 0f)
            {
                Circle(c, Game.LrGlowR * pulse, LrMint.WithAlpha(0.16f * ga), 24);
                Circle(c, Game.LrGlowR * 0.55f * pulse, LrMint.WithAlpha(0.14f * ga), 18);
                Arc(c, Game.LrGlowR * pulse, 1.3f, new Rgba(0.75f, 1f, 0.85f, 0.55f * ga), 28);
                if (fxOn)
                    for (int m = 0; m < 3; m++)  // motes rising off the glow
                    {
                        float ph = (land * 0.7f + m / 3f + CavHash(seed + m) ) % 1f;
                        V2 mp = c + new V2((CavHash(seed + m * 7) - 0.5f) * Game.LrGlowR * 1.2f, -ph * 26f);
                        Circle(mp, 1.4f, new Rgba(0.8f, 1f, 0.88f, ga * (1f - ph) * 0.9f), 6);
                    }
            }
            if (land < LrBurstT)
            {
                float k = land / LrBurstT;
                Arc(c, Game.LrHit * (0.4f + 1.1f * k), 3f * (1f - k) + 1f, LrPale.WithAlpha(1f - k), 32);
                if (fxOn)
                {
                    float grow = Mathf.Min(1f, k * 3f);
                    for (int i = 0; i < 6; i++)
                    {
                        V2 d = V2.FromAngle(i * 1.047f + CavHash(seed + i) * 0.6f);
                        Line(c, c + d * (Game.LrHit * 0.85f * grow), 1.3f, LrGold.WithAlpha(0.8f * (1f - k)));
                    }
                }
            }
            DrawLanceSpear(c, 0.75f, Mathf.Clamp01(1f - land / 1.4f));  // left standing in the glow, fading
        }

        /// The spears dropping in and the landing flash, over the arena's contents.
        void DrawLanceRainSky()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.HolyLanceSpear)
                    continue;
                float age = fx.T;
                float fk = (age - (Game.LrWarn - LrFallT)) / LrFallT;
                if (fk > 0f && fk < 1f)
                {
                    float lift = (1f - fk * fk) * LrSky;
                    V2 tip = fx.Pos + new V2(0f, -lift);
                    if (fxOn)
                        for (int i = 0; i < 4; i++)  // the trail
                        {
                            float u0 = i / 4f, u1 = (i + 1) / 4f;
                            Line(tip + new V2(0f, -LrSpearLen - u0 * 90f), tip + new V2(0f, -LrSpearLen - u1 * 90f), 5f - i, LrGold.WithAlpha(0.55f * (1f - u0)));
                        }
                    DrawLanceSpear(tip, 1f, 1f);
                }
                float land = age - Game.LrWarn;
                if (land >= 0f && land < 0.08f && !fxReduced)
                    Circle(fx.Pos, Game.LrHit * 0.55f, LrPale.WithAlpha(0.7f * (1f - land / 0.08f)), 18);
            }
        }

        void DrawLanceRainGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.HolyLanceSpear)
                    continue;
                float age = fx.T, land = age - Game.LrWarn;
                float fk = (age - (Game.LrWarn - LrFallT)) / LrFallT;
                if (fk > 0f && fk < 1f)
                    GlowDisc(fx.Pos + new V2(0f, -(1f - fk * fk) * LrSky - LrSpearLen * 0.5f), 34f, LrGold, 0.5f);
                if (land >= 0f && land < 0.3f)
                    GlowDisc(fx.Pos, Game.LrHit * 1.4f, LrPale, 0.7f * (1f - land / 0.3f));
                float ga = LrGlowAlpha(fx);
                if (ga > 0f)
                    GlowDisc(fx.Pos, Game.LrGlowR * 1.7f, LrMint, 0.38f * ga);
            }
        }
    }
}
