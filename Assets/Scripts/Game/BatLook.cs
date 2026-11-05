using UnityEngine;

namespace Arena
{
    /// How a Bat Swarm bat looks (look B, "Belfry burst", of Docs/Mockups/bat-swarm-rework.html): a dark body with flapping
    /// wings and a light edge, a speed streak behind it while it dives, and feathers dropping off it as its time runs out.
    public partial class ArenaApp
    {
        static readonly Rgba BatBody = new Rgba(0.17f, 0.13f, 0.31f), BatEdge = new Rgba(0.79f, 0.66f, 1f);

        void DrawBat(Minion m, float t)
        {
            float left = m.T >= 0f ? m.T : 99f;
            float fade = MathX.Clamp01(left);  // the last second: wings fold and it dissolves
            float flap = Mathf.Sin(t * 30f + m.Id * 1.7f) * (0.4f + 0.6f * fade);
            V2 dir = V2.FromAngle(m.Aim), side = new V2(-dir.Y, dir.X);
            float s = 1.1f;
            bool dive = m.Duration - m.T > 1.2f;
            if (dive && fade > 0f)  // speed streak
                Line(m.Disp - dir * 22f, m.Disp - dir * 4f, 2f, BatEdge.WithAlpha(0.3f * fade));
            for (int k = -1; k <= 1; k += 2)  // two wings: a tip swept back, a notch, a second point
            {
                V2 root = m.Disp - dir * (1f * s);
                V2 tip = root - dir * (11f * s) + side * (k * (10f + 7f * flap) * s);
                V2 mid = root - dir * (4f * s) + side * (k * (6f + 4f * flap) * s);
                V2 end = root - dir * (15f * s) + side * (k * 3f * s);
                Line(root, mid, 4f * s, BatBody.WithAlpha(fade));
                Line(mid, tip, 3f * s, BatBody.WithAlpha(fade));
                Line(tip, end, 2.5f * s, BatBody.WithAlpha(fade));
                Line(mid, tip, 1.2f, BatEdge.WithAlpha(0.9f * fade));
            }
            Circle(m.Disp, 4.6f * s, BatBody.WithAlpha(fade), 10);
            Arc(m.Disp, 4.6f * s, 1.3f, BatEdge.WithAlpha(fade), 10);
            Circle(m.Disp + dir * 2.5f + side * 1.6f, 1f, new Rgba(1f, 0.82f, 0.88f, fade), 5);
            Circle(m.Disp + dir * 2.5f - side * 1.6f, 1f, new Rgba(1f, 0.82f, 0.88f, fade), 5);
            if (left < 1f)  // a feather or two drifting off as it dissolves
                for (int k = 0; k < 2; k++)
                {
                    float q = MathX.Fmod(t * 2f + k * 0.5f + m.Id * 0.13f, 1f);
                    V2 at = m.Disp + side * ((k == 0 ? 1f : -1f) * q * 14f) - dir * (q * 8f);
                    Line(at, at + V2.FromAngle(q * 6f + k) * 3f, 2f, BatEdge.WithAlpha(0.7f * (1f - q)));
                }
        }
    }
}
