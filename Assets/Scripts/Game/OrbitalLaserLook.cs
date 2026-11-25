using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Orbital Laser (Core/OrbitalLaser.cs), the look of Docs/Mockups/orbital-laser.html, drawn from the FxKind.OrbitalLaser
    /// effect's age alone (Pos = the point, Pos2 = where the beam ends up, Radius = the caster's id, Color.A = the radius / 1000),
    /// so every screen draws the same strike.
    ///   DrawOrbitalGround (in the effects pass): the red lock-on circle, its shrinking timer ring, the three-step countdown and
    ///     the gold brackets; after the strike, the molten glass crater with its cracks, rim and embers
    ///   DrawOrbitalSky (over the arena's contents): the red spotting line from the sky, the marked enemies, the dimming and pilot
    ///     beam of the charge, then the flash, the column of light with energy bands running down it, sparks and the shockwave
    ///   DrawOrbitalGlow (the glow pass): the lock-on's red, the charge's white, the beam's blue-white and the crater's heat
    /// The impact shakes the screen once (Juice.cs's Shake/Hold, which Reduced effects turn off; the flash is dimmer then too).
    public partial class ArenaApp
    {
        static readonly Rgba OrbRed = new Rgba(1f, 0.32f, 0.38f), OrbGold = new Rgba(1f, 0.8f, 0.35f), OrbWhite = new Rgba(1f, 1f, 1f);
        static readonly Rgba OrbCyan = new Rgba(0.62f, 0.88f, 1f), OrbBlue = new Rgba(0.3f, 0.55f, 1f), OrbMolten = new Rgba(1f, 0.55f, 0.2f);
        static readonly Rgba OrbGlass = new Rgba(0.13f, 0.06f, 0.05f), OrbInk = new Rgba(0f, 0f, 0f);
        const float OrbSkyUp = 1500f;  // how far up the screen the column reaches (well past the top)
        const int OrbCracks = 16, OrbEmbers = 46, OrbSparks = 34;
        readonly HashSet<Effect> orbShook = new HashSet<Effect>();  // strikes whose impact already shook this screen

        static float OrbR(Effect fx) => fx.Color.A > 0.05f ? fx.Color.A * 1000f : Game.OrbRadius;
        static int OrbSeed(Effect fx) => ((int)(fx.Pos.X * 7.1f) ^ (int)(fx.Pos.Y * 13.3f)) & 0xffff;
        static V2 OrbLerp(V2 a, V2 b, float k) => a + (b - a) * k;

        /// How far through the lock-on (0 at OrbLockT, 1 at the strike), and the circle's grow-in.
        static float OrbLock(float age) => Mathf.Clamp01((age - Game.OrbLockT) / (Game.OrbHitT - Game.OrbLockT));

        /// The beam's strength while it fires: a fast rise, a hold, a fade over its last quarter.
        static float OrbBeamEnv(float age)
        {
            float bt = (age - Game.OrbHitT) / Game.OrbBeamT;
            if (bt < 0f || bt > 1f)
                return 0f;
            return bt < 0.08f ? bt / 0.08f : bt > 0.75f ? (1f - bt) / 0.25f : 1f;
        }

        /// The scorch's heat, 1 as the beam lands down to 0 when it stops burning; and how much of the crater is left to see.
        static float OrbHeat(float age) => 1f - Mathf.Clamp01((age - Game.OrbHitT) / (Game.OrbScorchEndT - Game.OrbHitT));
        static float OrbCraterAlpha(float age) => Mathf.Clamp01((Game.OrbFxDur - age) / (Game.OrbFadeT + 0.5f));

        /// A ring from r0 to r1, its colour running from c0 (inside) to c1 (outside): r0 near 0 makes a soft disc.
        void OrbBand(V2 c, float r0, float r1, Rgba c0, Rgba c1, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / segs), d1 = V2.FromAngle(MathX.Tau * (i + 1) / segs);
                VQuad(c + d0 * r0, c0, c + d0 * r1, c1, c + d1 * r1, c1, c + d1 * r0, c0);
            }
        }

        void DrawOrbitalGround(Effect fx)
        {
            float age = fx.T, r = OrbR(fx), t = game.AnimT;
            int seed = OrbSeed(fx);
            V2 c = fx.Pos;

            if (age < Game.OrbHitT)
            {
                if (age < Game.OrbLockT)
                    return;
                float k = CavEaseOut((age - Game.OrbLockT) / 0.35f), lk = OrbLock(age), rr = r * k;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                // The danger circle: a red wash, darker at the edge, and its rim.
                OrbBand(c, 1f, rr, OrbRed.WithAlpha(0.05f + 0.04f * pulse), OrbRed.WithAlpha(0.2f + 0.08f * pulse), 48);
                Arc(c, rr, 3f, OrbRed.WithAlpha(0.75f + 0.25f * pulse), 64);
                Arc(c, rr - 9f, 1.5f, OrbRed.WithAlpha(0.35f), 64);
                // The timer: a pale ring closing in on the point.
                Arc(c, Mathf.Max(4f, rr * (1f - lk)), 2f, new Rgba(1f, 0.82f, 0.82f, 0.8f), 48);
                // The countdown: three arcs outside the rim, one going out each third of the lock-on; the last one left blinks.
                int left = Mathf.CeilToInt((1f - lk) * 3f);
                for (int i = 0; i < 3; i++)
                {
                    if (i >= left)
                        continue;
                    bool blink = i == left - 1 && Mathf.Sin(t * 30f) < 0f;
                    Arc(c, rr + 30f, 6f, OrbGold.WithAlpha(blink ? 0.35f : 0.95f), 20, -MathX.Tau / 4f + i * MathX.Tau / 3f + 0.12f, MathX.Tau / 3f - 0.24f);
                }
                // The point: four gold ticks round it.
                for (int i = 0; i < 4; i++)
                {
                    V2 d = V2.FromAngle(i * MathX.Tau / 4f);
                    Line(c + d * 14f, c + d * 40f, 2.5f, OrbGold.WithAlpha(0.9f));
                }
                return;
            }

            // The crater: molten glass where the beam stood, and the gouge it ploughed when mastered.
            float heat = OrbHeat(age), a = OrbCraterAlpha(age);
            if (a <= 0f)
                return;
            V2 end = fx.Pos2;
            int steps = end.DistanceTo(c) > 4f ? 6 : 1;
            for (int s = 0; s < steps; s++)
            {
                V2 p = steps == 1 ? end : OrbLerp(c, end, s / (float)(steps - 1));
                OrbBand(p, 1f, r, OrbGlass.WithAlpha(0.6f * a), OrbGlass.WithAlpha(0f), 40);
            }
            Rgba core = new Rgba(1f, Mathf.Lerp(0.45f, 0.95f, heat), Mathf.Lerp(0.15f, 0.7f, heat));
            OrbBand(end, 1f, r * 0.85f, core.WithAlpha((0.55f * heat + 0.12f) * a), OrbMolten.WithAlpha(0.22f * heat * a), 40);
            // Cracks running out from the middle, glowing while it's hot.
            for (int i = 0; i < OrbCracks; i++)
            {
                float ang = CavHash(seed + i * 5) * MathX.Tau, len = 0.55f + 0.5f * CavHash(seed + i * 9), bend = (CavHash(seed + i * 13) - 0.5f) * 0.6f;
                V2 prev = end;
                for (int j = 1; j <= 5; j++)
                {
                    ang += bend * 0.4f;
                    V2 next = end + V2.FromAngle(ang) * (r * len * j / 5f);
                    Line(prev, next, 2.2f - j * 0.25f, OrbMolten.WithAlpha((0.3f + 0.6f * heat) * a));
                    prev = next;
                }
            }
            // The rim: a dashed ring of cooling slag.
            for (int i = 0; i < 24; i++)
                Arc(end, r * 0.97f, 4f, OrbMolten.WithAlpha((0.15f + 0.45f * heat) * a), 4, i * MathX.Tau / 24f, MathX.Tau / 48f);
            // Embers lifting off it.
            for (int i = 0; i < OrbEmbers; i++)
            {
                float h1 = CavHash(seed + i * 3 + 7), h2 = CavHash(seed + i * 11 + 3), life = (t * 0.55f + h1) % 1f;
                V2 p = end + V2.FromAngle(h2 * MathX.Tau) * (r * 0.92f * Mathf.Sqrt(CavHash(seed + i * 17))) + new V2(Mathf.Sin(t * 3f + i) * 4f, -life * 46f);
                Circle(p, 2.2f, new Rgba(1f, 0.6f + 0.3f * h1, 0.25f, heat * a * (1f - life)), 6);
            }
        }

        void DrawOrbitalSky()
        {
            int live = 0;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.OrbitalLaser)
                {
                    DrawOrbitalSkyFx(fx);
                    live++;
                }
            if (orbShook.Count > live)
                orbShook.RemoveWhere(e => !game.Effects.Contains(e));
        }

        void DrawOrbitalSkyFx(Effect fx)
        {
            float age = fx.T, r = OrbR(fx), t = game.AnimT;
            int seed = OrbSeed(fx);
            V2 pos = fx.Pos, up = new V2(0f, -1f), top = pos + up * OrbSkyUp;

            if (age < Game.OrbHitT)
            {
                // The charge dims the world round the strike, deepening to the impact.
                float lk = OrbLock(age);
                if (lk > 0f)
                    Circle(pos, 2600f, OrbInk.WithAlpha(0.38f * lk * lk), 48);
                // The spotting line from the satellite.
                float u = CavEaseOut(age / Game.OrbLockT);
                Line(top, OrbLerp(top, pos, u), 2.5f, OrbRed.WithAlpha(0.85f));
                if (u >= 1f)
                    Circle(pos, 5f + 1.5f * Mathf.Sin(t * 20f), OrbRed, 10);
                // Marked: gold corners on every enemy inside the circle.
                if (age > Game.OrbLockT)
                    foreach (var e in game.Enemies.Values)
                    {
                        if (game.IsSpawning(e) || !e.Disp.CloserThan(pos, r + e.Radius))
                            continue;
                        float m = e.Radius + 7f, s = 6f;
                        var col = OrbGold.WithAlpha(Mathf.Clamp01((age - Game.OrbLockT) * 4f));
                        for (int q = 0; q < 4; q++)
                        {
                            float sx = q == 0 || q == 3 ? -1f : 1f, sy = q < 2 ? -1f : 1f;
                            V2 corner = e.Disp + new V2(sx * m, sy * m);
                            Line(corner, corner - new V2(0f, sy * s), 2f, col);
                            Line(corner, corner - new V2(sx * s, 0f), 2f, col);
                        }
                    }
                // Spinning gold brackets on the rim.
                if (age > Game.OrbLockT)
                {
                    float rr = r * CavEaseOut((age - Game.OrbLockT) / 0.35f) + 16f;
                    for (int i = 0; i < 4; i++)
                        Arc(pos, rr, 4f, OrbGold, 8, t * 1.6f + i * MathX.Tau / 4f - 0.25f, 0.5f);
                }
                // The pilot beam: a thin white line thickening into the impact.
                if (age > Game.OrbChargeT)
                {
                    float q = (age - Game.OrbChargeT) / (Game.OrbHitT - Game.OrbChargeT);
                    Line(top, pos, 2f + 7f * q, OrbWhite.WithAlpha(0.45f + 0.5f * q));
                    Circle(pos, 10f + 22f * q, OrbWhite.WithAlpha(0.5f * q), 16);
                }
                return;
            }

            if (orbShook.Add(fx) && age < Game.OrbHitT + 0.5f)
            {
                Shake(0.6f);
                Hold(0.07f, 0.3f);
            }

            // The column of light, coming down from off the top of the screen onto the beam's spot.
            float env = OrbBeamEnv(age);
            if (env > 0f)
            {
                V2 c = Game.OrbCentre(fx.Pos, fx.Pos2, age), ctop = c + up * OrbSkyUp;
                float wob = 1f + 0.05f * Mathf.Sin(t * 60f);
                OrbBand(c, 1f, r * 1.3f, OrbWhite.WithAlpha(0.8f * env), OrbBlue.WithAlpha(0f), 40);
                OrbColumn(c, ctop, r * 1.0f * wob * env, OrbBlue, 0.4f * env);
                OrbColumn(c, ctop, r * 0.7f * wob * env, OrbCyan, 0.6f * env);
                OrbColumn(c, ctop, r * 0.42f * wob * env, new Rgba(0.92f, 0.98f, 1f), 0.9f * env);
                OrbColumn(c, ctop, r * 0.2f * wob * env, OrbWhite, env);
                // Energy bands running down the column.
                for (int i = 0; i < 6; i++)
                {
                    float f = (t * 2.5f + i / 6f) % 1f;
                    V2 at = OrbLerp(ctop, c, f);
                    float half = r * (0.3f + 0.25f * f) * env;
                    Line(at - new V2(half, 0f), at + new V2(half, 0f), 3f, OrbCyan.WithAlpha(0.4f * env * (1f - Mathf.Abs(f - 0.5f) * 2f)));
                }
                // Sparks thrown up out of the strike.
                for (int i = 0; i < OrbSparks; i++)
                {
                    float h1 = CavHash(seed + i * 7 + 1), h2 = CavHash(seed + i * 3 + 5), life = (t * 2.2f + h1) % 1f;
                    V2 from = c + V2.FromAngle(h2 * MathX.Tau) * (r * Mathf.Sqrt(CavHash(seed + i * 19)));
                    V2 at = from + new V2((h2 - 0.5f) * 80f * life, -life * (160f + 240f * h1));
                    Line(at, at + new V2(0f, 18f), 2f, OrbCyan.WithAlpha(env * (1f - life)));
                }
            }

            // The shockwave rolling out from the point.
            float sw = (age - Game.OrbHitT) / 0.7f;
            if (sw < 1f)
            {
                Arc(pos, r * (0.4f + 1.6f * sw), 18f * (1f - sw) + 2f, new Rgba(1f, 0.95f, 0.8f, 1f - sw), 64);
                Arc(pos, r * (0.3f + 2.2f * sw), 4f, OrbCyan.WithAlpha(0.6f * (1f - sw)), 64);
            }
            // The flash.
            float fl = 1f - (age - Game.OrbHitT) / 0.3f;
            if (fl > 0f)
                Circle(pos, 3200f, OrbWhite.WithAlpha(fl * (fxReduced ? 0.25f : 0.85f)), 48);
        }

        /// One layer of the column: a trapezoid from the sky (narrower) down to the ground, bright down its middle, clear at its sides, with a round foot.
        void OrbColumn(V2 c, V2 top, float half, Rgba col, float a)
        {
            if (half < 1f || a <= 0f)
                return;
            Rgba mid = col.WithAlpha(a), clear = col.WithAlpha(0f);
            V2 x = new V2(1f, 0f), tx = x * (half * 0.55f), bx = x * half;
            VQuad(top - tx, clear, top, mid, c, mid, c - bx, clear);
            VQuad(top, mid, top + tx, clear, c + bx, clear, c, mid);
            OrbBand(c, 1f, half, mid, clear, 32);
        }

        void DrawOrbitalGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.OrbitalLaser)
                    continue;
                float age = fx.T, r = OrbR(fx);
                if (age < Game.OrbHitT)
                {
                    if (age > Game.OrbLockT)
                        GlowDisc(fx.Pos, 34f, OrbRed, 0.4f);
                    if (age > Game.OrbChargeT)
                    {
                        float q = (age - Game.OrbChargeT) / (Game.OrbHitT - Game.OrbChargeT);
                        GlowDisc(fx.Pos, 40f + 70f * q, OrbWhite, 0.6f * q);
                    }
                    continue;
                }
                float env = OrbBeamEnv(age);
                if (env > 0f)
                    GlowDisc(Game.OrbCentre(fx.Pos, fx.Pos2, age), r * 1.6f, OrbCyan, 0.8f * env);
                float heat = OrbHeat(age) * OrbCraterAlpha(age);
                if (heat > 0f)
                    GlowDisc(fx.Pos2, r * 1.1f, OrbMolten, 0.5f * heat);
            }
        }
    }
}
