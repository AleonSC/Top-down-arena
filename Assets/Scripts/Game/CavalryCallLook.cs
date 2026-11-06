using UnityEngine;

namespace Arena
{
    /// Cavalry Call (Core/CavalryCall.cs), the Gilded Charge look of the mockup: a gold lane band with marching chevrons, a summoning sigil
    /// and shaft of light at each lancer's spot, then gold-and-ivory lancers on armoured horses riding down the lane with a bow wave
    /// ahead and dust behind, sparkling out at the end. Drawn from the effect's age alone (FxKind.CavalryCall: Pos = where the knight
    /// stood, Pos2 = the lane's far end, Radius = lancers, colour alpha 1 = mastered), timed by Game.CavReach. Mockup: Docs/Mockups/cavalry-call.html.
    public partial class ArenaApp
    {
        static readonly Rgba CavGold = new Rgba(1f, 0.82f, 0.36f), CavIvory = new Rgba(1f, 0.96f, 0.84f), CavDust = new Rgba(0.82f, 0.74f, 0.58f);
        static readonly Rgba CavHorse = new Rgba(0.91f, 0.87f, 0.79f), CavHorseShade = new Rgba(0.66f, 0.61f, 0.5f), CavCape = new Rgba(0.69f, 0.2f, 0.2f);
        static readonly Rgba CavSteel = new Rgba(0.81f, 0.84f, 0.89f), CavArmour = new Rgba(0.53f, 0.58f, 0.67f), CavShaft = new Rgba(0.48f, 0.35f, 0.23f);

        static float CavHash(int i) { float s = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return s - (float)System.Math.Floor(s); }
        static float CavEaseOut(float f) { f = Mathf.Clamp01(f); return 1f - (1f - f) * (1f - f) * (1f - f); }

        void CavTri(V2 a, V2 b, V2 c, Rgba col) { SetColor(col); Tri(a, b, c); }

        void CavCapsule(V2 a, V2 b, float width, Rgba col)
        {
            Line(a, b, width, col);
            Circle(a, width / 2f, col, 10);
            Circle(b, width / 2f, col, 10);
        }

        void DrawCavalryCallFx(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len, side = dir.Orthogonal(), up = new V2(0f, 1f), origin = fx.Pos;
            float age = fx.T, ang = Mathf.Atan2(dir.Y, dir.X);
            int lancers = Mathf.RoundToInt(fx.Radius);
            float reach = Game.CavReach(age), outT = Mathf.Clamp01((age - Game.CavEndT) / Game.CavFadeT);
            float bandHalf = (lancers > 2 ? Game.CavLaneOuter : Game.CavLane) + 22f;

            // The cast flash at the knight's feet.
            if (age < 0.3f)
                Arc(origin, 20f + age * 150f, 4f, CavGold.WithAlpha(1f - age / 0.3f), 32);

            // The lane: a band along the aim with dashed edges and chevrons marching down it, gone soon after the charge starts.
            float la = age < Game.CavGoT ? CavEaseOut(age / 0.3f) : 1f - Mathf.Clamp01((age - Game.CavGoT) / 0.6f);
            if (la > 0.01f)
            {
                float far = Mathf.Min(len, 900f);
                V2 f0 = origin + dir * far;
                var near = CavGold.WithAlpha(0.2f * la);
                var clear = CavGold.WithAlpha(0.02f * la);
                VQuad(origin + side * bandHalf, near, origin - side * bandHalf, near, f0 - side * bandHalf, clear, f0 + side * bandHalf, clear);
                for (float s = (age * 70f) % 44f; s < far; s += 44f)
                {
                    float fade = (1f - s / far) * la;
                    Line(origin + dir * s + side * bandHalf, origin + dir * Mathf.Min(far, s + 24f) + side * bandHalf, 2f, CavGold.WithAlpha(0.8f * fade));
                    Line(origin + dir * s - side * bandHalf, origin + dir * Mathf.Min(far, s + 24f) - side * bandHalf, 2f, CavGold.WithAlpha(0.8f * fade));
                }
                for (int k = 0; k < 11; k++)
                {
                    float s = (k * 80f + age * 240f) % (far * 0.9f) + 50f;
                    float a = (1f - s / far) * la * 0.6f;
                    V2 c = origin + dir * s;
                    CavTri(c + side * 12f - dir * 6f, c + dir * 14f, c - side * 12f - dir * 6f, CavIvory.WithAlpha(a * 0.5f));
                }
            }

            for (int j = 0; j < lancers; j++)
            {
                float off = Game.CavOffset(j);
                V2 spawn = origin + dir * Game.CavStart + side * off;
                DrawCavSummon(spawn, side, up, age, j);
                // Dust behind the lancer: puffs left at where it was every 0.036 s.
                for (int k = 0; k < 52; k++)
                {
                    float ts = Game.CavGoT + k * 0.036f;
                    if (age < ts || ts > Game.CavEndT + 0.05f)
                        continue;
                    float f = (age - ts) / 1.1f;
                    if (f > 1f)
                        continue;
                    V2 at = spawn + dir * Game.CavReach(ts) - dir * 14f;
                    float jx = (CavHash(k * 5 + j * 70) - 0.5f) * 20f * (1f + 1.5f * f), jy = (CavHash(k * 11 + j * 70) - 0.5f) * 20f * (1f + 1.5f * f);
                    Circle(at + dir * jx + side * jy + up * (f * 8f), 8f + f * 18f, CavDust.WithAlpha(0.28f * (1f - f)), 10);
                }
                if (age >= Game.CavSpawnT && outT < 1f)
                {
                    float drop = Mathf.Clamp01((age - Game.CavSpawnT) / 0.4f);
                    float scale = 1.1f * (1f + (1f - CavEaseOut(drop)) * 0.7f), alpha = Mathf.Clamp01(drop * 2f) * (1f - outT);
                    V2 at = spawn + dir * (age < Game.CavGoT ? 0f : reach);
                    if (age > Game.CavGoT)
                        DrawCavBowWave(at + dir * 80f, dir, ang, alpha, j);
                    DrawCavLancer(at, dir, side, reach, alpha, scale, j, age);
                    if (age < Game.CavSpawnT + 0.4f)
                        Circle(at, 60f * (1f - drop * 0.4f), CavIvory.WithAlpha(0.5f * (1f - drop)), 20);
                }
                if (outT > 0f && outT < 1f)
                {
                    V2 at = spawn + dir * reach;
                    for (int q = 0; q < 10; q++)
                    {
                        float a = CavHash(q + j * 13) * MathX.Tau, r = outT * (32f + CavHash(q * 3) * 44f);
                        Circle(at + V2.FromAngle(a) * r + up * (outT * 22f), 2.6f * (1f - outT), CavIvory.WithAlpha(1f - outT), 6);
                    }
                }
            }
        }

