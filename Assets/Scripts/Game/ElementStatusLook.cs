using UnityEngine;

namespace Arena
{
    /// The looks of the Elementalist's earth and light statuses on an enemy: Brittle (Core/Brittle.cs) and Dazzled
    /// (Core/Dazzled.cs). Each has a body overlay (drawn with the other status looks in WorldEnemies.cs) and an icon for the
    /// status row over its head (DrawStatusIcons).
    public partial class ArenaApp
    {
        /// Brittle: dark cracks with glowing amber seams branch across the body, and a dusty tint.
        Rgba DrawBrittle(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var seam = Game.BrittleColor;
            var dark = new Rgba(0.22f, 0.15f, 0.1f, 0.85f);
            float glow = 0.55f + 0.25f * Mathf.Sin(t * 3f + e.Id);
            for (int k = 0; k < 3; k++)  // three cracks from near the middle out to the rim, each with one kink and a fork
            {
                float a = e.Id * 1.37f + k * MathX.Tau / 3f;
                V2 from = pos + V2.FromAngle(a + 1.9f) * r * 0.15f;
                V2 kink = pos + V2.FromAngle(a + 0.25f) * r * 0.55f;
                V2 rim = pos + V2.FromAngle(a - 0.1f) * r * 0.98f;
                V2 fork = pos + V2.FromAngle(a + 0.6f) * r * 0.9f;
                Line(from, kink, 2.4f, dark);
                Line(kink, rim, 2.4f, dark);
                Line(kink, fork, 1.6f, dark);
                Line(from, kink, 1f, seam.WithAlpha(glow));
                Line(kink, rim, 1f, seam.WithAlpha(glow));
            }
            return col.Mix(seam, 0.15f);
        }

        /// Dazzled: four little four-point glints wheel round its head, twinkling, and a warm light tint.
        Rgba DrawDazzled(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var light = Game.DazzleColor;
            for (int k = 0; k < 4; k++)
            {
                float a = t * 2.2f + k * MathX.Tau / 4f + e.Id;
                V2 at = pos + new V2(Mathf.Cos(a) * r * 0.95f, -r * 0.75f + Mathf.Sin(a) * r * 0.3f);
                float s = 3f + 2.2f * Mathf.Abs(Mathf.Sin(t * 6f + k * 1.7f + e.Id));
                DrawGlint(at, s, light);
            }
            return col.Mix(light, 0.18f);
        }

        /// A four-point star: a long cross with a white core.
        void DrawGlint(V2 c, float s, Rgba col)
        {
            Line(c - new V2(s, 0f), c + new V2(s, 0f), 1.8f, col.WithAlpha(0.8f));
            Line(c - new V2(0f, s * 1.3f), c + new V2(0f, s * 1.3f), 1.8f, col.WithAlpha(0.8f));
            Circle(c, 1.3f, Rgba.White, 6);
        }

        /// The status row's Brittle icon: a cracked stone (a hexagon split by a zigzag amber seam).
        void DrawBrittleIcon(V2 c, float size)
        {
            Polygon(c, size * 0.85f, 6, 0.3f, new Rgba(0.55f, 0.45f, 0.36f));
            var seam = Game.BrittleColor;
            V2 a = c + new V2(-size * 0.2f, -size * 0.8f), b = c + new V2(size * 0.15f, -size * 0.15f), m = c + new V2(-size * 0.15f, size * 0.25f), d = c + new V2(size * 0.2f, size * 0.8f);
            Line(a, b, 1.8f, seam);
            Line(b, m, 1.8f, seam);
            Line(m, d, 1.8f, seam);
            Line(b, b + new V2(size * 0.5f, size * 0.05f), 1.2f, seam);
        }

        /// The status row's Dazzled icon: a small sun (a disc and eight rays).
        void DrawDazzleIcon(V2 c, float size)
        {
            var light = Game.DazzleColor;
            for (int k = 0; k < 8; k++)
            {
                V2 d = V2.FromAngle(k * MathX.Tau / 8f);
                Line(c + d * size * 0.5f, c + d * size * (k % 2 == 0 ? 0.95f : 0.78f), 1.4f, light);
            }
            Circle(c, size * 0.38f, light, 10);
            Circle(c, size * 0.18f, Rgba.White, 8);
        }
    }
}
