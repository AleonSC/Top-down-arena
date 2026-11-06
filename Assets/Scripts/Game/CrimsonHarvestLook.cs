using UnityEngine;

namespace Arena
{
    /// Crimson Harvest (Core/CrimsonHarvest.cs), from Docs/Mockups/hemomancer-ult-rework.html: the tendrils of look B (Whirlpool: faint dotted
    /// threads from the ball to every enemy, with big droplets running back along them, each with a short trail) and the ball of look A
    /// (Membrane: a wobbling skin with a heartbeat, veins, a glint and a rim). While the ball is up (Player.HarvestHold > 0) it floats over its
    /// owner and swells with Player.HarvestBlood; in the hold a countdown ring runs round it, and the owner's own screen shows where it
    /// would land. The throw is FxKind.HarvestThrow, drawn from its age alone: the ball's flight (Pos to Pos2), then shock rings, a flash,
    /// flung drops and a blood stain.
    public partial class ArenaApp
    {
        static readonly Rgba HvCore = new Rgba(0.23f, 0.02f, 0.06f), HvMid = new Rgba(0.86f, 0.17f, 0.27f), HvRim = new Rgba(1f, 0.35f, 0.44f);
        static readonly Rgba HvThread = new Rgba(1f, 0.48f, 0.55f), HvDrop = new Rgba(1f, 0.29f, 0.39f), HvAmber = new Rgba(1f, 0.72f, 0.29f);
        static readonly Rgba HvStain = new Rgba(0.47f, 0.04f, 0.11f);

        /// Where the ball floats: over its owner, bobbing a little.
        static V2 HarvestBallAt(Player p, float t) => p.Disp + new V2(0f, -Game.HarvestLift + Mathf.Sin(t * 2.2f) * 8f);

        static V2 HvBez(V2 a, V2 ctrl, V2 b, float u)
        {
            float v = 1f - u;
            return a * (v * v) + ctrl * (2f * v * u) + b * (u * u);
        }

        static float HvSmooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// The ball's skin: the point `i` of 40 round a wobbling circle.
        static V2 HvSkinPt(V2 c, float R, float t, int i)
        {
            float a = MathX.Tau * i / 40f, rr = R * (1f + 0.035f * Mathf.Sin(a * 5f + t * 3f) + 0.025f * Mathf.Sin(a * 8f - t * 4.3f));
            return c + V2.FromAngle(a) * rr;
        }

        void HvBlob(V2 c, float R, float t, Rgba col)
        {
            SetColor(col);
            V2 prev = HvSkinPt(c, R, t, 0);
            for (int i = 1; i <= 40; i++)
            {
                V2 next = HvSkinPt(c, R, t, i);
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// Look A, the Membrane: a dark core under lighter layers pushed toward the glint, a bright rim, faint veins, a heartbeat.
        void DrawHarvestBall(V2 c, float R, float t, float heat)
        {
            R *= 1f + 0.05f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 5.2f)), 3f);
            GlowDisc(c, R * 2.1f, HvRim, 0.28f + 0.2f * heat);
            for (int k = 0; k < 5; k++)
            {
                float f = k / 4f;
                var col = new Rgba(HvCore.R + (HvMid.R - HvCore.R) * f, HvCore.G + (HvMid.G - HvCore.G) * f, HvCore.B + (HvMid.B - HvCore.B) * f);
                HvBlob(c + new V2(-0.07f, -0.08f) * (R * k * 0.75f), R * (1f - 0.17f * k), t, col);
            }
            V2 prev = HvSkinPt(c, R, t, 0);
            for (int i = 1; i <= 40; i++)
            {
                V2 next = HvSkinPt(c, R, t, i);
                Line(prev, next, 3.5f, HvRim);
                prev = next;
            }
            for (int k = 0; k < 6; k++)  // veins under the skin
            {
                float a = k * 1.05f;
                V2 m = c + V2.FromAngle(a + 0.8f + 0.3f * Mathf.Sin(t + k)) * (R * 0.5f), e = c + V2.FromAngle(a + 0.3f) * (R * 0.88f);
                Rgba vc = new Rgba(1f, 0.59f, 0.65f, 0.25f);
                Line(c + V2.FromAngle(a) * (R * 0.1f), m, 2f, vc);
                Line(m, e, 2f, vc);
            }
            Arc(c, R * 0.72f, Mathf.Max(3f, R * 0.08f), new Rgba(1f, 1f, 1f, 0.7f), 10, 3.5f, 0.9f);  // the glint
        }