        /// A sigil on the floor where a lancer arrives, with a shaft of light dropping onto it and motes rising.
        void DrawCavSummon(V2 p, V2 side, V2 up, float age, int j)
        {
            if (age < 0.12f)
                return;
            float sp = Mathf.Clamp01((age - 0.12f) / 0.45f);
            float sa = age < Game.CavGoT + 0.2f ? 1f : 1f - Mathf.Clamp01((age - Game.CavGoT - 0.2f) / 0.4f);
            if (sa <= 0.01f)
                return;
            float r = 42f * CavEaseOut(sp), spin = age * 1.4f * (j % 2 == 0 ? 1f : -1f);
            Circle(p, r, CavGold.WithAlpha(0.12f * sa), 24);
            Arc(p, r, 2.5f, CavGold.WithAlpha(0.9f * sa), 28);
            Arc(p, r * 0.72f, 1.6f, CavIvory.WithAlpha(0.6f * sa), 24);
            for (int k = 0; k < 8; k++)
            {
                V2 d = V2.FromAngle(spin + MathX.Tau * k / 8f);
                Line(p + d * r * 0.72f, p + d * r * 0.98f, 2f, CavGold.WithAlpha(0.9f * sa));
            }
            float h = Mathf.Clamp01((age - 0.15f) / 0.35f), fade = 1f - Mathf.Clamp01((age - Game.CavSpawnT - 0.1f) / 0.5f);
            if (fade > 0f)
            {
                float w = 16f + 7f * fade;
                var bright = CavIvory.WithAlpha(0.65f * fade);
                var gone = CavGold.WithAlpha(0f);
                VQuad(p - side * w, bright, p + side * w, bright, p + side * w + up * (190f * h), gone, p - side * w + up * (190f * h), gone);
            }
            for (int m = 0; m < 14; m++)
            {
                float ph = (CavHash(m + j * 30) + age * 0.9f) % 1f;
                V2 at = p + side * ((CavHash(m * 3 + j) - 0.5f) * 56f) + up * (ph * 130f);
                Circle(at, 2f, CavIvory.WithAlpha((1f - ph) * 0.9f * sp * sa), 6);
            }
        }

        /// Three arcs curling ahead of a lance (the bow wave) and speed streaks along the sides.
        void DrawCavBowWave(V2 c, V2 dir, float ang, float alpha, int j)
        {
            for (int w = 0; w < 3; w++)
                Arc(c - dir * (w * 15f), 38f + w * 7f, 3.2f - w * 0.8f, CavGold.WithAlpha((0.85f - w * 0.25f) * alpha), 12, ang - 1.05f, 2.1f);
            V2 side = dir.Orthogonal();
            for (int s = 0; s < 6; s++)
            {
                float o = (CavHash(s + j * 9) - 0.5f) * 70f, l0 = 45f + CavHash(s) * 35f;
                Line(c - dir * l0 + side * o, c - dir * (l0 + 45f) + side * o, 1.6f, CavIvory.WithAlpha(0.5f * alpha));
            }
        }

