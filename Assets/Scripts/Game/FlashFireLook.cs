using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Summoner's Flash Fire (Core/FlashFire.cs), take C "Pyre" of Docs/Mockups/flash-fire.html, drawn from the one
    /// `FxKind.FlashFire` effect. The kindle: a warm wash over the circle, six rune arcs turning round its edge and growing,
    /// embers streaming in to the caster and a glow at their feet. The flash: a white-hot ring rolls out to the edge over an
    /// orange wash. The pyre: a scorched band at the edge with a red glow and a hot line, flame tongues bursting up along it
    /// (overshoot, then flicker), embers rising inside; the last 0.5 s the flames sink and the band cools.
    ///
    /// Tongues stand up the screen, so they're split for depth like Bamboo Grove's stalks: the far half before the enemies,
    /// the near half after them (`DrawFlashFireBack` / `DrawFlashFireFront`); the wash, runes, ring, band and embers go
    /// under everything (`DrawFlashFireFloor`).
    public partial class ArenaApp
    {
        static readonly Rgba FfChar = new Rgba(24 / 255f, 10 / 255f, 6 / 255f), FfEmber = new Rgba(1f, 0.6f, 0.2f);

        static float FfHash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f, 1f);
        static float FfSeed(Effect fx) => Mathf.Repeat(fx.Pos.X * 0.37f + fx.Pos.Y * 1.91f, 1000f);

        /// How lit the pyre is: 0 before the ring arrives, 1 standing, down to 0 over the fade.
        static float FfPyreK(Effect fx)
        {
            float since = fx.T, stand = Game.FfPyreStart + fx.Pos2.Y;
            if (since < Game.FfPyreStart)
                return 0f;
            return since < stand ? 1f : Mathf.Clamp01(1f - (since - stand) / Game.FfFade);
        }

        static int FfTongues(Effect fx) => Mathf.Max(12, Mathf.RoundToInt(fx.Radius * MathX.Tau / 9f));

        /// Under everything: the wash, runes, embers in, the rolling ring, the scorched band and embers rising.
        void DrawFlashFireFloor(float t)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.FlashFire)
                    DrawFfFloor(fx, t);
        }

        void DrawFfFloor(Effect fx, float t)
        {
            float since = fx.T, R = fx.Radius, s = R / Game.FfRadius, seed = FfSeed(fx);
            V2 c = fx.Pos;
            if (since < Game.FfWindup)  // the kindle
            {
                float k = since / Game.FfWindup;
                Circle(c, R, FireOrange.WithAlpha(0.05f + 0.07f * k), 48);
                for (int i = 0; i < 6; i++)
                    Arc(c, R, 3f, FireYellow.WithAlpha(0.3f + 0.6f * k), 10, since * 4f + i * MathX.Tau / 6f, 0.15f + 0.5f * k);
                V2 feet = c;
                Player p;
                if (game.Players.TryGetValue((int)fx.Pos2.X, out p))
                    feet = p.Disp;
                for (int i = 0; i < 28; i++)
                {
                    float ph = Mathf.Repeat(FfHash(seed + i) + k * 1.6f, 1f), a = FfHash(seed + i * 3.3f) * MathX.Tau + ph * 1.2f;
                    V2 at = feet + V2.FromAngle(a) * (R * (1f - ph) * (0.6f + 0.4f * FfHash(seed + i * 7.1f)));
                    Circle(at, 1.6f + 1.6f * ph, FfEmber.WithAlpha(0.8f * k), 6);
                }
                SoftGlow(feet, 22f + 30f * k, FireYellow, 0.5f + 0.4f * k);
            }
            float roll = (since - Game.FfWindup) / Game.FfRoll;
            if (roll >= 0f && roll < 1.6f)  // the flash: a white-hot ring rolling out over an orange wash
            {
                float front = Game.FfFrontAt(since, R), q = Mathf.Clamp01(roll), fade = roll < 1f ? 1f : 1f - (roll - 1f) / 0.6f;
                Circle(c, front, FireOrange.WithAlpha(0.22f * (1f - q * 0.6f) * fade), 48);
                Arc(c, front, 16f * s, FireOrange.WithAlpha(0.45f * fade), 56);
                Arc(c, front, 6f * s, FireWhite.WithAlpha(0.95f * fade), 56);
            }
            float pk = FfPyreK(fx);
            if (pk <= 0f)
                return;
            float grow = Mathf.Clamp01((since - Game.FfPyreStart) / 0.2f);
            Circle(c, R - 6f * s, FfChar.WithAlpha(0.22f * pk), 48);  // scorched floor, warm light at the edge
            Circle(c, R - 24f * s, FireOrange.WithAlpha(0.05f * pk), 44);
            Arc(c, R, 22f * s, FfChar.WithAlpha(0.7f * pk * grow), 64);
            Arc(c, R, 9f * s, FireRed.WithAlpha((0.55f + 0.15f * Mathf.Sin(t * 6f)) * pk * grow), 64);
            Arc(c, R, 2.5f * s, FireYellow.WithAlpha((0.75f + 0.25f * Mathf.Sin(t * 11f)) * pk * grow), 64);
            for (int i = 0; i < 26; i++)  // embers rising inside
            {
                float ph = Mathf.Repeat(FfHash(seed + i * 1.9f) + t * 0.45f, 1f), a = FfHash(seed + i * 5.3f) * MathX.Tau;
                V2 at = c + V2.FromAngle(a) * (Mathf.Sqrt(FfHash(seed + i * 8.7f)) * R * 0.9f) + new V2(Mathf.Sin(t * 2f + i) * 3f, -ph * 36f * s);
                Circle(at, 1.8f, FfEmber.WithAlpha(0.75f * pk * (1f - ph)), 6);
            }
        }

        /// The far half of the pyre's tongues, before the enemies.
        void DrawFlashFireBack(float t) => DrawFfTongues(true, t);

        /// The near half, after the enemies.
        void DrawFlashFireFront(float t) => DrawFfTongues(false, t);

        void DrawFfTongues(bool back, float t)
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.FlashFire)
                    continue;
                float pk = FfPyreK(fx);
                if (pk <= 0f)
                    continue;
                float since = fx.T, R = fx.Radius, s = R / Game.FfRadius, seed = FfSeed(fx);
                int n = FfTongues(fx);
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * MathX.Tau + FfHash(seed + i * 2.1f) * 0.05f;
                    V2 b = fx.Pos + V2.FromAngle(a) * R;
                    if ((b.Y < fx.Pos.Y) != back)  // up the screen: behind the enemies
                        continue;
                    float burst = FlameBurst((since - Game.FfPyreStart - FfHash(seed + i) * 0.08f) / 0.22f);
                    if (burst <= 0.02f)
                        continue;
                    float h = (20f + 12f * FfHash(seed + i * 4.4f)) * s * burst * pk * (0.85f + 0.15f * Mathf.Sin(t * 13f + i * 1.3f));
                    FlameTongue(b, a, h, 6.5f * s * (0.6f + 0.4f * pk), t, i);
                }
            }
        }
    }
}
