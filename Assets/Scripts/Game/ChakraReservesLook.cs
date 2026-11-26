using UnityEngine;

namespace Arena
{
    /// Chakra Reserves (Core/ChakraReserves.cs), from the mockup (https://claude.ai/artifact/TRfxeCuY27ZcNHay8sJYrJ):
    ///   - the cast (`FxKind.ChakraThread`): a jade thread with a violet strand wound through it runs from the Ninja's palms to
    ///     the teammate (it follows them), then thins away; a lotus of eight petals opens under the teammate with a ring
    ///     racing out. When it refreshed their cooldowns, three violet seals spin up off their head.
    ///   - the Chakra Surge (Player.CrSurgeT): a jade ring under the feet that empties as it runs out, and a jade wake with
    ///     speed lines behind them.
    public partial class ArenaApp
    {
        static readonly Rgba CrJade = new Rgba(0.37f, 0.95f, 0.82f), CrViolet = new Rgba(0.71f, 0.55f, 1f), CrPale = new Rgba(0.88f, 1f, 0.97f);

        void DrawChakraThreadFx(Effect fx, float t)
        {
            float age = fx.T, dur = Mathf.Max(0.05f, fx.Dur);
            int id = (int)fx.Radius;
            bool refreshed = fx.Radius - id > 0.25f;
            V2 to = fx.Pos2;
            Player q;
            if (game != null && game.Players.TryGetValue(id, out q))
                to = q.Disp;
            V2 from = fx.Pos;

            // The thread: grows out to them, holds, then thins away.
            float reach = Mathf.Clamp01(age / Game.CrFxReach), fade = 1f - Mathf.Clamp01((age - Game.CrFxReach) / 0.35f);
            if (fade > 0f && from.DistanceSq(to) > 4f)
            {
                V2 d = to - from;
                V2 side = d.Normalized().Orthogonal();
                const int n = 22;
                for (int strand = 0; strand < 2; strand++)
                {
                    V2 last = from;
                    for (int i = 1; i <= n; i++)
                    {
                        float u = i / (float)n * reach;
                        float wob = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(u * 14f + t * 12f + strand * Mathf.PI) * 10f;
                        V2 at = from + d * u + side * wob;
                        if (strand == 0)
                        {
                            Line(last, at, 7f, CrJade.WithAlpha(0.18f * fade));
                            Line(last, at, 3f, CrJade.WithAlpha(0.9f * fade));
                        }
                        else
                            Line(last, at, 1.6f, CrViolet.WithAlpha(0.8f * fade));
                        last = at;
                    }
                }
                if (reach < 1f)
                    Circle(from + d * reach, 5f, CrPale.WithAlpha(fade), 10);  // the head of the thread
            }

            // The lotus and the ring, once the thread lands.
            float k = Mathf.Clamp01((age - Game.CrFxReach) / (dur - Game.CrFxReach));
            if (age < Game.CrFxReach)
                return;
            float open = 1f - (1f - k) * (1f - k) * (1f - k), a = 1f - k;
            float r = 22f + 40f * open;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f + t * 0.6f;
                V2 dir = V2.FromAngle(ang);
                Rgba col = (i % 2 == 0 ? CrJade : CrViolet).WithAlpha(0.5f * a);
                for (int s = 0; s < 4; s++)  // a petal: discs shrinking toward its tip
                    Circle(to + dir * (r * (0.3f + s * 0.18f)), r * (0.2f - s * 0.04f), col, 10);
            }
            Arc(to, 26f + 110f * open, 2.5f, CrJade.WithAlpha(0.8f * a), 40);
            if (!refreshed || fxReduced)
                return;
            for (int i = 0; i < 3; i++)  // the refreshed cooldowns: three seals spinning up off the head
            {
                V2 at = to + new V2((i - 1) * 16f, -34f - 26f * open);
                for (int e = 0; e < 6; e++)
                {
                    float b0 = e * Mathf.PI / 3f + t * 5f, b1 = b0 + Mathf.PI / 3f;
                    Line(at + V2.FromAngle(b0) * 6f, at + V2.FromAngle(b1) * 6f, 1.6f, CrViolet.WithAlpha(a));
                }
            }
        }

        /// Under a player in a Chakra Surge: the ring of its time left and the jade wake.
        void DrawChakraSurge(Player p, float t)
        {
            if (p.Downed || p.CrSurgeT <= 0f)
                return;
            float left = Mathf.Clamp01(p.CrSurgeT / Mathf.Max(0.01f, p.CrSurgeMax));
            float fade = Mathf.Clamp01(p.CrSurgeT / 0.3f);
            float r = p.Def.BodyR + 9f;
            Arc(p.Disp, r, 1.4f, CrJade.WithAlpha(0.2f * fade), 36);
            Arc(p.Disp, r, 3f, CrJade.WithAlpha(0.85f * fade), 36, -Mathf.PI / 2f, MathX.Tau * left);
            DrawDashWake(p, t, CrJade, CrPale, fade, V2.FromAngle(p.Aim + MathX.Pi));
        }
    }
}
