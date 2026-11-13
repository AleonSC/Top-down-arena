using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Burrower (Burrower.cs; Docs/Mockups/burrower-visuals.html, look A "dirt mound"), drawn from the minion's age:
    /// cracks creep out of the ground at the cursor (telegraph), it breaks out in a burst of rock and dust and rises with its
    /// mandibles open (breach), dives, then shows only as a moving mound of soil with cracks fanning out ahead, dust puffs
    /// behind and a dark scar that fades over 2.4 s (tunnel). When its run ends the mound shrinks into the ground. The scar and
    /// puffs come from a short history of where it has been, kept here per minion. FxKind.BurrowSnap is the head popping up
    /// under an enemy it bit. Reduced effects drop the rock, the puffs and the dust rings and keep the mound and the scar.
    public partial class ArenaApp
    {
        struct BurrowStep { public V2 P; public float T; }

        readonly Dictionary<int, List<BurrowStep>> burrowTrails = new Dictionary<int, List<BurrowStep>>();

        static readonly Rgba BurrowDark = new Rgba(0.28f, 0.23f, 0.16f), BurrowMid = new Rgba(0.48f, 0.39f, 0.27f), BurrowLight = new Rgba(0.64f, 0.54f, 0.38f),
            BurrowScar = new Rgba(0.12f, 0.09f, 0.06f), BurrowDust = new Rgba(0.73f, 0.64f, 0.48f), BurrowEye = new Rgba(1f, 0.82f, 0.4f), BurrowJaw = new Rgba(0.15f, 0.15f, 0.17f);

        static V2 BurrowAt(V2 o, float ang, float fwd, float side)
        {
            V2 f = V2.FromAngle(ang);
            return o + f * fwd + f.Orthogonal() * side;
        }

        void BurrowEllipse(V2 c, float rx, float ry, float rot, Rgba col)
        {
            if (rx <= 0.5f || ry <= 0.5f)
                return;
            SetColor(col);
            V2 prev = BurrowAt(c, rot, rx, 0f);
            for (int i = 1; i <= 18; i++)
            {
                float a = MathX.Tau * i / 18f;
                V2 next = BurrowAt(c, rot, Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Tri(c, prev, next);
                prev = next;
            }
        }

        void BurrowMandibles(V2 o, float ang, float open, float scale)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                V2 p0 = BurrowAt(o, ang, 8f * scale, s * 5f * scale), p1 = BurrowAt(o, ang, 26f * scale, s * (11f + open * 9f) * scale),
                    p2 = BurrowAt(o, ang, 36f * scale, s * (3f + open * 12f) * scale), p3 = BurrowAt(o, ang, 24f * scale, s * (7f + open * 4f) * scale);
                SetColor(BurrowJaw);
                Quad(p0, p1, p2, p3);
                Line(p0, p1, 1.6f, BurrowLight);
                Line(p1, p2, 1.6f, BurrowLight);
            }
        }

        /// A head rising out of the ground at `o`, u 0..1 (how far up it is), facing `ang`.
        void BurrowHead(V2 o, float ang, float u, float open)
        {
            if (u < 0.03f)
                return;
            V2 up = o + new V2(0f, -14f * u);
            Circle(o + new V2(0f, 6f), 22f * u, new Rgba(0f, 0f, 0f, 0.35f), 16);
            float k = Mathf.Min(1f, u + 0.3f);
            BurrowEllipse(up, 22f * k, 17f * k, ang, BurrowDark);
            BurrowEllipse(up + new V2(-2f, -2f) * k, 18f * k, 13f * k, ang, BurrowMid);
            BurrowEllipse(up + new V2(-5f, -5f) * k, 10f * k, 6f * k, ang, BurrowLight);
            Circle(BurrowAt(up, ang, 9f, 6f), 2.2f, BurrowEye, 6);
            Circle(BurrowAt(up, ang, 9f, -6f), 2.2f, BurrowEye, 6);
            BurrowMandibles(up, ang, open, 0.45f + 0.55f * u);
        }

        /// Chunks of rock thrown up from `o` that fall back and settle, `age` seconds in.
        void BurrowDebris(V2 o, float age, int n, float seed, float speed, float life)
        {
            if (fxReduced || age < 0f || age > life)
                return;
            float fade = 1f - age / life;
            for (int i = 0; i < n; i++)
            {
                float ang = TH(seed + i) * MathX.Tau, sp = speed * (0.45f + TH(seed + i + 50f) * 0.7f), v0 = 110f + TH(seed + i + 99f) * 150f;
                float x = o.X + Mathf.Cos(ang) * sp * age, y = o.Y + Mathf.Sin(ang) * sp * age * 0.55f, z = v0 * age - 190f * age * age;
                float r = 2f + TH(seed + i + 7f) * 3f;
                if (z < 0f && age > 0.15f)
                {
                    Circle(new V2(x, y), r, new Rgba(0.23f, 0.18f, 0.13f, fade), 6);
                    continue;
                }
                Circle(new V2(x, y + 2f), r, new Rgba(0f, 0f, 0f, 0.3f * fade), 6);
                Circle(new V2(x, y - Mathf.Max(0f, z)), r, (TH(seed + i + 3f) > 0.5f ? BurrowMid : BurrowDark).WithAlpha(fade), 6);
                Circle(new V2(x - r * 0.3f, y - Mathf.Max(0f, z) - r * 0.3f), r * 0.45f, BurrowLight.WithAlpha(0.7f * fade), 5);
            }
        }

        void BurrowDustRing(V2 o, float age, float radius)
        {
            if (fxReduced || age < 0f || age > 0.7f)
                return;
            float k = age / 0.7f;
            Arc(o, radius * TEase(age / 0.5f), 6f * (1f - k) + 1f, BurrowDust.WithAlpha(0.5f * (1f - k)), 32);
            Arc(o, radius * 0.7f * TEase(age / 0.5f), 2f, BurrowMid.WithAlpha(0.35f * (1f - k)), 32);
        }

        void DrawBurrower(Minion m, float t)
        {
            float age = m.Duration > 0f ? m.Duration - m.T : 1f;
            float runEnd = Game.BurrowerSurface + Game.BurrowerRun;
            V2 at = m.Disp;
            float seed = m.Id * 3.7f;
            if (!burrowTrails.TryGetValue(m.Id, out var trail))
            {
                trail = new List<BurrowStep>();
                burrowTrails[m.Id] = trail;
                if (burrowTrails.Count > 8)  // forget the minions that are gone
                {
                    var gone = DrawScratch.FreshIds();
                    foreach (int id in burrowTrails.Keys)
                        if (!game.Minions.ContainsKey(id))
                            gone.Add(id);
                    foreach (int id in gone)
                        burrowTrails.Remove(id);
                }
            }
            if (age >= Game.BurrowerSurface && age <= runEnd + 0.05f && (trail.Count == 0 || trail[trail.Count - 1].P.DistanceTo(at) > 6f))
                trail.Add(new BurrowStep { P = at, T = t });
            while (trail.Count > 0 && t - trail[0].T > 2.6f)
                trail.RemoveAt(0);

            // the scar it leaves, and the dust it kicks up
            for (int i = 0; i < trail.Count; i++)
            {
                float sa = t - trail[i].T, a = 0.8f * (1f - Mathf.Clamp01(sa / 2.4f));
                if (a <= 0f)
                    continue;
                V2 p = trail[i].P;
                Circle(p, Mathf.Max(6f, 14f - sa * 2f), BurrowScar.WithAlpha(a), 10);
                if (TH(seed + i) > 0.5f)
                    Circle(p + new V2(TH(seed + i + 1f) - 0.5f, TH(seed + i + 2f) - 0.5f) * 22f, 2f, BurrowMid.WithAlpha(a), 5);
                if (!fxReduced && sa < 0.9f)
                {
                    float k = sa / 0.9f;
                    Circle(p + new V2((TH(seed + i + 4f) - 0.5f) * 30f * k, (TH(seed + i + 5f) - 0.5f) * 14f * k - 10f * k), 6f + 20f * k, BurrowDust.WithAlpha(0.3f * (1f - k)), 10);
                }
            }

            // telegraph: cracks creep out of the ground where it will break out
            if (age < Game.BurrowerTelegraph + 0.05f)
            {
                float k = Mathf.Clamp01(age / Game.BurrowerTelegraph);
                Arc(at, 34f - 14f * k + 2f * Mathf.Sin(t * 24f), 2f, new Rgba(1f, 0.82f, 0.48f, 0.35f + 0.4f * k), 28);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * MathX.Tau / 6f + 0.3f, len = 10f + 26f * k * (0.7f + TH(seed + i) * 0.6f);
                    Line(at, at + V2.FromAngle(a) * len, 3f, BurrowScar.WithAlpha(0.9f));
                }
            }

            // breach: rock and dust, and the head rising
            float rel = age - Game.BurrowerTelegraph;
            BurrowDustRing(at, rel, 70f);
            BurrowDebris(at, rel, 16, seed + 11f, 90f, 0.9f);
            if (rel >= 0f && rel < 0.6f)
            {
                float u = rel < 0.3f ? TEase(rel / 0.3f) : rel < 0.35f ? 1f : 1f - TEase((rel - 0.35f) / 0.25f);
                BurrowHead(at, m.Aim, u, u > 0.6f ? 1f : 0.3f);
            }

            // the end of the run: the mound shrinks into the ground in a last burst
            float sinkAge = Game.BurrowerSink - m.T;
            if (age >= runEnd)
            {
                BurrowDustRing(at, sinkAge, 50f);
                BurrowDebris(at, sinkAge, 9, seed + 40f, 60f, 0.8f);
            }

            // tunnelling: a ridge of soil with cracks fanning out ahead
            float sc = 0f;
            if (age >= Game.BurrowerSurface)
                sc = Mathf.Min(Mathf.Clamp01((age - Game.BurrowerSurface) / 0.2f), age < runEnd ? 1f : Mathf.Clamp01(1f - sinkAge / 0.5f));
            if (sc <= 0.01f)
                return;
            for (int j = 8; j >= 1; j--)
            {
                int n = trail.Count - 1 - j * 2;
                if (n < 1)
                    continue;
                float r = (1f - j / 10f) * 13f * sc, ra = (trail[n].P - trail[n - 1].P).Angle;
                BurrowEllipse(trail[n].P, r * 1.5f, r, ra, BurrowMid.WithAlpha(0.55f * (1f - j / 10f)));
            }
            float ang = m.Aim;
            BurrowEllipse(at + new V2(1f, 7f) * sc, 31f * sc, 17f * sc, ang, new Rgba(0f, 0f, 0f, 0.38f));
            BurrowEllipse(at, 28f * sc, 16f * sc, ang, BurrowDark);
            BurrowEllipse(BurrowAt(at, ang, -2f * sc, -3f * sc), 22f * sc, 12.5f * sc, ang, BurrowMid);
            BurrowEllipse(BurrowAt(at, ang, -5f * sc, -6f * sc), 14f * sc, 7f * sc, ang, BurrowLight);
            for (int i = -2; i <= 2; i++)
                Line(BurrowAt(at, ang, 26f * sc, i * 5f * sc), BurrowAt(at, ang, (40f + TH(seed + i + 3f) * 10f) * sc, i * 11f * sc), 1.8f, BurrowScar.WithAlpha(0.8f));
        }

        /// FxKind.BurrowSnap: the head pops up under an enemy it bit (Pos = the enemy, Pos2 = the Burrower), with a burst of rock.
        void DrawBurrowSnapFx(Effect fx)
        {
            float age = fx.T;
            V2 o = fx.Pos2;
            float seed = fx.Pos.X * 0.017f + fx.Pos.Y * 0.031f;
            float ang = (fx.Pos - fx.Pos2).Length > 1f ? (fx.Pos - fx.Pos2).Angle : 0f;
            if (age < 0.42f)
            {
                float u = Mathf.Sin(Mathf.PI * age / 0.42f);
                BurrowHead(o, ang, u, u > 0.5f ? 1f : 0.2f);
            }
            BurrowDustRing(o, age, 46f);
            BurrowDebris(o, age, 8, seed, 70f, 0.8f);
        }
    }
}
