using UnityEngine;

namespace Arena
{
    /// How the Wyvern Staff looks (Core/WyvernStaff.cs; the Wyvern Staff mockup): the wind Wyvern (a tapering segmented tail with a gold
    /// spade, scalloped bat wings on finger bones, a horned head, gold dorsal spines) hovering over you with its gale (a faint disc, a
    /// dashed rim turning one way and swirl arcs turning the other, wind streaks) under it; the fire stream it breathes (flames flying
    /// from its mouth to the spot it faces, a glow where they land); the dive (wings beating fast, speed lines) and the slam (a gold
    /// shockwave, cracks, dust; FxKind.WyvernSlam); and the Cinder meter, a teal-to-orange arc behind you.
    public partial class ArenaApp
    {
        static readonly Rgba WyvBody = new Rgba(0.18f, 0.6f, 0.55f), WyvBelly = new Rgba(0.28f, 0.77f, 0.71f), WyvDark = new Rgba(0.05f, 0.23f, 0.21f);
        static readonly Rgba WyvMembrane = new Rgba(0.45f, 0.88f, 0.81f), WyvGold = new Rgba(0.95f, 0.76f, 0.31f), WyvFlame = new Rgba(1f, 0.48f, 0.24f), WyvHot = new Rgba(1f, 0.85f, 0.4f);

        /// A point in the Wyvern's own frame (x ahead, y to its right), scaled.
        static V2 WyvP(V2 o, V2 f, V2 s, float sc, float x, float y) => o + f * (x * sc) + s * (y * sc);

        void WyvFill(Rgba col, params V2[] fan)
        {
            SetColor(col);
            for (int i = 1; i + 1 < fan.Length; i++)
                Tri(fan[0], fan[i], fan[i + 1]);
        }

        /// One Wyvern (MinionKind.Wyvern). Wind: 0 hovering, 1 breathing, 2 diving.
        void DrawWyvernMinion(Minion m, float t)
        {
            Player owner;
            float R = game.Players.TryGetValue(m.Owner, out owner) ? Game.WyvGaleRadiusOf(owner) : Game.WyvGaleR;
            bool diving = m.Wind == 2f, breathing = m.Wind == 1f;
            V2 at = m.Disp;
            if (!diving)
                DrawWyvernGale(at, R, t, m.Id);
            if (breathing)
                DrawWyvernBreath(m, t);
            DrawWyvernBody(at, m.Aim, diving, breathing, t, m.Id);
        }

        /// The gale: a faint disc, a dashed rim turning one way, swirl arcs the other, a few wind streaks blowing out.
        void DrawWyvernGale(V2 at, float R, float t, int seed)
        {
            Circle(at, R, Game.WyvColor.WithAlpha(0.07f), 36);
            if (!fxOn)
            {
                Arc(at, R, 1.6f, Game.WyvColor.WithAlpha(0.45f), 36);
                return;
            }
            Arc(at, R, 1.6f, Game.WyvColor.WithAlpha(0.2f), 36);
            for (int i = 0; i < 9; i++)  // the dashed rim, turning
            {
                float a0 = t * 0.7f + i * MathX.Tau / 9f;
                Arc(at, R, 1.8f, Game.WyvColor.WithAlpha(0.5f), 6, a0, 0.38f);
            }
            for (int i = 0; i < 3; i++)  // the swirl, the other way
            {
                float a0 = -t * (2.4f - i * 0.6f) + i * 2.1f + seed;
                Arc(at, R * (0.4f + 0.2f * i), 2f, Game.WyvColor.WithAlpha(0.42f - 0.1f * i), 10, a0, 1.5f);
            }
            for (int i = 0; i < 8; i++)  // streaks blowing out
            {
                float ph = (t * 1.3f + i / 8f) % 1f, a = BeeHash(i * 2.7f + seed) * MathX.Tau;
                V2 d = V2.FromAngle(a + ph * 0.6f);
                Line(at + d * (R * (0.25f + 0.65f * ph)), at + d * (R * (0.25f + 0.65f * ph) + 9f), 1.4f, WyvMembrane.WithAlpha(0.55f * (1f - ph)));
            }
        }