        /// Called for each player: the ball, its tendrils and droplets, and the owner's aim ring.
        void DrawHarvest(Player p, float t)
        {
            if (p.Downed || !Game.HarvestActive(p))
                return;
            float el = p.HarvestClock, R = Game.HarvestBallR(p.HarvestBlood);
            bool holding = el >= Game.HarvestRip;
            V2 ball = HarvestBallAt(p, t);
            Circle(p.Disp, R * 0.7f, new Rgba(0f, 0f, 0f, 0.25f), 16);  // its shadow on the floor
            if (!holding)
                DrawHarvestThreads(ball, el, t);
            else if (p.Id == game.MyId)
                DrawHarvestAim(p, ball);
            DrawHarvestBall(ball, R, t, holding ? Mathf.Clamp01((el - Game.HarvestRip) / Mathf.Max(0.1f, p.HarvestHold)) : Mathf.Clamp01(p.HarvestBlood / 30f) * 0.6f);
            if (holding)  // the time left to throw it, a ring from the top
            {
                float left = Mathf.Clamp01(Game.HarvestLeft(p) / Mathf.Max(0.1f, p.HarvestHold));
                Arc(ball, R + 22f, 6f, HvAmber.WithAlpha(0.95f), 48, -MathX.Pi / 2f, MathX.Tau * left);
            }
        }

