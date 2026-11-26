using UnityEngine;

namespace Arena
{
    /// How the Summoner Healer's Phoenix looks (Core/Phoenix.cs does its flight and heals): a firebird seen from above, with
    /// layered flame wings that beat, a crested head and beak, three long tail plumes that ripple behind it and embers
    /// shed in its wake. Every PhoenixPulse seconds it sends out a wide ring of healing fire (PhoenixPulseRange), and a
    /// faint ring at that range burns down as its time runs out.
    public partial class ArenaApp
    {
        const float PhoenixPulse = Game.PhoenixPulse, PhoenixPulseRange = Game.PhoenixPulseRange;
        static readonly Rgba PhxRed = new Rgba(0.93f, 0.25f, 0.12f), PhxOrange = new Rgba(1f, 0.55f, 0.15f),
            PhxGold = Rgba.Gold, PhxCore = new Rgba(1f, 0.96f, 0.75f);

        void DrawPhoenix(Minion m, float t)
        {
            V2 c = m.Disp, dir = V2.FromAngle(m.Aim), side = new V2(-dir.Y, dir.X);
            float age = m.Duration > 0f && m.T >= 0f ? m.Duration - m.T : t;
            float left = m.T >= 0f ? MathX.Clamp01(m.T) : 1f;  // the last second: it dims
            DrawPhoenixPulse(m, c, age);
            if (!fxReduced)
                GlowDisc(c, 52f, PhxOrange, 0.32f * left);
            for (int k = 0; k < 7; k++)  // embers shed in its wake
            {
                float q = MathX.Fmod(age * 1.4f + k / 7f, 1f);
                V2 at = c - dir * (14f + q * 58f) + side * (Mathf.Sin(k * 2.7f + age * 3f) * 14f * q);
                Circle(at, 2.4f * (1f - q) + 0.6f, (q < 0.4f ? PhxGold : PhxRed).WithAlpha((1f - q) * left), 6);
            }
            for (int k = -1; k <= 1; k++)  // three tail plumes rippling behind
            {
                V2 prev = c - dir * 6f;
                const int n = 9;
                for (int i = 1; i <= n; i++)
                {
                    float u = i / (float)n;
                    float wave = Mathf.Sin(age * 8f - i * 0.8f + k * 1.3f) * 6f * u;
                    V2 at = c - dir * (6f + u * (k == 0 ? 54f : 44f)) + side * (k * u * 16f + wave);
                    Rgba col = u < 0.35f ? PhxGold : u < 0.7f ? PhxOrange : PhxRed;
                    Line(prev, at, Mathf.Lerp(6f, 1.5f, u), col.WithAlpha((1f - 0.5f * u) * left));
                    prev = at;
                }
                Circle(prev, 3.4f, PhxRed.WithAlpha(left), 8);  // a flame eye at each plume's tip
                Circle(prev, 1.6f, PhxGold.WithAlpha(left), 6);
            }
            float flap = Mathf.Sin(age * 9f + m.Id);
            for (int k = -1; k <= 1; k += 2)  // the wings: red outer feathers, orange, then gold, swept back
                DrawPhoenixWing(c + dir * 2f + side * (k * 4f), dir, side * k, flap, left);
            Circle(c - dir * 5f, 6.5f, PhxOrange.WithAlpha(left), 12);  // body
            Circle(c, 7.5f, PhxGold.WithAlpha(left), 14);
            Circle(c + dir * 1.5f, 4f, PhxCore.WithAlpha(left), 10);
            V2 head = c + dir * 10f;
            Circle(head, 5f, PhxGold.WithAlpha(left), 10);
            SetColor(PhxOrange.WithAlpha(left));
            Tri(head + dir * 10f, head + side * 3f, head - side * 3f);  // beak
            Circle(head + dir * 1.5f + side * 2.2f, 1.1f, PhxRed.WithAlpha(left), 5);
            Circle(head + dir * 1.5f - side * 2.2f, 1.1f, PhxRed.WithAlpha(left), 5);
        }

        /// One wing: three layers of long flame feathers fanning back from the shoulder; the beat folds them in and out.
        void DrawPhoenixWing(V2 root, V2 dir, V2 outward, float flap, float a)
        {
            float span = 0.78f + 0.22f * flap;
            for (int layer = 0; layer < 3; layer++)
            {
                Rgba col = (layer == 0 ? PhxRed : layer == 1 ? PhxOrange : PhxGold).WithAlpha(a);
                float len = (layer == 0 ? 48f : layer == 1 ? 37f : 24f) * span;
                SetColor(col);
                for (int i = 0; i < 6; i++)  // feathers from the leading edge (out to the side) to the trailing edge (back)
                {
                    float sweep = 0.05f + i * 0.24f + (1f - span) * 0.7f;  // radians back from straight out
                    V2 fd = (outward * Mathf.Cos(sweep) - dir * Mathf.Sin(sweep)).Normalized();
                    V2 fs = new V2(-fd.Y, fd.X);
                    float l = len * (1f - i * 0.1f);
                    V2 mid = root + fd * (l * 0.45f);
                    Tri(root + fs * 3f, root - fs * 3f, mid + fs * 5f);  // a feather: broad in the middle, a sharp tip
                    Tri(root - fs * 3f, mid - fs * 5f, mid + fs * 5f);
                    Tri(mid + fs * 5f, mid - fs * 5f, root + fd * l);
                }
            }
        }

        /// The healing pulse: a wide ring of fire rolling out from the phoenix every PhoenixPulse seconds, flecked with
        /// feathers pointing out, over a faint warm fill; and the range ring, which burns down as its time runs out.
        void DrawPhoenixPulse(Minion m, V2 c, float age)
        {
            float R = PhoenixPulseRange;
            float timeFrac = m.T >= 0f && m.Duration > 0f ? MathX.Clamp01(m.T / m.Duration) : 1f;
            Arc(c, R, 1.5f, PhxGold.WithAlpha(0.18f), 64);
            Arc(c, R, 2.5f, PhxGold.WithAlpha(0.45f), 64, -MathX.Pi / 2f, MathX.Tau * timeFrac);
            float u = MathX.Fmod(age, PhoenixPulse) / 0.7f;  // the ring takes 0.7 s to roll out
            if (u >= 1f)
                return;
            float e = 1f - (1f - u) * (1f - u) * (1f - u), r = Mathf.Max(8f, R * e), a = 1f - u;
            if (!fxReduced)
                FanGradient(c, r, PhxGold.WithAlpha(0.03f * a), PhxOrange.WithAlpha(0.07f * a), 40);
            if (!fxReduced)
                GlowArc(c, r, 3f + 4f * a, PhxGold.WithAlpha(0.9f * a), 64);
            else
                Arc(c, r, 3f + 4f * a, PhxGold.WithAlpha(0.9f * a), 64);
            Arc(c, r - 6f, 2f, PhxRed.WithAlpha(0.6f * a), 64);
            for (int i = 0; i < 16; i++)  // feather flecks on the ring, pointing out
            {
                V2 d = V2.FromAngle(i * MathX.Tau / 16f + age * 0.4f), s = new V2(-d.Y, d.X);
                V2 at = c + d * r;
                SetColor(PhxGold.WithAlpha(a));
                Tri(at + d * (10f * a + 4f), at + s * 3.5f, at - s * 3.5f);
            }
            Circle(c, 18f * (1f - u), PhxCore.WithAlpha(0.6f * a), 14);  // the flash at its heart as it lets go
        }
    }
}
