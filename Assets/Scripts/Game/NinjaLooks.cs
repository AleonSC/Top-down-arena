using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Two Ninja DPS looks. Vanish, "Smoke" (look A of Docs/Mockups/ninja-vanish.html): a ring of puffs when you go, wisps shed
    /// by the faint body, a smoke bank and a dashed "last seen" ring on the spot enemies now aim at, and a second puff when you
    /// come back. Blade Fan's orbit (look B of Docs/Mockups/ninja-blade-fan.html): the circling kunai (Game.OrbitBlade).
    /// Reduced effects: the bank and ring stay, the puffs, wisps and streaks go.
    public partial class ArenaApp
    {
        static readonly Rgba VanishSmokeCol = new Rgba(0.69f, 0.6f, 0.88f);
        static readonly Rgba SteelCol = new Rgba(0.84f, 0.86f, 0.92f);
        static readonly Rgba OrbitCol = new Rgba(0.65f, 0.55f, 0.94f);
        readonly Dictionary<int, float> vanishPeak = new Dictionary<int, float>();   // the longest VanishT seen this vanish, so the time since the cast is known
        readonly Dictionary<int, float> vanishEnd = new Dictionary<int, float>();    // when each player last came back (to time the second puff)
        readonly Dictionary<int, V2> vanishLast = new Dictionary<int, V2>();         // and where

        static float SmokeHash(float n) => Mathf.Abs(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f) % 1f;

        /// One soft puff of smoke: a glow disc that grows and fades with k (0 to 1).
        void SmokePuff(V2 c, float r, float k, float a)
        {
            k = Mathf.Clamp01(k);
            GlowDisc(c, r + k * r * 1.4f, VanishSmokeCol, a * Mathf.Pow(1f - k, 1.3f));
        }

        void DrawVanishSmoke(Player p, float t)
        {
            float peak;
            vanishPeak.TryGetValue(p.Id, out peak);
            bool on = p.VanishT > 0f && !p.Downed;
            if (on)
            {
                if (p.VanishT > peak)
                    peak = p.VanishT;
                vanishPeak[p.Id] = peak;
                vanishLast[p.Id] = p.Disp;
            }
            else if (peak > 0f)
            {
                vanishPeak[p.Id] = 0f;
                vanishEnd[p.Id] = t;  // it just ended: the reveal puff
            }
            if (on)
            {
                float since = peak - p.VanishT, fade = Mathf.Clamp01(p.VanishT / 0.5f);
                V2 spot = p.VanishFrom;
                for (int i = 0; i < 5; i++)  // the smoke bank on the vanish spot
                {
                    float an = i * 1.26f + (fxReduced ? 0f : since * 0.5f);
                    GlowDisc(spot + new V2(Mathf.Cos(an) * 14f, Mathf.Sin(an) * 10f - Mathf.Min(1f, since / 1.2f) * 10f), 34f + i * 3f, VanishSmokeCol, 0.3f * fade);
                }
                for (int i = 0; i < 8; i++)  // the dashed "last seen" ring
                    Arc(spot, 26f, 1.5f, VanishSmokeCol.WithAlpha(0.5f * fade), 4, i * MathX.Tau / 8f, MathX.Tau / 16f);
                if (fxReduced)
                    return;
                if (since < 1f)  // the ring of puffs as you go
                    for (int i = 0; i < 9; i++)
                    {
                        float k = since / 0.9f, an = i * MathX.Tau / 9f + SmokeHash(i) * 0.6f, d = 46f * (1f - (1f - Mathf.Clamp01(k)) * (1f - Mathf.Clamp01(k))) * (0.7f + SmokeHash(i + 5f) * 0.5f);
                        SmokePuff(spot + V2.FromAngle(an) * d - new V2(0f, k * 8f), 16f, k, 0.55f);
                    }
                for (int k = 0; k < 6; k++)  // wisps shed by the body
                {
                    float ph = MathX.Fmod(t * 1.1f + k / 6f, 1f), an = k * 1.9f + p.Id;
                    V2 at = p.Disp + V2.FromAngle(an) * (6f + ph * 18f) - new V2(0f, ph * 14f);
                    GlowDisc(at, 7f + ph * 10f, VanishSmokeCol, 0.22f * (1f - ph));
                }
            }
            float sinceEnd;
            if (!fxReduced && vanishEnd.TryGetValue(p.Id, out sinceEnd) && (sinceEnd = t - sinceEnd) < 0.8f && vanishLast.ContainsKey(p.Id))
                for (int i = 0; i < 7; i++)  // the puff as you come back
                {
                    float k = sinceEnd / 0.8f, an = i * MathX.Tau / 7f + SmokeHash(i + 3f) * 0.6f, d = 30f * (1f - (1f - k) * (1f - k)) * (0.7f + SmokeHash(i + 8f) * 0.5f);
                    SmokePuff(p.Disp + V2.FromAngle(an) * d, 12f, k, 0.5f);
                }
        }

        /// Blade Fan's circling kunai: a faint ring, then each kunai point-first along its circle with a short streak behind.
        void DrawBladeOrbit(Player p, float t)
        {
            if (p.Downed || p.OrbitN <= 0)
                return;
            float flick = p.OrbitT < 1f ? (MathX.Fmod(t, 0.12f) < 0.06f ? 1f : 0.5f) : 1f;  // it flickers in the last second, before they fly out
            float grow = Mathf.Clamp01((Game.OrbitTime - p.OrbitT) / Game.OrbitSpin);
            Arc(p.Disp, Game.BladeOrbitR * grow, 1.5f, OrbitCol.WithAlpha(0.22f * flick), 32);
            for (int i = 0; i < p.OrbitN; i++)
            {
                V2 b = Game.OrbitBlade(p, i) - p.Pos + p.Disp, rel = b - p.Disp;
                float ang = Mathf.Atan2(rel.Y, rel.X) + MathX.Pi * 0.5f;
                V2 dir = V2.FromAngle(ang);
                if (!fxReduced)
                    Arc(p.Disp, rel.Length, 2f, OrbitCol.WithAlpha(0.3f * flick), 5, Mathf.Atan2(rel.Y, rel.X) - 0.4f, 0.4f);
                Line(b - dir * 9f, b + dir * 9f, 3.2f, SteelCol.WithAlpha(flick));
                Line(b + dir * 4f, b + dir * 10f, 2f, OrbitCol.WithAlpha(flick));
                Circle(b - dir * 10f, 2.2f, OrbitCol.WithAlpha(flick), 6);
            }
        }
    }
}
