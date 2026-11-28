using UnityEngine;

namespace Arena
{
    /// Chalice of Life (Core/ChaliceOfLife.cs), the look of Docs/Mockups/chalice-of-life.html, drawn from the effects alone:
    ///   FxKind.ChaliceOfLife (Pos = the chalice): the cup dropping in with a gold shock ring, then a faint gold disc with a turning
    ///     dashed rim (where you drink), the time left as a gold arc, and the gold cup on its stem with a ruby and mint liquid that
    ///     sinks with each sip (the ChaliceSip effects counted against the players in the run), sparkles rising off it
    ///   FxKind.ChaliceSip (Radius = the drinker's id): a mint stream from the cup to the drinker, a ring and motes off them
    ///   DrawChaliceGlow (the glow pass): the cup's warm light and each drinker's flash
    /// Visual effects off drops the sparkles and motes. The chalice fades once everyone has drunk.
    public partial class ArenaApp
    {
        static readonly Rgba ChGold = new Rgba(1f, 0.8f, 0.35f), ChDeep = new Rgba(0.48f, 0.35f, 0.11f), ChBowl = new Rgba(0.72f, 0.53f, 0.17f);
        static readonly Rgba ChPale = new Rgba(1f, 0.95f, 0.77f), ChMint = new Rgba(0.49f, 1f, 0.69f), ChRuby = new Rgba(0.78f, 0.3f, 0.34f);
        const float ChSky = 220f, ChSipT = 1.1f, ChScale = 1.35f;

        /// How many have drunk from the chalice at `at`, and how long ago the last one did.
        int ChaliceSips(V2 at, out float newest)
        {
            int n = 0;
            newest = 999f;
            foreach (var e in game.Effects)
                if (e.Kind == FxKind.ChaliceSip && e.Pos.DistanceSq(at) < 1f)
                {
                    n++;
                    newest = Mathf.Min(newest, e.T);
                }
            return n;
        }

        /// The chalice's strength: up as it lands, fading at its end or once everyone has drunk; `level` is the liquid left (0-1).
        float ChaliceAlpha(Effect fx, out float level)
        {
            float newest;
            int sips = ChaliceSips(fx.Pos, out newest), players = Mathf.Max(1, game.Players.Count);
            level = Mathf.Clamp01(1f - sips / (float)players);
            float a = Mathf.Clamp01((fx.Dur - fx.T) / Game.ChFade);
            if (sips >= players)
                a = Mathf.Min(a, Mathf.Clamp01(1f - (newest - 0.3f) / Game.ChFade));
            return a;
        }

