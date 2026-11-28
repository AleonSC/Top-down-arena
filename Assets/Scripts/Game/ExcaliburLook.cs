using UnityEngine;

namespace Arena
{
    /// Excalibur's look (Core/Excalibur.cs; from its mockup). Drawing only, from the replicated ExcT, ExcAge and ExcSwing and
    /// FxKind.Excalibur.
    ///   - Draw: a shaft of light comes down on you (the fx) and the sword grows in your hand, a ring of light closing in.
    ///   - Held: a giant sword of white light with a gold edge, guard and pommel and a blue gem, glowing and shedding motes. At
    ///     rest it's held out to the side the last swing ended on; a swing sweeps it across 150° in front of you with a gold fan
    ///     behind it; a Judgement lifts it overhead (shorter, seen from above) and drives it down your aim with a flash.
    ///   - Under you: a pool of gold light, a gold ring draining with the time left (flashing near the end) and four diamonds
    ///     counting the swings to the next Judgement.
    ///   - The fx: the shockwaves (gold crescents edged white, a Judgement's taller with a blue rim), the burst at the end.
    public partial class ArenaApp
    {
        static float ExcH(float n) { float x = Mathf.Sin(n * 78.233f + 12.9898f) * 43758.5453f; return x - Mathf.Floor(x); }
        static float ExcEase(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        /// Where the sword points and how long it is, from the swing.
        static void ExcPose(Player p, out float ang, out float len, out float flash)
        {
            float half = Game.ExcArcDeg * 0.5f * MathX.Pi / 180f;
            int n = Game.ExcSwingNo(p);
            float k = p.ExcSwing - n;
            float side = n % 2 == 1 ? 1f : -1f;  // odd swings go left to right and end on the right
            flash = 0f;
            len = Game.ExcSwordLen;
            if (p.ExcAge < Game.ExcDraw)
            {
                float g = Mathf.Clamp01(p.ExcAge / Game.ExcDraw);
                ang = p.Aim - 0.9f;
                len = Mathf.Lerp(30f, Game.ExcSwordLen, EaseOut3(g));
                return;
            }
            if (n > 0 && k > 0f && Game.ExcIsJudge(n))
            {
                ang = p.Aim;
                if (k < 0.45f)  // raised overhead: foreshortened
                    len = Game.ExcSwordLen * Mathf.Lerp(1f, 0.35f, ExcEase(k / 0.45f));
                else
                {
                    float d = (k - 0.45f) / 0.55f;
                    len = Game.ExcSwordLen * Mathf.Lerp(0.35f, 1.12f, EaseOut3(Mathf.Clamp01(d * 2.5f)));
                    flash = 1f - d;
                }
                return;
            }
            if (n > 0 && k > 0f)
            {
                ang = p.Aim - side * half + side * 2f * half * EaseOut3(k);
                flash = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                return;
            }
            ang = p.Aim + (n == 0 ? -0.9f : side * 0.9f);
        }

        /// The sword from c along ang, len long (hilt at c).
        void DrawExcSword(V2 c, float ang, float len, float glow, float t, float alpha)
        {
            V2.Axes(ang, out V2 X, out V2 Y);
            float sc = len / Game.ExcSwordLen, bw = 7f * Mathf.Max(0.6f, sc), guard = len * 0.13f;
            V2 b0 = c + X * guard, b1 = c + X * (len * 0.88f), tip = c + X * len;
            if (fxOn)
            {
                for (int i = 0; i < 4; i++)
                    GlowDisc(V2.Lerp(b0, tip, 0.15f + i * 0.25f), 30f * sc + 6f, Game.ExcGold, 0.22f * glow * alpha);
                Quad(b0 - Y * (bw * 2.2f), b1 - Y * (bw * 2.2f), b1 + Y * (bw * 2.2f), b0 + Y * (bw * 2.2f), Game.ExcGold.WithAlpha(0.16f * glow * alpha));
            }
            Quad(b0 - Y * bw, b1 - Y * bw, b1 + Y * bw, b0 + Y * bw, Game.ExcHoly.WithAlpha(alpha));  // the blade
            SetColor(Game.ExcHoly.WithAlpha(alpha));
            Tri(b1 - Y * bw, tip, b1 + Y * bw);
            Line(b0 - Y * bw, b1 - Y * bw, 1.6f, Game.ExcGold.WithAlpha(0.95f * alpha));  // the gold edges
            Line(b0 + Y * bw, b1 + Y * bw, 1.6f, Game.ExcGold.WithAlpha(0.95f * alpha));
            Line(b1 - Y * bw, tip, 1.6f, Game.ExcGold.WithAlpha(0.95f * alpha));
            Line(b1 + Y * bw, tip, 1.6f, Game.ExcGold.WithAlpha(0.95f * alpha));
            Line(b0 + X * 6f, b1, 1.8f, Rgba.White.WithAlpha(0.9f * alpha));  // the fuller's light
            float gw = bw * 3.4f;  // the guard, the grip, the pommel, the gem
            Quad(b0 - Y * gw - X * 3f, b0 + Y * gw - X * 3f, b0 + Y * gw + X * 4f, b0 - Y * gw + X * 4f, Game.ExcGold.WithAlpha(alpha));
            Line(c - X * 4f, b0, 5f, new Rgba(0.33f, 0.2f, 0.11f, alpha));
            Circle(c - X * 6f, 4.5f * Mathf.Max(0.7f, sc), Game.ExcGold.WithAlpha(alpha), 10);
            Circle(b0, 3.2f, Game.ExcGem.WithAlpha(alpha), 8);
            if (fxOn && glow > 0.5f)  // a glint running up the blade
            {
                float g = Mathf.Repeat(t * 0.9f, 1f);
                GlowDisc(V2.Lerp(b0, tip, g), 10f, Rgba.White, 0.7f * alpha * Mathf.Sin(g * MathX.Pi));
            }
        }

        /// WorldRenderer.DrawPlayerAuras: the pool of light, the time ring and the Judgement pips.
        void DrawExcaliburUnder(Player p, float t)
        {
            if (!Game.ExcOn(p))
                return;
            V2 c = p.Disp;
            float r = p.Def.BodyR;
            if (fxOn)
                GlowDisc(c, 130f, Game.ExcGold, 0.2f);
            if (p.ExcAge < Game.ExcDraw)
            {
                float k = Mathf.Clamp01(p.ExcAge / Game.ExcDraw);
                Arc(c, r + 8f + 70f * (1f - EaseOut3(k)), 2.5f, Game.ExcHoly.WithAlpha(0.3f + 0.6f * k), 40);
                return;
            }
            float total = Mathf.Max(0.01f, p.ExcAge + p.ExcT - Game.ExcDraw), left = Mathf.Clamp01(p.ExcT / total);
            Rgba ring = p.ExcT < 1.5f && Mathf.Sin(t * 30f) > 0f ? Game.ExcHoly : Game.ExcGold;
            Arc(c, r + 13f, 3f, ring.WithAlpha(0.18f), 40);
            Arc(c, r + 13f, 2.5f, ring.WithAlpha(0.9f), 40, -MathX.Pi / 2f, MathX.Tau * left);
            int n = Game.ExcSwingNo(p), done = n % Game.ExcJudgeEvery;
            if (Game.ExcIsJudge(n) && p.ExcSwing - n > 0f)
                done = Game.ExcJudgeEvery;
            for (int i = 0; i < Game.ExcJudgeEvery; i++)
            {
                float a = MathX.Pi / 2f + (i - (Game.ExcJudgeEvery - 1) * 0.5f) * 0.24f;  // a short row under you
                V2 at = c + V2.FromAngle(a) * (r + 22f);
                bool lit = i < done, last = i == Game.ExcJudgeEvery - 1;
                float s = lit ? 3.4f : 2.4f;
                Quad(at + new V2(0f, -s), at + new V2(s, 0f), at + new V2(0f, s), at + new V2(-s, 0f),
                    (lit ? (last ? Game.ExcHoly : Game.ExcGold) : Game.ExcGold.Mix(new Rgba(0f, 0f, 0f), 0.55f)).WithAlpha(lit ? 1f : 0.6f));
            }
        }

        /// WorldRenderer, after the player's body: the swing's fan, then the sword, then motes off it.
        void DrawExcaliburOver(Player p, float t)
        {
            if (!Game.ExcOn(p) || LaunchHides(p))
                return;
            V2 c = p.Disp;
            ExcPose(p, out float ang, out float len, out float flash);
            int n = Game.ExcSwingNo(p);
            float k = p.ExcSwing - n;
            if (n > 0 && k > 0f && !Game.ExcIsJudge(n) && p.ExcAge >= Game.ExcDraw)  // the fan the blade sweeps
            {
                float half = Game.ExcArcDeg * 0.5f * MathX.Pi / 180f, side = n % 2 == 1 ? 1f : -1f, from = p.Aim - side * half;
                const int segs = 14;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = Mathf.Lerp(from, ang, (float)i / segs), a1 = Mathf.Lerp(from, ang, (float)(i + 1) / segs);
                    float al = 0.05f + 0.3f * (i + 1f) / segs;
                    V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                    Quad(c + d0 * 30f, c + d0 * Game.ExcReach, c + d1 * Game.ExcReach, c + d1 * 30f, Game.ExcGold.WithAlpha(al * flash * 0.6f));
                }
                Arc(c, Game.ExcReach, 3f, Game.ExcHoly.WithAlpha(0.7f * flash), 30, Mathf.Min(from, ang), Mathf.Abs(ang - from));
            }
            V2 hilt = c + V2.FromAngle(ang) * (p.Def.BodyR - 4f);
            if (Game.ExcIsJudge(n) && k > 0.45f && fxOn)  // the Judgement's flash down the aim
            {
                V2 X = V2.FromAngle(p.Aim);
                Line(c, c + X * (Game.ExcSwordLen * 1.4f), 26f * flash, Game.ExcHoly.WithAlpha(0.35f * flash));
                GlowDisc(c + X * (Game.ExcSwordLen * 0.9f), 60f, Game.ExcHoly, 0.6f * flash);
            }
            DrawExcSword(hilt, ang, len, 0.75f + 0.25f * Mathf.Sin(t * 9f) + flash * 0.5f, t, 1f);
            if (fxOn)  // motes drifting off the blade
                for (int i = 0; i < 6; i++)
                {
                    float ph = Mathf.Repeat(t * 0.8f + ExcH(i + p.Id * 3), 1f), along = ExcH(i * 2.3f + 1f);
                    V2 at = hilt + V2.FromAngle(ang) * (len * (0.2f + 0.75f * along)) + new V2(Mathf.Sin(i * 5f + t) * 6f, -ph * 26f);
                    Circle(at, 1.6f, Game.ExcHoly.WithAlpha(0.8f * Mathf.Sin(ph * MathX.Pi)), 6);
                }
        }

