using UnityEngine;

namespace Arena
{
    /// A steam cloud (Core/Steam.cs, ZoneKind.Steam): it bursts out over a quarter second, then white wisps keep rising through it
    /// and fading at the top, over a faint warm glow where the fire met the water; a soft rim marks its reach and it thins out
    /// over its last second.
    public partial class ArenaApp
    {
        static readonly Rgba SteamWarm = new Rgba(1f, 0.72f, 0.55f);

        void DrawSteam(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float grow = Mathf.Clamp01(age / 0.25f), fade = Mathf.Clamp01(z.T / 1f);
            float r = z.Radius * (0.35f + 0.65f * grow);
            var white = Game.SteamColor;
            Circle(z.Pos, r, white.WithAlpha(0.1f * fade), 36);
            Circle(z.Pos, r * 0.45f, SteamWarm.WithAlpha(0.1f * fade * (0.7f + 0.3f * Mathf.Sin(t * 5f + z.Id))), 20);  // the heat at its heart
            int wisps = fxOn ? 10 : 5;
            for (int k = 0; k < wisps; k++)  // wisps rising through it, swelling and fading as they go up
            {
                float f = (t * 0.55f + k / (float)wisps + z.Id * 0.37f) % 1f;
                float x = Mathf.Sin(z.Id * 1.9f + k * 2.4f) * r * 0.6f + 5f * Mathf.Sin(t * 2.2f + k);
                V2 at = z.Pos + new V2(x, r * 0.45f - f * r * 1.15f);
                Circle(at, r * (0.18f + 0.22f * f), white.WithAlpha(0.2f * fade * Mathf.Sin(f * MathX.Pi)), 16);
            }
            for (int k = 0; k < 12; k++)  // a soft, broken rim: where it scalds
                Arc(z.Pos, r, 1.5f, white.WithAlpha(0.28f * fade), 6, k * MathX.Tau / 12f + t * 0.3f, MathX.Tau / 24f);
        }
    }
}
