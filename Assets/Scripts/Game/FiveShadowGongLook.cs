using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Five Shadow Gong's look (Core/FiveShadowGong.cs; from its mockup, the artifact "Five Shadow Gong"). Drawing only, from
    /// three effects and the smoke zones, so every game draws the same:
    ///   - FxKind.GongShow: the bronze gong dropping in on its red-lacquered frame, the Ninja's mallet winding back and snapping into
    ///     it, the disc swinging and glowing, three gold shockwave rings, and the arena dimmed from the strike to just after the sheathe;
    ///   - FxKind.GongCut: each shadow ninja (ink-dark with a lilac rim, red headband tails) at its latest spot, each cut as a
    ///     tapered gold blade from where it stood to where it landed, a fading afterimage where it left, and a gold cut mark on the
    ///     enemy until the sheathe;
    ///   - FxKind.GongSplit: an enemy cut at the sheathe splitting in two halves that fly apart, with ink and gold splatter;
    ///   - ZoneKind.GongSmoke (DrawGongSmoke): the grenade dropping with its fuse sparking, then a billowing lilac-grey cloud.
    /// With Visual effects off: no dim, glow, afterimages or splatter.
    public partial class ArenaApp
    {
        static readonly Rgba FsgWood = new Rgba(0.29f, 0.16f, 0.11f), FsgLacquer = new Rgba(0.42f, 0.23f, 0.14f), FsgRed = new Rgba(0.89f, 0.28f, 0.24f);
        static readonly Rgba FsgBronze = new Rgba(0.54f, 0.35f, 0.09f), FsgPale = new Rgba(1f, 0.9f, 0.64f);
        static readonly Rgba FsgSmokeLight = new Rgba(0.8f, 0.78f, 0.87f), FsgSmokeDark = new Rgba(0.55f, 0.52f, 0.64f);

        /// How many cuts each shadow has this cast (mastered casts run longer), from the show's length.
        static int FsgPerOf(Effect show) => show.Dur > Game.FsgEndAt(Game.FsgPer) + Game.FsgGongFade + 0.01f ? Game.FsgPerMastered : Game.FsgPer;

        readonly Dictionary<int, Effect> fsgLatest = new Dictionary<int, Effect>();

        /// WorldRenderer.cs, after the arena's contents: every gong, shadow, cut and split.
        void DrawShadowGongs(float t)
        {
            bool any = false;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GongShow || fx.Kind == FxKind.GongCut || fx.Kind == FxKind.GongSplit)
                {
                    any = true;
                    break;
                }
            if (!any)
                return;
            // the dim, from the strike until just after the sheathe
            float dim = 0f;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GongShow)
                {
                    float sh = Game.FsgSheatheAt(FsgPerOf(fx));
                    float a = Mathf.Clamp01((fx.T - Game.FsgStrike) * 4f) * (1f - Mathf.Clamp01((fx.T - sh) / 0.5f));
                    dim = Mathf.Max(dim, a);
                }
            if (dim > 0f && fxOn)
            {
                V2 c = new V2(AW / 2f, AH / 2f);
                Quad(c + new V2(-6000f, -6000f), c + new V2(6000f, -6000f), c + new V2(6000f, 6000f), c + new V2(-6000f, 6000f), new Rgba(0.02f, 0.02f, 0.06f, 0.38f * dim));
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GongShow)
                    DrawGongShow(fx, t);
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GongSplit)
                    DrawGongSplit(fx);
            // each shadow's newest cut says where it stands
            fsgLatest.Clear();
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.GongCut)
                    continue;
                int key = Mathf.RoundToInt(fx.Radius);
                Effect had;
                if (!fsgLatest.TryGetValue(key, out had) || fx.T < had.T)
                    fsgLatest[key] = fx;
                if (!(fx.Pos2 - fx.Pos).IsZero)
                    DrawGongCut(fx);
            }
            foreach (var kv in fsgLatest)
                DrawGongShadow(kv.Value, t);
        }

        void DrawGongShow(Effect fx, float t)
        {
            float age = fx.T, end = fx.Dur - Game.FsgGongFade;
            float fade = age > end ? 1f - Mathf.Clamp01((age - end) / Game.FsgGongFade) : 1f;
            if (fade <= 0f)
                return;
            V2 g = fx.Pos;
            float k = Mathf.Clamp01((age - 0.08f) / (Game.FsgLand - 0.08f));
            float drop = age < Game.FsgLand ? -420f * (1f - k * k) : 0f;
            float bounce = age > Game.FsgLand ? Mathf.Sin((age - Game.FsgLand) * 30f) * 6f * Mathf.Exp(-(age - Game.FsgLand) * 10f) : 0f;
            V2 at = g + new V2(0f, drop + bounce);
            // the shadow on the floor, growing as it falls
            Ellipse(g + new V2(0f, 46f), 50f * (0.4f + 0.6f * k), 9f, new Rgba(0f, 0f, 0f, 0.35f * fade * Mathf.Clamp01(age * 4f)));
            // the frame: two posts, the curved crossbar, red feet
            Quad(at + new V2(-46f, -54f), at + new V2(-38f, -54f), at + new V2(-38f, 46f), at + new V2(-46f, 46f), FsgWood.WithAlpha(fade));
            Quad(at + new V2(38f, -54f), at + new V2(46f, -54f), at + new V2(46f, 46f), at + new V2(38f, 46f), FsgWood.WithAlpha(fade));
            Quad(at + new V2(-60f, -62f), at + new V2(60f, -62f), at + new V2(56f, -50f), at + new V2(-56f, -50f), FsgLacquer.WithAlpha(fade));
            Quad(at + new V2(-64f, -68f), at + new V2(64f, -68f), at + new V2(60f, -62f), at + new V2(-60f, -62f), FsgRed.WithAlpha(fade));
            Quad(at + new V2(-49f, 40f), at + new V2(-35f, 40f), at + new V2(-35f, 48f), at + new V2(-49f, 48f), FsgRed.WithAlpha(fade));
            Quad(at + new V2(35f, 40f), at + new V2(49f, 40f), at + new V2(49f, 48f), at + new V2(35f, 48f), FsgRed.WithAlpha(fade));
            // the disc swinging on its cords after the strike
            float since = age - Game.FsgStrike;
            float swing = since > 0f ? Mathf.Sin(since * 14f) * 0.35f * Mathf.Exp(-since * 2.2f) : 0f;
            V2 hang = at + new V2(0f, -50f);
            V2 down = new V2(Mathf.Sin(swing), Mathf.Cos(swing));
            V2 disc = hang + down * 50f;
            V2 side = new V2(down.Y, -down.X);
            Line(hang - side * 14f, hang + down * 24f - side * 10f, 1.5f, FsgPale.WithAlpha(0.7f * fade));
            Line(hang + side * 14f, hang + down * 24f + side * 10f, 1.5f, FsgPale.WithAlpha(0.7f * fade));
            float glow = since > 0f ? Mathf.Exp(-since * 3f) : 0f;
            if (fxOn && glow > 0.02f)
                GlowDisc(disc, 90f, Game.FsgGold, 0.6f * glow * fade);
            Circle(disc, 32f, FsgBronze.WithAlpha(fade), 28);
            Circle(disc, 29f, Game.FsgGold.WithAlpha(fade), 28);
            Circle(disc + new V2(-7f, -8f), 15f, FsgPale.WithAlpha(0.55f * fade), 20);
            Arc(disc, 23f, 1.5f, FsgBronze.WithAlpha(0.6f * fade), 28);
            Arc(disc, 15f, 1.5f, FsgBronze.WithAlpha(0.6f * fade), 24);
            Circle(disc, 7f, FsgBronze.WithAlpha(fade), 14);
            if (glow > 0f)
                Circle(disc, 30f, Rgba.White.WithAlpha(0.5f * glow * glow * fade), 28);
            // the Ninja's mallet: wound back, then snapped into the disc
            Player p;
            if (game.Players.TryGetValue(Mathf.RoundToInt(fx.Radius), out p) && !p.Downed && age > Game.FsgLand - 0.1f && age < Game.FsgSpawn + 0.2f
                && p.Disp.CloserThan(disc, 200f))
            {
                float toward = (disc - p.Disp).Angle;
                float s0 = Game.FsgLand - 0.1f, s1 = Game.FsgStrike - 0.12f;
                float a = age < s1 ? -1.6f * (age - s0) / (s1 - s0) : age < Game.FsgStrike ? -1.6f + 1.75f * (age - s1) / 0.12f : 0.15f;
                V2 dir = V2.FromAngle(toward + a), perp = new V2(-dir.Y, dir.X);
                V2 grip = p.Disp + perp * 10f, head = grip + dir * 46f;
                Line(grip, head, 4f, new Rgba(0.48f, 0.29f, 0.16f));
                Ellipse(head + dir * 4f, 9f, 7f, toward + a, new Rgba(0.91f, 0.86f, 0.77f));
                Arc(head + dir * 4f, 8f, 2f, FsgRed, 14);
            }
            // the shockwave: three gold rings off the gong, and a flash at the strike
            if (since > 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float d = since - i * 0.12f;
                    if (d < 0f || d > 0.9f)
                        continue;
                    float u = d / 0.9f;
                    Arc(g, 40f + u * Game.FsgShockR, 6f * (1f - u) + 1f, Game.FsgGold.WithAlpha(0.8f * (1f - u)), 64);
                }
                if (fxOn && since < 0.15f)
                    GlowDisc(g, 260f, FsgPale, 0.7f * (1f - since / 0.15f));
            }
        }

        /// One cut: the blade from where the shadow stood to where it landed, its afterimage, and the mark on the enemy.
        void DrawGongCut(Effect fx)
        {
            float age = fx.T, left = fx.Dur - fx.T;
            V2 a = fx.Pos, b = fx.Pos2, d = b - a;
            float len = d.Length;
            V2 dir = d / len, perp = new V2(-dir.Y, dir.X);
            if (age < 0.55f)
            {
                float grow = Mathf.Clamp01(age / 0.035f), alpha = 1f - Mathf.Clamp01((age - 0.12f) / 0.43f);
                V2 tip = a + d * grow, mid = (a + tip) * 0.5f;
                if (fxOn)
                    DrawGongBlade(a, mid, tip, perp, 11f, Game.FsgLilac.WithAlpha(0.35f * alpha));
                DrawGongBlade(a, mid, tip, perp, 5f, Game.FsgGold.WithAlpha(alpha));
                DrawGongBlade(a, mid, tip, perp, 1.8f, new Rgba(1f, 0.98f, 0.94f, alpha));
                if (fxOn && age < 0.3f)
                    DrawShadowFigure(a, dir.Angle, 0.45f * (1f - age / 0.3f), 17f, true);  // the afterimage where it left
            }
            // the cut mark through the enemy, until the sheathe
            if (left > Game.FsgDepartGap + Game.FsgPop)
            {
                V2 c = b - dir * Game.FsgOver;
                Line(c - dir * 22f, c + dir * 22f, 2.5f, Game.FsgGold.WithAlpha(0.9f));
            }
        }

        void DrawGongBlade(V2 a, V2 mid, V2 tip, V2 perp, float w, Rgba col)
        {
            SetColor(col);
            Tri(a, mid + perp * w, tip);
            Tri(a, tip, mid - perp * (w * 0.4f));
        }

        /// A shadow at its newest spot: fading in as it appears, out as its smoke pops.
        void DrawGongShadow(Effect fx, float t)
        {
            float left = fx.Dur - fx.T, a = Mathf.Clamp01(fx.T / 0.15f) * Mathf.Clamp01(left / Game.FsgPop);
            if (a <= 0f)
                return;
            float face;
            if (!(fx.Pos2 - fx.Pos).IsZero)
                face = (fx.Pos2 - fx.Pos).Angle;
            else
            {
                Player p;
                V2 from = game.Players.TryGetValue(Mathf.RoundToInt(fx.Radius) / 10, out p) ? p.Disp : fx.Pos;
                face = (fx.Pos - from).IsZero ? 0f : (fx.Pos - from).Angle;  // a new shadow looks out from you
            }
            if (fxOn)
                GlowDisc(fx.Pos2, 40f, Game.FsgLilac, 0.25f * a);
            DrawShadowFigure(fx.Pos2, face, 0.95f * a, 17f, false);
        }

        /// A shadow ninja facing `face`: an ink body with a lilac rim, a red band across it, eye slits and headband tails.
        void DrawShadowFigure(V2 at, float face, float a, float r, bool ghost)
        {
            V2 f = V2.FromAngle(face), s = new V2(-f.Y, f.X);
            float sway = Mathf.Sin(game.AnimT * 11f + at.X * 0.05f) * 4f;
            V2 knot = at - f * (r * 0.8f);
            Line(knot - s * 3f, knot - f * 18f - s * (6f + sway), 3f, FsgRed.WithAlpha(a));
            Line(knot + s * 3f, knot - f * 20f + s * (8f - sway), 3f, FsgRed.WithAlpha(a));
            Ellipse(at - f * 2f + s * (r + 1f), 8f, 5.5f, face, new Rgba(0.23f, 0.21f, 0.33f, a));
            Ellipse(at - f * 2f - s * (r + 1f), 8f, 5.5f, face, new Rgba(0.23f, 0.21f, 0.33f, a));
            Circle(at, r, (ghost ? new Rgba(0.94f, 0.91f, 1f) : Game.FsgInk).WithAlpha(a), 22);
            Arc(at, r, 2.5f, new Rgba(0.85f, 0.8f, 1f, a), 22);
            Line(at - s * r * 0.95f - f * 2f, at + s * r * 0.95f - f * 2f, 4f, FsgRed.WithAlpha(a));
            Line(at + f * (r * 0.5f) - s * 6f, at + f * (r * 0.5f) - s * 2f, 2f, Rgba.White.WithAlpha(a));
            Line(at + f * (r * 0.5f) + s * 2f, at + f * (r * 0.5f) + s * 6f, 2f, Rgba.White.WithAlpha(a));
        }

        /// An enemy cut at the sheathe: two halves flying apart, ink and gold splatter, a flash along the cut.
        void DrawGongSplit(Effect fx)
        {
            float f = fx.T / fx.Dur, r = Mathf.Max(6f, fx.Radius);
            V2 cut = (fx.Pos2 - fx.Pos).IsZero ? new V2(1f, 0f) : (fx.Pos2 - fx.Pos).Normalized();
            V2 n = new V2(-cut.Y, cut.X);
            float spread = 40f * (1f - Mathf.Exp(-fx.T * 4f)), spin = 0.6f * (1f - Mathf.Exp(-fx.T * 3f));
            float a = 1f - f;
            for (int side = -1; side <= 1; side += 2)
            {
                V2 c = fx.Pos + n * (side * spread);
                float rot = cut.Angle + side * spin;
                SetColor(fx.Color.WithAlpha(a));
                const int segs = 10;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = rot + (side > 0 ? 0f : MathX.Pi) + MathX.Pi * i / segs, a1 = rot + (side > 0 ? 0f : MathX.Pi) + MathX.Pi * (i + 1) / segs;
                    Tri(c, c + V2.FromAngle(a0) * r, c + V2.FromAngle(a1) * r);
                }
                Line(c - V2.FromAngle(rot) * r, c + V2.FromAngle(rot) * r, 3f, FsgRed.WithAlpha(a));
            }
            if (fx.T < 0.25f)
                Line(fx.Pos - cut * (r + 26f), fx.Pos + cut * (r + 26f), 3f * (1f - fx.T / 0.25f) + 1f, FsgPale.WithAlpha(1f - fx.T / 0.25f));
            if (!fxOn)
                return;
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 2.39996f + fx.Pos.X * 0.013f, sp = 60f + (i * 37 % 11) * 14f;
                V2 dot = fx.Pos + V2.FromAngle(ang) * sp * (1f - Mathf.Exp(-fx.T * 6f));
                Circle(dot, (2f + i % 4) * (1f - f * 0.5f), (i % 3 == 0 ? Game.FsgGold : i % 3 == 1 ? FsgRed : Game.FsgInk).WithAlpha(a), 8);
            }
        }

        /// WorldEnemies.cs: a shadow's smoke grenade, then its cloud.
        void DrawGongSmoke(Zone z, float t)
        {
            float age = z.Duration - z.T;
            if (age < Game.FsgPop)
            {
                // the canister tumbling down with its fuse sparking
                float k = age / Game.FsgPop;
                V2 at = z.Pos + new V2(0f, -14f * Mathf.Sin(k * MathX.Pi) + 10f * k);
                float rot = age * 14f;
                V2 u = V2.FromAngle(rot) * 5f, v = V2.FromAngle(rot + MathX.Pi / 2f) * 7f;
                Quad(at - u - v, at + u - v, at + u + v, at - u + v, new Rgba(0.17f, 0.16f, 0.2f));
                Quad(at - u - v * 0.15f, at + u - v * 0.15f, at + u + v * 0.25f, at - u + v * 0.25f, FsgRed);
                Circle(at - v * 1.3f, 2.5f + Mathf.Sin(t * 60f) * 1f, FsgPale, 8);
                return;
            }
            float e = age - Game.FsgPop, grow = 1f - Mathf.Exp(-e * 7f), fade = Mathf.Clamp01(z.T / 1f);
            float scale = z.Radius / Game.FsgSmokeR;
            if (!fxOn)
            {
                Circle(z.Pos, z.Radius * grow, FsgSmokeDark.WithAlpha(0.3f * fade), 28);
                return;
            }
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 2.39996f + z.Id * 1.7f, dist = (i * 29 % 26) * scale;
                float pr = (18f + (i * 13 % 12)) * scale * (0.4f + grow * 1.1f) + Mathf.Sin(t * 1.5f + i) * 2f;
                V2 c = z.Pos + V2.FromAngle(ang) * dist * (0.5f + grow) + new V2(Mathf.Sin(t * 0.7f + i) * 4f * grow, -e * 5f);
                Circle(c, pr, FsgSmokeDark.WithAlpha(0.32f * fade), 18);
                Circle(c + new V2(-pr * 0.2f, -pr * 0.2f), pr * 0.62f, FsgSmokeLight.WithAlpha(0.3f * fade), 16);
            }
        }
    }
}
