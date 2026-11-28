using UnityEngine;

namespace Arena
{
    /// The DPS passive's stacks (Momentum.cs) as a power-up aura: a gold flame shape round the body that licks upward
    /// in jagged spikes, taller and brighter with each stack, with streaks rising through it. At 4 stacks a white-hot
    /// core shows; at 5 it crackles with lightning and pulses at its base. The shown level eases toward the stacks plus
    /// the kills toward the next one (Player.MomentumShow), so every kill swells it a little and each tier fades in.
    /// It gutters softly in the last second before the stacks age out, then sinks away. Drawn under the body (DrawPlayerAuras).
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
            // Ease the shown level toward the real one (stacks plus kills toward the next), so the aura swells with each
            // kill and sinks away when the stacks fade, never jumping a size.
            float goal = p.Downed ? 0f : Game.MomentumLevel(p);
            float rate = goal > p.MomentumShow ? 3f : 2f;
            p.MomentumShow += (goal - p.MomentumShow) * (1f - Mathf.Exp(-Time.deltaTime * rate));
            if (p.MomentumShow < 0.01f)
                return;
            float n = p.MomentumShow;  // 0 to MomentumMax, fractional
            float k = n / Game.MomentumMax;
            float fade = Mathf.Clamp01(n * 1.5f);  // the first kill fades it in rather than popping
            float gut = p.MomentumT < 1f && goal > 0f ? (0.7f + 0.3f * p.MomentumT) * (0.88f + 0.12f * Mathf.Sin(t * 22f)) : 1f;  // a soft gutter in the last second
            float seed = p.Id * 3.7f;
            float baseR = p.Def.BodyR + 6f + 3f * k;
            float height = (10f + 8f * n) * gut;  // how far the flame climbs above you
            float jag = (3f + 1.6f * n) * gut;
            float speed = 9f + 3f * n;
            float core = Mathf.Clamp01(n - 2f), hot = Mathf.Clamp01(n - 3f), full = Mathf.Clamp01(n - 4f);  // ease in from 2, 3, 4
            V2 c = p.Disp;

            if (full > 0f)  // the base pulse, a ring pushing out along the floor
            {
                float ph = MathX.Fmod(t * 1.8f, 1f);
                Arc(c, baseR + 4f + ph * 22f, 2.5f * (1f - ph), AuraHot.WithAlpha(0.55f * (1f - ph) * full), 32);
            }

            // Three nested flame layers, each flickering on its own phase: wide pale gold, gold, white-hot core.
            AuraLayer(c, baseR, height, jag, speed, t, seed, 1f, AuraGold.WithAlpha((0.19f + 0.1f * k) * gut * fade), true);
            AuraLayer(c, baseR * 0.86f, height * 0.72f, jag * 0.8f, speed * 1.15f, t, seed + 11f, 0.95f, AuraGold.WithAlpha((0.22f + 0.12f * k) * gut * fade), false);
            if (core > 0f)
            {
                var cc = new Rgba(Mathf.Lerp(AuraGold.R, AuraHot.R, hot), Mathf.Lerp(AuraGold.G, AuraHot.G, hot), Mathf.Lerp(AuraGold.B, AuraHot.B, hot));
                AuraLayer(c, baseR * 0.7f, height * Mathf.Lerp(0.35f, 0.5f, hot), jag * 0.6f, speed * 1.3f, t, seed + 23f, 0.9f,
                    cc.WithAlpha(Mathf.Lerp(0.24f, 0.38f, hot) * core * gut), false);
            }

            // Streaks rising through the aura.
            int streaks = 2 + 2 * Mathf.CeilToInt(n);
            for (int i = 0; i < streaks; i++)
            {
                float on = Mathf.Clamp01((n * 2f + 2f - i) * 0.5f) * fade;  // the newest streaks fade in with the level
                float h = AuraHash(seed + i * 1.31f);
                float s = MathX.Fmod(t * (0.9f + 0.5f * h + 0.15f * n) + h * 7f, 1f);
                float x = (AuraHash(seed + i * 2.77f + Mathf.Floor(t * 0.9f + h * 7f)) - 0.5f) * 2f * baseR * 0.9f;
                float y0 = baseR * 0.6f - s * (baseR + height * 0.9f);
                float len = 4f + 2f * n;
                Line(c + new V2(x, y0), c + new V2(x, y0 - len), 1.2f, AuraHot.WithAlpha(0.75f * (1f - s) * gut * on));
            }

            // Lightning at full stacks: two short zigzag bolts inside the aura, re-struck every few frames.
            if (full > 0f)
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
                        Line(at, nx, 1.3f, AuraSpark.WithAlpha(0.95f * full));
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
