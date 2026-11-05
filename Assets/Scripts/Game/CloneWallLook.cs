using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Shadow Clone's wall as the "Afterimage Phalanx" look (Docs/Mockups/shadow-clone-wall.html, C): the ninja splits into
    /// afterimages that dash out with trails and lock into line with a ring and a flash, a thin energy fence links neighbouring
    /// clones (and closes over a gap), hits glitch a clone with a cyan and magenta split and a bright slice, damage makes it
    /// jitter, and a clone that is destroyed shatters. Health is Minion.GuardFrac (replicated); the dash, landing, fence and
    /// glitch are drawn from the clone's age (Duration - T), and hits and shatters are found by watching it change.
    public partial class ArenaApp
    {
        class CloneVis
        {
            public float Frac = 1f, HitAt = -9f, LastT;
            public V2 Pos;
        }

        readonly Dictionary<int, CloneVis> cloneVis = new Dictionary<int, CloneVis>();
        readonly List<int> cloneGone = new List<int>();
        static readonly Rgba CwCyan = new Rgba(0.22f, 0.82f, 0.92f), CwMag = new Rgba(1f, 0.24f, 0.65f), CwIce = new Rgba(0.75f, 0.96f, 1f), CwBody = new Rgba(0.04f, 0.16f, 0.2f);
        const float CwDash = 0.34f;

        void ClearCloneWallLook() => cloneVis.Clear();

        static float CwEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        /// Once a frame after the minions are drawn: a clone that is gone shatters if it was broken (not just timed out).
        void SweepCloneWalls()
        {
            cloneGone.Clear();
            foreach (var kv in cloneVis)
                if (!game.Minions.TryGetValue(kv.Key, out var m) || m.Kind != MinionKind.CloneWall)
                    cloneGone.Add(kv.Key);
            foreach (int id in cloneGone)
            {
                var v = cloneVis[id];
                cloneVis.Remove(id);
                if (!Juicy || v.LastT < 0.4f)
                    continue;
                Sparks(v.Pos, CwCyan, 10, 250f, 0.6f, 2.4f);
                Sparks(v.Pos, CwMag, 8, 250f, 0.6f, 2.4f);
                Sparks(v.Pos, CwIce, 5, 160f, 0.4f, 3f);
                Ring(v.Pos, 40f, 0.35f, CwIce);
            }
        }

        void CwBodyAt(V2 at, float r, float a, float sp, float aim, float t, float seed)
        {
            Circle(at + new V2(-sp, 0f), r, CwCyan.WithAlpha(0.5f * a), 20);
            Circle(at + new V2(sp, 0f), r, CwMag.WithAlpha(0.45f * a), 20);
            Circle(at, r, CwBody.WithAlpha(a), 24);
            Arc(at, r, 2f, CwCyan.WithAlpha(a), 24);
            V2 dir = V2.FromAngle(aim);
            Arc(at, r * 0.62f, 4f, CwMag.WithAlpha(0.8f * a), 16, aim - 1.1f, 2.2f);  // the headband
            Line(at - dir * (r * 0.6f), at - dir * (r * 0.6f + 16f) + dir.Orthogonal() * (4f * Mathf.Sin(t * 9f + seed)), 2f, CwMag.WithAlpha(0.7f * a));  // the scarf
            Line(at + dir * (r * 0.25f) - dir.Orthogonal() * (r * 0.3f), at + dir * (r * 0.25f) + dir.Orthogonal() * (r * 0.3f), 2f, CwIce.WithAlpha(a));  // the eye slit
        }

        void DrawCloneWall(Minion m, float t)
        {
            bool has = game.Players.TryGetValue(m.Owner, out var owner);
            float r = m.Def.Radius;
            V2 side = V2.FromAngle(m.Aim).Orthogonal();
            float lateral = has ? Mathf.Abs((m.Disp - owner.Disp).Dot(side)) : 0f;
            float age = (m.Duration - m.T) - lateral * 0.0004f;  // centre first, then outwards
            if (age < 0f)
                return;
            float frac = Mathf.Clamp01(m.GuardFrac);
            if (!cloneVis.TryGetValue(m.Id, out var v))
                cloneVis[m.Id] = v = new CloneVis { Frac = frac };
            if (frac < v.Frac - 0.001f)
            {
                v.HitAt = t;
                if (Juicy)
                {
                    Sparks(m.Disp, CwCyan, 5, 170f, 0.3f, 1.8f);
                    Sparks(m.Disp, CwMag, 3, 170f, 0.3f, 1.8f);
                }
            }
            v.Frac = frac;
            v.LastT = m.T;
            v.Pos = m.Disp;

            float hit = Mathf.Clamp01(1f - (t - v.HitAt) / 0.22f);
            float alpha = Mathf.Clamp01(age * 6f) * (0.88f + 0.12f * Mathf.Sin(t * 30f + m.Id));
            if (m.T < 0.35f)  // timing out: flickers away
                alpha *= Mathf.Clamp01(m.T / 0.35f) * (Mathf.Sin(t * 90f) > 0f ? 1f : 0.4f);
            float jit = (1f - frac) * 2.2f + hit * 4f;
            V2 at = m.Disp + new V2(jit > 0f ? Mathf.Sin(t * 97f + m.Id * 3.1f) * jit : 0f, 0f);

            if (age < CwDash && has)  // dashing out from the ninja, leaving afterimages
            {
                at = V2.Lerp(owner.Disp, m.Disp, CwEase(age / CwDash));
                for (int g = 4; g >= 1; g--)
                {
                    V2 gp = V2.Lerp(owner.Disp, m.Disp, CwEase((age - g * 0.035f) / CwDash));
                    CwBodyAt(gp, r, 0.4f * (1f - g / 5f) * alpha, 1.2f, m.Aim, t, m.Id);
                }
            }
            else
            {
                float la = (age - CwDash) / 0.4f;
                if (la < 1f)  // lands: a ring and a flash
                {
                    Arc(m.Disp, r * 2.4f * CwEase(la), 3f * (1f - la), CwIce.WithAlpha(1f - la), 24);
                    Line(m.Disp, m.Disp + new V2(0f, -60f * (1f - la)), 6f * (1f - la), CwIce.WithAlpha(0.8f * (1f - la)));
                }
                DrawCloneFence(m, side, r, Mathf.Min(1f, (age - CwDash) / 0.25f) * alpha, t);
            }

            CwBodyAt(at, r, alpha, 1.6f + hit * 4f + (1f - frac) * 2.5f, m.Aim, t, m.Id);
            for (int i = 0; i < 3; i++)  // scan lines crawling across it
            {
                float y = (Mathf.Repeat(i / 3f + t * 0.8f, 1f) * 2f - 1f) * r;
                float hw = Mathf.Sqrt(Mathf.Max(0f, r * r - y * y)) * 0.9f;
                Line(at + new V2(-hw, y), at + new V2(hw, y), 1f, CwIce.WithAlpha(0.22f * alpha));
            }
            if (hit > 0f)  // the glitch slice
            {
                float y = Mathf.Sin(m.Id * 12.9f + v.HitAt * 78f) * r * 0.7f;
                Line(at + new V2(-r - 2f, y), at + new V2(r + 2f, y), 3f, Rgba.White.WithAlpha(0.6f * hit));
            }
            if (frac < 0.999f)
            {
                Arc(m.Disp, r + 5f, 2.2f, Rgba.White.WithAlpha(0.14f), 24);
                Arc(m.Disp, r + 5f, 2.2f, CwCyan.WithAlpha(0.9f * alpha), 24, -MathX.Pi / 2f, MathX.Tau * frac);
            }
        }

        /// The fence from this clone to the next one along the wall (to its right), so each pair is drawn once. A missing clone
        /// leaves a gap of two spacings, which the fence still closes.
        void DrawCloneFence(Minion m, V2 side, float r, float grow, float t)
        {
            Minion next = null;
            float best = 1e9f;
            foreach (var o in game.Minions.Values)
            {
                if (o.Kind != MinionKind.CloneWall || o.Owner != m.Owner || o.Id == m.Id || o.Duration - o.T < CwDash)
                    continue;
                float along = (o.Disp - m.Disp).Dot(side);
                if (along > 0f && along < best && o.Disp.DistanceTo(m.Disp) < r * 5f)
                {
                    best = along;
                    next = o;
                }
            }
            if (next == null || grow <= 0f)
                return;
            V2 a = m.Disp, b = V2.Lerp(m.Disp, next.Disp, Mathf.Clamp01(grow));
            Line(a, b, 7f, CwCyan.WithAlpha(0.18f * grow));
            float len = (b - a).Length;
            V2 dir = len > 0.01f ? (b - a) / len : new V2(1f, 0f);
            float off = Mathf.Repeat(t * 60f, 11f);
            for (float d = -off; d < len; d += 11f)  // marching dashes
            {
                float s0 = Mathf.Max(0f, d), s1 = Mathf.Min(len, d + 6f);
                if (s1 > s0)
                    Line(a + dir * s0, a + dir * s1, 1.6f, CwIce.WithAlpha(grow));
            }
            Line(a + new V2(0f, -10f), b + new V2(0f, -10f), 1f, CwMag.WithAlpha(0.4f * grow));
        }
    }
}
