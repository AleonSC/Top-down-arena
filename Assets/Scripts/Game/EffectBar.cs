using UnityEngine;

namespace Arena
{
    /// The effect bar: a thin bar over an enemy's health bar that isn't there until a timed status lands on it, then drains
    /// as the status runs out. One row per status (only Wet so far: Enemy.WetLeft/WetPeak, replicated in tenths of a second).
    /// Returns how many rows it drew, so the status icons sit above them. Called from DrawEnemy (WorldEnemies.cs).
    public partial class ArenaApp
    {
        static readonly Rgba WetBar = new Rgba(0.3f, 0.65f, 1f);

        int DrawEffectBar(Enemy e, V2 pos, float r, bool healthBar)
        {
            if (!e.Wet || e.WetPeak <= 0f || e.Spawning)
                return 0;
            float w = Mathf.Max(30f, r * 2f), h = 3f;
            V2 at = pos + new V2(-w / 2f, -r - (healthBar ? 15f : 10f));
            FillRect(at.X, at.Y, w, h, new Rgba(0f, 0f, 0f, 0.6f));
            FillRect(at.X, at.Y, w * MathX.Clamp01(e.WetLeft / e.WetPeak), h, WetBar);
            return 1;
        }
    }
}
