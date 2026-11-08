using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Greatsword's chain, guard and Rend (Docs/Mockups/greatsword-combo.html, the Moonlit Crescent look). Drawing only, with Visual effects on.
    ///   - the cuts and the overhead rend are ordinary Slash effects drawn by MeleeLooks.cs; the owner's GreatStep says which way
    ///     each sweeps (the return cut goes back), and DrawSteelWallFx (WorldEffects.cs) hangs each crescent the same way;
    ///   - the chain shows as three diamonds behind you (DrawGreatPips), the Temper as six, gold when full or when the next chain
    ///     starts on the rend;
    ///   - the guard is a glassy 120 degree arc in front of you (DrawGreatGuard) that brightens where a bullet died on it;
    ///   - the Rend (FxKind.GreatRend: Pos to Pos2 along the aim, Radius half its width, the colour's alpha its Temper) is a lance
    ///     of light with a crescent head and a trail of fading ones, wider for every point of Temper and gold at six.
    public partial class ArenaApp
    {
        static readonly Rgba GreatMoon = new Rgba(0.78f, 0.88f, 1f), GreatGold = new Rgba(1f, 0.84f, 0.51f);

        readonly Dictionary<Effect, float> greatWallSign = new Dictionary<Effect, float>();

        /// Which way a crescent hangs: the way its cut swept (the return cut, step 2, sweeps back). Fixed when first drawn.
        float GreatWallSign(Effect fx, V2 center)
        {
            float sign;
            if (greatWallSign.TryGetValue(fx, out sign))
                return sign;
            Player best = null;
            float bd = 44f;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || p.Weapon.Id != WeaponId.Greatsword)
                    continue;
                float d = Mathf.Min(p.Disp.DistanceTo(center), p.Pos.DistanceTo(center));
                if (d < bd)
                {
                    bd = d;
                    best = p;
                }
            }
            sign = best != null && best.GreatStep == 2 ? -1f : 1f;
            if (greatWallSign.Count > 24)
            {
                var live = new HashSet<Effect>(game.Effects);
                var gone = new List<Effect>();
                foreach (var k in greatWallSign.Keys)
                    if (!live.Contains(k))
                        gone.Add(k);
                foreach (var k in gone)
                    greatWallSign.Remove(k);
            }
            greatWallSign[fx] = sign;
            return sign;
        }

        /// The chain's three diamonds, or the Temper's six, on an arc behind you.
        void DrawGreatPips(Player p)
        {
            if (!fxOn || p.Downed)
                return;
            float t = Time.unscaledTime;
            bool temper = p.GreatGuard || p.GreatTemper > 0 || p.GreatPrimed;
            if (temper)
            {
                int n = p.GreatPrimed && !p.GreatGuard ? Game.GreatTemperMax : Mathf.Min(Game.GreatTemperMax, (int)p.GreatTemper);
                bool gold = n >= Game.GreatTemperMax;
                for (int d = 0; d < Game.GreatTemperMax; d++)
                {
                    float da = p.Aim + MathX.Pi + (d - 2.5f) * 0.34f;
                    V2 c = p.Disp + V2.FromAngle(da) * 30f;
                    Polygon(c, 4.3f + (gold ? 0.8f * Mathf.Sin(t * 10f) + 0.8f : 0f), 4, 0f, d < n ? (gold ? GreatGold : GreatMoon) : new Rgba(1f, 1f, 1f, 0.14f));
                }
                if (gold)
                    Arc(p.Disp, 26f, 3f, GreatGold.WithAlpha(0.25f + 0.1f * Mathf.Sin(t * 9f)), 32);
                return;
            }
            if (p.GreatStep == 0)
                return;
            for (int d = 0; d < 3; d++)
            {
                float da = p.Aim + MathX.Pi + (d - 1f) * 0.4f;
                V2 c = p.Disp + V2.FromAngle(da) * 30f;
                bool lit = d < p.GreatStep;
                Polygon(c, lit ? 5f : 4.3f, 4, 0f, lit ? GreatMoon : new Rgba(1f, 1f, 1f, 0.14f));
                if (d == 2 && p.GreatStep == 2)  // the rend is next
                    Polygon(c, 6.5f + 1.5f * Mathf.Sin(t * 10f), 4, 0f, Rgba.White.WithAlpha(0.35f));
            }
        }

        /// The guard arc while the weapon-action key is held.
        void DrawGreatGuard(Player p)
        {
            if (!fxOn || p.Downed || !p.GreatGuard)
                return;
            float t = Time.unscaledTime, r = Game.GreatGuardRadius, half = Game.GreatGuardHalfArc;
            float fill = Mathf.Clamp01((float)p.GreatTemper / Game.GreatTemperMax);
            bool full = p.GreatTemper >= Game.GreatTemperMax;
            Rgba col = full ? GreatGold : GreatMoon;
            V2 o = p.Disp;
            float a0 = p.Aim - half, sweep = 2f * half;
            Arc(o, r, Game.GreatGuardBand * 2f, col.WithAlpha(0.13f), 24, a0, sweep);
            Arc(o, r, 7f, col.WithAlpha(0.4f + 0.15f * fill), 24, a0, sweep);
            Arc(o, r, 2.4f, Rgba.White.WithAlpha(0.7f), 24, a0, sweep);
            float gl = Mathf.Repeat(t * 1.1f, 1.3f) - 0.15f;
            if (gl > 0f && gl < 1f)
            {
                float ga = a0 + sweep * gl;
                GlowDisc(o + V2.FromAngle(ga) * r, 22f, Rgba.White, 0.55f);
                Arc(o, r, 2.4f, Rgba.White.WithAlpha(0.9f), 6, ga - 0.07f, 0.14f);
            }
            var hitCol = Defs.BulletColor(Defs.KnightSlash);
            foreach (var sp in game.Effects)  // the host's spark where a bullet died: the arc brightens there
            {
                if (sp.Kind != FxKind.Spark || sp.Radius != 8f || sp.Color.R != hitCol.R || sp.Color.G != hitCol.G || sp.Color.B != hitCol.B)
                    continue;
                V2 rel = sp.Pos - o;
                float d = rel.Length, an = rel.Angle;
                float diff = Mathf.Repeat(an - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (Mathf.Abs(d - r) > Game.GreatGuardBand + 12f || Mathf.Abs(diff) > half + 0.1f)
                    continue;
                float q = Mathf.Clamp01(sp.T / 0.4f);
                if (q >= 1f)
                    continue;
                GlowDisc(o + V2.FromAngle(an) * r, 20f * (1f - q * 0.4f), Rgba.White, 0.8f * (1f - q));
                float run = 0.5f * EaseOut3(q);
                Arc(o, r, 2f, Rgba.White.WithAlpha(0.55f * (1f - q)), 12, an - run, 2f * run);
            }
        }

        /// The Rend: a lance of light flying out along the aim with a crescent head, wider for every point of Temper, gold at six.
        void DrawGreatRendFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length;
            if (len < 1f)
                return;
            V2 dir = to / len;
            int n = Mathf.Clamp(Mathf.RoundToInt((fx.Color.A - 0.2f) / 0.8f * Game.GreatTemperMax), 0, Game.GreatTemperMax);
            Rgba col = n >= Game.GreatTemperMax ? GreatGold : GreatMoon;
            float T = fx.T, fa = 1f - Mathf.Clamp01((T - 0.3f) / 0.2f);
            float head = Mathf.Min(len, 60f + 1500f * T), tl = Mathf.Min(head, 420f);
            V2 a = fx.Pos + dir * (head - tl), b = fx.Pos + dir * head;
            Line(a, b, fx.Radius * 1.4f, col.WithAlpha(0.16f * fa));
            Line(a, b, Mathf.Max(4f, fx.Radius * 0.5f), col.WithAlpha(0.45f * fa));
            Line(fx.Pos + dir * (head - tl * 0.7f), b, 2f + n * 0.4f, Rgba.White.WithAlpha(0.9f * fa));
            float cr = 34f + n * 5f, ang = dir.Angle;
            for (int k = 9; k >= 0; k--)
            {
                V2 c = b - dir * (18f + 12f * k);
                Arc(c, cr, 3f * (1f - k / 12f) + 1f, col.WithAlpha((1f - k / 10f) * 0.5f * fa), 12, ang - 0.95f, 1.9f);
            }
            Arc(b - dir * 18f, cr, 7f + n * 0.6f, col.WithAlpha(0.8f * fa), 14, ang - 0.95f, 1.9f);
            Arc(b - dir * 18f, cr, 2.4f, Rgba.White.WithAlpha(fa), 14, ang - 0.95f, 1.9f);
            GlowDisc(b, 24f + n * 3f, col, 0.5f * fa);
        }
    }
}
