using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Summoner Tank's Golem Wall as the "Basalt" look (Docs/Mockups/golem-wall-rework.html, B): black volcanic golems that
    /// grind up out of the ground in a ring of dust, face the way the caster aimed, and show their wear: faint ember veins at
    /// full health that widen and glow harder as the golem is shot, cracks, embers drifting off a hurt one, and a flash on each
    /// hit. A hot seam on the ground joins neighbouring golems into one wall; a golem that is broken (or replaced) bursts into
    /// rubble. The golems have no timer (Minion.T = -1): health is Minion.GuardFrac (replicated), the rise is drawn from when
    /// the golem was first seen, and hits and breaks are found by watching it change.
    public partial class ArenaApp
    {
        class GolemVis
        {
            public float Frac = 1f, HitAt = -9f, Born, Aim;
            public V2 Pos;
        }

        readonly Dictionary<int, GolemVis> golemVis = new Dictionary<int, GolemVis>();
        readonly List<int> golemGone = new List<int>();
        readonly List<GolemRubble> golemRubble = new List<GolemRubble>();
        static readonly Rgba GwDark = new Rgba(0.1f, 0.1f, 0.12f), GwMid = new Rgba(0.17f, 0.165f, 0.2f), GwLight = new Rgba(0.25f, 0.24f, 0.29f);
        static readonly Rgba GwEdge = new Rgba(0.05f, 0.045f, 0.06f), GwEmber = new Rgba(1f, 0.48f, 0.12f), GwHot = new Rgba(1f, 0.76f, 0.35f), GwDust = new Rgba(0.68f, 0.62f, 0.52f);
        const float GwRise = 0.7f, GwRubbleLife = 2.4f;
        static readonly V2[] GwTorso = new V2[10], GwTmp = new V2[10];

        struct GolemRubble { public V2 Pos; public float At, Aim; public int Seed; }

        void ClearGolemWallLook() { golemVis.Clear(); golemRubble.Clear(); }

        static float GwRnd(float i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return x - (float)System.Math.Floor(x); }
        static float GwEase(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// Local (forward, sideways) to world, for a golem at `at` facing `aim`.
        static V2 GwAt(V2 at, V2 fwd, V2 side, float x, float y) => at + fwd * x + side * y;

        void GwFill(V2[] pts, int n, Rgba c)
        {
            V2 mid = default(V2);
            for (int i = 0; i < n; i++) mid = mid + pts[i];
            mid = mid * (1f / n);
            SetColor(c);
            for (int i = 0; i < n; i++)
                Tri(mid, pts[i], pts[(i + 1) % n]);
        }

        void GwOutline(V2[] pts, int n, float w, Rgba c)
        {
            for (int i = 0; i < n; i++)
                Line(pts[i], pts[(i + 1) % n], w, c);
        }

        /// A lumpy blob: n points round (cx, cy) with radii rx and ry, each pushed in or out a little by the seed.
        void GwBlob(V2 at, V2 fwd, V2 side, float cx, float cy, float rx, float ry, float seed, int n, V2[] into, float grow = 1f)
        {
            for (int i = 0; i < n; i++)
            {
                float a = MathX.Tau * i / n, w = (0.86f + GwRnd(seed + i * 3.1f) * 0.22f) * grow;
                into[i] = GwAt(at, fwd, side, cx + Mathf.Cos(a) * rx * w, cy + Mathf.Sin(a) * ry * w);
            }
        }

        /// Once a frame after the minions are drawn: a golem that is gone bursts into rubble.
        void SweepGolemWalls(float t)
        {
            golemGone.Clear();
            foreach (var kv in golemVis)
                if (!game.Minions.TryGetValue(kv.Key, out var m) || m.Kind != MinionKind.Wall)
                    golemGone.Add(kv.Key);
            foreach (int id in golemGone)
            {
                var v = golemVis[id];
                golemVis.Remove(id);
                if (!Juicy)
                    continue;
                golemRubble.Add(new GolemRubble { Pos = v.Pos, At = t, Aim = v.Aim, Seed = id });
                Sparks(v.Pos, GwEmber, 10, 220f, 0.6f, 2.6f);
                Sparks(v.Pos, GwDust, 8, 150f, 0.7f, 3.4f);
                Ring(v.Pos, 54f, 0.35f, GwHot);
            }
            for (int i = golemRubble.Count - 1; i >= 0; i--)
            {
                if (t - golemRubble[i].At > GwRubbleLife)
                    golemRubble.RemoveAt(i);
                else
                    DrawGolemRubble(golemRubble[i], t - golemRubble[i].At);
            }
        }

        void DrawGolemRubble(GolemRubble r, float since)
        {
            float R = 38f, fade = since < 1.4f ? 1f : Mathf.Clamp01(1f - (since - 1.4f) / (GwRubbleLife - 1.4f));
            float k = GwEase(since / 0.35f);
            if (since < 0.9f)
                Circle(r.Pos, R * (0.7f + since), GwDust.WithAlpha(0.3f * (1f - since / 0.9f)), 20);
            for (int i = 0; i < 12; i++)
            {
                float a = GwRnd(r.Seed + i) * MathX.Tau, far = (0.3f + GwRnd(r.Seed + i + 50f) * 0.7f) * R * 0.95f, sz = R * (0.12f + GwRnd(r.Seed + i + 7f) * 0.14f);
                V2 at = r.Pos + V2.FromAngle(a) * (far * k);
                float rot = a * 3f + k * 2f;
                V2 u = V2.FromAngle(rot), w = u.Orthogonal();
                GwTmp[0] = at - u * sz - w * (sz * 0.7f); GwTmp[1] = at + u * (sz * 0.9f) - w * (sz * 0.4f);
                GwTmp[2] = at + u * (sz * 0.6f) + w * (sz * 0.8f); GwTmp[3] = at - u * (sz * 0.8f) + w * (sz * 0.5f);
                GwFill(GwTmp, 4, (i % 3 == 0 ? GwDark : GwMid).WithAlpha(fade));
                GwOutline(GwTmp, 4, 1f, GwEdge.WithAlpha(fade));
            }
            if (since < 1f)  // a few embers where it fell
                for (int i = 0; i < 6; i++)
                    Circle(r.Pos + new V2(Mathf.Cos(i * 1.1f) * R * 0.4f, Mathf.Sin(i * 1.1f) * R * 0.4f - since * 40f), 2.2f, GwEmber.WithAlpha(0.8f * (1f - since)), 6);
        }

        void DrawGolemWall(Minion m, float t)
        {
            float R = m.Def.Radius * m.Scale;
            float frac = Mathf.Clamp01(m.GuardFrac);
            if (!golemVis.TryGetValue(m.Id, out var v))
                golemVis[m.Id] = v = new GolemVis { Frac = frac, Born = t };
            if (frac < v.Frac - 0.001f)
            {
                v.HitAt = t;
                if (Juicy)
                {
                    Sparks(m.Disp, GwEmber, 4, 170f, 0.3f, 1.8f);
                    Sparks(m.Disp, GwHot, 2, 120f, 0.25f, 1.6f);
                }
            }
            v.Frac = frac;
            v.Pos = m.Disp;
            v.Aim = m.Aim;

            float age = t - v.Born, rise = GwEase(age / GwRise);
            float hit = Mathf.Clamp01(1f - (t - v.HitAt) / 0.14f), dmg = 1f - frac;
            V2 fwd = V2.FromAngle(m.Aim), side = fwd.Orthogonal();
            float s = 0.55f + 0.45f * rise, a = Mathf.Clamp01(rise * 1.6f);
            V2 at = m.Disp;
            float seed = m.Id * 5.3f;

            Circle(at + new V2(4f, 6f), R * 1.02f * rise, new Rgba(0f, 0f, 0f, 0.38f), 24);  // shadow
            if (age < GwRise)  // grinding up out of the ground: a dust ring and puffs
            {
                float q = age / GwRise;
                Arc(at, R * (0.7f + q * 1.1f), 3f, GwDust.WithAlpha(0.5f * (1f - q)), 28);
                for (int i = 0; i < 9; i++)
                {
                    float ang = i / 9f * MathX.Tau + GwRnd(i + seed) * 0.5f, dist = R * (0.8f + q * (0.9f + GwRnd(i + 9f + seed) * 0.6f));
                    Circle(at + V2.FromAngle(ang) * dist, 4f + 7f * q, GwDust.WithAlpha(0.45f * (1f - q)), 8);
                }
            }
            DrawGolemSeam(m, at, R, rise, t);

            // Arms and fists, then the torso, then the head. Everything is built from the golem's own frame: x ahead, y to the side.
            for (int sd = -1; sd <= 1; sd += 2)
            {
                GwBlob(at, fwd, side, R * 0.15f * s, sd * R * 0.7f * s, R * 0.42f * s, R * 0.26f * s, seed + sd * 11f, 8, GwTmp);
                GwFill(GwTmp, 8, GwDark.WithAlpha(a));
                GwBlob(at, fwd, side, R * 0.55f * s, sd * R * 0.8f * s, R * 0.3f * s, R * 0.3f * s, seed + sd * 21f, 8, GwTmp);
                GwFill(GwTmp, 8, GwMid.WithAlpha(a));
                GwOutline(GwTmp, 8, 1.5f, GwEdge.WithAlpha(a));
            }
            GwBlob(at, fwd, side, -R * 0.08f * s, 0f, R * 0.78f * s, R * 0.74f * s, seed, 10, GwTorso);
            GwFill(GwTorso, 10, GwMid.WithAlpha(a));
            GwBlob(at, fwd, side, -R * 0.2f * s, -R * 0.12f * s, R * 0.5f * s, R * 0.46f * s, seed, 10, GwTmp);  // a lighter plate catching the light, back and to one side
            GwFill(GwTmp, 10, GwLight.WithAlpha(0.55f * a));
            GwOutline(GwTorso, 10, 2f, GwEdge.WithAlpha(a));
            Line(GwAt(at, fwd, side, -R * 0.55f * s, -R * 0.35f * s), GwAt(at, fwd, side, R * 0.1f * s, -R * 0.45f * s), 1.5f, GwEdge.WithAlpha(0.55f * a));
            Line(GwAt(at, fwd, side, -R * 0.6f * s, R * 0.3f * s), GwAt(at, fwd, side, R * 0.05f * s, R * 0.42f * s), 1.5f, GwEdge.WithAlpha(0.55f * a));
            GwBlob(at, fwd, side, R * 0.42f * s, 0f, R * 0.3f * s, R * 0.28f * s, seed + 5f, 7, GwTmp);
            GwFill(GwTmp, 7, GwMid.WithAlpha(a));
            GwOutline(GwTmp, 7, 1.5f, GwEdge.WithAlpha(a));
            float eyes = (0.5f + 0.5f * frac) * a;  // the eyes gutter as it nears breaking
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 e = GwAt(at, fwd, side, R * 0.56f * s, sd * R * 0.15f * s);
                if (Juicy && !fxReduced)
                    Circle(e, 6f, GwEmber.WithAlpha(0.25f * eyes), 8);
                Circle(e, 2.6f, GwHot.WithAlpha(eyes), 8);
            }

            // Ember veins: faint when whole, wider and hotter with every hit.
            float heat = Mathf.Clamp01(0.28f + dmg * 0.9f), vw = 1.6f + dmg * 2.4f;
            var vein = new Rgba(1f, Mathf.Lerp(0.6f, 0.32f, dmg), 0.16f, heat * a);
            for (int i = 0; i < 3; i++)
                Line(GwAt(at, fwd, side, GwCrack(0, i, 0) * R * s, GwCrack(0, i, 1) * R * s), GwAt(at, fwd, side, GwCrack(0, i, 2) * R * 0.9f * s, GwCrack(0, i, 3) * R * 0.9f * s), vw, vein);
            int cracks = Mathf.Min(8, (int)(dmg * 8f + 0.0001f));  // and a new crack every eighth of its health
            for (int i = 0; i < cracks; i++)
                Line(GwAt(at, fwd, side, GwCrack(1, i, 0) * R, GwCrack(1, i, 1) * R), GwAt(at, fwd, side, GwCrack(1, i, 2) * R, GwCrack(1, i, 3) * R), 2f, new Rgba(1f, 0.67f, 0.27f, 0.9f * a));
            if (Juicy && !fxReduced && dmg > 0.4f && GwRnd((float)System.Math.Floor(t * 6f) + m.Id) < dmg * 0.5f)  // embers drifting off a hurt golem
                Circle(at + new V2(Mathf.Sin(t * 3f + m.Id) * R * 0.5f, -((t * 6f) % 1f) * 26f), 2f, GwEmber.WithAlpha(0.8f), 6);
            if (hit > 0f)  // the hit flash
                GwFill(GwTorso, 10, Rgba.White.WithAlpha(0.5f * hit));

            if (frac < 0.999f && rise >= 1f)  // health: a short bar under it
            {
                const float w = 48f;
                V2 b0 = at + new V2(-w / 2f, R + 10f);
                Line(b0 + new V2(-1f, 0f), b0 + new V2(w + 1f, 0f), 6f, new Rgba(0f, 0f, 0f, 0.6f));
                Line(b0, b0 + new V2(w * frac, 0f), 4f, frac > 0.5f ? new Rgba(0.44f, 0.89f, 0.77f) : frac > 0.25f ? new Rgba(1f, 0.71f, 0.28f) : new Rgba(1f, 0.42f, 0.42f));
            }
        }

        /// The ground seam to the next golem along the wall: glowing, and gone when either one is.
        void DrawGolemSeam(Minion m, V2 at, float R, float rise, float t)
        {
            if (rise < 1f)
                return;
            foreach (var o in game.Minions.Values)
            {
                if (o.Kind != MinionKind.Wall || o.Owner != m.Owner || o.Id <= m.Id || o.Disp.DistanceTo(at) > R * 2.4f || o.GuardFrac <= 0f)
                    continue;
                V2 dir = (o.Disp - at).Normalized(), side = dir.Orthogonal();
                float heat = 0.45f + 0.15f * Mathf.Sin(t * 3f + m.Id);
                Line(at + dir * (R * 0.55f) - side * 3f, o.Disp - dir * (R * 0.55f) - side * 3f, 7f, GwEmber.WithAlpha(0.14f));
                Line(at + dir * (R * 0.55f) - side * 3f, o.Disp - dir * (R * 0.55f) - side * 3f, 3f, GwEmber.WithAlpha(heat));
            }
        }

        /// A fixed set of crack and vein strokes, the same for every golem (they differ by where the golem faces).
        static float GwCrack(int row, int i, int k)
        {
            float a = GwRnd(row * 31f + i * 7f + 3f) * MathX.Tau, r0 = 0.1f + GwRnd(row * 5f + i + 1f) * 0.35f;
            float b = a + (GwRnd(i + row * 13f + 2f) - 0.5f) * 1.6f;
            switch (k)
            {
                case 0: return Mathf.Cos(a) * r0;
                case 1: return Mathf.Sin(a) * r0;
                case 2: return Mathf.Cos(b) * (r0 + 0.35f);
                default: return Mathf.Sin(b) * (r0 + 0.35f);
            }
        }
    }
}
