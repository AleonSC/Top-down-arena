using UnityEngine;

namespace Arena
{
    /// Legion (the Summoner DPS ultimate): "Blood Moon", look D of Docs/Mockups/legion-rework.html.
    /// A red moon rises over the arena and tints it for as long as the pack lasts, then sets. Drawn from the replicated
    /// LegionT alone. Reduced effects: no tint or glow, just the moon.
    public partial class ArenaApp
    {
        const float LegionTime = 10f;

        void DrawBloodMoon(Player p, float t)
        {
            float age = LegionTime - p.LegionT;
            float k = Mathf.Clamp01(age / 1.2f) * Mathf.Clamp01(p.LegionT / 0.8f);
            if (k <= 0f)
                return;
            if (!fxReduced)
                FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.47f, 0.04f, 0.08f, 0.2f * k));
            V2 moon = new V2(Game.Width - 120f, 90f - 40f * k + 60f * (1f - k));
            if (!fxReduced)
                FanGradient(moon, 130f, new Rgba(1f, 0.35f, 0.3f, 0.55f * k), new Rgba(1f, 0.25f, 0.25f, 0f), 32);
            Circle(moon, 34f, new Rgba(1f, 0.43f, 0.4f, k), 32);
            Circle(moon + new V2(-12f, -6f), 8f, new Rgba(0.75f, 0.2f, 0.2f, 0.5f * k), 12);
            Circle(moon + new V2(10f, 12f), 5f, new Rgba(0.75f, 0.2f, 0.2f, 0.5f * k), 10);
        }
    }
}
