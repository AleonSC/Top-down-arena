using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Senbon Seekers (Core/SenbonSeekers.cs), take C "Koi" of Docs/Mockups/senbon-seekers.html: steel needles with a red thread
    /// wrap and a venom-green tip school round the Ninja in a loose, breathing ring, pointing the way they swim. A cast throws
    /// the new ones out from the Ninja's body to their places; the others ease to the new spacing. A row of pips under the
    /// Ninja counts them (groups of 3). A needle in flight (Defs.Senbon) trails an ink tail with a thread of venom through it,
    /// kept from where this screen saw it. This screen runs its own clock, eased toward the host's.
    public partial class ArenaApp
    {
        static readonly Rgba SbSteelCol = C255(219, 228, 238), SbThreadCol = C255(194, 54, 43), SbVenomCol = C255(143, 209, 79),
            SbInkCol = C255(18, 20, 24), SbPipOff = new Rgba(1f, 1f, 1f, 0.16f);
        const int SbTrailMax = 14;

        class SbView
        {
            public float Clock, Seen = -1f, Age;
            public int N;
            public readonly float[] A = new float[Game.SbMaxMastered], R = new float[Game.SbMaxMastered];
            public readonly float[] OffA = new float[Game.SbMaxMastered], OffR = new float[Game.SbMaxMastered];
        }

        class SbTrail
        {
            public readonly List<V2> Pts = new List<V2>();
            public float SeenT;
        }

        readonly Dictionary<int, SbView> sbViews = new Dictionary<int, SbView>();
        readonly Dictionary<int, SbTrail> sbTrails = new Dictionary<int, SbTrail>();
        readonly List<int> sbStale = new List<int>();

        SbView SbViewOf(Player p, float dt)
        {
            SbView v;
            if (!sbViews.TryGetValue(p.Id, out v))
                sbViews[p.Id] = v = new SbView { Clock = p.SbClock };
            if (p.SbClock != v.Seen)
            {
                v.Seen = p.SbClock;
                v.Age = 0f;
            }
            else
                v.Age += dt;
            float est = p.SbClock + Mathf.Min(v.Age, 0.25f);
            v.Clock = Mathf.Abs(est - v.Clock) > 0.25f ? est : v.Clock + dt + (est - v.Clock) * Mathf.Min(1f, 8f * dt);
            int n = Mathf.Min(p.SbN, Game.SbMaxMastered);
            if (n != v.N)  // the count changed: the old ones ease to the new spacing, new ones come out of the body
            {
                float k = Mathf.Clamp01(p.SbNewT / Game.SbNewTime);  // (as far out as the cast's flourish has gone)
                for (int i = 0; i < n; i++)
                {
                    float a, r;
                    Game.SbSlot(i, n, v.Clock, out a, out r);
                    bool fresh = i >= v.N;
                    v.OffA[i] = fresh ? 0f : MathX.WrapPi(v.A[i] - a);
                    v.OffR[i] = fresh ? -r * 0.85f * k : v.R[i] - r;
                }
                v.N = n;
            }
            float decay = Mathf.Exp(-9f * dt);
            for (int i = 0; i < n; i++)
            {
                float a, r;
                Game.SbSlot(i, n, v.Clock, out a, out r);
                v.OffA[i] *= decay;
                v.OffR[i] *= decay;
                v.A[i] = a + v.OffA[i];
                v.R[i] = r + v.OffR[i];
            }
            return v;
        }

        /// Over the model: the needles held, and the count under the Ninja.
        void DrawSenbonRing(Player p, float t)
        {
            if (p.SbN <= 0)
            {
                sbViews.Remove(p.Id);
                return;
            }
            var v = SbViewOf(p, Time.deltaTime);
            V2 c = p.Disp;
            float flourish = Mathf.Clamp01(p.SbNewT / Game.SbNewTime);
            for (int i = 0; i < v.N; i++)
            {
                V2 at = c + V2.FromAngle(v.A[i]) * v.R[i];
                float swim = v.A[i] + MathX.Tau / 4f + 0.18f * Mathf.Sin(v.Clock * 3f + i * 1.3f);
                DrawSenbonNeedle(at, swim, 16f, 1f, flourish * 0.6f);
            }
            int max = Game.SbMaxOf(p), groups = (max + 2) / 3;
            float w = max * 5f + (groups - 1) * 3f;
            V2 o = c + new V2(-w / 2f, p.Def.BodyR + 9f);
            for (int j = 0; j < max; j++)
            {
                V2 a = o + new V2(j * 5f + (j / 3) * 3f, 0f);
                Line(a, a + new V2(3.5f, 0f), 2f, j < p.SbN ? SbSteelCol : SbPipOff);
            }
        }

        /// One needle: steel shaft, red thread wrap at the base, venom tip with a faint glint.
        void DrawSenbonNeedle(V2 mid, float ang, float len, float alpha, float flash)
        {
            V2 d = V2.FromAngle(ang);
            V2 tip = mid + d * (len * 0.55f), tail = mid - d * (len * 0.45f);
            GlowDisc(tip, 6f, SbVenomCol, (0.18f + flash) * alpha);
            Line(tail, tip, 2f, SbSteelCol.WithAlpha(alpha));
            Line(tail, tail + d * 3.5f, 2.6f, SbThreadCol.WithAlpha(alpha));
            Circle(tip, 1.4f, SbVenomCol.WithAlpha(alpha), 6);
        }

        /// A needle in flight: its ink tail (from where this screen saw it), then the needle along its heading.
        void DrawSenbonShot(Bullet b, float t)
        {
            SbTrail tr;
            if (!sbTrails.TryGetValue(b.Id, out tr))
                sbTrails[b.Id] = tr = new SbTrail();
            if (tr.Pts.Count == 0 || tr.Pts[tr.Pts.Count - 1].DistanceTo(b.Pos) > 3f)
            {
                tr.Pts.Add(b.Pos);
                if (tr.Pts.Count > SbTrailMax)
                    tr.Pts.RemoveAt(0);
            }
            tr.SeenT = t;
            V2 d = b.Vel.IsZero ? V2.FromAngle(0f) : b.Vel.Normalized();
            if (tr.Pts.Count < 3)  // just launched (or a still picture): a short straight tail
            {
                VTri(b.Pos - d * 34f, SbInkCol.WithAlpha(0f), b.Pos + d.Orthogonal() * 2f, SbInkCol.WithAlpha(0.8f), b.Pos - d.Orthogonal() * 2f, SbInkCol.WithAlpha(0.8f));
                Line(b.Pos - d * 26f, b.Pos, 1f, SbVenomCol.WithAlpha(0.35f));
            }
            else
                for (int j = 1; j < tr.Pts.Count; j++)
                {
                    float f = j / (float)tr.Pts.Count;
                    V2 a = tr.Pts[j - 1], e = j == tr.Pts.Count - 1 ? b.Pos : tr.Pts[j];
                    Line(a, e, 1f + f * 3.5f, SbInkCol.WithAlpha(f * 0.85f));
                    Line(a, e, 1f, SbVenomCol.WithAlpha(f * 0.4f));
                }
            DrawSenbonNeedle(b.Pos, d.Angle, 19f, 1f, 0.15f);
            if (sbTrails.Count > 24)  // forget the trails of needles gone
            {
                sbStale.Clear();
                foreach (var kv in sbTrails)
                    if (kv.Value.SeenT < t - 0.5f)
                        sbStale.Add(kv.Key);
                foreach (int id in sbStale)
                    sbTrails.Remove(id);
            }
        }
    }
}
