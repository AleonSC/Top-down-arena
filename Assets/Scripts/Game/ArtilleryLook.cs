using UnityEngine;

namespace Arena
{
    /// Artillery Assault (the DPS Soldier's ultimate, once Bullet Storm) while it's active: a heat aura and ring round the
    /// soldier, orbiting sparks, speed streaks and a flickering muzzle flash at the barrels. Reduced effects: the aura and
    /// ring only, steady. (Mockup: Docs/Mockups/bullet-rain.html, look A, "Storm Surge".)
    public partial class ArenaApp
    {
        static readonly Rgba ArtilleryOrange = new Rgba(1f, 0.59f, 0.27f);
        static readonly Rgba ArtilleryHot = new Rgba(1f, 0.88f, 0.5f);

        void DrawArtillery(Player p, float t)
        {
            if (p.Downed)
                return;
            float r = p.Def.BodyR;
            float lastSecond = p.UltT < 1f ? (MathX.Fmod(t, 0.1f) < 0.05f ? 1f : 0.45f) : 1f;  // flickers as it runs out
            Circle(p.Disp, r + 30f, ArtilleryOrange.WithAlpha(0.14f * lastSecond), 24);
            Arc(p.Disp, r + 12f + (fxReduced ? 0f : Mathf.Sin(t * 18f) * 2.5f), 2f, ArtilleryOrange.WithAlpha(0.6f * lastSecond), 28);
            for (int k = 0; k < 3; k++)  // orbiting sparks
                Arc(p.Disp, r + 7f + k * 4f, 2f, ArtilleryOrange.WithAlpha((0.8f - k * 0.2f) * lastSecond), 12, t * (8f + k * 3f), 1.2f);
            if (fxReduced)
                return;
            // Speed streaks trailing back from the soldier.
            for (int k = 0; k < 8; k++)
            {
                float a = p.Aim + MathX.Pi + (k - 3.5f) * 0.32f;
                float phase = MathX.Fmod(t * 2.2f + k * 0.37f, 1f);
                V2 from = p.Disp + V2.FromAngle(a) * (r + 12f + phase * 34f);
                Line(from, from + V2.FromAngle(a) * 14f, 2f, ArtilleryHot.WithAlpha(0.5f * (1f - phase) * lastSecond));
            }
            // Muzzle flash at both barrels: a glow and a star with randomly long spikes, re-rolled every 0.03 s.
            int seed = Mathf.FloorToInt(t / 0.03f);
            for (int side = -1; side <= 1; side += 2)
            {
                V2 tip = p.Disp + V2.FromAngle(p.Aim) * (r + 24f) + V2.FromAngle(p.Aim + MathX.Pi * 0.5f) * (side * 7f);
                Circle(tip, 14f, ArtilleryHot.WithAlpha(0.55f), 12);
                for (int k = 0; k < 5; k++)
                {
                    float h = Mathf.Abs(Mathf.Sin((seed + side * 7 + k * 13) * 12.9898f) * 43758.5453f) % 1f;
                    float a = p.Aim + (k - 2f) * 0.5f + (h - 0.5f) * 0.2f;
                    Line(tip, tip + V2.FromAngle(a) * (10f + h * 18f), 3f, ArtilleryHot.WithAlpha(0.85f));
                }
            }
        }
    }
}
