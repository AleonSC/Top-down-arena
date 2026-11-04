using UnityEngine;

namespace Arena
{
    /// Bullet Rain (the Elementalist DPS ultimate): "Downpour", look D of Docs/Mockups/bullet-rain-elementalist.html.
    /// The whole arena darkens and rains (soft, harmless drops with ripples); inside the zone each drop falls soft, flashes
    /// halfway down as it hardens, and lands as a needle with a ring and splash. The drops are drawn from the clock alone,
    /// so they cost nothing on the network. Reduced effects: no darkening, no lightning flash, fewer drops.
    public partial class ArenaApp
    {
        static readonly Rgba RainSoft = new Rgba(0.59f, 0.76f, 0.94f), RainSharp = new Rgba(0.92f, 0.98f, 1f), RainBlue = new Rgba(0.47f, 0.69f, 0.94f);
        const float RainSlant = 0.22f, RainHeight = 430f;

        void DrawBulletRain(Zone z, float t)
        {
            float R = z.Radius, age = z.Duration - z.T;
            float fade = Mathf.Clamp01(age / 0.6f) * Mathf.Clamp01(z.T / 0.9f);
            if (fade <= 0f)
                return;
            // Over the whole arena: a blue tint, soft rain, ripples and (now and then) a flash of lightning.
            if (!fxReduced)
            {
                FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.16f, 0.27f, 0.47f, 0.16f * fade));
                float flash = MathX.Fmod(t, 2.1f);
                if (flash < 0.12f)
                    FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.82f, 0.88f, 1f, 0.14f * (1f - flash / 0.12f) * fade));
            }
            int ambient = fxReduced ? 40 : 110;
            for (int k = 0; k < ambient; k++)
            {
                float period = 0.45f + 0.25f * Hash01(k + 0.3f), u = t / period + Hash01(k + 0.7f);
                float cyc = (float)System.Math.Floor(u), ph = u - cyc, seed = k * 17.3f + cyc * 3.1f;
                V2 land = new V2(Hash01(seed) * Game.Width, Hash01(seed + 1.5f) * Game.Height);
                float h = RainHeight * (1f - ph);
                V2 head = land + new V2(RainSlant * h, -h);
                if (ph < 0.9f)
                    Line(head, head + new V2(RainSlant, -1f).Normalized() * 24f, 1.4f, RainSoft.WithAlpha(0.22f * fade));
                else
                    Arc(land, 2f + (ph - 0.9f) * 70f, 1f, RainSoft.WithAlpha(0.4f * (1f - ph) * 10f * fade), 10);
            }
            // The zone: a disc of rain with a dashed edge.
            FanGradient(z.Pos, R, RainBlue.WithAlpha(0.03f * fade), RainBlue.WithAlpha(0.2f * fade), 32);
            for (int k = 0; k < 20; k++)
                Arc(z.Pos, R, 2.5f, RainBlue.WithAlpha(0.85f * fade), 3, k * MathX.Tau / 20f - t * 0.6f, MathX.Tau / 40f * 1.4f);
            Arc(z.Pos, R + 12f, 1f, RainBlue.WithAlpha(0.3f * fade), 48);
            // The needles.
            int drops = fxReduced ? 30 : 70;
            for (int k = 0; k < drops; k++)
            {
                float period = 0.5f + 0.3f * Hash01(k + 40.3f), u = t / period + Hash01(k + 40.7f);
                float cyc = (float)System.Math.Floor(u), ph = u - cyc, seed = k * 29.7f + cyc * 5.3f + 100f;
                float rad = R * Mathf.Sqrt(Hash01(seed)) * 0.96f, an = Hash01(seed + 2.5f) * MathX.Tau;
                V2 land = z.Pos + V2.FromAngle(an) * rad;
                float h = RainHeight * (1f - ph);
                V2 head = land + new V2(RainSlant * h, -h);
                V2 back = new V2(RainSlant, -1f).Normalized();
                if (ph < 0.5f)  // still water
                    Line(head, head + back * 26f, 1.6f, RainSoft.WithAlpha(0.4f * fade));
                else if (ph < 0.9f)  // hardened: a needle with a bright core
                {
                    Line(head, head + back * 62f, 5f, RainBlue.WithAlpha(0.25f * fade));
                    Line(head, head + back * 34f, 1.8f, RainSharp.WithAlpha(0.95f * fade));
                    if (ph < 0.54f)  // the flash as it hardens
                    {
                        float g = 1f - (ph - 0.5f) / 0.04f;
                        Line(head - new V2(12f * g, 0f), head + new V2(12f * g, 0f), 1.5f, Rgba.White.WithAlpha(g * fade));
                        Line(head - new V2(0f, 12f * g), head + new V2(0f, 12f * g), 1.5f, Rgba.White.WithAlpha(g * fade));
                    }
                }
                else  // landed: a ring and a splash of droplets
                {
                    float q = (ph - 0.9f) / 0.1f;
                    Arc(land, 2f + 20f * q, 2f * (1f - q) + 0.5f, RainSoft.WithAlpha(0.9f * (1f - q) * fade), 16);
                    for (int s = 0; s < 4; s++)
                    {
                        float sa = -MathX.Pi * 0.5f + (Hash01(seed + s) - 0.5f) * 2.2f, sp = 18f + 22f * Hash01(seed + s + 7f);
                        Circle(land + V2.FromAngle(sa) * sp * q + new V2(0f, 14f * q * q), 1.6f * (1f - q), RainSharp.WithAlpha((1f - q) * fade), 6);
                    }
                }
            }
        }
    }
}
