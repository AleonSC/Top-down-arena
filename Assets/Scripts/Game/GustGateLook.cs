using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Gust Gate's look (GustGate.cs), from its mockup (variant A, Vortex): a ring of wind on the floor, three layers of pale
    /// streaks circling at different speeds with dust motes riding them (a few gold), a dashed rim drifting round and a thin arc
    /// outside draining with its time. It whirls open over 0.25 s and thins out in its last 0.6 s. A boosted player (GustT) trails
    /// three speed lines, a couple of fading afterimages and a mint swirl underfoot, all fading with the boost. With Visual effects
    /// off: the plain ring and its timer, and one speed line. Drawing only, from the replicated zone and GustT.
    public partial class ArenaApp
    {
        static readonly Rgba GustPale = new Rgba(0.86f, 0.98f, 1f), GustMint = new Rgba(0.5f, 0.9f, 0.82f), GustDim = new Rgba(0.4f, 0.64f, 1f), GustGold = Rgba.Gold;

        readonly Dictionary<int, V2> gustLastDisp = new Dictionary<int, V2>();   // per player: where it was drawn last frame
        readonly Dictionary<int, V2> gustHeading = new Dictionary<int, V2>();    // per player: its smoothed way of travel

        void DrawGustGateZone(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float grow = Mathf.Clamp01(age / 0.25f), a = grow * Mathf.Clamp01(z.T / 0.6f);
            float r = z.Radius * (0.6f + 0.4f * grow);
            float left = Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            Arc(z.Pos, r + 14f, 2f, GustDim.WithAlpha(0.35f * a), 40, -MathX.Pi * 0.5f, MathX.Tau * left);  // its time
            if (!fxOn)
            {
                Circle(z.Pos, r, GustMint.WithAlpha(0.08f * a), 40);
                Arc(z.Pos, r, 2.5f, GustPale.WithAlpha(0.8f * a), 48);
                return;
            }

            // the floor: a faint mint wash, stronger toward the edge
            Circle(z.Pos, r * 1.1f, GustMint.WithAlpha(0.05f * a), 44);
            Arc(z.Pos, r - 6f, 14f, GustMint.WithAlpha(0.07f * a), 48);

            // wind streaks: three rings of arcs, each a bright head with a fading tail (drawn as stepped arcs)
            float spin = age * 3.2f;
            DrawGustLayer(z.Pos, r * 0.86f, spin * 1.0f, 6, 1.6f, a, t);
            DrawGustLayer(z.Pos, r * 1.0f, spin * 1.35f, 7, 2.6f, a, t);
            DrawGustLayer(z.Pos, r * 1.12f, spin * 0.8f, 5, 1.6f, a, t);

            // dust motes riding the wind
            for (int i = 0; i < 28; i++)
            {
                float seed = i * 12.9898f;
                float rr = r * (0.55f + 0.6f * Mathf.Repeat(Mathf.Sin(seed) * 43758.5f, 1f));
                float sp = 1.5f + 2.5f * Mathf.Repeat(Mathf.Sin(seed * 1.7f) * 24634.6f, 1f);
                float an = i * 2.39996f + age * sp;
                Rgba mc = i % 7 == 0 ? GustGold : GustPale;
                Circle(z.Pos + V2.FromAngle(an) * rr, 1.3f, mc.WithAlpha(0.75f * a), 6);
            }

            // the rim: a dashed line drifting round
            const int dashes = 22;
            for (int k = 0; k < dashes; k++)
                Arc(z.Pos, r, 1f, GustPale.WithAlpha(0.3f * a), 3, -t * 0.45f + k * MathX.Tau / dashes, MathX.Tau / dashes * 0.35f);
        }

        void DrawGustLayer(V2 c, float rad, float spin, int n, float width, float a, float t)
        {
            for (int i = 0; i < n; i++)
            {
                float len = 0.55f + 0.25f * Mathf.Sin(t * 2f + i);
                float head = spin + i * MathX.Tau / n + rad * 0.04f;
                float wob = Mathf.Sin(t * 3f + i) * 2f;
                const int steps = 5;
                for (int s = 0; s < steps; s++)  // tail to head, brighter each step
                {
                    float k = (s + 1f) / steps;
                    Arc(c, rad + wob, width, GustPale.WithAlpha(0.85f * a * k * k), 4, head - len + len * s / steps, len / steps);
                }
            }
        }

        /// A player with a Gust Gate boost: speed lines behind, afterimages, a swirl underfoot.
        void DrawGustBoost(Player p, float t)
        {
            V2 last;
            if (!gustLastDisp.TryGetValue(p.Id, out last))
                last = p.Disp;
            gustLastDisp[p.Id] = p.Disp;
            if (p.GustT <= 0f || p.Downed)
                return;
            V2 step = p.Disp - last, head;
            if (!gustHeading.TryGetValue(p.Id, out head) || head.Length < 0.1f)
                head = V2.FromAngle(p.Aim);
            if (step.Length > 0.3f)
                head = (head * 0.8f + step.Normalized() * 0.2f).Normalized();
            gustHeading[p.Id] = head;

            float k = Mathf.Clamp01(p.GustT / Game.GgFade);
            float body = p.Def.BodyR;
            V2 side = new V2(-head.Y, head.X);
            if (!fxOn)
            {
                Line(p.Disp - head * (body + 4f), p.Disp - head * (body + 26f), 2f, GustPale.WithAlpha(0.7f * k));
                return;
            }
            for (int i = 1; i <= 2; i++)  // afterimages
                Circle(p.Disp - head * (i * 10f), body, GustPale.WithAlpha(0.12f * k / i), 20);
            for (int i = -1; i <= 1; i++)  // speed lines
            {
                float len = 18f + 12f * k + Mathf.Sin(t * 20f + i) * 4f;
                V2 o = p.Disp + side * (i * 7f) - head * (body + 4f);
                Line(o, o - head * len, 1.6f, GustPale.WithAlpha(0.7f * k));
            }
            float sw = t * 8f;  // the swirl underfoot
            Arc(p.Disp, body + 6f, 2f, GustMint.WithAlpha(0.6f * k), 10, sw, 2f);
            Arc(p.Disp, body + 6f, 2f, GustMint.WithAlpha(0.6f * k), 10, sw + MathX.Pi, 2f);
        }
    }
}