        /// The fire stream: flames flying from its mouth to the spot it faces (Game.BreathStand ahead), a glow where they land.
        void DrawWyvernBreath(Minion m, float t)
        {
            V2 f = V2.FromAngle(m.Aim), mouth = m.Disp + f * 38f, hit = m.Disp + f * Game.BreathStand;
            V2.Axes(m.Aim, out V2 fa, out V2 sd);
            float len = Mathf.Max(8f, (hit - mouth).Length);
            Circle(hit, Game.BreathWidth * 1.7f, WyvFlame.WithAlpha(0.18f + 0.05f * Mathf.Sin(t * 30f)), 20);
            Circle(hit, Game.BreathWidth * 0.9f, WyvHot.WithAlpha(0.22f), 16);
            if (!fxOn)
            {
                Line(mouth, hit, Game.BreathWidth * 1.2f, WyvFlame.WithAlpha(0.5f));
                return;
            }
            for (int i = 0; i < 26; i++)
            {
                float ph = (t * 3.4f + i / 26f) % 1f, jit = (BeeHash(i * 3.1f + Mathf.Floor(t * 3.4f + i / 26f) * 1.7f) - 0.5f) * 2f;
                V2 p = mouth + fa * (len * ph) + sd * (jit * (3f + Game.BreathWidth * 0.9f * ph));
                float r = 3f + 7.5f * ph;
                Rgba c = WyvHot.Mix(WyvFlame, ph).WithAlpha(0.85f * (1f - ph * 0.55f));
                Circle(p, r, c, 10);
            }
            Circle(mouth, 9f + Mathf.Sin(t * 40f), WyvHot.WithAlpha(0.7f), 12);
        }

