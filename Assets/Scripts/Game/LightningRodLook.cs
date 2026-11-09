using UnityEngine;

namespace Arena
{
    /// Lightning Rod's look (Docs/Mockups/lightning-rod.html; Core/LightningRod.cs). Drawing only.
    ///   - the charge: while Player.RodT is up, a blue glow round the tank, a crackling halo and small arcs jumping on its
    ///     body (re-struck 20 times a second), flickering over the last second;
    ///   - FxKind.RodBolt: a jagged bolt from the sky onto where the shooter stands, a white flash on it and a scorch ring
    ///     on the floor (the damage itself shows as the usual numbers);
    ///   - FxKind.RodArc: each arc on from it, a thinner jagged line;
    ///   - FxKind.RodGround: a small grey "-2" off the tank, what the grounded hit took (on the HUD, with damage numbers on).
    /// With Visual effects off: a plain blue ring round the tank, and straight lines for the bolt and arcs.
    public partial class ArenaApp
    {
        static readonly Rgba RodPale = new Rgba(0.82f, 0.92f, 1f), RodHot = new Rgba(0.95f, 0.98f, 1f);
        static readonly Rgba RodGrey = new Rgba(0.6f, 0.64f, 0.68f);
        const float RodSkyRise = 560f;    // how far above the struck enemy the bolt starts (off the top of the view)
        const float RodBoltLit = 0.25f;   // the bolt itself is up this long; the flash and scorch outlast it

        /// 0 to 1: how strong the charge looks (fades over its last half second and flickers over its last second).
        static float RodStrength(Player p, float t)
        {
            if (p.RodT <= 0f || p.Downed)
                return 0f;
            float k = Mathf.Clamp01(p.RodT / 0.5f);
            if (p.RodT < 1f && Mathf.Sin(t * 40f) > 0.3f)
                k *= 0.45f;
            return k;
        }

        /// WorldRenderer.DrawPlayerAuras: the charge round a player.
        void DrawRodAura(Player p, float t)
        {
            float k = RodStrength(p, t);
            if (k <= 0f)
                return;
            float r = p.Def.BodyR;
            V2 c = p.Disp;
            if (!fxOn)
            {
                Arc(c, r + 9f, 2f, Game.RodBlue.WithAlpha((0.55f + 0.25f * Mathf.Sin(t * 9f)) * k), 28);  // plain: a pulsing blue ring
                return;
            }
            // The halo: a soft band and a thin bright ring, breathing.
            float breath = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            Arc(c, r + 11f + 2f * breath, 6f, Game.RodBlue.WithAlpha(0.16f * k), 32);
            Arc(c, r + 8f, 1.5f, RodPale.WithAlpha((0.45f + 0.3f * breath) * k), 32);
            // A spark or two flying off (the arcs on the body are drawn over it: DrawRodSparks).
            int strike = Mathf.FloorToInt(t * 20f);
            for (int j = 0; j < 3; j++)
            {
                float ph = Mathf.Repeat(t * 2.2f + j * 0.33f + p.Id * 0.17f, 1f);
                V2 d = V2.FromAngle(RodHash(strike / 4 + j * 13) * MathX.Tau);
                Circle(c + d * (r + 6f + ph * 18f), 1.6f * (1f - ph), RodHot.WithAlpha(k * (1f - ph)), 6);
            }
        }

        /// WorldRenderer, after the player's body: small arcs jumping on it, four spokes turning, each re-struck 20 times a second.
        void DrawRodSparks(Player p, float t)
        {
            float k = RodStrength(p, t);
            if (k <= 0f || !fxOn || LaunchHides(p))
                return;
            float r = p.Def.BodyR;
            V2 c = p.Disp;
            int strike = Mathf.FloorToInt(t * 20f);
            for (int j = 0; j < 4; j++)
            {
                float a = t * 7f + j * 1.6f;
                V2 d = V2.FromAngle(a);
                int seed = strike * 7 + j + p.Id * 31;
                Jag(c + d * (r * 0.35f), c + d * (r + 16f), 3.5f, Game.RodBlue.WithAlpha(0.3f * k), seed, 4, 6f, true);
                Jag(c + d * (r * 0.35f), c + d * (r + 16f), 1.5f, RodHot.WithAlpha(0.9f * k), seed, 4, 6f, true);
            }
        }

        /// Juice.DrawGlowPass: the charge's glow, and each bolt's flash.
        void DrawRodGlow()
        {
            float t = game.AnimT;
            foreach (var p in game.Players.Values)
            {
                float k = RodStrength(p, t);
                if (k > 0f && !LaunchHides(p))
                {
                    GlowDisc(p.Disp, p.Def.BodyR * 3.4f, Game.RodBlue, (0.42f + 0.08f * Mathf.Sin(t * 9f)) * k);
                    GlowDisc(p.Disp, p.Def.BodyR * 1.6f, RodPale, 0.25f * k);
                }
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.RodBolt && fx.T < 0.4f)
                {
                    float f = fx.T / 0.4f;
                    GlowDisc(fx.Pos, 26f + fx.Radius + 30f * f, RodPale, 0.8f * (1f - f));
                }
                else if (fx.Kind == FxKind.RodArc && fx.T < fx.Dur)
                    GlowDisc(fx.Pos2, 16f, Game.RodBlue, 0.5f * (1f - fx.T / fx.Dur));
            }
        }

