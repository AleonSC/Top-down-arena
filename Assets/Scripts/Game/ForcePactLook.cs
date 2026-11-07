using UnityEngine;

namespace Arena
{
    /// Force Pact (Core/ForcePact.cs), the Soul Chains look from Docs/Mockups/force-pact.html. A violet chain of links flies from the
    /// caster to the enemy and four cuffs close on it while its colour drains to grey and violet floods in (FxKind.PactBind; the
    /// enemy is already out of the wave, so this is a ghost of it). The thrall keeps a sagging chain to its owner, a broken
    /// shackle ring turning round it, and its enemy type's shape and colour. When it falls the chain snaps and shards fly home
    /// to the owner (FxKind.PactSnap); the owner's feet show a ring of links filling as the respawn runs (Player.PactRez), and the
    /// thrall is thrown back out on a chain (FxKind.PactRise). A thrall replaced by a new pact dissolves into motes (PactDissolve).
    public partial class ArenaApp
    {
        static readonly Rgba FpViolet = new Rgba(0.75f, 0.55f, 1f), FpGray = new Rgba(0.6f, 0.62f, 0.66f);

        static float FpSmooth(float u) => u * u * (3f - 2f * u);

        /// A chain from a to b sagging by `slack` pixels, its links alternating wide and narrow.
        void FpChain(V2 a, V2 b, float slack, float step, Rgba c)
        {
            float len = (b - a).Length;
            int n = Mathf.Max(2, (int)(len / step));
            V2 ctrl = (a + b) * 0.5f + new V2(0f, slack * 2f);
            V2 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n, v = 1f - u;
                V2 pt = a * (v * v) + ctrl * (2f * v * u) + b * (u * u);
                V2 d = pt - prev, mid = (pt + prev) * 0.5f;
                if (d.Length > 0.01f)
                {
                    V2 dir = d.Normalized(), side = dir.Orthogonal();
                    if (i % 2 == 0)  // a wide link seen face on: two rails
                    {
                        Line(mid - dir * (step * 0.5f) + side * 3f, mid + dir * (step * 0.5f) + side * 3f, 1.6f, c);
                        Line(mid - dir * (step * 0.5f) - side * 3f, mid + dir * (step * 0.5f) - side * 3f, 1.6f, c);
                    }
                    else  // a narrow one seen edge on
                        Line(mid - dir * (step * 0.32f), mid + dir * (step * 0.32f), 3.4f, c);
                }
                prev = pt;
            }
        }

        /// A thrall: its enemy's shape, a chain to its owner and a broken shackle ring turning round it.
        void DrawThrall(Minion m, float t)
        {
            var T = Defs.Get((EnemyType)m.Pact);
            float r = T.Radius;
            Player owner;
            if (game.Players.TryGetValue(m.Owner, out owner) && !owner.Downed)
            {
                float d = (m.Disp - owner.Disp).Length;
                FpChain(owner.Disp, m.Disp, Mathf.Clamp01(1f - d / 300f) * 46f + 4f, 11f, FpViolet.WithAlpha(0.75f));
            }
            GlowDisc(m.Disp, r + 12f, FpViolet, 0.16f);
            Polygon(m.Disp, r, T.Sides, m.Aim, T.Color);
            Circle(m.Disp, r * 0.42f, FpViolet.WithAlpha(0.45f), 12);
            float ring = r + 12f, rot = t * 1.6f;
            for (int i = 0; i < 3; i++)
            {
                float a0 = rot + i * MathX.Tau / 3f;
                Arc(m.Disp, ring, 3.5f, FpViolet.WithAlpha(0.95f), 10, a0, 1.25f);
                Circle(m.Disp + V2.FromAngle(a0 + 1.25f) * ring, 2.4f, FpViolet.WithAlpha(0.6f), 6);
            }
            if (m.GuardFrac < 0.999f)
                Bar(m.Disp + new V2(-18f, -r - 14f), 36f, 3.5f, Mathf.Clamp01(m.GuardFrac), FpViolet);
        }

        /// The ring of links at a player's feet that fills while a fallen thrall is on its way back.
        void DrawPactTimer(Player p, float t)
        {
            if (p.PactRez == 0 || p.Downed)
                return;
            float k = p.PactRez / 255f;
            const int N = 16;
            for (int i = 0; i < N; i++)
            {
                float a = -MathX.Pi / 2f + i * MathX.Tau / N;
                bool lit = (i + 0.5f) / N <= k;
                V2 c = p.Disp + V2.FromAngle(a) * 40f, tan = V2.FromAngle(a + MathX.Pi / 2f);
                float half = i % 2 == 0 ? 5f : 3.2f;
                Rgba col = lit ? FpViolet.WithAlpha(0.95f) : FpViolet.WithAlpha(0.28f);
                Line(c - tan * half, c + tan * half, i % 2 == 0 ? 2.4f : 3.6f, col);
            }
        }