        /// The Wyvern itself, facing `aim`: its shadow on the ground, then the tail, hind claws, wings, body, spines, neck and head.
        void DrawWyvernBody(V2 at, float aim, bool diving, bool breathing, float t, int seed)
        {
            float flap = 0.72f + 0.28f * Mathf.Sin(t * (diving ? 30f : 11f) + seed);
            float sc = diving ? 1.0f : 1.2f;
            V2.Axes(aim, out V2 f, out V2 s);
            Ellipse(at + new V2(8f, 24f), 34f * flap, 11f, 0f, new Rgba(0f, 0f, 0f, 0.28f), 16);
            if (diving)
                for (int i = 1; i <= 5; i++)  // speed lines streaking behind it
                    Line(at - f * (14f * i), at - f * (14f * i + 18f), 2.6f - 0.4f * i, WyvMembrane.WithAlpha(0.4f - 0.07f * i));
            // tail: tapering segments, ending in a gold spade
            V2[] tail = new V2[9];
            for (int i = 0; i <= 8; i++)
                tail[i] = WyvP(at, f, s, sc, -9f - i * 6.5f, Mathf.Sin(t * 4.5f - i * 0.7f + seed) * (1.5f + i * 1.3f));
            for (int i = 0; i < 8; i++)
                Line(tail[i], tail[i + 1], (7f - i * 0.6f) * sc, (i % 2 == 0 ? WyvBody : WyvBody.WithAlpha(0.85f)));
            V2 tp = tail[8];
            WyvFill(WyvGold, tp - f * (9f * sc), tp + f * (-1f * sc) - s * (5f * sc), tp + f * (2f * sc), tp + f * (-1f * sc) + s * (5f * sc));
            for (int k = -1; k <= 1; k += 2)  // hind claws
                WyvFill(WyvDark.WithAlpha(0.9f), WyvP(at, f, s, sc, -6f, k * 6f), WyvP(at, f, s, sc, -13f, k * 11f), WyvP(at, f, s, sc, -9f, k * 5f));
            // wings: scalloped membrane on an arm and three fingers
            for (int k = -1; k <= 1; k += 2)
            {
                V2 sh = WyvP(at, f, s, sc, 3f, k * 5f), el = WyvP(at, f, s, sc, -2f, k * (13f + 6f * flap));
                V2 f1 = WyvP(at, f, s, sc, 10f, k * (40f * flap + 4f)), f2 = WyvP(at, f, s, sc, -8f, k * (42f * flap + 2f)), f3 = WyvP(at, f, s, sc, -24f, k * (30f * flap + 2f));
                V2 rt = WyvP(at, f, s, sc, -14f, k * 6f);
                V2 c1 = WyvP(at, f, s, sc, 1f, k * (24f * flap + 4f)), c2 = WyvP(at, f, s, sc, -17f, k * (26f * flap + 4f)), c3 = WyvP(at, f, s, sc, -22f, k * 10f);
                WyvFill(WyvBody.WithAlpha(0.9f), sh, f1, c1, f2, c2, f3, c3, rt);
                WyvFill(WyvMembrane.WithAlpha(0.55f), sh, f1, c1, f2, c2, f3, c3, rt);
                Rgba edge = new Rgba(0.75f, 0.97f, 0.93f, 0.95f);
                Line(f1, c1, 1.4f, edge); Line(c1, f2, 1.4f, edge); Line(f2, c2, 1.4f, edge); Line(c2, f3, 1.4f, edge); Line(f3, c3, 1.4f, edge); Line(c3, rt, 1.4f, edge);
                Line(sh, el, 2.2f * sc, WyvDark.WithAlpha(0.9f));
                Line(el, f1, 1.8f * sc, WyvDark.WithAlpha(0.9f));
                Line(el, f2, 1.5f * sc, WyvDark.WithAlpha(0.8f));
                Line(el, f3, 1.5f * sc, WyvDark.WithAlpha(0.8f));
                Circle(f1, 2.2f, WyvGold, 6);
            }
            // body with its belly lighter, a ridge of gold spines
            Ellipse(at, 16f * sc, 7.5f * sc, aim, WyvDark, 18);
            Ellipse(at, 15f * sc, 6.6f * sc, aim, WyvBody, 18);
            Ellipse(at + s * (0.5f * sc), 11f * sc, 3.2f * sc, aim, WyvBelly.WithAlpha(0.8f), 14);
            for (int i = -2; i <= 2; i++)
            {
                V2 c = WyvP(at, f, s, sc, i * 4.5f, 0f);
                WyvFill(WyvGold, c - f * (1.8f * sc), c + s * (2.2f * sc), c + f * (1.8f * sc), c - s * (2.2f * sc));
            }
            // neck, head, horns, eyes
            Line(WyvP(at, f, s, sc, 10f, 0f), WyvP(at, f, s, sc, 20f, 0f), 7f * sc, WyvBody);
            V2 h0 = WyvP(at, f, s, sc, 17f, -5f), h1 = WyvP(at, f, s, sc, 27f, -3.4f), h2 = WyvP(at, f, s, sc, 35f, -1.2f);
            V2 h3 = WyvP(at, f, s, sc, 35f, 1.2f), h4 = WyvP(at, f, s, sc, 27f, 3.4f), h5 = WyvP(at, f, s, sc, 17f, 5f);
            WyvFill(WyvDark, h0, h1, h2, h3, h4, h5);
            WyvFill(WyvBelly, WyvP(at, f, s, sc, 18f, -4f), WyvP(at, f, s, sc, 27f, -2.6f), WyvP(at, f, s, sc, 33.5f, -0.8f), WyvP(at, f, s, sc, 33.5f, 0.8f), WyvP(at, f, s, sc, 27f, 2.6f), WyvP(at, f, s, sc, 18f, 4f));
            for (int k = -1; k <= 1; k += 2)
            {
                Line(WyvP(at, f, s, sc, 20f, k * 3.6f), WyvP(at, f, s, sc, 14f, k * 6.4f), 2f * sc, WyvGold);
                Line(WyvP(at, f, s, sc, 14f, k * 6.4f), WyvP(at, f, s, sc, 9f, k * 11f), 1.6f * sc, WyvGold);
                Circle(WyvP(at, f, s, sc, 25f, k * 3.2f), 1.8f * sc, breathing ? WyvHot : WyvGold, 6);
                Circle(WyvP(at, f, s, sc, 25.4f, k * 3.2f), 0.7f * sc, WyvDark, 4);
            }
            if (breathing)
                Circle(WyvP(at, f, s, sc, 36f, 0f), 14f, WyvHot.WithAlpha(0.45f + 0.1f * Mathf.Sin(t * 40f)), 14);
        }

