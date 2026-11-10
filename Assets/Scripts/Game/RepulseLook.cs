using System;
using UnityEngine;

namespace Arena
{
    /// The Repulsion Cannon as a cannon blast (Docs/Mockups/repulsion-cannon.html, look D), with Visual effects on: a white
    /// muzzle star, smoke billowing through the cone, a crisp shock ring, sparks, and a very small screen kick (Juice.cs).
    /// Also the Undertow (Alt pull) current and its meter. Drawing only; the cone, shove, orbs and pull are in Repulsion.cs.
    public partial class ArenaApp
    {
        /// FxKind.Repulse. f is how far through the effect's life it is (Game.RepulseFxTime).
        void DrawRepulseBlast(Effect fx, float f)
        {
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, reach = mid.Length, half = fx.Radius;
            float t = f * Game.RepulseFxTime;  // seconds since the shot
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663 ^ (int)(aim * 1000f) * 83492791) | 1u;
            Func<float> rnd = () => { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed & 0xFFFFFF) / 16777216f; };
            var smoke = new Rgba(0.8f, 0.92f, 0.98f);
            // Smoke: soft puffs thrown out through the cone, growing as they thin.
            for (int i = 0; i < 26; i++)
            {
                float a = aim + (rnd() * 2f - 1f) * half * 0.92f, sp = 0.6f + rnd() * 0.55f, born = rnd() * 0.06f, life = 0.4f + rnd() * 0.2f, size = rnd();
                float u = (t - born) / life;
                if (u <= 0f || u >= 1f)
                    continue;
                float d = 14f + (reach - 40f) * (1f - Mathf.Pow(1f - u, 3f)) * sp, rad = 12f + 22f * u + 8f * size, al = 0.3f * (1f - u);
                V2 at = c + V2.FromAngle(a) * d;
                Circle(at, rad, smoke.WithAlpha(al * 0.35f), 14);  // soft: three stacked discs
                Circle(at, rad * 0.7f, smoke.WithAlpha(al * 0.4f), 14);
                Circle(at, rad * 0.4f, smoke.WithAlpha(al * 0.5f), 12);
            }
            // The shock ring: thin and crisp.
            float g = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.16f), 3f), ringFade = 1f - Mathf.Clamp01((t - 0.1f) / 0.2f);
            if (ringFade > 0f)
            {
                float rr = 24f + (reach - 24f) * g;
                Arc(c, rr, 9f, fx.Color.WithAlpha(0.22f * ringFade), 16, aim - half, 2f * half);
                Arc(c, rr, 3f, new Rgba(0.92f, 0.99f, 1f, 0.9f * ringFade), 16, aim - half, 2f * half);
            }
            // The muzzle star.
            if (t < 0.11f)
            {
                float k = 1f - t / 0.11f;
                Circle(c, 56f, fx.Color.WithAlpha(0.12f * k), 18);
                Circle(c, 38f, new Rgba(0.67f, 0.94f, 1f, 0.25f * k), 18);
                Circle(c, 20f, Rgba.White.WithAlpha(0.7f * k), 16);
                SetColor(Rgba.White.WithAlpha(0.9f * k));
                for (int i = 0; i < 7; i++)
                {
                    float a = aim + (i / 6f - 0.5f) * half * 2.1f;
                    Quad(c, c + V2.FromAngle(a - 0.07f) * (70f * k), c + V2.FromAngle(a) * (96f * k), c + V2.FromAngle(a + 0.07f) * (70f * k));
                }
            }
            // Sparks, with trails.
            for (int i = 0; i < 20; i++)
            {
                float a = aim + (rnd() * 2f - 1f) * half, sp = 0.5f + rnd() * 0.5f, u = t / (0.2f + rnd() * 0.15f), tail = 16f + 16f * rnd();
                if (u >= 1f)
                    continue;
                float d = 20f + (reach + 10f) * (1f - Mathf.Pow(1f - u, 3f)) * sp;
                Line(c + V2.FromAngle(a) * Mathf.Max(0f, d - tail), c + V2.FromAngle(a) * d, 1.6f, Rgba.White.WithAlpha(1f - u));
            }
        }
    
        static readonly Rgba PullViolet = new Rgba(0.77f, 0.61f, 1f), PullDeep = new Rgba(0.62f, 0.4f, 1f), PullPale = new Rgba(0.94f, 0.89f, 1f);

        /// The Undertow (Repulsion.cs; Docs/Mockups/repulsion-pull.html, option B): while the pull is held, a violet current
        /// fills its cone, flow lines crawl in toward the muzzle, the muzzle throbs, and every enemy caught gets a violet rim.
        void DrawRepelPull(Player p)
        {
            if (!fxOn || p.Downed || !p.RepelPull)
                return;
            float t = Time.unscaledTime, reach = game.RepelPullReachOf(p), half = game.RepelPullHalfOf(p), aim = p.Aim;
            V2 c = p.Disp, muzzle = c + V2.FromAngle(aim) * (p.Def.BodyR + 12f);
            FanGradient(c, reach, PullViolet.WithAlpha(0.16f), PullViolet.WithAlpha(0.03f), 20, aim - half, 2f * half);
            Arc(c, reach, 1.6f, PullViolet.WithAlpha(0.45f), 24, aim - half, 2f * half);
            for (int i = 0; i < 14; i++)  // flow lines: three dashes a lane, crawling inward and swaying
            {
                float lane = (i / 13f - 0.5f) * 2f * half * 0.9f;
                for (int j = 0; j < 3; j++)
                {
                    float u = (t * 1.4f + j / 3f + i * 0.13f) % 1f, d = reach * (1f - u) + 28f;
                    float a1 = aim + lane + Mathf.Sin(u * 6f + i) * 0.08f, a2 = aim + lane + Mathf.Sin((u + 0.06f) * 6f + i) * 0.08f;
                    Line(c + V2.FromAngle(a1) * d, c + V2.FromAngle(a2) * Mathf.Max(24f, d - 26f), 2f, PullViolet.WithAlpha(0.7f * Mathf.Sin(MathX.Pi * u)));
                }
            }
            float throb = 0.7f + 0.3f * Mathf.Sin(t * 20f);
            Circle(muzzle, 14f, PullViolet.WithAlpha(0.3f * throb), 14);
            Circle(muzzle, 6f, PullPale.WithAlpha(0.7f * throb), 10);
            foreach (var e in game.Enemies.Values)  // the caught
            {
                if (e.Hp <= 0f || e.Def.IsBoss || e.Def.Heavy)
                    continue;
                V2 rel = e.Disp - c;
                float d = rel.Length;
                if (d < 1f || d - e.Radius > reach)
                    continue;
                float off = Mathf.Abs(MathX.Fmod(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (off > half + (float)Math.Asin(Math.Min(1f, e.Radius / d)))
                    continue;
                Arc(e.Disp, e.Radius + 4f, 2f, PullViolet.WithAlpha(0.8f), 18);
            }
        }

        /// The Undertow meter: a curved gauge behind you, violet as it fills, with a notch where a pull can start; it glows
        /// when full and drains as you pull.
        void DrawRepelMeter(Player p)
        {
            if (!fxOn || p.Downed || p.RepelMeter == 0 && !p.RepelPull)
                return;
            float fill = Mathf.Clamp01(p.RepelMeter / Game.RepelMeterMax), sweep = 1.9f, r = p.Def.BodyR + 13f;
            float start = p.Aim + MathX.Pi - sweep / 2f;  // centred behind you, filling from your right round to your left
            bool ready = p.RepelMeter >= Game.RepelPullMin, full = p.RepelMeter >= Game.RepelMeterMax;
            Arc(p.Disp, r, 5f, PullViolet.WithAlpha(0.16f), 20, start, sweep);
            if (fill > 0f)
            {
                Rgba col = ready ? PullDeep : PullDeep.WithAlpha(0.45f);
                if (full || p.RepelPull)
                    Arc(p.Disp, r, 9f, PullViolet.WithAlpha(0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 7f)), 20, start, sweep * fill);
                Arc(p.Disp, r, 5f, col, 20, start, sweep * fill);
            }
            float notch = start + sweep * Game.RepelPullMin / Game.RepelMeterMax;
            Line(p.Disp + V2.FromAngle(notch) * (r - 4f), p.Disp + V2.FromAngle(notch) * (r + 4f), 1.4f, PullPale.WithAlpha(0.6f));
        }
}
}