        /// WorldEffects.cs: FxKind.RodBolt.
        void DrawRodBoltFx(Effect fx)
        {
            float a = fx.T, er = fx.Radius;
            V2 at = fx.Pos;
            int seed = Mathf.RoundToInt(at.X * 7f + at.Y * 13f);
            V2 top = at + new V2(8f + (RodHash(seed) - 0.5f) * 40f, -RodSkyRise);
            if (!fxOn)
            {
                if (a < RodBoltLit)
                {
                    Line(top, at, 2f, RodPale.WithAlpha(1f - a / RodBoltLit));
                    Arc(at, er + 6f, 2f, Game.RodBlue.WithAlpha(1f - a / RodBoltLit), 20);
                }
                return;
            }
            // The scorch on the floor: a ring rolling out and a dark mark fading.
            float s = Mathf.Clamp01(a / fx.Dur);
            Arc(at, er + 6f + 26f * EaseOut3(s), 2.5f * (1f - s) + 0.5f, Game.RodBlue.WithAlpha(0.7f * (1f - s)), 28);
            if (a < RodBoltLit)
            {
                float f = a / RodBoltLit, fade = 1f - f;
                int flick = seed + Mathf.FloorToInt(a * 30f);  // it re-strikes as it fades
                Jag(top, at, 7f, Game.RodBlue.WithAlpha(0.28f * fade), flick, 9, 22f, false);  // the outer glow
                Jag(top, at, 2.6f, RodHot.WithAlpha(fade), flick, 9, 22f, false);              // the white-hot core
                // A branch off the upper half.
                V2 mid = V2.Lerp(top, at, 0.35f + 0.2f * RodHash(flick + 3));
                V2 end = mid + new V2((RodHash(flick + 5) - 0.5f) * 120f, 70f + 40f * RodHash(flick + 6));
                Jag(mid, end, 1.4f, RodPale.WithAlpha(0.7f * fade), flick + 11, 5, 12f, false);
                // The flash on the shooter.
                Circle(at, (er > 0f ? er : 10f) + 4f, RodHot.WithAlpha(0.55f * fade), 18);
                Arc(at, (er > 0f ? er : 10f) + 8f + 14f * f, 2f, RodPale.WithAlpha(fade), 22);
            }
        }

        /// WorldEffects.cs: FxKind.RodArc.
        void DrawRodArcFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), fade = 1f - f;
            if (!fxOn)
            {
                Line(fx.Pos, fx.Pos2, 1.5f, Game.RodBlue.WithAlpha(fade));
                return;
            }
            int seed = Mathf.RoundToInt(fx.Pos2.X * 5f + fx.Pos2.Y * 11f) + Mathf.FloorToInt(fx.T * 30f);
            Jag(fx.Pos, fx.Pos2, 4.5f, Game.RodBlue.WithAlpha(0.25f * fade), seed, 7, 14f, false);
            Jag(fx.Pos, fx.Pos2, 1.6f, RodPale.WithAlpha(fade), seed, 7, 14f, false);
            Circle(fx.Pos2, 4f * fade + 1f, RodHot.WithAlpha(fade), 8);
        }

        /// The HUD: the grey number off the tank for each grounded hit.
        void DrawRodPops()
        {
            if (game == null || !fxNumbers)
                return;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.RodGround || fx.T > 1.1f)
                    continue;
                Player p;
                if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                    continue;
                int taken = Mathf.Max(0, Mathf.RoundToInt(fx.Pos2.Y));
                if (taken <= 0 && fx.Pos2.Y <= 0.05f)
                    continue;  // a shot in the hit grace: grounded, but it took nothing
                float jitter = (RodHash(Mathf.RoundToInt(fx.Pos.X * 3f + fx.Pos.Y)) - 0.5f) * 10f;  // beside the body, clear of the name and health bar
                RisingPop(p.Disp + new V2(p.Def.BodyR + 16f + jitter, -2f), fx.T, "-" + Mathf.Max(1, taken), RodGrey, 12);
            }
        }

        /// A jagged line from a to b: n segments, each kink pushed sideways up to amp (seeded, so a seed holds its shape).
        /// taper: thinner toward b.
        void Jag(V2 a, V2 b, float width, Rgba col, int seed, int n, float amp, bool taper)
        {
            V2 d = b - a;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 nrm = d / len;
            nrm = nrm.Orthogonal();
            V2 prev = a;
            for (int k = 1; k <= n; k++)
            {
                float u = k / (float)n;
                V2 pt = k == n ? b : a + d * u + nrm * ((RodHash(seed * 13 + k) - 0.5f) * amp * 2f * Mathf.Sin(u * MathX.Pi));
                Line(prev, pt, taper ? width * (1.2f - 0.6f * u) : width, col);
                prev = pt;
            }
        }

        static float RodHash(int i)
        {
            float s = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
