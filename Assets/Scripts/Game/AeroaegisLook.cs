using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Elementalist tank's Aeroaegis in style B, "Gust shell" (Docs/Mockups/aeroaegis.html): a pale cloud wall ringing
    /// the protected player under two counter-rotating dashed air rings, with leaves, twigs and dust orbiting inside. It spins
    /// up over 0.3 s, thins out for the last 0.45 s, and a rim round the outside drains with its time. Where an enemy is
    /// pressed against the wall the wall puffs out and flares white. With Visual effects off it is the plain spinning hexagon,
    /// at the vortex's real size. Drawing only: everything comes from the replicated AegisT and the enemies this screen has.
    public partial class ArenaApp
    {
        static readonly Rgba AirPale = new Rgba(0.84f, 1f, 0.97f), AirMint = new Rgba(0.5f, 0.89f, 0.82f);
        const int AegisBuff = 5;  // its place in Buffs (AbilityLooks.cs); the gust shell's own rim shows its time

        readonly Dictionary<int, Vector2> aegisSeen = new Dictionary<int, Vector2>();  // per player: the timer at its cast and last frame

        void DrawAeroaegis(Player p, float t)
        {
            float R = Game.AegisRadius;
            V2 c = p.Disp;
            if (!fxOn)
            {
                for (int k = 0; k < 6; k++)
                    Line(c + V2.FromAngle(t * 3f + MathX.Tau * k / 6f) * R, c + V2.FromAngle(t * 3f + MathX.Tau * (k + 1) / 6f) * R, 3f, new Rgba(0.8f, 0.5f, 1f, 0.85f));
                return;
            }
            aegisSeen.TryGetValue(p.Id, out var seen);
            float peak = seen.x;
            if (p.AegisT > seen.y + 0.5f || peak <= 0f)
                peak = Mathf.Max(p.AegisT, 3f);  // a fresh cast (3 s, or 4.5 s mastered; seen late, it counts as 3 s)
            aegisSeen[p.Id] = new Vector2(peak, p.AegisT);
            float age = Mathf.Max(0f, peak - p.AegisT);
            float a = Mathf.Clamp01(p.AegisT / 0.45f);
            float r = Mathf.Max(16f, R * EaseOut3(Mathf.Clamp01(age / 0.3f)));
            float rem = Mathf.Clamp01(p.AegisT / Mathf.Max(0.01f, peak));

            // where an enemy is pressed against the wall
            float hot = 0f;
            foreach (var e in game.Enemies.Values)
            {
                V2 off = e.Disp - c;
                float d = off.Length;
                if (d > R + e.Radius + 8f)
                    continue;
                hot = 1f;
                float ang = Mathf.Atan2(off.Y, off.X);
                Arc(c, r, 6f, Rgba.White.WithAlpha(0.85f * a), 8, ang - 0.5f, 1f);
            }

            // the glass: brighter toward the wall
            for (int k = 0; k < 4; k++)
                Circle(c, r * (1f - k * 0.04f), AirMint.WithAlpha(0.05f * a), 48);
            Arc(c, r - 8f, 16f, AirPale.WithAlpha(0.12f * a), 48);

            // the cloud wall
            for (int i = 0; i < 18; i++)
            {
                float an = i / 18f * MathX.Tau + t * 0.6f, pr = r + Mathf.Sin(t * 3f + i * 2f) * 3f;
                GlowDisc(c + V2.FromAngle(an) * pr, 9f + 2f * Mathf.Sin(t * 4f + i) + hot * 4f, AirPale, 0.5f * a);
            }
            DashRing(c, r, 14f, 10f, t * 72f, 4f, AirPale.WithAlpha(0.8f * a));
            DashRing(c, r - 13f, 8f, 12f, -t * 102f, 3f, AirPale.WithAlpha(0.8f * a));

            // leaves, twigs and dust going round inside
            for (int k = 0; k < 18; k++)
            {
                float ph = Hash01(k + 3f) * MathX.Tau, w = (1.6f + Hash01(k) * 2.6f) * (k % 2 == 0 ? -1f : 1f), an = t * w + ph;
                float rd = Mathf.Min(r * (0.35f + 0.7f * Hash01(k + 7f)), r * 0.92f);
                V2 at = c + V2.FromAngle(an) * rd, dir = V2.FromAngle(an * (k % 3 == 0 ? 3f : 2f));
                if (k % 3 == 0)
                    Line(at - dir * 3.5f, at + dir * 3.5f, 3.2f, new Rgba(0.47f, 0.75f, 0.39f, 0.95f * a));
                else if (k % 3 == 1)
                    Line(at - dir * 3f, at + dir * 3f, 2f, new Rgba(0.71f, 0.58f, 0.36f, 0.95f * a));
                else
                    Circle(at, 1.6f, Rgba.White.WithAlpha(0.8f * a), 6);
            }

            // time left, draining clockwise from the top
            Arc(c, r + 10f, 3f, AirPale.WithAlpha(0.15f * a), 48);
            Arc(c, r + 10f, 3f, AirPale.WithAlpha(0.9f * a), 48, -MathX.Pi / 2f, MathX.Tau * rem);
        }

        void DashRing(V2 c, float rad, float dash, float gap, float offset, float width, Rgba col)
        {
            int n = Mathf.Max(6, Mathf.RoundToInt(MathX.Tau * rad / (dash + gap)));
            float step = MathX.Tau / n, sweep = step * dash / (dash + gap), o = offset / rad;
            for (int i = 0; i < n; i++)
                Arc(c, rad, width, col, 3, o + i * step, sweep);
        }
    }
}