        void DrawChaliceGround(Effect fx)
        {
            if (fx.Kind == FxKind.ChaliceSip)
            {
                DrawChaliceSip(fx);
                return;
            }
            float level, a = ChaliceAlpha(fx, out level);
            if (a <= 0.01f)
                return;
            V2 c = fx.Pos;
            float drop = Mathf.Clamp01(fx.T / Game.ChDropT);
            if (drop >= 1f)
            {
                float ga = a * Mathf.Clamp01((fx.T - Game.ChDropT) / 0.25f);
                Circle(c, Game.ChDrink, ChGold.WithAlpha(0.09f * ga), 40);
                const int dashes = 28;
                float turn = game.AnimT * 0.4f;
                for (int i = 0; i < dashes; i++)
                    Arc(c, Game.ChDrink, 1.6f, ChGold.WithAlpha(0.45f * ga), 3, turn + MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
                float left = Mathf.Clamp01(1f - (fx.T - Game.ChDropT) / (Game.ChLife - Game.ChDropT));
                Arc(c, Game.ChDrink + 7f, 3f, ChGold.WithAlpha(0.85f * ga), 40, -MathX.Pi / 2f, MathX.Tau * left);
                float land = fx.T - Game.ChDropT;
                if (land < 0.5f)  // the shock ring as it lands
                    Arc(c, 14f + land / 0.5f * 90f, 3f * (1f - land / 0.5f) + 1f, ChPale.WithAlpha(1f - land / 0.5f), 36);
            }
            float lift = (1f - drop * drop) * ChSky;
            DrawChaliceCup(c + new V2(0f, -lift), ChScale, a, level, fx.T);
        }

        /// The cup, standing on its base at `at` (s: its size), seen from above and a little to the front.
        void DrawChaliceCup(V2 at, float s, float a, float level, float t)
        {
            Ellipse(at + new V2(0f, 2f * s), 22f * s, 8f * s, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 20);  // its shadow
            Ellipse(at, 20f * s, 7f * s, 0f, ChDeep.WithAlpha(a), 20);  // the base
            Quad(at + new V2(-4f * s, 0f), at + new V2(-4f * s, -20f * s), at + new V2(4f * s, -20f * s), at + new V2(4f * s, 0f), ChGold.WithAlpha(a));  // the stem
            Ellipse(at + new V2(0f, -11f * s), 6f * s, 2.5f * s, 0f, ChBowl.WithAlpha(a), 10);  // the knop
            V2 top = at + new V2(0f, -40f * s);
            const int n = 8;
            for (int i = 0; i < n; i++)  // the bowl: rounded sides narrowing to the stem
            {
                float u0 = i / (float)n, u1 = (i + 1) / (float)n;
                float w0 = 26f * s * Mathf.Sqrt(1f - u0 * u0 * 0.85f), w1 = 26f * s * Mathf.Sqrt(1f - u1 * u1 * 0.85f);
                if (i == n - 1)
                    w1 = 4f * s;
                float y0 = top.Y + u0 * 22f * s, y1 = top.Y + u1 * 22f * s;
                Quad(new V2(at.X - w0, y0), new V2(at.X - w1, y1), new V2(at.X + w1, y1), new V2(at.X + w0, y0), ChGold.WithAlpha(a));
            }
            Line(top + new V2(-22f * s, 6f * s), top + new V2(-12f * s, 18f * s), 1.6f * s, ChPale.WithAlpha(0.7f * a));  // a shine on the bowl
            Circle(at + new V2(0f, -24f * s), 3.2f * s, ChRuby.WithAlpha(a), 10);  // the ruby
            Circle(at + new V2(-0.8f * s, -24.8f * s), 1.1f * s, ChPale.WithAlpha(0.8f * a), 6);
            Ellipse(top, 26f * s, 8f * s, 0f, ChBowl.WithAlpha(a), 24);  // the rim
            EllipseLine(top, 26f * s, 8f * s, 1.4f * s, ChPale.WithAlpha(0.9f * a), 24);
            if (level > 0.01f)
            {
                float k = 0.35f + 0.65f * level, wave = 1f + 0.04f * Mathf.Sin(t * 4f);
                Ellipse(top + new V2(0f, (1f - level) * 2f * s), 22f * s * k * wave, 6f * s * k, 0f, ChMint.WithAlpha(0.92f * a), 20);
                Ellipse(top + new V2(-5f * s * k, -1f * s * k), 7f * s * k, 2f * s * k, 0f, new Rgba(0.85f, 1f, 0.9f, 0.6f * a), 10);
            }
            if (fxOn && level > 0.01f)
                for (int i = 0; i < 6; i++)  // sparkles rising off the liquid
                {
                    float ph = (t * 0.7f + i / 6f) % 1f;
                    V2 m = top + new V2((i - 2.5f) * 6f * s + Mathf.Sin(t * 3f + i) * 3f, -ph * 46f * s);
                    Circle(m, 1.5f * s, ChMint.WithAlpha(a * (1f - ph)), 6);
                }
        }

        /// A player drinking: a mint stream from the cup to them, then a ring and motes off them.
        void DrawChaliceSip(Effect fx)
        {
            if (fx.T > ChSipT)
                return;
            Player q;
            if (!game.Players.TryGetValue((int)fx.Radius, out q))
                return;
            float k = fx.T / ChSipT;
            V2 from = fx.Pos + new V2(0f, -40f * ChScale), to = q.Disp;
            if (fx.T < 0.35f)
            {
                float u = fx.T / 0.35f;
                for (int i = 0; i < 6; i++)
                {
                    float v0 = i / 6f, v1 = (i + 1) / 6f;
                    if (v1 > u + 0.2f)
                        break;
                    V2 p0 = V2.Lerp(from, to, v0) + new V2(0f, -Mathf.Sin(v0 * MathX.Pi) * 30f), p1 = V2.Lerp(from, to, v1) + new V2(0f, -Mathf.Sin(v1 * MathX.Pi) * 30f);
                    Line(p0, p1, 3f, ChMint.WithAlpha(0.8f * (1f - u * 0.5f)));
                }
            }
            Arc(to, 18f + k * 34f, 3f * (1f - k) + 0.8f, ChMint.WithAlpha(1f - k), 28);
            if (fxOn)
                for (int i = 0; i < 10; i++)  // motes rising off the drinker
                {
                    float h = CavHash((int)fx.Radius * 31 + i);
                    V2 m = to + new V2((h - 0.5f) * 40f, 6f - k * (30f + 30f * CavHash(i * 7 + 3)));
                    Circle(m, 1.6f, new Rgba(0.8f, 1f, 0.88f, 1f - k), 6);
                }
        }

        void DrawChaliceGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.ChaliceOfLife)
                {
                    float level, a = ChaliceAlpha(fx, out level);
                    float drop = Mathf.Clamp01(fx.T / Game.ChDropT);
                    V2 cup = fx.Pos + new V2(0f, -(1f - drop * drop) * ChSky - 30f * ChScale);
                    GlowDisc(cup, 90f, ChGold, (0.35f + 0.25f * level) * a);
                }
                else if (fx.Kind == FxKind.ChaliceSip && fx.T < 0.6f)
                {
                    Player q;
                    if (game.Players.TryGetValue((int)fx.Radius, out q))
                        GlowDisc(q.Disp, 60f, ChMint, 0.6f * (1f - fx.T / 0.6f));
                }
            }
        }
    }
}
