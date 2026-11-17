using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Water Ward (Core/WaterWard.cs), look C "Bubbles" of Docs/Mockups/water-ward.html: wobbling water bubbles circling the caster, each
    /// with a swirl turning inside, a crescent shine and a soft glow, squashed along its path. A new drop falls from above as a stretched
    /// teardrop over its shadow and a shrinking landing ring, then lands with a splash ring and a jiggle. FxKind.WwSplash is a drop
    /// splashing on an enemy (flash, rim-bright disc, two rings, a crown of droplets, a puddle that fades); FxKind.WwBurst a drop
    /// eating a shot (the shot hisses to steam, a mist billows out over a dashed ring, a wide spray of fine droplets).
    /// Each screen keeps its own copy of every player's drops (wwVis), matched to the replicated ones by their place on the ring, so the
    /// drops glide smoothly between snapshots and a drop that splashes is the one that vanishes.
    public partial class ArenaApp
    {
        static readonly Rgba WwDeep = new Rgba(0.27f, 0.75f, 0.88f), WwEdge = new Rgba(0.8f, 0.97f, 1f), WwFoam = new Rgba(0.88f, 0.99f, 1f),
            WwSteam = new Rgba(0.88f, 0.88f, 0.88f);
        const float WwBubbleR = 9.5f, WwLift = 150f;

        class WwDropVis { public float Off, Fall, Land, Seed; public bool Seen; }
        class WwVis { public float Ang, Clock = -1f; public readonly List<WwDropVis> Drops = new List<WwDropVis>(); }
        readonly Dictionary<int, WwVis> wwVis = new Dictionary<int, WwVis>();
        readonly V2[] wwPts = new V2[25];

        static float WwHash(float n) { float x = Mathf.Sin(n * 91.7f + 47.3f) * 43758.5453f; return x - Mathf.Floor(x); }
        static float WwWrap(float a) => Mathf.Repeat(a + Mathf.PI, MathX.Tau) - Mathf.PI;

        /// Called for every player each frame (cheap when they have no drops).
        void DrawWaterWard(Player p, float t)
        {
            WwVis v;
            if (!wwVis.TryGetValue(p.Id, out v))
            {
                if (p.WwOff.Count == 0)
                    return;
                wwVis[p.Id] = v = new WwVis { Ang = p.WwAng };
            }
            float dt = v.Clock < 0f ? 0f : Mathf.Clamp(t - v.Clock, 0f, 0.05f);
            v.Clock = t;
            v.Ang += Game.WwSpin * dt;  // turn on our own clock, pulled toward the host's
            v.Ang += WwWrap(p.WwAng - v.Ang) * Mathf.Min(1f, dt * 8f);
            if (Mathf.Abs(WwWrap(p.WwAng - v.Ang)) > 1f)
                v.Ang = p.WwAng;
            WwMatch(p, v, dt);
            if (v.Drops.Count == 0)
            {
                wwVis.Remove(p.Id);
                return;
            }

            V2 c = p.Disp;
            Arc(c, Game.WwOrbit, 1.2f, WwDeep.WithAlpha(0.1f + 0.012f * v.Drops.Count), 40);  // a faint path
            foreach (var d in v.Drops)  // shadows and landing marks first, under everything
            {
                V2 at = c + V2.FromAngle(v.Ang + d.Off) * Game.WwOrbit;
                float z = WwHeight(d.Fall), s = 1f / (1f + z / 90f);
                Ellipse(at + new V2(0f, 8f), WwBubbleR * 1.1f * s + 1f, WwBubbleR * 0.45f * s + 0.5f, 0f, new Rgba(0f, 0.04f, 0.06f, 0.35f * s), 12);
                if (d.Fall > 0f)
                {
                    float k = d.Fall >= Game.WwFall ? 0f : 1f - d.Fall / Game.WwFall;
                    Arc(at + new V2(0f, 2f), Mathf.Lerp(20f, 8f, k), 1.6f, WwEdge.WithAlpha(0.25f + 0.5f * k), 20);
                }
            }
            foreach (var d in v.Drops)
            {
                float a = v.Ang + d.Off;
                V2 at = c + V2.FromAngle(a) * Game.WwOrbit;
                if (d.Fall > 0f)
                {
                    if (d.Fall < Game.WwFall)
                        WwFalling(at, d.Fall);
                    continue;
                }
                if (d.Land > 0f)
                    WwLanding(at, 1f - d.Land, d.Seed);
                WwBubble(at, a + Mathf.PI * 0.5f, d, t);
            }
        }

        /// Keep our copy of a player's drops in step with the replicated ones: each replicated drop takes the nearest of ours on the ring.
        void WwMatch(Player p, WwVis v, float dt)
        {
            foreach (var d in v.Drops)
                d.Seen = false;
            for (int i = 0; i < p.WwOff.Count; i++)
            {
                WwDropVis best = null;
                float bd = 0.7f;
                foreach (var d in v.Drops)
                {
                    float gap = Mathf.Abs(WwWrap(p.WwOff[i] - d.Off));
                    if (!d.Seen && gap < bd)
                    {
                        bd = gap;
                        best = d;
                    }
                }
                if (best == null)
                {
                    best = new WwDropVis { Off = p.WwOff[i], Fall = p.WwFall[i], Seed = WwHash(p.Id * 13 + i + Time.frameCount) * 6.28f };
                    v.Drops.Add(best);
                }
                best.Seen = true;
                best.Off += WwWrap(p.WwOff[i] - best.Off) * Mathf.Min(1f, dt * 12f);
                float was = best.Fall;
                best.Fall = Mathf.Max(0f, Mathf.Min(best.Fall - dt, p.WwFall[i] + 0.05f));  // our own clock, never far behind the host's
                if (p.WwFall[i] <= 0f)
                    best.Fall = 0f;
                if (was > 0f && best.Fall <= 0f)
                    best.Land = 1f;
                best.Land = Mathf.Max(0f, best.Land - dt * 2.5f);
            }
            v.Drops.RemoveAll(d => !d.Seen);
        }

        /// How high a falling drop is: gravity, from WwLift down to the ground over WwFall s.
        static float WwHeight(float fall)
        {
            if (fall <= 0f)
                return 0f;
            float k = Mathf.Clamp01(fall / Game.WwFall);
            return WwLift * (1f - (1f - k) * (1f - k));
        }

        /// A drop falling: a streak above it and a teardrop stretched down, glowing.
        void WwFalling(V2 at, float fall)
        {
            float z = WwHeight(fall), sp = Mathf.Clamp01(1f - fall / Game.WwFall);
            V2 top = at - new V2(0f, z);
            Line(top - new V2(0f, 6f), top - new V2(0f, 6f + 30f * sp), 2.2f, WwEdge.WithAlpha(0.35f * sp));
            GlowDisc(top, WwBubbleR * 2.4f, WwDeep, 0.35f);
            float r = WwBubbleR * 0.75f, tail = r * (1f + sp * 1.4f);
            WwShape(top, r, tail, -Mathf.PI * 0.5f, WwDeep.WithAlpha(0.75f));
            Ellipse(top + new V2(-r * 0.3f, -r * 0.1f), r * 0.28f, r * 0.18f, -0.6f, Rgba.White.WithAlpha(0.85f), 8);
        }

        /// A teardrop: a round head toward `face` and a point `tail` behind it.
        void WwShape(V2 c, float r, float tail, float face, Rgba col)
        {
            V2 f = V2.FromAngle(face), s = new V2(-f.Y, f.X);
            SetColor(col);
            const int n = 12;
            V2 prev = c - f * tail;
            for (int i = 1; i <= n; i++)
            {
                float a = Mathf.PI * i / n;  // round the head from one side to the other
                V2 next = c + f * (Mathf.Sin(a) * r * 0.9f) + s * (Mathf.Cos(a) * r);
                Tri(c - f * tail, prev, next);
                prev = next;
            }
        }

        /// The splash as a drop lands: a ring out and droplets thrown up and falling back.
        void WwLanding(V2 at, float k, float seed)
        {
            Arc(at, 3f + 17f * (1f - (1f - k) * (1f - k)), 2f * (1f - k) + 0.5f, WwEdge.WithAlpha(0.8f * (1f - k)), 18);
            WwCrown(at, 7, seed, k * 0.5f, 55f, 120f, 1.8f, 1f - k);
        }

        /// A crown of droplets thrown out from `at`, `tau` s after: each flies out and up, then falls back (520 px/s²) and is gone when it lands.
        void WwCrown(V2 at, int n, float seed, float tau, float speed, float up, float size, float alpha)
        {
            for (int i = 0; i < n; i++)
            {
                float h = WwHash(seed + i * 3.7f), a = MathX.Tau * i / n + h * 0.4f, s = speed * (0.6f + 0.6f * WwHash(seed + i * 5.1f));
                float vz = up * (0.7f + 0.6f * h), z = vz * tau - 260f * tau * tau;
                if (z < 0f)
                    continue;
                float damp = (1f - Mathf.Exp(-1.5f * tau)) / 1.5f;
                V2 g = at + V2.FromAngle(a) * (s * damp);
                float r = size * (0.8f + 0.8f * WwHash(seed + i));
                Ellipse(g + new V2(0f, 2f), r, r * 0.4f, 0f, new Rgba(0f, 0f, 0f, 0.25f * alpha), 6);
                Ellipse(g - new V2(0f, z), r * 0.8f, r * 1.25f, 0f, WwFoam.WithAlpha(0.95f * alpha), 8);
            }
        }

        /// One bubble: a glow, a wobbling see-through body squashed along its path, a bright rim, a swirl turning inside and a shine.
        void WwBubble(V2 at, float path, WwDropVis d, float t)
        {
            float R = WwBubbleR, land = d.Land, jig = land * Mathf.Sin((1f - land) * 16f);
            float sx = 1.13f + 0.3f * jig, sy = 0.92f - 0.25f * jig;  // stretched along the path, squashed across; a jiggle as it lands
            V2 ux = V2.FromAngle(path), uy = new V2(-ux.Y, ux.X);
            GlowDisc(at, R * 2.4f, WwDeep, 0.24f);
            int n = fxReduced ? 16 : 24;
            for (int i = 0; i < n; i++)
            {
                float a = MathX.Tau * i / n;
                float r = R * (1f + (fxReduced ? 0f : 0.05f * Mathf.Sin(3f * a + t * 5f + d.Seed) + 0.025f * Mathf.Sin(5f * a - t * 7f)));
                wwPts[i] = at + ux * (Mathf.Cos(a) * r * sx) + uy * (Mathf.Sin(a) * r * sy);
            }
            var body = WwDeep.WithAlpha(0.3f);
            for (int i = 0; i < n; i++)  // the body: see-through, deeper toward the rim
                VTri(wwPts[i], WwDeep.WithAlpha(0.42f), wwPts[(i + 1) % n], WwDeep.WithAlpha(0.42f), at, body.WithAlpha(0.14f));
            for (int i = 0; i < n; i++)
                Line(wwPts[i], wwPts[(i + 1) % n], 1.5f, WwEdge.WithAlpha(0.88f));
            float spin = t * 3f + d.Seed;  // the swirl inside
            Arc(at, R * 0.55f, 1.3f, WwFoam.WithAlpha(0.6f), 12, spin, Mathf.PI * 0.9f);
            Arc(at, R * 0.3f, 1.2f, WwDeep.Mix(Rgba.White, 0.4f).WithAlpha(0.65f), 10, spin + Mathf.PI, Mathf.PI * 0.8f);
            Arc(at, R * 0.72f, 1.9f, Rgba.White.WithAlpha(0.9f), 10, -2.6f, 0.85f);  // the crescent shine (up and left, whatever the path)
            Circle(at + new V2(R * 0.45f, R * 0.4f), 1.2f, Rgba.White.WithAlpha(0.8f), 6);
        }

        /// FxKind.WwSplash: a flash, a disc bright at its rim spreading to the reach, two rings, a crown of droplets and a puddle that fades.
        void DrawWwSplashFx(Effect fx)
        {
            float T = fx.T, R = fx.Radius, f = Mathf.Clamp01(T / 0.6f), fade = Mathf.Clamp01(1f - T / Mathf.Max(0.01f, fx.Dur));
            V2 o = fx.Pos;
            float seed = o.X * 0.37f + o.Y * 0.11f;
            // the puddle left behind: an uneven blot, fading over the whole effect
            SetColor(WwDeep.WithAlpha(0.3f * fade * fade));
            int n = 18;
            float pr = R * 0.42f * (1f + 0.15f * (1f - fade));
            V2 prev = V2.Zero;
            for (int i = 0; i <= n; i++)
            {
                float a = MathX.Tau * i / n, r = pr * (0.8f + 0.25f * Mathf.Sin(a * 3f + seed) + 0.12f * Mathf.Sin(a * 5f + seed * 2f));
                V2 q = o + new V2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.85f);
                if (i > 0)
                    Tri(o, prev, q);
                prev = q;
            }
            if (f < 1f)
            {
                float grow = 1f - (1f - Mathf.Min(1f, f * 2.2f)) * (1f - Mathf.Min(1f, f * 2.2f)) * (1f - Mathf.Min(1f, f * 2.2f));
                FanGradient(o, R * grow, WwDeep.WithAlpha(0.08f * (1f - f)), WwDeep.WithAlpha(0.26f * (1f - f)), 24);
                float e1 = 1f - Mathf.Pow(1f - Mathf.Min(1f, f / 0.65f), 3f);
                Arc(o, Mathf.Lerp(6f, R, e1), 3.5f * (1f - f * 0.6f), WwEdge.WithAlpha(0.9f * (1f - f)), 32);
                float f2 = Mathf.Clamp01((T - 0.06f) / 0.5f);
                if (f2 > 0f && f2 < 1f)
                    Arc(o, Mathf.Lerp(4f, R * 0.7f, 1f - Mathf.Pow(1f - f2, 3f)), 1.5f, WwFoam.WithAlpha(0.8f * (1f - f2)), 28);
                WwCrown(o, fxReduced ? 8 : 14, seed, T, 95f, 170f, 2.2f, 1f);
            }
            if (T < 0.16f)
                GlowDisc(o, R * 0.55f, WwFoam, 0.9f * (1f - T / 0.16f));
        }

        /// FxKind.WwBurst: the shot hisses to steam at Pos2; a mist billows out from the drop over a dashed ring; a wide spray of fine drops.
        void DrawWwBurstFx(Effect fx)
        {
            float T = fx.T, R = fx.Radius, f = Mathf.Clamp01(T / Mathf.Max(0.01f, fx.Dur));
            V2 o = fx.Pos, shot = fx.Pos2;
            float seed = o.X * 0.21f + o.Y * 0.53f;
            for (int i = 0; i < 9; i++)  // mist puffs drifting out and swelling
            {
                float a = WwHash(seed + i) * MathX.Tau, s = 40f + 50f * WwHash(seed + i * 2.3f), start = R * 0.35f * WwHash(seed + i * 4.1f);
                float life = 0.75f + 0.35f * WwHash(seed + i * 6.7f), k = Mathf.Clamp01(T / life);
                if (k >= 1f)
                    continue;
                V2 at = o + V2.FromAngle(a) * (start + s * (1f - Mathf.Exp(-3f * T)) / 3f * 2.2f);
                float r = Mathf.Lerp(8f, 26f + 14f * WwHash(seed + i * 8.9f), 1f - Mathf.Pow(1f - k, 3f));
                GlowDisc(at, r * 1.4f, WwFoam, 0.32f * (1f - k) * Mathf.Min(1f, k * 6f));
            }
            if (f < 0.7f)
            {
                float k = f / 0.7f, r = Mathf.Lerp(10f, R, 1f - Mathf.Pow(1f - k, 3f));
                FanGradient(o, r, WwFoam.WithAlpha(0.05f * (1f - k)), WwDeep.WithAlpha(0.18f * (1f - k)), 24);
                const int dashes = 18;  // the dashed ring, its dashes running round
                for (int i = 0; i < dashes; i++)
                    Arc(o, r, 2f, WwEdge.WithAlpha(0.85f * (1f - k)), 4, MathX.Tau * i / dashes + k * 0.6f, MathX.Tau / dashes * 0.55f);
            }
            if (T < 0.6f)
                WwCrown(o, fxReduced ? 8 : 16, seed, T, 190f, 75f, 1.3f, 1f);
            for (int i = 0; i < 5; i++)  // steam where the shot was
            {
                float life = 0.6f + 0.3f * WwHash(seed + i * 1.9f), k = Mathf.Clamp01(T / life);
                if (k >= 1f)
                    continue;
                V2 at = shot + new V2((WwHash(seed + i * 7.3f) - 0.5f) * 12f, -(20f + 25f * WwHash(seed + i)) * T);
                GlowDisc(at, (3f + 3f * WwHash(seed + i * 2.9f)) * (1f + 2f * k) * 1.6f, WwSteam, 0.5f * (1f - k));
            }
            if (T < 0.12f)
                GlowDisc(o, 22f, new Rgba(1f, 0.94f, 0.86f), 0.9f * (1f - T / 0.12f));
        }
    }
}
