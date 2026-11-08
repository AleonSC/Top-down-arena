using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Arterial Spray (Core/ArterialSpray.cs), look A "Jet" of Docs/Mockups/arterial-spray.html: a
    /// bright, tight stream holds together for about 60% of the reach, then bursts into a fan of streaking drops that fills
    /// the cone. Drops that land leave spatter on the floor that fades over a few seconds, and soaked enemies are flecked
    /// red. All of it comes from the replicated Player.SprayOn and aim; the spatter is this screen's own.
    public partial class ArenaApp
    {
        struct Spatter { public V2 Pos; public float Born, R, Rot; }
        readonly List<Spatter> spatters = new List<Spatter>();
        float spatterAt;
        int spatterSeed;
        float SprayRoll() => SprayHash(++spatterSeed * 0.731f + 0.17f);  // the spatter's own dice (deterministic, so previews repeat)
        const float SpatterLife = 3f;
        const int SpatterMax = 120;

        static float SprayHash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);

        /// World, over the model (BloodMageLook.cs): the jet itself, and red flecks on what it soaks.
        void DrawSpray(Player p, float r, float t, float a)
        {
            if (!p.SprayOn)
                return;
            float reach = Game.SprayReachOf(p), half = Game.SprayHalfArc;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal();
            V2 from = p.Disp + dir * (r + 2f);
            float core = reach * 0.6f;
            // A faint haze over the cone, so the reach reads.
            SetColor(BloodRed.WithAlpha(0.07f * a));
            for (int k = 0; k < 8; k++)
            {
                float a0 = p.Aim - half + 2f * half * k / 8f, a1 = p.Aim - half + 2f * half * (k + 1) / 8f;
                Tri(from, p.Disp + V2.FromAngle(a0) * reach, p.Disp + V2.FromAngle(a1) * reach);
            }
            // The stream: tight and bright, wobbling a little, swelling toward where it bursts.
            float wob = Mathf.Sin(t * 31f) * 1.2f;
            V2 burst = from + dir * core + side * wob;
            SetColor(BloodDark.WithAlpha(0.9f * a));
            Quad(from + side * 2.5f, burst + side * 5f, burst - side * 5f, from - side * 2.5f);
            SetColor(BloodRed.WithAlpha(a));
            Quad(from + side * 1.6f, burst + side * 3.5f, burst - side * 3.5f, from - side * 1.6f);
            Line(from, burst - dir * 6f, 1.2f, new Rgba(1f, 0.75f, 0.75f, 0.85f * a));
            // The fan: drops streaking out from the burst point to the end of the reach.
            const int drops = 44;
            for (int k = 0; k < drops; k++)
            {
                float cyc = t * 3.2f + k * 0.618f, life = Mathf.Repeat(cyc, 1f), seed = Mathf.Floor(cyc) * 13.7f + k * 7.3f;
                float ang = (SprayHash(seed) * 2f - 1f) * half * Mathf.Min(1f, life * 1.6f);
                float dist = core * 0.85f + life * (reach - core * 0.85f) * (0.8f + 0.25f * SprayHash(seed + 3f));
                V2 d = V2.FromAngle(p.Aim + ang);
                V2 pos = p.Disp + d * dist;
                float len = 6f + 8f * (1f - life), w = 3.2f - 1.4f * life;
                Line(pos - d * len, pos, w, BloodRed.WithAlpha((1f - life * 0.7f) * a));
            }
            // The mouth: a splash where it leaves your hand.
            Circle(from, 3.5f + 0.8f * Mathf.Sin(t * 40f), BloodRed.WithAlpha(a), 10);
            // Soaked enemies: dense red flecks that drip.
            foreach (var e in game.Enemies.Values)
            {
                if (!Game.InSpray(p.Disp, p.Aim, reach, e.Disp, e.Radius))
                    continue;
                for (int k = 0; k < 6; k++)
                {
                    float sx = SprayHash(e.Id * 3.1f + k) * 2f - 1f, sy = SprayHash(e.Id * 5.7f + k * 1.3f) * 2f - 1f;
                    float drip = Mathf.Repeat(t * 1.5f + k * 0.37f, 1f) * 4f;
                    Circle(e.Disp + new V2(sx * e.Radius * 0.7f, sy * e.Radius * 0.7f + drip), 1.6f, BloodRed.WithAlpha(0.9f * a), 6);
                }
            }
        }

        /// World, on the floor under everything (WorldRenderer.cs): fresh spatter where drops land, fading out.
        void DrawSprayStains(float t)
        {
            if (game == null)
                return;
            if (t < spatterAt - 1f)
                spatterAt = t;  // a new game's clock
            bool due = t - spatterAt >= 0.05f;
            if (due)
            {
                spatterAt = t;
                foreach (var p in game.Players.Values)
                {
                    if (!p.SprayOn)
                        continue;
                    float reach = Game.SprayReachOf(p);
                    for (int k = 0; k < 2; k++)
                    {
                        float ang = p.Aim + (SprayRoll() * 2f - 1f) * Game.SprayHalfArc;
                        float dist = reach * (0.55f + 0.45f * SprayRoll());
                        spatters.Add(new Spatter { Pos = p.Disp + V2.FromAngle(ang) * dist, Born = t, R = 2f + 3.5f * SprayRoll(), Rot = SprayRoll() * MathX.Tau });
                    }
                }
                while (spatters.Count > SpatterMax)
                    spatters.RemoveAt(0);
            }
            for (int i = spatters.Count - 1; i >= 0; i--)
            {
                var s = spatters[i];
                float age = t - s.Born;
                if (age > SpatterLife || age < 0f)
                {
                    spatters.RemoveAt(i);
                    continue;
                }
                float fade = 1f - age / SpatterLife;
                Circle(s.Pos, s.R, BloodDark.WithAlpha(0.75f * fade), 8);
                Circle(s.Pos + V2.FromAngle(s.Rot) * s.R * 0.9f, s.R * 0.45f, BloodDark.WithAlpha(0.6f * fade), 6);
                Circle(s.Pos - V2.FromAngle(s.Rot + 0.8f) * s.R * 1.2f, s.R * 0.25f, BloodRed.WithAlpha(0.5f * fade), 5);
            }
        }
    }
}