        /// FxKind.PactBind: chain out to the enemy, cuffs closing, its colour draining to violet.
        void DrawPactBindFx(Effect fx, float f, float t)
        {
            var T = Defs.Get((EnemyType)Mathf.RoundToInt(fx.Radius));
            V2 a = fx.Pos, b = fx.Pos2;
            Rgba body = f < 0.5f ? T.Color.Mix(FpGray, f * 2f) : FpGray.Mix(FpViolet, (f - 0.5f) * 2f);
            float shake = (1f - Mathf.Abs(f - 0.5f) * 2f) * 2.2f;
            V2 at = b + new V2(CavHash((int)(t * 40f)) - 0.5f, CavHash((int)(t * 40f) + 7) - 0.5f) * shake;
            GlowDisc(at, T.Radius + 12f, body, 0.18f);
            Polygon(at, T.Radius, T.Sides, MathX.Pi / 2f, body);
            float ext = FpSmooth(Mathf.Clamp01(f / 0.4f)), fade = Mathf.Clamp01((1f - f) / 0.2f);
            FpChain(a, V2.Lerp(a, b, ext), (1f - ext) * 26f, 12f, FpViolet.WithAlpha(0.95f * fade));
            if (f > 0.35f)
            {
                float k = FpSmooth(Mathf.Clamp01((f - 0.35f) / 0.55f)), rr = Mathf.Lerp(T.Radius + 34f, T.Radius + 5f, k);
                for (int i = 0; i < 4; i++)
                    Arc(b, rr, 4f, FpViolet.WithAlpha(0.95f * fade), 10, i * MathX.Tau / 4f + k * 1.2f + 0.15f, 1.05f);
                if (CavHash((int)(t * 20f)) < 0.5f)
                    GlowDisc(b + V2.FromAngle(CavHash((int)(t * 20f) + 3) * MathX.Tau) * T.Radius, 3f, FpViolet, 0.9f);
            }
        }

        /// FxKind.PactSnap: the chain's links scatter and the thrall's shards are hauled home to its owner.
        void DrawPactSnapFx(Effect fx, float f)
        {
            var T = Defs.Get((EnemyType)Mathf.RoundToInt(fx.Radius));
            V2 a = fx.Pos, b = fx.Pos2, mid = (a + b) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                float h = CavHash(i * 3 + (int)a.X);
                V2 p = mid + new V2((h - 0.5f) * 120f * f, -30f * f + 180f * f * f * (0.4f + h)) + (b - a) * ((CavHash(i + 40) - 0.5f) * 0.4f);
                V2 dir = V2.FromAngle(h * MathX.Tau + f * 6f);
                Line(p - dir * 4f, p + dir * 4f, 3f, FpViolet.WithAlpha(1f - f));
            }
            float pull = FpSmooth(Mathf.Clamp01(f * 1.2f));
            for (int i = 0; i < 14; i++)
            {
                float h = CavHash(i * 5 + (int)a.Y);
                V2 start = a + V2.FromAngle(h * MathX.Tau) * (10f + 50f * f * (0.4f + h));
                V2 p = V2.Lerp(start, b, pull * pull);
                Polygon(p, 4f * (1f - f * 0.6f), 3, f * 8f + i, i % 2 == 0 ? FpViolet.WithAlpha(1f - f * 0.5f) : T.Color.WithAlpha(1f - f * 0.5f));
            }
            Arc(a, T.Radius + 80f * f, 3f, T.Color.WithAlpha(1f - f), 28);
        }

        /// FxKind.PactDissolve: the old thrall's motes stream to the new pact's spot.
        void DrawPactDissolveFx(Effect fx, float f)
        {
            var T = Defs.Get((EnemyType)Mathf.RoundToInt(fx.Radius));
            V2 a = fx.Pos, b = fx.Pos2;
            for (int i = 0; i < 18; i++)
            {
                float h = CavHash(i * 7 + (int)a.X);
                V2 start = a + V2.FromAngle(h * MathX.Tau) * (14f + 40f * h) * (1f - f);
                float u = Mathf.Clamp01(f * (0.8f + h * 0.4f));
                GlowDisc(V2.Lerp(start, b, u * u), 3.5f * (1f - f * 0.4f), i % 3 == 0 ? T.Color : FpViolet, 0.95f * (1f - f * f));
            }
            Arc(a, T.Radius + 60f * f, 2.5f, FpViolet.WithAlpha(1f - f), 24);
        }

        /// FxKind.PactRise: a chain thrown out from the owner to where the thrall comes back.
        void DrawPactRiseFx(Effect fx, float f)
        {
            V2 a = fx.Pos, b = fx.Pos2;
            float ext = FpSmooth(Mathf.Clamp01(f / 0.5f)), fade = Mathf.Clamp01((1f - f) / 0.4f);
            FpChain(a, V2.Lerp(a, b, ext), (1f - ext) * 20f, 11f, FpViolet.WithAlpha(0.9f * fade));
            Arc(a, 20f + 80f * f, 3f, FpViolet.WithAlpha(1f - f), 24);
            Arc(b, 10f + 60f * f, 3f, FpViolet.WithAlpha(1f - f), 24);
        }
    }
}
