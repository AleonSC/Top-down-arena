using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Last Stand as option B "Aegis" (Docs/Mockups/last-stand-aegis.html): the knight plants three stakes behind them and nine
    /// steel plates slam down in a crescent, centre first, 120 px out. Each plate kicks back and flashes where a shot lands,
    /// and cracks, glows and rattles as the knight's health runs down (the barrier's only wear is the knight's own health).
    /// Replicated state is Player.StandOn and Player.StandAim; the slam, the collapse and the kick are local (found by watching
    /// StandOn change, and by the FxKind.StandHit effects the host sends for every stopped shot).
    public partial class ArenaApp
    {
        class StandVis
        {
            public bool Up;
            public float PlantAt = -9f, EndAt = -9f, Aim;
        }

        readonly Dictionary<int, StandVis> standVis = new Dictionary<int, StandVis>();
        readonly float[] standHeat = new float[Game.StandPlates];
        const float StandFade = 0.4f;
        static readonly Rgba StandRim = new Rgba(0.55f, 0.78f, 1f);

        static float StandPlateAngle(int i) => -Game.StandHalfArc + (i + 0.5f) * (2f * Game.StandHalfArc / Game.StandPlates);

        void DrawLastStand(Player p, float r, float t)
        {
            if (!standVis.TryGetValue(p.Id, out var v))
                standVis[p.Id] = v = new StandVis { Up = p.StandOn, Aim = p.StandAim };  // first seen already planted (a late joiner): it stands, no slam
            if (!p.StandOn && !v.Up && t - v.EndAt > StandFade)
                return;
            float R = Game.StandRadius;
            if (p.StandOn && !v.Up)  // just planted
            {
                v.PlantAt = t;
                if (Juicy)
                    Ring(p.Disp, 26f, 0.4f, StandRim);
            }
            else if (!p.StandOn && v.Up)  // just came down
            {
                v.EndAt = t;
                if (Juicy)
                    Ring(p.Disp, R * 0.8f, 0.35f, StandRim);
            }
            v.Up = p.StandOn;
            if (p.StandOn)
                v.Aim = p.StandAim;
            float down = t - v.EndAt;
            if (!p.StandOn && down > StandFade)
                return;
            float fade = p.StandOn ? 1f : 1f - down / StandFade;
            float aim = v.Aim, half = Game.StandHalfArc;
            float wear = p.MaxHp > 0f ? 1f - Mathf.Clamp01(p.Hp / p.MaxHp) : 0f;
            if (!fxOn)
            {
                Arc(p.Disp, R, 5f, new Rgba(0.6f, 0.78f, 1f, 0.6f * fade), 24, aim - half, 2f * half);
                return;
            }

            // stakes driven into the floor behind the knight
            float plant = Mathf.Clamp01((t - v.PlantAt) / Game.StandPlant);
            if (p.StandOn || fade > 0f)
                for (int k = -1; k <= 1; k++)
                {
                    float a = aim + MathX.Pi + k * 0.9f;
                    V2 d = V2.FromAngle(a), side = d.Orthogonal();
                    float r0 = r + 8f + 14f * (1f - EaseOut3(plant)), r1 = r0 + 18f * EaseOut3(plant);
                    V2 b0 = p.Disp + d * r0;
                    Quad(b0 - side * 4f, b0 + side * 4f, p.Disp + d * r1 + side * 0.5f, p.Disp + d * r1 - side * 0.5f, new Rgba(0.1f, 0.14f, 0.19f, fade));
                    Line(b0 - side * 4f, p.Disp + d * r1, 1.2f, StandRim.WithAlpha(0.5f * fade));
                    Line(b0 + side * 4f, p.Disp + d * r1, 1.2f, StandRim.WithAlpha(0.5f * fade));
                }

            // where each plate has just been hit
            for (int i = 0; i < standHeat.Length; i++)
                standHeat[i] = 0f;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.StandHit && fx.Pos2.DistanceTo(p.Pos) < 40f)
                {
                    V2 off = fx.Pos - p.Pos;
                    float rel = Mathf.Repeat(Mathf.Atan2(off.Y, off.X) - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                    int i = Mathf.Clamp(Mathf.FloorToInt((rel + half) / (2f * half / Game.StandPlates)), 0, Game.StandPlates - 1);
                    float k = Mathf.Exp(-fx.T * 9f) * (fx.Radius >= 30f ? 1.4f : 0.8f);
                    standHeat[i] = Mathf.Max(standHeat[i], k);
                    if (i > 0) standHeat[i - 1] = Mathf.Max(standHeat[i - 1], 0.3f * k);
                    if (i < Game.StandPlates - 1) standHeat[i + 1] = Mathf.Max(standHeat[i + 1], 0.3f * k);
                }

            // the sheltered pocket and the plates' rail
            Arc(p.Disp, R, 17f, new Rgba(0.55f, 0.78f, 1f, 0.12f * fade), 24, aim - half, 2f * half);
            Arc(p.Disp, R * 0.6f, 1f, new Rgba(0.55f, 0.78f, 1f, 0.16f * fade), 20, aim - half, 2f * half);
            Line(p.Disp + V2.FromAngle(aim) * (r + 2f), p.Disp + V2.FromAngle(aim) * (R - 10f), 2f, StandRim.WithAlpha(0.3f * fade * plant));

            for (int i = 0; i < Game.StandPlates; i++)
            {
                float mid = Mathf.Abs(i - (Game.StandPlates - 1) * 0.5f) / ((Game.StandPlates - 1) * 0.5f);  // 0 centre .. 1 edge
                float land = Mathf.Clamp01((plant - mid * 0.4f) / 0.6f);
                if (land <= 0f)
                    continue;
                float a = aim + StandPlateAngle(i);
                float heat = standHeat[i];
                float slam = (1f - PlateSlam(land)) * 70f;
                if (!p.StandOn)  // coming down: they lift away
                    slam = -50f * (1f - fade) * (1f - fade);
                float jit = 0f, jr = 0f;
                if (wear > 0.65f && p.StandOn)  // nearly out of health: they rattle
                {
                    float step = (float)System.Math.Floor(t * 18f);
                    jit = (Hash01(i + step) - 0.5f) * (wear - 0.65f) * 14f;
                    jr = (Hash01(i * 3f + step) - 0.5f) * (wear - 0.65f) * 0.5f;
                }
                V2 radial = V2.FromAngle(a), tang = radial.Orthogonal();
                V2 c = p.Disp + radial * (R - 5f * heat + slam + jit);
                float ca = Mathf.Cos(jr), sa = Mathf.Sin(jr);
                float flash = Mathf.Clamp01(heat);
                float pw = 2f * half / Game.StandPlates;
                float wOut = R * pw * 0.5f - 0.8f, wIn = wOut * 0.86f;
                Rgba lo = MixWhite(WallLo, flash).WithAlpha(fade), hi = MixWhite(WallHi, flash).WithAlpha(fade);
                Quad(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, -8f, wIn), PlatePt(c, radial, tang, ca, sa, 0f, wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 0f, -wIn * 1.04f), lo);
                Quad(PlatePt(c, radial, tang, ca, sa, 0f, -wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 0f, wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 8f, wOut), PlatePt(c, radial, tang, ca, sa, 8f, -wOut), lo.Mix(hi, 0.7f));
                Rgba rim = (wear > 0.5f ? new Rgba(1f, Mathf.Lerp(0.92f, 0.55f, (wear - 0.5f) * 2f), Mathf.Lerp(0.98f, 0.3f, (wear - 0.5f) * 2f)) : new Rgba(0.82f, 0.92f, 1f)).Mix(Rgba.White, flash).WithAlpha(fade);
                Line(PlatePt(c, radial, tang, ca, sa, 8f, -wOut), PlatePt(c, radial, tang, ca, sa, 8f, wOut), 1.6f + flash, rim);
                Line(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, -8f, wIn), 1.2f, rim.WithAlpha(0.7f * fade));
                Line(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, 8f, -wOut), 1.2f, rim.WithAlpha(0.8f * fade));
                Line(PlatePt(c, radial, tang, ca, sa, -8f, wIn), PlatePt(c, radial, tang, ca, sa, 8f, wOut), 1.2f, rim.WithAlpha(0.8f * fade));
                Circle(PlatePt(c, radial, tang, ca, sa, -4.5f, -wIn * 0.5f), 1f, new Rgba(0.23f, 0.27f, 0.34f, fade), 5);
                Circle(PlatePt(c, radial, tang, ca, sa, -4.5f, wIn * 0.5f), 1f, new Rgba(0.23f, 0.27f, 0.34f, fade), 5);
                if (i == Game.StandPlates / 2)  // the crest on the centre plate
                {
                    Line(PlatePt(c, radial, tang, ca, sa, -3f, 0f), PlatePt(c, radial, tang, ca, sa, 4f, 0f), 2f, new Rgba(1f, 0.71f, 0.28f, fade));
                    Line(PlatePt(c, radial, tang, ca, sa, 0.5f, -3.5f), PlatePt(c, radial, tang, ca, sa, 0.5f, 3.5f), 2f, new Rgba(1f, 0.71f, 0.28f, fade));
                }
                float thr = 0.14f + 0.08f * (i % 4);
                if (wear > thr)  // cracks, longer as the knight wears down
                {
                    float k = Mathf.Clamp01((wear - thr) / 0.5f);
                    V2 prev = PlatePt(c, radial, tang, ca, sa, 7f, (Hash01(i * 5f) - 0.5f) * 10f);
                    float x = 7f, y = (Hash01(i * 5f) - 0.5f) * 10f;
                    int n = 1 + Mathf.RoundToInt(k * 3f);
                    for (int m = 0; m < n; m++)
                    {
                        x -= 3.2f;
                        y += (Hash01(i * 11f + m) - 0.5f) * 6f;
                        V2 next = PlatePt(c, radial, tang, ca, sa, x, y);
                        Line(prev, next, 2.6f, new Rgba(1f, 0.55f, 0.24f, 0.55f * wear * fade));
                        Line(prev, next, 1f, new Rgba(0.05f, 0.07f, 0.09f, 0.95f * fade));
                        prev = next;
                    }
                }
            }
        }

        /// A shot stopped by the plates (FxKind.StandHit; Pos = where, Pos2 = the knight, Radius = its damage): a bright burst at
        /// the plate and sparks thrown back out toward where it came from.
        void DrawStandHitFx(Effect fx, float f)
        {
            if (!fxOn)
                return;
            bool big = fx.Radius >= 30f;
            V2 at = fx.Pos, off = at - fx.Pos2, dir = off.IsZero ? new V2(1f, 0f) : off.Normalized();
            Arc(at, 3f + (big ? 32f : 16f) * f, (big ? 3f : 1.8f) * (1f - f) + 0.4f, StandRim.WithAlpha(1f - f), 14);
            if (f < 0.3f)
                GlowDisc(at, (big ? 22f : 12f) * (1f - f / 0.3f), new Rgba(0.92f, 0.96f, 1f), 0.9f);
            int n = big ? 9 : 4;
            for (int k = 0; k < n; k++)
            {
                float ang = Mathf.Atan2(dir.Y, dir.X) + (k - (n - 1) * 0.5f) * 0.3f, sp = 50f + 30f * (k % 3) + (big ? 40f : 0f);
                V2 d = V2.FromAngle(ang);
                Line(at + d * sp * f, at + d * (sp * f + 6f * (1f - f)), 1.6f, new Rgba(1f, 0.9f, 0.7f, 1f - f));
            }
        }
    }
}
