using UnityEngine;

namespace Arena
{
    /// Living Bolt's look (Core/LivingBolt.cs; from its mockup). Drawing only, from the replicated BoltT and BoltAge, the
    /// residue zones and FxKind.LivingBolt.
    ///   - the channel: a violet ring closing in and six arcs jumping inward onto the body;
    ///   - electrified: a cyan halo, small arcs on the body re-struck 20 times a second, the time left draining round you in
    ///     gold, flickering violet over the last 2 s;
    ///   - a dash (FxKind.LivingBolt): a jagged violet-and-white bolt from where you were to where you are, with branches,
    ///     fading afterimages along it and a ring where you land; each enemy shocked gets a short arc (FxKind.RodArc);
    ///   - the residue (ZoneKind.StaticResidue): a faint lane with a thin bolt crackling along it, re-struck 14 times a
    ///     second, sparks hopping off, fading out over its last 0.6 s.
    /// With Visual effects off: plain rings and straight lines.
    public partial class ArenaApp
    {
        const float BoltFlickerAt = 2f;   // the last seconds flicker
        const float BoltLit = 0.18f;      // a dash's bolt is up this long; its afterimages and landing ring outlast it

        /// 0 to 1: how strong the charge looks once past the channel (fading over its last 0.3 s, flickering over its last 2 s).
        static float BoltStrength(Player p, float t)
        {
            if (p.BoltT <= 0f || p.Downed || p.BoltAge < Game.BoltChannel)
                return 0f;
            float k = Mathf.Clamp01(p.BoltT / 0.3f);
            if (p.BoltT < BoltFlickerAt && Mathf.Sin(t * 38f) > 0.2f)
                k *= 0.5f;
            return k;
        }

        /// The share of the electrified time left, 0..1 (for the gold ring).
        static float BoltLeft(Player p)
        {
            float total = p.BoltAge + p.BoltT - Game.BoltChannel;
            return total > 0.01f ? Mathf.Clamp01(p.BoltT / total) : 0f;
        }

        /// WorldRenderer.DrawPlayerAuras: the channel, and the halo and gold time ring.
        void DrawLivingBoltAura(Player p, float t)
        {
            if (p.BoltT <= 0f || p.Downed)
                return;
            float r = p.Def.BodyR;
            V2 c = p.Disp;
            if (p.BoltAge < Game.BoltChannel)
            {
                float k = Mathf.Clamp01(p.BoltAge / Game.BoltChannel), close = r + 6f + 50f * (1f - EaseOut3(k));
                Arc(c, close, 2f, Game.BoltViolet.WithAlpha(0.3f + 0.5f * k), 40);
                if (!fxOn)
                    return;
                int strike = Mathf.FloorToInt(t * 24f);
                for (int j = 0; j < 6; j++)
                {
                    V2 d = V2.FromAngle(j * MathX.Tau / 6f + t * 3f + RodHash(strike + j) * 0.5f);
                    int seed = strike * 11 + j + p.Id * 37;
                    Jag(c + d * close, c + d * (r + 1f), 3.5f, Game.BoltViolet.WithAlpha(0.3f * k), seed, 4, 7f, true);
                    Jag(c + d * close, c + d * (r + 1f), 1.3f, (j % 2 == 0 ? Game.BoltCyan : Game.BoltWhite).WithAlpha(0.5f + 0.5f * k), seed, 4, 7f, true);
                }
                return;
            }
            float s = BoltStrength(p, t);
            Rgba ring = p.BoltT < BoltFlickerAt && Mathf.Sin(t * 38f) > 0.2f ? Game.BoltViolet : Game.BoltCyan;
            float left = BoltLeft(p);
            if (!fxOn)
            {
                Arc(c, r + 8f, 2f, ring.WithAlpha(0.6f * s), 28);
                Arc(c, r + 13f, 2.5f, Game.BoltGold.WithAlpha(0.85f * s), 32, -MathX.Pi / 2f, MathX.Tau * left);
                return;
            }
            float breath = 0.5f + 0.5f * Mathf.Sin(t * 8f);
            Arc(c, r + 10f + 2f * breath, 6f, ring.WithAlpha(0.16f * s), 32);
            Arc(c, r + 7f, 1.5f, Game.BoltWhite.WithAlpha((0.4f + 0.3f * breath) * s), 32);
            Arc(c, r + 15f, 3f, Game.BoltGold.WithAlpha(0.18f * s), 40);
            Arc(c, r + 15f, 2.5f, Game.BoltGold.WithAlpha(0.9f * s), 40, -MathX.Pi / 2f, MathX.Tau * left);
            int st = Mathf.FloorToInt(t * 20f);
            for (int j = 0; j < 3; j++)  // sparks flying off
            {
                float ph = Mathf.Repeat(t * 2.6f + j * 0.33f + p.Id * 0.21f, 1f);
                V2 d = V2.FromAngle(RodHash(st / 4 + j * 17 + p.Id) * MathX.Tau);
                Circle(c + d * (r + 6f + ph * 22f), 1.7f * (1f - ph), Game.BoltWhite.WithAlpha(s * (1f - ph)), 6);
            }
        }