        /// One crescent band: `span` wide round `mid`, radius r, thickness th (thinning to the tips).
        void ExcCrescent(V2 c, float mid, float span, float r, float th, Rgba col)
        {
            const int segs = 22;
            for (int i = 0; i < segs; i++)
            {
                float u0 = (float)i / segs, u1 = (float)(i + 1) / segs;
                float a0 = mid + (u0 - 0.5f) * span, a1 = mid + (u1 - 0.5f) * span;
                float t0 = th * Mathf.Sin(MathX.Pi * u0) * 0.5f, t1 = th * Mathf.Sin(MathX.Pi * u1) * 0.5f;
                V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                Quad(c + d0 * (r - t0), c + d0 * (r + t0), c + d1 * (r + t1), c + d1 * (r - t1), col);
            }
        }

        /// FxKind.Excalibur: a shockwave, the burst at the end, or the shaft of light on the draw.
        void DrawExcalibur(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius);
            float q = Mathf.Clamp01(fx.T / Mathf.Max(0.05f, fx.Dur));
            if (code == Game.ExcFxDraw)
            {
                Player owner;
                V2 c = game.Players.TryGetValue((int)fx.Pos2.X, out owner) ? owner.Disp : fx.Pos;
                float w = 30f * (1f - 0.6f * q), fade = 1f - q * q;
                for (int i = 0; i < 6; i++)  // the shaft, brighter toward you
                {
                    float y0 = -620f + i * 100f, y1 = y0 + 100f, sa = (0.1f + 0.12f * i) * fade;
                    Quad(c + new V2(-w, y0), c + new V2(w, y0), c + new V2(w, y1), c + new V2(-w, y1), Game.ExcHoly.WithAlpha(sa));
                }
                Quad(c + new V2(-w * 0.35f, -620f), c + new V2(w * 0.35f, -620f), c + new V2(w * 0.35f, 0f), c + new V2(-w * 0.35f, 0f), Rgba.White.WithAlpha(0.5f * fade));
                if (fxOn)
                    GlowDisc(c, 70f, Game.ExcHoly, 0.6f * fade);
                return;
            }
            if (code == Game.ExcFxBurst)
            {
                float e = EaseOut3(q), R = Game.ExcBurstRadius * e, fade = 1f - q;
                if (fxOn)
                    GlowDisc(fx.Pos, Game.ExcBurstRadius * 0.8f, Game.ExcHoly, 0.7f * fade);
                Arc(fx.Pos, R, 10f * fade + 1f, Game.ExcGold.WithAlpha(0.5f * fade), 48);
                Arc(fx.Pos, R, 3f, Rgba.White.WithAlpha(0.95f * fade), 48);
                for (int i = 0; i < 14; i++)  // shards of the blade flying out
                {
                    V2 d = V2.FromAngle(MathX.Tau * i / 14f + ExcH(i + fx.Pos.X) * 0.4f);
                    float s = R * (0.6f + 0.4f * ExcH(i * 1.7f + fx.Pos.Y));
                    Line(fx.Pos + d * s, fx.Pos + d * (s + 18f), 3f, (i % 2 == 0 ? Game.ExcGold : Game.ExcHoly).WithAlpha(fade));
                }
                return;
            }
            bool judge = code == Game.ExcFxJudge;
            float range = fx.Pos2.DistanceTo(fx.Pos), aim = (fx.Pos2 - fx.Pos).Angle;
            float life = (range - Game.ExcWaveFrom) / Game.ExcWaveSpeed, Rw = Game.ExcWaveR(fx.T, range);
            float al = fx.T <= life ? 1f : Mathf.Clamp01(1f - (fx.T - life) / Game.ExcWaveTail);
            al *= 1f - 0.35f * Mathf.Clamp01((Rw - Game.ExcWaveFrom) / range);
            float span = 2f * (judge ? Game.ExcJudgeHalf : Game.ExcWaveHalf), th = judge ? 34f : 20f;
            ExcCrescent(fx.Pos, aim, span * 0.9f, Rw - th * 1.1f, th * 0.8f, Game.ExcGold.WithAlpha(0.14f * al));  // the trail
            ExcCrescent(fx.Pos, aim, span, Rw, th * 1.9f, Game.ExcGold.WithAlpha(0.28f * al));
            ExcCrescent(fx.Pos, aim, span, Rw, th, Game.ExcHoly.WithAlpha(0.75f * al));
            ExcCrescent(fx.Pos, aim, span * 0.94f, Rw + th * 0.1f, th * 0.35f, Rgba.White.WithAlpha(0.95f * al));
            if (judge)
                Arc(fx.Pos, Rw + th * 0.9f, 3f, Game.ExcGem.WithAlpha(0.7f * al), 30, aim - span * 0.4f, span * 0.8f);
        }
    }
}
