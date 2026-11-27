using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Iron Armadillo (Core/IronArmadillo.cs), the look of the playable mockup: spirit arcs close in round you and a giant
    /// armadillo shell curls up in your place (Game.ArmCurl s); rolling, the ball's banded plates roll over the top along where it
    /// goes (each band a strip of the sphere: drawn from how far it has rolled), with a teal spirit rim, a dust trail and speed
    /// lines when it's fast. Your own ball shows the rev: a gold dashed arrow along the launch and a ring filling round it. On the
    /// uncurl the ball shrinks back into you. A thin bar over the ball is the time left. Replicated state is Player.ArmT and
    /// ArmAge; how far it rolled, its heading and speed are read here from how it moves. Visual effects off: no glow, dust or lines.
    public partial class ArenaApp
    {
        class ArmVis
        {
            public bool Up;
            public float OnAt = -9f, OffAt = -9f;
            public V2 Last, OffAt2;
            public float Dist, Head, Spd, DustT;
            public readonly List<ArmDust> Dust = new List<ArmDust>();
        }

        struct ArmDust { public V2 At; public float Born, Size; }

        readonly Dictionary<int, ArmVis> armVis = new Dictionary<int, ArmVis>();
        const float ArmDustLife = 0.8f, ArmDustGap = 0.035f, ArmHeadTurn = 9f;
        const int ArmBands = 7;  // plates over half the ball
        static readonly Rgba ArmPlate = new Rgba(0.79f, 0.71f, 0.54f), ArmPlateDark = new Rgba(0.63f, 0.56f, 0.41f),
            ArmCore = new Rgba(0.23f, 0.2f, 0.15f), ArmGap = new Rgba(0.12f, 0.09f, 0.05f), ArmLite = new Rgba(1f, 0.93f, 0.78f),
            ArmDustCol = new Rgba(0.59f, 0.51f, 0.39f);

        static Rgba ArmShade(Rgba c, float k, float a) => new Rgba(c.R * k, c.G * k, c.B * k, c.A * a);

        ArmVis ArmState(Player p, float t)
        {
            bool on = Game.ArmOn(p);
            if (!armVis.TryGetValue(p.Id, out var v))
            {
                if (!on)
                    return null;
                armVis[p.Id] = v = new ArmVis { Up = true, OnAt = t - p.ArmAge, Last = p.Disp, Head = p.Aim };
            }
            if (on && !v.Up)
            {
                v.OnAt = t - Mathf.Clamp(p.ArmAge, 0f, 0.2f);
                v.Last = p.Disp;
                v.Head = p.Aim;
                v.Dist = 0f;
                v.Spd = 0f;
            }
            else if (!on && v.Up)
            {
                v.OffAt = t;
                v.OffAt2 = p.Disp;
            }
            v.Up = on;
            float dt = Mathf.Max(0f, Time.deltaTime);
            if (on && dt > 0f)
            {
                V2 moved = p.Disp - v.Last;
                float len = moved.Length;
                if (len < 300f)  // (not a teleport)
                {
                    v.Dist += len;
                    v.Spd = Mathf.MoveTowards(v.Spd, len / dt, 2400f * dt);
                    if (p == game.Me)
                        v.Head = game.ArmHeading;
                    else if (len > 30f * dt)
                    {
                        float want = Mathf.Atan2(moved.Y, moved.X);
                        float d = Mathf.Repeat(want - v.Head + MathX.Pi, MathX.Tau) - MathX.Pi;
                        v.Head += Mathf.Clamp(d, -ArmHeadTurn * dt, ArmHeadTurn * dt);
                    }
                    if (fxOn && p.ArmAge >= Game.ArmCurl && v.Spd > 150f && (v.DustT -= dt) <= 0f)
                    {
                        v.DustT = ArmDustGap;
                        V2 back = p.Disp - V2.FromAngle(v.Head) * (Game.ArmR * 0.75f);
                        v.Dust.Add(new ArmDust { At = back + new V2(Mathf.Repeat(v.Dist * 0.37f, 40f) - 20f, Mathf.Repeat(v.Dist * 0.71f, 40f) - 20f), Born = t, Size = 8f + Mathf.Repeat(v.Dist * 0.13f, 8f) });  // (scattered by how far it rolled)
                    }
                }
            }
            v.Last = p.Disp;
            v.Dust.RemoveAll(d => t - d.Born > ArmDustLife);
            return v;
        }

        /// From DrawPlayer, before the model: the ball (true: drawn in place of the model), or while it curls or uncurls the
        /// shell growing over you or shrinking back (false: you are drawn too).
        bool DrawIronArmadillo(Player p, float t, float alpha)
        {
            if (p.ArmT <= 0f && !armVis.ContainsKey(p.Id))
                return false;
            var v = ArmState(p, t);
            if (v == null)
                return false;
            foreach (var d in v.Dust)  // the dust trail, puffing out and fading
            {
                float k = (t - d.Born) / ArmDustLife;
                Circle(d.At, d.Size * (1f + k), ArmDustCol.WithAlpha(0.28f * (1f - k) * alpha), 14);
            }
            if (!v.Up)
            {
                float k = (t - v.OffAt) / Game.ArmUncurl;
                if (k < 1f)  // the uncurl: the shell shrinks back into you
                    DrawArmBall(p.Disp, Mathf.Lerp(Game.ArmR, p.Def.BodyR, k), v.Head, v.Dist, alpha * (1f - k * k), false);
                else if (v.Dust.Count == 0)
                    armVis.Remove(p.Id);
                return false;
            }
            float since = t - v.OnAt;
            if (since < Game.ArmCurl)  // the curl: spirit arcs close in, then the shell swells round you
            {
                float k = since / Game.ArmCurl;
                if (fxOn)
                    for (int i = 0; i < 3; i++)
                        Arc(p.Disp, Game.ArmR + 50f * (1f - k) + i * 12f, 3f, Game.ArmSpirit.WithAlpha(0.7f * (1f - k) * alpha), 24, k * 6f + i * 2.1f, 2.2f);
                if (k < 0.35f)
                    return false;
                float g = ArmBack((k - 0.35f) / 0.65f);
                DrawArmBall(p.Disp, Mathf.Lerp(p.Def.BodyR, Game.ArmR, g), v.Head, v.Dist, alpha, true);
                return true;
            }
            if (fxOn && v.Spd > 330f)  // speed lines behind it (gold while launched)
            {
                V2.Axes(v.Head, out V2 fw, out V2 side);
                bool boosted = v.Spd > Game.ArmTop + 40f;
                var lc = (boosted ? Game.ArmGold : new Rgba(0.62f, 0.76f, 1f)).WithAlpha((boosted ? 0.5f : 0.35f) * alpha);
                for (int i = -3; i <= 3; i++)
                {
                    float l = 50f + Mathf.Repeat(i * 37f + t * 125f, 60f);
                    V2 from = p.Disp + side * (i * 20f) - fw * (Game.ArmR + 10f);
                    Line(from, from - fw * l, 2f, lc);
                }
            }
            DrawArmBall(p.Disp, Game.ArmR, v.Head, v.Dist, alpha, true);
            if (p == game.Me && game.ArmRevNow > 0f)  // your rev: the launch arrow and a ring filling round the ball
            {
                float rev = game.ArmRevNow;
                V2 dir = V2.FromAngle(game.ArmHeading);
                float from = Game.ArmR + 8f, to = Game.ArmR + 40f + 140f * rev;
                for (float s = from; s < to; s += 18f)
                    Line(p.Disp + dir * s, p.Disp + dir * Mathf.Min(to, s + 10f), 3f, Game.ArmGold.WithAlpha(0.9f * alpha));
                Arc(p.Disp, Game.ArmR + 12f, 3f, Game.ArmGold.WithAlpha(0.9f * alpha), 40, -MathX.Pi / 2f, MathX.Tau * rev);
            }
            return true;
        }

        static float ArmBack(float x) { x = Mathf.Clamp01(x); const float c = 1.7f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        /// The ball at `c` (radius R) rolling toward `head`, `dist` px rolled so far: plates as strips of the sphere, its seams,
        /// a lit top, a dark edge and the spirit rim.
        void DrawArmBall(V2 c, float R, float head, float dist, float alpha, bool glow)
        {
            if (R < 1f || alpha <= 0.01f)
                return;
            V2.Axes(head, out V2 fw, out V2 side);
            Circle(c + new V2(8f, R * 0.2f), R * 1.02f, new Rgba(0f, 0f, 0f, 0.35f * alpha), 32);  // the shadow
            if (glow && fxOn)
                GlowDisc(c, R * 1.3f, Game.ArmSpirit, 0.3f * alpha);
            Circle(c, R, ArmShade(ArmCore, 1f, alpha), 40);
            // The plates: band k spans the angles a..a+D round the rolling axis; seen from above, x = R sin(angle) along the way
            // it rolls, lit by cos(angle). They come over the top as it rolls, so their order is kept from how far it went.
            const float D = MathX.Pi / ArmBands, H = MathX.Pi / 2f;
            float turns = dist / Game.ArmR;
            float th0 = Mathf.Repeat(turns, D);
            int n = Mathf.FloorToInt(turns / D);
            for (int k = -ArmBands - 2; k <= ArmBands + 2; k++)
            {
                float a = th0 + k * D, b = a + D;
                if (b <= -H || a >= H)
                    continue;
                float a2 = Mathf.Max(a, -H), b2 = Mathf.Min(b, H);
                float x1 = R * Mathf.Sin(a2), x2 = R * Mathf.Sin(b2);
                float h1 = Mathf.Sqrt(Mathf.Max(0f, R * R - x1 * x1)), h2 = Mathf.Sqrt(Mathf.Max(0f, R * R - x2 * x2));
                float sh = 0.42f + 0.58f * Mathf.Cos((a2 + b2) * 0.5f);
                bool odd = ((k - n) % 2 + 2) % 2 == 1;
                SetColor(ArmShade(odd ? ArmPlateDark : ArmPlate, sh, alpha));
                Quad(c + fw * x1 - side * h1, c + fw * x2 - side * h2, c + fw * x2 + side * h2, c + fw * x1 + side * h1);
                float xm = (x1 + x2) * 0.5f, hm = Mathf.Sqrt(Mathf.Max(0f, R * R - xm * xm)), w = (x2 - x1) * 0.22f;
                if (w > 1.2f)  // a row of scutes along the plate
                    for (int i = -1; i <= 2; i++)
                        Circle(c + fw * xm + side * ((i - 0.5f) * 0.42f * hm), Mathf.Min(w, R * 0.07f), ArmLite.WithAlpha(0.2f * sh * alpha), 10);
            }
            for (int k = -ArmBands - 2; k <= ArmBands + 2; k++)  // the seams between plates, with a lit lip behind each
            {
                float a = th0 + k * D;
                if (a <= -H || a >= H)
                    continue;
                float x = R * Mathf.Sin(a), cs = Mathf.Cos(a), h = Mathf.Sqrt(Mathf.Max(0f, R * R - x * x));
                Line(c + fw * x - side * h, c + fw * x + side * h, 1f + 3f * cs * R / Game.ArmR, ArmGap.WithAlpha((0.5f + 0.4f * cs) * alpha));
                Line(c + fw * (x + 2f * cs + 1f) - side * h * 0.98f, c + fw * (x + 2f * cs + 1f) + side * h * 0.98f, 1.2f, ArmLite.WithAlpha(0.35f * cs * alpha));
            }
            float sy = R * 0.86f, sx = Mathf.Sqrt(R * R - sy * sy);  // the side seams where the shell's two halves meet
            Line(c - fw * sx - side * sy, c + fw * sx - side * sy, 3f * R / Game.ArmR, ArmGap.WithAlpha(0.6f * alpha));
            Line(c - fw * sx + side * sy, c + fw * sx + side * sy, 3f * R / Game.ArmR, ArmGap.WithAlpha(0.6f * alpha));
            if (fxOn)
                GlowDisc(c + new V2(-0.35f, -0.4f) * R, R * 0.6f, Rgba.White, 0.22f * alpha);  // the lit top
            Arc(c, R - 4f, 8f, new Rgba(0f, 0f, 0f, 0.3f * alpha), 40);  // the dark edge
            Arc(c, R, 2.5f, (glow ? Game.ArmSpirit : new Rgba(0.36f, 0.29f, 0.2f)).WithAlpha(0.85f * alpha), 40);
        }

        /// The time left, a thin teal bar over the ball.
        void DrawArmBar(Player p)
        {
            if (!Game.ArmRolling(p))
                return;
            float roll = p.ArmT + p.ArmAge - Game.ArmCurl;
            float left = roll > 0f ? Mathf.Clamp01(p.ArmT / roll) : 0f;
            float w = 70f, h = 3f;
            V2 at = p.Disp + new V2(-w / 2f, OverheadRow(h));  // (OverheadStack.cs)
            FillRect(at.X - 1f, at.Y - 1f, w + 2f, h + 2f, new Rgba(0f, 0f, 0f, 0.6f));
            FillRect(at.X, at.Y, w * left, h, (p.ArmT < 2f && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? Game.ArmGold : Game.ArmSpirit).WithAlpha(0.9f));
        }
    }
}