        /// WorldRenderer, after the player's body: arcs jumping on it while electrified.
        void DrawLivingBoltSparks(Player p, float t)
        {
            float s = BoltStrength(p, t);
            if (s <= 0f || !fxOn || LaunchHides(p))
                return;
            float r = p.Def.BodyR;
            V2 c = p.Disp;
            int strike = Mathf.FloorToInt(t * 20f);
            for (int j = 0; j < 3; j++)
            {
                V2 d = V2.FromAngle(t * 9f + j * 2.1f + RodHash(strike + j * 5) * 0.8f);
                int seed = strike * 7 + j + p.Id * 41;
                Jag(c + d * (r * 0.3f), c + d * (r + 12f), 3.5f, Game.BoltCyan.WithAlpha(0.3f * s), seed, 4, 6f, true);
                Jag(c + d * (r * 0.3f), c + d * (r + 12f), 1.4f, Game.BoltWhite.WithAlpha(0.9f * s), seed, 4, 6f, true);
            }
        }

        /// WorldEffects.cs: FxKind.LivingBolt, one dash.
        void DrawLivingBoltFx(Effect fx)
        {
            V2 a = fx.Pos, b = fx.Pos2;
            float age = fx.T, f = Mathf.Clamp01(age / Mathf.Max(0.01f, fx.Dur)), fade = 1f - f;
            if (!fxOn)
            {
                Line(a, b, 2.5f, Game.BoltCyan.WithAlpha(fade));
                Arc(b, 14f + 20f * f, 2f, Game.BoltCyan.WithAlpha(fade), 20);
                return;
            }
            float len = a.DistanceTo(b);
            int n = Mathf.Max(4, Mathf.RoundToInt(len / 20f));
            int seed = Mathf.RoundToInt(a.X * 7f + b.Y * 13f) + Mathf.FloorToInt(age * 30f);  // it re-strikes as it fades
            // afterimages: you, left behind along the path
            for (int i = 1; i <= 4; i++)
            {
                float u = i / 5f, lag = Mathf.Clamp01((age - (1f - u) * 0.06f) / (fx.Dur * 0.85f));
                if (lag < 1f && age >= (1f - u) * 0.06f)
                    Circle(V2.Lerp(a, b, u), 11f, Game.BoltCyan.WithAlpha(0.32f * (1f - lag) * u), 16);
            }
            if (age < BoltLit)
            {
                float k = 1f - age / BoltLit;
                Jag(a, b, 9f, Game.BoltViolet.WithAlpha(0.3f * k), seed, n, 16f, false);   // the outer glow
                Jag(a, b, 3f, Game.BoltWhite.WithAlpha(k), seed, n, 16f, false);            // the white-hot core
                Jag(a, b, 1.3f, Game.BoltCyan.WithAlpha(0.8f * k), seed + 9, n, 22f, false); // a second strand
                for (int j = 0; j < 2; j++)  // branches off it
                {
                    V2 mid = V2.Lerp(a, b, 0.25f + 0.5f * RodHash(seed + j * 3));
                    V2 end = mid + V2.FromAngle(RodHash(seed + j * 5 + 1) * MathX.Tau) * (20f + 26f * RodHash(seed + j * 7 + 2));
                    Jag(mid, end, 1.2f, Game.BoltCyan.WithAlpha(0.7f * k), seed + 21 + j, 3, 8f, true);
                }
                Circle(a, 6f * k + 2f, Game.BoltViolet.WithAlpha(0.6f * k), 12);
            }
            // the landing: a ring rolling out
            Arc(b, 12f + 34f * EaseOut3(f), 3f * fade + 0.5f, Game.BoltCyan.WithAlpha(0.8f * fade), 28);
        }

