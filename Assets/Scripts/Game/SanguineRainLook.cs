using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Sanguine Rain (Core/SanguineRain.cs), from Docs/Mockups/sanguine-rain.html. Everything is drawn from Player.RainClock (seconds since the
    /// cast) and the players' own positions, so every screen agrees and nothing is kept per effect:
    ///   Siphon: five wavering ribbons and streaking drops pour from the healer into a spinning orb overhead; the floor darkens under them.
    ///   Seed:   the orb bursts in a shock ring and a bead of blood arcs to each ally.
    ///   Rain:   a rain circle follows each ally with a slight lag (a dashed rim, drops falling and rippling, heal motes drifting to the ally),
    ///           a tether of blood beads runs from the healer to each circle, and a countdown ring runs round the healer.
    /// The end is FxKind.RainRelease, one per ally: the ring fades and flings sparks out.
    public partial class ArenaApp
    {
        static readonly Rgba RnBlood = new Rgba(0.88f, 0.13f, 0.25f), RnBright = new Rgba(1f, 0.35f, 0.45f);
        static readonly Rgba RnRose = new Rgba(1f, 0.7f, 0.75f), RnPink = new Rgba(1f, 0.5f, 0.58f), RnDeep = new Rgba(0.36f, 0.06f, 0.12f);
        const float RainOrbUp = 96f, RainOrbMax = 30f;

        readonly Dictionary<int, V2> rainRings = new Dictionary<int, V2>();  // each ally's ring centre, trailing them a little

        static float RnFrac(float x) => x - (float)System.Math.Floor(x);

        static V2 RnCurve(V2 a, V2 b, float sway, float u)
        {
            V2 d = b - a;
            float len = Mathf.Max(1f, d.Length);
            return HvBez(a, (a + b) * 0.5f + d.Orthogonal() * (sway / len), b, u);
        }

        /// A point `u` (0 to 1) along drop `k`'s spiral from the healer's body to the orb.
        static V2 RainSuckPt(V2 body, V2 orb, int k, float u)
        {
            float a = CavHash(k * 5) * MathX.Tau, r0 = 12f + 9f * CavHash(k * 5 + 1), sw = (0.6f + CavHash(k * 5 + 2)) * (k % 2 == 0 ? 1f : -1f);
            float e = u * u, amp = Mathf.Sin(u * MathX.Pi) * 46f * sw * (1f - e * 0.4f);
            V2 pt = V2.Lerp(body + V2.FromAngle(a) * r0, orb, e);
            return pt + new V2(Mathf.Cos(a + u * 5f * sw) * amp, Mathf.Sin(a + u * 5f * sw) * amp * 0.55f);
        }

        /// Called for each player with a Sanguine Rain running: the siphon, the seeds, the rings and the tether.
        void DrawRain(Player p, float t)
        {
            if (p.Downed)
                return;
            float clk = p.RainClock, land = Game.RainCast + Game.RainSeed;
            V2 orb = p.Disp + new V2(0f, -RainOrbUp);
            float rainLen = Mathf.Max(1f, clk + p.UltT - Game.RainCast);
            if (clk < Game.RainCast)
                DrawRainSiphon(p, orb, clk, t);
            else if (clk < land)
                DrawRainBurst(orb, clk - Game.RainCast);
            foreach (var q in game.Players.Values)
            {
                if (q == p || q.Downed)
                    continue;
                if (clk >= Game.RainCast && clk < land)  // the bead in flight
                    DrawRainSeed(orb, q, (clk - Game.RainCast) / Game.RainSeed);
                if (clk < land)
                {
                    rainRings[q.Id] = q.Disp;
                    continue;
                }
                V2 c;
                if (!rainRings.TryGetValue(q.Id, out c))
                    c = q.Disp;
                c = V2.Lerp(c, q.Disp, 1f - Mathf.Exp(-5f * ViewDt));
                rainRings[q.Id] = c;
                float grow = HvSmooth((clk - land) / 0.4f);
                DrawRainTether(p, c, q.Id, grow, t);
                DrawRainRing(q, c, Game.RainRingR * (0.2f + 0.8f * grow), clk - land, t, grow);
            }
            if (clk >= land)  // the countdown round the healer
            {
                float frac = Mathf.Clamp01(p.UltT / rainLen), r = p.Def.BodyR + 14f;
                Arc(p.Disp, r, 4f, RnBlood.WithAlpha(0.22f), 40);
                Arc(p.Disp, r, 4f, RnPink.WithAlpha(0.95f), 40, -MathX.Pi / 2f, MathX.Tau * frac);
                int ticks = Mathf.RoundToInt(rainLen);
                for (int k = 0; k < ticks; k++)
                {
                    V2 d = V2.FromAngle(-MathX.Pi / 2f + MathX.Tau * k / ticks);
                    Line(p.Disp + d * (r - 3.5f), p.Disp + d * (r + 3.5f), 1.6f, RnDeep.WithAlpha(0.9f));
                }
            }
        }

        void DrawRainSiphon(Player p, V2 orb, float clk, float t)
        {
            float k = HvSmooth(clk / Game.RainCast), R = RainOrbMax * k;
            Circle(p.Disp, p.Def.BodyR + 12f + 10f * k, RnDeep.WithAlpha(0.35f * k), 24);  // the floor darkens under the healer
            for (int i = 0; i < 5; i++)  // ribbons, wavering from the body to the orb
            {
                V2 prev = p.Disp;
                for (int s = 1; s <= 14; s++)
                {
                    float u = s / 14f, off = Mathf.Sin(u * MathX.Pi) * (16f + i * 6f) * Mathf.Sin(t * 5f + i * 1.7f + u * 8f);
                    V2 pt = V2.Lerp(p.Disp, orb, u) + new V2(off, 0f);
                    Line(prev, pt, 3f + 2.5f * k, RnBlood.WithAlpha(0.22f + 0.4f * k));
                    prev = pt;
                }
            }
            for (int i = 0; i < 56; i++)  // drops streaking in, more of them as it builds
            {
                if (CavHash(i * 11 + 3) > 0.25f + 0.75f * k)
                    continue;
                float life = 0.45f + 0.4f * CavHash(i * 7 + 2), u = RnFrac(clk / life + CavHash(i * 3 + 9));
                V2 prev = RainSuckPt(p.Disp, orb, i, u);
                for (int s = 1; s <= 5; s++)
                {
                    V2 pt = RainSuckPt(p.Disp, orb, i, Mathf.Max(0f, u - s * 0.035f));
                    Line(prev, pt, 3.4f - s * 0.4f, RnBright.WithAlpha((1f - s / 5f) * Mathf.Min(1f, (1f - u) * 4f) * 0.9f));
                    prev = pt;
                }
            }
            DrawRainOrb(orb, R, t);
        }

        void DrawRainOrb(V2 c, float R, float t)
        {
            if (R < 2f)
                return;
            R += 1.6f * Mathf.Sin(t * 14f);
            GlowDisc(c, R * 2.6f, RnBright, 0.5f);
            Circle(c, R, RnBlood, 24);
            Circle(c + new V2(-R * 0.18f, -R * 0.2f), R * 0.7f, RnBright, 20);
            Circle(c + new V2(-R * 0.3f, -R * 0.34f), R * 0.3f, new Rgba(1f, 0.9f, 0.92f, 0.9f), 12);
            for (int q = 0; q < 3; q++)
                Arc(c, R * (1.15f + q * 0.2f), 2f, RnRose.WithAlpha(0.8f), 10, t * 3f * (q % 2 == 0 ? 1f : -1f) + q * 2.1f, 1.8f);
            for (int q = 0; q < 6; q++)
            {
                float a = -t * 2.4f + q * MathX.Tau / 6f;
                GlowDisc(c + new V2(Mathf.Cos(a) * R * 1.6f, Mathf.Sin(a) * R * 1.1f), 5f, RnBright, 0.8f);
            }
        }

        /// The orb bursting: a shock ring and sparks thrown out (a is seconds since the burst).
        void DrawRainBurst(V2 orb, float a)
        {
            float q = Mathf.Clamp01(a / 0.7f);
            Arc(orb, 12f + 300f * Mathf.Sqrt(q), 5f * (1f - q) + 1f, RnBright.WithAlpha(0.85f * (1f - q)), 48);
            GlowDisc(orb, 90f * (1f - q) + 20f, new Rgba(1f, 0.9f, 0.9f), 0.7f * (1f - q));
            for (int i = 0; i < 26; i++)
            {
                float ang = CavHash(i * 3 + 1) * MathX.Tau, dist = (80f + 220f * CavHash(i * 5)) * (1f - Mathf.Pow(1f - q, 2f));
                GlowDisc(orb + V2.FromAngle(ang) * dist, 4f, RnBright, 0.9f * (1f - q));
            }
        }

        /// A bead of blood arcing from the orb to an ally; u runs 0 to 1.
        void DrawRainSeed(V2 orb, Player q, float u)
        {
            V2 prev = default(V2);
            for (int s = 8; s >= 0; s--)
            {
                float uu = HvSmooth(Mathf.Clamp01(u - s * 0.04f));
                V2 pt = V2.Lerp(orb, q.Disp, uu) + new V2(0f, -Mathf.Sin(uu * MathX.Pi) * 90f);
                if (s < 8)
                    Line(prev, pt, 5f * (1f - s / 9f), RnBright.WithAlpha(0.9f * (1f - s / 9f)));
                prev = pt;
            }
            GlowDisc(prev, 9f, RnBright, 1f);
        }

        /// The blood running from the healer down to an ally's ring: a faint curve with beads riding it.
        void DrawRainTether(Player p, V2 to, int id, float grow, float t)
        {
            float sway = 50f * Mathf.Sin(t * 1.2f + id * 2f);
            V2 prev = p.Disp;
            for (int s = 1; s <= 24; s++)
            {
                V2 pt = RnCurve(p.Disp, to, sway, s / 24f);
                Line(prev, pt, 2f, RnBlood.WithAlpha(0.3f * grow));
                prev = pt;
            }
            for (int k = 0; k < 5; k++)
            {
                float u = RnFrac(t * 0.55f + k / 5f + id * 0.3f);
                DrawDrop(RnCurve(p.Disp, to, sway, u), 3.4f, (to - p.Disp).Normalized(), RnBright.WithAlpha(Mathf.Sin(u * MathX.Pi) * grow));
            }
        }

        /// One ally's rain circle. a = seconds since the rings landed, g = how grown (0 to 1).
        void DrawRainRing(Player q, V2 c, float r, float a, float t, float g)
        {
            FanGradient(c, r, RnBlood.WithAlpha(0.06f * g), RnPink.WithAlpha(0.3f * g), 32);
            Arc(c, r, 10f, RnPink.WithAlpha(0.14f * g), 40);
            Arc(c, r, 2.5f, RnBright.WithAlpha(0.95f * g), 40);
            for (int ring = 0; ring < 2; ring++)  // two dashed runes turning against each other
            {
                float rr = r * (ring == 0 ? 0.78f : 0.52f), dir = ring == 0 ? 1f : -1f;
                for (int d = 0; d < 14; d++)
                    Arc(c, rr, 1.6f, RnRose.WithAlpha(0.5f * g), 3, t * 0.5f * dir + d * MathX.Tau / 14f, 0.26f);
            }
            for (int k = 0; k < 3; k++)
            {
                float ang = t * 1.4f + k * MathX.Tau / 3f;
                GlowDisc(c + V2.FromAngle(ang) * r, 4f, RnBright, 0.85f * g);
            }
            for (int i = 0; i < 36; i++)  // rain: each slot falls, lands and ripples in a new spot every cycle
            {
                float period = 0.9f + 0.4f * CavHash(i * 9 + q.Id), fall = 0.4f;
                float s = t + CavHash(i * 13 + 7) * period;
                int cyc = (int)System.Math.Floor(s / period);
                float ph = s - cyc * period;
                float ang = CavHash(cyc * 31 + i * 17 + q.Id * 5) * MathX.Tau, rad = Mathf.Sqrt(CavHash(cyc * 29 + i * 3 + 11)) * r * 0.92f;
                V2 at = c + V2.FromAngle(ang) * rad;
                if (ph < fall)
                {
                    float u = ph / fall, h = 130f * (1f - u) * (1f - u);
                    Line(at + new V2(0f, -h - 13f), at + new V2(0f, -h), 2.2f, RnPink.WithAlpha(Mathf.Min(1f, u * 3f) * 0.9f * g));
                }
                else if (ph < fall + 0.5f)
                {
                    float u = (ph - fall) / 0.5f;
                    Arc(at, 3f + 13f * Mathf.Sqrt(u), 1.6f, RnBright.WithAlpha((1f - u) * 0.75f * g), 10);
                }
            }
            if (a > 0f)  // heal motes drifting up to the ally after each tick
            {
                int tick = (int)System.Math.Floor(a / Game.RainTick);
                for (int tk = tick; tk >= Mathf.Max(0, tick - 1); tk--)
                {
                    float age = a - tk * Game.RainTick;
                    for (int m = 0; m < 5; m++)
                    {
                        float life = 0.45f + 0.35f * CavHash(tk * 5 + m + q.Id * 101), u = age / life;
                        if (u <= 0f || u >= 1f)
                            continue;
                        float ang = CavHash(tk * 23 + m * 7 + q.Id) * MathX.Tau, rad = r * (0.5f + 0.5f * CavHash(tk * 19 + m * 3 + 2));
                        V2 from = c + V2.FromAngle(ang) * rad, bend = new V2((CavHash(tk * 3 + m) - 0.5f) * 60f, -30f);
                        V2 pt = HvBez(from, (from + q.Disp) * 0.5f + bend, q.Disp, HvSmooth(u));
                        GlowDisc(pt, 5f, RnPink, Mathf.Sin(u * MathX.Pi) * 0.95f + 0.1f);
                    }
                }
            }
        }

        /// A ring splashing away when the rain ends: it shrinks and fades while sparks fly out of its rim.
        void DrawRainReleaseFx(Effect fx, float f)
        {
            Arc(fx.Pos, fx.Radius * (1f - 0.7f * f), 2.5f, RnBright.WithAlpha(1f - f), 40);
            Circle(fx.Pos, fx.Radius * (1f - 0.7f * f), RnBlood.WithAlpha(0.18f * (1f - f)), 28);
            for (int i = 0; i < 16; i++)
            {
                V2 d = V2.FromAngle(CavHash(i * 3 + 1) * MathX.Tau);
                GlowDisc(fx.Pos + d * (fx.Radius * 0.9f + 80f * Mathf.Sqrt(f) * (0.5f + CavHash(i))), 4f, RnBright, 0.9f * (1f - f));
            }
        }
    }
}
