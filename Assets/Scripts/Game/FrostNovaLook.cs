using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Frost Nova in style A, "Shatter" (Docs/Mockups/frost-nova-rework.html), with Visual effects on: a white flash, a shard-tipped
    /// wave out to the edge in 0.45 s, cracks racing out from the caster's feet and settling into frosted ground with a faceted
    /// rim; each frozen enemy sits in a faceted ice block with a ring counting its freeze down. The cast's Ring effect lasts
    /// FrostNovaFxTime (the freeze plus the wave) so the ground can stay; with effects off only its first half second draws
    /// as the old ring. Drawing only: nothing is synced.
    public partial class ArenaApp
    {
        const float NovaWave = 0.45f, NovaFreeze = 3f;
        static readonly Rgba NovaIce = new Rgba(0.5f, 0.82f, 1f), NovaPale = new Rgba(0.88f, 0.96f, 1f);
        readonly Dictionary<int, Vector2> frozenSeen = new Dictionary<int, Vector2>();  // per enemy: when it froze and last frame

        bool IsFrostNova(Effect fx) => fx.Kind == FxKind.Ring && Mathf.Abs(fx.Radius - 220f) < 1f && Mathf.Abs(fx.Dur - Game.FrostNovaFxTime) < 0.01f;

        void DrawFrostNova(Effect fx)
        {
            float age = fx.T, R = fx.Radius;
            if (!fxOn)  // the old ring: out and gone in half a second
            {
                float f = age / 0.5f;
                if (f < 1f)
                    Arc(fx.Pos, R * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                return;
            }
            V2 c = fx.Pos;
            float ap = Mathf.Clamp01(age / NovaWave), w = R * EaseOut3(ap);
            float fd = Mathf.Clamp01((Game.FrostNovaFxTime - age) / 0.5f);
            if (age < 0.14f)
                GlowDisc(c, w + 30f, Rgba.White, 0.55f * (1f - age / 0.14f));
            // the frosted ground, heavier toward the edge
            for (int k = 0; k < 6; k++)
                Circle(c, w * (1f - k * 0.09f), NovaIce.WithAlpha(0.05f * fd * EaseOut3(ap)), 48);
            // cracks out from the caster's feet
            float seed = (float)System.Math.Floor(c.X * 0.13f + c.Y * 0.07f);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * MathX.Tau + (Hash01(i + seed) - 0.5f) * 0.3f, len = R * (0.55f + 0.45f * Hash01(i + 3f + seed));
                V2 p0 = c, branchAt = c;
                float ba = 0f;
                for (int s = 1; s <= 7; s++)
                {
                    a += (Hash01(i * 9f + s + seed) - 0.5f) * 0.5f;
                    V2 p1 = p0 + V2.FromAngle(a) * (len / 7f);
                    if (p0.DistanceTo(c) > w)
                        break;
                    CrackSeg(p0, p1, c, w, fd);
                    if (s == 3) { branchAt = p1; ba = a; }
                    p0 = p1;
                }
                if (Hash01(i + 50f + seed) > 0.4f)
                {
                    float b = ba + (Hash01(i + 60f + seed) > 0.5f ? 0.8f : -0.8f);
                    V2 q0 = branchAt;
                    for (int s = 1; s <= 3; s++)
                    {
                        V2 q1 = q0 + V2.FromAngle(b) * 18f;
                        if (q0.DistanceTo(c) > w)
                            break;
                        CrackSeg(q0, q1, c, w, fd);
                        q0 = q1;
                    }
                }
            }
            // the wave itself, and the shard-tipped rim it leaves
            if (age < 0.7f)
                Arc(c, w, 10f * (1f - age / 0.7f) + 2f, NovaIce.WithAlpha(0.95f * (1f - age / 0.7f)), 64);
            for (int i = 0; i < 34; i++)
            {
                float a = i / 34f * MathX.Tau, hv = Hash01(i);
                float h = (age < 0.9f ? 26f + hv * 22f : 9f + hv * 5f) * (age < 0.9f ? Mathf.Clamp01(1.3f - age / 0.9f) : 1f);
                float al = (age < 0.9f ? Mathf.Clamp01(1.2f - age / 0.9f) + 0.4f : 0.8f) * fd;
                V2 dir = V2.FromAngle(a), b0 = c + dir * w, n = dir.Orthogonal() * 5.5f;
                SetColor((i % 2 == 0 ? NovaPale : NovaIce).WithAlpha(Mathf.Clamp01(al)));
                Tri(b0 + n, b0 - n, b0 + dir * h);
                Line(b0, b0 + dir * h, 1.2f, Rgba.White.WithAlpha(0.5f * al));
            }
        }

        void CrackSeg(V2 a, V2 b, V2 center, float reach, float fd)
        {
            if (b.DistanceTo(center) > reach)  // the tip is still racing out
                b = a + (b - a) * Mathf.Clamp01((reach - a.DistanceTo(center)) / Mathf.Max(1f, (b - a).Length));
            Line(a, b, 5f, NovaIce.WithAlpha(0.12f * fd));
            Line(a, b, 2.2f, Rgba.White.WithAlpha(0.55f * fd));
        }

        /// A frozen enemy's faceted ice block, with a ring counting down the three seconds of a Frost Nova (WorldEnemies.cs).
        void DrawIceBlockFacets(Enemy e, V2 pos, float r, float t)
        {
            frozenSeen.TryGetValue(e.Id, out var seen);
            float start = seen.x;
            if (t - seen.y > 0.5f || start <= 0f)
                start = t;  // newly frozen
            frozenSeen[e.Id] = new Vector2(start, t);
            float left = Mathf.Clamp01(1f - (t - start) / NovaFreeze);
            float s = r + 13f;
            V2[] h = new V2[6];
            for (int i = 0; i < 6; i++)
                h[i] = pos + V2.FromAngle(0.5f + MathX.Tau * i / 6f) * s;
            SetColor(new Rgba(0.75f, 0.92f, 1f, 0.55f));
            for (int i = 0; i < 6; i++)
                Tri(pos, h[i], h[(i + 1) % 6]);
            for (int i = 0; i < 6; i++)
                Line(h[i], h[(i + 1) % 6], 2f, Rgba.White.WithAlpha(0.9f));
            SetColor(new Rgba(1f, 1f, 1f, 0.3f));
            Tri(pos + new V2(-s * 0.5f, -s * 0.85f), pos + new V2(0f, -s * 0.2f), pos + new V2(-s * 0.9f, -s * 0.1f));
            SetColor(new Rgba(0.24f, 0.47f, 0.75f, 0.3f));
            Tri(pos + new V2(s * 0.9f, s * 0.1f), pos + new V2(s * 0.3f, s * 0.85f), pos + new V2(0f, s * 0.2f));
            Line(pos + new V2(-s * 0.55f, -s * 0.4f), pos + new V2(-s * 0.15f, -s * 0.7f), 1.5f, Rgba.White.WithAlpha(0.7f));
            if (left > 0f && left < 1f)
                Arc(pos, r + 18f, 2.2f, NovaPale.WithAlpha(0.9f), 24, -MathX.Pi / 2f, MathX.Tau * left);
        }
    }
}