        /// WorldEnemies' zones: ZoneKind.StaticResidue, a dash's live trail.
        void DrawStaticResidue(Zone z, float t)
        {
            V2 ex = V2.FromAngle(z.Aim);
            V2 a = z.Pos - ex * z.Radius, b = z.Pos + ex * z.Radius;
            float k = Mathf.Clamp01(z.T / 0.6f);
            if (k <= 0f)
                return;
            if (!fxOn)
            {
                Line(a, b, 2f, Game.BoltCyan.WithAlpha(0.5f * k));
                return;
            }
            Line(a, b, Game.ResidueWidth * 2f, Game.BoltCyan.WithAlpha(0.09f * k));
            Line(a, b, Game.ResidueWidth * 0.8f, Game.BoltCyan.WithAlpha(0.07f * k));
            int n = Mathf.Max(3, Mathf.RoundToInt(z.Radius * 2f / 18f));
            int strike = Mathf.FloorToInt(t * 14f), seed = z.Id * 31 + strike;
            float lit = 0.55f + 0.45f * RodHash(seed);
            Jag(a, b, 1.6f, Game.BoltCyan.WithAlpha(lit * k), seed, n, 7f, false);
            if (RodHash(seed + 3) > 0.5f)
                Jag(a, b, 0.9f, Game.BoltViolet.WithAlpha(0.8f * k), seed + 7, n, 11f, false);
            for (int j = 0; j < 2; j++)  // sparks hopping off it
            {
                float u = RodHash(seed * 3 + j), ph = Mathf.Repeat(t * 7f + j * 0.5f, 1f);
                V2 at = V2.Lerp(a, b, u) + V2.FromAngle(RodHash(seed + j * 11) * MathX.Tau) * (4f + 10f * ph);
                Circle(at, 1.6f * (1f - ph), Game.BoltWhite.WithAlpha(k * (1f - ph)), 6);
            }
        }

        /// Juice.DrawGlowPass: the charge, each dash's flash and the residue's glow.
        void DrawLivingBoltGlow()
        {
            float t = game.AnimT;
            foreach (var p in game.Players.Values)
            {
                if (p.BoltT <= 0f || p.Downed || LaunchHides(p))
                    continue;
                float s = p.BoltAge < Game.BoltChannel ? 0.5f * Mathf.Clamp01(p.BoltAge / Game.BoltChannel) : BoltStrength(p, t);
                if (s > 0f)
                    GlowDisc(p.Disp, p.Def.BodyR * 3.2f, p.BoltAge < Game.BoltChannel ? Game.BoltViolet : Game.BoltCyan, (0.38f + 0.07f * Mathf.Sin(t * 9f)) * s);
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.LivingBolt && fx.T < BoltLit)
                {
                    float k = 1f - fx.T / BoltLit;
                    for (int i = 0; i <= 3; i++)
                        GlowDisc(V2.Lerp(fx.Pos, fx.Pos2, i / 3f), 34f, Game.BoltCyan, 0.45f * k);
                }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.StaticResidue)
                    continue;
                float k = Mathf.Clamp01(z.T / 0.6f);
                V2 ex = V2.FromAngle(z.Aim);
                int n = Mathf.Max(1, Mathf.RoundToInt(z.Radius / 40f));
                for (int i = -n; i <= n; i++)
                    GlowDisc(z.Pos + ex * (z.Radius * i / n), 26f, Game.BoltCyan, 0.12f * k);
            }
        }
    }
}
