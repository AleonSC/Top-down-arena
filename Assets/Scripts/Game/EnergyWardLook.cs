using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Healer Elementalist's Energy Ward as a bubble of energy, Heal core (4) with Jelly hits (1) of
    /// Docs/Mockups/energy-ward-shrink.html, ported from the mockup's numbers.
    ///
    /// The bubble's radius is a spring chasing the shield: it shrinks to 55% as the shield drains, or as the last 2.5 s
    /// run out. Each hit knocks the spring down, squashes the bubble toward where the shot came from and sets the rim
    /// wobbling, then it settles a little smaller. The shell goes from blue to green between half and a fifth of the
    /// shield, and a green core grows and beats inside as the shell shrinks: that core is the heal charging up. When the
    /// ward breaks or runs out a green ring and motes play where the bubble was.
    ///
    /// Drawing only, with Visual effects on (off, WorldRenderer draws the old ring). The fraction comes from the
    /// replicated ShieldHp, EnergyMax and ShieldT; the hit direction is guessed on this screen as toward the nearest enemy,
    /// so nothing else is synced.
    public partial class ArenaApp
    {
        const float EnergyRatio = 3.4f;     // the full bubble's radius, in body radii
        const float EnergyMinSize = 0.55f;  // how small it gets
        const float EnergySpringK = 90f, EnergySpringC = 5f;

        class EnergyWardState
        {
            public float R, Rv, Q, Qv, From, Wob0, Wob1, Wph, PrevHp, Last = -9f, LastHit = -9f, End = -9f, Soaked;
            public bool Active, Started;
        }

        readonly Dictionary<int, EnergyWardState> energyWard = new Dictionary<int, EnergyWardState>();

        static float EnergyFrac(Player p) =>
            Mathf.Min(Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.EnergyMax)), Mathf.Clamp01(p.ShieldT / 2.5f));

        /// Where, on this screen, the nearest enemy is: a hit is drawn as coming from there.
        float EnergyHitAngle(Player p)
        {
            float best = float.MaxValue, ang = p.Aim + MathX.Pi;
            foreach (var e in game.Enemies.Values)
            {
                float d = (e.Disp - p.Disp).Length;
                if (d < best)
                {
                    best = d;
                    ang = (e.Disp - p.Disp).Angle;
                }
            }
            return ang;
        }

        /// Under a player, from DrawPlayerAuras: the bubble while the ward is up, and the way it goes.
        void DrawEnergyWard(Player p, float t)
        {
            bool on = p.ShieldHp > 0f && p.EnergyMax > 0f;
            EnergyWardState st;
            if (!energyWard.TryGetValue(p.Id, out st))
            {
                if (!on)
                    return;
                energyWard[p.Id] = st = new EnergyWardState();
            }
            float body = p.Def.BodyR, full = body * EnergyRatio;
            float dt = Mathf.Clamp(t - st.Last, 0f, 0.05f);
            if (t - st.Last > 0.5f || t < st.Last)
                st.Started = false;  // not drawn for a while: a fresh look, not a continued one
            st.Last = t;

            if (on)
            {
                float f = EnergyFrac(p), target = full * Mathf.Lerp(EnergyMinSize, 1f, f);
                if (!st.Started || !st.Active)
                {
                    st.R = full * 0.3f;  // grows in at the cast
                    st.Rv = st.Q = st.Qv = st.Wob0 = st.Wob1 = 0f;
                    st.PrevHp = p.ShieldHp;
                    st.Started = true;
                    st.Soaked = 0f;
                    st.End = -9f;
                }
                float drop = st.PrevHp - p.ShieldHp;
                if (drop > 0.5f)
                {
                    float k = Mathf.Min(2f, drop / 10f), sc = full / 64f;
                    st.From = EnergyHitAngle(p);
                    st.Rv -= 55f * k * sc;
                    st.Q = Mathf.Min(0.38f, st.Q + 0.2f * k);
                    st.Wob0 += 0.05f;
                    st.Wob1 += 0.04f;
                    st.LastHit = t;
                    st.Soaked += drop;
                }
                st.PrevHp = p.ShieldHp;
                for (float left = dt; left > 0f; left -= 1f / 60f)
                {
                    float h = Mathf.Min(1f / 60f, left);
                    st.Rv += (EnergySpringK * (target - st.R) - EnergySpringC * st.Rv) * h;
                    st.R += st.Rv * h;
                    st.Qv += (-120f * st.Q - 8f * st.Qv) * h;
                    st.Q += st.Qv * h;
                    st.Wph += 9f * h;
                    st.Wob0 *= Mathf.Exp(-3f * h);
                    st.Wob1 *= Mathf.Exp(-4.2f * h);
                }
                st.Active = true;
                DrawEnergyBubble(p, st, t, f);
            }
            else
            {
                if (st.Active && t - st.Last < 0.25f && !p.Downed)
                    st.End = t;  // it just went: break or run out
                st.Active = false;
                st.Started = false;
                float age = t - st.End;
                if (st.End > 0f && age >= 0f && age < 1.1f)
                    DrawEnergyPop(p, st, age, full);
            }
        }

        void DrawEnergyBubble(Player p, EnergyWardState st, float t, float f)
        {
            V2 o = p.Disp;
            var blue = new Rgba(0.43f, 0.67f, 1f);
            var green = new Rgba(0.35f, 1f, 0.55f);
            var col = blue.Mix(green, Mathf.Clamp01((0.5f - f) / 0.3f));
            float urgent = f < 0.35f ? 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(6f, 14f, 1f - f / 0.35f)) : 0f;
            float since = t - st.LastHit, flash = Mathf.Clamp01(1f - since / 0.3f);
            const int N = 44;
            var pts = new V2[N];
            for (int i = 0; i < N; i++)
            {
                float a = MathX.Tau * i / N;
                float rr = st.R * (1f - st.Q * Mathf.Cos(2f * (a - st.From)) + st.Wob0 * Mathf.Sin(3f * a + st.Wph) + st.Wob1 * Mathf.Sin(5f * a - st.Wph * 1.3f));
                pts[i] = o + V2.FromAngle(a) * rr;
            }
            // the body: a faint disc, stronger toward the rim (a smaller one on top for the falloff)
            SetColor(col.WithAlpha(0.08f + 0.08f * urgent));
            for (int i = 0; i < N; i++)
                Tri(o, pts[i], pts[(i + 1) % N]);
            Circle(o, st.R * 0.8f, col.WithAlpha(0.03f), 32);
            // the heal core: grows and beats as the shell shrinks
            if (f < 1f)
            {
                float charge = Mathf.Pow(1f - f, 0.85f);
                float cr = st.R * 0.62f * charge * (1f + 0.12f * Mathf.Sin(t * Mathf.Lerp(4f, 12f, 1f - f)) + flash * 0.12f);
                if (cr > 1f)
                {
                    Circle(o, cr, green.WithAlpha(0.2f), 28);
                    Circle(o, cr * 0.72f, green.WithAlpha(0.3f), 24);
                    Circle(o, cr * 0.42f, new Rgba(0.75f, 1f, 0.82f, 0.55f), 20);
                }
            }
            // slow swirling arcs inside
            for (int k = 0; k < 3; k++)
                Arc(o, st.R * (0.55f + k * 0.14f), 1.6f, col.WithAlpha(0.35f), 8, t * (0.9f + k * 0.35f) + k * 2.1f + p.Id, 1.4f);
            // the rim: a soft wide line under a bright one
            var rim = col.Mix(Rgba.White, 0.35f);
            for (int i = 0; i < N; i++)
            {
                V2 a = pts[i], b = pts[(i + 1) % N];
                Line(a, b, 6f + 3f * urgent + flash * 3f, col.WithAlpha(0.18f + 0.12f * urgent));
                Line(a, b, 2.5f, rim.WithAlpha(0.95f));
            }
            if (flash > 0f)
                Arc(o, st.R, 5f, Rgba.White.WithAlpha(flash * 0.9f), 10, st.From - 0.5f, 1f);
        }

        /// The break: a green ring, and motes flowing in to the player (the heal).
        void DrawEnergyPop(Player p, EnergyWardState st, float age, float full)
        {
            V2 o = p.Disp;
            float a = 1f - age / 1.1f, sc = full / 64f;
            var green = new Rgba(0.47f, 1f, 0.63f);
            Arc(o, full * EnergyMinSize + age * 70f * sc, 4f * a + 1f, green.WithAlpha(a * 0.9f), 40);
            float k = Mathf.Clamp01(age / 0.7f), rr = Mathf.Lerp(full * EnergyMinSize + 30f * sc, 10f * sc, k * k);
            for (int i = 0; i < 10; i++)
                Circle(o + V2.FromAngle(MathX.Tau * i / 10f + i) * rr, 3f, new Rgba(0.55f, 1f, 0.67f, (1f - k) * 0.9f), 8);
        }
    }
}
