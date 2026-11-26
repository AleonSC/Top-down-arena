using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Railgun's charged ricochet beam and Heat Dump (Core/RailBeam.cs; Docs/Mockups/railgun-rework.html): each stretch
    /// of the beam (FxKind.RailBeam) flashes white-hot in the shot's colour (cyan, gold for a perfect shot) with arcs
    /// crackling down its sides once charged, then the charged path glows and sparks for as long as it hurts; the Heat
    /// Dump (FxKind.HeatDump) is a cone of fire that rolls out to its reach, licking flames and embers. Also your own aim
    /// preview while charging (the beam's width and bounces) and the heat gauge behind a Railgun's holder. The gun itself
    /// (coils lighting with the charge, rails glowing with the heat) is DpsGunLooks.cs.
    public partial class ArenaApp
    {
        static readonly Rgba RailHot = new Rgba(1f, 0.48f, 0.18f), RailWhiteHot = new Rgba(1f, 0.93f, 0.75f), RailSteel = new Rgba(0.36f, 0.49f, 0.75f),
            DumpCore = new Rgba(1f, 0.95f, 0.75f), DumpMid = new Rgba(1f, 0.62f, 0.2f), DumpEdge = new Rgba(0.88f, 0.25f, 0.1f);
        readonly List<V2> railPts = new List<V2>();


        /// Steel, then orange, then white-hot, as the heat (0-1) climbs.
        static Rgba RailHeatCol(float h) => h < 0.6f ? RailSteel.Mix(RailHot, h / 0.6f) : RailHot.Mix(RailWhiteHot, (h - 0.6f) / 0.4f);

        /// A soft band along a-b, `half` wide either side: `mid` down the middle, clear at the edges.
        void RailBand(V2 a, V2 b, V2 side, float half, Rgba mid)
        {
            var edge = mid.WithAlpha(0f);
            VTri(a + side * half, edge, b + side * half, edge, b, mid);
            VTri(a + side * half, edge, b, mid, a, mid);
            VTri(a - side * half, edge, b - side * half, edge, b, mid);
            VTri(a - side * half, edge, b, mid, a, mid);
        }

        /// FxKind.RailBeam: one stretch of the beam, its flash and then its charged path.
        void DrawRailBeamFx(Effect fx)
        {
            V2 a = fx.Pos, b = fx.Pos2, d = b - a;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 dir = d / len, side = dir.Orthogonal();
            float age = fx.T, w = fx.Radius, c = Mathf.Clamp01((fx.Color.A - 0.5f) * 2f), flash = Game.RailBeamFx;
            var col = fx.Color.WithAlpha(1f);
            float seed = a.X * 0.37f + a.Y * 0.11f;
            if (fx.Dur > flash + 0.01f)  // the charged path, for as long as it hurts
            {
                float fade = Mathf.Clamp01((fx.Dur - age) / 0.4f) * Mathf.Clamp01(age / 0.06f), bw = w * 0.75f + 4f;
                RailBand(a, b, side, bw, col.WithAlpha(0.24f * fade));
                Line(a, b, 1.4f, col.WithAlpha(0.5f * fade));
                int n = Math.Max(2, (int)(len / 36f));
                for (int i = 0; i < n; i++)  // sparks jittering along it
                {
                    float along = len * ((i + 0.5f) / n) + Mathf.Sin(age * 9f + i * 1.7f) * 10f;
                    float j = Mathf.Sin(age * 47f + i * 2.3f + seed) * bw * 0.8f;
                    V2 at = a + dir * Mathf.Clamp(along, 0f, len);
                    Line(at + side * j, at - side * (j * 0.3f) + dir * 8f, 1.2f, Rgba.White.WithAlpha(0.55f * fade));
                }
            }
            if (age >= flash)
                return;
            float q = 1f - age / flash, e = q * q, ww = w * (0.6f + 0.4f * q);
            Line(a, b, ww * 2.6f + 6f, col.WithAlpha(0.28f * e));  // the glow
            Line(a, b, ww, col.WithAlpha(0.9f * e));                // the beam
            Line(a, b, Mathf.Max(1.2f, ww * 0.35f), Rgba.White.WithAlpha(e));  // its white-hot core
            if (c > 0.4f)  // arcs crackling down its sides
            {
                int arcs = 2 + (int)(c * 3f), steps = Math.Max(4, (int)(len / 40f));
                for (int k = 0; k < arcs; k++)
                {
                    float off = (k % 2 == 0 ? 1f : -1f) * ww * 0.7f, amp = ww * 0.4f + 2f;
                    V2 prev = a + side * off;
                    for (int i = 1; i <= steps; i++)
                    {
                        float u = (float)i / steps, jit = i == steps ? 0f : (Hash01(seed + k * 31f + i * 7.3f + Mathf.Floor(age * 30f)) * 2f - 1f) * amp;
                        V2 next = a + dir * (len * u) + side * (off + jit);
                        Line(prev, next, 1.3f, col.Mix(Rgba.White, 0.4f).WithAlpha(0.8f * e));
                        prev = next;
                    }
                }
            }
            Circle(b, ww * 1.1f + 7f, col.WithAlpha(0.35f * e), 12);  // where it struck
            Circle(b, ww * 0.5f + 3f, Rgba.White.WithAlpha(0.8f * e), 10);
        }

        /// FxKind.HeatDump: the cone of fire rolling out to its reach.
        void DrawHeatDumpFx(Effect fx)
        {
            V2 o = fx.Pos, to = fx.Pos2 - o;
            float reach = to.Length;
            if (reach < 1f)
                return;
            float aim = to.Angle, half = Game.DumpHalf, h = Mathf.Clamp01(fx.Radius / Game.RailHeatMax), age = fx.T, f = Mathf.Clamp01(age / fx.Dur);
            float grow = Mathf.Clamp01(age / 0.3f), r = reach * (0.3f + 0.7f * Mathf.Sqrt(grow)), fade = 1f - Mathf.Clamp01((f - 0.3f) / 0.7f);
            const int segs = 14;
            for (int k = 0; k < segs; k++)  // the cone: white-yellow at the gun, orange, then clear at its edge
            {
                V2 d0 = V2.FromAngle(aim - half + 2f * half * k / segs), d1 = V2.FromAngle(aim - half + 2f * half * (k + 1) / segs);
                Rgba core = DumpCore.WithAlpha(0.6f * fade), mid = DumpMid.WithAlpha(0.4f * fade), edge = DumpEdge.WithAlpha(0f);
                VTri(o, core, o + d0 * (r * 0.55f), mid, o + d1 * (r * 0.55f), mid);
                VTri(o + d0 * (r * 0.55f), mid, o + d0 * r, edge, o + d1 * r, edge);
                VTri(o + d0 * (r * 0.55f), mid, o + d1 * r, edge, o + d1 * (r * 0.55f), mid);
            }
            Arc(o, r, 2f + 5f * fade, DumpMid.WithAlpha(0.9f * fade), segs, aim - half, 2f * half);  // the front of the blast
            Arc(o, r * 0.78f, 1.5f, DumpCore.WithAlpha(0.5f * fade), segs, aim - half * 0.9f, 1.8f * half);
            int tongues = 14 + (int)(26f * h);
            float seed = o.X * 0.13f + o.Y * 0.07f;
            for (int i = 0; i < tongues; i++)  // flames licking out through it
            {
                float ha = Hash01(seed + i * 3.1f), hs = Hash01(seed + i * 5.7f + 1f), hc = Hash01(seed + i * 9.2f + 2f);
                float an = aim + half * 0.95f * (ha * 2f - 1f), dist = reach * (0.1f + 0.9f * Mathf.Sqrt(grow) * (0.35f + 0.65f * hs));
                float size = (3f + 6f * h) * (0.6f + 0.6f * hc) * (1f - 0.6f * f);
                Rgba col = hc < 0.33f ? DumpCore : hc < 0.66f ? DumpMid : DumpEdge;
                V2 at = o + V2.FromAngle(an) * dist;
                Circle(at, size, col.WithAlpha(0.75f * fade), 8);
                Line(at, at - V2.FromAngle(an) * (size * 2.5f), size * 0.8f, col.WithAlpha(0.35f * fade));  // its tail
            }
            if (age < 0.12f)  // the muzzle burst
                Circle(o + V2.FromAngle(aim) * 30f, 14f + 10f * h, DumpCore.WithAlpha(0.8f * (1f - age / 0.12f)), 12);
        }

        /// The heat gauge: a curved strip behind the holder, steel to orange to white-hot as it fills, a notch where a dump
        /// becomes possible, pulsing when full.
        void DrawRailHeat(Player p)
        {
            if (!fxOn || p.Downed || p.RailHeat == 0)
                return;
            float fill = Mathf.Clamp01(p.RailHeat / Game.RailHeatMax), sweep = 1.9f, r = p.Def.BodyR + 13f;
            float start = p.Aim + MathX.Pi - sweep / 2f;
            bool ready = p.RailHeat >= Game.DumpMin, full = p.RailHeat >= Game.RailHeatMax;
            Arc(p.Disp, r, 5f, RailHot.WithAlpha(0.14f), 20, start, sweep);
            var col = RailHeatCol(fill);
            if (full)
                Arc(p.Disp, r, 9f, RailHot.WithAlpha(0.2f + 0.12f * Mathf.Sin(Time.unscaledTime * 9f)), 20, start, sweep);
            Arc(p.Disp, r, 5f, ready ? col : col.WithAlpha(0.45f), 20, start, sweep * fill);
            float notch = start + sweep * Game.DumpMin / Game.RailHeatMax;
            Line(p.Disp + V2.FromAngle(notch) * (r - 4f), p.Disp + V2.FromAngle(notch) * (r + 4f), 1.4f, RailWhiteHot.WithAlpha(0.6f));
        }

        /// Your own aim while charging: the beam's width along its line and each bounce off the walls, gold in the perfect window.
        void DrawRailAim(Player p)
        {
            if (!fxOn || p != game.Me || p.Downed || p.DrawT <= 0f)
                return;
            float c = game.BowCharge(p, p.DrawT);
            bool perfect = game.BowPerfect(p, p.DrawT);
            V2 dir = V2.FromAngle(p.Aim);
            game.RailLine(p.Disp + dir * (p.Def.BodyR + 14f), dir, Game.RailBounces(c, game.ExtraShots(p)), railPts);
            float half = Game.RailWidth(c) * p.Stats.ShotSizeMult * 0.5f;
            var col = perfect ? Rgba.Gold : new Rgba(0.55f, 0.91f, 1f);
            for (int i = 0; i + 1 < railPts.Count; i++)
            {
                V2 a = railPts[i], b = railPts[i + 1], d = b - a;
                if (d.LengthSq < 1f)
                    continue;
                V2 side = d.Normalized().Orthogonal();
                float al = (perfect ? 0.2f : 0.08f + 0.1f * c) * (i == 0 ? 1f : 0.6f);
                SetColor(col.WithAlpha(al));
                Tri(a + side * half, b + side * half, b - side * half);
                Tri(a + side * half, b - side * half, a - side * half);
                Line(a + side * half, b + side * half, 1f, col.WithAlpha(al * 2.5f));
                Line(a - side * half, b - side * half, 1f, col.WithAlpha(al * 2.5f));
                if (i > 0)
                    Circle(a, 3f, col.WithAlpha(0.5f), 8);  // a bounce
            }
        }

        /// A Railgun beam just went off at its holder's muzzle (Juice's new-effects pass): the gun's flash, kick and steam.
        void RailBeamFeel(Effect fx, Player me)
        {
            if (fx.Kind != FxKind.RailBeam || fx.Radius <= 0f)
                return;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon.Id != WeaponId.Railgun || fx.Pos.DistanceTo(p.Pos) > p.Def.BodyR + 50f)
                    continue;
                OnRailgunShot(p, fx.Pos, (fx.Pos2 - fx.Pos).Normalized(), Mathf.Clamp01((fx.Color.A - 0.5f) * 2f));  // DpsGunLooks.cs
                if (p == me)
                    Shake(0.05f + 0.25f * (fx.Color.A - 0.5f) * 2f);
                return;
            }
        }
    }
}
