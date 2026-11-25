using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Static Bubble's look (Core/StaticBubble.cs; from its mockup, claude.ai artifact "Static Bubble"). Drawing only, from the
    /// replicated BubT, BubR, BubCharge, RushT and RushLen and FxKind.StaticBurst.
    ///   - the bubble: a clear body brightening to its rim, a jittery shell drawn three times (a wide glow, the colour, a white
    ///     core) re-struck 20 times a second, arcs from you to its inside and sparks crawling over its surface; it runs from
    ///     cyan to white-violet as it fills, shaking harder and pulsing faster near full; a gold arc round it is the charge,
    ///     a thin grey one the time left;
    ///   - the burst (FxKind.StaticBurst): a flash, a ring rolling out to its reach (a second gold one when full) and bolts
    ///     forking out along it; a full one shakes your screen;
    ///   - Static Rush: a gold ring draining round each player who has it, gold sparks flying off them.
    /// Feed arcs are FxKind.RodArc (LightningRodLook.cs). With Visual effects off: plain rings.
    public partial class ArenaApp
    {
        readonly HashSet<Effect> bubShook = new HashSet<Effect>();

        /// Its colour for a fill: cyan, running white-violet as it fills, flickering near full.
        static Rgba BubColour(float fill, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (6f + 22f * fill));
            float k = Mathf.Clamp01(fill * 1.1f + (fill > 0.75f ? pulse * 0.25f : 0f));
            return Game.BubCyan.Mix(Game.BubViolet, k);
        }

        /// WorldRenderer.DrawPlayerAuras: the bubble round its caster, and Static Rush on anyone who has it.
        void DrawStaticBubble(Player p, float t)
        {
            if (Game.BubOn(p) && !LaunchHides(p))
                DrawBubShell(p.Disp, p.BubR, Game.BubFill(p), Game.BubTime - p.BubT, p.BubT, p.Id, t);
            if (p.RushT > 0f && !p.Downed && !LaunchHides(p))
                DrawRush(p, t);
        }

        void DrawBubShell(V2 c, float radius, float fill, float age, float left, int id, float t)
        {
            float open = EaseOut3(Mathf.Clamp01(age / 0.25f)), R = radius * open;
            if (R < 2f)
                return;
            Rgba col = BubColour(fill, t);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (6f + 22f * fill));
            float time = Mathf.Clamp01(left / Game.BubTime);
            if (!fxOn)
            {
                Arc(c, R, 2f, col.WithAlpha(0.7f), 40);
                Arc(c, R + 9f, 2.5f, Game.StaticRushGold.WithAlpha(0.9f), 40, -MathX.Pi / 2f, MathX.Tau * fill);
                return;
            }
            // the body: clear in the middle, brighter toward the rim
            Circle(c, R, col.WithAlpha(0.03f), 40);
            Arc(c, R - 7f, 14f, col.WithAlpha(0.06f + 0.06f * fill), 48);
            Arc(c, R - 2f, 5f, col.WithAlpha(0.2f + 0.1f * pulse), 48);
            // arcs from you to its inside
            int strike = Mathf.FloorToInt(t * 16f);
            int arcs = 3 + Mathf.RoundToInt(fill * 4f);
            for (int j = 0; j < arcs; j++)
            {
                int seed = strike * 7 + j * 13 + id * 31;
                V2 d = V2.FromAngle(RodHash(seed) * MathX.Tau);
                float l = R * (0.55f + 0.43f * RodHash(seed + 5));
                Jag(c, c + d * l, 1.2f, col.WithAlpha(0.55f), seed, 6, l * 0.18f, true);
            }
            // the shell: jittery, harder as it fills
            const int N = 56;
            int st = Mathf.FloorToInt(t * 20f);
            float jit = 1.2f + 4f * fill + (fill > 0.75f ? 2f * pulse : 0f);
            V2 first = default(V2), prev = default(V2);
            for (int i = 0; i <= N; i++)
            {
                int ii = i % N;
                float r = R + (RodHash(st * N + ii + id * 977) - 0.5f) * 2f * jit;
                V2 pt = c + V2.FromAngle(MathX.Tau * ii / N) * r;
                if (i == 0)
                    first = pt;
                else
                {
                    V2 to = i == N ? first : pt;
                    Line(prev, to, 7f, col.WithAlpha(0.16f));
                    Line(prev, to, 2.2f, col.WithAlpha(0.55f));
                    Line(prev, to, 1f, Game.BoltWhite.WithAlpha(0.75f));
                }
                prev = pt;
            }
            // sparks crawling over its surface
            int crawl = 4 + Mathf.RoundToInt(fill * 6f);
            for (int j = 0; j < crawl; j++)
            {
                int seed = st * 5 + j * 17 + id * 13;
                float a = RodHash(seed) * MathX.Tau, a2 = a + 0.2f + 0.3f * RodHash(seed + 3);
                Jag(c + V2.FromAngle(a) * R, c + V2.FromAngle(a2) * R, 1f, Game.BoltWhite.WithAlpha(0.8f), seed, 4, 5f, false);
            }
            // the charge (gold) and the time left (grey)
            Arc(c, R + 9f, 2.5f, Game.StaticRushGold.WithAlpha(0.15f), 48);
            Arc(c, R + 9f, 2.5f, Game.StaticRushGold.WithAlpha(0.95f), 48, -MathX.Pi / 2f, MathX.Tau * fill);
            Arc(c, R + 14f, 1.2f, new Rgba(0.63f, 0.71f, 0.82f, 0.45f), 48, -MathX.Pi / 2f, MathX.Tau * time);
        }

        void DrawRush(Player p, float t)
        {
            float r = p.Def.BodyR, k = Mathf.Clamp01(p.RushT / Mathf.Max(0.01f, p.RushLen));
            float fade = p.RushT < 1.5f && Mathf.Sin(t * 30f) > 0.3f ? 0.5f : 1f;
            V2 c = p.Disp;
            Arc(c, r + 12f, 3f, Game.StaticRushGold.WithAlpha(0.18f * fade), 32);
            Arc(c, r + 12f, 3f, Game.StaticRushGold.WithAlpha(fade), 32, -MathX.Pi / 2f, MathX.Tau * k);
            if (!fxOn)
                return;
            int st = Mathf.FloorToInt(t * 20f);
            for (int j = 0; j < 4; j++)  // gold sparks flying off
            {
                float ph = Mathf.Repeat(t * 3f + j * 0.25f + p.Id * 0.17f, 1f);
                V2 d = V2.FromAngle(RodHash(st / 4 + j * 19 + p.Id) * MathX.Tau);
                Circle(c + d * (r + 8f + ph * 20f), 1.8f * (1f - ph), Game.StaticRushGold.WithAlpha(fade * (1f - ph)), 6);
            }
        }

        /// WorldEffects.cs: FxKind.StaticBurst.
        void DrawStaticBurstFx(Effect fx)
        {
            V2 c = fx.Pos;
            float R = fx.Radius, f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), fade = 1f - f;
            bool full = fx.Pos2.Y > 0.5f;
            Rgba col = full ? Game.BubViolet : Game.BubCyan;
            float roll = EaseOut3(f);
            if (!fxOn)
            {
                Arc(c, Mathf.Lerp(20f, R, roll), 2.5f, col.WithAlpha(fade), 40);
                return;
            }
            if (full && (int)fx.Pos2.X == game.MyId && bubShook.Add(fx))
                Shake(0.5f);
            if (bubShook.Count > 32)
                bubShook.Clear();
            Circle(c, R * EaseOut3(Mathf.Min(1f, f * 2.5f)), col.WithAlpha(0.22f * fade * fade), 48);
            Arc(c, Mathf.Lerp(20f, R, roll), 2f + 6f * fade, col.WithAlpha(0.9f * fade), 56);
            if (full)
            {
                float g = Mathf.Clamp01((fx.T - 0.08f) / Mathf.Max(0.01f, fx.Dur - 0.08f));
                if (g > 0f)
                    Arc(c, Mathf.Lerp(10f, R * 0.7f, EaseOut3(g)), 1.5f + 4f * (1f - g), Game.StaticRushGold.WithAlpha(0.85f * (1f - g)), 48);
            }
            if (f < 0.6f)
            {
                float k = 1f - f / 0.6f;
                int n = full ? 16 : 8, seed0 = Mathf.RoundToInt(c.X * 3f + c.Y * 7f) + Mathf.FloorToInt(fx.T * 30f);
                for (int i = 0; i < n; i++)
                {
                    int seed = seed0 * 17 + i;
                    float a = MathX.Tau * i / n + (RodHash(seed) - 0.5f) * 0.3f, l = R * (0.7f + 0.35f * RodHash(seed + 3)) * Mathf.Lerp(0.5f, 1f, roll);
                    V2 end = c + V2.FromAngle(a) * l;
                    Jag(c, end, 3.5f, col.WithAlpha(0.3f * k), seed, 7, l * 0.2f, true);
                    Jag(c, end, 1.3f, Game.BoltWhite.WithAlpha(k), seed, 7, l * 0.2f, true);
                }
            }
        }

        /// Juice.DrawGlowPass: the bubble's light, the burst's flash and Static Rush's.
        void DrawStaticBubbleGlow()
        {
            float t = game.AnimT;
            foreach (var p in game.Players.Values)
            {
                if (LaunchHides(p))
                    continue;
                if (Game.BubOn(p))
                    GlowDisc(p.Disp, p.BubR * 1.3f, BubColour(Game.BubFill(p), t), 0.16f + 0.12f * Game.BubFill(p));
                if (p.RushT > 0f && !p.Downed)
                    GlowDisc(p.Disp, p.Def.BodyR * 2.6f, Game.StaticRushGold, 0.22f + 0.05f * Mathf.Sin(t * 9f));
            }
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.StaticBurst && fx.T < 0.35f)
                    GlowDisc(fx.Pos, fx.Radius, fx.Pos2.Y > 0.5f ? Game.BubViolet : Game.BubCyan, 0.6f * (1f - fx.T / 0.35f));
        }
    }
}
