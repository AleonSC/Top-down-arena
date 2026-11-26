using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Caltrop Run (the DPS Knight's 3rd choice, Core/CaltropRun.cs), look "Iron Scatter" of the claude.ai artifact "Knight
    /// Caltrop Run". Everything is drawn from replicated state, so it costs nothing extra on the network:
    ///   the caltrop  (each `ZoneKind.Caltrop` zone, under the enemies) a dark-iron four-point star with a steel rim, bouncing
    ///                out of the knight's heels from Game.CaltropFrom to Pos with a spin and its shadow on the floor, then lying
    ///                with a steel glint that pulses; once it has caught an enemy (GuardFrac below 1) one point is bent and
    ///                tipped red; it fades over its last 0.6 s;
    ///   the run      (Player.CaltropT) gold speed lines streaming off your heels and a little leather pouch at your hip;
    ///   the effects  FxKind.CaltropRun a ring of dust at your feet as you set off; FxKind.CaltropHit the spike popping up
    ///                white with red sparks and a drop of blood; FxKind.CaltropBreak the caltrop snapping into iron bits.
    /// Visual effects off: the plain star and its rim, no glint, shadow or speed lines.
    public partial class ArenaApp
    {
        static readonly Rgba CaltropIron = new Rgba(0.29f, 0.36f, 0.47f), CaltropRim = new Rgba(0.62f, 0.85f, 1f),
            CaltropGlint = new Rgba(0.87f, 0.95f, 1f), CaltropBlood = new Rgba(0.85f, 0.2f, 0.24f), CaltropRun = new Rgba(1f, 0.8f, 0.35f),
            CaltropPouch = new Rgba(0.42f, 0.32f, 0.2f), CaltropDust = new Rgba(0.6f, 0.55f, 0.48f);

        /// Under the enemies (WorldEnemies.cs): one caltrop.
        void DrawCaltrop(Zone z, float t)
        {
            float age = Game.CaltropAge(z), land = Mathf.Clamp01(age / Game.CaltropLand);
            float fade = Mathf.Clamp01(z.T / 0.6f) * Mathf.Clamp01(age / 0.05f);
            if (fade <= 0f)
                return;
            V2 from = Game.CaltropFrom(z);
            float hop = Mathf.Sin(land * MathX.Pi) * 10f;
            V2 at = V2.Lerp(from, z.Pos, land) - new V2(0f, hop);
            float spin = z.Id * 1.37f + (1f - land) * 8f;
            bool bent = z.GuardFrac < 0.99f;
            if (fxOn)
            {
                V2 shadow = V2.Lerp(from, z.Pos, land) + new V2(0f, 3f);
                Circle(shadow, 6f * (1f - 0.3f * hop / 10f), new Rgba(0f, 0f, 0f, 0.4f * fade), 10);
            }
            CaltropStar(at, 9f, spin, CaltropIron.WithAlpha(0.95f * fade), CaltropRim.WithAlpha(fade), bent);
            if (bent)
            {
                V2 tip = at + V2.FromAngle(spin) * 9f;
                Circle(tip, 1.8f, CaltropBlood.WithAlpha(fade), 6);
            }
            if (fxOn && land >= 1f)
            {
                float g = 0.5f + 0.5f * Mathf.Sin(t * 6f + z.Id);
                Circle(at, 1.2f + g * 0.9f, CaltropGlint.WithAlpha((0.55f + 0.45f * g) * fade), 8);
            }
        }

        /// A four-point caltrop seen from above: thin spikes from a small hub; `bent` turns the first spike's tip aside.
        void CaltropStar(V2 c, float r, float rot, Rgba fill, Rgba rim, bool bent)
        {
            const int pts = 4;
            const float inner = 0.28f;
            var p = new V2[pts * 2];
            for (int i = 0; i < pts * 2; i++)
            {
                float rr = i % 2 == 1 ? r * inner : r, a = rot + i * MathX.Pi / pts;
                if (bent && i == 0)
                {
                    rr *= 0.75f;
                    a += 0.45f;
                }
                p[i] = c + V2.FromAngle(a) * rr;
            }
            SetColor(fill);
            for (int i = 0; i < p.Length; i++)
                Tri(c, p[i], p[(i + 1) % p.Length]);
            for (int i = 0; i < p.Length; i++)
                Line(p[i], p[(i + 1) % p.Length], 1.2f, rim);
        }

        // Each runner's last drawn place and smoothed heading (the way they run isn't replicated, so it's read off Disp).
        readonly Dictionary<int, V2> caltropLastAt = new Dictionary<int, V2>(), caltropHeading = new Dictionary<int, V2>();

        /// Under the enemies (KnightDpsLook.cs DrawKnightDpsUnder): a running knight's speed lines and pouch.
        void DrawCaltropRunners(float t)
        {
            foreach (var p in game.Players.Values)
            {
                if (p.CaltropT <= 0f || p.Downed)
                {
                    caltropLastAt.Remove(p.Id);
                    caltropHeading.Remove(p.Id);
                    continue;
                }
                V2 at = p.Disp, last, head;
                if (!caltropLastAt.TryGetValue(p.Id, out last))
                    last = at;
                if (!caltropHeading.TryGetValue(p.Id, out head))
                    head = V2.Zero;
                V2 moved = at - last;
                if (moved.Length > 0.2f)
                    head = V2.Lerp(head, moved.Normalized(), 0.25f);
                else
                    head = head * 0.9f;
                caltropLastAt[p.Id] = at;
                caltropHeading[p.Id] = head;
                float k = Mathf.Clamp01(p.CaltropT / 0.3f) * Mathf.Clamp01((Game.CaltropRunTime - p.CaltropT) / 0.15f + 0.4f);
                float sp = head.Length;
                V2 back = sp > 0.3f ? -head / sp : -V2.FromAngle(p.Aim), side = new V2(-back.Y, back.X);
                if (fxOn && sp > 0.3f)
                    for (int i = -1; i <= 1; i++)
                    {
                        float wob = Mathf.Repeat(t * 3.1f + i * 0.37f, 1f);
                        V2 s0 = at + back * (p.Def.BodyR + 4f) + side * (i * 6f);
                        Line(s0, s0 + back * (10f + 8f * wob), 1.5f, CaltropRun.WithAlpha(0.5f * k * (1f - 0.5f * wob)));
                    }
                V2 hip = at + back * 4f + side * (p.Def.BodyR * 0.7f);
                Circle(hip, 3.6f, CaltropPouch.WithAlpha(0.95f * k), 10);
                Circle(hip - new V2(0f, 1f), 1.3f, CaltropRim.WithAlpha(0.8f * k), 6);
            }
        }

        /// FxKind.CaltropRun: a ring of dust at your feet as you set off.
        void DrawCaltropRunFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / fx.Dur);
            Arc(fx.Pos, 10f + fx.Radius * KEo(f), 2f, CaltropRun.WithAlpha(0.6f * (1f - f)), 24);
            if (fxOn)
                KPuffs(fx.Pos, fx.T, 6, fx.Pos.X * 0.13f, 0f, MathX.Tau, 60f, fx.Dur, CaltropDust, 5f, 0.35f);
        }

        /// FxKind.CaltropHit: the spike popping up white, red sparks and a drop of blood toward the enemy.
        void DrawCaltropHitFx(Effect fx)
        {
            float a = fx.T, f = Mathf.Clamp01(a / fx.Dur), pop = 1f + 0.6f * (1f - Mathf.Clamp01(a / 0.12f));
            V2 to = fx.Pos2 - fx.Pos;
            float ang = to.LengthSq > 1f ? to.Angle : 0f;
            CaltropStar(fx.Pos, 9f * pop, ang, CaltropGlint.WithAlpha(0.8f * (1f - f)), CaltropBlood.WithAlpha(1f - f), false);
            if (fxOn)
            {
                GlowDisc(fx.Pos, 16f * (1f - f), CaltropBlood, 0.6f * (1f - f));
                KSparks(fx.Pos, a, 7, fx.Pos.X * 0.31f + fx.Pos.Y, ang, 1.6f, 180f, fx.Dur, CaltropBlood);
            }
        }

        /// FxKind.CaltropBreak: the caltrop snapping into four iron bits that skid away.
        void DrawCaltropBreakFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / fx.Dur), d = 4f + 14f * KEo(f);
            for (int i = 0; i < 4; i++)
            {
                V2 dir = V2.FromAngle(fx.Pos.X * 0.21f + i * MathX.Pi / 2f);
                Line(fx.Pos + dir * d, fx.Pos + dir * (d + 5f), 2f, CaltropRim.WithAlpha(1f - f));
            }
        }
    }
}
