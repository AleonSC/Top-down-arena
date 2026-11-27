using UnityEngine;

namespace Arena
{
    /// The DPS passive's stacks (Momentum.cs) as a power-up aura: a gold flame shape round the body that licks upward
    /// in jagged spikes, taller and brighter with each stack, with streaks rising through it. At 4 stacks a white-hot
    /// core shows; at 5 it crackles with lightning and pulses at its base. It gutters in the last second before the
    /// stacks age out. Drawn under the body (DrawPlayerAuras).
    public partial class ArenaApp
    {
        static readonly Rgba AuraGold = new Rgba(1f, 0.82f, 0.28f);
        static readonly Rgba AuraHot = new Rgba(1f, 0.96f, 0.7f);
        static readonly Rgba AuraSpark = new Rgba(0.72f, 0.9f, 1f);
        const int AuraPts = 36;
        static readonly V2[] auraPts = new V2[AuraPts];

        static float AuraHash(float x) { float s = Mathf.Sin(x * 127.1f) * 43758.55f; return s - Mathf.Floor(s); }

        void DrawMomentum(Player p, float t)
        {
            if (p.Downed)
                return;
            int n = p.MomentumStacks;
            float k = n / (float)Game.MomentumMax;
            float gut = p.MomentumT < 1f ? (0.55f + 0.45f * p.MomentumT) * (MathX.Fmod(t, 0.12f) < 0.06f ? 1f : 0.7f) : 1f;
            float seed = p.Id * 3.7f;
            float baseR = p.Def.BodyR + 6f + 3f * k;
            float height = (10f + 8f * n) * gut;  // how far the flame climbs above you
            float jag = (3f + 1.6f * n) * gut;
            float speed = 9f + 3f * n;
            V2 c = p.Disp;

            if (n >= 5)  // the base pulse, a ring pushing out along the floor
            {
                float ph = MathX.Fmod(t * 1.8f, 1f);
                Arc(c, baseR + 4f + ph * 22f, 2.5f * (1f - ph), AuraHot.WithAlpha(0.55f * (1f - ph)), 32);
            }

            // Three nested flame layers, each flickering on its own phase: wide pale gold, gold, white-hot core.
            AuraLayer(c, baseR, height, jag, speed, t, seed, 1f, AuraGold.WithAlpha((0.19f + 0.1f * k) * gut), true);
            AuraLayer(c, baseR * 0.86f, height * 0.72f, jag * 0.8f, speed * 1.15f, t, seed + 11f, 0.95f, AuraGold.WithAlpha((0.22f + 0.12f * k) * gut), false);
            if (n >= 3)
                AuraLayer(c, baseR * 0.7f, height * (n >= 4 ? 0.5f : 0.35f), jag * 0.6f, speed * 1.3f, t, seed + 23f, 0.9f,
                    (n >= 4 ? AuraHot : AuraGold).WithAlpha((n >= 4 ? 0.38f : 0.24f) * gut), false);

            // Streaks rising through the aura.
            int streaks = 2 + 2 * n;
            for (int i = 0; i < streaks; i++)
            {
                float h = AuraHash(seed + i * 1.31f);
                float s = MathX.Fmod(t * (0.9f + 0.5f * h + 0.15f * n) + h * 7f, 1f);
                float x = (AuraHash(seed + i * 2.77f + Mathf.Floor(t * 0.9f + h * 7f)) - 0.5f) * 2f * baseR * 0.9f;
                float y0 = baseR * 0.6f - s * (baseR + height * 0.9f);
                float len = 4f + 2f * n;
                Line(c + new V2(x, y0), c + new V2(x, y0 - len), 1.2f, AuraHot.WithAlpha(0.75f * (1f - s) * gut));
            }

            // Lightning at full stacks: two short zigzag bolts inside the aura, re-struck every few frames.
            if (n >= 5)
            {
                float strike = Mathf.Floor(t * 14f);
                for (int b = 0; b < 2; b++)
                {
                    float bs = seed + strike * 5.1f + b * 17f;
                    if (AuraHash(bs) < 0.25f)
                        continue;  // a gap now and then, so it crackles
                    float a0 = MathX.Tau * AuraHash(bs + 1f);
                    V2 at = c + V2.FromAngle(a0) * (baseR * 0.7f);
                    V2 dir = V2.FromAngle(a0 + (AuraHash(bs + 2f) - 0.5f) * 1.6f);
                    for (int sgm = 0; sgm < 4; sgm++)
                    {
                        V2 nx = at + dir * 6f + dir.Orthogonal() * ((AuraHash(bs + 3f + sgm) - 0.5f) * 9f);
                        nx.Y -= 3f;  // bolts climb with the flame
                        Line(at, nx, 1.3f, AuraSpark.WithAlpha(0.95f));
                        at = nx;
                    }
                }
            }
        }

        /// One flame layer: a closed outline round `c`, round at the bottom and climbing up into jagged spikes at the
        /// top (screen up), filled flat from the centre; `rim` also draws its bright edge.
        void AuraLayer(V2 c, float baseR, float height, float jag, float speed, float t, float seed, float sway, Rgba col, bool rim)
        {
            float lean = Mathf.Sin(t * 2.3f + seed) * 0.12f * sway;  // the whole flame sways side to side a little
            for (int i = 0; i < AuraPts; i++)
            {
                float a = MathX.Tau * i / AuraPts;
                V2 d = V2.FromAngle(a);
                float up = Mathf.Clamp01((-d.Y + 0.25f) / 1.25f);  // 0 at the bottom, 1 straight up
                float climb = height * up * up;
                float ph = AuraHash(seed + i * 0.73f) * MathX.Tau;
                float flick = 0.5f + 0.5f * Mathf.Sin(t * speed + ph) * Mathf.Sin(t * speed * 0.61f + ph * 1.9f);
                float spike = (i % 2 == 0 ? 1f : -0.25f) * jag * (0.5f + 0.5f * up) * (0.45f + 0.55f * flick);
                float r = baseR + climb * (0.75f + 0.35f * flick) + spike;
                V2 q = c + d * r;
                q.Y -= Mathf.Max(0f, spike) * 0.9f;  // tips curl upward, so even the side spikes climb
                q.X += lean * (q.Y < c.Y ? c.Y - q.Y : 0f);
                auraPts[i] = q;
            }
            SetColor(col);
            for (int i = 0; i < AuraPts; i++)
                Tri(c, auraPts[i], auraPts[(i + 1) % AuraPts]);
            if (rim)
            {
                var edge = AuraHot.WithAlpha(Mathf.Min(0.7f, col.A * 2.6f));
                for (int i = 0; i < AuraPts; i++)
                    Line(auraPts[i], auraPts[(i + 1) % AuraPts], 1.3f, edge);
            }
        }
    }
}