        /// FxKind.WyvernSlam (Pos where it landed, Radius the shockwave's reach, Dur its life): a gold ring racing out, a fading disc, cracks, dust.
        void DrawWyvernSlamFx(Effect fx)
        {
            float age = fx.T, k = Mathf.Clamp01(age / 0.28f), ease = 1f - (1f - k) * (1f - k) * (1f - k), fade = 1f - Mathf.Clamp01((age - 0.28f) / Mathf.Max(0.05f, fx.Dur - 0.28f));
            float r = Mathf.Max(8f, fx.Radius * ease);
            Circle(fx.Pos, r, new Rgba(0.95f, 0.76f, 0.31f, 0.13f * fade), 28);
            Arc(fx.Pos, r, 5f * fade + 1f, new Rgba(0.95f, 0.76f, 0.31f, fade), 36);
            Arc(fx.Pos, r * 0.82f, 2f, Game.WyvColor.WithAlpha(0.6f * fade), 30);
            if (!fxOn)
                return;
            for (int i = 0; i < 9; i++)  // cracks running out of the point
            {
                float a = i * MathX.Tau / 9f + BeeHash(i * 1.9f) * 0.4f, len = fx.Radius * (0.45f + 0.35f * BeeHash(i * 5.3f)) * ease;
                V2 d = V2.FromAngle(a);
                Line(fx.Pos + d * 8f, fx.Pos + d * len, 2f, WyvDark.WithAlpha(0.8f * fade));
            }
            for (int i = 0; i < 18; i++)  // dust thrown up
            {
                float a = BeeHash(i * 3.7f) * MathX.Tau, sp = 40f + 120f * BeeHash(i * 8.1f);
                V2 p = fx.Pos + V2.FromAngle(a) * (sp * age) + new V2(0f, -40f * age + 90f * age * age);
                Circle(p, 2.4f * (1f - k * 0.5f), new Rgba(0.95f, 0.8f, 0.5f, 0.7f * fade), 6);
            }
        }

        /// The Cinder meter: a teal-to-orange arc behind you while it isn't full, glowing as you breathe; a notch where the breath can start.
        void DrawCinderMeter(Player p)
        {
            bool breathing = p.Firing || p.FiringSeen;
            if (!fxOn || p.Downed || p.Cinder >= Game.CinderMax - 0.5f && !breathing)
                return;
            float fill = Mathf.Clamp01(p.Cinder / Game.CinderMax), sweep = 1.9f, r = p.Def.BodyR + 13f;
            float start = p.Aim + MathX.Pi - sweep / 2f;
            Arc(p.Disp, r, 5f, Game.WyvColor.WithAlpha(0.16f), 20, start, sweep);
            if (fill > 0f)
            {
                if (breathing && p.Cinder > 0f)
                    Arc(p.Disp, r, 9f, WyvFlame.WithAlpha(0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 7f)), 20, start, sweep * fill);
                Arc(p.Disp, r, 5f, Game.WyvColor.Mix(WyvFlame, fill < 0.5f ? 1f - fill * 2f : 0f).WithAlpha(p.Cinder >= Game.CinderMin ? 1f : 0.45f), 20, start, sweep * fill);
            }
            for (int i = 1; i < 4; i++)  // a mark every 25
            {
                float a = start + sweep * i / 4f;
                Line(p.Disp + V2.FromAngle(a) * (r - 3f), p.Disp + V2.FromAngle(a) * (r + 3f), 1f, Rgba.White.WithAlpha(0.35f));
            }
            float notch = start + sweep * Game.CinderMin / Game.CinderMax;
            Line(p.Disp + V2.FromAngle(notch) * (r - 4f), p.Disp + V2.FromAngle(notch) * (r + 4f), 1.4f, WyvHot.WithAlpha(0.7f));
        }
    }
}
