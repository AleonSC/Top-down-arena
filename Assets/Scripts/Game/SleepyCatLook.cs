using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// How Sleepy Cat looks (the Healer Summoner's fourth ultimate, Core/SleepyCat.cs), from the mockup (claude.ai artifact
    /// "Sleepy Cat"). All from the replicated zone (`ZoneKind.SleepyCat`: Pos the cat, Aim its heading, Radius the meow's reach,
    /// Marched its speed, Quiet napping, BoltsLeft the meows left, GunCd since the last meow) and each enemy's CatNap byte:
    ///   - on the floor (DrawSleepyCatFloor, from DrawZone): while it prowls, a dashed gold ring of the meow's reach round the cat,
    ///     with a paw pip for each meow left; while it naps, a dusky disc, the purr's dashed green ring with ripples rolling out, and
    ///     the nap's time left as a lilac arc;
    ///   - over the arena (DrawSleepyCats): the lilac cat, side on. Prowling it gallops (legs, stretch and bob by its speed, tail
    ///     streaming), and a meow opens its mouth and sends sound arcs out ahead; napping it is a breathing loaf with its tail
    ///     wrapped round, eyes shut, a crescent moon and Zs drifting up; it fades at the end;
    ///   - on enemies (DrawCatMarks, from the enemy draw): a gold paw over a marked enemy; a sleeper wears a lilac ring of its
    ///     sleep left and little Zs drifting off its head (no daze stars).
    public partial class ArenaApp
    {
        static readonly Rgba CatFur = new Rgba(0.66f, 0.55f, 0.94f), CatFurDark = new Rgba(0.43f, 0.33f, 0.72f), CatBelly = new Rgba(0.91f, 0.87f, 0.99f);
        static readonly Rgba CatPink = new Rgba(0.95f, 0.65f, 0.78f), CatGold = new Rgba(1f, 0.8f, 0.35f), CatLilac = new Rgba(0.71f, 0.55f, 1f);
        static readonly Rgba CatInk = new Rgba(0.1f, 0.06f, 0.19f), CatPurr = new Rgba(0.35f, 0.94f, 0.54f), CatZ = new Rgba(0.89f, 0.84f, 1f);
        const float CatScale = 1.3f;

        readonly Dictionary<int, V2> catDisp = new Dictionary<int, V2>();     // each cat where this screen draws it (eased after Pos)
        readonly Dictionary<int, float> catStride = new Dictionary<int, float>();  // each cat's run cycle

        V2 CatDisp(Zone z)
        {
            V2 at;
            if (!catDisp.TryGetValue(z.Id, out at) || (at - z.Pos).Length > 300f)
                at = z.Pos;
            at += (z.Pos - at) * Mathf.Min(1f, Time.unscaledDeltaTime * 20f);
            catDisp[z.Id] = at;
            return at;
        }

        /// Under the arena's contents (DrawZone): the meow's reach while it prowls; the purr and the time left while it naps.
        void DrawSleepyCatFloor(Zone z, float t)
        {
            V2 c = CatDisp(z);
            float age = Game.ScAge(z);
            if (!z.Quiet)
            {
                float a = Mathf.Clamp01(age / 0.25f);
                if (fxOn)
                    Circle(c, z.Radius, CatGold.WithAlpha(0.06f * a), 36);
                for (int k = 0; k < 16; k++)  // the dashed ring, crawling round
                    Arc(c, z.Radius, 2f, CatGold.WithAlpha(0.5f * a), 3, k * MathX.Tau / 16f + t * 0.8f, MathX.Tau / 32f);
                float left = Mathf.Clamp01(1f - age / Game.ScProwl);  // the prowl's time left, a thin arc just inside
                Arc(c, z.Radius - 5f, 2f, CatGold.WithAlpha(0.8f * a), 40, -MathX.Pi * 0.5f, MathX.Tau * left);
                if (z.GunCd < 0.45f && fxOn)  // the meow's pulse
                {
                    float q = z.GunCd / 0.45f;
                    Circle(c, z.Radius * (0.35f + 0.65f * Mathf.Sqrt(q)), CatGold.WithAlpha(0.16f * (1f - q)), 36);
                }
                for (int i = 0; i < z.BoltsLeft && i < 6; i++)  // a paw pip for each meow left, just under the cat
                    DrawPaw(c + new V2((i - (z.BoltsLeft - 1) * 0.5f) * 13f, 32f * CatScale), 4.5f, CatGold.WithAlpha(0.9f * a));
                return;
            }
            float fade = Mathf.Clamp01(z.T / Game.ScFade) * Mathf.Clamp01(age / 0.4f), napLeft = Mathf.Clamp01(1f - age / Game.ScNap);
            if (age >= Game.ScNap)
                return;
            float R = Game.ScPurrR;
            Circle(c, R, new Rgba(0.16f, 0.12f, 0.43f, 0.22f * fade), 40);  // dusk round the sleeping cat
            if (fxOn)
                for (int i = 0; i < 3; i++)  // the purr rolling out
                {
                    float p = (t * 0.6f + i / 3f) % 1f;
                    Arc(c, 30f + p * (R - 30f), 2f, CatPurr.WithAlpha(0.35f * (1f - p) * fade), 36);
                }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            for (int k = 0; k < 24; k++)
                Arc(c, R, 2f, CatPurr.WithAlpha((0.4f + 0.25f * pulse) * fade), 3, k * MathX.Tau / 24f - t * 0.3f, MathX.Tau / 60f);
            Arc(c, R + 7f, 3f, CatLilac.WithAlpha(0.85f * fade), 48, -MathX.Pi * 0.5f, MathX.Tau * napLeft);
        }

        /// Over the arena's contents: every cat, running or napping.
        void DrawSleepyCats(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.SleepyCat)
                    continue;
                V2 c = CatDisp(z);
                if (!z.Quiet)
                    DrawRunningCat(z, c, t);
                else
                    DrawNappingCat(z, c, t);
            }
        }

        /// The cat side on, galloping after the cursor; a fresh meow opens its mouth and sends arcs out ahead.
        void DrawRunningCat(Zone z, V2 c, float t)
        {
            float sp = Mathf.Clamp01(z.Marched / Game.ScSpeed), f = Mathf.Cos(z.Aim) < 0f ? -1f : 1f, k = CatScale;
            float dtf = Time.unscaledDeltaTime;
            float s;
            if (dtf > 0f)
            {
                catStride.TryGetValue(z.Id, out s);
                s += (3f + sp * 15f) * dtf;
                catStride[z.Id] = s;
            }
            else
                s = t * (3f + sp * 15f);  // (the preview: no frame clock)
            float bob = fxOn ? Mathf.Abs(Mathf.Sin(s)) * 4f * sp : 0f, a = Mathf.Clamp01(Game.ScAge(z) / 0.2f);
            catC = c; catFx = f * k; catFy = k; catBob = bob;
            Ellipse(c + new V2(0f, 17f * k), 26f * k, 7f * k, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 16);  // its shadow
            Rgba dark = CatFurDark.WithAlpha(a), fur = CatFur.WithAlpha(a);
            // the tail, streaming back
            V2 t0 = CatPt(-18f, -2f), t1 = CatPt(-30f, -6f - 8f * sp), t2 = CatPt(-38f + Mathf.Sin(s * 0.5f) * 3f, -20f - Mathf.Cos(s) * 6f * sp);
            Line(t0, t1, 5.5f * k, dark);
            Line(t1, t2, 5f * k, dark);
            Circle(t2, 2.6f * k, dark, 8);
            // legs: the front pair and the back pair swing opposite
            float sw = Mathf.Sin(s) * 9f * sp;
            float[] lx = { -11f, -6f, 10f, 15f }, lo = { sw, sw * 0.7f, -sw, -sw * 0.7f };
            for (int i = 0; i < 4; i++)
                Line(CatPt(lx[i], 6f), CatPt(lx[i] + lo[i], 17f + bob), 4.5f * k, dark);
            // body (stretching as it runs) and belly
            Ellipse(CatPt(0f, 0f), (21f + 4f * sp) * k, (11f - 1.5f * sp) * k, 0f, fur, 20);
            Ellipse(CatPt(2f, 5f), 13f * k, 4.5f * k, 0f, CatBelly.WithAlpha(a), 14);
            // head, ears, eye, nose, whiskers
            float hx = 22f + 3f * sp, hy = -8f;
            Circle(CatPt(hx, hy), 11f * k, fur, 18);
            SetColor(fur);
            Tri(CatPt(hx - 9f, hy - 5f), CatPt(hx - 6f, hy - 19f), CatPt(hx - 1f, hy - 9f));
            Tri(CatPt(hx + 1f, hy - 9f), CatPt(hx + 7f, hy - 19f), CatPt(hx + 10f, hy - 4f));
            SetColor(CatPink.WithAlpha(a));
            Tri(CatPt(hx - 7f, hy - 8f), CatPt(hx - 5.5f, hy - 15f), CatPt(hx - 3f, hy - 9f));
            bool meowing = z.GunCd < 0.3f;
            Ellipse(CatPt(hx + 4f, hy - 2f), 2.6f * k, (meowing ? 3.6f : 3f) * k, 0f, CatGold.WithAlpha(a), 10);
            Line(CatPt(hx + 4.2f, hy - 4.5f), CatPt(hx + 4.2f, hy + 0.5f), 1.3f * k, CatInk.WithAlpha(a));
            Circle(CatPt(hx + 10.5f, hy + 2f), 1.8f * k, CatPink.WithAlpha(a), 8);
            if (meowing)
                Ellipse(CatPt(hx + 8f, hy + 6f), 2.6f * k, 3f * k, 0f, new Rgba(0.23f, 0.09f, 0.22f, a), 10);
            Rgba whisk = new Rgba(1f, 1f, 1f, 0.7f * a);
            Line(CatPt(hx + 9f, hy + 3f), CatPt(hx + 20f, hy + 1f), 1f, whisk);
            Line(CatPt(hx + 9f, hy + 4f), CatPt(hx + 20f, hy + 6f), 1f, whisk);
            if (z.GunCd < 0.6f && fxOn)  // the meow: sound arcs out ahead of its mouth
            {
                float q = z.GunCd / 0.6f;
                V2 mouth = CatPt(hx + 12f, hy + 4f);
                float dir = f > 0f ? 0f : MathX.Pi;
                for (int i = 0; i < 3; i++)
                {
                    float r = 8f + (q * 30f + i * 9f) * k;
                    Arc(mouth, r, 2.5f, CatGold.WithAlpha((1f - q) * (1f - i * 0.25f) * a), 10, dir - 0.55f, 1.1f);
                }
            }
        }

        /// The cat curled up asleep: a breathing loaf, tail round the front, eyes shut, a moon and Zs. Fades at the end.
        void DrawNappingCat(Zone z, V2 c, float t)
        {
            float age = Game.ScAge(z), k = CatScale;
            float a = Mathf.Clamp01(z.T / Game.ScFade);
            float br = fxOn ? 1f + Mathf.Sin(t * 2.4f) * 0.04f : 1f;
            catC = c; catFx = k * br; catFy = k * br; catBob = 0f;
            Ellipse(c + new V2(0f, 18f * k), 30f * k, 7f * k, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 16);
            Rgba fur = CatFur.WithAlpha(a), dark = CatFurDark.WithAlpha(a);
            Ellipse(CatPt(0f, 2f), 26f * k * br, 17f * k * br, 0f, fur, 24);  // the loaf
            Ellipse(CatPt(-4f, -4f), 14f * k, 7f * k, -0.3f, CatFurDark.WithAlpha(0.5f * a), 14);  // a stripe of darker fur
            Circle(CatPt(14f, 4f), 11f * k * br, fur, 18);  // the head tucked in at the right
            SetColor(fur);
            Tri(CatPt(6f, -2f), CatPt(8f, -14f), CatPt(13f, -6f));
            Tri(CatPt(15f, -6f), CatPt(21f, -14f), CatPt(23f, -1f));
            Rgba ink = CatInk.WithAlpha(a);
            Arc(CatPt(12f, 4f), 3f * k, 1.6f, ink, 6, 0.1f * MathX.Pi, 0.8f * MathX.Pi);  // eyes shut
            Arc(CatPt(19f, 4f), 3f * k, 1.6f, ink, 6, 0.1f * MathX.Pi, 0.8f * MathX.Pi);
            Arc(CatPt(0f, 4f), 22f * k * br, 6.5f * k, dark, 14, 0.15f * MathX.Pi, 0.7f * MathX.Pi);  // the tail wrapped round the front
            if (age >= Game.ScNap)
                return;
            // the moon and the Zs
            V2 moon = CatPt(-24f, -34f);
            Circle(moon, 9f, CatGold.WithAlpha(a), 16);
            Circle(moon + new V2(4f, -3f), 8f, new Rgba(0.08f, 0.1f, 0.2f, a), 16);
            for (int i = 0; i < 3; i++)
            {
                float p = (t * 0.45f + i / 3f) % 1f;
                DrawZ(CatPt(14f + p * 24f, -18f - p * 44f), (6f + p * 6f) * k, CatZ.WithAlpha(a * Mathf.Sin(p * MathX.Pi)));
            }
        }

        V2 catC;
        float catFx, catFy, catBob;

        /// A point on the cat being drawn: (x, y) in its own px (x forward), set up by the draw above.
        V2 CatPt(float x, float y) => catC + new V2(x * catFx, (y - catBob) * catFy);

        /// An enemy's mark or sleep (from the enemy draw, in place of the daze stars).
        void DrawCatMarks(Enemy e, V2 pos, float r, float t)
        {
            if (Game.CatMarked(e))
            {
                DrawPaw(pos + new V2(0f, -r - 14f), 7f, CatGold);
                return;
            }
            if (!Game.CatAsleep(e))
                return;
            float left = Mathf.Clamp01(Game.CatSleepLeft(e) / Game.ScNap);
            Arc(pos, r + 7f, 2.5f, CatLilac.WithAlpha(0.25f), 24);
            Arc(pos, r + 7f, 2.5f, CatLilac, 24, -MathX.Pi * 0.5f, MathX.Tau * left);
            float seed = (e.Id * 0.37f) % 1f;
            for (int i = 0; i < 3; i++)
            {
                float p = (t * 0.5f + i / 3f + seed) % 1f;
                DrawZ(pos + new V2(8f + p * 16f + Mathf.Sin(p * 6f) * 3f, -r - 4f - p * 28f), 4f + p * 4f, CatZ.WithAlpha(Mathf.Sin(p * MathX.Pi)));
            }
        }

        /// A paw print: a pad and four toes.
        void DrawPaw(V2 c, float s, Rgba col)
        {
            Ellipse(c + new V2(0f, s * 0.35f), s * 0.62f, s * 0.5f, 0f, col, 12);
            Ellipse(c + new V2(-0.62f * s, -0.25f * s), s * 0.22f, s * 0.27f, 0f, col, 8);
            Ellipse(c + new V2(-0.22f * s, -0.62f * s), s * 0.22f, s * 0.27f, 0f, col, 8);
            Ellipse(c + new V2(0.22f * s, -0.62f * s), s * 0.22f, s * 0.27f, 0f, col, 8);
            Ellipse(c + new V2(0.62f * s, -0.25f * s), s * 0.22f, s * 0.27f, 0f, col, 8);
        }

        /// A sleepy "Z", `h` px tall, drawn with three strokes.
        void DrawZ(V2 c, float h, Rgba col)
        {
            if (col.A <= 0.01f)
                return;
            float w = h * 0.8f, lw = Mathf.Max(1.5f, h * 0.18f);
            V2 tl = c + new V2(-w * 0.5f, -h * 0.5f), tr = c + new V2(w * 0.5f, -h * 0.5f), bl = c + new V2(-w * 0.5f, h * 0.5f), brr = c + new V2(w * 0.5f, h * 0.5f);
            Line(tl, tr, lw, col);
            Line(tr, bl, lw, col);
            Line(bl, brr, lw, col);
        }
    }
}
