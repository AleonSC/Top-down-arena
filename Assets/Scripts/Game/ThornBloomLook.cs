using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Thorn Bloom (Core/ThornBloom.cs), look B "Needle Volley" of Docs/Mockups/thorn-bloom.html:
    /// twelve pale needles burst out of the caster (short spikes piercing out of the body, a flash and a spray of drops),
    /// each flying out with a fading red streak and bursting in a little star where it strikes (or hanging and fading at
    /// full reach). From every enemy struck, a wavy blood tendril with a barbed head whips on to the next, a dark tapered
    /// ribbon with a pink core and beads running along it; it rings the enemy it reaches, holds, then lets go from its root.
    public partial class ArenaApp
    {
        static readonly Rgba TbDeep = new Rgba(0.55f, 0.03f, 0.1f), TbRed = new Rgba(1f, 0.25f, 0.33f),
            TbPale = new Rgba(1f, 0.89f, 0.91f), TbPink = new Rgba(1f, 0.43f, 0.51f);

        /// FxKind.ThornBloom: the needles piercing out of the caster, a flash and a spray of drops.
        void DrawThornBloomFx(Effect fx)
        {
            float age = fx.T;
            Player p;
            V2 at = game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p.Disp : fx.Pos;
            float body = p != null ? p.Def.BodyR : 12f, aim = fx.Pos2.Y;
            int n = Mathf.Max(1, (int)fx.Radius);
            if (age < 0.22f)  // spikes through the body, between the needles' lines
            {
                float k = age / 0.22f, L = (1f - (1f - k) * (1f - k)) * 11f * (1f - 0.6f * k);
                for (int i = 0; i < n; i++)
                {
                    V2 d = V2.FromAngle(aim + (i + 0.5f) * MathX.Tau / n), side = d.Orthogonal();
                    Tri3(at + d * (body + L), at + side * 2.2f, at - side * 2.2f, TbPale.WithAlpha(1f - k * 0.5f));
                }
            }
            if (age < 0.16f)
                Circle(at, body + 4f + age * 60f, TbRed.WithAlpha(0.35f * (1f - age / 0.16f)), 20);
            for (int j = 0; j < 14; j++)  // drops flung off
            {
                float u = Mathf.Clamp01(age / 0.4f);
                if (u >= 1f)
                    break;
                float a = SprayHash(fx.Pos.X * 0.13f + j * 1.7f) * MathX.Tau, sp = 30f + 40f * SprayHash(fx.Pos.Y * 0.21f + j * 2.9f);
                V2 dp = at + V2.FromAngle(a) * (body + sp * (1f - (1f - u) * (1f - u)));
                Circle(dp, 1.4f + 1.2f * SprayHash(j * 5.3f), new Rgba(0.78f, 0.06f, 0.18f, 1f - u), 6);
            }
        }

        /// FxKind.ThornNeedle: a needle flying out with a fading streak; a burst where it strikes, or hanging and fading at
        /// full reach.
        void DrawThornNeedleFx(Effect fx)
        {
            V2 from = fx.Pos, to = fx.Pos2, path = to - from;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len, side = dir.Orthogonal();
            float flight = Mathf.Max(0.01f, fx.Dur - Game.TbNeedleTail), age = fx.T;
            bool hit = fx.Radius > 0.5f;
            if (age < flight)
            {
                float d = len * age / flight, trail = Mathf.Min(d, 70f);
                V2 head = from + dir * d;
                const int segs = 6;
                for (int s = 0; s < segs; s++)  // the streak, fading toward its tail
                {
                    float u0 = (float)s / segs, u1 = (float)(s + 1) / segs;
                    Line(head - dir * (trail * (1f - u0)), head - dir * (trail * (1f - u1)), 3f, TbRed.WithAlpha(0.85f * u1));
                }
                Circle(head, 6f, TbRed.WithAlpha(0.3f), 10);
                Tri3(head + dir * 9f, head + side * 2.6f - dir * 8f, head - side * 2.6f - dir * 8f, TbPale);
                return;
            }
            float k = Mathf.Clamp01((age - flight) / Game.TbNeedleTail);
            if (!hit)  // spent at full reach: it hangs and fades
            {
                Tri3(to + dir * 9f, to + side * 2.6f - dir * 8f, to - side * 2.6f - dir * 8f, TbPale.WithAlpha(1f - k));
                return;
            }
            float r = 4f + 14f * k;
            Circle(to, r, TbRed.WithAlpha(0.35f * (1f - k)), 12);
            for (int i = 0; i < 5; i++)  // a little star of blood
            {
                V2 d = V2.FromAngle(Mathf.Atan2(dir.Y, dir.X) + (i - 2) * 0.55f);
                Line(to + d * (r * 0.3f), to + d * (r + 4f), 2.2f * (1f - k) + 0.4f, TbRed.WithAlpha(1f - k));
            }
            if (k < 0.3f)
                Circle(to, 5f, TbPale.WithAlpha(1f - k / 0.3f), 10);
        }

        /// FxKind.ThornTendril: a wavy ribbon whipping from Pos to Pos2 after its delay, a barbed head; a ring where it
        /// lands; it holds, then lets go from its root.
        void DrawThornTendrilFx(Effect fx, float t)
        {
            float age = fx.T - Game.TbTendrilDelay(fx);
            if (age <= 0f)
                return;
            V2 a = fx.Pos, b = fx.Pos2, path = b - a;
            float len = path.Length;
            if (len < 1f)
                return;
            float travel = Game.TbTendrilBase + len / Game.TbTendrilSpeed, p = Mathf.Clamp01(age / travel), after = age - travel;
            float start = 0f, alpha = 1f;
            if (after > Game.TbTendrilHold)
            {
                float k = (after - Game.TbTendrilHold) / Game.TbTendrilFade;
                if (k >= 1f)
                    return;
                start = k;
                alpha = 1f - 0.5f * k;
            }
            V2 n = new V2(-path.Y / len, path.X / len);
            float amp = Mathf.Min(16f, len * 0.12f), seed = SprayHash(a.X * 0.07f + b.Y * 0.11f) * 10f;
            const int segs = 24;
            V2 prev = a, head = a, beforeHead = a;
            bool started = false;
            for (int s = 0; s <= segs; s++)
            {
                float u = (float)s / segs;
                if (u > p)
                    break;
                float off = Mathf.Sin(u * Mathf.PI) * amp * Mathf.Sin(seed + u * 8f - t * 12f);
                V2 q = a + path * u + n * off;
                if (u >= start && started)
                {
                    Line(prev, q, 5.5f - 2.5f * u, new Rgba(TbDeep.R + 0.3f * u, TbDeep.G, TbDeep.B, alpha));
                    Line(prev, q, 1.3f, TbPink.WithAlpha(0.8f * alpha));
                }
                if (u >= start)
                    started = true;
                beforeHead = prev;
                prev = head = q;
            }
            if (!started)
                return;
            for (int j = 0; j < 3; j++)  // beads of blood running along it
            {
                float u = Mathf.Lerp(start, p, (t * 2.2f + j / 3f) % 1f);
                float off = Mathf.Sin(u * Mathf.PI) * amp * Mathf.Sin(seed + u * 8f - t * 12f);
                Circle(a + path * u + n * off, 1.6f, TbPale.WithAlpha(alpha), 6);
            }
            if (p < 1f)  // the barbed head
            {
                V2 d = head - beforeHead;
                float dl = d.Length;
                V2 ux = dl > 0.01f ? d / dl : path / len, uy = ux.Orthogonal();
                Tri3(head + ux * 7f, head + uy * 4f, head - uy * 4f, TbPale);
            }
            else if (after < 0.15f)
                Arc(b, 14f + after * 60f, 2f, TbRed.WithAlpha(1f - after / 0.15f), 20);
        }
    }
}
