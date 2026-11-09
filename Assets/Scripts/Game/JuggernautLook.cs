using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Juggernaut Suit (Core/Juggernaut.cs), from the second Tank > Soldier card of Docs/Mockups/ultimate-ideas.html: six grey steel
    /// plates swing in and bolt onto the body one after another over Game.JuggClamp s (a spark as each one lands), the body swells to
    /// 1.3x, a segmented steel armour bar sits over the health bar and drains, and every flak shard is a short amber tracer flying back
    /// at its shooter (FxKind.JuggShard). As the armour runs down the plates come off one by one (a plate per sixth of the armour);
    /// when the suit ends the rest fall away and the body shrinks back.
    /// Replicated state is Player.JuggT, JuggArmor and JuggMax; the clamp, the falling plates and the swell are local (found by watching
    /// JuggT change). With Visual effects off it is a plain grey ring round the body, the bar and a thin tracer line.
    public partial class ArenaApp
    {
        class JuggVis
        {
            public bool Up;
            public float OnAt = -9f, OffAt = -9f;
            public int Plates;                          // plates still on
            public readonly float[] LostAt = new float[JuggPlates];  // when each plate came off (-9: still on, or never)
            public float Ghost = 1f;                    // the bar's drain ghost
        }

        readonly Dictionary<int, JuggVis> juggVis = new Dictionary<int, JuggVis>();
        const int JuggPlates = 6;
        const float JuggSwellBy = 0.3f, JuggShrink = 0.3f, JuggFall = 0.55f;
        static readonly Rgba JuggPlate = new Rgba(0.48f, 0.52f, 0.56f), JuggPlateHi = new Rgba(0.72f, 0.77f, 0.82f),
            JuggEdge = new Rgba(0.18f, 0.2f, 0.23f), JuggSpark = new Rgba(1f, 0.86f, 0.59f), JuggTracer = new Rgba(1f, 0.84f, 0.55f),
            JuggBarFill = new Rgba(0.72f, 0.76f, 0.8f);

        static float JuggEase(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }

        /// Watches JuggT to time the clamp and the fall. Called once per player per frame (from JuggSwell, before the model).
        JuggVis JuggState(Player p, float t)
        {
            bool on = p.JuggT > 0f && !p.Downed;
            if (!juggVis.TryGetValue(p.Id, out var v))
            {
                juggVis[p.Id] = v = new JuggVis { Up = on, OnAt = on ? t - Game.JuggSince(p) : -9f };  // a late joiner sees it already on
                for (int i = 0; i < JuggPlates; i++)
                    v.LostAt[i] = -9f;
                v.Plates = on ? JuggKeep(p) : 0;
                v.Ghost = p.JuggMax > 0f ? Mathf.Clamp01(p.JuggArmor / p.JuggMax) : 0f;
            }
            if (on && !v.Up)
            {
                v.OnAt = t - Mathf.Clamp(Game.JuggSince(p), 0f, 0.2f);
                v.Plates = JuggPlates;
                v.Ghost = 1f;
                for (int i = 0; i < JuggPlates; i++)
                    v.LostAt[i] = -9f;
            }
            else if (!on && v.Up)
            {
                v.OffAt = t;
                for (int i = 0; i < v.Plates; i++)
                    v.LostAt[i] = t;  // the rest fall away together
                v.Plates = 0;
                if (Juicy)
                    Sparks(p.Disp, JuggSpark, 10, 160f, 0.35f, 2f);
            }
            v.Up = on;
            if (on)
            {
                // A plate comes off for every sixth of the armour gone (the last one stays until the suit ends).
                float frac = p.JuggMax > 0f ? Mathf.Clamp01(p.JuggArmor / p.JuggMax) : 0f;
                int keep = JuggKeep(p);
                while (v.Plates > keep)
                {
                    v.Plates--;
                    v.LostAt[v.Plates] = t;
                    if (Juicy)
                        Sparks(p.Disp + V2.FromAngle(JuggPlateAngle(p, v.Plates)) * (p.Def.BodyR * 2f), JuggSpark, 6, 120f, 0.3f, 1.8f);
                }
                v.Ghost = Mathf.Max(frac, Mathf.MoveTowards(v.Ghost, frac, Time.deltaTime * 0.8f));
            }
            return v;
        }

        static int JuggKeep(Player p) =>
            Mathf.Clamp(Mathf.CeilToInt((p.JuggMax > 0f ? Mathf.Clamp01(p.JuggArmor / p.JuggMax) : 0f) * JuggPlates - 0.001f), 1, JuggPlates);

        static float JuggPlateAngle(Player p, int k) => p.Aim + k * MathX.Tau / JuggPlates + 0.3f;

        /// How much bigger the body is drawn (1 off, 1.3 on), eased in with the clamp and out when it ends. DrawPlayer scales the model by it.
        float JuggSwell(Player p, float t)
        {
            if (p.JuggT <= 0f && !juggVis.ContainsKey(p.Id))
                return 1f;
            var v = JuggState(p, t);
            if (v.Up)
                return 1f + JuggSwellBy * JuggEase((t - v.OnAt) / Game.JuggClamp);
            float k = (t - v.OffAt) / JuggShrink;
            return k >= 1f ? 1f : 1f + JuggSwellBy * (1f - JuggEase(k));
        }

        /// The plates over the model (r is the swollen model radius): bolting on, on, or falling away.
        void DrawJuggernaut(Player p, float r, float t, float alpha)
        {
            if (!juggVis.TryGetValue(p.Id, out var v))
                return;
            bool anyFalling = false;
            for (int i = 0; i < JuggPlates; i++)
                anyFalling |= v.LostAt[i] > -9f && t - v.LostAt[i] < JuggFall;
            if (!v.Up && !anyFalling)
                return;
            float s = r / 10f;  // the mockup's plates are drawn for a body of radius 10
            if (!fxOn)
            {
                if (v.Up)
                    Arc(p.Disp, r + 5f, 3.5f, new Rgba(0.62f, 0.66f, 0.7f, 0.8f * alpha), 28);
                return;
            }
            float since = t - v.OnAt;
            for (int k = 0; k < JuggPlates; k++)
            {
                float a = JuggPlateAngle(p, k);
                float land = Mathf.Clamp01((since - k * 0.08f) / 0.2f);  // each swings in 0.08 s after the one before
                float fall = v.LostAt[k] > -9f ? (t - v.LostAt[k]) / JuggFall : -1f;
                if (fall >= 1f || (fall < 0f && (!v.Up || land <= 0f || k >= v.Plates)))
                    continue;
                float fade = alpha * (fall >= 0f ? 1f - fall * fall : land);
                float pr = Mathf.Lerp(40f, 16f, JuggEase(land)) * s;  // swings in from 2.5x out
                V2 at = p.Disp;
                float spin = 0f;
                if (fall >= 0f)  // knocked loose: thrown out and tumbling, dropping a little
                {
                    pr = 16f * s + 22f * fall;
                    spin = (k % 2 == 0 ? 1f : -1f) * 2.4f * fall;
                    at += new V2(0f, 14f * fall * fall);
                }
                // the plate: a trapezoid, wider on the inside, with a lit outer edge and two rivets
                V2 rad = V2.FromAngle(a + spin * 0.3f), tan = rad.Orthogonal();
                float hitGlow = JuggHitGlow(p, a, t);
                Rgba lo = JuggPlate.Mix(Rgba.White, 0.5f * hitGlow).WithAlpha(fade), hi = JuggPlateHi.Mix(Rgba.White, 0.6f * hitGlow).WithAlpha(fade);
                float sc = s / 1.3f;
                V2 c = at + rad * pr;
                V2 i0 = c + rad * (-3f * sc) + tan * (-7f * sc), i1 = c + rad * (-3f * sc) + tan * (7f * sc);
                V2 o0 = c + rad * (4f * sc) + tan * (-5f * sc), o1 = c + rad * (4f * sc) + tan * (5f * sc);
                if (spin != 0f)
                {
                    i0 = c + (i0 - c).Rotated(spin); i1 = c + (i1 - c).Rotated(spin);
                    o0 = c + (o0 - c).Rotated(spin); o1 = c + (o1 - c).Rotated(spin);
                }
                Quad(i0, i1, o1, o0, lo);
                Line(o0, o1, 1.6f * sc, hi);  // the lit outer edge
                Line(i0, o0, 1.2f, JuggEdge.WithAlpha(fade));
                Line(i1, o1, 1.2f, JuggEdge.WithAlpha(fade));
                Line(i0, i1, 1.2f, JuggEdge.WithAlpha(fade));
                Circle(Vector2Lerp(i0, o0, 0.5f) + (o1 - o0) * 0.18f, 0.9f * sc, JuggEdge.WithAlpha(fade), 6);
                Circle(Vector2Lerp(i1, o1, 0.5f) - (o1 - o0) * 0.18f, 0.9f * sc, JuggEdge.WithAlpha(fade), 6);
                // the spark as it bolts on
                if (fall < 0f && land > 0f && land < 1f)
                {
                    GlowDisc(c, 9f * sc * (1f - land) + 2f, JuggSpark, 0.9f * (1f - land) * alpha);
                    Circle(c, 5f * sc * (1f - land), JuggSpark.WithAlpha(0.9f * alpha), 8);
                }
                else if (fall < 0f && land >= 1f && since - k * 0.08f - 0.2f < 0.05f && Juicy)
                    Sparks(c, JuggSpark, 4, 90f, 0.25f, 1.6f);  // the bolt driving home
            }
            // steel straps between neighbouring plates while it's fully on
            if (v.Up && since > Game.JuggClamp)
                Arc(p.Disp, 16f * s - 3f * s / 1.3f, 1.4f, JuggEdge.WithAlpha(0.7f * alpha), 28);
        }

        /// A plate glows white for a moment where a shot just struck the armour (the shard's FxKind.JuggShard starts there).
        float JuggHitGlow(Player p, float plateAngle, float t)
        {
            float best = 0f;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.JuggShard || (int)fx.Radius != p.Id || fx.T > 0.15f)
                    continue;
                V2 off = fx.Pos - p.Pos;
                float d = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(off.Y, off.X) - plateAngle + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (d < MathX.Tau / JuggPlates * 0.6f)
                    best = Mathf.Max(best, 1f - fx.T / 0.15f);
            }
            return best;
        }

        /// The armour bar over the health bar: six segments (one per plate), steel fill, a pale ghost where it just drained.
        void DrawJuggBar(Player p, float r)
        {
            if (p.JuggT <= 0f || p.Downed || p.JuggMax <= 0f)
                return;
            float frac = Mathf.Clamp01(p.JuggArmor / p.JuggMax);
            float ghost = juggVis.TryGetValue(p.Id, out var v) ? Mathf.Max(frac, v.Ghost) : frac;
            float w = 40f, h = 4f;
            V2 at = p.Disp + new V2(-w / 2f, -r - 19f);
            FillRect(at.X - 1f, at.Y - 1f, w + 2f, h + 2f, new Rgba(0f, 0f, 0f, 0.65f));
            if (ghost > frac + 1e-3f)
                FillRect(at.X, at.Y, w * ghost, h, new Rgba(1f, 0.9f, 0.7f, 0.55f));
            FillRect(at.X, at.Y, w * frac, h, JuggBarFill);
            FillRect(at.X, at.Y, w * frac, 1f, new Rgba(1f, 1f, 1f, 0.5f));  // lit top
            for (int i = 1; i < JuggPlates; i++)  // the segment notches
                FillRect(at.X + w * i / JuggPlates - 0.5f, at.Y, 1f, h, new Rgba(0f, 0f, 0f, 0.55f));
            float left = Mathf.Clamp01(p.JuggT / Game.JuggTime);  // the time left, a hairline under it
            FillRect(at.X, at.Y + h + 1f, w * left, 1f, new Rgba(1f, 0.84f, 0.35f, 0.7f));
        }

        /// A flak shard (FxKind.JuggShard; Pos = where the shot struck, Pos2 = the shooter): an amber tracer racing back, a pop on arrival.
        void DrawJuggShardFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            V2 d = fx.Pos2 - fx.Pos;
            float fly = Mathf.Clamp01(f / 0.8f);
            V2 head = fx.Pos + d * JuggEase(fly);
            if (!fxOn)
            {
                if (fly < 1f)
                    Line(head - d.Normalized() * 10f, head, 1.5f, JuggTracer.WithAlpha(0.8f));
                return;
            }
            if (fly < 1f)
            {
                float tail = Mathf.Max(12f, d.Length * 0.08f);
                V2 tailAt = head - d.Normalized() * tail;
                Line(tailAt, head, 3.2f, JuggTracer.WithAlpha(0.35f));
                Line(tailAt, head, 1.8f, JuggTracer.WithAlpha(0.95f));
                Circle(head, 1.6f, Rgba.White, 6);
            }
            else
            {
                float k = (f - 0.8f) / 0.2f;
                GlowDisc(fx.Pos2, 10f + 8f * k, JuggTracer, 0.8f * (1f - k));
            }
            if (fx.T < 0.06f)
                GlowDisc(fx.Pos, 8f, JuggSpark, 0.7f * (1f - fx.T / 0.06f));  // the strike on the plate
        }
    }
}