        /// Look B's tendrils: a faint dotted thread from the ball to each enemy (reaching out, then drawing back), and ten big drops
        /// with trails running down it to the ball. The timing is Game.HarvestStart, so every screen agrees with the host.
        void DrawHarvestThreads(V2 ball, float el, float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 to = e.Disp;
                float start = Game.HarvestStart(e.Pos.DistanceTo(ball), e.Id);
                float ext = Mathf.Clamp01((el - start) / 0.3f), ret = Mathf.Clamp01((el - (start + 1.1f)) / 0.3f), reach = ext * (1f - ret);
                float w = e.Def.IsBoss ? 2f : e.Def.Heavy ? 1.6f : 1f;
                V2 d = to - ball;
                float len = Mathf.Max(1f, d.Length), side = e.Id % 2 == 0 ? -1f : 1f;
                V2 ctrl = (ball + to) * 0.5f + d.Orthogonal() * ((side * len * 0.2f + Mathf.Sin(t * 3f + e.Id * 0.7f) * 16f) / len);
                if (reach > 0f)
                {
                    int dots = Mathf.Clamp((int)(len * reach / 16f), 1, 60);
                    for (int i = 0; i <= dots; i++)
                        Circle(HvBez(ball, ctrl, to, reach * i / dots), 2.2f * w, HvThread.WithAlpha(0.55f), 6);
                }
                for (int i = 0; i < Game.HarvestDrops; i++)
                {
                    float t0 = start + 0.3f + i * 0.08f, dur = 0.45f + CavHash(e.Id * 13 + i) * 0.1f, q = (el - t0) / dur;
                    if (q < 0f || q >= 1f)
                        continue;
                    float sz = (e.Def.Heavy || e.Def.IsBoss ? 6.6f : 4.6f) * 1.25f, eq = HvSmooth(q);
                    for (int k = 4; k >= 0; k--)
                    {
                        float u = 1f - Mathf.Clamp01(eq - k * 0.035f);
                        Circle(HvBez(ball, ctrl, to, u), sz * (1f - k * 0.12f), HvDrop.WithAlpha(k == 0 ? 1f : 0.35f - k * 0.07f), 8);
                    }
                }
            }
        }

        /// The owner's aim: where the ball would land, the blast and its full-damage core, and a ring round each enemy it would reach.
        void DrawHarvestAim(Player p, V2 ball)
        {
            V2 at = game.HarvestAimPoint(p, p.AimAt);
            float R = Game.HarvestRadius(p.HarvestBlood);
            DrawHarvestReticle(at, R, ball, 1f);
            foreach (var e in game.Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Disp.DistanceTo(at) <= R + e.Radius)
                    Arc(e.Disp, e.Radius + 6f, 3f, HvAmber.WithAlpha(0.9f), 16);
        }

        void DrawHarvestReticle(V2 at, float R, V2 from, float a)
        {
            Circle(at, R, new Rgba(1f, 0.24f, 0.35f, 0.08f * a), 48);
            for (int k = 0; k < 40; k += 2)  // the dashed edge
                Arc(at, R, 3f, new Rgba(1f, 0.47f, 0.55f, 0.85f * a), 3, MathX.Tau * k / 40f, MathX.Tau / 40f);
            Arc(at, R * 0.5f, 2.5f, new Rgba(1f, 0.82f, 0.84f, 0.55f * a), 40);  // the core
            Line(at + new V2(-14f, 0f), at + new V2(14f, 0f), 2.5f, new Rgba(1f, 0.82f, 0.84f, 0.7f * a));
            Line(at + new V2(0f, -14f), at + new V2(0f, 14f), 2.5f, new Rgba(1f, 0.82f, 0.84f, 0.7f * a));
            V2 ctrl = new V2((from.X + at.X) * 0.5f, Mathf.Min(from.Y, at.Y) - 140f);
            for (int i = 1; i < 24; i += 2)  // the lob's path, dotted
                Circle(HvBez(from, ctrl, at, i / 24f), 2.2f, new Rgba(1f, 0.82f, 0.84f, 0.45f * a), 6);
        }

        /// FxKind.HarvestThrow: Pos = where the thrower stood, Pos2 = where it lands, Radius = the blood in it.
        void DrawHarvestThrowFx(Effect fx)
        {
            float age = fx.T, R = Game.HarvestRadius(fx.Radius), ballR = Game.HarvestBallR(fx.Radius);
            V2 land = fx.Pos2;
            if (age < Game.HarvestThrowT)
            {
                float p = age / Game.HarvestThrowT;
                V2 g = V2.Lerp(fx.Pos, land, p);
                V2 at = g + new V2(0f, -Game.HarvestLift * (1f - p) - 440f * p * (1f - p));
                DrawHarvestReticle(land, R, fx.Pos + new V2(0f, -Game.HarvestLift), 0.8f);
                Circle(g, ballR * 0.8f, new Rgba(0f, 0f, 0f, 0.3f), 16);
                for (int k = 6; k >= 1; k--)  // a trail of drops
                {
                    float q = Mathf.Max(0f, p - k * 0.04f);
                    V2 gq = V2.Lerp(fx.Pos, land, q);
                    Circle(gq + new V2(0f, -Game.HarvestLift * (1f - q) - 440f * q * (1f - q)), Mathf.Max(3f, ballR * 0.3f - k * 1.5f), HvDrop.WithAlpha(0.5f - k * 0.07f), 8);
                }
                DrawHarvestBall(at, ballR * (1f + 0.12f * Mathf.Sin(p * MathX.Pi)), age * 1.5f, 1f);
                return;
            }
            float a = age - Game.HarvestThrowT;
            if (a < 0.3f)  // the flash
                GlowDisc(land, R * 0.8f, new Rgba(1f, 0.9f, 0.9f), 0.7f * (1f - a / 0.3f));
            for (int k = 0; k < 3; k++)  // three shock rings
            {
                float q = (a - k * 0.12f) / 0.55f;
                if (q <= 0f || q >= 1f)
                    continue;
                float e = 1f - (1f - q) * (1f - q) * (1f - q);
                Arc(land, R * e, 2f + 12f * (1f - q), HvRim.WithAlpha(0.9f * (1f - q)), 64);
            }
            if (a < 0.7f)  // drops flung out
                for (int k = 0; k < 28; k++)
                {
                    float ang = CavHash(k * 3) * MathX.Tau, dist = R * (0.35f + 0.65f * CavHash(k * 7 + 1)) * (1f - Mathf.Pow(1f - a / 0.7f, 2f));
                    Circle(land + V2.FromAngle(ang) * dist, 4f * (1f - a / 0.7f) + 1f, HvDrop.WithAlpha(1f - a / 0.7f), 6);
                }
            float al = Mathf.Clamp01(a / 0.2f) * Mathf.Clamp01((4.5f - a) / 1.2f);  // the stain
            if (al > 0.01f)
            {
                for (int k = 0; k < 9; k++)
                {
                    float ang = k * 0.7f + fx.Pos2.X, dd = R * 0.55f * CavHash(k);
                    Circle(land + V2.FromAngle(ang) * dd, R * (0.18f + 0.14f * CavHash(k + 9)), HvStain.WithAlpha(0.5f * al), 20);
                }
                Circle(land, R * 0.43f, HvStain.WithAlpha(0.5f * al), 28);
                for (int k = 0; k < 36; k += 2)
                    Arc(land, R * 0.85f, 3f, HvRim.WithAlpha(0.4f * al), 3, MathX.Tau * k / 36f, MathX.Tau / 36f);
            }
        }
    }
}
