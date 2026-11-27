using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Clot Crystals (the Healer Hemomancer's 4th choice, Core/ClotCrystals.cs), look A Garnet from the "Clot Crystals" mockup.
    /// Everything comes from the minion sync (Pos, Aim, GuardFrac its health, Stacks its state, Wind 0..1 through it), so it costs
    /// nothing extra on the network:
    ///   a crystal: a six-faceted garnet pointing along Aim, lit facets, a bright upper edge, a dark outline and an inner pulse,
    ///     a soft red glow, a shadow, and a thin thread of blood back to its Hemomancer;
    ///   rising: it pops up out of a small pool, a little too big then settling; the shudder: it trembles at its shot;
    ///   the jerk: a smear of afterimages behind it and a brighter thread; a hit: a white flash and cracks that spread
    ///     across the facets as its health goes (seeded by its id), and a health arc once it's hurt;
    ///   melting: it shrinks and fades as it flies home; `FxKind.ClotShatter`: shards and a splash where it broke.
    /// Visual effects off: a plain hexagon.
    public partial class ArenaApp
    {
        static readonly Rgba CryLit = new Rgba(1f, 0.44f, 0.53f), CryRed = new Rgba(0.88f, 0.16f, 0.29f), CryMid = new Rgba(0.64f, 0.07f, 0.18f);
        static readonly Rgba CryDeep = new Rgba(0.43f, 0.06f, 0.14f), CryDark = new Rgba(0.56f, 0.06f, 0.16f), CryWarm = new Rgba(0.77f, 0.1f, 0.23f);
        static readonly Rgba CryPale = new Rgba(1f, 0.82f, 0.85f), CryEdge = new Rgba(0.23f, 0.02f, 0.07f);
        const float CrySize = 1.35f;  // the garnet's scale (its point is 20 px from its middle)

        /// Per crystal on this screen: its health last frame and when it was last hit (for the flash), and its recent spots (the smear).
        readonly Dictionary<int, Vector2> cryHit = new Dictionary<int, Vector2>();
        readonly Dictionary<int, List<Vector3>> cryTrail = new Dictionary<int, List<Vector3>>();

        // The garnet's outline, along +x: point, upper shoulder, upper back, tail, lower back, lower shoulder.
        static readonly V2[] CryShape = { new V2(15f, 0f), new V2(5f, -7.5f), new V2(-6f, -6.5f), new V2(-13f, 0f), new V2(-6f, 6.5f), new V2(5f, 7.5f) };
        static readonly Rgba[] CryFacet = { CryLit, CryRed, CryMid, CryDeep, CryDark, CryWarm };

        static V2 CryLocal(V2 c, V2 dir, V2 side, V2 p, float s) => c + dir * (p.X * s) + side * (p.Y * s);

        /// The garnet at `c` facing `dir`, `s` its scale, `frac` its health left (cracks), `flash` 0..1 white.
        void DrawGarnet(V2 c, V2 dir, float s, float frac, float flash, float pulse, int seed, float alpha)
        {
            V2 side = dir.Orthogonal(), mid = CryLocal(c, dir, side, new V2(1f, 0f), s);
            for (int i = 0; i < 6; i++)
            {
                V2 a = CryLocal(c, dir, side, CryShape[i], s), b = CryLocal(c, dir, side, CryShape[(i + 1) % 6], s);
                Tri3(a, b, mid, CryFacet[i].WithAlpha(alpha));
            }
            Ellipse(CryLocal(c, dir, side, new V2(2f, -1f), s), 5f * s, 2.4f * s, dir.Angle, CryPale.WithAlpha((0.18f + 0.18f * pulse) * alpha), 10);  // the inner pulse
            for (int i = 0; i < 6; i++)
                Line(CryLocal(c, dir, side, CryShape[i], s), CryLocal(c, dir, side, CryShape[(i + 1) % 6], s), 1.1f, CryEdge.WithAlpha(alpha));
            Line(CryLocal(c, dir, side, CryShape[0], s), CryLocal(c, dir, side, CryShape[1], s), 1.2f, CryPale.WithAlpha(alpha));  // the lit edge
            Line(CryLocal(c, dir, side, CryShape[1], s), CryLocal(c, dir, side, CryShape[2], s), 1.2f, CryPale.WithAlpha(0.8f * alpha));
            int cracks = Mathf.RoundToInt((1f - Mathf.Clamp01(frac)) * 7f);
            for (int k = 0; k < cracks; k++)  // cracks spread across the facets as it wears (the same ones each frame: seeded by its id)
            {
                float ang = Hash01(seed * 31 + k * 7) * MathX.Tau;
                V2 p = new V2(Mathf.Cos(ang), Mathf.Sin(ang)) * 1.5f;
                for (int j = 0; j < 3; j++)
                {
                    ang += (Hash01(seed * 31 + k * 7 + j + 1) - 0.5f) * 1.1f;
                    V2 q = p + new V2(Mathf.Cos(ang), Mathf.Sin(ang)) * (1.8f + 1.8f * Hash01(seed * 17 + k * 5 + j));
                    Line(CryLocal(c, dir, side, p, s), CryLocal(c, dir, side, q, s), 0.9f, new Rgba(1f, 0.92f, 0.94f, 0.85f * alpha));
                    p = q;
                }
            }
            if (flash > 0.01f)
                for (int i = 0; i < 6; i++)
                    Tri3(CryLocal(c, dir, side, CryShape[i], s), CryLocal(c, dir, side, CryShape[(i + 1) % 6], s), mid, new Rgba(1f, 0.95f, 0.95f, 0.8f * flash * alpha));
        }

        /// A Clot Crystal minion (DrawMinion).
        void DrawClotCrystal(Minion m, float t)
        {
            float frac = Mathf.Clamp01(m.GuardFrac);
            if (frac <= 0f)
                return;  // shattered (ClotShatter draws the shards)
            V2 c = m.Disp;
            if (!fxOn)
            {
                Polygon(c, m.Def.Radius, 6, m.Aim, CryRed);
                return;
            }
            // A hit: its health dropped since last frame.
            Vector2 hit;
            if (!cryHit.TryGetValue(m.Id, out hit))
                hit = new Vector2(frac, -9f);
            if (frac < hit.x - 0.001f)
                hit.y = t;
            hit.x = frac;
            cryHit[m.Id] = hit;
            float flash = Mathf.Clamp01(1f - (t - hit.y) * 6f);
            // Its recent spots, for the smear behind a jerk.
            List<Vector3> trail;
            if (!cryTrail.TryGetValue(m.Id, out trail))
                cryTrail[m.Id] = trail = new List<Vector3>();
            trail.Add(new Vector3(c.X, c.Y, t));
            while (trail.Count > 0 && t - trail[0].z > 0.14f)
                trail.RemoveAt(0);
            if (cryHit.Count > 64)  // (crystals come and go: forget old ones now and then)
            {
                cryHit.Clear();
                cryTrail.Clear();
            }

            int state = m.Stacks;
            float s = CrySize, alpha = 1f;
            if (state == Game.CryRising)
                s *= Mathf.Max(0.05f, Game.CryEase(m.Wind));
            else if (state == Game.CryMelting)
            {
                s *= Mathf.Max(0.05f, 1f - m.Wind);
                alpha = 1f - 0.6f * m.Wind;
            }
            else if (state == Game.CryJerking)
                s *= 1.08f;
            else if (state == Game.CrySettling)
                s *= 1f + 0.12f * Mathf.Sin(m.Wind * Mathf.PI) * (1f - m.Wind);
            if (state == Game.CryTelling)  // the shudder before the jerk
                c += new V2(Mathf.Sin(t * 173f + m.Id) * 1.6f, Mathf.Cos(t * 151f + m.Id * 3f) * 1.6f);
            float bob = state == Game.CryIdle ? Mathf.Sin(t * 3f + m.Id * 2f) * 1.5f : 0f;
            V2 at = c + new V2(0f, bob), dir = V2.FromAngle(m.Aim);

            // The thread of blood back to its Hemomancer.
            Player owner;
            if (game.Players.TryGetValue(m.Owner, out owner) && state != Game.CryRising)
            {
                V2 o = owner.Disp, d = at - o;
                V2 bend = (o + at) * 0.5f + new V2(d.Y, -d.X) * 0.18f;
                bool hot = state == Game.CryJerking;
                V2 prev = o;
                for (int i = 1; i <= 8; i++)
                {
                    float u = i / 8f;
                    V2 q = V2.Lerp(V2.Lerp(o, bend, u), V2.Lerp(bend, at, u), u);
                    Line(prev, q, hot ? 1.6f : 1f, CryMid.WithAlpha((hot ? 0.75f : 0.32f) * alpha));
                    prev = q;
                }
            }
            if (state == Game.CryRising)  // the pool it climbs out of
                Ellipse(c + new V2(0f, 5f), 14f * Mathf.Clamp01(m.Wind * 2f), 8f * Mathf.Clamp01(m.Wind * 2f), 0f, CryDeep.WithAlpha(0.6f), 14);
            // The smear behind a jerk.
            if (state == Game.CryJerking || state == Game.CrySettling)
                for (int i = 0; i < trail.Count - 1; i++)
                {
                    float k = 1f - (t - trail[i].z) / 0.14f;
                    V2 g = new V2(trail[i].x, trail[i].y);
                    if (g.CloserThan(c, 3f))
                        continue;
                    V2 side = dir.Orthogonal();
                    for (int f = 0; f < 6; f++)
                        Tri3(CryLocal(g, dir, side, CryShape[f], s), CryLocal(g, dir, side, CryShape[(f + 1) % 6], s), g, CryRed.WithAlpha(0.35f * k * k));
                }
            Ellipse(c + new V2(0f, 17f), 10f * s / CrySize, 3f * s / CrySize, 0f, new Rgba(0f, 0f, 0f, 0.3f * alpha), 12);  // its shadow
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f + m.Id);
            GlowDisc(at, 30f * s / CrySize, CryRed, (0.3f + 0.2f * pulse + 0.4f * flash) * alpha);
            DrawGarnet(at, dir, s, frac, flash, pulse, m.Id, alpha);
            if (frac < 0.999f && state != Game.CryMelting)  // its health, once hurt: an arc round it, gold when nearly broken
            {
                const float a0 = MathX.Pi * 0.65f, sweep = MathX.Pi * 1.7f;
                Arc(at, 22f, 1.5f, new Rgba(1f, 1f, 1f, 0.08f), 24, a0, sweep);
                Arc(at, 22f, 1.5f, (frac > 0.34f ? CryLit : new Rgba(1f, 0.7f, 0.28f)).WithAlpha(0.8f), 24, a0, sweep * frac);
            }
        }

        /// `FxKind.ClotShatter`: a crystal broke: shards fly out spinning, a flash and a splash of blood.
        void DrawClotShatterFx(Effect fx)
        {
            float age = fx.T, k = Mathf.Clamp01(age / Mathf.Max(0.01f, fx.Dur));
            V2 c = fx.Pos;
            int seed = (int)(c.X * 7f) ^ (int)(c.Y * 13f);
            Ellipse(c + new V2(0f, 4f), 16f * Mathf.Clamp01(k * 4f), 9f * Mathf.Clamp01(k * 4f), 0f, CryDeep.WithAlpha(0.55f * (1f - k)), 14);  // the splash
            if (!fxOn)
            {
                Arc(c, 10f + 20f * k, 2f, CryRed.WithAlpha(1f - k), 16);
                return;
            }
            if (age < 0.12f)
                GlowDisc(c, 34f, CryPale, 1f - age / 0.12f);
            float out1 = 1f - (1f - k) * (1f - k) * (1f - k);
            for (int i = 0; i < 12; i++)
            {
                float ang = MathX.Tau * i / 12f + Hash01(seed + i) * 0.5f, dist = (24f + 36f * Hash01(seed + i * 3 + 1)) * out1;
                V2 at = c + new V2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                float rot = ang + age * (6f + 8f * Hash01(seed + i * 5)), size = 2.5f + 2.5f * Hash01(seed + i * 7);
                V2 d = V2.FromAngle(rot), side = d.Orthogonal();
                var col = (i % 3 == 0 ? CryPale : i % 2 == 0 ? CryLit : CryRed).WithAlpha(1f - k);
                Tri3(at + d * size, at - d * (size * 0.5f) + side * (size * 0.6f), at - d * (size * 0.5f) - side * (size * 0.6f), col);
            }
        }
    }
}
