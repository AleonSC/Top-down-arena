using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Kinetic Wall (Core/KineticWall.cs), look B "Plasma membrane" of Docs/Mockups/kinetic-wall-fx.html on the lens of
    /// Docs/Mockups/kinetic-wall.html. While the lens is up:
    ///   boot      it draws out from its middle in KwBoot s, a bright head racing to each end;
    ///   membrane  seven wobbling skins of thin arcs (two bright outer rims), light streaks flowing along it and sparks running the rims;
    ///   ripples   each catch (FxKind.KwCatch, from its age) sends a bulge both ways along the field and a ring at the hit point;
    ///   held      caught shots jitter in place, each with a flickering arc to the nearest rim;
    ///   strain    as it fills it brightens and turns from wind teal toward white gale, its sparks speed up, and past 80% it flickers.
    /// A countdown along the outer edge and a charge bar under the caster; the caster's own screen outlines the cone. FxKind.KwBlast is
    /// the unleash: the lens collapsing to a bright core, then a fan with rings rolling out and the held shots flung along it.
    /// Sounds: KineticWallSoundBank.cs, played from TickKineticWallSounds below.
    public partial class ArenaApp
    {
        static readonly Rgba KwRim = new Rgba(0.84f, 1f, 0.97f), KwCore = new Rgba(0.96f, 1f, 1f), KwHot = new Rgba(0.93f, 1f, 1f);
        const float KwBoot = 0.3f, KwRipple = 0.7f, KwRippleSpeed = 2.6f;  // ripple: its life, and how far it runs along the field a second (in lens widths / 2)

        static float KwHash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 4.1f) * 43758.5453f, 1f);

        /// The field's colour at charge fraction k: wind teal, whitening toward a gale.
        static Rgba KwTint(float k) => Game.KwColor.Mix(KwHot, k * 0.85f);

        /// A point on the lens: `u` -1..1 across it, `d` px out from its middle line.
        static V2 KwAt(Player p, float u, float d)
        {
            float a = p.KwAim + u * Game.KwSpan(p);
            return Game.KwCentre(p) + V2.FromAngle(a) * (Game.KwRadius + d);
        }

        /// The lens coordinate (-1..1) of a world point.
        static float KwU(Player p, V2 at)
        {
            V2 rel = at - Game.KwCentre(p);
            float off = MathX.Fmod(rel.Angle - p.KwAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            return Mathf.Clamp(off / Game.KwSpan(p), -1f, 1f);
        }

        /// A caught shot's place, jittering where it hangs.
        static V2 KwDotAt(Player p, int i, float t)
        {
            Game.KwDot(p, i, out float lat, out float rad);
            float o = t * 23f + KwHash(i) * 40f;
            return Game.KwDotPos(p, lat, rad) + new V2(Mathf.Sin(o) * 1.1f, Mathf.Cos(o * 0.83f) * 1.1f);
        }

        readonly List<Effect> kwRipples = new List<Effect>();

        /// How hard the ripples push at lens coordinate u: a pulse running both ways from each catch, and a bump where it struck.
        float KwRippleAt(float u)
        {
            float e = 0f;
            foreach (var fx in kwRipples)
            {
                float age = fx.T, front = age * KwRippleSpeed, d = Mathf.Abs(u - fx.Pos2.Y), f = 1f - age / KwRipple;
                e += Mathf.Exp(-(d - front) * (d - front) / 0.012f) * f + Mathf.Exp(-d * d / 0.004f) * Mathf.Max(0f, 1f - age / 0.25f) * 1.4f;
            }
            return e;
        }

        /// Called for each player with a lens up.
        void DrawKineticWall(Player p, float t)
        {
            if (p.KwT <= 0f)
                return;
            kwRipples.Clear();
            foreach (var fx in game.Effects)  // this lens's recent catches; Pos2.Y becomes their lens coordinate
                if (fx.Kind == FxKind.KwCatch && (int)fx.Pos2.X == p.Id && fx.T < KwRipple)
                    kwRipples.Add(new Effect { Pos = fx.Pos, Pos2 = new V2(0f, KwU(p, fx.Pos)), T = fx.T });

            V2 c = Game.KwCentre(p);
            float R = Game.KwRadius, span = Game.KwSpan(p), band = Game.KwBand;
            float k = Mathf.Clamp01(p.KwDots.Count / (float)Game.KwCapOf(p));
            float age = Game.KwTime - p.KwT, boot = 1f - (1f - Mathf.Clamp01(age / KwBoot)) * (1f - Mathf.Clamp01(age / KwBoot));
            float reach = boot, a = boot * Mathf.Clamp01(p.KwT / 0.15f + 0.2f);
            float strain = k > 0.8f ? 0.75f + 0.25f * Mathf.Sin(t * 47f) * Mathf.Sin(t * 13f) : 1f;
            Rgba col = KwTint(k), rimCol = KwRim.Mix(Rgba.White, k * 0.6f);
            float from = p.KwAim - span * reach, sweep = span * 2f * reach;

            GlowDisc(KwAt(p, 0f, 0f), 105f + 50f * k, col, (0.12f + 0.22f * k) * a);
            for (int i = 0; i < 3; i++)  // the soft body under the skins
                Arc(c, R, 22f - i * 6f, col.WithAlpha((0.08f + 0.12f * k) * (1f - i * 0.2f) * a), 32, from, sweep);

            // The membrane: seven skins across the band, each a wobbling polyline; the ripples push them apart where they pass.
            const int N = 40;
            for (int layer = 0; layer < 7; layer++)
            {
                float d0 = (layer - 3) * 3f;
                bool outer = layer == 0 || layer == 6;
                var lc = outer ? rimCol.WithAlpha(0.85f * a * strain) : col.WithAlpha((0.22f + 0.18f * k) * a * strain);
                float w = outer ? 1.6f : 2.4f;
                V2 prev = default;
                for (int i = 0; i <= N; i++)
                {
                    float u = -reach + 2f * reach * i / N;
                    float wob = Mathf.Sin(u * 9f + t * 4.2f + layer) * 1.6f + Mathf.Sin(u * 17f - t * 6.1f + layer * 2f) * 0.9f;
                    V2 pt = KwAt(p, u, d0 + wob + KwRippleAt(u) * 4.2f * (layer - 3) / 3f);
                    if (i > 0)
                        Line(prev, pt, w, lc);
                    prev = pt;
                }
            }
            for (int i = 0; i < 9; i++)  // light streaks flowing along it, both ways, faster as it fills
            {
                float dir = i % 2 == 1 ? 1f : -1f, phase = Mathf.Repeat(t * (0.35f + k * 0.5f) + i / 9f, 1f) * 2f - 1f, u = phase * dir * reach;
                float d = ((i * 37) % 7 - 3) * 2.4f, len = 0.16f;
                Arc(c, R + d, 1.3f, KwCore.WithAlpha(0.55f * a * (1f - Mathf.Abs(u))), 6, p.KwAim + (u - len * 0.5f) * span, len * span);
            }
            foreach (var r in kwRipples)  // the bulges running out from each catch
            {
                float f = 1f - r.T / KwRipple, fr = r.T * KwRippleSpeed;
                for (int s = -1; s <= 1; s += 2)
                {
                    float u0 = r.Pos2.Y + s * fr;
                    if (Mathf.Abs(u0) < reach)
                        Arc(c, R, 7f, Rgba.White.WithAlpha(0.3f * f * a), 6, p.KwAim + (u0 - 0.05f) * span, 0.1f * span);
                }
                if (r.T < 0.35f)  // a ring where it struck
                    Arc(r.Pos, 4f + r.T * 50f, 1.4f, Rgba.White.WithAlpha(0.9f * (1f - r.T / 0.35f)), 16);
            }
            float sp = 0.6f + k * 2.2f;  // sparks running the rims
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 1 ? 1f : -1f, u = (Mathf.Repeat(t * sp + i * 0.37f, 2f) - 1f) * reach * (i % 3 == 0 ? -1f : 1f);
                V2 at = KwAt(p, u, side * (band - 4f));
                GlowDisc(at, 8f, Rgba.White, 0.7f * a);
                Circle(at, 1.4f, Rgba.White.WithAlpha(a), 6);
            }
            if (boot < 1f)  // the boot's racing heads
                for (int s = -1; s <= 1; s += 2)
                    GlowDisc(KwAt(p, s * reach, 0f), 26f, Rgba.White, 0.9f * (1f - boot));
            for (int s = -1; s <= 1; s += 2)  // end caps
                Line(KwAt(p, s * reach, -band), KwAt(p, s * reach, band), 2f, rimCol.WithAlpha(0.9f * a));
            float left = Mathf.Clamp01(p.KwT / Game.KwTime);  // time left: a line along the outer edge, from the middle out
            Arc(c, R + band + 5f, 1.8f, KwRim.Mix(Rgba.White, 0.4f).WithAlpha(0.75f * a), 28, p.KwAim - span * left * reach, span * 2f * left * reach);

            for (int i = 0; i < p.KwDots.Count; i++)  // the held shots
            {
                V2 d = KwDotAt(p, i, t);
                GlowDisc(d, 9f + 4f * k, col, 0.5f);
                Circle(d, 3f, col.Mix(Rgba.White, 0.35f + 0.4f * k), 10);
                Circle(d, 1.2f, KwCore, 6);
                if (Mathf.Sin(t * 31f + KwHash(i) * 50f) > 0.35f)  // a flickering arc to the nearest rim
                {
                    Game.KwDot(p, i, out float lat, out float rad);
                    V2 to = KwAt(p, lat, (rad >= 0f ? 1f : -1f) * band), prevA = d;
                    for (int s = 1; s <= 3; s++)
                    {
                        float f = s / 3f;
                        V2 nx = d + (to - d) * f + (s < 3 ? new V2(KwHash(i * 7 + s + (int)(t * 30f)) - 0.5f, KwHash(i * 11 + s + (int)(t * 30f)) - 0.5f) * 4f : V2.Zero);
                        Line(prevA, nx, 0.8f, Rgba.White.WithAlpha(0.7f));
                        prevA = nx;
                    }
                }
            }

            if (p.Downed)
                return;
            for (int s = -1; s <= 1; s += 2)  // the caster's hands, raised to it
            {
                V2 h = p.Disp + V2.FromAngle(p.Aim) * 12f + V2.FromAngle(p.Aim + MathX.Pi / 2f) * (s * 10f);
                GlowDisc(h, 10f, KwRim, 0.5f + 0.2f * Mathf.Sin(t * 9f + s));
                Circle(h, 2.4f, KwCore, 8);
            }
            V2 under = p.Disp + new V2(0f, p.Def.BodyR + 14f);  // the charge bar under the caster
            float bw = 54f;
            Line(under - new V2(bw * 0.5f, 0f), under + new V2(bw * 0.5f, 0f), 5f, new Rgba(0.1f, 0.12f, 0.16f, 0.85f));
            if (k > 0f)
                Line(under - new V2(bw * 0.5f, 0f), under + new V2(bw * (k - 0.5f), 0f), 3.4f, col.Mix(Rgba.White, 0.3f));
            if (k >= 1f)
                Line(under - new V2(bw * 0.5f, 0f), under + new V2(bw * 0.5f, 0f), 3.4f, Rgba.White.WithAlpha(0.5f + 0.5f * Mathf.Sin(t * 16f)));
            if (p.Id == game.MyId)  // where the unleash will reach
            {
                float face = (p.AimAt - p.Pos).IsZero ? p.Aim : (p.AimAt - p.Pos).Angle, half = Game.KwConeHalf, reachPx = Game.KwReach;
                var edge = col.WithAlpha(0.28f);
                Line(p.Disp + V2.FromAngle(face - half) * 30f, p.Disp + V2.FromAngle(face - half) * reachPx, 1.4f, edge);
                Line(p.Disp + V2.FromAngle(face + half) * 30f, p.Disp + V2.FromAngle(face + half) * reachPx, 1.4f, edge);
                Arc(p.Disp, reachPx, 1.4f, edge, 20, face - half, half * 2f);
            }
        }

        /// FxKind.KwCatch: a shot dragged to a stop as it enters, warm while it moves and teal once it's held, with a fading streak.
        void DrawKwCatchFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), ease = 1f - (1f - f) * (1f - f);
            V2 dir = V2.FromAngle(fx.Pos2.Y);
            V2 at = fx.Pos - dir * (16f * (1f - ease)), tail = at - dir * (14f * (1f - f));
            Line(tail, at, 2f, KwRim.WithAlpha(0.55f * (1f - f)));
            Circle(at, 3.5f + 1.5f * (1f - f), fx.Color.Mix(new Rgba(1f, 0.55f, 0.4f), 1f - ease), 10);
        }

        /// FxKind.KwBlast: the lens collapsing into a bright core (the first 0.12 s), then a fan that grows to the reach in 0.18 s and
        /// fades, five rings rolling out, the hung shots flung along it as streaks, and a flash over the screen.
        void DrawKwBlastFx(Effect fx)
        {
            float T = fx.T;
            V2 o = fx.Pos, far = fx.Pos2 - fx.Pos;
            float face = far.Angle, reach = far.Length, half = Game.KwConeHalf;
            var col = fx.Color;
            if (T < 0.12f)  // the collapse
            {
                float f = T / 0.12f;
                V2 core = o + V2.FromAngle(face) * 40f;
                Arc(core + V2.FromAngle(face) * (Game.KwRadius * (1f - f) - Game.KwFront * 0.4f), Game.KwRadius * (1f - 0.8f * f), 3f + 5f * f, KwRim.WithAlpha(0.9f * (1f - f)), 20,
                    face + MathX.Pi - 0.55f * (1f - f), 1.1f * (1f - f));
                GlowDisc(core, 26f + 50f * f, Rgba.White, 0.5f + 0.5f * f);
            }
            float q = T - 0.05f;
            if (q <= 0f)
                return;
            float grow = Mathf.Clamp01(q / 0.18f), fade = q < 0.18f ? 1f : Mathf.Clamp01(1f - (q - 0.18f) / Mathf.Max(0.01f, fx.Dur - 0.23f)), r = reach * grow;
            FanGradient(o, r, KwCore.WithAlpha(0.5f * fade), col.WithAlpha(0f), 18, face - half, half * 2f);
            Wedge(o, r, face, half, col.WithAlpha(0.2f * fade), 18);
            for (int i = 0; i < 5; i++)
            {
                float rr = reach * Mathf.Min(1f, q * 1.8f + i * 0.04f) - i * 18f;
                if (rr > 16f)
                    Arc(o, rr, 4.5f - i * 0.8f, (i == 0 ? Rgba.White : KwRim).WithAlpha(fade * (0.95f - i * 0.16f)), 20, face - half, half * 2f);
            }
            for (int s = -1; s <= 1; s += 2)
                Line(o, o + V2.FromAngle(face + s * half) * r, 1.6f, KwCore.WithAlpha(0.85f * fade));
            int n = Mathf.Min(60, (int)fx.Radius);
            for (int i = 0; i < n; i++)  // the hung shots, flung out
            {
                float a = face + (KwHash(i) * 2f - 1f) * half * 0.9f, d = 25f + Mathf.Max(0f, q - KwHash(i + 5) * 0.06f) * (520f + KwHash(i + 3) * 180f);
                if (d > reach || d <= 25f)
                    continue;
                V2 head = o + V2.FromAngle(a) * d;
                Line(head - V2.FromAngle(a) * 18f, head, 2.2f, KwRim.WithAlpha(fade));
                Circle(head, 2.2f, KwCore.WithAlpha(fade), 6);
            }
            if (q < 0.18f)
                GlowDisc(o + V2.FromAngle(face) * 40f, 90f, col, 0.6f * (1f - q / 0.18f));
        }

        // ---- sounds ----------------------------------------------------------------------------------------------------------------

        readonly Dictionary<int, int> kwSeen = new Dictionary<int, int>();  // per caster: -1 no lens, else the charge last heard
        readonly List<int> kwGone = new List<int>();

        /// Kinetic Wall's sounds, from what this screen sees (nothing is sent): the cast when a lens appears, a catch per FxKind.KwCatch,
        /// the chime when a lens fills, the unleash per FxKind.KwBlast, and the hum under your own lens (an ally's, quieter). Runs before
        /// TickArcLockSounds, which marks every effect Heard.
        void TickKineticWallSounds()
        {
            if (sounds == null)
                return;
            float now = Time.unscaledTime;
            foreach (var fx in game.Effects)
            {
                if (fx.Heard)
                    continue;
                if (fx.Kind == FxKind.KwCatch)
                    sounds.PlayKwCatch(now, (int)fx.Pos2.X == game.MyId, game.QuietAt(fx.Pos));
                else if (fx.Kind == FxKind.KwBlast)
                    sounds.PlayKw(Sfx.KwUnleash, now, game.Me != null && fx.Pos.DistanceTo(game.Me.Pos) < 40f, game.QuietAt(fx.Pos));
            }
            float hum = 0f, humK = 0f;
            foreach (var p in game.Players.Values)
            {
                bool up = p.KwT > 0f && !p.Downed, mine = p.Id == game.MyId;
                int seen;
                if (!kwSeen.TryGetValue(p.Id, out seen))
                    seen = -1;
                if (up)
                {
                    int cap = Game.KwCapOf(p);
                    if (seen < 0)
                        sounds.PlayKw(Sfx.KwCast, now, mine, mine ? 0f : game.QuietAt(p.KwPos));
                    else if (seen < cap && p.KwDots.Count >= cap)
                        sounds.PlayKw(Sfx.KwFull, now, mine, mine ? 0f : game.QuietAt(p.KwPos));
                    kwSeen[p.Id] = p.KwDots.Count;
                    float level = mine ? 1f : 0.35f * (1f - game.QuietAt(p.KwPos));
                    if (level > hum)
                    {
                        hum = level;
                        humK = Mathf.Clamp01(p.KwDots.Count / (float)cap);
                    }
                }
                else
                    kwSeen[p.Id] = -1;
            }
            kwGone.Clear();
            foreach (var id in kwSeen.Keys)
                if (!game.Players.ContainsKey(id))
                    kwGone.Add(id);
            foreach (int id in kwGone)
                kwSeen.Remove(id);
            sounds.SetKwHum(hum, humK);
        }
    }
}