        static V2 CavL(V2 pos, V2 dir, V2 side, float sc, float x, float y) => pos + dir * (x * sc) + side * (y * sc);

        /// One knight on a horse, top down, facing `dir`: shadow, legs trotting by distance ridden, barding, horse, lance with pennant, rider.

void DrawCavLancer(V2 pos, V2 dir, V2 side, float ridden, float alpha, float sc, int j, float age)
        {
            if (alpha <= 0.01f)
                return;
            float gait = Mathf.Sin(ridden * 0.2f + j * 1.7f) * 5f;
            CavCapsule(CavL(pos, dir, side, sc, -20f, 5f), CavL(pos, dir, side, sc, 20f, 5f), 22f * sc, new Rgba(0f, 0f, 0f, 0.28f * alpha));
            for (int k = 0; k < 4; k++)
            {
                float lx = k < 2 ? 14f : -14f, sg = (k % 2 == 0 ? -1f : 1f) * gait, sd = k % 2 == 0 ? -1f : 1f;
                Line(CavL(pos, dir, side, sc, lx, sd * 8f), CavL(pos, dir, side, sc, lx + sg, sd * 15f), 3.3f * sc, CavHorseShade.WithAlpha(alpha));
            }
            float fl = Mathf.Sin(ridden * 0.25f + j) * 3f;
            var cape = CavCape.WithAlpha(0.95f * alpha);
            CavTri(CavL(pos, dir, side, sc, -14f, -12f), CavL(pos, dir, side, sc, -32f, -4f + gait * 0.2f), CavL(pos, dir, side, sc, -14f, 12f), cape);
            CavTri(CavL(pos, dir, side, sc, -32f, -4f), CavL(pos, dir, side, sc, -32f, 4f), CavL(pos, dir, side, sc, -14f, 0f), cape);
            CavCapsule(CavL(pos, dir, side, sc, -19f, 0f), CavL(pos, dir, side, sc, 19f, 0f), 24f * sc, CavHorseShade.WithAlpha(alpha));
            CavCapsule(CavL(pos, dir, side, sc, -19f, 0f), CavL(pos, dir, side, sc, 19f, 0f), 20f * sc, CavHorse.WithAlpha(alpha));
            CavCapsule(CavL(pos, dir, side, sc, 18f, 0f), CavL(pos, dir, side, sc, 34f, 0f), 11f * sc, CavHorse.WithAlpha(alpha));
            CavCapsule(CavL(pos, dir, side, sc, 33f, 0f), CavL(pos, dir, side, sc, 40f, 0f), 12f * sc, CavHorse.WithAlpha(alpha));
            Line(CavL(pos, dir, side, sc, -16f, 7f), CavL(pos, dir, side, sc, 86f, 2f), 5f * sc, CavShaft.WithAlpha(alpha));
            CavTri(CavL(pos, dir, side, sc, 84f, -4f), CavL(pos, dir, side, sc, 112f, 2f), CavL(pos, dir, side, sc, 84f, 8f), CavSteel.WithAlpha(alpha));
            Circle(CavL(pos, dir, side, sc, 106f, 2f), 14f * sc, CavGold.WithAlpha(0.35f * alpha), 12);
            var pen = CavGold.WithAlpha(0.95f * alpha);
            CavTri(CavL(pos, dir, side, sc, 70f, 3f), CavL(pos, dir, side, sc, 48f, -5f + fl), CavL(pos, dir, side, sc, 46f, 9f - fl * 0.8f), pen);
            CavTri(CavL(pos, dir, side, sc, -2f, -8f), CavL(pos, dir, side, sc, -24f, 0f), CavL(pos, dir, side, sc, -2f, 8f), CavCape.WithAlpha(alpha));
            Circle(CavL(pos, dir, side, sc, 2f, -8f), 5.5f * sc, CavArmour.WithAlpha(alpha), 10);
            Circle(CavL(pos, dir, side, sc, 2f, 8f), 5.5f * sc, CavArmour.WithAlpha(alpha), 10);
            Circle(CavL(pos, dir, side, sc, 4f, 0f), 8f * sc, CavSteel.WithAlpha(alpha), 12);
            Line(CavL(pos, dir, side, sc, -4f, 0f), CavL(pos, dir, side, sc, 2f, 0f), 3.5f * sc, CavGold.WithAlpha(alpha));
        }
    }
}
